using ArisenStudio.Constants;
using ArisenStudio.Database;
using ArisenStudio.Models.GameData.PS3;
using ArisenStudio.Runtime;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Lists the PS3 cheats for one game and applies the selected cheat.
    /// Stock WinForms port of the old DevExpress GameCheatsDialog.
    /// The dialog never talks to the console: Apply calls the ApplyCheat delegate and the host performs the writes.
    /// Set GameItem, Language and the delegates before the dialog is shown.
    /// </summary>
    internal class GameCheatsDialog : Form
    {
        private const string CheatsBaseUrl = "https://raw.githubusercontent.com/OhhSoWzrd/game-cheats/refs/heads/main/PS3/";
        private const string NoConsoleWriterText =
            "Applying a cheat needs a connection to the console, which is not available in this build.";

        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly Label _labelGame;
        private readonly Label _labelVersion;
        private readonly Label _labelRegion;
        private readonly ListView _listCheats;
        private readonly ColumnHeader _columnName;
        private readonly Button _buttonApply;
        private readonly Button _buttonReport;
        private readonly Button _buttonHelp;
        private readonly Button _buttonClose;

        private bool _isClosed;
        private Cheats _selectedCheatItem;

        public GameCheatsDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Cheats";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(630, 432);

            int pad = Font.Height / 2;

            _labelGame = new Label { AutoSize = true, UseMnemonic = false, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, pad, 0), Text = "Game", MinimumSize = new Size(1, 0) }; // Never 0 wide: LoadHeader empties this label when there is no GameItem, and a zero-width control cannot be drawn on Mono.
            _labelVersion = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, pad, 0), Text = "- Version" };
            _labelRegion = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0), Text = "(REGION)" };

            _columnName = new ColumnHeader { Text = "Name", Width = Font.Height * 20 };

            _listCheats = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                GridLines = false,
                Margin = Padding.Empty
            };
            _listCheats.Columns.Add(_columnName);
            _listCheats.SelectedIndexChanged += ListCheats_SelectedIndexChanged;
            _listCheats.Resize += (s, e) => ResizeColumns();

            _buttonApply = new Button { AutoSize = true, Enabled = false, Text = "Apply Cheat", Margin = new Padding(pad, 0, 0, 0) };
            _buttonApply.Click += ButtonApply_Click;

            _buttonReport = new Button { AutoSize = true, Text = "Report Issue", Margin = new Padding(pad, 0, 0, 0) };
            _buttonReport.Click += ButtonReport_Click;

            _buttonHelp = new Button { AutoSize = true, Text = "Help && Support", Margin = new Padding(pad, 0, 0, 0) };
            _buttonHelp.Click += ButtonHelp_Click;

            _buttonClose = new Button { AutoSize = true, Text = "Close", Margin = new Padding(pad, 0, 0, 0) };
            _buttonClose.Click += (s, e) => Close();

            FlowLayoutPanel header = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(pad),
                Margin = Padding.Empty
            };
            header.Controls.Add(_labelGame);
            header.Controls.Add(_labelVersion);
            header.Controls.Add(_labelRegion);

            // Right-to-left so the first control added sits at the right edge: Apply, Report Issue, Help & Support, Close.
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(pad),
                Margin = Padding.Empty
            };
            actions.Controls.Add(_buttonClose);
            actions.Controls.Add(_buttonHelp);
            actions.Controls.Add(_buttonReport);
            actions.Controls.Add(_buttonApply);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(_listCheats, 0, 1);
            root.Controls.Add(actions, 0, 2);

            Controls.Add(root);
        }

        /// <summary>
        /// The game to show: its file name, title, region and version. Set it before the dialog is shown.
        /// </summary>
        public GameItemData GameItem { get; set; }

        /// <summary>
        /// The cheats for the game. Filled from the cheat file when the dialog loads; read it after the dialog has loaded.
        /// </summary>
        public GameCheatsData GameCheats { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Connects to the console and attaches to the running game. Called on load, before the cheats are listed.
        /// Null (the default) skips the connection, as the old DEBUG build did. Throw to report a failure: the dialog then shows the message and closes.
        /// </summary>
        public Action ConnectConsole { get; set; }

        /// <summary>
        /// Applies one cheat to the console and returns true when every write succeeded.
        /// The host performs the writes: for each Offsets entry with Opcode "00002000", write the Value to the Offset as a 32-bit integer,
        /// where a zero Offset means the previous result. The AOB (byte array) branch of the old code was unreachable and is not ported.
        /// Null (the default) means no console writer is attached: Apply then shows a message and never reports success.
        /// </summary>
        public Func<Cheats, bool> ApplyCheat { get; set; }

        /// <summary>
        /// Raised when Report Issue is clicked. The host opens the issue report.
        /// </summary>
        public event EventHandler ReportIssueRequested;

        /// <summary>
        /// Opens a web address. Null (the default) uses Process.Start.
        /// </summary>
        public Action<string> OpenUrl { get; set; }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            ResizeColumns();

            try
            {
                _listCheats.Enabled = false;
                UseWaitCursor = true;

                GameCheats = await GetGameCheatsPS3(GameItem?.File);

                if (_isClosed)
                {
                    return;
                }

                if (ConnectConsole != null)
                {
                    try
                    {
                        ConnectConsole();
                    }
                    catch (Exception ex)
                    {
                        AppHost.Logger?.Invoke(ex, "Unable to connect or attach using webMAN API.");
                        _ = MessageBox.Show(this, string.Format("Unable to connect or attach using webMAN. You must have PS3MAPI enabled in your webMAN settings. Error: {0}", ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Close();
                        return;
                    }
                }

                LoadHeader();
                LoadCheats();
            }
            catch (Exception ex)
            {
                if (_isClosed)
                {
                    return;
                }

                AppHost.Logger?.Invoke(ex, "Unable to load game cheats.");
                _ = MessageBox.Show(this, string.Format("Unable to load game cheats. Error: {0}", ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
            finally
            {
                if (!_isClosed && !IsDisposed)
                {
                    UseWaitCursor = false;
                    _listCheats.Enabled = true;
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _isClosed = true;
            base.OnFormClosed(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        public async Task<GameCheatsData> GetGameCheatsPS3(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return new GameCheatsData { Cheats = new List<Cheats>() };
            }

            string response = await HttpClient.GetStringAsync(CheatsBaseUrl + fileName);
            return JsonConvert.DeserializeObject<GameCheatsData>(response) ?? new GameCheatsData { Cheats = new List<Cheats>() };
        }

        private void ApplyLanguage()
        {
            _buttonApply.Text = Lookup("LABEL_APPLY_CHEAT", "Apply Cheat");
            _buttonReport.Text = Lookup("LABEL_REPORT_ISSUE", "Report Issue");
            _buttonHelp.Text = Lookup("LABEL_HELP_SUPPORT", "Help && Support");
        }

        private void LoadHeader()
        {
            _labelGame.Text = GameItem?.Game ?? string.Empty;
            _labelVersion.Text = "- " + (GameItem?.Version ?? string.Empty);
            _labelRegion.Text = "(" + (GameItem?.Region ?? string.Empty) + ")";
        }

        private void LoadCheats()
        {
            List<Cheats> cheats = GameCheats?.Cheats ?? new List<Cheats>();

            _listCheats.BeginUpdate();
            _listCheats.Items.Clear();
            foreach (Cheats cheat in cheats)
            {
                _listCheats.Items.Add(new ListViewItem(cheat.Name ?? string.Empty) { Tag = cheat });
            }
            _listCheats.EndUpdate();

            // Select the first cheat explicitly, so Apply and the selection agree when the dialog opens.
            _selectedCheatItem = cheats.Count > 0 ? cheats[0] : null;
            if (_listCheats.Items.Count > 0)
            {
                _listCheats.Items[0].Selected = true;
            }

            UpdateActionState();
        }

        private void UpdateActionState()
        {
            _buttonApply.Enabled = _selectedCheatItem != null;
        }

        /// <summary>
        /// Keeps the Name column filling the list width.
        /// </summary>
        private void ResizeColumns()
        {
            _columnName.Width = Math.Max(Font.Height * 8, _listCheats.ClientSize.Width - 4);
        }

        private void ListCheats_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedCheatItem = _listCheats.SelectedItems.Count > 0
                ? _listCheats.SelectedItems[0].Tag as Cheats
                : null;
            UpdateActionState();
        }

        private void ButtonApply_Click(object sender, EventArgs e)
        {
            if (_selectedCheatItem == null)
            {
                return;
            }

            if (ContainsUnimplementedOpcodes(_selectedCheatItem))
            {
                _ = MessageBox.Show(this, Lookup("CHEAT_NOT_SUPPORTED", "This cheat can't be applied because it contains opcode that hasn't been implemented yet."), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (ApplyCheat == null)
            {
                _ = MessageBox.Show(this, NoConsoleWriterText, Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                if (ApplyCheat(_selectedCheatItem))
                {
                    _ = MessageBox.Show(this, Lookup("CHEAT_APPLIED", "Successfully applied cheat to game."), Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _ = MessageBox.Show(this, string.Format(Lookup("CHEAT_NOT_APPLIED", "Unable to apply cheat to game. Error: {0}"), "The console did not accept the cheat."), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to apply cheat.");
                _ = MessageBox.Show(this, string.Format(Lookup("CHEAT_NOT_APPLIED", "Unable to apply cheat to game. Error: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonReport_Click(object sender, EventArgs e)
        {
            ReportIssueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonHelp_Click(object sender, EventArgs e)
        {
            try
            {
                if (OpenUrl != null)
                {
                    OpenUrl(Urls.WebsiteHelp);
                }
                else
                {
                    _ = Process.Start(Urls.WebsiteHelp);
                }
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to open the help page.");
            }
        }

        private static bool ContainsUnimplementedOpcodes(Cheats cheat)
        {
            return cheat.Offsets != null && cheat.Offsets.Exists(x => x != null && x.ContainsUnimplementedOpcodes());
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
