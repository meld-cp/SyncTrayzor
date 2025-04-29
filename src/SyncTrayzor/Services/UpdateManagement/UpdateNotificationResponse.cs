using Newtonsoft.Json;
using System;

namespace SyncTrayzor.Services.UpdateManagement
{
    public class UpdateNotificationData
    {
        [JsonProperty("version")]
        public string VersionRaw { get; set; }

        public Version Version
        {
            get => String.IsNullOrWhiteSpace(VersionRaw) ? null : new Version(VersionRaw);
            set => VersionRaw = value.ToString(3);
        }

        [JsonProperty("direct_download_url")]
        public string DirectDownloadUrl { get; set; }

        [JsonProperty("sha512sum_download_url")]
        public string Sha512sumDownloadUrl { get; set; }

        [JsonProperty("release_page_url")]
        public string ReleasePageUrl { get; set; }

        [JsonProperty("release_notes")]
        public string ReleaseNotes { get; set; }

        public override string ToString()
        {
            return $"<UpdateNotificationData version={Version.ToString(3)} direct_download_url={DirectDownloadUrl} sha512sum_download_url={Sha512sumDownloadUrl} " +
                $"release_page_url={ReleasePageUrl} release_notes={ReleaseNotes}>";
        }
    }

    public class UpdateNotificationError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        public override string ToString()
        {
            return $"<UpdateNotificationError code={Code} message={ Message}>";
        }
    }

    public class UpdateNotificationResponse
    {
        [JsonProperty("data")]
        public UpdateNotificationData Data { get; set; }

        [JsonProperty("error")]
        public UpdateNotificationError Error { get; set; }

        public override string ToString()
        {
            return $"<UpdateNotificationResponse data={Data} error={Error}>";
        }
    }
}
