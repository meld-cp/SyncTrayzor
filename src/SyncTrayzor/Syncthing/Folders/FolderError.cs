using System;

namespace SyncTrayzor.Syncthing.Folders
{
    public class FolderError : IEquatable<FolderError>
    {
        public string Error { get; }
        public string Path { get; }

        public FolderError(string error, string path)
        {
            Error = error;
            Path = path;
        }

        public bool Equals(FolderError other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (ReferenceEquals(other, null))
                return false;

            return Error == other.Error &&
                Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FolderError);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Error.GetHashCode();
                hash = hash * 23 + Path.GetHashCode();
                return hash;
            }
        }
    }
}
