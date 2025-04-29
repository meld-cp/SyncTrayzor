using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class FolderSummaryEventData
    {
        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("summary")]
        public FolderStatus Summary { get; set; }
    }

    public class FolderSummaryEvent : Event
    {
        [JsonProperty("data")]
        public FolderSummaryEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Folder);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<FolderSummary ID={Id} Time={Time} Folder={Data.Folder} Summary={Data.Summary}>";
        }
    }
}
