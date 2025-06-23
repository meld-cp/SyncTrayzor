using Stylet;
using SyncTrayzor.Properties;
using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.Folders;
using SyncTrayzor.Syncthing.TransferHistory;
using SyncTrayzor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SyncTrayzor.Pages.Tray
{
    public class FileTransferViewModel : PropertyChangedBase, IDisposable
    {
        public readonly FileTransfer FileTransfer;
        private readonly DispatcherTimer completedTimeAgoUpdateTimer;

        public string Path { get; }
        public Folder Folder { get; }
        public string FullPath { get; }
        public ImageSource Icon { get; }
        public string Error { get; private set; }
        public bool WasDeleted { get; }

        public DateTime Completed => FileTransfer.FinishedUtc.GetValueOrDefault().ToLocalTime();

        public string CompletedTimeAgo
        {
            get
            {
                if (FileTransfer.FinishedUtc.HasValue)
                    return FormatUtils.TimeSpanToTimeAgo(DateTime.UtcNow - FileTransfer.FinishedUtc.Value);
                else
                    return null;
            }
        }

        public string ProgressString { get; private set; }
        public float ProgressPercent { get; private set; }

        private bool disposed;

        public FileTransferViewModel(FileTransfer fileTransfer)
        {
            completedTimeAgoUpdateTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMinutes(1),
            };
            completedTimeAgoUpdateTimer.Tick += Tick;
            completedTimeAgoUpdateTimer.Start();

            FileTransfer = fileTransfer;
            Path = System.IO.Path.GetFileName(FileTransfer.Path);
            FullPath = FileTransfer.Path;
            Folder = FileTransfer.Folder;
            using (var icon = ShellTools.GetIcon(FileTransfer.Path, FileTransfer.ItemType != ItemChangedItemType.Dir))
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                Icon = bs;
            }

            WasDeleted = FileTransfer.ActionType == ItemChangedActionType.Delete;

            UpdateState();
        }

        public void UpdateState()
        {
            switch (FileTransfer.Status)
            {
                case FileTransferStatus.InProgress:
                    if (FileTransfer.DownloadBytesPerSecond.HasValue)
                    {
                        ProgressString = String.Format(Resources.FileTransfersTrayView_Downloading_RateKnown,
                            FormatUtils.BytesToHuman(FileTransfer.BytesTransferred),
                            FormatUtils.BytesToHuman(FileTransfer.TotalBytes),
                            FormatUtils.BytesToHuman(FileTransfer.DownloadBytesPerSecond.Value, 1));
                    }
                    else
                    {
                        ProgressString = String.Format(Resources.FileTransfersTrayView_Downloading_RateUnknown,
                            FormatUtils.BytesToHuman(FileTransfer.BytesTransferred),
                            FormatUtils.BytesToHuman(FileTransfer.TotalBytes));
                    }

                    ProgressPercent = ((float)FileTransfer.BytesTransferred / (float)FileTransfer.TotalBytes) * 100;
                    break;

                case FileTransferStatus.Completed:
                    ProgressPercent = 100;
                    ProgressString = null;
                    break;
            }

            Error = FileTransfer.Error;
        }

        private void Tick(Object sender, EventArgs e)
        {
            NotifyOfPropertyChange(() => CompletedTimeAgo);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                completedTimeAgoUpdateTimer.Stop();
                completedTimeAgoUpdateTimer.Tick -= Tick;
            }

            disposed = true;
        }
    }
}