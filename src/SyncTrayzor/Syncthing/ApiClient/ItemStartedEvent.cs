using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class ItemStartedEventData
    {
        [JsonProperty("item")]
        public string Item { get; set; }

        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("type")]
        public ItemChangedItemType Type { get; set; }

        [JsonProperty("action")]
        public ItemChangedActionType Action { get; set; }
    }

    public class ItemStartedEvent : Event
    {
        [JsonProperty("data")]
        public ItemStartedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Item) &&
            !string.IsNullOrWhiteSpace(Data.Folder) &&
            Data.Type != ItemChangedItemType.Unknown &&
            Data.Action != ItemChangedActionType.Unknown;

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<ItemStarted ID={Id} Time={Time} Item={Data.Item} Folder={Data.Folder} Type={Data.Type} Action={Data.Action}>";
        }
    }
}
