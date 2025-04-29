using Newtonsoft.Json;
using System;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class FolderStatus
    {
        [JsonProperty("globalBytes")]
        public long GlobalBytes { get; set; }

        [JsonProperty("globalDeleted")]
        public int GlobalDeleted { get; set; }

        [JsonProperty("globalFiles")]
        public int GlobalFiles { get; set; }

        [JsonProperty("localBytes")]
        public long LocalBytes { get; set; }

        [JsonProperty("localDeleted")]
        public int LocalDeleted { get; set; }

        [JsonProperty("localFiles")]
        public int LocalFiles { get; set; }

        [JsonProperty("inSyncBytes")]
        public long InSyncBytes { get; set; }

        [JsonProperty("inSyncFiles")]
        public int InSyncFiles { get; set; }

        [JsonProperty("needBytes")]
        public long NeedBytes { get; set; }

        [JsonProperty("needFiles")]
        public int NeedFiles { get; set; }

        [JsonProperty("invalid")]
        public string Invalid { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("stateChanged")]
        public DateTime StateChanged { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        public override string ToString()
        {
            return $"<FolderStatus GlobalBytes={GlobalBytes} GlobalDeleted={GlobalDeleted} GlobalFiles={GlobalFiles} " +
                $"LocalBytes={LocalBytes} LocalDeleted={LocalDeleted} LocalFiles={LocalFiles} " +
                $"InSyncBytes={InSyncBytes} InSyncFiles={InSyncFiles} NeedBytes={NeedBytes} NeedFiles={NeedFiles} " +
                $"Invalid={Invalid} State={State} StateChanged={StateChanged} Version={Version}>";
        }
    }
}
