using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
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
    /// Lists the .pkg files in the package folder on the PS3, and installs (uploads), downloads or deletes them.
    /// Stock WinForms port of the old DevExpress PackageFilesManager form (the old grid is a ListView here).
    /// Host hooks, set before the form is shown:
    ///  - FileTransfer: the FTP boundary (default: a stub that fails with a message, which closes the form).
    ///  - Settings: PackageInstallPathPS3 is the console folder, UseRelativeTimes and UseFormattedFileSizes set the
    ///    column text, PathDownloads is the local folder for Download File.
    /// </summary>
    public class PackageFilesManager : Form
    {
        private readonly ListView _listPackages;
        private readonly Button _buttonInstall;
        private readonly Button _buttonDownload;
        private readonly Button _buttonDelete;
        private readonly Button _buttonDeleteAll;
        private readonly ToolStripStatusLabel _status;

        private readonly List<Ps3FileEntry> _packageFiles = new List<Ps3FileEntry>();

        public PackageFilesManager()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _listPackages = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            _listPackages.Columns.Add("File Name", h * 22);
            _listPackages.Columns.Add("Modified Date", h * 9);
            _listPackages.Columns.Add("File Size", h * 9);

            _buttonInstall = ToolFormSupport.NewButton(this, 8);
            _buttonDownload = ToolFormSupport.NewButton(this, 8);
            _buttonDelete = ToolFormSupport.NewButton(this, 8);
            _buttonDeleteAll = ToolFormSupport.NewButton(this, 8);
            _buttonDeleteAll.Margin = Padding.Empty;
            _buttonDownload.Enabled = false;
            _buttonDelete.Enabled = false;
            _buttonDeleteAll.Enabled = false;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddFullRow(root, _listPackages, true);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonInstall, _buttonDownload, _buttonDelete, _buttonDeleteAll), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _listPackages.SelectedIndexChanged += ListPackages_SelectedIndexChanged;
            _buttonInstall.Click += ButtonInstall_Click;
            _buttonDownload.Click += ButtonDownload_Click;
            _buttonDelete.Click += ButtonDelete_Click;
            _buttonDeleteAll.Click += ButtonDeleteAll_Click;

            ApplyLanguage();

            MinimumSize = new Size(h * 36, h * 22);
            ClientSize = new Size(h * 46, h * 30);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source. Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// FTP boundary used to list, upload, download and delete package files.
        /// </summary>
        public IPs3FileTransfer FileTransfer { get; set; } = new UnavailablePs3FileTransfer();

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        /// <summary>
        /// Console folder with a single trailing slash (the setting is "/dev_hdd0/packages/" by default).
        /// </summary>
        private string PackageFolder
        {
            get { return Settings.PackageInstallPathPS3.TrimEnd('/') + "/"; }
        }

        private void ApplyLanguage()
        {
            Text = Text_("PACKAGE_FILES_MANAGER", "Package Files Manager");

            _listPackages.Columns[0].Text = Text_("LABEL_FILE_NAME", "File Name");
            _listPackages.Columns[1].Text = Text_("LABEL_MODIFIED_DATE", "Modified Date");
            _listPackages.Columns[2].Text = Text_("LABEL_FILE_SIZE", "File Size");

            _buttonInstall.Text = Text_("LABEL_INSTALL_FILE", "Install File");
            _buttonDownload.Text = Text_("LABEL_DOWNLOAD_FILE", "Download File");
            _buttonDelete.Text = Text_("LABEL_DELETE", "Delete");
            _buttonDeleteAll.Text = Text_("LABEL_DELETE_ALL", "Delete All");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadPackagesAsync();
        }

        private async Task LoadPackagesAsync()
        {
            try
            {
                SetStatus("Fetching listing...");
                UseWaitCursor = true;

                IList<Ps3FileEntry> entries = await FileTransfer.ListFilesAsync(PackageFolder);

                _packageFiles.Clear();

                foreach (Ps3FileEntry entry in entries)
                {
                    if (!entry.IsDirectory && entry.Name != null && entry.Name.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
                    {
                        _packageFiles.Add(entry);
                    }
                }

                _listPackages.BeginUpdate();
                _listPackages.Items.Clear();

                foreach (Ps3FileEntry package in _packageFiles)
                {
                    ListViewItem item = new ListViewItem(package.Name);
                    item.SubItems.Add(ToolFormSupport.FormatDate(package.Modified, Settings.UseRelativeTimes));
                    item.SubItems.Add(ToolFormSupport.FormatBytes(package.Size, Settings.UseFormattedFileSizes, Text_("LABEL_BYTES", "bytes")));
                    item.Tag = package;
                    _listPackages.Items.Add(item);
                }

                _listPackages.EndUpdate();

                UpdateButtons();
                SetStatus(Text_("FETCHED_LISTING", "Fetched listing."));
            }
            catch (Exception ex)
            {
                _buttonInstall.Enabled = false;
                _buttonDownload.Enabled = false;
                _buttonDelete.Enabled = false;
                _buttonDeleteAll.Enabled = false;

                ToolFormSupport.LogError(ex, "Unable to fetch the package listing of " + PackageFolder);
                SetStatus("Unable to fetch the listing of " + PackageFolder);
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to fetch the listing of " + PackageFolder + ".\n\n" + ex.Message);
                Close();
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private Ps3FileEntry Selected()
        {
            if (_listPackages.SelectedItems.Count == 0)
            {
                return null;
            }

            return _listPackages.SelectedItems[0].Tag as Ps3FileEntry;
        }

        private void UpdateButtons()
        {
            bool hasSelection = _listPackages.SelectedItems.Count > 0;
            _buttonDownload.Enabled = hasSelection;
            _buttonDelete.Enabled = hasSelection;
            _buttonDeleteAll.Enabled = _listPackages.Items.Count > 0;
        }

        private void ListPackages_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private async void ButtonInstall_Click(object sender, EventArgs e)
        {
            string localFilePath;

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Package File";
                dialog.Filter = "PKG Files (*.pkg)|*.pkg|All Files (*.*)|*.*";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                localFilePath = dialog.FileName;
            }

            try
            {
                string fileName = Path.GetFileName(localFilePath);
                string installFilePath = PackageFolder + fileName;

                if (await FileTransfer.FileExistsAsync(installFilePath))
                {
                    MessageBox.Show(this, "Package file with this name already exists on your console.", "Package File Exists", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                SetStatus("Installing file: " + fileName);
                await FileTransfer.UploadFileAsync(localFilePath, installFilePath);
                SetStatus("Successfully installed file.");
                await LoadPackagesAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to install the package file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to install the package file. Error: " + ex.Message);
            }
        }

        private async void ButtonDelete_Click(object sender, EventArgs e)
        {
            Ps3FileEntry selected = Selected();

            if (selected == null)
            {
                return;
            }

            if (!ToolFormSupport.Confirm(this, Text_("CONFIRM_DELETE", "Confirm Delete"), "Do you really want to delete the selected package file from your console?"))
            {
                return;
            }

            try
            {
                SetStatus("Deleting file: " + selected.Name);
                await FileTransfer.DeleteFileAsync(PackageFolder + selected.Name);
                SetStatus("Successfully deleted file.");
                await LoadPackagesAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to delete the package file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to delete the package file. Error: " + ex.Message);
            }
        }

        private async void ButtonDeleteAll_Click(object sender, EventArgs e)
        {
            if (!ToolFormSupport.Confirm(this, Text_("CONFIRM_DELETE_ALL", "Confirm Delete All"), "Do you really want to delete all package files from your console?"))
            {
                return;
            }

            try
            {
                foreach (Ps3FileEntry package in new List<Ps3FileEntry>(_packageFiles))
                {
                    SetStatus("Deleting file: " + package.Name);
                    await FileTransfer.DeleteFileAsync(PackageFolder + package.Name);
                }

                SetStatus("Successfully deleted package files.");
                await LoadPackagesAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to delete the package files.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to delete the package files. Error: " + ex.Message);
                await LoadPackagesAsync();
            }
        }

        private async void ButtonDownload_Click(object sender, EventArgs e)
        {
            Ps3FileEntry selected = Selected();

            if (selected == null)
            {
                return;
            }

            try
            {
                string folder = Settings.PathDownloads.GetFullPath();

                if (string.IsNullOrWhiteSpace(folder))
                {
                    return;
                }

                Directory.CreateDirectory(folder);

                string localFile = Path.Combine(folder, selected.Name);

                SetStatus("Downloading file: " + selected.Name);
                await FileTransfer.DownloadFileAsync(localFile, PackageFolder + selected.Name);
                SetStatus("Successfully downloaded file.");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to download the package file.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to download the package file. Error: " + ex.Message);
            }
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
