using System;

namespace SyncTrayzor.Syncthing.Folders
{
    public class FolderSyncStateChangedEventArgs : EventArgs
    {
        public string FolderId { get; }
        public FolderSyncState PrevSyncState { get; }
        public FolderSyncState SyncState { get; }

        public FolderSyncStateChangedEventArgs(string folderId, FolderSyncState prevSyncState, FolderSyncState syncState)
        {
            FolderId = folderId;
            PrevSyncState = prevSyncState;
            SyncState = syncState;
        }
    }
}
