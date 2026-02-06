using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.Download.DownloadDecisionOverride
{
    public class DownloadDecisionOverrideException : NzbDroneException
    {
        public DownloadDecisionOverrideException(string message)
            : base(message)
        {
        }

        public DownloadDecisionOverrideException(string message, params object[] args)
            : base(message, args)
        {
        }

        public DownloadDecisionOverrideException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
