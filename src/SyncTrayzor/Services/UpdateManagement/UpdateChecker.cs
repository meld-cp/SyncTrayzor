using NLog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SyncTrayzor.Services.UpdateManagement
{
    public class VersionCheckResults
    {
        public Version NewVersion { get; }
        public string DownloadUrl { get; }
        public string Sha512sumDownloadUrl { get; }
        public string ReleaseNotes { get; }
        public string ReleasePageUrl { get; }

        public VersionCheckResults(
            Version newVersion,
            string downloadUrl,
            string sha512sumDownloadUrl,
            string releaseNotes,
            string releasePageUrl)
        {
            NewVersion = newVersion;
            DownloadUrl = downloadUrl;
            Sha512sumDownloadUrl = sha512sumDownloadUrl;
            ReleaseNotes = releaseNotes;
            ReleasePageUrl = releasePageUrl;
        }

        public override string ToString()
        {
            return $"<VersionCheckResults NewVersion={NewVersion} DownloadUrl={DownloadUrl} Sha512sumDownloadUrl={Sha512sumDownloadUrl} " +
                $"ReleaseNotes={ReleaseNotes} ReleasePageUrl={ReleasePageUrl}>";
        }
    }

    public interface IUpdateChecker
    {
        Task<VersionCheckResults> FetchUpdateAsync();
        Task<VersionCheckResults> CheckForAcceptableUpdateAsync(Version latestIgnoredVersion = null);
    }

    public class UpdateChecker : IUpdateChecker
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly Dictionary<Architecture, string> processorArchitectureToStringMap = new()
        {
            { Architecture.X64, "x64" },
            { Architecture.X86, "x86" },
            { Architecture.Arm, "arm" },
            { Architecture.Armv6, "arm" },
            { Architecture.Arm64, "arm64" },
        };

        private readonly Version applicationVersion;
        private readonly Architecture processorArchitecture;
        private readonly string variant;
        private readonly IUpdateNotificationClient updateNotificationClient;

        public UpdateChecker(
            Version applicationVersion,
            Architecture processorArchitecture,
            string variant,
            IUpdateNotificationClient updateNotificationClient)
        {
            this.applicationVersion = applicationVersion;
            this.processorArchitecture = processorArchitecture;
            this.variant = variant;
            this.updateNotificationClient = updateNotificationClient;
        }

        public async Task<VersionCheckResults> FetchUpdateAsync()
        {
            // We don't care if we fail
            try
            {
                var update = await updateNotificationClient.FetchUpdateAsync(
                    applicationVersion.ToString(3),
                    processorArchitectureToStringMap[processorArchitecture],
                    variant);

                if (update == null)
                {
                    logger.Info("No updates found");
                    return null;
                }

                if (update.Error != null)
                {
                    logger.Warn("Update API returned an error. Code: {0} Message: {1}", update.Error.Code, update.Error.Message);
                    return null;
                }

                var updateData = update.Data;
                if (updateData == null)
                {
                    logger.Info("No updates available");
                    return null;
                }

                var results = new VersionCheckResults(updateData.Version, updateData.DirectDownloadUrl, update.Data.Sha512sumDownloadUrl, updateData.ReleaseNotes, updateData.ReleasePageUrl);
                logger.Info("Found new version: {0}", results);
                return results;
            }
            catch (Exception e)
            {
                logger.Warn(e, "Fetching updates failed with an error");
                return null;
            }
        }
        
        public async Task<VersionCheckResults> CheckForAcceptableUpdateAsync(Version latestIgnoredVersion)
        {
            var results = await FetchUpdateAsync();

            if (results == null)
                return null;

            if (results.NewVersion <= applicationVersion)
            {
                logger.Warn($"Found update, but it's <= the current application version ({applicationVersion}), so ignoring");
                return null;
            }

            if (latestIgnoredVersion != null && results.NewVersion <= latestIgnoredVersion)
            {
                logger.Info($"Found update, but it's <= the latest ignored version ({latestIgnoredVersion}), so ignoring");
                return null;
            }

            return results;
        }
    }
}
