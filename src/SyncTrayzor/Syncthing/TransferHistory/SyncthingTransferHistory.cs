using NLog;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.EventWatcher;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SyncTrayzor.Syncthing.TransferHistory
{
    public interface ISyncthingTransferHistory : IDisposable
    {
        event EventHandler<FileTransferChangedEventArgs> TransferStateChanged;
        event EventHandler<FileTransferChangedEventArgs> TransferStarted;
        event EventHandler<FileTransferChangedEventArgs> TransferCompleted;
        event EventHandler<FolderSynchronizationFinishedEventArgs> FolderSynchronizationFinished;

        IEnumerable<FileTransfer> CompletedTransfers { get; }
        IEnumerable<FileTransfer> InProgressTransfers { get; }
        IEnumerable<FailingTransfer> FailingTransfers { get; }
    }

    public class SyncthingTransferHistory : ISyncthingTransferHistory
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly Logger downloadLogger = LogManager.GetLogger("DownloadLog");

        private readonly ISyncthingEventWatcher eventWatcher;
        private readonly ISyncthingFolderManager folderManager;
        private readonly SynchronizedEventDispatcher eventDispatcher;

        private const int maxCompletedTransfers = 100;

        // Locks both completedTransfers, inProgressTransfers, and recentlySynchronized
        private readonly object transfersLock = new();

        // It's a queue because we limit its length
        private readonly Queue<FileTransfer> completedTransfers = new();

        private readonly Dictionary<FolderPathKey, FileTransfer> inProgressTransfers = new();
        private readonly Dictionary<FolderPathKey, FailingTransfer> currentlyFailingTransfers = new();

        // Collection of stuff synchronized recently. Keyed on folder. Cleared when that folder finished synchronizing
        private readonly Dictionary<string, List<FileTransfer>> recentlySynchronized = new();

        public event EventHandler<FileTransferChangedEventArgs> TransferStateChanged;
        public event EventHandler<FileTransferChangedEventArgs> TransferStarted;
        public event EventHandler<FileTransferChangedEventArgs> TransferCompleted;
        public event EventHandler<FolderSynchronizationFinishedEventArgs> FolderSynchronizationFinished;

        public IEnumerable<FileTransfer> CompletedTransfers
        {
            get
            {
                lock (transfersLock)
                {
                    return completedTransfers.ToArray();
                }
            }
        }

        public IEnumerable<FileTransfer> InProgressTransfers
        {
            get
            {
                lock (transfersLock)
                {
                    return inProgressTransfers.Values.ToArray();
                }
            }
        }

        public IEnumerable<FailingTransfer> FailingTransfers
        {
            get
            {
                lock (transfersLock)
                {
                    return currentlyFailingTransfers.Values.ToArray();
                }
            }
        }

        public SyncthingTransferHistory(ISyncthingEventWatcher eventWatcher, ISyncthingFolderManager folderManager)
        {
            eventDispatcher = new SynchronizedEventDispatcher(this);

            this.eventWatcher = eventWatcher;
            this.folderManager = folderManager;

            this.eventWatcher.ItemStarted += ItemStarted;
            this.eventWatcher.ItemFinished += ItemFinished;
            this.eventWatcher.ItemDownloadProgressChanged += ItemDownloadProgressChanged;

            // We can't use the EventWatcher to watch for folder sync state change events: events could be skipped.
            // The folder manager knows how to listen to skipped event notifications, and refresh the folder states appropriately
            this.folderManager.SyncStateChanged += SyncStateChanged;
        }

        private FileTransfer FetchOrInsertInProgressFileTransfer(string folderId, string path, ItemChangedItemType itemType, ItemChangedActionType actionType)
        {
            var key = new FolderPathKey(folderId, path);
            bool created = false;
            FileTransfer fileTransfer;
            lock (transfersLock)
            {
                if (!inProgressTransfers.TryGetValue(key, out fileTransfer) &&
                    folderManager.TryFetchById(folderId, out var folder))
                {
                    created = true;
                    fileTransfer = new FileTransfer(folder, path, itemType, actionType);
                    logger.Debug("Created file transfer: {0}", fileTransfer);
                    inProgressTransfers.Add(key, fileTransfer);
                }
            }

            if (created)
                OnTransferStarted(fileTransfer);

            return fileTransfer;
        }

        private void ItemStarted(object sender, ItemStartedEventArgs e)
        {
            logger.Debug("Item started. Folder: {0}, Item: {1}, Type: {2}, Action: {3}", e.Folder, e.Item, e.ItemType, e.Action);
            // We only care about files or folders - no metadata please!
            if ((e.ItemType != ItemChangedItemType.File && e.ItemType != ItemChangedItemType.Dir) ||
                (e.Action != ItemChangedActionType.Update && e.Action != ItemChangedActionType.Delete))
            {
                return;
            }

            FetchOrInsertInProgressFileTransfer(e.Folder, e.Item, e.ItemType, e.Action);
        }

        private void ItemFinished(object sender, ItemFinishedEventArgs e)
        {
            // Folder,Path,Type,Action,Error
            downloadLogger.Info($"{e.Folder},{e.Item},{e.ItemType},{e.Action},{e.Error}");
            logger.Debug("Item finished. Folder: {0}, Item: {1}, Type: {2}, Action: {3}", e.Folder, e.Item, e.ItemType, e.Action);

            if ((e.ItemType != ItemChangedItemType.File && e.ItemType != ItemChangedItemType.Dir) ||
                (e.Action != ItemChangedActionType.Update && e.Action != ItemChangedActionType.Delete))
            {
                return;
            }

            // It *should* be in the 'in progress transfers'...
            FileTransfer fileTransfer;
            lock (transfersLock)
            {
                fileTransfer = FetchOrInsertInProgressFileTransfer(e.Folder, e.Item, e.ItemType, e.Action);
                // If it wasn't, and we couldn't create it, fileTransfer is null
                if (fileTransfer != null)
                {
                    CompleteFileTransfer(fileTransfer, e.Error);
                }
            }

            if (fileTransfer != null)
            {
                OnTransferStateChanged(fileTransfer);
                OnTransferCompleted(fileTransfer);
            }
        }

        private void CompleteFileTransfer(FileTransfer fileTransfer, string error)
        {
            // This is always called from within a lock, but you can't be too sure...
            lock (transfersLock)
            {
                var key = new FolderPathKey(fileTransfer.Folder.FolderId, fileTransfer.Path);

                bool isNewError = false;
                if (error == null)
                {
                    currentlyFailingTransfers.Remove(key);
                }
                else
                {
                    if (!currentlyFailingTransfers.TryGetValue(key, out var failingTransfer) || failingTransfer.Error != error)
                    {
                        // Remove will only do something in the case that the failure existed, but the error changed
                        currentlyFailingTransfers.Remove(key);
                        currentlyFailingTransfers.Add(key, new FailingTransfer(fileTransfer.Folder.FolderId, fileTransfer.Path, error));
                        isNewError = true;
                    }
                }

                fileTransfer.SetComplete(error, isNewError);
                inProgressTransfers.Remove(key);

                logger.Debug("File Transfer set to complete: {0}", fileTransfer);

                completedTransfers.Enqueue(fileTransfer);
                if (completedTransfers.Count > maxCompletedTransfers)
                    completedTransfers.Dequeue();

                if (!recentlySynchronized.TryGetValue(fileTransfer.Folder.FolderId, out var recentlySynchronizedList))
                {
                    recentlySynchronizedList = new List<FileTransfer>();
                    recentlySynchronized[fileTransfer.Folder.FolderId] = recentlySynchronizedList;
                }
                recentlySynchronizedList.Add(fileTransfer);
            }
        }

        private void ItemDownloadProgressChanged(object sender, ItemDownloadProgressChangedEventArgs e)
        {
            logger.Debug("Item progress changed. Folder: {0}, Item: {1}", e.Folder, e.Item);

            // If we didn't see the started event, tough. We don't have enough information to re-create it...
            var key = new FolderPathKey(e.Folder, e.Item);
            FileTransfer fileTransfer;
            lock (transfersLock)
            {
                if (!inProgressTransfers.TryGetValue(key, out fileTransfer))
                    return; // Nothing we can do...

                fileTransfer.SetDownloadProgress(e.BytesDone, e.BytesTotal);
                logger.Debug("File transfer progress changed: {0}", fileTransfer);
            }

            OnTransferStateChanged(fileTransfer);
        }

        private void SyncStateChanged(object sender, FolderSyncStateChangedEventArgs e)
        {
            var folderId = e.FolderId;

            if (e.PrevSyncState == FolderSyncState.Syncing)
            {
                List<FileTransfer> transferredList = null;
                List<FileTransfer> completedFileTransfers; // Those that Syncthing didn't tell us had completed

                lock (transfersLock)
                {
                    // Syncthing may not have told us that a file has completed, because it can forget events.
                    // Therefore mark everything in this folder as having completed
                    completedFileTransfers = inProgressTransfers.Where(x => x.Key.Folder == folderId).Select(x => x.Value).ToList();
                    foreach (var completedFileTransfer in completedFileTransfers)
                    {
                        CompleteFileTransfer(completedFileTransfer, error: null);
                    }

                    if (recentlySynchronized.TryGetValue(folderId, out transferredList))
                        recentlySynchronized.Remove(folderId);
                }

                foreach (var fileTransfer in completedFileTransfers)
                {
                    OnTransferStateChanged(fileTransfer);
                    OnTransferCompleted(fileTransfer);
                }
                OnFolderSynchronizationFinished(folderId, transferredList ?? new List<FileTransfer>());
            }
        }

        private void OnTransferStateChanged(FileTransfer fileTransfer)
        {
            eventDispatcher.Raise(TransferStateChanged, new FileTransferChangedEventArgs(fileTransfer));
        }

        private void OnTransferStarted(FileTransfer fileTransfer)
        {
            eventDispatcher.Raise(TransferStarted, new FileTransferChangedEventArgs(fileTransfer));
        }

        private void OnTransferCompleted(FileTransfer fileTransfer)
        {
            eventDispatcher.Raise(TransferCompleted, new FileTransferChangedEventArgs(fileTransfer));
        }

        private void OnFolderSynchronizationFinished(string folderId, List<FileTransfer> fileTransfers)
        {
            if (!folderManager.TryFetchById(folderId, out var folder))
                return;

            eventDispatcher.Raise(FolderSynchronizationFinished, new FolderSynchronizationFinishedEventArgs(folder, fileTransfers));
        }

        public void Dispose()
        {
            eventWatcher.ItemStarted -= ItemStarted;
            eventWatcher.ItemFinished -= ItemFinished;
            eventWatcher.ItemDownloadProgressChanged -= ItemDownloadProgressChanged;
            folderManager.SyncStateChanged -= SyncStateChanged;
        }

        private struct FolderPathKey : IEquatable<FolderPathKey>
        {
            public readonly string Folder;
            public readonly string Path;

            public FolderPathKey(string folder, string path)
            {
                Folder = folder;
                Path = path;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Folder.GetHashCode();
                    hash = hash * 31 + Path.GetHashCode();
                    return hash;
                }
            }

            public override bool Equals(object obj)
            {
                return (obj is FolderPathKey) && Equals((FolderPathKey)obj);
            }

            public bool Equals(FolderPathKey other)
            {
                return Folder == other.Folder && Path == other.Path;
            }
        }
    }
}
