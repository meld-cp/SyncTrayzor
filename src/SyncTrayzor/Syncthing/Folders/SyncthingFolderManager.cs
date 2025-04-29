using NLog;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.EventWatcher;
using SyncTrayzor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Syncthing.Folders
{
    public interface ISyncthingFolderManager
    {
        bool TryFetchById(string folderId, out Folder folder);
        IReadOnlyCollection<Folder> FetchAll();

        event EventHandler FoldersChanged;
        event EventHandler<FolderSyncStateChangedEventArgs> SyncStateChanged;
        event EventHandler<FolderStatusChangedEventArgs> StatusChanged;
        event EventHandler<FolderErrorsChangedEventArgs> FolderErrorsChanged;
    }

    public class SyncthingFolderManager : ISyncthingFolderManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string uncPrefix = @"\\?\";

        private readonly SynchronizedEventDispatcher eventDispatcher;

        private readonly SynchronizedTransientWrapper<ISyncthingApiClient> apiClient;
        private readonly ISyncthingEventWatcher eventWatcher;

        public event EventHandler FoldersChanged;
        public event EventHandler<FolderSyncStateChangedEventArgs> SyncStateChanged;
        public event EventHandler<FolderStatusChangedEventArgs> StatusChanged;
        public event EventHandler<FolderErrorsChangedEventArgs> FolderErrorsChanged;

        // Folders is a ConcurrentDictionary, which suffices for most access
        // However, it is sometimes set outright (in the case of an initial load or refresh), so we need this lock
        // to create a memory barrier. The lock is only used when setting/fetching the field, not when accessing the
        // Folders dictionary itself.
        private readonly object foldersLock = new();
        private ConcurrentDictionary<string, Folder> _folders = new();
        private ConcurrentDictionary<string, Folder> folders
        {
            get { lock (foldersLock) { return _folders; } }
            set { lock (foldersLock) { _folders = value; } }
        }

        public SyncthingFolderManager(
            SynchronizedTransientWrapper<ISyncthingApiClient> apiClient,
            ISyncthingEventWatcher eventWatcher)
        {
            eventDispatcher = new SynchronizedEventDispatcher(this);
            this.apiClient = apiClient;

            this.eventWatcher = eventWatcher;
            this.eventWatcher.SyncStateChanged += (o, e) => FolderSyncStateChanged(e);
            this.eventWatcher.FolderStatusChanged += (o, e) => FolderStatusChanged(e.FolderId, e.FolderStatus);
            this.eventWatcher.ItemStarted += (o, e) => ItemStarted(e.Folder, e.Item);
            this.eventWatcher.ItemFinished += (o, e) => ItemFinished(e.Folder, e.Item);
            this.eventWatcher.FolderErrorsChanged += (o, e) => FolderErrorsChangedEvt(e.FolderId, e.Errors);
        }

        public bool TryFetchById(string folderId, out Folder folder)
        {
            var folders = this.folders;
            if (folders == null)
            {
                folder = null;
                return false;
            }
            else
            {
                return folders.TryGetValue(folderId, out folder);
            }
        }

        public IReadOnlyCollection<Folder> FetchAll()
        {
            var folders = this.folders;
            return folders == null ? null : new List<Folder>(folders.Values).AsReadOnly();
        }

        public async Task LoadFoldersAsync(Config config, string tilde, CancellationToken cancellationToken)
        {
            var folders = await FetchFoldersAsync(config, tilde, cancellationToken);
            this.folders = new ConcurrentDictionary<string, Folder>(folders.Select(x => new KeyValuePair<string, Folder>(x.FolderId, x)));

            OnFoldersChanged();
        }

        public async Task ReloadFoldersAsync(Config config, string tilde, CancellationToken cancellationToken)
        {
            var folders = await FetchFoldersAsync(config, tilde, cancellationToken);
            var newFolders = new ConcurrentDictionary<string, Folder>();
            var existingFolders = this.folders;

            // Maybe nothing changed?
            if (new HashSet<Folder>(existingFolders.Values).SetEquals(folders))
                return;

            var changeNotifications = new List<Action>();

            // Re-use the existing folder object if possible
            foreach (var folder in folders)
            {
                if (existingFolders.TryGetValue(folder.FolderId, out var existingFolder))
                {
                    if (existingFolder.SyncState != folder.SyncState)
                    {
                        changeNotifications.Add(() => OnSyncStateChanged(folder, existingFolder.SyncState, folder.SyncState));
                        // A sync state change always implies that the status has changed, since the two go together
                        changeNotifications.Add(() => OnStatusChanged(folder, folder.Status));
                        existingFolder.SyncState = folder.SyncState;
                    }
                    // Things like the label may have changed, so need to use the new folder instance
                    newFolders[folder.FolderId] = folder;
                }
                else
                {
                    newFolders[folder.FolderId] = folder;
                }
            }

            this.folders = newFolders;
            foreach (var changeNotification in changeNotifications)
            {
                changeNotification();
            }

            OnFoldersChanged();
        }

        private async Task<IEnumerable<Folder>> FetchFoldersAsync(Config config, string tilde, CancellationToken cancellationToken)
        {
            // If the folder is invalid for any reason, we'll ignore it.
            // Again, there's the potential for duplicate folder IDs (if the user's been fiddling their config). 
            // In this case, there's nothing really sensible we can do. Just pick one of them :)
            var folderConstructionTasks = config.Folders
                .Where(x => String.IsNullOrWhiteSpace(x.Invalid))
                .DistinctBy(x => x.ID)
                .Select(async folder =>
                {
                    var status = await FetchFolderStatusAsync(folder.ID, cancellationToken);
                    var syncState = FolderStateTransformer.SyncStateFromString(status.State);

                    var path = folder.Path;
                    // Strip off UNC prefix, if they're put it on
                    if (path.StartsWith(uncPrefix))
                        path = path.Substring(uncPrefix.Length);
                    // Change forward slashes to backslashes
                    path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    if (path.StartsWith("~"))
                        path = Path.Combine(tilde, path.Substring(1).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                    return new Folder(folder.ID, folder.Label, path, folder.IsFsWatcherEnabled, syncState, status);
                });

            cancellationToken.ThrowIfCancellationRequested();

            var folders = await Task.WhenAll(folderConstructionTasks);
            var actualFolders = folders.Where(x => x != null);
            return actualFolders;
        }

        private async Task<FolderStatus> FetchFolderStatusAsync(string folderId, CancellationToken cancellationToken)
        {
            var status = await apiClient.Value.FetchFolderStatusAsync(folderId, cancellationToken);
            return status;
        }

        private void ItemStarted(string folderId, string item)
        {
            if (!folders.TryGetValue(folderId, out var folder))
                return; // Don't know about it

            folder.AddSyncingPath(item);
        }

        private void ItemFinished(string folderId, string item)
        {
            if (!folders.TryGetValue(folderId, out var folder))
                return; // Don't know about it

            folder.RemoveSyncingPath(item);
        }

        private void FolderErrorsChangedEvt(string folderId, List<FolderErrorData> errors)
        {
            if (!folders.TryGetValue(folderId, out var folder))
                return; // Don't know about it

            var folderErrors = errors.Select(x => new FolderError(x.Error, x.Path)).ToList();
            folder.SetFolderErrors(folderErrors);
            OnFolderErrorsChanged(folder, folderErrors);
        }

        private void FolderSyncStateChanged(SyncStateChangedEventArgs e)
        {
            if (!folders.TryGetValue(e.FolderId, out var folder))
                return; // We don't know about this folder

            var syncState = FolderStateTransformer.SyncStateFromString(e.SyncState);
            folder.SyncState = syncState;

            if (syncState == FolderSyncState.Syncing)
            {
                folder.ClearFolderErrors();
                OnFolderErrorsChanged(folder, new List<FolderError>());
            }

            OnSyncStateChanged(folder, FolderStateTransformer.SyncStateFromString(e.PrevSyncState), syncState);
        }

        private void FolderStatusChanged(string folderId, FolderStatus folderStatus)
        {
            if (!folders.TryGetValue(folderId, out var folder))
                return; // Don't know about it

            folder.Status = folderStatus;

            OnStatusChanged(folder, folderStatus);
        }

        private void OnFoldersChanged()
        {
            eventDispatcher.Raise(FoldersChanged);
        }

        private void OnSyncStateChanged(Folder folder, FolderSyncState prevSyncState, FolderSyncState newSyncState)
        {
            eventDispatcher.Raise(SyncStateChanged, new FolderSyncStateChangedEventArgs(folder.FolderId, prevSyncState, newSyncState));
        }

        private void OnStatusChanged(Folder folder, FolderStatus folderStatus)
        {
            eventDispatcher.Raise(StatusChanged, new FolderStatusChangedEventArgs(folder.FolderId, folderStatus));
        }

        private void OnFolderErrorsChanged(Folder folder, List<FolderError> folderErrors)
        {
            eventDispatcher.Raise(FolderErrorsChanged, new FolderErrorsChangedEventArgs(folder.FolderId, folderErrors));
        }
    }
}
