using System.Collections.Generic;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalPrioritizationHookResponse
    {
        public string SelectedGuid { get; set; }
        public List<string> OrderedGuids { get; set; }
        public bool? Defer { get; set; }
        public int? DeferMinutes { get; set; }
        public string Reason { get; set; }
    }
}
