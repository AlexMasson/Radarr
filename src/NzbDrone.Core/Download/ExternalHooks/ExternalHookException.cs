using System;

namespace NzbDrone.Core.Download.ExternalHooks
{
    public class ExternalHookException : Exception
    {
        public ExternalHookException(string message)
            : base(message)
        {
        }

        public ExternalHookException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
