using NzbDrone.Core.Configuration;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Config
{
    public class DownloadClientConfigResource : RestResource
    {
        public string DownloadClientWorkingFolders { get; set; }

        public bool EnableCompletedDownloadHandling { get; set; }
        public int CheckForFinishedDownloadInterval { get; set; }

        public bool AutoRedownloadFailed { get; set; }
        public bool AutoRedownloadFailedFromInteractiveSearch { get; set; }

        // External Hooks
        public bool ExternalRejectionHookEnabled { get; set; }
        public string ExternalRejectionHookUrl { get; set; }
        public int ExternalRejectionHookTimeout { get; set; }
        public bool ExternalPrioritizationHookEnabled { get; set; }
        public string ExternalPrioritizationHookUrl { get; set; }
        public int ExternalPrioritizationHookTimeout { get; set; }
        public string ExternalHooksUsername { get; set; }
        public string ExternalHooksPassword { get; set; }
        public string ExternalHooksSkipTag { get; set; }
    }

    public static class DownloadClientConfigResourceMapper
    {
        public static DownloadClientConfigResource ToResource(IConfigService model)
        {
            return new DownloadClientConfigResource
            {
                DownloadClientWorkingFolders = model.DownloadClientWorkingFolders,

                EnableCompletedDownloadHandling = model.EnableCompletedDownloadHandling,
                CheckForFinishedDownloadInterval = model.CheckForFinishedDownloadInterval,

                AutoRedownloadFailed = model.AutoRedownloadFailed,
                AutoRedownloadFailedFromInteractiveSearch = model.AutoRedownloadFailedFromInteractiveSearch,

                // External Hooks
                ExternalRejectionHookEnabled = model.ExternalRejectionHookEnabled,
                ExternalRejectionHookUrl = model.ExternalRejectionHookUrl,
                ExternalRejectionHookTimeout = model.ExternalRejectionHookTimeout,
                ExternalPrioritizationHookEnabled = model.ExternalPrioritizationHookEnabled,
                ExternalPrioritizationHookUrl = model.ExternalPrioritizationHookUrl,
                ExternalPrioritizationHookTimeout = model.ExternalPrioritizationHookTimeout,
                ExternalHooksUsername = model.ExternalHooksUsername,
                ExternalHooksPassword = model.ExternalHooksPassword,
                ExternalHooksSkipTag = model.ExternalHooksSkipTag
            };
        }
    }
}
