using Stylet;
using SyncTrayzor.Localization;
using SyncTrayzor.Properties;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Services.UpdateManagement;
using SyncTrayzor.Syncthing;
using System;
using System.Reflection;

namespace SyncTrayzor.Pages
{
    public class AboutViewModel : Screen
    {
        // Not in the app.config, in case some sysadmin wants to change it
        private readonly IWindowManager windowManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly IUpdateManager updateManager;
        private readonly Func<ThirdPartyComponentsViewModel> thirdPartyComponentsViewModelFactory;
        private readonly IProcessStartProvider processStartProvider;

        public string Version { get; set; }
        public string SyncthingVersion { get; set; }
        public string HomepageUrl { get; set; }

        public string NewerVersion { get; set; }
        public bool ShowTranslatorAttributation => Localizer.Translate("TranslatorAttributation") == Localizer.OriginalTranslation("TranslatorAttributation");
        private string newerVersionDownloadUrl;

        public IDonationManager DonationManager { get; }

        public AboutViewModel(
            IWindowManager windowManager,
            ISyncthingManager syncthingManager,
            IUpdateManager updateManager,
            Func<ThirdPartyComponentsViewModel> thirdPartyComponentsViewModelFactory,
            IProcessStartProvider processStartProvider,
            IDonationManager donationManager)
        {
            this.windowManager = windowManager;
            this.syncthingManager = syncthingManager;
            this.updateManager = updateManager;
            this.thirdPartyComponentsViewModelFactory = thirdPartyComponentsViewModelFactory;
            this.processStartProvider = processStartProvider;
            DonationManager = donationManager;

            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            HomepageUrl = AppSettings.Instance.HomepageUrl;

            this.syncthingManager.DataLoaded += SyncthingDataLoaded;
            LoadSyncthingVersion();

            CheckForNewerVersionAsync();
        }

        private void SyncthingDataLoaded(object sender, EventArgs e)
        {
            LoadSyncthingVersion();
        }

        private void LoadSyncthingVersion()
        {
            SyncthingVersion = syncthingManager.Version == null ? Resources.AboutView_UnknownVersion : syncthingManager.Version.ShortVersion;
        }

        private async void CheckForNewerVersionAsync()
        {
            var results = await updateManager.CheckForAcceptableUpdateAsync();

            if (results == null)
                return;

            NewerVersion = results.NewVersion.ToString(3);
            newerVersionDownloadUrl = results.ReleasePageUrl;
        }

        public void ShowHomepage()
        {
            processStartProvider.StartDetached(HomepageUrl);
        }

        public void DownloadNewerVersion()
        {
            if (newerVersionDownloadUrl == null)
                return;

            processStartProvider.StartDetached(newerVersionDownloadUrl);
        }

        public void ShowLicenses()
        {
            var vm = thirdPartyComponentsViewModelFactory();
            windowManager.ShowDialog(vm);
            RequestClose(true);
        }

        public void Close()
        {
            RequestClose(true);
        }

        protected override void OnClose()
        {
            syncthingManager.DataLoaded -= SyncthingDataLoaded;
        }
    }
}
