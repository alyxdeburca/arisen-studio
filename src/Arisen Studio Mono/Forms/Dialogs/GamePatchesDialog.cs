using ArisenStudio.Models.Database;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Lists the Xbox 360 patches for one game and applies the selected patch.
    /// Stock WinForms port of the old DevExpress GamePatchesDialog.
    /// The dialog never talks to the console: Apply calls the ApplyPatch delegate and the host performs the memory writes.
    /// Set GamePatchItem, Language and ApplyPatch before the dialog is shown.
    /// </summary>
    internal class GamePatchesDialog : Form
    {
        private const string NoConsoleWriterText =
            "Applying a patch needs a connection to the console, which is not available in this build.";

        private readonly Label _labelGame;
        private readonly Label _labelTitleId;
        private readonly ListView _listPatches;
        private readonly ColumnHeader _columnName;
        private readonly ColumnHeader _columnAuthor;
        private readonly Button _buttonApply;
        private readonly Button _buttonReport;
        private readonly Button _buttonClose;

        public GamePatchesDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Cheats";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(650, 400);

            int pad = Font.Height / 2;

            _labelGame = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, pad, 0),
                Text = "Game"
            };

            _labelTitleId = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0),
                Text = "(TitleId)"
            };

            _columnName = new ColumnHeader { Text = "Name", Width = Font.Height * 20 };
            _columnAuthor = new ColumnHeader { Text = "Author", Width = 130 };

            _listPatches = new ListView
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
            _listPatches.Columns.AddRange(new[] { _columnName, _columnAuthor });
            _listPatches.SelectedIndexChanged += ListPatches_SelectedIndexChanged;
            _listPatches.Resize += (s, e) => ResizeColumns();

            _buttonApply = new Button { AutoSize = true, Enabled = false, Text = "Apply Cheat", Margin = new Padding(pad, 0, 0, 0) };
            _buttonApply.Click += ButtonApply_Click;

            _buttonReport = new Button { AutoSize = true, Text = "Report Issue", Margin = new Padding(pad, 0, 0, 0) };
            _buttonReport.Click += ButtonReport_Click;

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
            header.Controls.Add(_labelTitleId);

            // Right-to-left so the first control added sits at the right edge: Apply, Report Issue, Close.
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
            root.Controls.Add(_listPatches, 0, 1);
            root.Controls.Add(actions, 0, 2);

            Controls.Add(root);
        }

        /// <summary>
        /// The game and its patches. Set it before the dialog is shown. A null value or an empty patch list shows an empty list.
        /// </summary>
        public GamePatchItemData GamePatchItem { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Applies one patch to the console and returns true when the console accepted every write.
        /// The host performs the Be8, Be16 and Be32 writes (WriteByte, WriteUInt16, WriteUInt32), each list null-checked.
        /// Null (the default) means no console writer is attached: Apply then shows a message and never reports success.
        /// </summary>
        public Func<Patch, bool> ApplyPatch { get; set; }

        private Patch SelectedPatchItem { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            LoadPatches();
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
            _buttonApply.Text = Lookup("LABEL_APPLY_CHEAT", "Apply Cheat");
            _buttonReport.Text = Lookup("LABEL_REPORT_ISSUE", "Report Issue");
        }

        private void LoadPatches()
        {
            GamePatchItemData game = GamePatchItem;
            List<Patch> patches = game?.Patch ?? new List<Patch>();

            _labelGame.Text = game?.TitleName ?? string.Empty;
            _labelTitleId.Text = game == null ? string.Empty : "(" + game.TitleId + ")";

            _listPatches.BeginUpdate();
            _listPatches.Items.Clear();
            foreach (Patch patch in patches)
            {
                string[] values = { patch.Name ?? string.Empty, patch.Author ?? string.Empty };
                _listPatches.Items.Add(new ListViewItem(values) { Tag = patch });
            }
            _listPatches.EndUpdate();

            // Select the first patch explicitly, so Apply and the selection agree when the dialog opens.
            SelectedPatchItem = patches.Count > 0 ? patches[0] : null;
            if (_listPatches.Items.Count > 0)
            {
                _listPatches.Items[0].Selected = true;
            }

            UpdateActionState();
        }

        private void UpdateActionState()
        {
            _buttonApply.Enabled = SelectedPatchItem != null;
        }

        /// <summary>
        /// Keeps the Name column filling the list width after the fixed Author column.
        /// </summary>
        private void ResizeColumns()
        {
            _columnName.Width = Math.Max(Font.Height * 8, _listPatches.ClientSize.Width - _columnAuthor.Width - 4);
        }

        private void ListPatches_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedPatchItem = _listPatches.SelectedItems.Count > 0
                ? _listPatches.SelectedItems[0].Tag as Patch
                : null;
            UpdateActionState();
        }

        private void ButtonApply_Click(object sender, EventArgs e)
        {
            if (SelectedPatchItem == null)
            {
                _ = MessageBox.Show(this, "SelectedPatchItem is null", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (ApplyPatch == null)
            {
                _ = MessageBox.Show(this, NoConsoleWriterText, Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                if (ApplyPatch(SelectedPatchItem))
                {
                    _ = MessageBox.Show(this, Lookup("CHEAT_APPLIED", "Successfully applied cheat to game."), Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _ = MessageBox.Show(this, string.Format(Lookup("CHEAT_NOT_APPLIED", "Unable to apply cheat to game. Error: {0}"), "The console did not accept the patch."), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to apply patch for Xbox 360.");
                _ = MessageBox.Show(this, string.Format(Lookup("CHEAT_NOT_APPLIED", "Unable to apply cheat to game. Error: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonReport_Click(object sender, EventArgs e)
        {
            // Same text as the old form. It opens nothing; the issue link is handled by the host.
            StringBuilder message = new StringBuilder()
                .Append("You will now be redirected to our GitHub Issues page for ArisenStudio. All details will be automatically filled for you. Please provide information about the issue to help us fix your problem.\n")
                .AppendLine("Click the 'Submit' button to open a new issue which can help us fix any problems.");

            _ = MessageBox.Show(message.ToString(), "Opening GitHub Issues", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
