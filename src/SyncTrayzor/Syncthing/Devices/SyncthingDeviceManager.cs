using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SyncTrayzor.Syncthing.EventWatcher;
using SyncTrayzor.Syncthing.ApiClient;
using System.Collections.Concurrent;
using NLog;
using System.Threading;
using SyncTrayzor.Utils;

namespace SyncTrayzor.Syncthing.Devices
{
    public interface ISyncthingDeviceManager
    {
        event EventHandler<DeviceConnectedEventArgs> DeviceConnected;
        event EventHandler<DeviceDisconnectedEventArgs> DeviceDisconnected;
        event EventHandler<DevicePausedEventArgs> DevicePaused;
        event EventHandler<DeviceResumedEventArgs> DeviceResumed;

        bool TryFetchById(string deviceId, out Device device);
        IReadOnlyCollection<Device> FetchDevices();

        Task PauseDeviceAsync(Device device);
        Task ResumeDeviceAsync(Device device);
    }

    public class SyncthingDeviceManager : ISyncthingDeviceManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly SynchronizedEventDispatcher eventDispatcher;
        private readonly SynchronizedTransientWrapper<ISyncthingApiClient> apiClient;
        private readonly ISyncthingEventWatcher eventWatcher;
        private readonly ISyncthingCapabilities capabilities;

        private readonly object devicesLock = new();
        private ConcurrentDictionary<string, Device> _devices = new();
        public ConcurrentDictionary<string, Device> devices
        {
            get { lock (devicesLock) { return _devices; } }
            set { lock (devicesLock) _devices = value; }
        }

        public event EventHandler<DeviceConnectedEventArgs> DeviceConnected;
        public event EventHandler<DeviceDisconnectedEventArgs> DeviceDisconnected;
        public event EventHandler<DevicePausedEventArgs> DevicePaused;
        public event EventHandler<DeviceResumedEventArgs> DeviceResumed;

        public SyncthingDeviceManager(SynchronizedTransientWrapper<ISyncthingApiClient> apiClient, ISyncthingEventWatcher eventWatcher, ISyncthingCapabilities capabilities)
        {
            eventDispatcher = new SynchronizedEventDispatcher(this);
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.eventWatcher = eventWatcher ?? throw new ArgumentNullException(nameof(eventWatcher));
            this.capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));

            this.eventWatcher.DeviceConnected += EventDeviceConnected;
            this.eventWatcher.DeviceDisconnected += EventDeviceDisconnected;
            this.eventWatcher.DevicePaused += EventDevicePaused;
            this.eventWatcher.DeviceResumed += EventDeviceResumed;
        }

        public bool TryFetchById(string deviceId, out Device device)
        {
            return devices.TryGetValue(deviceId, out device);
        }

        public IReadOnlyCollection<Device> FetchDevices()
        {
            return new List<Device>(devices.Values).AsReadOnly();
        }

        public async Task LoadDevicesAsync(Config config, CancellationToken cancellationToken)
        {
            var devices = await FetchDevicesAsync(config, cancellationToken);
            this.devices = new ConcurrentDictionary<string, Device>(devices.Select(x => new KeyValuePair<string, Device>(x.DeviceId, x)));
        }

        public async Task ReloadDevicesAsync(Config config, CancellationToken cancellationToken)
        {
            // Raise events as appropriate

            var devices = await FetchDevicesAsync(config, cancellationToken);
            var newDevices = new ConcurrentDictionary<string, Device>();
            var changeNotifications = new List<Action>();

            foreach (var device in devices)
            {
                if (this.devices.TryGetValue(device.DeviceId, out var existingDevice))
                {
                    if (!existingDevice.IsConnected && device.IsConnected)
                        changeNotifications.Add(() => OnDeviceConnected(device));
                    else if (existingDevice.IsConnected && !device.IsConnected)
                        changeNotifications.Add(() => OnDeviceDisconnected(device));
                }

                newDevices[device.DeviceId] = device;
            }

            this.devices = newDevices;
            foreach (var changeNotification in changeNotifications)
            {
                changeNotification();
            }
        }

        private async Task<IEnumerable<Device>> FetchDevicesAsync(Config config, CancellationToken cancellationToken)
        {
            var connections = await apiClient.Value.FetchConnectionsAsync(cancellationToken);
            // We can potentially see duplicate devices (if the user set their config file that way). Ignore them.
            var devices = Enumerable.DistinctBy(config.Devices, x => x.DeviceID).Select(device =>
            {
                var deviceObj = new Device(device.DeviceID, device.Name);
                if (connections.DeviceConnections.TryGetValue(device.DeviceID, out var connectionData))
                {
                    if (connectionData.Connected && connectionData.Address != null)
                        deviceObj.SetConnected(SyncthingAddressParser.Parse(connectionData.Address));
                    if (connectionData.Paused)
                        deviceObj.SetPaused();
                }
                return deviceObj;
            });

            cancellationToken.ThrowIfCancellationRequested();

            return devices;
        }
        
        public async Task PauseDeviceAsync(Device device)
        {
            if (!capabilities.SupportsDevicePauseResume)
                throw new InvalidOperationException("Syncthing version does not support device pause and resume");

            var client = apiClient.Value;
            if (client == null)
                throw new InvalidOperationException("Client is not connected");

            device.SetPaused();
            await client.PauseDeviceAsync(device.DeviceId);
        }

        public async Task ResumeDeviceAsync(Device device)
        {
            if (!capabilities.SupportsDevicePauseResume)
                throw new InvalidOperationException("Syncthing version does not support device pause and resume");

            var client = apiClient.Value;
            if (client == null)
                throw new InvalidOperationException("Client is not connected");

            device.SetResumed();
            await client.ResumeDeviceAsync(device.DeviceId);
        }

        private void EventDeviceConnected(object sender, EventWatcher.DeviceConnectedEventArgs e)
        {
            if (!devices.TryGetValue(e.DeviceId, out var device))
            {
                logger.Warn("Unexpected device connected: {0}, address {1}. It wasn't fetched when we fetched our config", e.DeviceId, e.Address);
                return; // Not expecting this device! It wasn't in the config...
            }

            device.SetConnected(SyncthingAddressParser.Parse(e.Address));

            OnDeviceConnected(device);
        }

        private void EventDeviceDisconnected(object sender, EventWatcher.DeviceDisconnectedEventArgs e)
        {
            if (!devices.TryGetValue(e.DeviceId, out var device))
            {
                logger.Warn("Unexpected device connected: {0}, error {1}. It wasn't fetched when we fetched our config", e.DeviceId, e.Error);
                return; // Not expecting this device! It wasn't in the config...
            }

            device.SetDisconnected();

            OnDeviceDisconnected(device);
        }

        private void EventDevicePaused(object sender, EventWatcher.DevicePausedEventArgs e)
        {
            if (!devices.TryGetValue(e.DeviceId, out var device))
            {
                logger.Warn("Unexpected device paused: {0}. It wasn't fetched when we fetched our config", e.DeviceId);
                return; // Not expecting this device! It wasn't in the config...
            }

            device.SetPaused();

            OnDevicePaused(device);
        }

        private void EventDeviceResumed(object sender, EventWatcher.DeviceResumedEventArgs e)
        {
            if (!devices.TryGetValue(e.DeviceId, out var device))
            {
                logger.Warn("Unexpected device resumed: {0}. It wasn't fetched when we fetched our config", e.DeviceId);
                return; // Not expecting this device! It wasn't in the config...
            }

            device.SetResumed();

            OnDeviceResumed(device);
        }

        private void OnDeviceConnected(Device device)
        {
            eventDispatcher.Raise(DeviceConnected, new DeviceConnectedEventArgs(device));
        }

        private void OnDeviceDisconnected(Device device)
        {
            eventDispatcher.Raise(DeviceDisconnected, new DeviceDisconnectedEventArgs(device));
        }

        private void OnDevicePaused(Device device)
        {
            eventDispatcher.Raise(DevicePaused, new DevicePausedEventArgs(device));
        }

        private void OnDeviceResumed(Device device)
        {
            eventDispatcher.Raise(DeviceResumed, new DeviceResumedEventArgs(device));
        }
    }
}
