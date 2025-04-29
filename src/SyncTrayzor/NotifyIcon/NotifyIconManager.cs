using Hardcodet.Wpf.TaskbarNotification;
using Stylet;
using SyncTrayzor.Localization;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.Devices;
using SyncTrayzor.Syncthing.TransferHistory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SyncTrayzor.NotifyIcon
{
    public interface INotifyIconManager : IDisposable
    {
        bool ShowOnlyOnClose { get; set; }
        bool MinimizeToTray { get; set; }
        bool CloseToTray { get; set; }
        Dictionary<string, bool> FolderNotificationsEnabled { get; set; }
        bool ShowSynchronizedBalloonEvenIfNothingDownloaded { get; set; }
        bool ShowDeviceConnectivityBalloons { get; set; }
        bool ShowDeviceOrFolderRejectedBalloons { get; set; }

        void EnsureIconVisible();

        Task<bool?> ShowBalloonAsync(object viewModel, int? timeout = null, CancellationToken? cancellationToken = null);
    }

    public class NotifyIconManager : INotifyIconManager
    {
        // Amount of time to squish 'synced' messages for after a connectivity event
        private static readonly TimeSpan syncedDeadTime = TimeSpan.FromSeconds(10);

        private readonly IViewManager viewManager;
        private readonly NotifyIconViewModel viewModel;
        private readonly IApplicationState application;
        private readonly IApplicationWindowState applicationWindowState;
        private readonly ISyncthingManager syncthingManager;
        private readonly IConnectedEventDebouncer connectedEventDebouncer;

        private TaskbarIcon taskbarIcon;

        private TaskCompletionSource<bool?> balloonTcs;

        private bool _showOnlyOnClose;
        public bool ShowOnlyOnClose
        {
            get => _showOnlyOnClose;
            set
            {
                _showOnlyOnClose = value;
                viewModel.Visible = !_showOnlyOnClose || applicationWindowState.ScreenState == ScreenState.Closed;
            }
        }

        public bool MinimizeToTray { get; set; }

        private bool _closeToTray;
        public bool CloseToTray
        {
            get => _closeToTray;
            set { _closeToTray = value; SetShutdownMode(); }
        }

        // FolderId -> is enabled
        public Dictionary<string, bool> FolderNotificationsEnabled { get; set; }
        public bool ShowSynchronizedBalloonEvenIfNothingDownloaded { get; set; }
        public bool ShowDeviceConnectivityBalloons { get; set; }
        public bool ShowDeviceOrFolderRejectedBalloons { get; set; }

        public NotifyIconManager(
            IViewManager viewManager,
            NotifyIconViewModel viewModel,
            IApplicationState application,
            IApplicationWindowState applicationWindowState,
            ISyncthingManager syncthingManager,
            IConnectedEventDebouncer connectedEventDebouncer)
        {
            this.viewManager = viewManager;
            this.viewModel = viewModel;
            this.application = application;
            this.applicationWindowState = applicationWindowState;
            this.syncthingManager = syncthingManager;
            this.connectedEventDebouncer = connectedEventDebouncer;

            taskbarIcon = (TaskbarIcon)this.application.FindResource("TaskbarIcon");
            taskbarIcon.TrayBalloonTipClicked += (o, e) =>
            {
                this.applicationWindowState.EnsureInForeground();
            };

            // Need to hold off until after the application is started, otherwise the ViewManager won't be set
            this.application.Startup += ApplicationStartup;

            this.applicationWindowState.RootWindowActivated += RootViewModelActivated;
            this.applicationWindowState.RootWindowDeactivated += RootViewModelDeactivated;
            this.applicationWindowState.RootWindowClosed += RootViewModelClosed;

            this.viewModel.WindowOpenRequested += (o, e) =>
            {
                this.applicationWindowState.EnsureInForeground();
            };
            this.viewModel.WindowCloseRequested += (o, e) =>
            {
                // Always minimize, regardless of settings
                this.application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                this.applicationWindowState.CloseToTray();
            };
            this.viewModel.ExitRequested += (o, e) => this.application.Shutdown();

            this.syncthingManager.TransferHistory.FolderSynchronizationFinished += FolderSynchronizationFinished;
            this.syncthingManager.Devices.DeviceConnected += DeviceConnected;
            this.syncthingManager.Devices.DeviceDisconnected += DeviceDisconnected;
            this.syncthingManager.DeviceRejected += DeviceRejected;
            this.syncthingManager.FolderRejected += FolderRejected;

            this.connectedEventDebouncer.DeviceConnected += DebouncedDeviceConnected;
        }

        private void ApplicationStartup(object sender, EventArgs e)
        {
            viewManager.BindViewToModel(taskbarIcon, viewModel);
        }

        private void DeviceConnected(object sender, DeviceConnectedEventArgs e)
        {
            if (ShowDeviceConnectivityBalloons &&
                    DateTime.UtcNow - syncthingManager.StartedTime > syncedDeadTime)
            {
                connectedEventDebouncer.Connect(e.Device);
            }
        }

        private void DebouncedDeviceConnected(object sender, DeviceConnectedEventArgs e)
        {
            taskbarIcon.HideBalloonTip();
            taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_DeviceConnected_Title, Localizer.F(Resources.TrayIcon_Balloon_DeviceConnected_Message, e.Device.Name), BalloonIcon.Info);
        }

        private void DeviceDisconnected(object sender, DeviceDisconnectedEventArgs e)
        {
            if (ShowDeviceConnectivityBalloons &&
                    DateTime.UtcNow - syncthingManager.StartedTime > syncedDeadTime)
            {
                if (connectedEventDebouncer.Disconnect(e.Device))
                {
                    taskbarIcon.HideBalloonTip();
                    taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_DeviceDisconnected_Title, Localizer.F(Resources.TrayIcon_Balloon_DeviceDisconnected_Message, e.Device.Name), BalloonIcon.Info);
                }
            }
        }

        private void DeviceRejected(object sender, DeviceRejectedEventArgs e)
        {
            if (ShowDeviceOrFolderRejectedBalloons)
            {
                taskbarIcon.HideBalloonTip();
                taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_DeviceRejected_Title, Localizer.F(Resources.TrayIcon_Balloon_DeviceRejected_Message, e.DeviceId, e.Address), BalloonIcon.Info);
            }
        }

        private void FolderRejected(object sender, FolderRejectedEventArgs e)
        {
            if (ShowDeviceOrFolderRejectedBalloons)
            {
                taskbarIcon.HideBalloonTip();
                taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_FolderRejected_Title, Localizer.F(Resources.TrayIcon_Balloon_FolderRejected_Message, e.Device.Name, e.Folder.Label), BalloonIcon.Info);
            }
        }

        private void FolderSynchronizationFinished(object sender, FolderSynchronizationFinishedEventArgs e)
        {
            // If it only contains failed transfers we've seen before, then we don't care.
            // Otherwise we'll keep bugging the user (every minute) for a failing transfer. 
            // However, with this behaviour, we'll still remind them about the failure whenever something succeeds (or a new failure is added)
            if (e.FileTransfers.All(x => x.Error != null && !x.IsNewError))
                return;

            if (FolderNotificationsEnabled != null && FolderNotificationsEnabled.TryGetValue(e.Folder.FolderId, out bool notificationsEnabled) && notificationsEnabled)
            {
                if (e.FileTransfers.Count == 0)
                {
                    if (ShowSynchronizedBalloonEvenIfNothingDownloaded &&
                        DateTime.UtcNow - syncthingManager.LastConnectivityEventTime > syncedDeadTime &&
                        DateTime.UtcNow - syncthingManager.StartedTime > syncedDeadTime)
                    {
                        taskbarIcon.HideBalloonTip();
                        taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_FinishedSyncing_Title, String.Format(Resources.TrayIcon_Balloon_FinishedSyncing_Message, e.Folder.Label), BalloonIcon.Info);
                    }
                }
                else if (e.FileTransfers.Count == 1)
                {
                    var fileTransfer = e.FileTransfers[0];
                    string msg = null;
                    if (fileTransfer.Error == null)
                    {
                        if (fileTransfer.ActionType == ItemChangedActionType.Update)
                            msg = Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_UpdatedSingleFile, e.Folder.Label, Path.GetFileName(fileTransfer.Path));
                        else if (fileTransfer.ActionType == ItemChangedActionType.Delete)
                            msg = Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_DeletedSingleFile, e.Folder.Label, Path.GetFileName(fileTransfer.Path));
                    }
                    else
                    {
                        if (fileTransfer.ActionType == ItemChangedActionType.Update)
                            msg = Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_FailedToUpdateSingleFile, e.Folder.Label, Path.GetFileName(fileTransfer.Path), fileTransfer.Error);
                        else if (fileTransfer.ActionType == ItemChangedActionType.Delete)
                            msg = Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_FailedToDeleteSingleFile, e.Folder.Label, Path.GetFileName(fileTransfer.Path), fileTransfer.Error);
                    }

                    if (msg != null)
                    {
                        taskbarIcon.HideBalloonTip();
                        taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_FinishedSyncing_Title, msg, BalloonIcon.Info);
                    }
                }
                else
                {
                    var updates = e.FileTransfers.Where(x => x.ActionType == ItemChangedActionType.Update).ToArray();
                    var deletes = e.FileTransfers.Where(x => x.ActionType == ItemChangedActionType.Delete).ToArray();

                    var messageParts = new List<string>();

                    if (updates.Length > 0)
                    {
                        var failureCount = updates.Count(x => x.Error != null);
                        if (failureCount > 0)
                            messageParts.Add(Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_UpdatedFileWithFailures, updates.Length, failureCount));
                        else
                            messageParts.Add(Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_UpdatedFile, updates.Length));
                    }


                    if (deletes.Length > 0)
                    {
                        var failureCount = deletes.Count(x => x.Error != null);
                        if (failureCount > 0)
                            messageParts.Add(Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_DeletedFileWithFailures, deletes.Length, failureCount));
                        else
                            messageParts.Add(Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_DeletedFile, deletes.Length));
                    }

                    var text = Localizer.F(Resources.TrayIcon_Balloon_FinishedSyncing_Multiple, e.Folder.Label, messageParts);

                    taskbarIcon.HideBalloonTip();
                    taskbarIcon.ShowBalloonTip(Resources.TrayIcon_Balloon_FinishedSyncing_Title, text, BalloonIcon.Info);
                }
            }
        }

        private void SetShutdownMode()
        {
            application.ShutdownMode = _closeToTray ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnMainWindowClose;
        }

        public async Task<bool?> ShowBalloonAsync(object viewModel, int? timeout = null, CancellationToken? cancellationToken = null)
        {
            var token = cancellationToken ?? CancellationToken.None;

            CloseCurrentlyOpenBalloon(cancel: false);

            var view = viewManager.CreateViewForModel(viewModel);
            taskbarIcon.ShowCustomBalloon(view, System.Windows.Controls.Primitives.PopupAnimation.Slide, timeout);
            taskbarIcon.CustomBalloon.StaysOpen = false;
            viewManager.BindViewToModel(view, viewModel); // Re-assign DataContext, after NotifyIcon overwrote it ><

            balloonTcs = new TaskCompletionSource<bool?>();
            new BalloonConductor(taskbarIcon, viewModel, view, balloonTcs);

            using (token.Register(() =>
            {
                if (taskbarIcon.CustomBalloon.Child == view)
                    CloseCurrentlyOpenBalloon(cancel: true);
            }))
            {
                return await balloonTcs.Task;
            }
        }

        private void CloseCurrentlyOpenBalloon(bool cancel)
        {
            if (balloonTcs == null)
                return;

            taskbarIcon.CloseBalloon();

            if (cancel)
                balloonTcs.TrySetCanceled();
            else
                balloonTcs.TrySetResult(null);

            balloonTcs = null;
        }

        public void EnsureIconVisible()
        {
            viewModel.Visible = true;
        }

        private void RootViewModelActivated(object sender, ActivationEventArgs e)
        {
            // If it's minimize to tray, not close to tray, then we'll have set the shutdown mode to OnExplicitShutdown just before closing
            // In this case, re-set Shutdownmode
            SetShutdownMode();

            viewModel.MainWindowVisible = true;
            if (ShowOnlyOnClose)
                viewModel.Visible = false;
        }

        private void RootViewModelDeactivated(object sender, DeactivationEventArgs e)
        {
            if (MinimizeToTray)
            {
                // Don't do this if it's shutting down
                if (application.HasMainWindow)
                    application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                applicationWindowState.CloseToTray();

                viewModel.MainWindowVisible = false;
                if (ShowOnlyOnClose)
                    viewModel.Visible = true;
            }
        }

        private void RootViewModelClosed(object sender, CloseEventArgs e)
        {
            viewModel.MainWindowVisible = false;
            if (ShowOnlyOnClose)
                viewModel.Visible = true;
        }

        public void Dispose()
        {
            application.Startup -= ApplicationStartup;

            applicationWindowState.RootWindowActivated -= RootViewModelActivated;
            applicationWindowState.RootWindowDeactivated -= RootViewModelDeactivated;
            applicationWindowState.RootWindowClosed -= RootViewModelClosed;

            syncthingManager.TransferHistory.FolderSynchronizationFinished -= FolderSynchronizationFinished;
            syncthingManager.Devices.DeviceConnected -= DeviceConnected;
            syncthingManager.Devices.DeviceDisconnected -= DeviceDisconnected;
            syncthingManager.DeviceRejected -= DeviceRejected;
            syncthingManager.FolderRejected -= FolderRejected;
        }
    }
}
