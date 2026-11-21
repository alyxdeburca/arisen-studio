using ArisenStudio.Controls;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Lists the trainers for one Xbox 360 game and offers Download, Install, Report Issue and Help &amp; Support.
    /// Stock WinForms port of the old DevExpress GameTrainersDialog form.
    /// The dialog opens no other form and never touches the console: the buttons raise events and the host does the work.
    /// Set TrainerGameData, Settings, Language, Database, ConsoleProfile and IsConsoleConnected before the dialog is shown.
    /// </summary>
    internal class GameTrainersDialog : Form
    {
        // Text of the old label under the grid (the old form read it from its .resx, which is English only).
        private const string NoteText =
            "Before installing,make sure you have set the correct Aurora path in the Settings page.\r\n\r\n" +
            "Unload Freestyle Plugin. Set trainer options in aurora, and launch game.\r\n\r\n" +
            "All credit goes to their appropriate developers, as they created the trainers, any info on a specific one, would be best gotten over there. " +
            "Most trainers are TU specific, so if it doesn't work, check online and it will tell you which TU is needed for it to work.";

        private readonly DetailLayout _layout;
        private readonly TableLayoutPanel _body;
        private readonly ListView _listTrainers;
        private readonly ColumnHeader _columnName;
        private readonly ColumnHeader _columnType;
        private readonly ColumnHeader _columnUpdated;
        private readonly ColumnHeader _columnFiles;
        private readonly Label _labelNote;
        private readonly Button _buttonDownload;
        private readonly Button _buttonInstall;
        private readonly Button _buttonReport;
        private readonly Button _buttonHelp;

        public GameTrainersDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Trainers";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            // Columns: Name fills the rest of the width; the other three keep fixed widths from the font height.
            _columnName = new ColumnHeader { Text = "Name", Width = h * 10 };
            _columnType = new ColumnHeader { Text = "Type", Width = h * 5 };
            _columnUpdated = new ColumnHeader { Text = "Last Updated", Width = h * 8 };
            _columnFiles = new ColumnHeader { Text = "# Files", Width = h * 6 };

            _listTrainers = new ListView
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
            _listTrainers.Columns.AddRange(new[] { _columnName, _columnType, _columnUpdated, _columnFiles });
            _listTrainers.SelectedIndexChanged += ListTrainers_SelectedIndexChanged;
            _listTrainers.Resize += (s, e) => ResizeColumns();

            _labelNote = new Label
            {
                AutoSize = true,
                Text = NoteText,
                Margin = new Padding(0, pad, 0, 0)
            };

            // Body: the trainer list at a fixed height of 12 font heights, then the note. The note wraps to the body width.
            _body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            _ = _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _body.RowStyles.Add(new RowStyle(SizeType.Absolute, h * 12));
            _ = _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _body.Controls.Add(_listTrainers, 0, 0);
            _body.Controls.Add(_labelNote, 0, 1);
            _body.Resize += (s, e) => _labelNote.MaximumSize = new Size(Math.Max(1, _body.ClientSize.Width - _body.Padding.Horizontal), 0);

            // Shared shell: header with crumbs and name, the body, and the action row ending with Close.
            _layout = new DetailLayout
            {
                Dock = DockStyle.Fill,
                CloseText = "Close"
            };
            _layout.CloseRequested += (s, e) => Close();
            _layout.Body = _body;

            // AddAction places each button to the left of the ones added before it, after Close.
            // Added Help, Report, Install, Download, so the visual order is Download, Install, Report Issue, Help & Support, Close.
            _buttonHelp = _layout.AddAction("Help && Support", ButtonHelp_Click);
            _buttonReport = _layout.AddAction("Report Issue", ButtonReport_Click);
            _buttonInstall = _layout.AddAction("Install", ButtonInstall_Click);
            _buttonDownload = _layout.AddAction("Download", ButtonDownload_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The game to show: its title ID and its trainers. Read once when the dialog loads; set it before showing the dialog.
        /// </summary>
        public TrainerGameItem TrainerGameData { get; set; }

        /// <summary>
        /// The trainer that Download and Install act on. The first trainer is selected when the dialog loads, and the
        /// selection follows the list. Null when the game has no trainers. Read it after the dialog closes or in an event handler.
        /// </summary>
        public TrainerItem SelectedTrainerItem { get; private set; }

        /// <summary>
        /// Settings that control the Last Updated text and whether Install is enabled for a USB install.
        /// Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The database. Used only to turn the title ID into the game title; when it is null or has no title, the title ID is shown.
        /// </summary>
        public DatabaseClient Database { get; set; }

        /// <summary>
        /// The console profile. Kept for parity with the old static; this form does not read it.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Install is enabled when a trainer is selected and this returns true,
        /// or Settings.InstallGameModsToUsbDevice is set. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised when the Download button is clicked. SelectedTrainerItem holds the trainer. The host starts the download.
        /// </summary>
        public event EventHandler DownloadTrainerRequested;

        /// <summary>
        /// Raised when the Install button is clicked. SelectedTrainerItem holds the trainer. The host starts the install.
        /// </summary>
        public event EventHandler InstallTrainerRequested;

        /// <summary>
        /// Raised when the Report Issue button is clicked. The old button did nothing; the host decides what to do.
        /// </summary>
        public event EventHandler ReportIssueRequested;

        /// <summary>
        /// Raised when the Help &amp; Support button is clicked. The old button did nothing; the host decides what to do.
        /// </summary>
        public event EventHandler HelpSupportRequested;

        /// <summary>
        /// Raised when the trainers cannot be listed. The dialog closes after this event. The host shows the error.
        /// </summary>
        public event EventHandler<System.IO.ErrorEventArgs> LoadFailed;

        /// <summary>
        /// Raised when the InstallTrainerRequested handler throws. The dialog stays open. The host shows the error.
        /// </summary>
        public event EventHandler<System.IO.ErrorEventArgs> InstallFailed;

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();

            try
            {
                ShowGameTrainers();
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to load trainers cheats.");
                LoadFailed?.Invoke(this, new System.IO.ErrorEventArgs(ex));
                Close();
            }

            SizeToContent();
            ResizeColumns();
            base.OnLoad(e);
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

        private void ApplyLanguage()
        {
            _buttonDownload.Text = Lookup("LABEL_DOWNLOAD", "Download");
            _buttonInstall.Text = Lookup("LABEL_INSTALL", "Install");
            _buttonReport.Text = Lookup("LABEL_REPORT_ISSUE", "Report Issue");
            _buttonHelp.Text = Lookup("LABEL_HELP_SUPPORT", "Help && Support");
        }

        private void ShowGameTrainers()
        {
            TrainerGameItem game = TrainerGameData;
            List<TrainerItem> trainers = game?.Trainers ?? new List<TrainerItem>();

            if (game == null)
            {
                _layout.SetCrumbs("Trainers", string.Empty, string.Empty);
                _layout.SetName(string.Empty);
            }
            else
            {
                _layout.SetCrumbs("Trainers", "Xbox 360", game.TitleId);
                _layout.SetName(TitleNameFor(game.TitleId));
            }

            _listTrainers.BeginUpdate();
            _listTrainers.Items.Clear();
            foreach (TrainerItem trainer in trainers)
            {
                string[] values =
                {
                    trainer.Name ?? string.Empty,
                    trainer.Type ?? string.Empty,
                    UpdatedText(trainer),
                    (trainer.InstallPaths?.Length ?? 0).ToString()
                };

                _listTrainers.Items.Add(new ListViewItem(values) { Tag = trainer });
            }
            _listTrainers.EndUpdate();

            // The first trainer is selected, as the old form did. Null when there are none.
            SelectedTrainerItem = trainers.Count > 0 ? trainers[0] : null;
            if (_listTrainers.Items.Count > 0)
            {
                _listTrainers.Items[0].Selected = true;
            }

            UpdateActionState();
        }

        /// <summary>
        /// The title for the title ID, or the title ID itself when the database has no title for it.
        /// </summary>
        private string TitleNameFor(string titleId)
        {
            string title = null;
            try
            {
                title = Database?.TitleIdsX360?.GetTitleFromTitleId(titleId);
            }
            catch (Exception)
            {
                // The database lookup throws for a title ID it does not know. Fall back to the title ID.
            }

            return string.IsNullOrEmpty(title) ? titleId ?? string.Empty : title;
        }

        private string UpdatedText(TrainerItem trainer)
        {
            SettingsData settings = Settings ?? AppHost.Settings;
            return settings.UseRelativeTimes ? trainer.LastUpdated.Humanize() : trainer.LastUpdated.ToString();
        }

        private void UpdateActionState()
        {
            bool hasSelection = SelectedTrainerItem != null;
            _buttonDownload.Enabled = hasSelection;
            _buttonInstall.Enabled = hasSelection && IsInstallAvailable();
        }

        private bool IsInstallAvailable()
        {
            SettingsData settings = Settings ?? AppHost.Settings;
            Func<bool> isConnected = IsConsoleConnected;
            bool connected = isConnected != null && isConnected();
            return connected || settings.InstallGameModsToUsbDevice;
        }

        /// <summary>
        /// Keeps the Name column filling the list width after the three fixed columns.
        /// </summary>
        private void ResizeColumns()
        {
            int fixedWidth = _columnType.Width + _columnUpdated.Width + _columnFiles.Width;
            _columnName.Width = Math.Max(Font.Height * 8, _listTrainers.ClientSize.Width - fixedWidth);
        }

        /// <summary>
        /// Sizes the form 38 font heights wide and to its content tall, each clamped to 90% of the working area
        /// of the screen it is on. The width is set first so the header wraps the name to it.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 38, (int)(work.Width * 0.9));
            ClientSize = new Size(width, ClientSize.Height);

            int height = Math.Min(_layout.PreferredSize.Height, (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void ListTrainers_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedTrainerItem = _listTrainers.SelectedItems.Count > 0
                ? _listTrainers.SelectedItems[0].Tag as TrainerItem
                : null;
            UpdateActionState();
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            DownloadTrainerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            try
            {
                InstallTrainerRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                InstallFailed?.Invoke(this, new System.IO.ErrorEventArgs(ex));
            }
        }

        private void ButtonReport_Click(object sender, EventArgs e)
        {
            ReportIssueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonHelp_Click(object sender, EventArgs e)
        {
            HelpSupportRequested?.Invoke(this, EventArgs.Empty);
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
