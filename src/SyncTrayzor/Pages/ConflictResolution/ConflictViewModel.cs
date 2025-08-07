using Stylet;
using SyncTrayzor.Services.Conflicts;
using System;
using System.IO;
using System.Linq;
using SyncTrayzor.Utils;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows;
using SyncTrayzor.Localization;

namespace SyncTrayzor.Pages.ConflictResolution
{
    public class ConflictViewModel : PropertyChangedBase
    {
        public ConflictSet ConflictSet { get; }

        public string FilePath => ConflictSet.File.FilePath;

        public string FileName => Path.GetFileName(ConflictSet.File.FilePath);

        public string DisplayName =>
            Deleted ? Localizer.Translate("ConflictResolutionView_FileName_FileDeleted") : FileName;

        public DateTime? LastModified => ConflictSet.File.LastModified;

        public string Folder => Path.GetDirectoryName(ConflictSet.File.FilePath);

        public string InnerFolder => Path.GetFileName(Folder);

        public string FolderLabel { get; }

        public BindableCollection<ConflictOptionViewModel> ConflictOptions { get; }

        public ImageSource Icon { get; }

        public string Size => ConflictSet.File.SizeBytes is null
            ? "???"
            : FormatUtils.BytesToHuman((double)ConflictSet.File.SizeBytes, 1);

        public bool Deleted => ConflictSet.File.Deleted;

        public bool IsSelected { get; set; }

        public ConflictViewModel(ConflictSet conflictSet, string folderName)
        {
            ConflictSet = conflictSet;
            FolderLabel = folderName;

            ConflictOptions =
                new BindableCollection<ConflictOptionViewModel>(
                    ConflictSet.Conflicts.Select(x => new ConflictOptionViewModel(x)));

            // These bindings aren't called lazily, so don't bother being lazy
            using var icon = ShellTools.GetIcon(ConflictSet.File.FilePath, isFile: true);
            if (icon != null)
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                Icon = bs;
            }
        }
    }
}