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

        // Download Decision Override
        public bool DownloadDecisionOverrideEnabled { get; set; }
        public string DownloadDecisionOverrideUrl { get; set; }
        public int DownloadDecisionOverrideTimeout { get; set; }
        public string DownloadDecisionOverrideUsername { get; set; }
        public string DownloadDecisionOverridePassword { get; set; }
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

                // Download Decision Override
                DownloadDecisionOverrideEnabled = model.DownloadDecisionOverrideEnabled,
                DownloadDecisionOverrideUrl = model.DownloadDecisionOverrideUrl,
                DownloadDecisionOverrideTimeout = model.DownloadDecisionOverrideTimeout,
                DownloadDecisionOverrideUsername = model.DownloadDecisionOverrideUsername,
                DownloadDecisionOverridePassword = model.DownloadDecisionOverridePassword
            };
        }
    }
}
