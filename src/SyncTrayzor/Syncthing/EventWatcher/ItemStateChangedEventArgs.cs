using System;

namespace SyncTrayzor.Syncthing.EventWatcher
{
    public class ItemStateChangedEventArgs : EventArgs
    {
        public string Folder { get; }
        public string Item { get; }

        public ItemStateChangedEventArgs(string folder, string item)
        {
            Folder = folder;
            Item = item;
        }
    }
}
