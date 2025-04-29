using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.Devices;

namespace SyncTrayzor.Services.Metering
{
    // Device state: Unpaused, Paused, UnpausedRenegade, PausedRenegade
    // If it's renegade, don't transition it.
    // Event decide device needs pausing: Unpaused -> Paused
    // Event decide device needs unpausing: Paused -> Unpaused
    // Event device paused: Unpaused -> PausedRenegade, UnpausedRenegade -> Paused
    // Event device resumed: Paused -> UnpausedRenegade, PausedRenegade -> Unpaused

    public interface IMeteredNetworkManager : IDisposable
    {
        event EventHandler PausedDevicesChanged;

        bool IsEnabled { get; set; }
        bool IsSupportedByWindows { get; }

        IReadOnlyList<Device> PausedDevices { get; }
    }

    public class MeteredNetworkManager : IMeteredNetworkManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ISyncthingManager syncthingManager;
        private readonly INetworkCostManager costManager;

        public event EventHandler PausedDevicesChanged;

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;
                _isEnabled = value;
                if (value)
                    Enable();
                else
                    Disable();
            }
        }

        public bool IsSupportedByWindows => costManager.IsSupported;

        private readonly object syncRoot = new();

        public IReadOnlyList<Device> PausedDevices { get; private set; } = new List<Device>().AsReadOnly();

        private readonly Dictionary<Device, DeviceState> deviceStates = new();

        public MeteredNetworkManager(ISyncthingManager syncthingManager, INetworkCostManager costManager)
        {
            this.syncthingManager = syncthingManager;
            this.costManager = costManager;

            // We won't know whether or not Syncthing supports this until it loads
            if (this.costManager.IsSupported)
            {
                this.syncthingManager.StateChanged += SyncthingStateChanged;
                this.syncthingManager.DataLoaded += DataLoaded;
                this.syncthingManager.Devices.DevicePaused += DevicePaused;
                this.syncthingManager.Devices.DeviceResumed += DeviceResumed;
                this.syncthingManager.Devices.DeviceConnected += DeviceConnected;
                this.syncthingManager.Devices.DeviceDisconnected += DeviceDisconnected;
                this.costManager.NetworkCostsChanged += NetworkCostsChanged;
                this.costManager.NetworksChanged += NetworksChanged;
            }
        }

        private void SyncthingStateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            if (e.NewState != SyncthingState.Running)
                ClearAllDevices();
            // Else, we'll get DataLoaded shortly
        }

        private void DataLoaded(object sender, EventArgs e)
        {
            if (!IsEnabled)
                return;

            ClearAllDevices();

            Update();
        }

        private void ClearAllDevices()
        {
            bool changed;
            lock (syncRoot)
            {
                changed = deviceStates.Values.Any(x => x == DeviceState.Paused);
                deviceStates.Clear();
            }

            if (changed)
                UpdatePausedDeviceIds();
        }

        private void DevicePaused(object sender, DevicePausedEventArgs e)
        {
            if (!IsEnabled)
                return;

            bool changed = false;
            lock (syncRoot)
            {
                if (!deviceStates.TryGetValue(e.Device, out var deviceState))
                {
                    logger.Warn($"Unable to pause device {e.Device.DeviceId} as we don't have a record of its state. This should not happen");
                    return;
                }

                if (deviceState == DeviceState.Unpaused)
                {
                    deviceStates[e.Device] = DeviceState.PausedRenegade;
                    logger.Debug($"Device {e.Device.DeviceId} has been paused, and has gone renegade");
                }
                else if (deviceState == DeviceState.UnpausedRenegade)
                {
                    deviceStates[e.Device] = DeviceState.Paused;
                    logger.Debug($"Device {e.Device.DeviceId} has been paused, and has stopped being renegade");
                    changed = true;
                }
            }

            if (changed)
                UpdatePausedDeviceIds();
        }

        private void DeviceResumed(object sender, DeviceResumedEventArgs e)
        {
            if (!IsEnabled)
                return;

            bool changed = false;
            lock (syncRoot)
            {
                if (!deviceStates.TryGetValue(e.Device, out var deviceState))
                {
                    logger.Warn($"Unable to resume device {e.Device.DeviceId} as we don't have a record of its state. This should not happen");
                    return;
                }

                if (deviceState == DeviceState.Paused)
                {
                    deviceStates[e.Device] = DeviceState.UnpausedRenegade;
                    logger.Debug($"Device {e.Device.DeviceId} has been resumed, and has gone renegade");
                    changed = true;
                }
                else if (deviceState == DeviceState.PausedRenegade)
                {
                    deviceStates[e.Device] = DeviceState.Unpaused;
                    logger.Debug($"Device {e.Device.DeviceId} has been resumed, and has stopped being renegade");
                }
            }

            if (changed)
                UpdatePausedDeviceIds();
        }

        private async void DeviceConnected(object sender, DeviceConnectedEventArgs e)
        {
            if (!IsEnabled)
                return;

            var changed = await UpdateDeviceAsync(e.Device);
            if (changed)
                UpdatePausedDeviceIds();
        }

        private void DeviceDisconnected(object sender, DeviceDisconnectedEventArgs e)
        {
            // Not sure what to do here - this is caused by the pausing. We can't unpause it otherwise
            // we'll get stuck in a cycle of connected/disconnected.
        }

        private void NetworkCostsChanged(object sender, EventArgs e)
        {
            if (!IsEnabled)
                return;

            logger.Debug("Network costs changed. Updating devices");
            ResetRenegades();
            Update();
        }

        private void NetworksChanged(object sender, EventArgs e)
        {
            if (!IsEnabled)
                return;

            logger.Debug("Networks changed. Updating devices");
            ResetRenegades();
            Update();
        }

        private void ResetRenegades()
        {
            lock (syncRoot)
            {
                foreach (var kvp in deviceStates.ToArray())
                {
                    if (kvp.Value == DeviceState.PausedRenegade)
                        deviceStates[kvp.Key] = DeviceState.Paused;
                    else if (kvp.Value == DeviceState.UnpausedRenegade)
                        deviceStates[kvp.Key] = DeviceState.Unpaused;
                }
            }
        }

        private void UpdatePausedDeviceIds()
        {
            lock (syncRoot)
            {
                PausedDevices = deviceStates.Where(x => x.Value == DeviceState.Paused).Select(x => x.Key).ToList().AsReadOnly();
            }

            PausedDevicesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Enable()
        {
            Update();
        }

        private async void Disable()
        {
            List<Device> devicesToUnpause;
            lock (syncRoot)
            {
                devicesToUnpause = deviceStates.Where(x => x.Value == DeviceState.Paused).Select(x => x.Key).ToList();
            }

            ClearAllDevices();

            if (syncthingManager.State == SyncthingState.Running)
                await Task.WhenAll(devicesToUnpause.Select(x => syncthingManager.Devices.ResumeDeviceAsync(x)).ToList());
        }

        private async void Update()
        {
            var devices = syncthingManager.Devices.FetchDevices();

            // Keep device states in sync with devices
            lock (syncRoot)
            {
                foreach (var device in devices)
                {
                    if (!deviceStates.ContainsKey(device))
                        deviceStates[device] = device.Paused ? DeviceState.Paused : DeviceState.Unpaused;
                }
                var deviceIds = new HashSet<string>(devices.Select(x => x.DeviceId));
                foreach (var deviceState in deviceStates.Keys.ToList())
                {
                    if (!deviceIds.Contains(deviceState.DeviceId))
                        deviceStates.Remove(deviceState);
                }
            }

            var updateTasks = devices.Select(device => UpdateDeviceAsync(device));
            var results = await Task.WhenAll(updateTasks);

            if (results.Any())
                UpdatePausedDeviceIds();
        }

        private async Task<bool> UpdateDeviceAsync(Device device)
        {
            // This is called when the list of devices changes, when the network cost changes, or when a device connects
            // If the list of devices has changed, then the device won't be renegade

            if (!IsEnabled || syncthingManager.State != SyncthingState.Running || !syncthingManager.Capabilities.SupportsDevicePauseResume)
                return false;

            DeviceState deviceState;
            lock (syncRoot)
            {
                if (!deviceStates.TryGetValue(device, out deviceState))
                {
                    logger.Warn($"Unable to fetch device state for device ID {device.DeviceId}. This should not happen.");
                    return false;
                }
            }

            if (deviceState == DeviceState.PausedRenegade || deviceState == DeviceState.UnpausedRenegade)
            {
                logger.Debug($"Skipping update of device {device.DeviceId} as it has gone renegade");
                return false;
            }

            // The device can become disconnected at any point....
            var deviceAddress = device.Address;
            var shouldBePaused = device.IsConnected && deviceAddress != null && await costManager.IsConnectionMetered(deviceAddress.Address);

            bool changed = false;

            if (shouldBePaused && !device.Paused)
            {
                logger.Debug($"Pausing device {device.DeviceId}");
                try
                {
                    await syncthingManager.Devices.PauseDeviceAsync(device);

                    lock (syncRoot)
                    {
                        deviceStates[device] = DeviceState.Paused;
                    }
                    changed = true;
                }
                catch (Exception e)
                {
                    // Could be that the client is not connected, or that this specific request fails
                    logger.Warn($"Could not pause device {device.DeviceId}: {e.Message}");
                }
            }
            else if (!shouldBePaused && device.Paused)
            {
                logger.Debug($"Resuming device {device.DeviceId}");
                try
                {
                    await syncthingManager.Devices.ResumeDeviceAsync(device);

                    lock (syncRoot)
                    {
                        deviceStates[device] = DeviceState.Unpaused;
                    }
                    changed = true;
                }
                catch (Exception e)
                {
                    // Could be that the client is not connected, or that this specific request fails
                    logger.Warn($"Could not resume device {device.DeviceId}: {e.Message}");
                }
            }

            return changed;
        }

        public void Dispose()
        {
            syncthingManager.StateChanged -= SyncthingStateChanged;
            syncthingManager.DataLoaded -= DataLoaded;
            syncthingManager.Devices.DevicePaused -= DevicePaused;
            syncthingManager.Devices.DeviceResumed -= DeviceResumed;
            syncthingManager.Devices.DeviceConnected -= DeviceConnected;
            syncthingManager.Devices.DeviceDisconnected -= DeviceDisconnected;
            costManager.NetworkCostsChanged -= NetworkCostsChanged;
            costManager.NetworksChanged -= NetworksChanged;
        }

        private enum DeviceState
        {
            Paused,
            Unpaused,
            PausedRenegade,
            UnpausedRenegade,
        }
    }
}
