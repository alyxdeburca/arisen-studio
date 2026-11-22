using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Services.M7
{
    // M7 boundary: AutoUpdater (Autoupdater.NET.Official 1.9.2, AutoUpdaterDotNET).
    // The old UpdateExtensions.CheckApplicationVersion configured the static AutoUpdater class and started a check against
    // the feed URL Urls.UpdateData (Constants/Urls.cs in the old tree; not in the Mono tree yet, so the caller passes it).
    // The check result arrived on AutoUpdater.CheckForUpdateEvent as UpdateInfoEventArgs. Autoupdater.NET is a WinForms/WPF
    // library whose install step runs a Windows installer/zip updater, so it is not used here.
    // The old result handling (dialogs, status text) stays with the caller; this boundary only carries the data and the
    // two actions. Mono's existing Extensions/UpdateExtensions.cs keeps the version and GitHub release data and has no
    // updater code.

    /// <summary>
    /// Settings the old code gave AutoUpdater before Start. Defaults are the old values.
    /// </summary>
    public sealed class UpdateCheckOptions
    {
        /// <summary>Update feed URL (old: Urls.UpdateData). Required.</summary>
        public string FeedUrl { get; set; }

        /// <summary>Old: "Arisen Studio Auto Updater".</summary>
        public string HttpUserAgent { get; set; } = "Arisen Studio Auto Updater";

        /// <summary>Form that owns any updater dialog (old: AutoUpdater.SetOwner(MainWindow.Window)). Optional.</summary>
        public IWin32Window Owner { get; set; }

        /// <summary>Old: Properties.Resources.arisenstudio. Optional.</summary>
        public Icon Icon { get; set; }

        /// <summary>Old: false.</summary>
        public bool ShowSkipButton { get; set; }

        /// <summary>Old: false.</summary>
        public bool ShowRemindLaterButton { get; set; }

        /// <summary>Old: true. Let the updater errors surface instead of staying silent.</summary>
        public bool ReportErrors { get; set; } = true;

        /// <summary>Old: true. Start the installer elevated.</summary>
        public bool RunUpdateAsAdmin { get; set; } = true;
    }

    /// <summary>
    /// The outcome of a check (old UpdateInfoEventArgs: Error, IsUpdateAvailable, Mandatory, CurrentVersion,
    /// InstalledVersion, DownloadURL, ChangelogURL).
    /// </summary>
    public sealed class UpdateCheckResult
    {
        /// <summary>Null on success. The old code branched on "is it a WebException" for the message shown.</summary>
        public Exception Error { get; set; }

        public bool IsUpdateAvailable { get; set; }

        /// <summary>The old code read args.Mandatory.Value; false when the feed does not say.</summary>
        public bool Mandatory { get; set; }

        public Version LatestVersion { get; set; }

        public Version InstalledVersion { get; set; }

        public string DownloadUrl { get; set; }

        public string ChangelogUrl { get; set; }
    }

    /// <summary>
    /// Checks for a newer application version and installs it.
    /// </summary>
    public interface IApplicationUpdater
    {
        /// <summary>
        /// False for a stub. A caller can use this to skip the update UI instead of showing an error at startup.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Raised once per check with the result. May be raised on a worker thread; the handler marshals to the UI thread
        /// (the old event was raised by the library; the old handler touched the form directly).
        /// </summary>
        event Action<UpdateCheckResult> CheckCompleted;

        /// <summary>Start an asynchronous check. Returns at once; the result comes on CheckCompleted. Does not throw.</summary>
        void CheckForUpdate(UpdateCheckOptions options);

        /// <summary>
        /// Download and start the install of the update found by the last check (old AutoUpdater.DownloadUpdate(args)).
        /// True means the installer started and the caller should exit the app (the old code called Application.Exit()).
        /// False means nothing was started. Throws on a download error, as the old call did.
        /// </summary>
        bool DownloadAndInstall();
    }

    /// <summary>
    /// Stub: reports that no update is available and does nothing else. It does not raise an error, because the old caller
    /// checks at every startup and an error dialog on each start would be worse than silence.
    /// CheckCompleted is raised synchronously with IsUpdateAvailable false and Error null. DownloadAndInstall returns false.
    /// Default of M7Services.Updater.
    /// </summary>
    public sealed class NoOpApplicationUpdater : IApplicationUpdater
    {
        public bool IsSupported
        {
            get { return false; }
        }

        public event Action<UpdateCheckResult> CheckCompleted;

        public void CheckForUpdate(UpdateCheckOptions options)
        {
            Action<UpdateCheckResult> handler = CheckCompleted;
            if (handler != null)
            {
                handler(new UpdateCheckResult { IsUpdateAvailable = false });
            }
        }

        public bool DownloadAndInstall()
        {
            return false;
        }
    }
}
