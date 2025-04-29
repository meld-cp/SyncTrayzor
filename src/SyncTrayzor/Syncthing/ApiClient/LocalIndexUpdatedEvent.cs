using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class LocalIndexUpdatedEventData
    {
        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("items")]
        public int Items { get; set; }

        [JsonProperty("version")]
        public long Version { get; set; }
    }

    public class LocalIndexUpdatedEvent : Event
    {
        [JsonProperty("data")]
        public LocalIndexUpdatedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Folder) &&
            Data.Items >= 0;

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<LocalIndexUpdated ID={Id} Time={Time} Folder={Data.Folder} Items={Data.Items} Version={Data.Version}>";
        }
    }
}
