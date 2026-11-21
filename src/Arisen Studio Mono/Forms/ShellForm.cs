using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Controls;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Io;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// Main window shell (S0, S1 and S6): a menu strip, an action strip holding the ribbon replacement commands, a status
    /// strip, and a split with the left navigation list and the page host. Stock WinForms defaults, no palette (D7/D8).
    /// Page keys are ShellPageKeys values. Host hooks not wired yet are listed in the S0S1 result file.
    /// UI thread only.
    /// </summary>
    internal sealed class ShellForm : Form
    {
        internal const string GroupGeneral = "general";
        internal const string GroupLibrary = "library";

        private readonly SplitContainer _split;
        private readonly NavListPanel _nav;
        private readonly PageHost _pages;
        private readonly ActionStrip _actions;
        private readonly RibbonBar _ribbon;
        private readonly DashboardPage _dashboard;
        private readonly AboutPage _about;
        private readonly StatusStrip _status;
        private readonly MenuStrip _menu;
        private readonly ToolStripStatusLabel _statusText;
        private readonly ToolStripStatusLabel _connectionText;
        private readonly ToolStripStatusLabel _currentGameHeader;
        private readonly ToolStripStatusLabel _currentGame;

        public ShellForm()
        {
            Text = "Arisen Studio [" + Application.ProductVersion + "]";
            ClientSize = new Size(1280, 800);

            _nav = new NavListPanel { Dock = DockStyle.Fill };
            _pages = new PageHost { Dock = DockStyle.Fill };
            _split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
            _split.Panel1.Controls.Add(_nav);
            _split.Panel2.Controls.Add(_pages);

            _actions = new ActionStrip();
            _ribbon = new RibbonBar(_actions);
            _ribbon.CommandInvoked += OnRibbonCommand;

            _status = new StatusStrip();
            ToolStripStatusLabel headerStatus = new ToolStripStatusLabel("Status:") { Name = "StatusLabelHeaderStatus" };
            // "Idling..." is the old app's idle text (STATUS_IDLING in en_US); "Status" was only the designer default.
            _statusText = new ToolStripStatusLabel("Idling...") { Name = "StatusLabelStatus", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _connectionText = new ToolStripStatusLabel("Not Connected") { Name = "StatusLabelHeaderIsConnected" };
            _currentGameHeader = new ToolStripStatusLabel("Current Game:") { Name = "StatusLabelHeaderCurrentGame", Visible = false };
            _currentGame = new ToolStripStatusLabel("n/a") { Name = "StatusLabelCurrentGame", Visible = false };
            _ = _status.Items.Add(headerStatus);
            _ = _status.Items.Add(_statusText);
            _ = _status.Items.Add(_connectionText);
            _ = _status.Items.Add(_currentGameHeader);
            _ = _status.Items.Add(_currentGame);

            _menu = new MenuStrip();
            ToolStripMenuItem file = new ToolStripMenuItem("File");
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += (s, e) => Close();
            _ = file.DropDownItems.Add(exit);
            _ = _menu.Items.Add(file);
            ToolStripMenuItem help = new ToolStripMenuItem("Help");
            ToolStripMenuItem about = new ToolStripMenuItem("About");
            about.Click += (s, e) => ShowPage(ShellPageKeys.About);
            _ = help.DropDownItems.Add(about);
            ToolStripMenuItem reportIssue = new ToolStripMenuItem("Report issue...");
            reportIssue.Click += (s, e) =>
            {
                using (ReportIssueDialog dialog = new ReportIssueDialog()) { dialog.ShowDialog(this); }
            };
            _ = help.DropDownItems.Add(reportIssue);
            _ = _menu.Items.Add(help);

            _nav.LeafSelected += (s, e) => ShowResolvedPage(e.LeafKey);
            AddNavLeaf(null, ShellPageKeys.Dashboard, "Dashboard");
            _nav.AddGroup(GroupGeneral, "General");
            AddNavLeaf(GroupGeneral, ShellPageKeys.Downloads, "Downloads");
            AddNavLeaf(GroupGeneral, ShellPageKeys.InstalledFiles, "Installed Files");
            AddNavLeaf(GroupGeneral, ShellPageKeys.FileManager, "File Manager");
            AddNavLeaf(GroupGeneral, ShellPageKeys.Settings, "Settings");
            _nav.AddGroup(GroupLibrary, "Library");
            AddNavLeaf(GroupLibrary, ShellPageKeys.CustomMods, "Custom Mods");
            AddNavLeaf(GroupLibrary, ShellPageKeys.GameMods, "Game Mods");
            AddNavLeaf(GroupLibrary, ShellPageKeys.Homebrew, "Homebrew");
            AddNavLeaf(GroupLibrary, ShellPageKeys.Trainers, "Trainers");
            AddNavLeaf(GroupLibrary, ShellPageKeys.Resources, "Resources");
            AddNavLeaf(GroupLibrary, ShellPageKeys.PkgStore, "PKG Store");
            AddNavLeaf(GroupLibrary, ShellPageKeys.Games, "Games");
            AddNavLeaf(GroupLibrary, ShellPageKeys.GameSaves, "Game Saves");
            AddNavLeaf(GroupLibrary, ShellPageKeys.GameCheats, "Game Cheats");
            _about = new AboutPage();
            _pages.RegisterPage(ShellPageKeys.About, _about);

            // The real dashboard replaces the placeholder registered above under the same key (S2). It reads from the
            // empty stub data source until the shell supplies a real one (set _dashboard.DataSource, then ReloadAll).
            _dashboard = new DashboardPage();
            _dashboard.StatusMessage += (s, e) => SetStatusText(e.Message);
            _pages.RegisterPage(ShellPageKeys.Dashboard, _dashboard);

            Load += (s, e) =>
            {
                if (_split.Width >= 500)
                {
                    _split.SplitterDistance = 240;
                    _split.Panel1MinSize = 180;
                }
                _dashboard.ReloadAll();
                ShowPage(ShellPageKeys.Dashboard);
            };
            // Last chance before the check harness's Shown handler and capture timer: nothing in the page may be zero-sized.

            // Fill control first, then the strips, then the menu (the order the spike verified).
            Controls.Add(_split);
            Controls.Add(_actions);
            Controls.Add(_status);
            Controls.Add(_menu);
            MainMenuStrip = _menu;
        }

        /// <summary>
        /// The navigation list. Its SetLeafVisible, SetLeafCaption and SetGroupCaption are the platform and language hooks.
        /// </summary>
        public NavListPanel Navigation
        {
            get { return _nav; }
        }

        /// <summary>
        /// The page host. A slice replaces a placeholder with Pages.RegisterPage under the same key.
        /// </summary>
        public PageHost Pages
        {
            get { return _pages; }
        }

        /// <summary>
        /// The top action strip. It holds the ribbon replacement commands (S6); use Ribbon to change them.
        /// </summary>
        public ActionStrip Actions
        {
            get { return _actions; }
        }

        /// <summary>
        /// The ribbon replacement. Its SetCaption, SetEnabled, SetVisible, SetChecked and ApplyConsoleState are the
        /// language, platform and settings hooks.
        /// </summary>
        internal RibbonBar Ribbon
        {
            get { return _ribbon; }
        }

        /// <summary>
        /// The About page (S7). Call its SetVersion with the release date once the release list is loaded (MW3-137).
        /// </summary>
        internal AboutPage About
        {
            get { return _about; }
        }

        /// <summary>
        /// Raised for a ribbon command the shell does not carry out itself (connection, profiles, downloads folder, the
        /// PS3 and Xbox tools and webMAN/XBDM commands, update) and for the two check commands (Discord Presence and
        /// Install Mods to USB) after the shell has shown the Settings page. Without a subscriber an unhandled command only
        /// sets the status text. UI thread only.
        /// </summary>
        internal event EventHandler<RibbonCommandEventArgs> RibbonCommandRequested;

        /// <summary>
        /// Shows or hides the "New Update Available" button (old UpdateAvailable, MW1-027). UI thread only.
        /// </summary>
        public void SetUpdateAvailable(bool available)
        {
            _ribbon.SetUpdateAvailable(available);
        }

        /// <summary>
        /// Applies the connected platform and state to the tool commands (old EnableConsoleActions, MW3-148). UI thread only.
        /// </summary>
        public void SetConsoleState(Platform platform, bool connected, bool webManInstalled)
        {
            SetConnectionState(connected);
            _ribbon.ApplyConsoleState(platform, connected, webManInstalled);
        }

        /// <summary>
        /// Maps a nav leaf key to the page key shown for it (the platform choice for Game Mods and Homebrew, MW1-087 and MW1-088).
        /// Null means the page key is the leaf key.
        /// </summary>
        public Func<string, string> ResolvePageKey { get; set; }

        /// <summary>
        /// Shows a page. A nav leaf key selects its leaf (and so shows the page). Any other registered key, such as About,
        /// clears the nav selection and shows the page. Returns false for an unknown key. UI thread only.
        /// </summary>
        public bool ShowPage(string key)
        {
            if (_nav.SelectLeaf(key))
            {
                return true;
            }
            _nav.ClearLeafSelection();
            return _pages.ShowPage(key);
        }

        /// <summary>
        /// Sets the status text in the status strip. Null becomes an empty string. UI thread only.
        /// </summary>
        public void SetStatusText(string text)
        {
            _statusText.Text = text == null ? string.Empty : text;
        }

        /// <summary>
        /// Shows the connection state. "Connected" is not from source; the old label text is set elsewhere. UI thread only.
        /// </summary>
        public void SetConnectionState(bool connected)
        {
            _connectionText.Text = connected ? "Connected" : "Not Connected";
            _ribbon.SetConnected(connected);
        }

        /// <summary>
        /// Sets the current game text. Null or empty becomes "n/a". UI thread only.
        /// </summary>
        public void SetCurrentGame(string gameName)
        {
            _currentGame.Text = string.IsNullOrEmpty(gameName) ? "n/a" : gameName;
        }

        /// <summary>
        /// Shows or hides the current-game header and value. Hidden by default, as in the old app (MW2-039). UI thread only.
        /// </summary>
        public void SetCurrentGameVisible(bool visible)
        {
            _currentGameHeader.Visible = visible;
            _currentGame.Visible = visible;
        }

        private void AddNavLeaf(string groupKey, string leafKey, string caption)
        {
            _nav.AddLeaf(groupKey, leafKey, caption);
            _pages.RegisterPage(leafKey, new PlaceholderPage(caption));
        }

        private void OnRibbonCommand(object sender, RibbonCommandEventArgs e)
        {
            switch (e.CommandId)
            {
                case RibbonCommandIds.HeaderDonate:
                    OpenExternal(Urls.DonatePatreon);
                    return;
                case RibbonCommandIds.SendFeedback:
                    OpenExternal(Urls.GitHubRepo + "issues/new/choose");
                    return;
                case RibbonCommandIds.HowToGuides:
                    if (MessageBox.Show(this, "Our How-To-Guides have been moved to our website at https://arisen.studio/guide\n\nWould you like to open the link in your browser?.", "How-To Guides", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        OpenExternal(Urls.ProjectWebsite);
                    }
                    return;
                case RibbonCommandIds.SetupWizard:
                    using (SetupWizardDialog dialog = new SetupWizardDialog()) { _ = dialog.ShowDialog(this); }
                    return;
                case RibbonCommandIds.RequestMods:
                    using (RequestModsDialog dialog = new RequestModsDialog()) { _ = dialog.ShowDialog(this); }
                    return;
                case RibbonCommandIds.AdvancedSettings:
                    _ = ShowPage(ShellPageKeys.Settings);
                    return;
                case RibbonCommandIds.About:
                    _ = ShowPage(ShellPageKeys.About);
                    return;
                case RibbonCommandIds.OpenLogFile:
                    OpenLogFile();
                    return;
                case RibbonCommandIds.OpenLogFolder:
                    SetStatusText("Opening Log folder...");
                    if (!Directory.Exists(UserFolders.Logs))
                    {
                        _ = Directory.CreateDirectory(UserFolders.Logs);
                    }
                    OpenExternal(UserFolders.Logs);
                    return;
                case RibbonCommandIds.DiscordPresence:
                case RibbonCommandIds.InstallModsToUsb:
                    // The old check items only jumped to the Settings page (MW1-038, MW1-039); the tab is not selected yet
                    // because SettingsPage has no public way to select a tab. The host may write the setting from the event.
                    _ = ShowPage(ShellPageKeys.Settings);
                    break;
            }

            EventHandler<RibbonCommandEventArgs> handler = RibbonCommandRequested;
            if (handler != null)
            {
                handler(this, e);
            }
            else if (!e.IsCheck)
            {
                SetStatusText(e.Caption + ": not available yet.");
            }
        }

        private void OpenLogFile()
        {
            SetStatusText("Opening Log file...");
            string logFile = Path.Combine(UserFolders.Logs, "latest.log");
            if (File.Exists(logFile))
            {
                try
                {
                    _ = Process.Start(new ProcessStartInfo(logFile) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    SetStatusText("Failed to open Log file... " + ex.Message);
                    _ = MessageBox.Show(this, "Failed to open the log file.\nIt may have been deleted, that's ok.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                SetStatusText("Failed to find Log file...");
                _ = MessageBox.Show(this, "Failed to find the log file.\nIt may have been deleted, that's ok.", "No Log File.", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenExternal(string target)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetStatusText("Could not open " + target + ": " + ex.Message);
            }
        }

        private void ShowResolvedPage(string leafKey)
        {
            string pageKey = ResolvePageKey == null ? leafKey : ResolvePageKey(leafKey);
            if (!_pages.ShowPage(pageKey))
            {
                _pages.ShowPage(leafKey);
            }
        }
    }
}
