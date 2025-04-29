using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DeviceResumedEventData
    {
        [JsonProperty("device")]
        public string DeviceId { get; set; }
    }

    public class DeviceResumedEvent : Event
    {
        [JsonProperty("data")]
        public DevicePausedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrEmpty(Data.DeviceId);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<DeviceResumed ID={Id} Time={Time} DeviceId={Data.DeviceId}>";
        }
    }
}
