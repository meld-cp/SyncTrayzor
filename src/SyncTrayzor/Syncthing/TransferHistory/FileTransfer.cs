using SyncTrayzor.Syncthing.ApiClient;
using SyncTrayzor.Syncthing.Folders;
using System;

namespace SyncTrayzor.Syncthing.TransferHistory
{
    public class FileTransfer
    {
        public FileTransferStatus Status { get; set; }

        public long BytesTransferred { get; private set; }
        public long TotalBytes { get; private set; }
        public double? DownloadBytesPerSecond { get; private set; }

        public Folder Folder { get; }
        public string Path { get; }
        public ItemChangedItemType ItemType { get; }
        public ItemChangedActionType ActionType { get; }

        public DateTime StartedUtc { get; private set; }
        public DateTime? FinishedUtc { get; private set; }

        public string Error { get; private set; }
        public bool IsNewError { get; private set; }

        private DateTime? lastProgressUpdateUtc;

        public FileTransfer(Folder folder, string path, ItemChangedItemType itemType, ItemChangedActionType actionType)
        {
            Folder = folder;
            Path = path;

            Status = FileTransferStatus.Started;
            StartedUtc = DateTime.UtcNow;
            ItemType = itemType;
            ActionType = actionType;
        }

        public void SetDownloadProgress(long bytesTransferred, long totalBytes)
        {
            var now = DateTime.UtcNow;
            if (lastProgressUpdateUtc.HasValue)
            {
                var deltaBytesTransferred = bytesTransferred - BytesTransferred;
                DownloadBytesPerSecond = deltaBytesTransferred / (now - lastProgressUpdateUtc.Value).TotalSeconds;
            }

            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
            Status = FileTransferStatus.InProgress;
            lastProgressUpdateUtc = now;
        }

        public void SetComplete(string error, bool isNewError)
        {
            Status = FileTransferStatus.Completed;
            BytesTransferred = TotalBytes;
            FinishedUtc = DateTime.UtcNow;
            Error = error;
            IsNewError = isNewError;
        }

        public override string ToString()
        {
            return $"<FileTransfer Folder={Folder.Label} Path={Path} Status={Status} ItemType={ItemType} ActionType={ActionType} Started={StartedUtc} Finished={FinishedUtc}>";
        }
    }
}
