using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SyncTrayzor.Syncthing.Devices;

namespace SyncTrayzor.NotifyIcon
{
    public interface IConnectedEventDebouncer
    {
        event EventHandler<DeviceConnectedEventArgs> DeviceConnected;

        void Connect(Device device);
        bool Disconnect(Device device);
    }

    public class ConnectedEventDebouncer : IConnectedEventDebouncer
    {
        private static readonly TimeSpan debounceTime = TimeSpan.FromSeconds(10);

        private readonly object syncRoot = new();

        // Devices for which we've seen a connected event, but haven't yet generated a
        // Connected notification, and the CTS to cancel the timer which will signal the 
        // DeviceConnected event being fired
        private readonly Dictionary<Device, CancellationTokenSource> pendingDeviceIds = new();

        public event EventHandler<DeviceConnectedEventArgs> DeviceConnected;

        public void Connect(Device device)
        {
            var cts = new CancellationTokenSource();

            lock (syncRoot)
            {
                if (pendingDeviceIds.TryGetValue(device, out var existingCts))
                {
                    // It already exists. Cancel it, restart.
                    existingCts.Cancel();
                }

                pendingDeviceIds[device] = cts;
            }

            WaitAndRaiseConnected(device, cts.Token);
        }

        private async void WaitAndRaiseConnected(Device device, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(debounceTime, cancellationToken);
            }
            catch (OperationCanceledException) { }

            bool raiseEvent = false;

            lock (syncRoot)
            {
                if (pendingDeviceIds.ContainsKey(device))
                {
                    pendingDeviceIds.Remove(device);
                    raiseEvent = true;
                }
            }

            if (raiseEvent)
            {
                DeviceConnected?.Invoke(this, new DeviceConnectedEventArgs(device));
            }
        }


        public bool Disconnect(Device device)
        {
            lock (syncRoot)
            {
                if (pendingDeviceIds.TryGetValue(device, out var cts))
                {
                    cts.Cancel();
                    pendingDeviceIds.Remove(device);

                    return false;
                }

                return true;
            }
        }
    }
}
