using System;

namespace SyncTrayzor.Syncthing.EventWatcher
{
    public class FolderRejectedEventArgs : EventArgs
    {
        public string DeviceId { get; }
        public string FolderId { get; }

        public FolderRejectedEventArgs(string deviceId, string folderId)
        {
            DeviceId = deviceId;
            FolderId = folderId;
        }
    }
}
