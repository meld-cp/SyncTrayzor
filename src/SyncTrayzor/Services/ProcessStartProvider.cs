using NLog;
using SyncTrayzor.Services.Config;
using SyncTrayzor.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SyncTrayzor.Services
{
    public interface IProcessStartProvider
    {
        void Start(string filename);
        void Start(string filename, string arguments);
        void StartDetached(string filename);
        void StartDetached(string filename, string arguments, string launchAfterFinished = null);
        void StartElevatedDetached(string filename, string arguments, string launchAfterFinished = null);
        void ShowFolderInExplorer(string path);
        void ShowFileInExplorer(string filePath);
    }

    public class ProcessStartProvider : IProcessStartProvider
    {
        private static readonly string processRunner = "ProcessRunner.exe";
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly string processRunnerPath;

        private readonly IConfigurationProvider configurationProvider;

        public ProcessStartProvider(IConfigurationProvider configurationProvider)
        {
            processRunnerPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, processRunner);
            this.configurationProvider = configurationProvider;
        }

        public void Start(string filename)
        {
            logger.Debug("Starting {0}", filename);
            Process.Start(filename);
        }

        public void Start(string filename, string arguments)
        {
            logger.Debug("Starting {0} {1}", filename, arguments);
            Process.Start(filename, arguments);
        }

        public void StartDetached(string filename)
        {
            StartDetached(filename, null);
        }

        public void StartDetached(string filename, string arguments, string launchAfterFinished = null)
        {
            if (arguments == null)
                arguments = String.Empty;

            var launch = launchAfterFinished == null ? null : $"--launch=\"{launchAfterFinished.Replace("\"", "\\\"")}\"";
            var formattedArguments = $"--shell {launch} -- \"{filename}\" {arguments}";

            logger.Debug("Starting {0} {1}", processRunnerPath, formattedArguments);
            var startInfo = new ProcessStartInfo()
            {
                FileName = processRunnerPath,
                Arguments = formattedArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            Process.Start(startInfo);
        }

        public void StartElevatedDetached(string filename, string arguments, string launchAfterFinished = null)
        {
            if (arguments == null)
                arguments = String.Empty;

            var launch = launchAfterFinished == null ? null : $"--launch=\"{launchAfterFinished.Replace("\"", "\\\"")}\"";
            var formattedArguments = $"--nowindow -- \"{processRunnerPath}\" --runas {launch} -- \"{filename}\" {arguments}";

            logger.Debug("Starting {0} {1}", processRunnerPath, formattedArguments);

            var startInfo = new ProcessStartInfo()
            {
                FileName = processRunnerPath,
                Arguments = formattedArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            Process.Start(startInfo);
        }

        public void ShowFolderInExplorer(string path)
        {
            var command = configurationProvider.Load().OpenFolderCommand;
            StartExplorerHelper(command, path);
        }

        public void ShowFileInExplorer(string filePath)
        {
            var command = configurationProvider.Load().ShowFileInFolderCommand;
            StartExplorerHelper(command, filePath);
        }

        private void StartExplorerHelper(string commandFromConfig, string path)
        {
            var parts = StringExtensions.SplitCommandLine(String.Format(commandFromConfig, path));
            var executable = parts.FirstOrDefault();
            if (executable == null)
            {
                logger.Error($"Command {commandFromConfig} is badly formed, and does not contain an executable");
                return;
            }

            StartDetached(executable, StringExtensions.JoinCommandLine(parts.Skip(1)));
        }
    }
}
