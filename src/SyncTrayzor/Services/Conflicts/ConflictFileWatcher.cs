using NLog;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTrayzor.Services.Conflicts
{
    public interface IConflictFileWatcher : IDisposable
    {
        bool IsEnabled { get; set; }
        List<string> ConflictedFiles { get; }

        TimeSpan BackoffInterval { get; set; }
        TimeSpan FolderExistenceCheckingInterval { get; set; }

        event EventHandler ConflictedFilesChanged;
    }

    public class ConflictFileWatcher : IConflictFileWatcher
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string versionsFolder = ".stversions";

        private readonly ISyncthingManager syncthingManager;
        private readonly IConflictFileManager conflictFileManager;
        private readonly IFileWatcherFactory fileWatcherFactory;

        // Locks both conflictedFiles and conflictFileOptions
        private readonly object conflictFileRecordsLock = new();

        // Contains all of the unique conflicted files, resolved from conflictFileOptions
        private List<string> conflictedFiles = new();

        // Contains all of the .sync-conflict files found
        private readonly HashSet<string> conflictFileOptions = new();

        private readonly object fileWatchersLock = new();
        private readonly List<FileWatcher> fileWatchers = new();

        private readonly SemaphoreSlim scanLock = new(1, 1);
        private CancellationTokenSource scanCts;

        private readonly object backoffTimerLock = new();
        private readonly System.Timers.Timer backoffTimer;

        public List<string> ConflictedFiles
        {
            get
            {
                lock (conflictFileRecordsLock)
                {
                    return conflictedFiles.ToList();
                }
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                Reset();
            }
        }

        public TimeSpan BackoffInterval { get; set; } =  TimeSpan.FromSeconds(10); // Need a default here

        public TimeSpan FolderExistenceCheckingInterval { get; set; }

        public event EventHandler ConflictedFilesChanged;

        public ConflictFileWatcher(
            ISyncthingManager syncthingManager,
            IConflictFileManager conflictFileManager,
            IFileWatcherFactory fileWatcherFactory)
        {
            this.syncthingManager = syncthingManager;
            this.conflictFileManager = conflictFileManager;
            this.fileWatcherFactory = fileWatcherFactory;

            this.syncthingManager.StateChanged += SyncthingStateChanged;
            this.syncthingManager.Folders.FoldersChanged += FoldersChanged;

            backoffTimer = new System.Timers.Timer() // Interval will be set when it's started
            {
                AutoReset = false,
            };
            backoffTimer.Elapsed += (o, e) =>
            {
                RefreshConflictedFiles();
            };
        }

        private void SyncthingStateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            Reset();
        }

        private void FoldersChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void RestartBackoffTimer()
        {
            lock (backoffTimerLock)
            {
                backoffTimer.Stop();
                backoffTimer.Interval = BackoffInterval.TotalMilliseconds;
                backoffTimer.Start();
            }
        }

        private async void Reset()
        {
            StopWatchers();

            if (IsEnabled && syncthingManager.State == SyncthingState.Running)
            {
                var folders = syncthingManager.Folders.FetchAll();

                StartWatchers(folders);
                await ScanFoldersAsync(folders);
            }
            else
            {
                lock (conflictFileRecordsLock)
                {
                    conflictFileOptions.Clear();
                }
                RefreshConflictedFiles();
            }
        }
        
        private void RefreshConflictedFiles()
        {
            var conflictFiles = new HashSet<string>();

            lock (conflictFileRecordsLock)
            {
                foreach (var conflictedFile in conflictFileOptions)
                {
                    if (conflictFileManager.TryParseConflictFile(conflictedFile, out var parsedConflictFileInfo))
                    {
                        conflictFiles.Add(parsedConflictFileInfo.OriginalPath);
                    }
                }

                conflictedFiles = conflictFiles.ToList();

                logger.Debug($"Refreshing conflicted files. Found {conflictedFiles.Count} from {conflictFileOptions.Count} options");
            }

            ConflictedFilesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void StopWatchers()
        {
            lock (fileWatchersLock)
            {
                foreach (var watcher in fileWatchers)
                {
                    watcher.Dispose();
                }

                fileWatchers.Clear();
            }
        }

        private void StartWatchers(IReadOnlyCollection<Folder> folders)
        {
            lock (fileWatchersLock)
            {
                foreach (var folder in folders)
                {
                    logger.Debug("Starting watcher for folder: {0} ({1})", folder.FolderId, folder.Label);

                    var watcher = fileWatcherFactory.Create(FileWatcherMode.CreatedOrDeleted, folder.Path, FolderExistenceCheckingInterval, conflictFileManager.ConflictPattern);
                    watcher.PathChanged += PathChanged;
                    fileWatchers.Add(watcher);
                }
            }
        }

        private void PathChanged(object sender, PathChangedEventArgs e)
        {
            var fullPath = Path.Combine(e.Directory, e.Path);

            if (conflictFileManager.IsPathIgnored(fullPath) || conflictFileManager.IsFileIgnored(fullPath))
                return;

            logger.Debug("Conflict file changed: {0} FileExists: {1}", fullPath, e.PathExists);

            bool changed;

            lock (conflictFileRecordsLock)
            {
                if (e.PathExists)
                    changed = conflictFileOptions.Add(fullPath);
                else
                    changed = conflictFileOptions.Remove(fullPath);
            }

            if (changed)
                RestartBackoffTimer();
        }

        private async Task ScanFoldersAsync(IReadOnlyCollection<Folder> folders)
        {
            if (folders.Count == 0)
                return;

            // We're not re-entrant. There's a CTS which will abort the previous invocation, but we'll need to wait
            // until that happens
            scanCts?.Cancel();
            using (await scanLock.WaitAsyncDisposable())
            {
                scanCts = new CancellationTokenSource();
                try
                {
                    var newConflictFileOptions = new HashSet<string>();

                    foreach (var folder in folders)
                    {
                        logger.Debug("Scanning folder {0} ({1}) ({2}) for conflict files", folder.FolderId, folder.Label, folder.Path);

                        var options = await conflictFileManager.FindConflicts(folder.Path)
                            .SelectMany(conflict => conflict.Conflicts)
                            .Select(conflictOptions => Path.Combine(folder.Path, conflictOptions.FilePath))
                            .ToList()
                            .ToTask(scanCts.Token);

                        newConflictFileOptions.UnionWith(options);
                    }

                    // If we get aborted, we won't refresh the conflicted files: it'll get done again in a minute anyway
                    bool conflictedFilesChanged;
                    lock (conflictFileRecordsLock)
                    {
                        conflictedFilesChanged = !conflictFileOptions.SetEquals(newConflictFileOptions);
                        if (conflictedFilesChanged)
                        {
                            conflictFileOptions.Clear();
                            foreach (var file in newConflictFileOptions)
                            {
                                conflictFileOptions.Add(file);
                            }
                        }
                    }

                    if (conflictedFilesChanged)
                        RestartBackoffTimer();

                }
                catch (OperationCanceledException) { }
                catch (AggregateException e) when (e.InnerException is OperationCanceledException) { }
                finally
                {
                    scanCts = null;
                }
            }
        }

        public void Dispose()
        {
            StopWatchers();
            syncthingManager.StateChanged -= SyncthingStateChanged;
            syncthingManager.Folders.FoldersChanged -= FoldersChanged;
            backoffTimer.Stop();
            backoffTimer.Dispose();
        }
    }
}
