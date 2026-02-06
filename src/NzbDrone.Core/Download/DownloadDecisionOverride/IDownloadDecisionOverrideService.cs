using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.Download.DownloadDecisionOverride
{
    public interface IDownloadDecisionOverrideService
    {
        Task<DownloadDecisionOverrideResult> EvaluateAsync(
            Movie movie,
            List<DownloadDecision> prioritizedDecisions,
            CancellationToken cancellationToken = default);
    }

    public class DownloadDecisionOverrideResult
    {
        public bool ShouldProceed { get; set; } = true;
        public List<DownloadDecision> ModifiedDecisions { get; set; }
        public string RejectionReason { get; set; }
        public int? DeferMinutes { get; set; }

        public static DownloadDecisionOverrideResult Proceed(List<DownloadDecision> decisions)
        {
            return new DownloadDecisionOverrideResult
            {
                ShouldProceed = true,
                ModifiedDecisions = decisions
            };
        }

        public static DownloadDecisionOverrideResult Reject(string reason)
        {
            return new DownloadDecisionOverrideResult
            {
                ShouldProceed = false,
                RejectionReason = reason
            };
        }

        public static DownloadDecisionOverrideResult Defer(int minutes, string reason)
        {
            return new DownloadDecisionOverrideResult
            {
                ShouldProceed = false,
                DeferMinutes = minutes,
                RejectionReason = reason
            };
        }
    }
}
