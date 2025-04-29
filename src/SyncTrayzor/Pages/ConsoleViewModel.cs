using Stylet;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Pages.Settings;

namespace SyncTrayzor.Pages
{
    public class ConsoleViewModel : Screen, IDisposable
    {
        private const int maxLogMessages = 1500;

        private readonly IWindowManager windowManager;
        private readonly ISyncthingManager syncthingManager;
        private readonly Buffer<string> logMessagesBuffer;
        private readonly Func<SettingsViewModel> settingsViewModelFactory;

        public Queue<string> LogMessages { get;  }
        public bool LogPaused { get; set; }

        public ConsoleViewModel(
            IWindowManager windowManager,
            ISyncthingManager syncthingManager,
            IConfigurationProvider configurationProvider,
            Func<SettingsViewModel> settingsViewModelFactory)
        {
            this.windowManager = windowManager;
            this.syncthingManager = syncthingManager;
            this.settingsViewModelFactory = settingsViewModelFactory;
            LogMessages = new Queue<string>();

            // Display log messages 100ms after the previous message, or every 500ms if they're arriving thick and fast
            logMessagesBuffer = new Buffer<string>(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));
            logMessagesBuffer.Delivered += LogMessageDelivered;

            this.syncthingManager.MessageLogged += SyncthingMessageLogged;
        }

        private void LogMessageDelivered(object sender, BufferDeliveredEventArgs<string> e)
        {
            foreach (var message in e.Items)
            {
                LogMessages.Enqueue(message);
                if (LogMessages.Count > maxLogMessages)
                    LogMessages.Dequeue();
            }

            if (!LogPaused)
                NotifyOfPropertyChange(nameof(LogMessages));
        }

        private void SyncthingMessageLogged(object sender, MessageLoggedEventArgs e)
        {
            logMessagesBuffer.Add(e.LogMessage);
        }

        public void ClearLog()
        {
            LogMessages.Clear();
            NotifyOfPropertyChange(nameof(LogMessages));
        }

        public void ShowSettings()
        {
            var vm = settingsViewModelFactory();
            vm.SelectLoggingTab();
            windowManager.ShowDialog(vm);
        }

        public void PauseLog()
        {
            LogPaused = true;
        }

        public void ResumeLog()
        {
            LogPaused = false;
            NotifyOfPropertyChange(nameof(LogMessages));
        }

        public void Dispose()
        {
            syncthingManager.MessageLogged -= SyncthingMessageLogged;
        }
    }
}
