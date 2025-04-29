using Newtonsoft.Json;
using System;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class StateChangedEventData
    {
        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("duration")]
        public double DurationSeconds { get; set; }

        public TimeSpan Duration
        {
            get => TimeSpan.FromSeconds(DurationSeconds);
            set => DurationSeconds = value.TotalSeconds;
        }
    }

    public class StateChangedEvent : Event
    {
        [JsonProperty("data")]
        public StateChangedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Folder) &&
            !string.IsNullOrWhiteSpace(Data.From) &&
            !string.IsNullOrWhiteSpace(Data.To) &&
            Data.Duration >= TimeSpan.Zero;

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<StateChangedEvent ID={Id} Time={Time} Folder={Data.Folder} From={Data.From} To={Data.To} Duration={Data.Duration}>";
        }
    }
}
