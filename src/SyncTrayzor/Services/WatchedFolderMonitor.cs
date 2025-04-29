using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.Folders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SyncTrayzor.Services
{
    public interface IWatchedFolderMonitor : IDisposable
    {
        IEnumerable<string> WatchedFolderIDs { get; set; }
        TimeSpan BackoffInterval { get; set; }
        TimeSpan FolderExistenceCheckingInterval { get; set; }
    }

    public class WatchedFolderMonitor : IWatchedFolderMonitor
    {
        // Paths we don't alert Syncthing about
        private static readonly string[] specialPaths = new[] { ".stversions", ".stfolder", "~syncthing~", ".syncthing." };

        private readonly ISyncthingManager syncthingManager;
        private readonly IDirectoryWatcherFactory directoryWatcherFactory;

        private readonly List<DirectoryWatcher> directoryWatchers = new();

        private List<string> _watchedFolders;
        public IEnumerable<string> WatchedFolderIDs
        {
            get => _watchedFolders;
            set
            {
                if (_watchedFolders != null && value != null && _watchedFolders.SequenceEqual(value))
                    return;

                _watchedFolders = value?.ToList();
                Reset();
            }
        }

        public TimeSpan BackoffInterval { get; set; }
        public TimeSpan FolderExistenceCheckingInterval { get; set; }

        public WatchedFolderMonitor(ISyncthingManager syncthingManager, IDirectoryWatcherFactory directoryWatcherFactory)
        {
            this.syncthingManager = syncthingManager;
            this.directoryWatcherFactory = directoryWatcherFactory;

            this.syncthingManager.Folders.FoldersChanged += FoldersChanged;
            this.syncthingManager.Folders.SyncStateChanged += FolderSyncStateChanged;
            this.syncthingManager.StateChanged += StateChanged;
        }

        private void FoldersChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void FolderSyncStateChanged(object sender, FolderSyncStateChangedEventArgs e)
        {
            // Don't monitor failed folders, and pick up on unfailed folders
            if (e.SyncState == FolderSyncState.Error || e.PrevSyncState == FolderSyncState.Error)
                Reset();
        }

        private void StateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            Reset();
        }

        private void Reset()
        {
            // Has everything loaded yet?
            if (_watchedFolders == null)
                return;

            foreach (var watcher in directoryWatchers)
            {
                watcher.Dispose();
            }
            directoryWatchers.Clear();

            if (syncthingManager.State != SyncthingState.Running)
                return;

            var folders = syncthingManager.Folders.FetchAll();
            if (folders == null)
                return; // Folders haven't yet loaded

            foreach (var folder in folders)
            {
                // If Syncthing is watching the folder, don't watch it ourselves
                if (!_watchedFolders.Contains(folder.FolderId) || folder.IsFsWatcherEnabled || folder.SyncState == FolderSyncState.Error)
                    continue;

                var watcher = directoryWatcherFactory.Create(folder.Path, BackoffInterval, FolderExistenceCheckingInterval);
                watcher.PreviewDirectoryChanged += (o, e) => e.Cancel = WatcherPreviewDirectoryChanged(folder, e);
                watcher.DirectoryChanged += (o, e) => WatcherDirectoryChanged(folder, e.SubPath);

                directoryWatchers.Add(watcher);
            }
        }

        // Returns true to cancel
        private bool WatcherPreviewDirectoryChanged(Folder folder, PreviewDirectoryChangedEventArgs e)
        {
            var subPath = e.SubPath;

            // Is it a syncthing temp/special path?
            if (specialPaths.Any(x => subPath.StartsWith(x)))
                return true;

            return folder.SyncState == FolderSyncState.Syncing || folder.IsSyncingPath(subPath);
        }

        private void WatcherDirectoryChanged(Folder folder, string subPath)
        {
            // If it's currently syncing, then don't refresh it
            if (folder.SyncState == FolderSyncState.Syncing)
                return;

            syncthingManager.ScanAsync(folder.FolderId, subPath.Replace(Path.DirectorySeparatorChar, '/'));
        }

        public void Dispose()
        {
            syncthingManager.Folders.FoldersChanged -= FoldersChanged;
            syncthingManager.Folders.SyncStateChanged -= FolderSyncStateChanged;
            syncthingManager.StateChanged -= StateChanged;
        }
    }
}
