using Stylet;
using SyncTrayzor.Services;
using SyncTrayzor.Services.Config;
using System;
using System.Drawing;
using System.Text;
using System.IO;

namespace SyncTrayzor.Pages
{
    public class UnhandledExceptionViewModel : Screen
    {
        private readonly IApplicationPathsProvider applicationPathsProvider;
        private readonly IProcessStartProvider processStartProvider;
        private readonly IAssemblyProvider assemblyProvider;

        public Exception Exception { get; set; }

        public string IssuesUrl { get; }

        public string ErrorMessage => GenerateErrorMessage();
        public Icon Icon => SystemIcons.Error;

        public UnhandledExceptionViewModel(IApplicationPathsProvider applicationPathsProvider, IProcessStartProvider processStartProvider, IAssemblyProvider assemblyProvider)
        {
            this.applicationPathsProvider = applicationPathsProvider;
            this.processStartProvider = processStartProvider;
            this.assemblyProvider = assemblyProvider;

            IssuesUrl = AppSettings.Instance.IssuesUrl;
        }

        private string GenerateErrorMessage()
        {
            var sb = new StringBuilder();
            sb.Append(
                $"Version: {assemblyProvider.FullVersion}; Variant: {AppSettings.Instance.Variant}; Arch: {assemblyProvider.ProcessorArchitecture}");
            sb.AppendLine();

            sb.Append($"Path: {Environment.ProcessPath!}");
            sb.AppendLine();

            sb.AppendLine(Exception.ToString());

            return sb.ToString();
        }

        public void ShowIssues()
        {
            processStartProvider.StartDetached(IssuesUrl);
        }

        public void OpenLogFilePath()
        {
            processStartProvider.ShowFileInExplorer(Path.Combine(applicationPathsProvider.LogFilePath, "SyncTrayzor.log"));
        }

        public void Close()
        {
            RequestClose(true);
        }
    }
}
