using Newtonsoft.Json;
namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DeviceRejectedEventData
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("device")]
        public string DeviceId { get; set; }
    }

    public class DeviceRejectedEvent : Event
    {
        [JsonProperty("data")]
        public DeviceRejectedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Address) &&
            !string.IsNullOrWhiteSpace(Data.DeviceId);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<DeviceRejected ID={Id} Time={Time} Address={Data.Address} DeviceId={Data.DeviceId}>";
        }
    }
}
