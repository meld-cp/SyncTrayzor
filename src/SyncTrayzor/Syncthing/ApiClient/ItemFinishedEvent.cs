using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class ItemFinishedEventData
    {
        [JsonProperty("item")]
        public string Item { get; set; }

        [JsonProperty("folder")]
        public string Folder { get; set; }

        // Irritatingly, 'error' is currently a structure containing an 'Err' property,
        // but in the future may just become a string....

        [JsonProperty("error")]
        public JToken ErrorRaw { get; set; }

        public string Error
        {
            get
            {
                if (ErrorRaw == null)
                    return null;
                if (ErrorRaw.Type == JTokenType.String)
                    return (string)ErrorRaw;
                if (ErrorRaw.Type == JTokenType.Object)
                    return (string)((JObject)ErrorRaw)["Err"];
                return null;
            }
        }

        [JsonProperty("type")]
        public ItemChangedItemType Type { get; set; }

        [JsonProperty("action")]
        public ItemChangedActionType Action { get; set; }
    }

    public class ItemFinishedEvent : Event
    {
        [JsonProperty("data")]
        public ItemFinishedEventData Data { get; set; }

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
            return $"<ItemFinished ID={Id} Time={Time} Item={Data.Item} Folder={Data.Folder} Error={Data.Error}>";
        }
    }
}
