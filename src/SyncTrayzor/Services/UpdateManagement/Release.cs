using System;

namespace SyncTrayzor.Services.UpdateManagement
{
    public class Release
    {
        public Version Version { get; }
        public string DownloadUrl { get; }
        public string Body { get; }

        public Release(Version version, string downloadUrl, string body)
        {
            Version = version;
            DownloadUrl = downloadUrl;
            Body = body;
        }
    }
}
