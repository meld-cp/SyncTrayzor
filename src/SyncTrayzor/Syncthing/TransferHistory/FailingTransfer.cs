using System;

namespace SyncTrayzor.Syncthing.TransferHistory
{
    public class FailingTransfer : IEquatable<FailingTransfer>
    {
        public string FolderId { get; }
        public string Path { get; }
        public string Error { get; }

        public FailingTransfer(string folderId, string path, string error)
        {
            FolderId = folderId;
            Path = path;
            Error = error;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FailingTransfer);
        }

        public bool Equals(FailingTransfer other)
        {
            if (ReferenceEquals(other, null))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return FolderId == other.FolderId && Path == other.Path;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + FolderId.GetHashCode();
                hash = hash * 31 + Path.GetHashCode();
                return hash;
            }
        }
    }
}
