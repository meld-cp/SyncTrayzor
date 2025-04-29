using System;

namespace SyncTrayzor.Syncthing.EventWatcher
{
    public class DeviceDisconnectedEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public string Error { get; }

        public DeviceDisconnectedEventArgs(string deviceId, string error)
        {
            DeviceId = deviceId;
            Error = error;
        }
    }
}
