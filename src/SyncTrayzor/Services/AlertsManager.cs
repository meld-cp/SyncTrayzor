using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.Services.Metering;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SyncTrayzor.Services
{
    public interface IAlertsManager : IDisposable
    {
        event EventHandler AlertsStateChanged;
        bool AnyWarnings { get; }

        bool EnableFailedTransferAlerts { get; set; }
        bool EnableConflictedFileAlerts { get; set; }

        IReadOnlyList<string> ConflictedFiles { get; }

        IReadOnlyList<string> FoldersWithFailedTransferFiles { get; }

        IReadOnlyList<string> PausedDeviceIdsFromMetering { get; }
    }

    public class AlertsManager : IAlertsManager
    {
        private readonly ISyncthingManager syncthingManager;
        private readonly IConflictFileWatcher conflictFileWatcher;
        private readonly IMeteredNetworkManager meteredNetworkManager;
        private readonly SynchronizedEventDispatcher eventDispatcher;

        public bool AnyWarnings => ConflictedFiles.Count > 0 || FoldersWithFailedTransferFiles.Count > 0;


        private IReadOnlyList<string> _conflictedFiles = EmptyReadOnlyList<string>.Instance;
        public IReadOnlyList<string> ConflictedFiles => _enableConflictedFileAlerts ? _conflictedFiles : EmptyReadOnlyList<string>.Instance;

        private IReadOnlyList<string> _foldersWithFailedTransferFiles = EmptyReadOnlyList<string>.Instance;
        public IReadOnlyList<string> FoldersWithFailedTransferFiles => _enableFailedTransferAlerts ? _foldersWithFailedTransferFiles : EmptyReadOnlyList<string>.Instance;

        public IReadOnlyList<string> PausedDeviceIdsFromMetering { get; private set; } = EmptyReadOnlyList<string>.Instance;

        public event EventHandler AlertsStateChanged;

        private bool _enableFailedTransferAlerts;
        public bool EnableFailedTransferAlerts
        {
            get => _enableFailedTransferAlerts;
            set
            {
                if (_enableFailedTransferAlerts == value)
                    return;
                _enableFailedTransferAlerts = value;
                OnAlertsStateChanged();
            }
        }

        private bool _enableConflictedFileAlerts;
        public bool EnableConflictedFileAlerts
        {
            get => _enableConflictedFileAlerts;
            set
            {
                if (_enableConflictedFileAlerts == value)
                    return;
                _enableConflictedFileAlerts = value;
                OnAlertsStateChanged();
            }
        }

        public AlertsManager(ISyncthingManager syncthingManager, IConflictFileWatcher conflictFileWatcher, IMeteredNetworkManager meteredNetworkManager)
        {
            this.syncthingManager = syncthingManager;
            this.conflictFileWatcher = conflictFileWatcher;
            this.meteredNetworkManager = meteredNetworkManager;
            eventDispatcher = new SynchronizedEventDispatcher(this);

            this.syncthingManager.Folders.FolderErrorsChanged += FolderErrorsChanged;

            this.conflictFileWatcher.ConflictedFilesChanged += ConflictFilesChanged;

            this.meteredNetworkManager.PausedDevicesChanged += PausedDevicesChanged;
        }

        private void OnAlertsStateChanged()
        {
            eventDispatcher.Raise(AlertsStateChanged);
        }

        private void FolderErrorsChanged(object sender, FolderErrorsChangedEventArgs e)
        {
            var folders = syncthingManager.Folders.FetchAll();
            _foldersWithFailedTransferFiles = folders.Where(x => x.FolderErrors.Any()).Select(x => x.Label).ToList().AsReadOnly();
 
            OnAlertsStateChanged();
        }

        private void ConflictFilesChanged(object sender, EventArgs e)
        {
            _conflictedFiles = conflictFileWatcher.ConflictedFiles.ToList().AsReadOnly();

            OnAlertsStateChanged();
        }

        private void PausedDevicesChanged(object sender, EventArgs e)
        {
            PausedDeviceIdsFromMetering = meteredNetworkManager.PausedDevices.Select(x => x.DeviceId).ToList().AsReadOnly();

            OnAlertsStateChanged();
        }

        public void Dispose()
        {
            syncthingManager.Folders.FolderErrorsChanged -= FolderErrorsChanged;
            conflictFileWatcher.ConflictedFilesChanged -= ConflictFilesChanged;
            meteredNetworkManager.PausedDevicesChanged -= PausedDevicesChanged;
        }

        private static class EmptyReadOnlyList<T>
        {
            public static readonly IReadOnlyList<T> Instance = new List<T>().AsReadOnly();
        }
    }
}
