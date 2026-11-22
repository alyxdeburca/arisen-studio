using System;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Io;
using ArisenStudio.Localization;
using ArisenStudio.Shell;
using ArisenStudio.Shell.Wiring;

namespace ArisenStudio.Runtime
{
    /// <summary>
    /// Application lifecycle glue (the part of the old Program.cs and MainWindow Load/FormClosing that is not UI):
    /// error log, settings load at start-up, settings save at close, and binding the shell controller and the localizer.
    /// Nothing here edits ShellForm; it subscribes from the outside.
    /// <code>
    /// // Program.Main, before the exception handlers (optional, AttachToShell does it too):
    /// AppBootstrapper.Initialize(smoke);
    /// // ...
    /// using (ShellForm shell = new ShellForm())
    /// {
    ///     AppBootstrapper.AttachToShell(shell, smoke);   // before Application.Run(shell)
    ///     Application.Run(shell);
    /// }
    /// </code>
    /// Normal mode: log at UserFolders.Logs/latest.log, settings at UserFolders.SettingsData.
    /// Smoke mode: deterministic and offline. Log and settings live in a fresh temp folder
    /// (Path.GetTempPath()/arisen-studio-smoke-GUID), so the real user folder is never read or written; the first-run
    /// setup wizard (a modal dialog) is off; no start-up task (database load, release check) is installed.
    /// UI thread only. New type; nothing was compiled or run.
    /// </summary>
    internal static class AppBootstrapper
    {
        private static readonly object InitLock = new object();
        private static bool _initialized;
        private static bool _smoke;
        private static string _settingsPath;
        private static string _logPath;
        private static FileErrorLog _log;
        private static ShellController _controller;

        /// <summary>The settings file in use (real user folder, or a temp file in smoke mode). Null before <see cref="Initialize" />.</summary>
        public static string SettingsPath
        {
            get { return _settingsPath; }
        }

        /// <summary>The log file in use. Null before <see cref="Initialize" />.</summary>
        public static string LogFilePath
        {
            get { return _logPath; }
        }

        /// <summary>True when the settings were read from disk (or created with defaults) without error.</summary>
        public static bool SettingsLoaded { get; private set; }

        /// <summary>
        /// Path of the copy made of an unreadable settings file, or null. The copy is made before the defaults can overwrite
        /// the file on close (the old app lost such a file silently).
        /// </summary>
        public static string CorruptSettingsCopy { get; private set; }

        /// <summary>The controller created by <see cref="AttachToShell" />, or null.</summary>
        public static ShellController Controller
        {
            get { return _controller; }
        }

        /// <summary>
        /// Configures <see cref="AppHost.Logger" /> and loads <see cref="AppHost.Settings" />. Safe to call more than once
        /// with the same mode (later calls do nothing); a different mode throws. Never throws for IO problems.
        /// </summary>
        public static void Initialize(bool smokeMode)
        {
            lock (InitLock)
            {
                if (_initialized)
                {
                    if (_smoke != smokeMode)
                    {
                        throw new InvalidOperationException("AppBootstrapper was already initialized with smokeMode=" + _smoke + ".");
                    }
                    return;
                }

                _smoke = smokeMode;

                string logFolder;
                if (smokeMode)
                {
                    // Fresh folder per process: nothing to clean up or collide with, and no stale state makes a run differ.
                    string root = Path.Combine(Path.GetTempPath(), "arisen-studio-smoke-" + Guid.NewGuid().ToString("N"));
                    logFolder = Path.Combine(root, "Logs");
                    _settingsPath = Path.Combine(root, "SettingsData.json");
                }
                else
                {
                    logFolder = UserFolders.Logs;
                    _settingsPath = UserFolders.SettingsData;
                }

                _logPath = Path.Combine(logFolder, "latest.log");
                _log = new FileErrorLog(_logPath);
                AppHost.Logger = _log.Write;

                LoadSettings();
                _initialized = true;
            }
        }

        /// <summary>
        /// Writes one error line through the same logger (for Program.Fatal and the like). Never throws.
        /// </summary>
        public static void LogError(Exception exception, string message)
        {
            try
            {
                FileErrorLog log = _log;
                if (log != null)
                {
                    log.Write(exception, message);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Binds the app lifecycle to the shell. Call once, on the UI thread, after <c>new ShellForm()</c> and before
        /// <c>Application.Run</c>. Initializes first when the caller did not.
        /// <para>
        /// Both modes: subscribes to <c>shell.FormClosing</c> to save <see cref="AppHost.Settings" /> to
        /// <see cref="SettingsPath" /> (skipped when another handler cancelled the close), and unsubscribes on FormClosed.
        /// The controller also saves on close; the second write is identical and harmless, and keeps the save independent
        /// of whether the controller attached.
        /// </para>
        /// <para>
        /// Normal mode: creates a <see cref="ShellController" /> and attaches it with <see cref="ResourceText.Localizer" />
        /// as the localizer. A failure to attach is logged and shown in the status strip; the shell then runs with its
        /// placeholder pages. Smoke mode: the same, but an attach failure is rethrown so the smoke run fails visibly.
        /// </para>
        /// </summary>
        /// <returns>The controller, or null when it could not be attached.</returns>
        public static ShellController AttachToShell(ShellForm shell, bool smokeMode)
        {
            if (shell == null)
            {
                throw new ArgumentNullException("shell");
            }
            if (_controller != null && _controller.IsAttached)
            {
                throw new InvalidOperationException("AppBootstrapper is already attached to a shell.");
            }

            Initialize(smokeMode);

            // Subscribed before the controller attaches, so this handler runs first on close. Settings are saved even
            // if the controller never attached.
            shell.FormClosing += OnShellClosing;
            shell.FormClosed += OnShellClosed;

            ShellController controller = new ShellController();
            try
            {
                // The settings were loaded by Initialize, so the controller must not load them again.
                // The controller's own FormClosing handler also saves to the same path, then disconnects and clears the cache.
                ShellServices services = new ShellServices
                {
                    SettingsPath = _settingsPath,
                    LoadSettingsOnAttach = false,
                    Localizer = ResourceText.Localizer,
                    DiscordAvailable = false,
                    RunSetupWizardOnFirstUse = !smokeMode
                    // StartupAsync stays null: the database load and the release check are network work and are not wired here.
                    // SwitchLanguage stays null: the controller only stores Settings.Language and saves.
                };

                controller.Attach(shell, services);
                _controller = controller;
                return controller;
            }
            catch (Exception ex)
            {
                LogError(ex, "Unable to attach the shell controller.");
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

        private static void LoadSettings()
        {
            bool existed = false;
            try
            {
                existed = File.Exists(_settingsPath);
            }
            catch (Exception)
            {
            }

            // AppHost.LoadSettings creates the folder and writes the defaults when the file is missing (the old
            // LoadApplicationSettings did the same), then replaces AppHost.Settings. It logs and sets the status on failure.
            SettingsLoaded = AppHost.LoadSettings(_settingsPath);

            if (!SettingsLoaded)
            {
                if (existed)
                {
                    CopyUnreadableSettings();
                }
                return;
            }

            // Old behaviour: a profile saved without an id gets one.
            try
            {
                foreach (Models.Resources.ConsoleProfile profile in AppHost.Settings.ConsoleProfiles)
                {
                    if (profile != null && !profile.Id.HasValue)
                    {
                        profile.Id = Extensions.DataExtensions.GenerateUniqueId();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Unable to assign profile ids.");
            }
        }

        private static void CopyUnreadableSettings()
        {
            try
            {
                string copy = _settingsPath + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(_settingsPath, copy, false);
                CorruptSettingsCopy = copy;
            }
            catch (Exception ex)
            {
                LogError(ex, "Unable to keep a copy of the unreadable settings file.");
            }
        }

        private static void OnShellClosing(object sender, FormClosingEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            // AppHost.SaveSettings catches, logs and reports its own errors and returns false; it does not throw.
            AppHost.SaveSettings(_settingsPath);
        }

        private static void OnShellClosed(object sender, FormClosedEventArgs e)
        {
            Form form = sender as Form;
            if (form != null)
            {
                form.FormClosing -= OnShellClosing;
                form.FormClosed -= OnShellClosed;
            }
        }
    }
}
