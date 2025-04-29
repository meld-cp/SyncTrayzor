using System;

namespace SyncTrayzor.Syncthing
{
    public interface ISyncthingCapabilities
    {
        bool SupportsDebugFacilities { get; }
        bool SupportsDevicePauseResume { get; }
    }

    public class SyncthingCapabilities : ISyncthingCapabilities
    {
        private static readonly Version debugFacilitiesIntroduced = new(0, 12, 0);
        private static readonly Version devicePauseResumeIntroduced = new(0, 12, 0);

        public Version SyncthingVersion { get; set; } = new(0, 0, 0);

        public bool SupportsDebugFacilities => SyncthingVersion >= debugFacilitiesIntroduced;
        public bool SupportsDevicePauseResume => SyncthingVersion >= devicePauseResumeIntroduced;
    }
}
