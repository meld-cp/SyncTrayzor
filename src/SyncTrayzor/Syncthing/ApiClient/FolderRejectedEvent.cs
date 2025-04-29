using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class FolderRejectedEventData
    {
        [JsonProperty("device")]
        public string DeviceId { get; set; }

        [JsonProperty("folder")]
        public string FolderId { get; set; }
    }

    public class FolderRejectedEvent : Event
    {
        [JsonProperty("data")]
        public FolderRejectedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.DeviceId) &&
            !string.IsNullOrWhiteSpace(Data.FolderId);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<FolderRejected ID={Id} Time={Time} DeviceId={Data.DeviceId} FolderId={Data.FolderId}>";
        }
    }
}
