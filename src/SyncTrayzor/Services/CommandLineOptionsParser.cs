using Mono.Options;
using Stylet;
using System.IO;
using System.Windows;

namespace SyncTrayzor.Services
{
    public class CommandLineOptionsParser
    {
        private readonly IWindowManager windowManager;

        public bool Shutdown { get; private set; }
        public bool StartMinimized { get; private set; }
        public bool StartSyncthing { get; private set; }
        public bool StopSyncthing { get; private set; }
        public string Culture { get; private set; }

        public CommandLineOptionsParser(IWindowManager windowManager)
        {
            this.windowManager = windowManager;
        }

        public bool Parse(string[] args)
        {
            // If no flags are passed, we do not start minimized.
            // If -minimized is passed, we start minimized
            // If -start-syncthing or -stop-syncthing is passed, we default to minimized, but can be overridden
            //   by -show.

            bool minimized = false;
            bool show = false;

            var options = new OptionSet()
                .Add("shutdown", "\nIf another SyncTrayzor process is running, tell it to shutdown.", v => Shutdown = true)
                .Add("start-syncthing", "\nIf another SyncTrayzor process is running, tell it to start Syncthing. Otherwise, launch with Syncthing started regardless of configuration.", v => StartSyncthing = true)
                .Add("stop-syncthing", "\nIf another SyncTrayzor process is running, tell it to stop Syncthing. Otherwise, launch with Syncthing stopped regardless of configuration.", v => StopSyncthing = true)
                .Add("noautostart", null, v => StopSyncthing = true, hidden: true)
                .Add("minimized", "\nIf another SyncTrayzor process is running, this flag has no effect. Otherwise, start in the tray rather than in the foreground.", v => minimized = true)
                .Add("show", "\nIf another SyncTrayzor process is running, tell it to show its main window. Otherwise, this flag has no effect.", v => show = true)
                .Add("culture=", "\nForce SyncTrayzor to use a particular language", v => Culture = v);

            var unknownArgs = options.Parse(args);

            if (unknownArgs.Count > 0)
            {
                var writer = new StringWriter();
                options.WriteOptionDescriptions(writer);
                windowManager.ShowMessageBox(writer.ToString(), "SyncTrayzor command-line usage");
                return false;
            }

            if (Shutdown && (StartSyncthing || StopSyncthing || minimized || show))
            {
                windowManager.ShowMessageBox("--shutdown may not be used with any other options", "Error", icon: MessageBoxImage.Error);
                return false;
            }
            if (StartSyncthing && StopSyncthing)
            {
                windowManager.ShowMessageBox("--start-syncthing and --stop-syncthing may not be used together", "Error", icon: MessageBoxImage.Error);
                return false;
            }
            if (minimized && show)
            {
                windowManager.ShowMessageBox("--minimized and --show may not be used together", "Error", icon: MessageBoxImage.Error);
                return false;
            }

            StartMinimized = minimized || ((StartSyncthing || StopSyncthing) && !show);

            return true;
        }
    }
}
