using NLog;
using Stylet;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SyncTrayzor.Services.UpdateManagement
{
    public class VersionIgnoredEventArgs : EventArgs
    {
        public Version IgnoredVersion { get;  }

        public VersionIgnoredEventArgs(Version ignoredVersion)
        {
            IgnoredVersion = ignoredVersion;
        }
    }

    public interface IUpdateManager : IDisposable
    {
        event EventHandler<VersionIgnoredEventArgs> VersionIgnored;
        Version LatestIgnoredVersion { get; set; }
        string UpdateCheckApiUrl { get; set; }
        bool CheckForUpdates { get; set; }

        Task<VersionCheckResults> CheckForAcceptableUpdateAsync();
    }

    public class UpdateManager : IUpdateManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static readonly TimeSpan deadTimeAfterStarting = TimeSpan.FromMinutes(5);
        // We'll never check more frequently than this, ever
        private static readonly TimeSpan updateCheckDebounceTime = TimeSpan.FromHours(24);
        // If 'remind me later' is active, we'll check this frequently
        private static readonly TimeSpan remindMeLaterTime = TimeSpan.FromDays(7);
        // How often the update checking timer should fire. Having it fire too often is OK: we won't
        // take action
        private static readonly TimeSpan updateCheckingTimerInterval = TimeSpan.FromDays(5);

        private readonly IApplicationState applicationState;
        private readonly IApplicationWindowState applicationWindowState;
        private readonly IUserActivityMonitor userActivityMonitor;
        private readonly IUpdateCheckerFactory updateCheckerFactory;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IUpdatePromptProvider updatePromptProvider;
        private readonly Func<IUpdateVariantHandler> updateVariantHandlerFactory;

        private readonly object promptTimerLock = new();
        private readonly DispatcherTimer promptTimer;

        private readonly SemaphoreSlim versionCheckLock = new(1, 1);

        private DateTime lastCheckedTime;
        private CancellationTokenSource toastCts;
        private bool remindLaterActive;

        public event EventHandler<VersionIgnoredEventArgs> VersionIgnored;
        public Version LatestIgnoredVersion { get; set; }
        public string UpdateCheckApiUrl { get; set; }

        private bool _checkForUpdates;
        public bool CheckForUpdates
        {
            get => _checkForUpdates;
            set
            {
                if (_checkForUpdates == value)
                    return;
                _checkForUpdates = value;
                UpdateCheckForUpdates(value);
            }
        }

        public UpdateManager(
            IApplicationState applicationState,
            IApplicationWindowState applicationWindowState,
            IUserActivityMonitor userActivityMonitor,
            IUpdateCheckerFactory updateCheckerFactory,
            IProcessStartProvider processStartProvider,
            IUpdatePromptProvider updatePromptProvider,
            Func<IUpdateVariantHandler> updateVariantHandlerFactory)
        {
            this.applicationState = applicationState;
            this.applicationWindowState = applicationWindowState;
            this.userActivityMonitor = userActivityMonitor;
            this.updateCheckerFactory = updateCheckerFactory;
            this.processStartProvider = processStartProvider;
            this.updatePromptProvider = updatePromptProvider;
            this.updateVariantHandlerFactory = updateVariantHandlerFactory;

            promptTimer = new DispatcherTimer();
            promptTimer.Tick += PromptTimerElapsed;

            // Strategy time:
            // We'll always check when the user starts up or resumes from sleep.
            // We'll check whenever the user opens the app, debounced to a suitable period.
            // We'll check periodically if none of the above have happened, on a longer interval.
            // If 'remind me later' is active, we'll do none of the above for a long interval.

            this.applicationState.ResumeFromSleep += ResumeFromSleep;
            this.applicationWindowState.RootWindowActivated += RootWindowActivated;
        }

        private bool ShouldCheckForUpdates()
        {
            if (remindLaterActive)
                return DateTime.UtcNow - lastCheckedTime > remindMeLaterTime;
            else
                return DateTime.UtcNow - lastCheckedTime > updateCheckDebounceTime;
        }

        private async void UpdateCheckForUpdates(bool checkForUpdates)
        {
            if (checkForUpdates)
            {
                RestartTimer();
                // Give them a minute to catch their breath
                if (ShouldCheckForUpdates())
                {
                    await Task.Delay(deadTimeAfterStarting);
                    await CheckForUpdatesAsync();
                }
            }
            else
            {
                lock (promptTimerLock)
                {
                    promptTimer.IsEnabled = false;
                }
            }
        }

        private async void ResumeFromSleep(object sender, EventArgs e)
        {
            if (ShouldCheckForUpdates())
            {
                // We often wake up before the network does. Give the network some time to sort itself out
                await Task.Delay(deadTimeAfterStarting);
                await CheckForUpdatesAsync();
            }
        }

        private async void RootWindowActivated(object sender, ActivationEventArgs e)
        {
            if (toastCts != null)
                toastCts.Cancel();

            if (ShouldCheckForUpdates())
                await CheckForUpdatesAsync();
        }

        private async void PromptTimerElapsed(object sender, EventArgs e)
        {
            if (ShouldCheckForUpdates())
                await CheckForUpdatesAsync();
        }

        private void OnVersionIgnored(Version ignoredVersion)
        {
            VersionIgnored?.Invoke(this, new VersionIgnoredEventArgs(ignoredVersion));
        }

        private void RestartTimer()
        {
            lock(promptTimerLock)
            {
                promptTimer.IsEnabled = false;
                promptTimer.Interval = updateCheckingTimerInterval;
                promptTimer.IsEnabled = true;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            RestartTimer();

            if (!versionCheckLock.Wait(0))
                return;

            try
            {
                lastCheckedTime = DateTime.UtcNow;

                if (!CheckForUpdates || string.IsNullOrWhiteSpace(UpdateCheckApiUrl))
                    return;

                var variantHandler = updateVariantHandlerFactory();

                var updateChecker = updateCheckerFactory.CreateUpdateChecker(UpdateCheckApiUrl, variantHandler.VariantName);
                var checkResult = await updateChecker.CheckForAcceptableUpdateAsync(LatestIgnoredVersion);

                if (checkResult == null)
                    return;

                if (!await variantHandler.TryHandleUpdateAvailableAsync(checkResult))
                {
                    logger.Info("Can't update, as TryHandleUpdateAvailableAsync returned false");
                    return;
                }

                VersionPromptResult promptResult;
                if (applicationState.HasMainWindow)
                {
                    promptResult = updatePromptProvider.ShowDialog(checkResult, variantHandler.CanAutoInstall, variantHandler.RequiresUac);
                }
                else
                {
                    // If another application is fullscreen, don't bother
                    if (userActivityMonitor.IsWindowFullscreen())
                    {
                        logger.Debug("Another application was fullscreen, so we didn't prompt the user");
                        return;
                    }

                    try
                    {
                        toastCts = new CancellationTokenSource();
                        promptResult = await updatePromptProvider.ShowToast(checkResult, variantHandler.CanAutoInstall, variantHandler.RequiresUac, toastCts.Token);
                        toastCts = null;

                        // Special case
                        if (promptResult == VersionPromptResult.ShowMoreDetails)
                        {
                            applicationWindowState.EnsureInForeground();
                            promptResult = updatePromptProvider.ShowDialog(checkResult, variantHandler.CanAutoInstall, variantHandler.RequiresUac);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        toastCts = null;
                        logger.Debug("Update toast cancelled. Moving to a dialog");
                        promptResult = updatePromptProvider.ShowDialog(checkResult, variantHandler.CanAutoInstall, variantHandler.RequiresUac);
                    }
                }

                remindLaterActive = false;
                switch (promptResult)
                {
                    case VersionPromptResult.InstallNow:
                        Debug.Assert(variantHandler.CanAutoInstall);
                        logger.Info("Auto-installing {0}", checkResult.NewVersion);
                        variantHandler.AutoInstall(PathToRestartApplication());
                        break;

                    case VersionPromptResult.Download:
                        logger.Info("Proceeding to download URL {0}", checkResult.DownloadUrl);
                        processStartProvider.StartDetached(checkResult.ReleasePageUrl);
                        break;

                    case VersionPromptResult.Ignore:
                        logger.Info("Ignoring version {0}", checkResult.NewVersion);
                        OnVersionIgnored(checkResult.NewVersion);
                        break;

                    case VersionPromptResult.RemindLater:
                        remindLaterActive = true;
                        logger.Info("Not installing version {0}, but will remind later", checkResult.NewVersion);
                        break;

                    case VersionPromptResult.ShowMoreDetails:
                    default:
                        Debug.Assert(false);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error in UpdateManager.CheckForUpdatesAsync");
            }
            finally
            {
                versionCheckLock.Release();
            }
        }

        private string PathToRestartApplication()
        {
            var path = $"\"{Environment.ProcessPath!}\"";
            if (!applicationState.HasMainWindow)
                path += " -minimized";

            return path;
        }

        public Task<VersionCheckResults> CheckForAcceptableUpdateAsync()
        {
            var variantHandler = updateVariantHandlerFactory();
            var updateChecker = updateCheckerFactory.CreateUpdateChecker(UpdateCheckApiUrl, variantHandler.VariantName);
            return updateChecker.CheckForAcceptableUpdateAsync(LatestIgnoredVersion);
        }

        public void Dispose()
        {
            applicationState.ResumeFromSleep -= ResumeFromSleep;
            applicationWindowState.RootWindowActivated -= RootWindowActivated;
        }
    }
}
