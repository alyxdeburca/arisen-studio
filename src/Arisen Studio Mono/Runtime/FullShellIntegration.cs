using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Io;
using ArisenStudio.Localization;
using ArisenStudio.Models.Release_Data;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime.Startup;
using ArisenStudio.Services;
using ArisenStudio.Services.Adapters;
using ArisenStudio.Shell;
using ArisenStudio.Shell.Wiring;
using FluentFTP;
using Newtonsoft.Json;

namespace ArisenStudio.Runtime
{
    /// <summary>
    /// One call that wires the whole application: <see cref="AppBootstrapper.Initialize" /> (log and settings), the
    /// adapters of Services/Adapters, a <see cref="ShellServices" /> and <see cref="DialogContext" /> built from them,
    /// and <see cref="ShellController.Attach" />.
    /// <code>
    /// using (ShellForm shell = new ShellForm())
    /// {
    ///     FullShellIntegration.Attach(shell, smoke);   // instead of AppBootstrapper.AttachToShell(shell, smoke)
    ///     Application.Run(shell);
    /// }
    /// </code>
    /// Why this does not call <see cref="AppBootstrapper.AttachToShell" />: that method builds its own ShellServices (no
    /// adapters, no dialog context) and has no parameter to replace it, and ShellController.Attach may run only once per
    /// shell. So this class calls <see cref="AppBootstrapper.Initialize" /> (public, idempotent) and repeats the small
    /// remainder of AttachToShell (save settings on close, smoke rethrow, attach-failure status). Use this OR
    /// AttachToShell, never both. <see cref="AppBootstrapper.Controller" /> stays null; use <see cref="Controller" /> here.
    /// <para>
    /// Smoke mode is offline by construction. Nothing here connects, downloads or starts a task. The catalog and the
    /// feeds are empty (no database load, no release check, no news), the FTP client provider answers null (so FTP work
    /// fails with "There is no FTP connection to the console."), and the screenshot uploader has no endpoint. In smoke
    /// mode nothing is read from or written to the real user folder (backup records stay in memory), no HttpClient is
    /// created, and none of the network or process-wide startup behaviours below run.
    /// </para>
    /// <para>
    /// Normal mode also runs the startup behaviours (<c>Runtime/Startup</c>), each with its own catch-all that logs
    /// through <see cref="AppBootstrapper.LogError" />: the single-instance guard (first, in <see cref="Attach" />),
    /// then, once the shell is shown, the server status check with the maintenance gate and the What's New dialog (one
    /// network request, plus a release-list fetch while the What's New flag is set), the idle timer, and the
    /// current-game poll (inert until a console connection exists). The idle timer runs in both modes; it does no I/O.
    /// </para>
    /// UI thread only. Nothing was compiled or run.
    /// </summary>
    internal static class FullShellIntegration
    {
        private static ShellController _controller;
        private static AdapterContext _adapterContext;
        private static DialogContext _dialogContext;
        private static ShellServices _services;
        private static BackupFilesData _backupFiles;
        private static string _backupFilesPath;

        // Startup behaviours. All touched on the UI thread only (Attach, FormClosed), except where a handler says otherwise.
        private static SingleInstanceGuard _guard;
        private static ShellForm _startupShell;
        private static IdleStatusTimer _idle;
        private static EventHandler<StatusEventArgs> _idleStatusHandler;
        private static CurrentGamePoller _poller;
        private static IConsoleConnection _pollConnection;
        private static EventHandler _pollConnectionChanged;

        // Old behaviour (MainWindow.cs:215): the maintenance message is followed by Application.Exit(). Q-010.
        private const bool ExitOnMaintenance = true;

        /// <summary>True when <see cref="Attach" /> found another running instance (normal mode only). The shell then closes itself once shown.</summary>
        public static bool AnotherInstanceRunning { get; private set; }

        /// <summary>The controller created by <see cref="Attach" />, or null.</summary>
        public static ShellController Controller
        {
            get { return _controller; }
        }

        /// <summary>The adapter context shared by the adapters. Null before <see cref="Attach" />. The host fills its Catalog after a database load.</summary>
        public static AdapterContext Adapters
        {
            get { return _adapterContext; }
        }

        /// <summary>The services given to the controller. Null before <see cref="Attach" />.</summary>
        public static ShellServices Services
        {
            get { return _services; }
        }

        /// <summary>The dialog context given to the controller. Null before <see cref="Attach" />.</summary>
        public static DialogContext Dialogs
        {
            get { return _dialogContext; }
        }

        /// <summary>
        /// Wires the application to the shell. Call once, on the UI thread, after <c>new ShellForm()</c> and before
        /// <c>Application.Run</c>. Normal mode: an attach failure is logged and shown in the status strip, and null is
        /// returned. Smoke mode: the failure is rethrown.
        /// </summary>
        /// <returns>The controller, or null when it could not be attached.</returns>
        public static ShellController Attach(ShellForm shell, bool smokeMode)
        {
            if (shell == null)
            {
                throw new ArgumentNullException("shell");
            }
            if (_controller != null && _controller.IsAttached)
            {
                throw new InvalidOperationException("FullShellIntegration is already attached to a shell.");
            }

            // Startup 1: single-instance guard, taken before anything else. Normal mode only: smoke runs may be started in
            // parallel and must not touch a process-wide object. A failure of the guard itself counts as "not a duplicate",
            // so the app still launches; it is logged once the logger exists.
            SingleInstanceGuard guard = null;
            bool duplicate = false;
            Exception guardError = null;
            if (!smokeMode)
            {
                try
                {
                    guard = new SingleInstanceGuard();
                    if (!guard.TryAcquire())
                    {
                        duplicate = true;
                        guard.Dispose();
                        guard = null;
                    }
                }
                catch (Exception ex)
                {
                    guardError = ex;
                    guard = null;
                }
            }

            // Log, AppHost.Logger and AppHost.Settings (real user folder, or a temp folder in smoke mode).
            AppBootstrapper.Initialize(smokeMode);

            if (guardError != null)
            {
                AppBootstrapper.LogError(guardError, "The single-instance guard failed; starting without it.");
            }

            if (duplicate)
            {
                // Another instance is running. Report it and close this shell once it is shown (the old app exited here too,
                // silently). Returns before the save-on-close hook and before the controller attaches, so this instance never
                // rewrites the settings file or opens the first-run wizard.
                AnotherInstanceRunning = true;
                AppBootstrapper.LogError(null, "Another instance of Arisen Studio is already running; this instance exits.");
                try
                {
                    Console.Error.WriteLine("Arisen Studio is already running.");
                }
                catch (Exception)
                {
                }

                shell.Shown += delegate
                {
                    try
                    {
                        MessageBox.Show(shell, "Arisen Studio is already running.", "Arisen Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        AppBootstrapper.LogError(ex, "Unable to show the already-running message.");
                    }

                    try
                    {
                        if (!shell.IsDisposed)
                        {
                            shell.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        AppBootstrapper.LogError(ex, "Unable to close the duplicate instance.");
                    }
                };
                return null;
            }

            // The guard (null in smoke mode or when it failed) is held until the shell closes or is disposed, on this (the acquiring)
            // thread, and released even when the controller fails to attach. The same cleanup stops the idle timer and the poll.
            _guard = guard;
            _startupShell = shell;
            shell.FormClosed += OnShellClosedCleanup;
            shell.Disposed += OnShellDisposedCleanup;

            string settingsPath = AppBootstrapper.SettingsPath;

            // Saves the settings on close even when the controller never attached (same as AppBootstrapper.AttachToShell).
            FormClosingEventHandler saveOnClose = null;
            FormClosedEventHandler unhook = null;
            saveOnClose = delegate (object sender, FormClosingEventArgs e)
            {
                if (!e.Cancel)
                {
                    AppHost.SaveSettings(settingsPath);
                }
            };
            unhook = delegate (object sender, FormClosedEventArgs e)
            {
                shell.FormClosing -= saveOnClose;
                shell.FormClosed -= unhook;
            };
            shell.FormClosing += saveOnClose;
            shell.FormClosed += unhook;

            ShellController controller = new ShellController();
            try
            {
                _backupFilesPath = smokeMode ? null : UserFolders.BackupFilesData;
                _backupFiles = LoadBackupFiles(_backupFilesPath);

                AdapterContext adapters = BuildAdapterContext(settingsPath);
                DialogContext dialogs = BuildDialogContext(adapters, smokeMode);

                ShellServices services = new ShellServices
                {
                    // Pages. Each adapter reads the catalog and the settings through the shared context.
                    Dashboard = new DashboardDataSource(adapters, new DashboardFeeds()),
                    Downloads = new DownloadsDataSource(adapters),
                    InstalledFiles = new InstalledFilesDataSource(adapters),
                    Library = new LibraryDataSource(adapters),

                    // Connection stays null (NullConsoleConnection): no IConsoleConnection implementation exists yet.
                    // Lookup stays null: no IShellLookup implementation exists yet (the three "find by id" clicks report "not available").
                    Dialogs = dialogs,

                    SettingsPath = settingsPath,
                    LoadSettingsOnAttach = false, // AppBootstrapper.Initialize already loaded AppHost.Settings.
                    Localizer = ResourceText.Localizer,
                    DiscordAvailable = false,
                    RunSetupWizardOnFirstUse = !smokeMode
                    // StartupAsync stays null: the database load and the release check are network work and are not wired here.
                    // SwitchLanguage stays null: the controller only stores Settings.Language and saves.
                };

                controller.Attach(shell, services);

                _adapterContext = adapters;
                _dialogContext = dialogs;
                _services = services;
                _controller = controller;

                // Startup 2 to 5. Never throws: every step catches and logs its own failure. Skipped when the attach above fails.
                StartStartupBehaviours(shell, smokeMode, controller);
                return controller;
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Unable to attach the full shell integration.");
                if (smokeMode)
                {
                    throw;
                }

                try
                {
                    AppHost.SetStatus("Unable to bind the pages: " + ex.Message, ex);
                    shell.SetStatusText("Unable to bind the pages: " + ex.Message);
                }
                catch (Exception)
                {
                }
                return null;
            }
        }

        // ====================================================================================================
        // Startup behaviours (Runtime/Startup). Step 1, the single-instance guard, is in Attach.
        // ====================================================================================================

        private const string MaintenanceMessageEnglish =
            "It looks like our servers are currently undergoing maintenance. Please try again in a little while.\n\n"
            + "If this issue persists after 30 minutes, please contact our support team to report the problem. Thank you for your patience and understanding!";

        private static void StartStartupBehaviours(ShellForm shell, bool smokeMode, ShellController controller)
        {
            // Startup 2 and 3: server status check, maintenance gate, What's New. Network work, so normal mode only.
            // Runs from the shell's Shown event (async), so the UI is never blocked by the request (up to 100 s).
            RunStep("status check and What's New hook", delegate
            {
                if (!smokeMode)
                {
                    shell.Shown += OnShellShownStartup;
                }
            });

            // Startup 4: idle timer. Both modes (no I/O).
            RunStep("idle timer", delegate { StartIdleTimer(shell); });

            // Startup 5: current-game poll. Normal mode only.
            RunStep("current-game poll", delegate
            {
                if (!smokeMode)
                {
                    StartCurrentGamePoll(shell, controller);
                }
            });
        }

        private static void RunStep(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Startup step failed (" + name + ").");
            }
        }

        // Runs on the UI thread after the controller's own Shown handler (so the first-run wizard comes first).
        private static async void OnShellShownStartup(object sender, EventArgs e)
        {
            ShellForm shell = sender as ShellForm;
            if (shell == null)
            {
                return;
            }

            try
            {
                shell.Shown -= OnShellShownStartup;

                // Old rule (MainWindow.cs:203): everything below sits inside the "internet connection detected" branch.
                bool internet = false;
                RunStep("internet check", delegate { internet = ArisenStudio.Extensions.HttpExtensions.CheckInternetConnection(); });
                if (!internet || shell.IsDisposed)
                {
                    return;
                }

                // Startup 2: status check and maintenance gate.
                bool exit = false;
                try
                {
                    bool statusOk = await StartupStatusCheck.IsServerUpAsync(
                        null,
                        System.Threading.CancellationToken.None,
                        delegate (string message) { AppBootstrapper.LogError(null, message); });

                    if (shell.IsDisposed)
                    {
                        return;
                    }

                    MaintenanceDecision decision = MaintenanceGate.Decide(true, statusOk);
                    if (decision.ShowMaintenanceMessage)
                    {
                        string text = ResourceText.Get("LABEL_SERVER_MAINTENANCE_MESSAGE", MaintenanceMessageEnglish);
                        string caption = ResourceText.Get("LABEL_SERVER_MAINTENANCE", "Server Maintenance");
                        AppHost.SetStatus(text);
                        MessageBox.Show(shell, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        exit = decision.RequestExit && ExitOnMaintenance;
                    }
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "The server status check failed.");
                }

                if (exit)
                {
                    RunStep("close after maintenance message", delegate
                    {
                        if (!shell.IsDisposed)
                        {
                            shell.Close();
                        }
                    });
                    return;
                }

                if (shell.IsDisposed)
                {
                    return;
                }

                // Startup 3: What's New, once (see ShowWhatsNewAsync).
                try
                {
                    await ShowWhatsNewAsync(shell);
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "The What's New dialog failed.");
                }
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "The startup status step failed.");
            }
        }

        private static async Task ShowWhatsNewAsync(ShellForm shell)
        {
            if (!WhatsNewPolicy.ShouldShow(true, AppHost.Settings.FirstTimeOpenAfterUpdate))
            {
                return;
            }

            GitHubReleaseData release = FirstRelease(ArisenStudio.Extensions.UpdateExtensions.AllReleases);
            if (release == null)
            {
                // Mono never assigns UpdateExtensions.AllReleases, so fetch the list here, off the UI thread (one extra
                // network request, normal mode only). Not stored back: the flag logic below needs only the newest release.
                try
                {
                    IList<GitHubReleaseData> fetched = await Task.Run(
                        () => ArisenStudio.Extensions.UpdateExtensions.GetAllReleasesData(ArisenStudio.Extensions.HttpExtensions.GetStream));
                    release = FirstRelease(fetched);
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "Unable to load the release list for the What's New dialog.");
                    return;
                }
            }

            if (shell.IsDisposed)
            {
                return;
            }

            string body = release == null ? null : WhatsNewPolicy.FormatBody(release.Body);
            if (release == null || body == null)
            {
                // Nothing to show: keep the flag, so the "once" is not spent on an empty dialog.
                AppBootstrapper.LogError(null, "No release text is available for the What's New dialog; it stays pending.");
                return;
            }

            // The old code cleared the flag before showing the dialog. The settings are saved when the shell closes.
            AppHost.Settings.FirstTimeOpenAfterUpdate = WhatsNewPolicy.FlagAfterShow();

            using (WhatsNewDialog dialog = new WhatsNewDialog(WhatsNewPolicy.Title, WhatsNewPolicy.FormatSubtitle(release.Name, release.PublishedAt), body))
            {
                // Old ShowDataViewDialog also set MaximumSize and a size bump; DataViewDialog.OnLoad already sizes the form
                // to the text and caps its height at 90% of the work area, so they are left out.
                dialog.ShowDialog(shell);
            }
        }

        private static GitHubReleaseData FirstRelease(IList<GitHubReleaseData> releases)
        {
            return releases != null && releases.Count > 0 ? releases[0] : null;
        }

        private static void StartIdleTimer(ShellForm shell)
        {
            StopIdleTimer();

            string idling = ResourceText.Get("STATUS_IDLING", "Idling...");
            IdleStatusTimer timer = new IdleStatusTimer();

            // Idle is raised on a thread-pool thread. AppHost.StatusChanged is static and has other subscribers, so hop to the
            // UI thread before raising it.
            timer.Idle += delegate
            {
                PostToShell(shell, delegate { AppHost.SetStatus(idling); });
            };

            // The old timer restarted only when SetStatus got an exception and a text other than the idle text.
            EventHandler<StatusEventArgs> onStatus = delegate (object sender, StatusEventArgs e)
            {
                try
                {
                    timer.RestartIfNeeded(e.Status, idling, e.Exception);
                }
                catch (Exception)
                {
                }
            };

            AppHost.StatusChanged += onStatus;
            _idleStatusHandler = onStatus;
            _idle = timer;
            timer.Start();
        }

        private static void StartCurrentGamePoll(ShellForm shell, ShellController controller)
        {
            StopCurrentGamePoll();

            IConsoleConnection connection = controller.Connection;
            if (connection == null)
            {
                return;
            }

            CurrentGamePoller poller = new CurrentGamePoller(
                delegate
                {
                    ConsoleProfile profile = connection.Profile;
                    return profile == null ? null : profile.Address;
                },
                delegate { return connection.WebManInstalled; });

            poller.GameRead += delegate (object sender, CurrentGameEventArgs e)
            {
                string caption = e.Caption;
                PostToShell(shell, delegate { shell.SetCurrentGame(caption); });
            };

            // Old rule: poll only while a console is connected and "always show the current game" is on. PS3 (webMAN) only:
            // the Xbox branch of the old poll is not ported, and PS4 has no webMAN.
            EventHandler apply = delegate
            {
                try
                {
                    ConsoleProfile profile = connection.Profile;
                    bool ps3 = connection.IsConnected && profile != null && profile.Platform == Platform.PS3;
                    poller.Apply(ps3, AppHost.Settings.AlwaysShowCurrentGamePlaying);
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "Unable to update the current-game poll.");
                }
            };

            connection.Changed += apply;
            _poller = poller;
            _pollConnection = connection;
            _pollConnectionChanged = apply;

            // Inert today: the connection is the NullConsoleConnection, which never connects.
            apply(null, EventArgs.Empty);
        }

        private static void PostToShell(ShellForm shell, Action action)
        {
            if (shell == null || shell.IsDisposed || !shell.IsHandleCreated)
            {
                return;
            }

            Action guarded = delegate
            {
                try
                {
                    if (!shell.IsDisposed)
                    {
                        action();
                    }
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "A startup update on the UI thread failed.");
                }
            };

            if (!shell.InvokeRequired)
            {
                guarded();
                return;
            }

            try
            {
                shell.BeginInvoke(guarded);
            }
            catch (InvalidOperationException)
            {
                // The form closed between the check and the call.
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Unable to post a startup update to the UI thread.");
            }
        }

        private static void StopIdleTimer()
        {
            try
            {
                if (_idleStatusHandler != null)
                {
                    AppHost.StatusChanged -= _idleStatusHandler;
                }
                if (_idle != null)
                {
                    _idle.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Unable to stop the idle timer.");
            }
            _idleStatusHandler = null;
            _idle = null;
        }

        private static void StopCurrentGamePoll()
        {
            try
            {
                if (_pollConnection != null && _pollConnectionChanged != null)
                {
                    _pollConnection.Changed -= _pollConnectionChanged;
                }
                if (_poller != null)
                {
                    _poller.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Unable to stop the current-game poll.");
            }
            _pollConnectionChanged = null;
            _pollConnection = null;
            _poller = null;
        }

        private static void OnShellClosedCleanup(object sender, FormClosedEventArgs e)
        {
            CleanUpStartup();
        }

        private static void OnShellDisposedCleanup(object sender, EventArgs e)
        {
            CleanUpStartup();
        }

        // Idempotent. Runs on FormClosed, and on Disposed for a shell that is disposed without being closed. The guard is
        // released on the thread that acquired it (the UI thread).
        private static void CleanUpStartup()
        {
            ShellForm shell = _startupShell;
            _startupShell = null;

            if (shell != null)
            {
                try
                {
                    shell.Shown -= OnShellShownStartup;
                    shell.FormClosed -= OnShellClosedCleanup;
                    shell.Disposed -= OnShellDisposedCleanup;
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "Unable to unhook the startup handlers.");
                }
            }

            StopCurrentGamePoll();
            StopIdleTimer();

            SingleInstanceGuard guard = _guard;
            _guard = null;
            if (guard != null)
            {
                try
                {
                    guard.Dispose();
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "Unable to release the single-instance guard.");
                }
            }
        }

        // ====================================================================================================

        private static AdapterContext BuildAdapterContext(string settingsPath)
        {
            AdapterContext context = new AdapterContext();

            // Empty catalog: every data source returns empty lists until the host fills it after a database load.
            context.Catalog = new DatabaseCatalog();

            // SettingsProvider stays null: the adapters read AppHost.Settings each time (it is replaced by LoadSettings).
            // ProfileProvider: there is no connection service, so no profile is "connected". Console work reports "no console profile".
            context.ProfileProvider = delegate { return null; };

            context.SaveSettings = delegate { AppHost.SaveSettings(settingsPath); };

            context.BackupFiles = _backupFiles;
            context.SaveBackupFiles = delegate { SaveBackupFiles(); };

            return context;
        }

        private static DialogContext BuildDialogContext(AdapterContext adapters, bool smokeMode)
        {
            // No FTP client exists (no connection layer), so the provider answers null. FtpService and the adapters
            // built on it then fail with "There is no FTP connection to the console." and open nothing.
            Func<AsyncFtpClient> noClient = delegate { return null; };

            FtpService ftp = new FtpService(noClient, null);

            DialogContext dialogs = new DialogContext
            {
                // Categories and Database stay null: they come from the online database, which is not loaded here.
                // The details dialogs and the controller handle null the way the old app did before a database load.
                BackupFiles = _backupFiles,
                SaveBackupFiles = delegate { SaveBackupFiles(); },
                TransferOperations = new Ps3TransferConsoleOperations(adapters, ftp, new Ps3TransferHooks()),
                Ps3FileTransfer = new Ps3FileTransfer(ftp, noClient),
                // Read the catalog on each call: the host replaces its members after a database load.
                TitleIdToTitle = delegate (string titleId)
                {
                    Func<string, string> map = adapters.Catalog.TitleIdToTitle;
                    return map != null ? map(titleId) : titleId;
                }
                // Ps3ConsoleControl, Ps3GameBrowser, XboxConsole, XboxPackageFactory: no implementation exists; the forms keep their
                // "Unavailable..." defaults. ConnectedProfile, IsConsoleConnected, SaveSettings, SetStatus, ConnectConsole,
                // ScanXboxConsoles, ConsoleAddress and Language are filled in by ShellController.Attach when left null.
            };

            if (!smokeMode)
            {
                // No endpoint: HttpScreenshotUploader refuses to upload ("not configured") until the user supplies one.
                dialogs.ScreenshotUploader = new HttpScreenshotUploader(new ScreenshotUploadOptions(), new HttpClient());
            }

            return dialogs;
        }

        private static BackupFilesData LoadBackupFiles(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    if (File.Exists(path))
                    {
                        BackupFilesData loaded = JsonConvert.DeserializeObject<BackupFilesData>(File.ReadAllText(path));
                        if (loaded != null)
                        {
                            return loaded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppBootstrapper.LogError(ex, "Unable to load the backup files data; starting with an empty list.");
                }
            }

            return new BackupFilesData();
        }

        private static void SaveBackupFiles()
        {
            // Smoke mode: no path, so records stay in memory.
            if (string.IsNullOrEmpty(_backupFilesPath) || _backupFiles == null)
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_backupFilesPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_backupFilesPath, JsonConvert.SerializeObject(_backupFiles));
            }
            catch (Exception ex)
            {
                AppBootstrapper.LogError(ex, "Unable to save the backup files data.");
                AppHost.SetStatus("Unable to save the backup files data: " + ex.Message, ex);
            }
        }
    }
}
