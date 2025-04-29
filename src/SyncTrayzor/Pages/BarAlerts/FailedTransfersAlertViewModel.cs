using Stylet;
using System.Collections.Generic;

namespace SyncTrayzor.Pages.BarAlerts
{
    public class FailedTransfersAlertViewModel : Screen, IBarAlert
    {
        public AlertSeverity Severity => AlertSeverity.Warning;

        public BindableCollection<string> FailingFolders { get; } = new();

        public FailedTransfersAlertViewModel(IEnumerable<string> failingFolders)
        {
            FailingFolders.AddRange(failingFolders);
        }
    }
}
