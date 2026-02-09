using System.Collections.Generic;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalRejectionHookResponse
    {
        public List<ExternalRejectionDecision> Decisions { get; set; }
    }

    public class ExternalRejectionDecision
    {
        public string Guid { get; set; }
        public bool Rejected { get; set; }
        public string Reason { get; set; }
    }
}
