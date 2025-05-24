using System.Collections.Generic;
using NLog;

namespace SyncTrayzor.Syncthing.Folders
{
    public static class FolderStateTransformer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly Dictionary<string, FolderSyncState> folderSyncStateLookup = new()
        {
            { "syncing", FolderSyncState.Syncing },
            { "sync-preparing", FolderSyncState.Syncing },
            { "scanning", FolderSyncState.Scanning },
            { "cleaning", FolderSyncState.Cleaning },
            { "scan-waiting", FolderSyncState.Waiting },
            { "sync-waiting", FolderSyncState.Waiting },
            { "clean-waiting", FolderSyncState.Waiting },
            { "idle", FolderSyncState.Idle },
            { "error", FolderSyncState.Error },
        };

        public static FolderSyncState SyncStateFromString(string state)
        {
            if (folderSyncStateLookup.TryGetValue(state, out var syncState))
                return syncState;

            logger.Warn($"Unknown folder sync state {state}. Defaulting to Idle");

            // Default
            return FolderSyncState.Idle;
        }
    }
}
