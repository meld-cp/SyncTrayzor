using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class RemoteIndexUpdatedEventData
    {
        [JsonProperty("device")]
        public string Device { get; set; }

        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("items")]
        public long Items { get; set; }
    }

    public class RemoteIndexUpdatedEvent : Event
    {
        [JsonProperty("data")]
        public RemoteIndexUpdatedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Device) &&
            !string.IsNullOrWhiteSpace(Data.Folder) &&
            Data.Items >= 0;

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<RemoteIndexUpdatedEvent ID={Id} Time={Time} Device={Data.Device} Folder={Data.Folder} Items={Data.Items}>";
        }
    }
}
