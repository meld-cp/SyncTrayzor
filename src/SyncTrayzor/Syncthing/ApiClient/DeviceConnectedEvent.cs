using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DeviceConnectedEventData
    {
        [JsonProperty("addr")]
        public string Address { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class DeviceConnectedEvent : Event
    {
        [JsonProperty("data")]
        public DeviceConnectedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Address) &&
            !string.IsNullOrWhiteSpace(Data.Id);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<DeviceConnected ID={Id} Time={Time} Addr={Data.Address} Id={Data.Id}>";
        }
    }
}
