using System;
using System.Collections.Generic;
using System.Resources;
using System.Threading.Tasks;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Forms.Pages.Library;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Shell.Wiring
{
    /// <summary>
    /// The console connection the shell needs and the Mono project does not have yet. There is no connection service
    /// in Services/ (FtpService.cs only wraps FTP calls), so this interface is defined here and a host implements it
    /// (or leaves <see cref="NullConsoleConnection" /> in place). All members run on the UI thread unless noted;
    /// the async ones may work on other threads and must raise <see cref="Changed" /> on the UI thread.
    /// New type, lives only in the Wiring folder.
    /// </summary>
    internal interface IConsoleConnection
    {
        bool IsConnected { get; }

        /// <summary>The connected profile, or null.</summary>
        ConsoleProfile Profile { get; }

        /// <summary>True when webMAN answered on the connected PS3 (enables the Boot Plugins Editor).</summary>
        bool WebManInstalled { get; }

        /// <summary>Name of the game running on the console, or null/empty (status strip "Current Game").</summary>
        string CurrentGameName { get; }

        /// <summary>File service of the connected console for the File Manager page, or null when not connected.</summary>
        IConsoleFileService FileService { get; }

        /// <summary>Raised after a connect, disconnect or current-game change. UI thread.</summary>
        event EventHandler Changed;

        /// <summary>Connects. Returns false on failure (the implementation reports the reason through AppHost.SetStatus).</summary>
        Task<bool> ConnectAsync(ConsoleProfile profile);

        void Disconnect();

        /// <summary>
        /// Runs a webMAN or XBDM ribbon command (RibbonCommandIds.ToolsPs... power, games and notify, ToolsXbox... power and
        /// notify). argument carries the text the shell asked for (notify message), otherwise null. Returns false when the
        /// command is not supported.
        /// </summary>
        Task<bool> RunCommandAsync(string commandId, string argument);

        /// <summary>Xbox scan for the Setup Wizard. Return an empty sequence when nothing is found.</summary>
        IEnumerable<ConsoleProfile> ScanXboxConsoles();
    }

    /// <summary>Default connection: never connects. Lets the shell run without a console layer.</summary>
    internal sealed class NullConsoleConnection : IConsoleConnection
    {
        public bool IsConnected { get { return false; } }
        public ConsoleProfile Profile { get { return null; } }
        public bool WebManInstalled { get { return false; } }
        public string CurrentGameName { get { return null; } }
        public IConsoleFileService FileService { get { return null; } }

        public event EventHandler Changed { add { } remove { } }

        public Task<bool> ConnectAsync(ConsoleProfile profile) { return Task.FromResult(false); }
        public void Disconnect() { }
        public Task<bool> RunCommandAsync(string commandId, string argument) { return Task.FromResult(false); }
        public IEnumerable<ConsoleProfile> ScanXboxConsoles() { return new ConsoleProfile[0]; }
    }

    /// <summary>
    /// Looks up database objects from the opaque rows the pages and the dashboard hand back. The pages carry the
    /// data source's own object in <c>Tag</c> where they can; these three cases carry only ids. Optional: when null,
    /// the controller reports "not available" in the status strip for those clicks. New type.
    /// </summary>
    internal interface IShellLookup
    {
        /// <summary>Downloads page "View details": the mod of a downloaded row (by PlatformText and ModId), or null.</summary>
        ModItemData FindDownloadedMod(DownloadRowInfo row);

        /// <summary>Installed Files page "View details": a ModItemData or a CustomItemData, or null.</summary>
        object FindInstalledItem(InstalledRowInfo row);

        /// <summary>Dashboard favourite mod tile: the mod, or null.</summary>
        ModItemData FindFavoriteMod(FavoriteModInfo mod);
    }

    /// <summary>
    /// What the controller needs from the host. Every member is optional; a null data source leaves the page on its
    /// own empty default. Create one, fill it, pass it to <see cref="ShellController.Attach" />.
    /// New type, lives only in the Wiring folder.
    /// </summary>
    internal sealed class ShellServices
    {
        // ---- data sources for the pages (all interfaces already exist in Forms/Pages and Forms/Dashboard) ----

        public IDashboardDataSource Dashboard { get; set; }
        public IDownloadsDataSource Downloads { get; set; }
        public IInstalledFilesDataSource InstalledFiles { get; set; }
        public ILibraryDataSource Library { get; set; }

        // ---- the two interfaces defined in this folder ----

        /// <summary>Null installs <see cref="NullConsoleConnection" />.</summary>
        public IConsoleConnection Connection { get; set; }

        /// <summary>Null: the three "find by id" clicks report "not available".</summary>
        public IShellLookup Lookup { get; set; }

        // ---- context for the dialogs and tool forms ----

        /// <summary>
        /// Pre-filled dialog context (categories, database client, console boundaries). The controller fills in
        /// ConnectedProfile, IsConsoleConnected, SaveSettings, SetStatus, ConnectConsole, ScanXboxConsoles and Language
        /// when the host left them null. Null: a new empty context.
        /// </summary>
        public DialogContext Dialogs { get; set; }

        // ---- text, paths, behaviour ----

        /// <summary>Old resource lookup by key (NAVIGATION_*, ...). A null or empty answer means "keep the English text".</summary>
        public Func<string, string> Localizer { get; set; }

        /// <summary>Old ResourceManager for the dialogs. Null leaves the dialogs on English.</summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The user picked a language on the Settings page (argument: the language name). Switch the host's language, update
        /// <see cref="Localizer" /> and <see cref="Language" /> on this object, and return true. Return false when the
        /// language is not supported: the controller then puts the previous language back in the combo. Null: the
        /// controller only stores AppHost.Settings.Language and saves.
        /// </summary>
        public Func<string, bool> SwitchLanguage { get; set; }

        /// <summary>A Discord toggle changed (settings already written and saved). Start or stop the presence here. Null: nothing.</summary>
        public Action DiscordSettingsChanged { get; set; }

        /// <summary>Settings file. Null uses UserFolders.SettingsData.</summary>
        public string SettingsPath { get; set; }

        /// <summary>
        /// True (default): Attach calls AppHost.LoadSettings(SettingsPath) before it binds the pages. Set false when the
        /// caller already loaded AppHost.Settings.
        /// </summary>
        public bool LoadSettingsOnAttach { get; set; }

        /// <summary>Show the Setup Wizard once when AppHost.Settings.FirstTimeUse is true and there is no profile (old first run). Default true.</summary>
        public bool RunSetupWizardOnFirstUse { get; set; }

        /// <summary>False greys out the Discord tab of the Settings page (decision D5 recommends no Discord for the first Linux release). Default false.</summary>
        public bool DiscordAvailable { get; set; }

        /// <summary>
        /// Called once after the form is shown, for the host's own startup work (database load, release check). Its
        /// Task is awaited, then the controller reloads the dashboard and the visible page. Null: nothing.
        /// </summary>
        public Func<Task> StartupAsync { get; set; }

        /// <summary>
        /// Called on close after the settings were saved, for the host's cleanup (Discord, connection). Null: nothing.
        /// </summary>
        public Action ShuttingDown { get; set; }

        public ShellServices()
        {
            LoadSettingsOnAttach = true;
            RunSetupWizardOnFirstUse = true;
            DiscordAvailable = false;
        }
    }
}
