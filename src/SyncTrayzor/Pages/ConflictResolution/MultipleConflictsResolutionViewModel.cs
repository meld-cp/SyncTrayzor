using Stylet;
using System.Collections.Generic;
using System.Linq;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public class MultipleConflictsResolutionViewModel : Screen
    {
        public List<ConflictViewModel> Conflicts { get; set; }

        public ConflictResolutionViewModel Delegate { get; set; }

        public void ChooseOriginal()
        {
            foreach (var conflict in Conflicts)
            {
                Delegate.ChooseOriginal(conflict);
            }
        }

        public void ChooseNewest()
        {
            foreach(var conflict in Conflicts)
            {
                var newestOption = conflict.ConflictOptions.MaxBy(x => x.DateCreated);
                if (newestOption.DateCreated > conflict.LastModified)
                    Delegate.ChooseConflictFile(conflict, newestOption);
                else
                    Delegate.ChooseOriginal(conflict);
            }
        }

        public void ChooseNewestConflict()
        {
            foreach (var conflict in Conflicts)
            {
                var newestOption = conflict.ConflictOptions.MaxBy(x => x.DateCreated);
                Delegate.ChooseConflictFile(conflict, newestOption);
            }
        }
    }
}
