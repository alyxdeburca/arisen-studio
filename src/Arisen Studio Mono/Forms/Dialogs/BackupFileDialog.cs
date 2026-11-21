using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Shows and edits the details of one game file backup.
    /// Stock WinForms port of the old BackupFileDialog form.
    /// Set the four properties before showing the dialog. Read them after the dialog returns OK.
    /// Dialog result: OK = the properties were updated from the boxes, Cancel = nothing was written.
    /// </summary>
    internal class BackupFileDialog : Form
    {
        private readonly Label _labelFileName;
        private readonly Label _labelGameId;
        private readonly Label _labelInstallPath;
        private readonly Label _labelLocalPath;
        private readonly TextBox _textBoxFileName;
        private readonly TextBox _textBoxGameId;
        private readonly TextBox _textBoxInstallPath;
        private readonly TextBox _textBoxLocalPath;
        private readonly Button _buttonBrowse;
        private readonly Button _buttonOk;
        private readonly Button _buttonCancel;
        private readonly TableLayoutPanel _root;

        private string _fileName = string.Empty;
        private string _categoryId = string.Empty;
        private string _installPath = string.Empty;
        private string _localPath = string.Empty;

        public BackupFileDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Backup File Details";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            _labelFileName = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Text = "File Name:",
                Margin = new Padding(0, 0, pad, pad / 2)
            };
            _labelGameId = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Text = "Game Id:",
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _labelInstallPath = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Text = "Install File Path:",
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _labelLocalPath = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Text = "Local File Path:",
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            // The old form disabled all four boxes, so their text was hard to read and could not be copied.
            // They are read-only here instead; Browse is still the only way to change the local path.
            _textBoxFileName = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                Margin = new Padding(0, 0, pad, pad / 2),
                TabIndex = 0
            };
            _textBoxGameId = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                MinimumSize = new Size(h * 8, 0),
                Margin = new Padding(0, 0, 0, pad / 2),
                TabIndex = 1
            };
            _textBoxInstallPath = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                Margin = new Padding(0, 0, 0, pad / 2),
                TabIndex = 2
            };
            _textBoxLocalPath = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                Margin = new Padding(0, 0, pad, pad / 2),
                TabIndex = 3
            };

            _buttonBrowse = new Button
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                MinimumSize = new Size(h * 2, 0),
                Text = "...",
                Margin = new Padding(0, 0, 0, pad / 2),
                TabIndex = 4
            };
            _buttonBrowse.Click += ButtonBrowseLocalPath_Click;

            _buttonOk = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0),
                Text = "OK",
                TabIndex = 0
            };
            _buttonOk.Click += ButtonOk_Click;

            _buttonCancel = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                Text = "Cancel",
                DialogResult = System.Windows.Forms.DialogResult.Cancel,
                TabIndex = 1
            };
            _buttonCancel.Click += ButtonCancel_Click;

            // Right-to-left flow: Cancel is added first so it sits at the far right, OK to its left.
            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, pad / 2, 0, 0),
                TabIndex = 5
            };
            buttonRow.Controls.Add(_buttonCancel);
            buttonRow.Controls.Add(_buttonOk);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 7; i++)
            {
                _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            _root.Controls.Add(_labelFileName, 0, 0);
            _root.Controls.Add(_labelGameId, 1, 0);
            _root.Controls.Add(_textBoxFileName, 0, 1);
            _root.Controls.Add(_textBoxGameId, 1, 1);

            _root.Controls.Add(_labelInstallPath, 0, 2);
            _root.SetColumnSpan(_labelInstallPath, 2);
            _root.Controls.Add(_textBoxInstallPath, 0, 3);
            _root.SetColumnSpan(_textBoxInstallPath, 2);

            _root.Controls.Add(_labelLocalPath, 0, 4);
            _root.SetColumnSpan(_labelLocalPath, 2);
            _root.Controls.Add(_textBoxLocalPath, 0, 5);
            _root.Controls.Add(_buttonBrowse, 1, 5);

            _root.Controls.Add(buttonRow, 0, 6);
            _root.SetColumnSpan(buttonRow, 2);
            Controls.Add(_root);
        }

        /// <summary>
        /// File name of the backup. Copied into the File Name box when the dialog loads. Null is stored as empty.
        /// </summary>
        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value ?? string.Empty; }
        }

        /// <summary>
        /// Game id of the backup. Copied into the Game Id box when the dialog loads. Null is stored as empty.
        /// </summary>
        public string CategoryId
        {
            get { return _categoryId; }
            set { _categoryId = value ?? string.Empty; }
        }

        /// <summary>
        /// Path the file is restored to on the console. Copied into the Install File Path box when the dialog loads.
        /// Null is stored as empty.
        /// </summary>
        public string InstallPath
        {
            get { return _installPath; }
            set { _installPath = value ?? string.Empty; }
        }

        /// <summary>
        /// Path of the file on this computer. Copied into the Local File Path box when the dialog loads.
        /// Null is stored as empty.
        /// </summary>
        public string LocalPath
        {
            get { return _localPath; }
            set { _localPath = value ?? string.Empty; }
        }

        protected override void OnLoad(EventArgs e)
        {
            _textBoxFileName.Text = _fileName;
            _textBoxGameId.Text = _categoryId;
            _textBoxInstallPath.Text = _installPath;
            _textBoxLocalPath.Text = _localPath;
            SizeToContent();
            base.OnLoad(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Start on OK, and clear any selection so the File Name text is not highlighted.
            ActiveControl = _buttonOk;
            foreach (TextBox box in new[] { _textBoxFileName, _textBoxGameId, _textBoxInstallPath, _textBoxLocalPath })
            {
                box.SelectionLength = 0;
            }
        }

        /// <summary>
        /// Sizes the form to its content, at least 26 font heights wide, clamped to 90% of the working area of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Size preferred = _root.PreferredSize;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 26, preferred.Width), (int)(work.Width * 0.9));
            int height = Math.Min(Math.Max(preferred.Height, h * 14), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void ButtonBrowseLocalPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.CheckFileExists = true;
                openFileDialog.Multiselect = false;

                string initialDirectory = GetDirectoryOrEmpty(_textBoxLocalPath.Text);
                if (initialDirectory.Length > 0)
                {
                    openFileDialog.InitialDirectory = initialDirectory;
                }

                if (openFileDialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    _textBoxLocalPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void ButtonOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBoxLocalPath.Text) ||
                _textBoxLocalPath.Text.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                _ = MessageBox.Show(
                    "You must include a local file path for the game file backup.",
                    "Empty Local Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            if (string.IsNullOrWhiteSpace(_textBoxInstallPath.Text))
            {
                _ = MessageBox.Show(
                    "You must include a console path for for the game file backup. This is where the file will be restored at on the console.",
                    "Empty Console Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            _categoryId = _textBoxGameId.Text;
            _fileName = Path.GetFileName(_textBoxInstallPath.Text);
            _localPath = _textBoxLocalPath.Text;
            _installPath = _textBoxInstallPath.Text;

            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Returns the directory part of a path, or empty when the path is blank, invalid or has no directory part.
        /// </summary>
        private static string GetDirectoryOrEmpty(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (PathTooLongException)
            {
                return string.Empty;
            }
        }
    }
}
