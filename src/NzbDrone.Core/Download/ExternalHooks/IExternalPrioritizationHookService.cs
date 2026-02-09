using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalPrioritizationResult
    {
        public bool ShouldProceed { get; set; }
        public List<DownloadDecision> ModifiedDecisions { get; set; }
        public int? DeferMinutes { get; set; }
        public string Reason { get; set; }

        public static ExternalPrioritizationResult Proceed(List<DownloadDecision> decisions)
        {
            return new ExternalPrioritizationResult
            {
                ShouldProceed = true,
                ModifiedDecisions = decisions
            };
        }

        public static ExternalPrioritizationResult Defer(int minutes, string reason)
        {
            return new ExternalPrioritizationResult
            {
                ShouldProceed = false,
                DeferMinutes = minutes,
                Reason = reason
            };
        }
    }

    public interface IExternalPrioritizationHookService
    {
        Task<ExternalPrioritizationResult> EvaluateAsync(List<DownloadDecision> decisions);
    }
}
