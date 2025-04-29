using Stylet;
using SyncTrayzor.Services.Conflicts;
using SyncTrayzor.Utils;
using System;
using System.IO;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public class ConflictOptionViewModel : PropertyChangedBase
    {
        public ConflictOption ConflictOption { get; }

        public string FileName => Path.GetFileName(ConflictOption.FilePath);

        public DateTime DateCreated => ConflictOption.Created;
        public DateTime LastModified => ConflictOption.LastModified;
        public string Size => FormatUtils.BytesToHuman(ConflictOption.SizeBytes, 1);
        public string ModifiedBy => ConflictOption.Device?.Name;

        public ConflictOptionViewModel(ConflictOption conflictOption)
        {
            ConflictOption = conflictOption;
        }
    }
}
