using ArisenStudio.Controls;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Picks, adds, edits or deletes console profiles.
    /// Stock WinForms port of the old DevExpress ConnectionsDialog form (built in code, no designer file).
    /// Dialog result: OK when a profile is chosen by double-click (read <see cref="ConsoleProfile" />).
    /// Add, Edit and Delete change <see cref="AppHost.Settings" /> directly; nothing is saved here, as before.
    /// </summary>
    public class ConnectionsDialog : Form
    {
        private const string ArtworkNamespace = "ArisenStudio.Icons.";

        private readonly GroupBox _groupConsoleProfiles;
        private readonly FlowLayoutPanel _panelConsoleProfiles;
        private readonly NoProfilesItem _noConsoleProfiles;
        private readonly Button _buttonAddNewProfile;
        private readonly Button _buttonEditProfile;
        private readonly Button _buttonDeleteProfile;

        // Console pictures are loaded once from the manifest and disposed with the dialog.
        private readonly Dictionary<string, Image> _artwork = new Dictionary<string, Image>();

        public ConnectionsDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            // Tiles scroll inside the panel. The old custom scroll bar (VScrollBar) is not ported; AutoScroll does the job on Mono.
            _panelConsoleProfiles = new FlowLayoutPanel
            {
                AutoScroll = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };

            // Shown instead of the panel when there are no profiles.
            _noConsoleProfiles = new NoProfilesItem
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            _groupConsoleProfiles = new GroupBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, pad)
            };
            _groupConsoleProfiles.Controls.Add(_noConsoleProfiles);
            _groupConsoleProfiles.Controls.Add(_panelConsoleProfiles);

            _buttonAddNewProfile = new Button
            {
                AutoSize = true,
                Margin = new Padding(0),
                TabIndex = 1
            };
            _buttonEditProfile = new Button
            {
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(pad, 0, 0, 0),
                TabIndex = 2
            };
            _buttonDeleteProfile = new Button
            {
                AutoSize = true,
                Enabled = false,
                Margin = new Padding(pad, 0, 0, 0),
                TabIndex = 3
            };

            _buttonAddNewProfile.Click += ButtonAddNewConsole_Click;
            _buttonEditProfile.Click += ButtonEditConsole_Click;
            _buttonDeleteProfile.Click += ButtonDeleteConsole_Click;

            // Right-to-left flow: Delete at the far right, Edit to its left.
            FlowLayoutPanel buttonsRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            buttonsRight.Controls.Add(_buttonDeleteProfile);
            buttonsRight.Controls.Add(_buttonEditProfile);

            TableLayoutPanel buttonBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, pad, 0, 0),
                Padding = new Padding(0)
            };
            buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttonBar.Controls.Add(_buttonAddNewProfile, 0, 0);
            buttonBar.Controls.Add(buttonsRight, 1, 0);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_groupConsoleProfiles, 0, 0);
            root.Controls.Add(buttonBar, 0, 1);
            Controls.Add(root);

            _panelConsoleProfiles.TabIndex = 0;

            // Fixed size from the font height, as in the other ported dialogs. Layout is unverified on Mono.
            ClientSize = new Size(h * 34, h * 28);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The settings the dialog lists and changes. Read live, so a settings reload is always seen.
        /// </summary>
        public static SettingsData Settings
        {
            get { return AppHost.Settings; }
        }

        /// <summary>
        /// True when the dialog edits all profiles. False when it picks one (optionally filtered by <see cref="Platform" />).
        /// </summary>
        public bool IsEditing { get; set; } = false;

        /// <summary>
        /// When set, only profiles for this platform are listed. Ignored while editing, as before.
        /// </summary>
        public Platform? Platform { get; set; } = null;

        /// <summary>
        /// The profile chosen with double-click, or the one last selected. Read it after the dialog returns OK.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; private set; }

        /// <summary>
        /// The selected tile.
        /// </summary>
        public ProfileItem SelectedConsole { get; private set; }

        /// <summary>
        /// Whether a console is connected. Replaces the old MainWindow.IsConsoleConnected. Defaults to not connected.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// The profile of the connected console, or null. Replaces the old MainWindow.ConsoleProfile. Defaults to none.
        /// </summary>
        public Func<ConsoleProfile> ConnectedConsoleProfile { get; set; } = () => null;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            ApplyLanguage();
            LoadConsoles();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                foreach (Image image in _artwork.Values)
                {
                    image?.Dispose();
                }

                _artwork.Clear();
            }
        }

        private void ApplyLanguage()
        {
            // The old file uses "CONNECTIONS" for the title; its English value is "Profile". Kept as-is.
            Text = Lookup("CONNECTIONS", "Profile");

            _groupConsoleProfiles.Text = Lookup("CONSOLE_PROFILES", "Console Profiles");

            _buttonAddNewProfile.Text = Lookup("PROFILE_ADD_NEW", "Add New Profile");
            _buttonEditProfile.Text = Lookup("PROFILE_EDIT", "Edit Profile");
            _buttonDeleteProfile.Text = Lookup("PROFILE_DELETE", "Delete Profile");

            _noConsoleProfiles.TitleText = Lookup("NO_CONSOLE_PROFILES", "NO CONSOLE PROFILES");
            _noConsoleProfiles.SubTitleText = Lookup("PROFILE_ADD_INSTRUCTION", "Click 'Add New Profile' to create one");
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private void LoadConsoles()
        {
            // Dispose the old tiles, so their message filters are removed.
            List<Control> oldTiles = new List<Control>();
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                oldTiles.Add(control);
            }

            _panelConsoleProfiles.Controls.Clear();

            foreach (Control control in oldTiles)
            {
                control.Dispose();
            }

            ConsoleProfile = null;
            SelectedConsole = null;

            foreach (ConsoleProfile consoleProfile in Settings.ConsoleProfiles)
            {
                if (!IsEditing && Platform.HasValue && consoleProfile.Platform != Platform.Value)
                {
                    continue;
                }

                ProfileItem consoleItem = new ProfileItem(consoleProfile.Name, GetConsoleImage(consoleProfile.PlatformType))
                {
                    ConsoleProfile = consoleProfile
                };
                consoleItem.OnClick += ConsoleItem_Click;
                consoleItem.OnDoubleClick += ConsoleItem_DoubleClick;
                _panelConsoleProfiles.Controls.Add(consoleItem);
            }

            bool hasProfiles = _panelConsoleProfiles.Controls.Count > 0;
            _noConsoleProfiles.Visible = !hasProfiles;
            _panelConsoleProfiles.Visible = hasProfiles;

            _buttonEditProfile.Enabled = ConsoleProfile != null;
            _buttonDeleteProfile.Enabled = ConsoleProfile != null;
        }

        /// <summary>
        /// Maps a platform type to its picture. Defaults to the PlayStation 3 Fat picture, as the old form did.
        /// </summary>
        private Image GetConsoleImage(PlatformType platformType)
        {
            switch (platformType)
            {
                case PlatformType.PlayStation3Slim:
                    return LoadArtwork("PlayStation3Slim.png");

                case PlatformType.PlayStation3SuperSlim:
                    return LoadArtwork("PlayStation3SuperSlim.png");

                case PlatformType.PlayStation4:
                    return LoadArtwork("playstation4.png");

                case PlatformType.Xbox360FatWhite:
                    return LoadArtwork("XboxFat.png");

                case PlatformType.Xbox360EliteFatBlack:
                    return LoadArtwork("XboxFatElite.png");

                case PlatformType.Xbox360Slim:
                    return LoadArtwork("XboxSlim.png");

                case PlatformType.Xbox360SlimE:
                    return LoadArtwork("XboxSlimE.png");

                default:
                    return LoadArtwork("PlayStation3Fat.png");
            }
        }

        /// <summary>
        /// Loads a picture from the embedded resources. Returns null when it is not embedded.
        /// </summary>
        private Image LoadArtwork(string fileName)
        {
            if (_artwork.TryGetValue(fileName, out Image cached))
            {
                return cached;
            }

            Image image = null;

            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ArtworkNamespace + fileName))
                {
                    if (stream != null)
                    {
                        using (Image source = Image.FromStream(stream))
                        {
                            image = new Bitmap(source);
                        }
                    }
                }
            }
            catch (Exception)
            {
                image = null;
            }

            _artwork[fileName] = image;
            return image;
        }

        private void ConsoleItem_Click(object sender, EventArgs e)
        {
            SelectTile(sender);

            ButtonEditAndDeleteState();
        }

        private void ConsoleItem_DoubleClick(object sender, EventArgs e)
        {
            SelectTile(sender);

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Marks the clicked tile as the only selected one and reads its profile.
        /// </summary>
        private void SelectTile(object sender)
        {
            ResetConsoleItems();

            ProfileItem item = sender as ProfileItem;
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            SelectedConsole = item;
            ConsoleProfile = item.ConsoleProfile;
        }

        private void ButtonEditAndDeleteState()
        {
            _buttonEditProfile.Enabled = ConsoleProfile != null;
            _buttonDeleteProfile.Enabled = ConsoleProfile != null;
        }

        private void ResetConsoleItems()
        {
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                ProfileItem item = control as ProfileItem;
                if (item != null)
                {
                    item.IsSelected = false;
                }
            }
        }

        private void SetTilesHover(bool shouldHover)
        {
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                ProfileItem item = control as ProfileItem;
                if (item != null)
                {
                    item.ShouldHover = shouldHover;
                }
            }
        }

        /// <summary>
        /// Shows the profile dialog. Returns the profile on OK. On a cancelled edit returns the same object;
        /// on a cancelled add returns null.
        /// </summary>
        private ConsoleProfile ShowNewConnectionWindow(ConsoleProfile consoleProfile, bool isEditing)
        {
            using (NewProfileDialog newProfileDialog = new NewProfileDialog
            {
                ConsoleProfile = consoleProfile,
                IsEditingProfile = isEditing,
                Language = Language
            })
            {
                if (newProfileDialog.ShowDialog(this) == DialogResult.OK)
                {
                    return newProfileDialog.ConsoleProfile;
                }
            }

            return isEditing ? consoleProfile : null;
        }

        private bool IsEditingConnectedProfile()
        {
            Func<bool> isConnected = IsConsoleConnected;
            Func<ConsoleProfile> connectedProfile = ConnectedConsoleProfile;

            return isConnected != null && isConnected()
                && connectedProfile != null && connectedProfile() == ConsoleProfile;
        }

        private void ButtonAddNewConsole_Click(object sender, EventArgs e)
        {
            SetTilesHover(false);

            ConsoleProfile consoleProfile = ShowNewConnectionWindow(new ConsoleProfile(), false);

            if (consoleProfile != null)
            {
                Settings.ConsoleProfiles.Add(consoleProfile);
                LoadConsoles();
            }
            else
            {
                SetTilesHover(true);
            }
        }

        private void ButtonEditConsole_Click(object sender, EventArgs e)
        {
            try
            {
                if (IsEditingConnectedProfile())
                {
                    _ = MessageBox.Show(
                        this,
                        Lookup("EDIT_PROFILE_ERROR", "You can't edit the details while you're connected to it."),
                        Lookup("LABEL_EXCLAMATION", "Exclamation"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                    return;
                }

                if (ConsoleProfile != null)
                {
                    SetTilesHover(false);

                    int selectedIndex = Settings.ConsoleProfiles.IndexOf(ConsoleProfile);
                    ConsoleProfile oldConsoleProfile = Settings.ConsoleProfiles[selectedIndex];

                    ConsoleProfile newConsoleProfile = ShowNewConnectionWindow(oldConsoleProfile, true);

                    Settings.ConsoleProfiles[selectedIndex] = newConsoleProfile;

                    SelectedConsole = null;
                    LoadConsoles();
                }
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Error editing console.");
                _ = MessageBox.Show(
                    this,
                    "Error editing console.",
                    Lookup("ERROR", "Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonDeleteConsole_Click(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                this,
                Lookup("CONFIRM_DELETE_ITEM", "Do you really want to delete the selected item?"),
                Lookup("CONFIRM_DELETE", "Confirm Delete"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            if (IsEditingConnectedProfile())
            {
                _ = MessageBox.Show(
                    this,
                    Lookup("EDIT_PROFILE_ERROR", "You can't edit the details while you're connected to it."),
                    Lookup("LABEL_EXCLAMATION", "Exclamation"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            _ = Settings.ConsoleProfiles.Remove(ConsoleProfile);
            SelectedConsole = null;
            LoadConsoles();
        }
    }
}
