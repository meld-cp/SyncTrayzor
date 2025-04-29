using Stylet;
using System;

namespace SyncTrayzor.Pages.BarAlerts
{
    public class ConflictsAlertViewModel : PropertyChangedBase, IBarAlert
    {
        public event EventHandler OpenConflictResolverClicked;

        public AlertSeverity Severity => AlertSeverity.Warning;

        public int NumConflicts { get; }

        public ConflictsAlertViewModel(int numConflicts)
        {
            NumConflicts = numConflicts;
        }

        public void OpenConflictResolver()
        {
            OpenConflictResolverClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
