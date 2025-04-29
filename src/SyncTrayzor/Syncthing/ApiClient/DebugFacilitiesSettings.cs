using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DebugFacilitiesSettings
    {
        [JsonProperty("enabled")]
        public List<string> Enabled { get; set; }

        [JsonProperty("facilities")]
        public Dictionary<string, string> Facilities { get; set; }

        public override string ToString()
        {
            var facilities = String.Join(",", Facilities?.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var enabled = (Enabled == null) ? String.Empty : String.Join(",", Enabled);
            return $"<DebugFacilities Enabled={enabled} Facilities={facilities}>";
        }
    }
}
