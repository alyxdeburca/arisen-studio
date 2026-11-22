namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// The startup server-maintenance decision.
    /// Ported from MainWindow.Load (old source: Forms/Windows/MainWindow.cs:207-216).
    /// Pure: no UI, no I/O, no settings.
    /// </summary>
    public static class MaintenanceGate
    {
        /// <summary>
        /// Decides whether the maintenance message is shown and whether exit is requested.
        /// Old rule: the check only runs when an internet connection is detected, and maintenance applies
        /// when the status check did not return OK.
        /// </summary>
        /// <param name="internetAvailable">Result of the internet connection check (old: HttpExtensions.CheckInternetConnection).</param>
        /// <param name="statusOk">Result of <see cref="StartupStatusCheck.IsServerUpAsync"/>. Ignored when there is no internet.</param>
        public static MaintenanceDecision Decide(bool internetAvailable, bool statusOk)
        {
            bool maintenance = internetAvailable && !statusOk;
            return new MaintenanceDecision(maintenance, maintenance);
        }
    }

    /// <summary>
    /// Result of <see cref="MaintenanceGate.Decide"/>.
    /// <see cref="ShowMaintenanceMessage"/> and <see cref="RequestExit"/> are both true or both false.
    /// <see cref="RequestExit"/> only asks the caller to exit; it does not stop startup. The old code also
    /// did not return after Application.Exit(), so the caller decides what happens next.
    /// </summary>
    public sealed class MaintenanceDecision
    {
        public MaintenanceDecision(bool showMaintenanceMessage, bool requestExit)
        {
            ShowMaintenanceMessage = showMaintenanceMessage;
            RequestExit = requestExit;
        }

        public bool ShowMaintenanceMessage { get; private set; }

        public bool RequestExit { get; private set; }
    }
}
