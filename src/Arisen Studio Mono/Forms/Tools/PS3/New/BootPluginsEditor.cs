using ArisenStudio.Io;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Edits the boot_plugins.txt file on the PS3 (one plugin path per line): download it, edit the lines in a grid,
    /// save and upload it, restore the empty default, or restore the copy that was taken the first time the editor ran.
    /// Stock WinForms port of the old DevExpress BootPluginsEditor form. Line and file handling is Services\BootPluginsFile.
    /// Host hooks, set before the form is shown:
    ///  - FileTransfer: the FTP boundary (default: a stub that fails with a message, which closes the form).
    ///  - Settings: Settings.BootPluginsFilePath is the console path of the file.
    ///  - AppDataFolder / BackupFilesFolder: where the local working copy and the backup copy go (default UserFolders).
    /// </summary>
    public class BootPluginsEditor : Form
    {
        private readonly DataGridView _grid;
        private readonly Button _buttonRestoreDefault;
        private readonly Button _buttonRestoreBackup;
        private readonly Button _buttonSaveUpdate;
        private readonly ToolStripStatusLabel _status;

        public BootPluginsEditor()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                ColumnHeadersVisible = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                Margin = new Padding(0, 0, 0, pad)
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FilePath", HeaderText = "File Path", SortMode = DataGridViewColumnSortMode.NotSortable });

            _buttonRestoreDefault = ToolFormSupport.NewButton(this, 9);
            _buttonRestoreBackup = ToolFormSupport.NewButton(this, 9);
            _buttonSaveUpdate = ToolFormSupport.NewButton(this, 9);
            _buttonSaveUpdate.Margin = Padding.Empty;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddFullRow(root, _grid, true);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonRestoreDefault, _buttonRestoreBackup, _buttonSaveUpdate), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _buttonRestoreDefault.Click += ButtonRestoreDefault_Click;
            _buttonRestoreBackup.Click += ButtonRestoreBackup_Click;
            _buttonSaveUpdate.Click += ButtonSaveUpdate_Click;

            ApplyLanguage();

            MinimumSize = new Size(h * 34, h * 22);
            ClientSize = new Size(h * 44, h * 28);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (BootPluginsFilePath). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// FTP boundary used to download and upload the file.
        /// </summary>
        public IPs3FileTransfer FileTransfer { get; set; } = new UnavailablePs3FileTransfer();

        /// <summary>
        /// Folder that holds the working copy ("Boot Plugins/boot_plugins.txt" inside it).
        /// </summary>
        public string AppDataFolder { get; set; } = UserFolders.AppData;

        /// <summary>
        /// Folder that holds the backup copy ("Boot Plugins/boot_plugins.txt" inside it).
        /// </summary>
        public string BackupFilesFolder { get; set; } = UserFolders.BackupFiles;

        private string ConsoleFilePath
        {
            get { return Settings.BootPluginsFilePath; }
        }

        private string WorkingFilePath
        {
            get { return BootPluginsFile.WorkingFilePath(AppDataFolder); }
        }

        private string BackupFilePath
        {
            get { return BootPluginsFile.BackupFilePath(BackupFilesFolder); }
        }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadBootPluginsAsync();
        }

        private void ApplyLanguage()
        {
            Text = Text_("BOOT_PLUGINS_EDITOR", "Boot Plugins Editor");
            _grid.Columns[0].HeaderText = Text_("LABEL_FILE_PATH", "File Path");
            _buttonRestoreDefault.Text = Text_("LABEL_RESTORE_DEFAULT", "Restore Default");
            _buttonRestoreBackup.Text = Text_("LABEL_RESTORE_BACKUP", "Restore Backup");
            _buttonSaveUpdate.Text = Text_("LABEL_SAVE_AND_UPDATE", "Save and Update");
        }

        private async Task LoadBootPluginsAsync()
        {
            if (SmokeBlocksFileAccess())
            {
                return;
            }

            try
            {
                SetStatus("Loading boot_plugins.txt...");

                if (!await FileTransfer.FileExistsAsync(ConsoleFilePath))
                {
                    SetStatus("Unable to find boot_plugins.txt on the console.");
                    ToolFormSupport.LogError(new FileNotFoundException("boot_plugins.txt does not exist on the console: " + ConsoleFilePath), "Unable to load the boot_plugins.txt file. It doesn't exist on the console.");
                    ToolFormSupport.ShowErrorAndClose(this, Text_("ERROR", "Error"), "Unable to find the boot_plugins.txt file on your console.");
                    return;
                }

                Directory.CreateDirectory(BootPluginsFile.WorkingFolder(AppDataFolder));
                Directory.CreateDirectory(BootPluginsFile.BackupFolder(BackupFilesFolder));

                // The backup is the copy from the first time the editor ran; it is not overwritten by later loads.
                if (!File.Exists(BackupFilePath))
                {
                    await FileTransfer.DownloadFileAsync(BackupFilePath, ConsoleFilePath);
                }

                await FileTransfer.DownloadFileAsync(WorkingFilePath, ConsoleFilePath);

                _grid.Rows.Clear();

                foreach (string line in BootPluginsFile.ReadFile(WorkingFilePath))
                {
                    _grid.Rows.Add(line);
                }

                SetStatus("Loaded boot_plugins.txt.");
            }
            catch (Exception ex)
            {
                SetStatus("Unable to load boot_plugins.txt: " + ex.Message);
                ToolFormSupport.LogError(ex, "Unable to load the boot_plugins.txt file. Error: " + ex.Message);
                ToolFormSupport.ShowErrorAndClose(this, Text_("ERROR", "Error"), "Unable to load the boot_plugins.txt file. Edit the file path in Settings to your correct file location.\n\nError Message: " + ex.Message);
            }
        }

        private List<string> GridLines()
        {
            _grid.EndEdit();

            List<string> lines = new List<string>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                lines.Add(Convert.ToString(row.Cells[0].Value) ?? string.Empty);
            }

            return lines;
        }

        private async void ButtonRestoreDefault_Click(object sender, EventArgs e)
        {
            if (SmokeBlocksFileAccess())
            {
                return;
            }

            if (!ToolFormSupport.Confirm(this, Text_("CONFIRM", "Confirm"), Text_("RESTORE_DEFAULT_FILE", "Do you want to restore the default file? This will replace the file on your console with an empty one.")))
            {
                return;
            }

            try
            {
                BootPluginsFile.WriteFile(WorkingFilePath, new string[0]);
                await FileTransfer.UploadFileAsync(WorkingFilePath, ConsoleFilePath);
                await LoadBootPluginsAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to restore the default boot_plugins.txt file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to restore the default file. Error: " + ex.Message);
            }
        }

        private async void ButtonRestoreBackup_Click(object sender, EventArgs e)
        {
            if (SmokeBlocksFileAccess())
            {
                return;
            }

            if (!File.Exists(BackupFilePath))
            {
                MessageBox.Show(this, Text_("CREATE_BACKUP_FILE", "There is no backup file yet. Open the editor with a console connected to create one."), Text_("NO_BACKUP_FILE", "No Backup File"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (!ToolFormSupport.Confirm(this, Text_("CONFIRM", "Confirm"), Text_("RESTORE_BACKUP_FILE", "Do you want to restore the backup file? This will replace the file on your console.")))
            {
                return;
            }

            try
            {
                await FileTransfer.UploadFileAsync(BackupFilePath, ConsoleFilePath);
                await LoadBootPluginsAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to restore the backup boot_plugins.txt file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to restore the backup file. Error: " + ex.Message);
            }
        }

        private async void ButtonSaveUpdate_Click(object sender, EventArgs e)
        {
            if (SmokeBlocksFileAccess())
            {
                return;
            }

            try
            {
                BootPluginsFile.WriteFile(WorkingFilePath, GridLines());
                await FileTransfer.UploadFileAsync(WorkingFilePath, ConsoleFilePath);

                SetStatus("Saved boot_plugins.txt to the console.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("BOOT_PLUGINS_FILE_SAVED", "The boot_plugins.txt file was saved and updated on your console."));
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to save or upload the boot_plugins.txt file. Error: " + ex.Message);

                string format = Text_("FILE_SAVE_ERROR", "Unable to save the file. Error: {0}");
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

        /// <summary>
        /// Smoke runs set ToolFormSupport.NonBlockingErrors (Program.cs, --smoke only). The default folders are the real user
        /// folder, so a smoke run makes no console call and reads or writes no local file. Returns true when it stops the action.
        /// </summary>
        private bool SmokeBlocksFileAccess()
        {
            if (!ToolFormSupport.NonBlockingErrors)
            {
                return false;
            }

            SetStatus("Smoke run: no console or local file access.");
            return true;
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
