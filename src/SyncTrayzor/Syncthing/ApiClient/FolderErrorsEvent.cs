using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class FolderErrorData
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        public override string ToString()
        {
            return $"<FolderError Error={Error} Path={Path}>";
        }
    }

    public class FolderErrorsEventData
    {
        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("errors")]
        public List<FolderErrorData> Errors { get; set; }
    }

    public class FolderErrorsEvent : Event
    {
        [JsonProperty("data")]
        public FolderErrorsEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Folder) &&
            Data.Errors != null;

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<FolderErrors ID={Id} Time={Time} Folder={Data.Folder} Errors=[{String.Join(", ", Data.Errors.ToString())}]>";
        }
    }
}
