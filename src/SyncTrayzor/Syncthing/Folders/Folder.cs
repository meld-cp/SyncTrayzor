using SyncTrayzor.Syncthing.ApiClient;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SyncTrayzor.Syncthing.Folders
{
    public class Folder : IEquatable<Folder>
    {
        private readonly object syncRoot = new();

        public string FolderId { get; }
        public string Label { get; }
        public string Path { get; }

        public bool IsFsWatcherEnabled { get; }

        private FolderSyncState _syncState;
        public FolderSyncState SyncState
        {
            get { lock (syncRoot) { return _syncState; } }
            set { lock (syncRoot) { _syncState = value; } }
        }

        private HashSet<string> syncingPaths { get; set; }

        private FolderStatus _status;
        public FolderStatus Status
        {
            get { lock(syncRoot) { return _status; } }
            set { lock(syncRoot) { _status = value; } }
        }

        private IReadOnlyList<FolderError> _folderErrors;
        public IReadOnlyList<FolderError> FolderErrors
        {
            get { lock(syncRoot) { return _folderErrors; } }
            private set { lock(syncRoot) { _folderErrors = value; } }
        }

        public Folder(string folderId, string label, string path, bool isFsWatcherEnabled, FolderSyncState syncState, FolderStatus status)
        {
            FolderId = folderId;
            Label = String.IsNullOrWhiteSpace(label) ? folderId : label;
            Path = path;
            IsFsWatcherEnabled = isFsWatcherEnabled;
            SyncState = syncState;
            syncingPaths = new HashSet<string>();
            _status = status;
            FolderErrors = new List<FolderError>().AsReadOnly();
        }

        public bool IsSyncingPath(string path)
        {
            lock (syncRoot)
            {
                return syncingPaths.Contains(path);
            }
        }

        public void AddSyncingPath(string path)
        {
            lock (syncRoot)
            {
                syncingPaths.Add(path);
            }
        }

        public void RemoveSyncingPath(string path)
        {
            lock (syncRoot)
            {
                syncingPaths.Remove(path);
            }
        }

        public void SetFolderErrors(IEnumerable<FolderError> folderErrors)
        {
            FolderErrors = folderErrors.ToList().AsReadOnly();
        }

        public void ClearFolderErrors()
        {
            FolderErrors = new List<FolderError>().AsReadOnly();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Folder);
        }

        public bool Equals(Folder other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (ReferenceEquals(other, null))
                return false;

            lock (syncRoot)
            {
                return FolderId == other.FolderId &&
                    Label == other.Label &&
                    Path == other.Path &&
                    IsFsWatcherEnabled == other.IsFsWatcherEnabled && 
                    SyncState == other.SyncState &&
                    Status == other.Status &&
                    FolderErrors.SequenceEqual(other.FolderErrors) &&
                    syncingPaths.SetEquals(other.syncingPaths);
            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                lock (syncRoot)
                {
                    int hash = 17;
                    hash = hash * 23 + FolderId.GetHashCode();
                    hash = hash * 23 + Label.GetHashCode();
                    hash = hash * 23 + IsFsWatcherEnabled.GetHashCode();
                    hash = hash * 23 + SyncState.GetHashCode();
                    hash = hash * 23 + Status.GetHashCode();
                    hash = hash * 23 + syncingPaths.GetHashCode();
                    foreach (var folderError in FolderErrors)
                    {
                        hash = hash * 23 + folderError.GetHashCode();
                    }
                    foreach (var syncingPath in syncingPaths)
                    {
                        hash = hash * 23 + syncingPath.GetHashCode();
                    }
                    return hash;
                }
            }
        }
    }
}
