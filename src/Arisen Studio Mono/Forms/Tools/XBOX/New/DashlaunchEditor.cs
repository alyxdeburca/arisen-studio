using ArisenStudio.Io;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Services.Extra;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Edits the Dashlaunch launch.ini file on the Xbox 360: pick a section, change a key's value, toggle liveblock and
    /// livestrong, then save and send the file back. Can restore a bundled default or the copy taken the first time the editor ran.
    /// Stock WinForms port of the old DevExpress DashlaunchEditor form. The ini reading and writing is Services\Extra\LaunchIniFile
    /// (it replaces the IniParser package). The console calls are not ported; they sit behind IXboxFileTransfer.
    /// Host hooks, set before the form is shown:
    ///  - FileTransfer: the console boundary (default: a stub that fails with a message, which closes the form).
    ///  - Settings: Settings.LaunchIniFilePath is the console path of the file ("Hdd:\launch.ini" by default).
    ///  - DefaultLaunchIni: text of the default launch.ini for Restore Default. The old form took it from an embedded resource
    ///    (Properties.Resources.launch) that the Mono project does not have; null makes Restore Default say so.
    ///  - AppDataFolder / BackupFilesFolder: where the local working copy and backup copy go (default UserFolders).
    /// </summary>
    public class DashlaunchEditor : Form
    {
        private readonly Label _labelSection;
        private readonly ComboBox _comboSections;
        private readonly DataGridView _grid;
        private readonly Label _labelKey;
        private readonly Label _labelValue;
        private readonly TextBox _textBoxKey;
        private readonly TextBox _textBoxValue;
        private readonly Button _buttonSetValue;
        private readonly CheckBox _checkBoxLiveBlock;
        private readonly CheckBox _checkBoxLiveStrong;
        private readonly Button _buttonRestoreDefault;
        private readonly Button _buttonRestoreBackup;
        private readonly Button _buttonSaveUpdate;
        private readonly ToolStripStatusLabel _status;

        private LaunchIniFile _launchFile;
        private bool _loading;

        public DashlaunchEditor()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _labelSection = ToolFormSupport.NewLabel(string.Empty);
            _comboSections = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Margin = new Padding(0, 0, 0, pad)
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Key", HeaderText = "Key", FillWeight = 40F });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value", FillWeight = 60F });

            _labelKey = ToolFormSupport.NewLabel(string.Empty);
            _labelValue = ToolFormSupport.NewLabel(string.Empty);
            _textBoxKey = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };
            _textBoxValue = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };
            _buttonSetValue = ToolFormSupport.NewButton(this, 8);

            _checkBoxLiveBlock = new CheckBox { AutoSize = true, UseMnemonic = false, Text = "Enable LiveBlock", Margin = new Padding(0, 0, pad, 0) };
            _checkBoxLiveStrong = new CheckBox { AutoSize = true, UseMnemonic = false, Text = "Enable LiveStrong", Margin = new Padding(0, 0, pad, 0) };

            _buttonRestoreDefault = ToolFormSupport.NewButton(this, 9);
            _buttonRestoreBackup = ToolFormSupport.NewButton(this, 9);
            _buttonSaveUpdate = ToolFormSupport.NewButton(this, 9);
            _buttonSaveUpdate.Margin = Padding.Empty;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddRow(root, _labelSection, _comboSections);
            ToolFormSupport.AddFullRow(root, _grid, true);
            ToolFormSupport.AddRow(root, _labelKey, _textBoxKey);
            ToolFormSupport.AddRow(root, _labelValue, _textBoxValue);
            ToolFormSupport.AddFullRow(root, _buttonSetValue, false);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_checkBoxLiveBlock, _checkBoxLiveStrong), false);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonRestoreDefault, _buttonRestoreBackup, _buttonSaveUpdate), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _comboSections.SelectedIndexChanged += ComboSections_SelectedIndexChanged;
            _grid.SelectionChanged += Grid_SelectionChanged;
            _buttonSetValue.Click += ButtonSetValue_Click;
            _checkBoxLiveBlock.CheckedChanged += CheckBoxLiveBlock_CheckedChanged;
            _checkBoxLiveStrong.CheckedChanged += CheckBoxLiveStrong_CheckedChanged;
            _buttonRestoreDefault.Click += ButtonRestoreDefault_Click;
            _buttonRestoreBackup.Click += ButtonRestoreBackup_Click;
            _buttonSaveUpdate.Click += ButtonSaveUpdate_Click;

            ApplyLanguage();
            SetEditingEnabled(false);

            MinimumSize = new Size(h * 36, h * 32);
            ClientSize = new Size(h * 46, h * 40);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (LaunchIniFilePath). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// Console boundary used to receive and send the file.
        /// </summary>
        public IXboxFileTransfer FileTransfer { get; set; } = UnavailableXboxConsoleAccess.Instance;

        /// <summary>
        /// Text of the default launch.ini used by Restore Default. Null means no default is bundled.
        /// </summary>
        public string DefaultLaunchIni { get; set; }

        /// <summary>
        /// Folder that holds the local working copy ("Launch/launch.ini" inside it).
        /// </summary>
        public string AppDataFolder { get; set; } = UserFolders.AppData;

        /// <summary>
        /// Folder that holds the backup copy ("Launch/launch.ini" inside it).
        /// </summary>
        public string BackupFilesFolder { get; set; } = UserFolders.BackupFiles;

        private string ConsoleFilePath
        {
            get { return Settings.LaunchIniFilePath; }
        }

        private string LocalFilePath
        {
            get { return Path.Combine(AppDataFolder, "Launch", "launch.ini"); }
        }

        private string LocalBackupFilePath
        {
            get { return Path.Combine(BackupFilesFolder, "Launch", "launch.ini"); }
        }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("DASHLAUNCH_EDITOR", "Dashlaunch Editor");
            _labelSection.Text = "Section";
            _labelKey.Text = "Key";
            _labelValue.Text = "Value";
            _grid.Columns[0].HeaderText = "Key";
            _grid.Columns[1].HeaderText = "Value";
            _buttonSetValue.Text = Text_("LABEL_SET_VALUE", "Set Value");
            _buttonRestoreDefault.Text = Text_("LABEL_RESTORE_DEFAULT", "Restore Default");
            _buttonRestoreBackup.Text = Text_("LABEL_RESTORE_BACKUP", "Restore Backup");
            _buttonSaveUpdate.Text = Text_("LABEL_SAVE_AND_UPDATE", "Save and Update");
        }

        private void SetEditingEnabled(bool enabled)
        {
            _comboSections.Enabled = enabled;
            _buttonSetValue.Enabled = enabled;
            _checkBoxLiveBlock.Enabled = enabled;
            _checkBoxLiveStrong.Enabled = enabled;
            _buttonSaveUpdate.Enabled = enabled;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadLaunchFileAsync();
        }

        private async Task LoadLaunchFileAsync()
        {
            try
            {
                SetStatus("Loading launch.ini...");

                Directory.CreateDirectory(Path.GetDirectoryName(LocalFilePath));
                Directory.CreateDirectory(Path.GetDirectoryName(LocalBackupFilePath));

                string consolePath = ConsoleFilePath;
                string localPath = LocalFilePath;
                string backupPath = LocalBackupFilePath;

                // The backup is the copy from the first time the editor ran; it is not overwritten by later loads.
                if (!File.Exists(backupPath))
                {
                    await Task.Run(() => FileTransfer.ReceiveFile(backupPath, consolePath));
                }

                await Task.Run(() => FileTransfer.ReceiveFile(localPath, consolePath));

                _launchFile = LaunchIniFile.Load(localPath);
                ShowSections();
                SetEditingEnabled(true);

                SetStatus("Loaded launch.ini.");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to load the launch.ini file. Error: " + ex.Message);
                ToolFormSupport.ShowErrorAndClose(this, Text_("ERROR", "Error"), "Unable to load the launch.ini file. Edit the file path in Settings to your correct file location.\n\nError Message: " + ex.Message);
            }
        }

        private void ShowSections()
        {
            _loading = true;

            try
            {
                _comboSections.Items.Clear();

                foreach (string name in _launchFile.SectionNames)
                {
                    _comboSections.Items.Add(name);
                }

                int plugins = _comboSections.Items.IndexOf("Plugins");
                _comboSections.SelectedIndex = plugins >= 0 ? plugins : (_comboSections.Items.Count > 0 ? 0 : -1);

                _checkBoxLiveBlock.Checked = _launchFile.GetBool("Settings", "liveblock");
                _checkBoxLiveStrong.Checked = _launchFile.GetBool("Settings", "livestrong");
            }
            finally
            {
                _loading = false;
            }

            ShowSection(CurrentSection());
        }

        private string CurrentSection()
        {
            return _comboSections.SelectedItem as string;
        }

        private void ShowSection(string sectionName)
        {
            _grid.Rows.Clear();
            _textBoxKey.Text = string.Empty;
            _textBoxValue.Text = string.Empty;

            if (_launchFile == null || sectionName == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in _launchFile.GetKeys(sectionName))
            {
                _grid.Rows.Add(pair.Key, pair.Value);
            }

            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
            }
        }

        private void ComboSections_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_loading)
            {
                ShowSection(CurrentSection());
            }
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0)
            {
                return;
            }

            DataGridViewRow row = _grid.SelectedRows[0];
            _textBoxKey.Text = Convert.ToString(row.Cells[0].Value);
            _textBoxValue.Text = Convert.ToString(row.Cells[1].Value);
        }

        private void ButtonSetValue_Click(object sender, EventArgs e)
        {
            string section = CurrentSection();
            string key = _textBoxKey.Text.Trim();

            if (section == null || key.Length == 0)
            {
                return;
            }

            // The old form reloaded the file from the console here, which threw the edit away.
            // This keeps the edit in memory; Save and Update sends it.
            _launchFile.Set(section, key, _textBoxValue.Text);
            ShowSection(section);
            SetStatus("Set " + key + " in [" + section + "]. Use Save and Update to send it to the console.");
        }

        private void CheckBoxLiveBlock_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading && _launchFile != null)
            {
                _launchFile.SetBool("Settings", "liveblock", _checkBoxLiveBlock.Checked);
            }
        }

        private void CheckBoxLiveStrong_CheckedChanged(object sender, EventArgs e)
        {
            if (!_loading && _launchFile != null)
            {
                _launchFile.SetBool("Settings", "livestrong", _checkBoxLiveStrong.Checked);
            }
        }

        private async void ButtonRestoreDefault_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(DefaultLaunchIni))
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "No default launch.ini is bundled with this build.");
                return;
            }

            if (!ToolFormSupport.Confirm(this, Text_("CONFIRM", "Confirm"), Text_("RESTORE_DEFAULT_FILE", "Do you want to restore the default file? This will replace the file on your console.")))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LocalFilePath));
                File.WriteAllText(LocalFilePath, DefaultLaunchIni, new UTF8Encoding(false));

                string localPath = LocalFilePath;
                string consolePath = ConsoleFilePath;
                await Task.Run(() => FileTransfer.SendFile(localPath, consolePath));
                await LoadLaunchFileAsync();
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to restore the default launch.ini file.", ex);
            }
        }

        private async void ButtonRestoreBackup_Click(object sender, EventArgs e)
        {
            if (!File.Exists(LocalBackupFilePath))
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
                string backupPath = LocalBackupFilePath;
                string consolePath = ConsoleFilePath;
                await Task.Run(() => FileTransfer.SendFile(backupPath, consolePath));
                await LoadLaunchFileAsync();
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to restore the backup launch.ini file.", ex);
            }
        }

        private async void ButtonSaveUpdate_Click(object sender, EventArgs e)
        {
            if (_launchFile == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LocalFilePath));
                File.WriteAllText(LocalFilePath, _launchFile.ToText(), new UTF8Encoding(false));

                string localPath = LocalFilePath;
                string consolePath = ConsoleFilePath;
                await Task.Run(() => FileTransfer.SendFile(localPath, consolePath));

                SetStatus("Saved launch.ini to the console.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("LAUNCH_FILE_SAVED", "The launch.ini file was saved and updated on your console."));
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to save or upload launch.ini file. Error: " + ex.Message);

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

        private void ReportFailure(string message, Exception ex)
        {
            ToolFormSupport.LogError(ex, message);
            SetStatus(message);
            ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), message + "\n\nError Message: " + ex.Message);
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
