using System;

namespace SyncTrayzor.Syncthing.Devices
{
    public class DevicePausedEventArgs : EventArgs
    {
        public Device Device { get; }

        public DevicePausedEventArgs(Device device)
        {
            Device = device;
        }
    }
}
