using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class ConfigFolderDevice : IEquatable<ConfigFolderDevice>
    {
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; }

        public bool Equals(ConfigFolderDevice other)
        {
            return other != null && DeviceId == other.DeviceId;
        }

        public override string ToString()
        {
            return $"<Device deviceId={DeviceId}>";
        }
    }

    public class ConfigFolder : IEquatable<ConfigFolder>
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("devices")]
        public List<ConfigFolderDevice> Devices { get; set; }

        // This has changed type, and we don't use it anyway
        //[JsonProperty("type")]
        //public bool Type { get; set; }

        [JsonProperty("rescanIntervalS")]
        public long RescanIntervalSeconds { get; set; }

        public TimeSpan RescanInterval
        {
            get => TimeSpan.FromSeconds(RescanIntervalSeconds);
            set => RescanIntervalSeconds = (long)value.TotalSeconds;
        }

        [JsonProperty("invalid")]
        public string Invalid { get; set; }

        [JsonProperty("fsWatcherEnabled")]
        public bool IsFsWatcherEnabled { get; set; }

        public bool Equals(ConfigFolder other)
        {
            return other != null &&
                ID == other.ID &&
                Path == other.Path &&
                Devices.SequenceEqual(other.Devices) &&
                //this.Type == other.Type &&
                RescanIntervalSeconds == other.RescanIntervalSeconds &&
                Invalid == other.Invalid &&
                IsFsWatcherEnabled == other.IsFsWatcherEnabled;
        }

        public override string ToString()
        {
            return $"<Folder id={ID} label={Label} path={Path} devices=[{String.Join(", ", Devices)}] rescalinterval={RescanInterval} " +
                $"invalid={Invalid} fsWatcherEnabled={IsFsWatcherEnabled}>";
        }
    }

    public class ConfigDevice : IEquatable<ConfigDevice>
    {
        [JsonProperty("deviceID")]
        public string DeviceID { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("addresses")]
        public List<string> Addresses { get; set; }

        // Apparently this can be 'never'
        // We don't use it, so commenting until it decided what data type it wants to be
        //[JsonProperty("compression")]
        //public string Compression { get; set; }

        [JsonProperty("certName")]
        public string CertName { get; set; }

        [JsonProperty("introducer")]
        public bool IsIntroducer { get; set; }

        public bool Equals(ConfigDevice other)
        {
            return other != null &&
                DeviceID == other.DeviceID &&
                Name == other.Name &&
                Addresses.SequenceEqual(other.Addresses) &&
                CertName == other.CertName &&
                IsIntroducer == other.IsIntroducer;
        }

        public override string ToString()
        {
            return $"Device id={DeviceID} name={Name} addresses=[{String.Join(", ", Addresses)}] compression=N/A certname={CertName} isintroducer={IsIntroducer}>";
        }
    }

    public class Config : IEquatable<Config>
    {
        [JsonProperty("version")]
        public long Version { get; set; }

        [JsonProperty("folders")]
        public List<ConfigFolder> Folders { get; set; }

        [JsonProperty("devices")]
        public List<ConfigDevice> Devices { get; set; }

        public bool Equals(Config other)
        {
            return other != null &&
                Version == other.Version &&
                Folders.SequenceEqual(other.Folders) &&
                Devices.SequenceEqual(other.Devices);
        }

        public override string ToString()
        {
            return $"<Config version={Version} folders=[{String.Join(", ", Folders)}] devices=[{String.Join(", ", Devices)}]>";
        }
    }
}
