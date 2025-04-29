namespace SyncTrayzor.Services.Config
{
    public class FolderConfiguration
    {
        public string ID { get; set; }
        public bool IsWatched { get; set; }
        public bool NotificationsEnabled { get; set; }

        public FolderConfiguration()
        {
        }

        public FolderConfiguration(string id, bool isWatched, bool notificationsEnabled)
        {
            ID = id;
            IsWatched = isWatched;
            NotificationsEnabled = notificationsEnabled;
        }

        public FolderConfiguration(FolderConfiguration other)
        {
            ID = other.ID;
            IsWatched = other.IsWatched;
            NotificationsEnabled = other.NotificationsEnabled;
        }

        public override string ToString()
        {
            return $"<Folder ID={ID} IsWatched={IsWatched} NotificationsEnabled={NotificationsEnabled}>";
        }
    }
}
