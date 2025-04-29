using System;

namespace SyncTrayzor.Syncthing.EventWatcher
{
    public class SyncStateChangedEventArgs : EventArgs
    {
        public string FolderId { get; }
        public string PrevSyncState { get; }
        public string SyncState { get; }

        public SyncStateChangedEventArgs(string folderId, string prevSyncState, string syncState)
        {
            FolderId = folderId;
            PrevSyncState = prevSyncState;
            SyncState = syncState;
        }
    }
}
