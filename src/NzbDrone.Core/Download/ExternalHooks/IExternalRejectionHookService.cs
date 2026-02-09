using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalRejectionResult
    {
        public List<DownloadDecision> Accepted { get; set; }
        public List<DownloadDecision> Rejected { get; set; }

        public ExternalRejectionResult(List<DownloadDecision> accepted, List<DownloadDecision> rejected)
        {
            Accepted = accepted;
            Rejected = rejected;
        }
    }

    public interface IExternalRejectionHookService
    {
        Task<ExternalRejectionResult> EvaluateAsync(List<DownloadDecision> decisions);
    }
}
