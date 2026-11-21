using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Lists the game file backups, shows the details of the selected one, edits or removes backups, and backs up
    /// or restores a game file on the console.
    /// Stock WinForms port of the old DevExpress BackupFilesManager form (the old grid is a ListView here).
    /// Host hooks, set before the form is shown:
    ///  - BackupFiles: the backup data to show (Models.Resources.BackupFilesData). Edits go straight to this object;
    ///    BackupFilesChanged is raised after each change so the host can save it.
    ///  - Categories: used to show the game title for a category id (falls back to the id).
    ///  - FileTransfer: the FTP boundary for Backup File and Restore File (default: a stub that fails with a message).
    ///  - IsConsoleConnected: enables Backup File and Restore File.
    /// </summary>
    public class BackupFilesManager : Form
    {
        private readonly ListView _listBackups;
        private readonly Button _buttonEdit;
        private readonly Button _buttonDelete;
        private readonly Button _buttonDeleteAll;
        private readonly Button _buttonBackupFile;
        private readonly Button _buttonRestoreFile;
        private readonly GroupBox _groupDetails;
        private readonly Label _headerTitle;
        private readonly Label _headerFileName;
        private readonly Label _headerFileSize;
        private readonly Label _headerCreatedOn;
        private readonly Label _headerLocalPath;
        private readonly Label _headerInstallPath;
        private readonly Label _valueTitle;
        private readonly Label _valueFileName;
        private readonly Label _valueFileSize;
        private readonly Label _valueCreatedOn;
        private readonly Label _valueLocalPath;
        private readonly Label _valueInstallPath;
        private readonly ToolStripStatusLabel _status;

        public BackupFilesManager()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _listBackups = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            _listBackups.Columns.Add("Game Title", h * 14);
            _listBackups.Columns.Add("File Name", h * 14);
            _listBackups.Columns.Add("File Size", h * 8);
            _listBackups.Columns.Add("Created On", h * 10);

            _headerTitle = ToolFormSupport.NewLabel(string.Empty);
            _headerFileName = ToolFormSupport.NewLabel(string.Empty);
            _headerFileSize = ToolFormSupport.NewLabel(string.Empty);
            _headerCreatedOn = ToolFormSupport.NewLabel(string.Empty);
            _headerLocalPath = ToolFormSupport.NewLabel(string.Empty);
            _headerInstallPath = ToolFormSupport.NewLabel(string.Empty);
            _valueTitle = NewValueLabel();
            _valueFileName = NewValueLabel();
            _valueFileSize = NewValueLabel();
            _valueCreatedOn = NewValueLabel();
            _valueLocalPath = NewValueLabel();
            _valueInstallPath = NewValueLabel();

            TableLayoutPanel details = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddRow(details, _headerTitle, _valueTitle);
            ToolFormSupport.AddRow(details, _headerFileName, _valueFileName);
            ToolFormSupport.AddRow(details, _headerFileSize, _valueFileSize);
            ToolFormSupport.AddRow(details, _headerCreatedOn, _valueCreatedOn);
            ToolFormSupport.AddRow(details, _headerLocalPath, _valueLocalPath);
            ToolFormSupport.AddRow(details, _headerInstallPath, _valueInstallPath);

            _groupDetails = new GroupBox
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(pad),
                Margin = new Padding(0, 0, 0, pad)
            };
            _groupDetails.Controls.Add(details);

            _buttonEdit = ToolFormSupport.NewButton(this, 7);
            _buttonDelete = ToolFormSupport.NewButton(this, 7);
            _buttonDeleteAll = ToolFormSupport.NewButton(this, 7);
            _buttonBackupFile = ToolFormSupport.NewButton(this, 7);
            _buttonRestoreFile = ToolFormSupport.NewButton(this, 7);
            _buttonRestoreFile.Margin = Padding.Empty;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddFullRow(root, _listBackups, true);
            ToolFormSupport.AddFullRow(root, _groupDetails, false);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonEdit, _buttonDelete, _buttonDeleteAll, _buttonBackupFile, _buttonRestoreFile), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _listBackups.SelectedIndexChanged += ListBackups_SelectedIndexChanged;
            _buttonEdit.Click += ButtonEdit_Click;
            _buttonDelete.Click += ButtonDelete_Click;
            _buttonDeleteAll.Click += ButtonDeleteAll_Click;
            _buttonBackupFile.Click += ButtonBackupFile_Click;
            _buttonRestoreFile.Click += ButtonRestoreFile_Click;

            ApplyLanguage();
            UpdateButtons();

            MinimumSize = new Size(h * 40, h * 30);
            ClientSize = new Size(h * 52, h * 36);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (UseFormattedFileSizes). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The backups to show and edit. Set it before the form is shown.
        /// </summary>
        public BackupFilesData BackupFiles { get; set; } = new BackupFilesData();

        /// <summary>
        /// Category data used to turn a category id into a game title. Null shows the id.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// FTP boundary used by Backup File and Restore File.
        /// </summary>
        public IPs3FileTransfer FileTransfer { get; set; } = new UnavailablePs3FileTransfer();

        /// <summary>
        /// Whether a console is connected now. Backup File and Restore File are only enabled when this returns true.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised after the backup list was changed (edit, delete, delete all). The host can save BackupFilesData then.
        /// </summary>
        public event EventHandler BackupFilesChanged;

        private static Label NewValueLabel()
        {
            return new Label
            {
                AutoSize = true,
                AutoEllipsis = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 3, 0, 3),
                Text = "-"
            };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            LoadBackupFiles();
        }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("BACKUP_FILES_MANAGER", "Backup Files Manager");

            _listBackups.Columns[0].Text = Text_("LABEL_GAME_TITLE", "Game Title");
            _listBackups.Columns[1].Text = Text_("LABEL_FILE_NAME", "File Name");
            _listBackups.Columns[2].Text = Text_("LABEL_FILE_SIZE", "File Size");
            _listBackups.Columns[3].Text = Text_("LABEL_CREATED_ON", "Created On");

            _groupDetails.Text = Text_("LABEL_DETAILS", "Details");
            _headerTitle.Text = _listBackups.Columns[0].Text + ":";
            _headerFileName.Text = _listBackups.Columns[1].Text + ":";
            _headerFileSize.Text = _listBackups.Columns[2].Text + ":";
            _headerCreatedOn.Text = _listBackups.Columns[3].Text + ":";
            _headerLocalPath.Text = Text_("LABEL_LOCAL_PATH", "Local Path") + ":";
            _headerInstallPath.Text = Text_("LABEL_INSTALL_PATH", "Install Path") + ":";

            _buttonEdit.Text = Text_("LABEL_EDIT", "Edit");
            _buttonDelete.Text = Text_("LABEL_DELETE", "Delete");
            _buttonDeleteAll.Text = Text_("LABEL_DELETE_ALL", "Delete All");
            _buttonBackupFile.Text = Text_("LABEL_BACKUP_FILE", "Backup File");
            _buttonRestoreFile.Text = Text_("LABEL_RESTORE_FILE", "Restore File");
        }

        private string CategoryTitle(string categoryId)
        {
            if (Categories == null)
            {
                return categoryId;
            }

            try
            {
                return Categories.GetCategoryById(categoryId).Title;
            }
            catch (Exception)
            {
                // GetCategoryById throws for an id it does not know; show the id instead.
                return categoryId;
            }
        }

        private string SizeText(BackupFile backupFile)
        {
            if (!File.Exists(backupFile.LocalPath))
            {
                return "No File Exists";
            }

            long bytes = new FileInfo(backupFile.LocalPath).Length;
            return ToolFormSupport.FormatBytes(bytes, Settings.UseFormattedFileSizes, Text_("LABEL_BYTES", "bytes"));
        }

        private BackupFile Selected()
        {
            if (_listBackups.SelectedItems.Count == 0)
            {
                return null;
            }

            return _listBackups.SelectedItems[0].Tag as BackupFile;
        }

        private void LoadBackupFiles()
        {
            BackupFile keep = Selected();

            _listBackups.BeginUpdate();
            _listBackups.Items.Clear();

            foreach (BackupFile backupFile in BackupFiles.BackupFiles)
            {
                ListViewItem item = new ListViewItem(CategoryTitle(backupFile.CategoryId));
                item.SubItems.Add(backupFile.FileName);
                item.SubItems.Add(SizeText(backupFile));
                item.SubItems.Add(backupFile.CreatedDate.ToLocalTime().ToString());
                item.Tag = backupFile;
                _listBackups.Items.Add(item);

                if (ReferenceEquals(backupFile, keep))
                {
                    item.Selected = true;
                }
            }

            _listBackups.EndUpdate();

            ShowDetails(Selected());
            UpdateButtons();
        }

        private void ShowDetails(BackupFile backupFile)
        {
            if (backupFile == null)
            {
                _valueTitle.Text = "-";
                _valueFileName.Text = "-";
                _valueFileSize.Text = "-";
                _valueCreatedOn.Text = "-";
                _valueLocalPath.Text = "-";
                _valueInstallPath.Text = "-";
                return;
            }

            _valueTitle.Text = CategoryTitle(backupFile.CategoryId);
            _valueFileName.Text = backupFile.FileName;
            _valueFileSize.Text = SizeText(backupFile);
            _valueCreatedOn.Text = backupFile.CreatedDate.ToLocalTime().ToString();
            _valueLocalPath.Text = backupFile.LocalPath;
            _valueInstallPath.Text = backupFile.InstallPath;

            if (!File.Exists(backupFile.LocalPath))
            {
                SetStatus("Local file " + backupFile.FileName + " can't be found at " + backupFile.LocalPath + ". If you moved it, edit the backup and locate the file; otherwise re-install the game update and back up the original game file again.");
            }
        }

        private void UpdateButtons()
        {
            bool hasSelection = _listBackups.SelectedItems.Count > 0;
            bool connected = IsConsoleConnected != null && IsConsoleConnected();

            _buttonEdit.Enabled = hasSelection;
            _buttonDelete.Enabled = hasSelection;
            _buttonDeleteAll.Enabled = _listBackups.Items.Count > 0;
            _buttonBackupFile.Enabled = hasSelection && connected;
            _buttonRestoreFile.Enabled = hasSelection && connected;
        }

        private void ListBackups_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowDetails(Selected());
            UpdateButtons();
        }

        private void RaiseChanged()
        {
            EventHandler handler = BackupFilesChanged;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ButtonEdit_Click(object sender, EventArgs e)
        {
            BackupFile selected = Selected();

            if (selected == null)
            {
                return;
            }

            int index = BackupFiles.BackupFiles.IndexOf(selected);

            using (BackupFileDialog dialog = new BackupFileDialog())
            {
                dialog.FileName = selected.FileName;
                dialog.CategoryId = selected.CategoryId;
                dialog.InstallPath = selected.InstallPath;
                dialog.LocalPath = selected.LocalPath;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                BackupFile updated = new BackupFile
                {
                    Platform = selected.Platform,
                    CategoryId = dialog.CategoryId,
                    FileName = dialog.FileName,
                    LocalPath = dialog.LocalPath,
                    InstallPath = dialog.InstallPath,
                    CreatedDate = selected.CreatedDate
                };

                BackupFiles.UpdateBackupFile(index, updated);
            }

            LoadBackupFiles();
            RaiseChanged();
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            BackupFile selected = Selected();

            if (selected == null)
            {
                return;
            }

            if (ToolFormSupport.Confirm(this, Text_("CONFIRM_DELETE", "Confirm Delete"), Text_("CONFIRM_DELETE_ITEM", "Do you really want to delete the selected item?")))
            {
                BackupFiles.BackupFiles.Remove(selected);
                LoadBackupFiles();
                RaiseChanged();
            }
        }

        private void ButtonDeleteAll_Click(object sender, EventArgs e)
        {
            if (ToolFormSupport.Confirm(this, Text_("CONFIRM_DELETE_ALL", "Confirm Delete All"), Text_("CONFIRM_DELETE_ALL_ITEMS", "Do you really want to delete all items?")))
            {
                BackupFiles.BackupFiles.Clear();
                LoadBackupFiles();
                RaiseChanged();
            }
        }

        private async void ButtonBackupFile_Click(object sender, EventArgs e)
        {
            BackupFile selected = Selected();

            if (selected == null)
            {
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(selected.LocalPath);

                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                await FileTransfer.DownloadFileAsync(selected.LocalPath, selected.InstallPath);

                SetStatus("Backed up " + selected.FileName + ".");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Successfully backed up file " + selected.FileName + " from " + selected.InstallPath + ".");
                LoadBackupFiles();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to backup game file. Error: " + ex.Message);
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "There was a problem downloading the file. Make sure the file exists on your console.\n\n" + ex.Message);
            }
        }

        private async void ButtonRestoreFile_Click(object sender, EventArgs e)
        {
            BackupFile selected = Selected();

            if (selected == null)
            {
                return;
            }

            try
            {
                if (!File.Exists(selected.LocalPath))
                {
                    ToolFormSupport.ShowError(this, "No File Found", "This file backup doesn't exist on your computer. If your game doesn't have mods installed, then I would suggest you backup the original files.");
                    return;
                }

                await FileTransfer.UploadFileAsync(selected.LocalPath, selected.InstallPath);

                SetStatus("Restored " + selected.FileName + ".");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Successfully restored file: " + selected.FileName + " to path: " + selected.InstallPath);
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "There was an issue attempting to restore file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "There was an issue restoring this file. Make sure the file exists on your computer and try again.\n\n" + ex.Message);
            }
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
