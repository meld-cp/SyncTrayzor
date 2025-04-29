using System;

namespace SyncTrayzor.Syncthing.Devices
{
    public class DeviceConnectedEventArgs : EventArgs
    {
        public Device Device { get; }

        public DeviceConnectedEventArgs(Device device)
        {
            Device = device;
        }
    }
}
