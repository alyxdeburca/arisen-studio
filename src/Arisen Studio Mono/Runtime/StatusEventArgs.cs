using System;

namespace ArisenStudio.Runtime
{
    /// <summary>
    /// Carries a status message (and optionally the exception behind it) from <see cref="AppHost" /> to the UI.
    /// </summary>
    public class StatusEventArgs : EventArgs
    {
        public StatusEventArgs(string status, Exception exception)
        {
            Status = status;
            Exception = exception;
        }

        /// <summary>
        /// The status text to show.
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// The exception behind the status, or null when there is none.
        /// </summary>
        public Exception Exception { get; private set; }
    }
}
