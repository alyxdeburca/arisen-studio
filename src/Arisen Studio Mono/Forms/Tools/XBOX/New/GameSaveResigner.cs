using ArisenStudio.Extensions;
using ArisenStudio.Io;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Opens an Xbox 360 game save (an STFS package), shows and edits its header (display name, title name, profile id,
    /// device id, console id, the two images) and saves it back re-signed. A profile can be loaded to copy its profile, device
    /// and console ids into the save. A finished save can be copied to a USB drive under "Game Saves/&lt;title id&gt;".
    /// Stock WinForms port of the old DevExpress GameSaveResigner form.
    /// The STFS library (Isolib.STFSPackage) is closed third-party code and is NOT ported; it sits behind IStfsPackageFactory.
    /// Host hooks, set before the form is shown:
    ///  - PackageFactory: opens STFS packages (default: a stub whose Open fails with a message).
    ///  - FileTransfer and ProfilePicker: the console boundary for "Add Profile From Console" (default: stubs that fail with a message).
    ///  - IsConsoleConnected: enables "Add Profile From Console".
    ///  - GetUsbDevices: the USB drives for "Save to Device" (default: removable drives that are ready).
    ///  - ProfilesFolder: where saved profiles are kept (default UserFolders.XboxProfiles).
    /// </summary>
    public class GameSaveResigner : Form
    {
        private const int MaximumReplacementImageBytes = 12000;

        private readonly MenuStrip _menu;
        private readonly ToolStripMenuItem _menuFile;
        private readonly ToolStripMenuItem _itemLoadFile;
        private readonly ToolStripMenuItem _itemSaveFile;
        private readonly ToolStripMenuItem _itemSaveToDevice;
        private readonly ToolStripMenuItem _menuProfile;
        private readonly ToolStripMenuItem _itemAddProfileDetails;
        private readonly ToolStripMenuItem _itemAddExistingProfile;
        private readonly ToolStripMenuItem _itemAddProfileFromConsole;

        private readonly Label _labelDisplayName;
        private readonly Label _labelTitleName;
        private readonly Label _labelTitleId;
        private readonly Label _labelProfileId;
        private readonly Label _labelDeviceId;
        private readonly Label _labelConsoleId;
        private readonly TextBox _textBoxDisplayName;
        private readonly TextBox _textBoxTitleName;
        private readonly TextBox _textBoxTitleId;
        private readonly TextBox _textBoxProfileId;
        private readonly TextBox _textBoxDeviceId;
        private readonly TextBox _textBoxConsoleId;

        private readonly Label _labelImagePackage;
        private readonly Label _labelImageContent;
        private readonly PictureBox _imagePackage;
        private readonly PictureBox _imageContent;
        private readonly ContextMenuStrip _imageMenu;
        private readonly ToolStripMenuItem _itemExtract;
        private readonly ToolStripMenuItem _itemReplace;
        private readonly ToolStripStatusLabel _status;

        private IStfsPackage _gameSave;
        private string _gameSavePath;
        private IStfsPackage _profile;
        private PictureBox _menuTarget;

        public GameSaveResigner()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            // Menus
            _itemLoadFile = new ToolStripMenuItem();
            _itemSaveFile = new ToolStripMenuItem();
            _itemSaveToDevice = new ToolStripMenuItem();
            _itemSaveToDevice.DropDownItems.Add(NewNoDeviceItem());
            _menuFile = new ToolStripMenuItem();
            _menuFile.DropDownItems.AddRange(new ToolStripItem[] { _itemLoadFile, _itemSaveFile, _itemSaveToDevice });

            _itemAddProfileDetails = new ToolStripMenuItem();
            _itemAddExistingProfile = new ToolStripMenuItem();
            _itemAddProfileFromConsole = new ToolStripMenuItem();
            _menuProfile = new ToolStripMenuItem();
            _menuProfile.DropDownItems.AddRange(new ToolStripItem[] { _itemAddProfileDetails, _itemAddExistingProfile, _itemAddProfileFromConsole });

            _menu = new MenuStrip();
            _menu.Items.AddRange(new ToolStripItem[] { _menuFile, _menuProfile });

            // Header fields
            _labelDisplayName = ToolFormSupport.NewLabel(string.Empty);
            _labelTitleName = ToolFormSupport.NewLabel(string.Empty);
            _labelTitleId = ToolFormSupport.NewLabel(string.Empty);
            _labelProfileId = ToolFormSupport.NewLabel(string.Empty);
            _labelDeviceId = ToolFormSupport.NewLabel(string.Empty);
            _labelConsoleId = ToolFormSupport.NewLabel(string.Empty);
            _textBoxDisplayName = NewTextBox(pad, 0);
            _textBoxTitleName = NewTextBox(pad, 0);
            _textBoxTitleId = NewTextBox(pad, 0);
            _textBoxTitleId.ReadOnly = true;
            _textBoxProfileId = NewTextBox(pad, 16);
            _textBoxDeviceId = NewTextBox(pad, 40);
            _textBoxConsoleId = NewTextBox(pad, 10);

            TableLayoutPanel fields = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddRow(fields, _labelDisplayName, _textBoxDisplayName);
            ToolFormSupport.AddRow(fields, _labelTitleName, _textBoxTitleName);
            ToolFormSupport.AddRow(fields, _labelTitleId, _textBoxTitleId);
            ToolFormSupport.AddRow(fields, _labelProfileId, _textBoxProfileId);
            ToolFormSupport.AddRow(fields, _labelDeviceId, _textBoxDeviceId);
            ToolFormSupport.AddRow(fields, _labelConsoleId, _textBoxConsoleId);

            // Images
            _imageMenu = new ContextMenuStrip();
            _itemExtract = new ToolStripMenuItem();
            _itemReplace = new ToolStripMenuItem();
            _imageMenu.Items.AddRange(new ToolStripItem[] { _itemExtract, _itemReplace });

            _labelImagePackage = ToolFormSupport.NewLabel(string.Empty);
            _labelImageContent = ToolFormSupport.NewLabel(string.Empty);
            _imagePackage = NewPicture(h, pad, _imageMenu);
            _imageContent = NewPicture(h, pad, _imageMenu);

            TableLayoutPanel images = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize));
            images.Dock = DockStyle.Top;
            images.Margin = new Padding(pad, 0, 0, 0);
            ToolFormSupport.AddRow(images, _labelImagePackage, null);
            ToolFormSupport.AddRow(images, _imagePackage, null);
            ToolFormSupport.AddRow(images, _labelImageContent, null);
            ToolFormSupport.AddRow(images, _imageContent, null);

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowCount = 1;
            fields.Dock = DockStyle.Top;
            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(images, 1, 0);

            // The main control goes in first, then the menu and the status bar, so they take the top and bottom edges.
            Controls.Add(root);
            Controls.Add(_menu);
            MainMenuStrip = _menu;
            _status = ToolFormSupport.AddStatusBar(this);

            _menuFile.DropDownOpening += MenuFile_DropDownOpening;
            _menuProfile.DropDownOpening += MenuProfile_DropDownOpening;
            _itemLoadFile.Click += ItemLoadFile_Click;
            _itemSaveFile.Click += ItemSaveFile_Click;
            _itemAddProfileDetails.Click += ItemAddProfileDetails_Click;
            _itemAddExistingProfile.Click += ItemAddExistingProfile_Click;
            _itemAddProfileFromConsole.Click += ItemAddProfileFromConsole_Click;
            _imageMenu.Opening += ImageMenu_Opening;
            _itemExtract.Click += ItemExtract_Click;
            _itemReplace.Click += ItemReplace_Click;

            ApplyLanguage();
            UpdateMenuState();

            MinimumSize = new Size(h * 40, h * 22);
            ClientSize = new Size(h * 52, h * 26);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Opens STFS packages. The default is a stub that fails with a message; see IStfsPackageFactory.
        /// </summary>
        public IStfsPackageFactory PackageFactory { get; set; } = new UnavailableStfsPackageFactory();

        /// <summary>
        /// Console boundary used to download a profile file for "Add Profile From Console".
        /// </summary>
        public IXboxFileTransfer FileTransfer { get; set; } = UnavailableXboxConsoleAccess.Instance;

        /// <summary>
        /// Console boundary used to choose a profile file on the console for "Add Profile From Console".
        /// </summary>
        public IXboxProfilePicker ProfilePicker { get; set; } = UnavailableXboxConsoleAccess.Instance;

        /// <summary>
        /// Whether a console is connected now. "Add Profile From Console" is only enabled when this returns true.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// The USB drives to offer in "Save to Device": Name is shown, Value is the drive root. The default lists removable drives that are ready.
        /// </summary>
        public Func<IList<ListItem>> GetUsbDevices { get; set; } = DefaultUsbDevices;

        /// <summary>
        /// Folder where profiles are saved and listed.
        /// </summary>
        public string ProfilesFolder { get; set; } = UserFolders.XboxProfiles;

        private static ToolStripMenuItem NewNoDeviceItem()
        {
            return new ToolStripMenuItem("No Devices Found") { Enabled = false };
        }

        private static IList<ListItem> DefaultUsbDevices()
        {
            List<ListItem> devices = new List<ListItem>();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        string label = drive.VolumeLabel;
                        devices.Add(new ListItem
                        {
                            Name = string.IsNullOrEmpty(label) ? drive.Name : label + " (" + drive.Name + ")",
                            Value = drive.RootDirectory.FullName
                        });
                    }
                }
                catch (IOException)
                {
                    // A drive that went away while it was being read is skipped.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return devices;
        }

        private static TextBox NewTextBox(int pad, int maxLength)
        {
            TextBox box = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };

            if (maxLength > 0)
            {
                box.MaxLength = maxLength;
            }

            return box;
        }

        private static PictureBox NewPicture(int h, int pad, ContextMenuStrip menu)
        {
            return new PictureBox
            {
                Size = new Size(h * 7, h * 7),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, pad),
                ContextMenuStrip = menu
            };
        }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = "Game Save Resigner";

            _menuFile.Text = Text_("FILE", "File");
            _itemLoadFile.Text = "Load File...";
            _itemSaveFile.Text = "Save File...";
            _itemSaveToDevice.Text = "Save to Device...";
            _menuProfile.Text = Text_("PROFILE", "Profile");
            _itemAddProfileDetails.Text = "Add Profile Details...";
            _itemAddExistingProfile.Text = "Add Existing Profile...";
            _itemAddProfileFromConsole.Text = "Add Profile From Console...";

            _labelDisplayName.Text = "Display Name";
            _labelTitleName.Text = "Title Name";
            _labelTitleId.Text = "Title ID";
            _labelProfileId.Text = "Profile ID";
            _labelDeviceId.Text = "Device ID";
            _labelConsoleId.Text = "Console ID";
            _labelImagePackage.Text = "Package Image";
            _labelImageContent.Text = "Title Image";

            _itemExtract.Text = "Extract...";
            _itemReplace.Text = "Replace...";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CloseQuietly(ref _gameSave);
            CloseQuietly(ref _profile);
            base.OnFormClosing(e);
        }

        private static void CloseQuietly(ref IStfsPackage package)
        {
            if (package == null)
            {
                return;
            }

            try
            {
                package.Close();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to close an STFS package.");
            }

            package = null;
        }

        private void UpdateMenuState()
        {
            bool hasSave = _gameSave != null;
            bool connected = IsConsoleConnected != null && IsConsoleConnected();

            _itemSaveFile.Enabled = hasSave;
            _itemSaveToDevice.Enabled = hasSave;
            _itemAddProfileDetails.Enabled = hasSave;
            _itemAddExistingProfile.Enabled = hasSave && Directory.Exists(ProfilesFolder);
            _itemAddProfileFromConsole.Enabled = hasSave && connected;
        }

        private void MenuProfile_DropDownOpening(object sender, EventArgs e)
        {
            UpdateMenuState();
        }

        private void MenuFile_DropDownOpening(object sender, EventArgs e)
        {
            UpdateMenuState();

            _itemSaveToDevice.DropDownItems.Clear();

            if (_gameSave == null)
            {
                _itemSaveToDevice.DropDownItems.Add(NewNoDeviceItem());
                return;
            }

            IList<ListItem> devices = null;

            try
            {
                devices = GetUsbDevices != null ? GetUsbDevices() : null;
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to list the USB devices.");
            }

            if (devices == null || devices.Count == 0)
            {
                _itemSaveToDevice.DropDownItems.Add(NewNoDeviceItem());
                return;
            }

            foreach (ListItem device in devices)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(device.Name) { Tag = device };
                item.Click += ItemDevice_Click;
                _itemSaveToDevice.DropDownItems.Add(item);
            }
        }

        private void ItemDevice_Click(object sender, EventArgs e)
        {
            ListItem device = ((ToolStripItem)sender).Tag as ListItem;

            if (device == null || _gameSave == null)
            {
                return;
            }

            string titleId = _textBoxTitleId.Text.Trim();

            if (titleId.Length == 0 || titleId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "This save has no valid title ID, so it can't be copied to a device.");
                return;
            }

            try
            {
                string path = _gameSavePath;

                _gameSave.Finish();
                _gameSave.Close();

                string folder = Path.Combine(device.Value, "Game Saves", titleId);
                Directory.CreateDirectory(folder);
                File.Copy(path, Path.Combine(folder, Path.GetFileName(path)), true);

                _gameSave = PackageFactory.Open(path);
                LoadFile();

                SetStatus("Saved the game save to " + folder);
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Saved the game save to:\n" + folder);
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to save the game save to the device.", ex);
            }
        }

        private void ItemLoadFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    IStfsPackage package = PackageFactory.Open(dialog.FileName);

                    CloseQuietly(ref _gameSave);
                    _gameSave = package;
                    _gameSavePath = dialog.FileName;

                    LoadFile();
                    UpdateMenuState();

                    SetStatus("Successfully loaded game save.");
                    ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Successfully loaded game save!");
                }
                catch (Exception ex)
                {
                    ReportFailure("Unable to parse game save file.", ex);
                }
            }
        }

        private void ItemSaveFile_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void LoadFile()
        {
            IStfsHeader header = _gameSave.Header;

            _textBoxDisplayName.Text = header.DisplayName;
            _textBoxTitleName.Text = header.TitleName;
            _textBoxProfileId.Text = header.ProfileId;
            _textBoxDeviceId.Text = header.DeviceId;
            _textBoxConsoleId.Text = header.ConsoleId;
            _imagePackage.Image = header.PackageImage;
            _imageContent.Image = header.ContentImage;
            _textBoxTitleId.Text = (header.TitleId ?? string.Empty).TrimStart('0');

            if (string.IsNullOrEmpty(_textBoxProfileId.Text) || _textBoxProfileId.Text == "0")
            {
                _textBoxProfileId.Text = "0000000000000000";
            }

            if (string.IsNullOrEmpty(_textBoxDeviceId.Text) || _textBoxDeviceId.Text == "0")
            {
                _textBoxDeviceId.Text = "0000000000000000000000000000000000000000";
            }

            if (string.IsNullOrEmpty(_textBoxConsoleId.Text) || _textBoxConsoleId.Text == "0")
            {
                _textBoxConsoleId.Text = "0000000000";
            }
        }

        private static bool IsHexText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (char c in text)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private void SaveFile()
        {
            if (_gameSave == null)
            {
                return;
            }

            if (!IsHexText(_textBoxProfileId.Text) || !IsHexText(_textBoxDeviceId.Text) || !IsHexText(_textBoxConsoleId.Text))
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "The Profile ID, Device ID and Console ID must be hexadecimal values.");
                return;
            }

            try
            {
                bool backup = MessageBox.Show(this, "Would you like to create a backup of this file before saving?", "Backup File", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

                CloseQuietly(ref _gameSave);

                if (backup)
                {
                    SetStatus("Creating backup of file: " + Path.GetFileName(_gameSavePath));
                    File.Copy(_gameSavePath, _gameSavePath + ".bak", true);
                    SetStatus("Successfully created backup file.");
                }

                _gameSave = PackageFactory.Open(_gameSavePath);

                IStfsHeader header = _gameSave.Header;
                header.DisplayName = _textBoxDisplayName.Text;
                header.TitleName = _textBoxTitleName.Text;
                header.ProfileId = _textBoxProfileId.Text;
                header.DeviceId = _textBoxDeviceId.Text;
                header.ConsoleId = _textBoxConsoleId.Text;
                header.PackageImage = _imagePackage.Image;
                header.ContentImage = _imageContent.Image;

                _gameSave.Finish();

                SetStatus("Successfully saved and resigned game save.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Successfully saved and resigned game save!");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to save or resign game save.", ex);
                UpdateMenuState();
            }
        }

        private void ImageMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _menuTarget = _imageMenu.SourceControl as PictureBox;

            if (_gameSave == null || _menuTarget == null)
            {
                e.Cancel = true;
                return;
            }

            _itemExtract.Enabled = _menuTarget.Image != null;
        }

        private void ItemExtract_Click(object sender, EventArgs e)
        {
            PictureBox target = _menuTarget;

            if (target == null || target.Image == null)
            {
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Extract Image";
                dialog.Filter = "PNG Image (*.png)|*.png";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    target.Image.Save(dialog.FileName, ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    ReportFailure("Unable to extract the image.", ex);
                }
            }
        }

        private void ItemReplace_Click(object sender, EventArgs e)
        {
            PictureBox target = _menuTarget;

            if (target == null)
            {
                return;
            }

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Replace Image";
                dialog.Filter = "PNG Image (*.png)|*.png";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    if (new FileInfo(dialog.FileName).Length > MaximumReplacementImageBytes)
                    {
                        ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("LABEL_IMAGE_SIZE_TOO_BIG", "The image is too big. It must be 12000 bytes or smaller."));
                        return;
                    }

                    // The image is copied into memory so the file is not left open.
                    using (FileStream stream = File.OpenRead(dialog.FileName))
                    using (Image fromStream = Image.FromStream(stream))
                    {
                        target.Image = new Bitmap(fromStream);
                    }
                }
                catch (Exception ex)
                {
                    ReportFailure("Unable to load the image.", ex);
                }
            }
        }

        private void ItemAddProfileDetails_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = Text_("LABEL_OPEN_FILE", "Open File");

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    string fileName = dialog.FileName;

                    if (MessageBox.Show(this, "Do you want to save this profile data?", Text_("LABEL_SAVE_PROFILE", "Save Profile"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        string name = Path.GetFileName(fileName);
                        string folder = Path.Combine(ProfilesFolder, name);

                        Directory.CreateDirectory(folder);
                        File.Copy(fileName, Path.Combine(folder, name + "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".bak"));
                    }

                    LoadProfile(fileName);
                }
                catch (Exception ex)
                {
                    ReportFailure("Unable to load profile details.", ex);
                }
            }
        }

        private void ItemAddExistingProfile_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(ProfilesFolder))
            {
                return;
            }

            try
            {
                List<ListItem> profiles = new List<ListItem>();

                foreach (string profile in Directory.GetFiles(ProfilesFolder, "*.*", SearchOption.AllDirectories))
                {
                    profiles.Add(new ListItem { Name = Path.GetFileName(profile), Value = profile });
                }

                if (profiles.Count == 0)
                {
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "You haven't saved any profiles.");
                    return;
                }

                ListItem chosen = ToolFormSupport.PickFromList(this, Text_("LABEL_OPEN_FILE", "Open File"), profiles);

                if (chosen == null)
                {
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "You don't have any profiles saved or none was selected.");
                    return;
                }

                LoadProfile(chosen.Value);
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to list the saved profiles.", ex);
            }
        }

        private async void ItemAddProfileFromConsole_Click(object sender, EventArgs e)
        {
            try
            {
                ListItem selected = ProfilePicker.PickProfileFile(this);

                if (selected == null)
                {
                    SetStatus("No profiles were found or you didn't select one.");
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "No profiles were found or you didn't select one.");
                    return;
                }

                string folder = Path.Combine(ProfilesFolder, selected.Name);
                string localFile = Path.Combine(folder, selected.Name);
                string consoleFile = selected.Value;
                IXboxFileTransfer transfer = FileTransfer;

                Directory.CreateDirectory(folder);
                await Task.Run(() => transfer.ReceiveFile(localFile, consoleFile));

                LoadProfile(localFile);
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to find profile files on console.", ex);
            }
        }

        private void LoadProfile(string fileName)
        {
            try
            {
                IStfsPackage package = PackageFactory.Open(fileName);

                CloseQuietly(ref _profile);
                _profile = package;

                _textBoxProfileId.Text = package.Header.ProfileId;
                _textBoxDeviceId.Text = package.Header.DeviceId;
                _textBoxConsoleId.Text = package.Header.ConsoleId;

                SetStatus("Successfully loaded profile file.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Successfully loaded profile!");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to load profile.", ex);
            }
        }

        private void ReportFailure(string message, Exception ex)
        {
            ToolFormSupport.LogError(ex, message);
            SetStatus(message);
            ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), message + " Error Message: " + ex.Message);
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
