using System;
using System.Drawing;
using System.Windows.Forms;
using ArisenStudio.Controls;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Dialogs;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// Main window shell (S0 and S1): a menu strip, an action strip placeholder, a status strip, and a split
    /// with the left navigation list and the page host. Stock WinForms defaults, no palette (D7/D8).
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
        private readonly DashboardPage _dashboard;
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
            _ = _actions.AddLabel("shell.actions.placeholder", "Commands are added with the ribbon replacement (S6).");

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
            _pages.RegisterPage(ShellPageKeys.About, new PlaceholderPage("About"));

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
            Shown += (s, e) => _pages.EnsureNonZeroSizes();

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
        /// The top action strip. S6 adds the ribbon commands here.
        /// </summary>
        public ActionStrip Actions
        {
            get { return _actions; }
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
