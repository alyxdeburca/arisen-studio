using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Lists the Xbox 360 game files from Settings.GameFilesXbox, renames an entry, and starts a game on the console.
    /// Stock WinForms port of the old DevExpress GameLauncher form. The console call (launch an .xex) is not ported;
    /// it sits behind IXboxGameLauncher.
    /// Host hooks, set before the form is shown:
    ///  - Launcher: the console boundary (default: a stub that fails with a message).
    ///  - Settings: Settings.GameFilesXbox is the list (Name shown, Value is the .xex path). Defaults to the app host settings.
    ///    A rename changes that list in memory; GameFilesChanged is raised so the host can save the settings.
    /// </summary>
    public class GameLauncher : Form
    {
        private readonly GroupBox _groupGames;
        private readonly ListBox _listGames;
        private readonly Label _labelNoGames;
        private readonly Button _buttonEditName;
        private readonly Button _buttonLaunch;
        private readonly ToolStripStatusLabel _status;

        public GameLauncher()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _listGames = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _labelNoGames = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                Visible = false
            };

            Panel listHost = new Panel { Dock = DockStyle.Fill };
            listHost.Controls.Add(_listGames);
            listHost.Controls.Add(_labelNoGames);

            _groupGames = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(pad), Margin = new Padding(0, 0, 0, pad) };
            _groupGames.Controls.Add(listHost);

            _buttonEditName = ToolFormSupport.NewButton(this, 8);
            _buttonLaunch = ToolFormSupport.NewButton(this, 8);
            _buttonLaunch.Margin = Padding.Empty;
            _buttonEditName.Enabled = false;
            _buttonLaunch.Enabled = false;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddFullRow(root, _groupGames, true);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonEditName, _buttonLaunch), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _listGames.SelectedIndexChanged += ListGames_SelectedIndexChanged;
            _buttonEditName.Click += ButtonEditName_Click;
            _buttonLaunch.Click += ButtonLaunch_Click;

            ApplyLanguage();

            MinimumSize = new Size(h * 26, h * 20);
            ClientSize = new Size(h * 34, h * 26);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (GameFilesXbox). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// Console boundary used to start a game.
        /// </summary>
        public IXboxGameLauncher Launcher { get; set; } = UnavailableXboxConsoleAccess.Instance;

        /// <summary>
        /// Raised after a game was renamed. The host can save the settings then.
        /// </summary>
        public event EventHandler GameFilesChanged;

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("GAMES_LAUNCHER", "Game Launcher");
            _groupGames.Text = Text_("LABEL_GAMES", "Games");
            _labelNoGames.Text = Text_("LABEL_NO_GAMES", "No games have been added.");
            _buttonEditName.Text = Text_("LABEL_EDIT_NAME", "Edit Name");
            _buttonLaunch.Text = Text_("LABEL_LAUNCH_GAME", "Launch Game");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            LoadGames();
        }

        private void LoadGames()
        {
            int keep = _listGames.SelectedIndex;

            SetStatus("Fetching games list...");

            _listGames.BeginUpdate();
            _listGames.Items.Clear();

            foreach (ListItem game in Settings.GameFilesXbox)
            {
                _listGames.Items.Add(game.Name);
            }

            _listGames.EndUpdate();

            _labelNoGames.Visible = Settings.GameFilesXbox.Count == 0;
            _labelNoGames.BringToFront();

            if (keep >= 0 && keep < _listGames.Items.Count)
            {
                _listGames.SelectedIndex = keep;
            }

            UpdateButtons();
            SetStatus("Successfully fetched games.");
        }

        private void UpdateButtons()
        {
            bool hasSelection = _listGames.SelectedIndex != -1;
            _buttonEditName.Enabled = hasSelection;
            _buttonLaunch.Enabled = hasSelection;
        }

        private void ListGames_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void ButtonEditName_Click(object sender, EventArgs e)
        {
            int index = _listGames.SelectedIndex;

            if (index < 0 || index >= Settings.GameFilesXbox.Count)
            {
                return;
            }

            ListItem game = Settings.GameFilesXbox[index];
            string newName = ToolFormSupport.PromptText(this, Text_("GAME_FILE", "Game File"), Text_("LABEL_NAME", "Name") + ":", game.Name);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                game.Name = newName;
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("FILE_RENAMED_SUCCESS", "The file was renamed."));
                LoadGames();

                EventHandler handler = GameFilesChanged;

                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        private void ButtonLaunch_Click(object sender, EventArgs e)
        {
            int index = _listGames.SelectedIndex;

            if (index < 0 || index >= Settings.GameFilesXbox.Count)
            {
                return;
            }

            try
            {
                SetStatus("Launching game...");

                ListItem game = Settings.GameFilesXbox[index];

                // The old form passed the same path as both the .xex path and the directory; kept as it was.
                Launcher.LaunchXex(game.Value, game.Value);

                SetStatus("Successfully launched game.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("SUCCESS_LAUNCHED_GAME", "The game was launched."));
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to launch game file. Error: " + ex.Message);
                SetStatus("Unable to launch game file. Error: " + ex.Message);

                string format = Text_("ERROR_LAUNCHING_GAME", "Unable to launch the game. Error: {0}");
                string message;

                try
                {
                    message = string.Format(format, ex.Message);
                }
                catch (FormatException)
                {
                    message = format + " " + ex.Message;
                }

                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), message);
            }
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
