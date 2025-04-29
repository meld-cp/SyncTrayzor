using System;

namespace SyncTrayzor.Syncthing.EventWatcher
{
    public class DeviceResumedEventArgs : EventArgs
    {
        public string DeviceId { get; }

        public DeviceResumedEventArgs(string deviceId)
        {
            DeviceId = deviceId;
        }
    }
}
