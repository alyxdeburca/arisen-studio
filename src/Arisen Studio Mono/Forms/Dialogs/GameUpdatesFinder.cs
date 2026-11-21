using ArisenStudio.Extensions;
using ArisenStudio.Models.GameData.PS3;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Searches the Sony update server for the updates of a PS3 title ID, and downloads or copies the selected update.
    /// Stock WinForms port of the old DevExpress GameUpdatesFinder form.
    /// Installing to the console needs two host hooks: IsConsoleConnected and UploadFile. While either is null, or the probe returns false, Install is disabled.
    /// Set Language before the dialog is shown when localised texts are wanted; otherwise the English texts are used.
    /// </summary>
    internal class GameUpdatesFinder : Form
    {
        private readonly TableLayoutPanel _root;
        private readonly Label _labelTitleId;
        private readonly TextBox _textBoxTitleId;
        private readonly Button _buttonSearch;
        private readonly Label _labelHeaderTitle;
        private readonly Label _labelTitle;
        private readonly ListView _listUpdates;
        private readonly ColumnHeader _columnFileName;
        private readonly ColumnHeader _columnVersion;
        private readonly ColumnHeader _columnSize;
        private readonly ColumnHeader _columnMinFirmware;
        private readonly Button _buttonInstall;
        private readonly Button _buttonDownload;
        private readonly Button _buttonCopyUrl;
        private readonly Button _buttonCopySha1;
        private readonly Label _labelStatus;

        private bool _busy;

        public GameUpdatesFinder()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Updates Finder";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            int pad = Font.Height / 2;

            _labelTitleId = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, pad, 0), Text = "Title ID" };
            _textBoxTitleId = new TextBox { Width = Font.Height * 10, Margin = new Padding(0, 0, pad, 0) };
            _buttonSearch = new Button { AutoSize = true, Text = "Search", Margin = new Padding(0) };
            _buttonSearch.Click += ButtonSearch_Click;

            FlowLayoutPanel search = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            search.Controls.Add(_labelTitleId);
            search.Controls.Add(_textBoxTitleId);
            search.Controls.Add(_buttonSearch);

            _labelHeaderTitle = new Label { AutoSize = true, UseMnemonic = false, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, pad, 0), Text = "Title:" };
            _labelTitle = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0), Text = "-" };

            FlowLayoutPanel title = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            title.Controls.Add(_labelHeaderTitle);
            title.Controls.Add(_labelTitle);

            _columnFileName = new ColumnHeader { Text = "File Name", Width = Font.Height * 20 };
            _columnVersion = new ColumnHeader { Text = "Version", Width = 70 };
            _columnSize = new ColumnHeader { Text = "Size", Width = 100 };
            _columnMinFirmware = new ColumnHeader { Text = "Min. FW", Width = 60 };

            _listUpdates = new ListView
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
            _listUpdates.Columns.AddRange(new[] { _columnFileName, _columnVersion, _columnSize, _columnMinFirmware });
            _listUpdates.SelectedIndexChanged += ListUpdates_SelectedIndexChanged;
            _listUpdates.Resize += (s, e) => ResizeColumns();

            _buttonInstall = new Button { AutoSize = true, Enabled = false, Text = "Install File", Margin = new Padding(0, 0, pad, 0) };
            _buttonInstall.Click += ButtonInstall_Click;
            _buttonDownload = new Button { AutoSize = true, Enabled = false, Text = "Download File", Margin = new Padding(0, 0, pad, 0) };
            _buttonDownload.Click += ButtonDownload_Click;
            _buttonCopyUrl = new Button { AutoSize = true, Enabled = false, Text = "Copy URL to Clipboard", Margin = new Padding(0, 0, pad, 0) };
            _buttonCopyUrl.Click += ButtonCopyUrl_Click;
            _buttonCopySha1 = new Button { AutoSize = true, Enabled = false, Text = "Copy SHA1 to Clipboard", Margin = new Padding(0) };
            _buttonCopySha1.Click += ButtonCopySha1_Click;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            actions.Controls.Add(_buttonInstall);
            actions.Controls.Add(_buttonDownload);
            actions.Controls.Add(_buttonCopyUrl);
            actions.Controls.Add(_buttonCopySha1);

            _labelStatus = new Label { AutoSize = true, UseMnemonic = false, Dock = DockStyle.Fill, Margin = new Padding(0), Padding = new Padding(pad, 0, pad, pad), Text = "Idle" };

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.Controls.Add(search, 0, 0);
            _root.Controls.Add(title, 0, 1);
            _root.Controls.Add(_listUpdates, 0, 2);
            _root.Controls.Add(actions, 0, 3);
            _root.Controls.Add(_labelStatus, 0, 4);

            Controls.Add(_root);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Returns true when a PS3 console is connected. Null (the default) means no connection can be checked, so Install stays disabled.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; }

        /// <summary>
        /// Uploads a local file to a remote console path. Null (the default) means no console writer is attached, so Install stays disabled.
        /// </summary>
        public Func<string, string, Task> UploadFile { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            SizeToContent();
            ResizeColumns();
            UpdateActionState();
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
            Text = Lookup("GAME_UPDATES_FINDER", "Game Updates Finder");
            _buttonSearch.Text = Lookup("LABEL_SEARCH", "Search");
            _labelTitleId.Text = Lookup("LABEL_TITLE_ID", "Title ID");
            _labelHeaderTitle.Text = Lookup("LABEL_TITLE", "Title") + ":";
            _buttonInstall.Text = Lookup("LABEL_INSTALL_FILE", "Install File");
            _buttonDownload.Text = Lookup("LABEL_DOWNLOAD_FILE", "Download File");
            _buttonCopySha1.Text = Lookup("LABEL_COPY_SHA1", "Copy SHA1 to Clipboard");
            _buttonCopyUrl.Text = Lookup("LABEL_COPY_URL", "Copy URL to Clipboard");
            _columnFileName.Text = Lookup("LABEL_FILE_NAME", "File Name");
            _columnVersion.Text = Lookup("LABEL_VERSION", "Version");
            _columnSize.Text = Lookup("LABEL_SIZE", "Size");
            _columnMinFirmware.Text = Lookup("LABEL_MIN_FW", "Min. FW");
            _labelStatus.Text = Lookup("IDLE", "Idle");
        }

        /// <summary>
        /// Sizes the form to its content, at least 44 font heights wide and 28 high, clamped to 90% of the working area of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Size preferred = _root.PreferredSize;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 44, preferred.Width), (int)(work.Width * 0.9));
            int height = Math.Min(Math.Max(preferred.Height, h * 28), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        /// <summary>
        /// Keeps the File Name column filling the list width after the three fixed columns.
        /// </summary>
        private void ResizeColumns()
        {
            _columnFileName.Width = Math.Max(Font.Height * 8, _listUpdates.ClientSize.Width - _columnVersion.Width - _columnSize.Width - _columnMinFirmware.Width - 4);
        }

        private void ListUpdates_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateActionState();
        }

        private Package SelectedPackage()
        {
            return _listUpdates.SelectedItems.Count > 0 ? _listUpdates.SelectedItems[0].Tag as Package : null;
        }

        /// <summary>
        /// Enables the row actions only when a row is selected and no operation is running. Install also needs the console hooks.
        /// </summary>
        private void UpdateActionState()
        {
            bool hasSelection = SelectedPackage() != null && !_busy;
            _buttonDownload.Enabled = hasSelection;
            _buttonCopyUrl.Enabled = hasSelection;
            _buttonCopySha1.Enabled = hasSelection;
            _buttonInstall.Enabled = hasSelection && IsConnected();
            _buttonSearch.Enabled = !_busy;
        }

        private bool IsConnected()
        {
            return UploadFile != null && IsConsoleConnected != null && IsConsoleConnected();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UpdateActionState();
        }

        private void SetStatus(string text, Exception ex = null)
        {
            _labelStatus.Text = text;
            _labelStatus.Refresh();

            if (ex != null)
            {
                AppHost.Logger?.Invoke(ex, text);
            }
        }

        private void ButtonSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_textBoxTitleId.Text))
            {
                _ = MessageBox.Show(this, Lookup("YOU_MUST_ENTER_TITLE_ID", "You must enter a title ID."), Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                SetStatus(Lookup("LABEL_SEARCHING", "Searching..."));

                TitlePatch gameUpdates = HttpExtensions.GetGameUpdatesFromTitleId(_textBoxTitleId.Text, AppHost.Logger);

                if (gameUpdates == null)
                {
                    _ = MessageBox.Show(this, Lookup("INVALID_TITLE_ID", "Invalid ID or cannot be found. Are you sure this is a valid PS3 Title ID?"), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _listUpdates.BeginUpdate();
                _listUpdates.Items.Clear();
                _labelTitle.Text = Regex.Replace(gameUpdates.Tag.Package.Last().Paramsfo.Title, @"\(.*?\)", string.Empty).Trim().Replace("Â®", "®");

                List<Package> packages = gameUpdates.Tag.Package;
                packages.Reverse();

                List<ListViewItem> rows = new List<ListViewItem>();
                foreach (Package update in packages)
                {
                    string size = AppHost.Settings.UseFormattedFileSizes
                        ? long.Parse(update.Size).Bytes().Humanize("#")
                        : update.Size + " " + Lookup("LABEL_BYTES", "Bytes");

                    string[] values =
                    {
                        Path.GetFileName(new Uri(update.Url).LocalPath),
                        update.Version.TrimStart('0'),
                        size,
                        update.Ps3SystemVer.TrimStart('0').Substring(0, update.Ps3SystemVer.Length - 3)
                    };
                    rows.Add(new ListViewItem(values) { Tag = update });
                }

                _listUpdates.Items.AddRange(rows.ToArray());
                _listUpdates.EndUpdate();

                if (_listUpdates.Items.Count > 0)
                {
                    _listUpdates.Items[0].Selected = true;
                }

                SetStatus(Lookup("LABEL_SEARCH_SUCCESS", "Successfully fetched items."));
            }
            catch (Exception ex)
            {
                _listUpdates.EndUpdate();
                SetStatus(string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), ex);
                _ = MessageBox.Show(this, string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                UpdateActionState();
            }
        }

        private async void ButtonInstall_Click(object sender, EventArgs e)
        {
            Package update = SelectedPackage();
            if (update == null)
            {
                return;
            }

            if (!IsConnected())
            {
                _ = MessageBox.Show(this, Lookup("YOU_MUST_BE_CONNECTED_TO_USE_FEATURE", "You must be connected to your console to use this feature."), Lookup("NOT_CONNECTED", "Not Connected"));
                return;
            }

            string fileName = Path.GetFileName(update.Url);
            string localPath = Path.Combine(AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData), fileName);
            string remotePath = AppHost.Settings.PackageInstallPathPS3 + fileName;

            SetBusy(true);
            try
            {
                SetStatus(string.Format(Lookup("FILE_DOWNLOADING", "Downloading file: {0}"), fileName));
                bool downloaded = await HttpExtensions.DownloadFileAsync(update.Url, localPath);
                if (IsDisposed)
                {
                    return;
                }

                if (!downloaded)
                {
                    SetStatus("The download did not complete.");
                    return;
                }

                SetStatus(string.Format(Lookup("FILE_INSTALLING", "Installing file: {0}"), fileName));
                await UploadFile(localPath, remotePath);

                SetStatus(Lookup("FILE_INSTALL_SUCCESS", "Successfully installed file(s)."));
                _ = MessageBox.Show(this, Lookup("FILE_INSTALL_SUCCESS", "Successfully installed file(s)."), Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    SetStatus(string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), ex);
                    _ = MessageBox.Show(this, string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                }
            }
        }

        private async void ButtonDownload_Click(object sender, EventArgs e)
        {
            Package update = SelectedPackage();
            if (update == null)
            {
                return;
            }

            string fileName = Path.GetFileName(update.Url);

            string folderPath;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = Lookup("CHOOSE_FOLDER_LOCATION", "Choose a folder location") })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                folderPath = dialog.SelectedPath;
            }

            SetBusy(true);
            try
            {
                SetStatus(string.Format(Lookup("FILE_DOWNLOADING", "Downloading file: {0}"), fileName));
                bool downloaded = await HttpExtensions.DownloadFileAsync(update.Url, Path.Combine(folderPath, fileName));
                if (IsDisposed)
                {
                    return;
                }

                if (!downloaded)
                {
                    SetStatus("The download did not complete.");
                    return;
                }

                SetStatus(Lookup("FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));
                _ = MessageBox.Show(this, Lookup("FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."), Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    SetStatus(string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), ex);
                    _ = MessageBox.Show(this, string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                }
            }
        }

        private void ButtonCopyUrl_Click(object sender, EventArgs e)
        {
            Package update = SelectedPackage();
            if (update == null || string.IsNullOrEmpty(update.Url))
            {
                return;
            }

            try
            {
                Clipboard.SetText(update.Url);
            }
            catch (Exception ex)
            {
                SetStatus(string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), ex);
                return;
            }

            SetStatus(Lookup("COPIED_URL", "URL has been copied to clipboard."));
            _ = MessageBox.Show(this, Lookup("COPIED_URL", "URL has been copied to clipboard."), Lookup("LABEL_COPIED", "Copied"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ButtonCopySha1_Click(object sender, EventArgs e)
        {
            Package update = SelectedPackage();
            if (update == null || string.IsNullOrEmpty(update.Sha1Sum))
            {
                return;
            }

            try
            {
                Clipboard.SetText(update.Sha1Sum);
            }
            catch (Exception ex)
            {
                SetStatus(string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), ex);
                return;
            }

            SetStatus(Lookup("COPIED_SHA1", "SHA1 has been copied to clipboard."));
            _ = MessageBox.Show(this, Lookup("COPIED_SHA1", "SHA1 has been copied to clipboard."), Lookup("LABEL_COPIED", "Copied"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
