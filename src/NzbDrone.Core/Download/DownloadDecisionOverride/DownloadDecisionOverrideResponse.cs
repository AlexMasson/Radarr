using System.Collections.Generic;

namespace NzbDrone.Core.Download.DownloadDecisionOverride
{
    public class DownloadDecisionOverrideResponse
    {
        /// <summary>
        /// Whether to proceed with the grab. False = reject all releases for this movie.
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// Optional: GUID of a specific release to grab instead of the default selection.
        /// </summary>
        public string SelectedReleaseGuid { get; set; }

        /// <summary>
        /// Optional: Reason for rejection (logged and stored in history).
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Optional: If rejected, defer the grab for this many minutes before retrying.
        /// </summary>
        public int? DeferMinutes { get; set; }

        /// <summary>
        /// Optional: Reorder releases by GUID. First entry becomes the new top priority.
        /// </summary>
        public List<string> ReleaseOrder { get; set; }
    }
}
