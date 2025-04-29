using Stylet;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public class SingleConflictResolutionViewModel : Screen
    {
        public ConflictViewModel Conflict { get; set; }

        public ConflictResolutionViewModel Delegate { get; set; }

        public void ShowFileInFolder()
        {
            Delegate.ShowFileInFolder(Conflict);
        }

        public void ChooseOriginal()
        {
            Delegate.ChooseOriginal(Conflict);
        }

        public void ChooseConflictFile(ConflictOptionViewModel conflictOption)
        {
            Delegate.ChooseConflictFile(Conflict, conflictOption);
        }
    }
}
