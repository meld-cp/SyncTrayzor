using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DevicePausedEventData
    {
        [JsonProperty("device")]
        public string DeviceId { get; set; }
    }

    public class DevicePausedEvent : Event
    {
        [JsonProperty("data")]
        public DevicePausedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.DeviceId);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<DevicePaused ID={Id} Time={Time} DeviceId={Data.DeviceId}>";
        }
    }
}
