using ArisenStudio.Controls;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// First-run setup wizard: welcome and change log, console profiles, and completion.
    /// Stock WinForms port of the old DevExpress SetupWizardDialog. The three pages are panels shown one at a time.
    /// Set Platform, Language, ConnectedConsoleProfile and ScanXboxConsoles before the dialog is shown when needed.
    /// </summary>
    internal class SetupWizardDialog : Form
    {
        private const string AboutText =
            "The elegant, powerful, and open-source mod manager.\r\n\r\n" +
            "Arisen Studio allows you to easily browse through a regularly updated database of mods, game saves, homebrew, resources, themes, and much more for the PlayStation 3 and Xbox 360. " +
            "All mods have been tested and verified in our library, which is populated by me, some friends, and awesome contributors. " +
            "Arisen Studio can also be used as an FTP client to automatically install and uninstall mods directly from your computer. " +
            "Without the need to search up old posts or use file managers - this will do everything for you.";

        private const string CompletionText =
            "Thank you for completing the setup. You're all set and ready to start using Arisen Studio!\r\n\r\n" +
            "Here are some things you need to know:\r\n\r\n" +
            "PlayStation 3\r\n\r\n" +
            "Requirements:\r\n" +
            "• webMAN, multiMAN or Rebug Toolbox\r\n\r\n" +
            "Usage:\r\n" +
            "• You must have webMAN installed so that all functions can work properly.\r\n" +
            "• You must have Rebug Toolbox or multiMAN open when installing or transferring files.\r\n\r\n" +
            "Xbox 360\r\n\r\n" +
            "Requirements:\r\n" +
            "• Neighborhood on your computer;\r\n" +
            "• DashLaunch & Xbdm.xex as Plugin #1\r\n\r\n" +
            "Usage:\r\n" +
            "• You must be at the Dashboard when installing files or using any built-in tools.";

        private const string NoScannerText =
            "Finding Xbox consoles needs the Xbox scanner, which is not available in this build.";

        private readonly Panel _host;
        private readonly Panel _pageWelcome;
        private readonly Panel _pageProfiles;
        private readonly Panel _pageComplete;
        private readonly Panel _profilesArea;
        private readonly FlowLayoutPanel _panelConsoleProfiles;
        private readonly NoProfilesItem _noConsoleProfiles;
        private readonly Label _labelAbout;
        private readonly Label _labelChangeLogVersion;
        private readonly Label _labelChangeLog;
        private readonly Label _labelCompletion;
        private readonly TabControl _tabControlChangeLog;
        private readonly TabPage _tabPageChangeLog;
        private readonly Button _buttonAddNewProfile;
        private readonly Button _buttonEditProfile;
        private readonly Button _buttonDeleteProfile;
        private readonly Button _buttonFindConsoles;
        private readonly Button _buttonBack;
        private readonly Button _buttonNext;
        private readonly Button _buttonCancel;
        private readonly Label _headingWelcome;
        private readonly Label _headingProfiles;
        private readonly Label _headingComplete;

        private readonly Panel[] _pages;
        private int _pageIndex;
        private ProfileItem _selectedItem;

        public SetupWizardDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Setup Wizard";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(764, 588);

            int pad = Font.Height / 2;
            Font headingFont = new Font(Font, FontStyle.Bold);

            // Welcome page: heading, about text, and the change log tab.
            _headingWelcome = new Label { AutoSize = true, UseMnemonic = false, Font = headingFont, Margin = new Padding(0, 0, 0, pad), Text = "Welcome to Arisen Studio" };
            _labelAbout = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, pad), Text = AboutText };

            _labelChangeLogVersion = new Label { AutoSize = true, UseMnemonic = false, Font = headingFont, Margin = new Padding(0, 0, 0, pad), Text = string.Empty, MinimumSize = new Size(1, 0) }; // Never 0 wide: LoadChangeLog leaves this label empty when there is no release data, and a zero-width control cannot be drawn on Mono.
            _labelChangeLog = new Label { AutoSize = true, UseMnemonic = false, Margin = Padding.Empty, Text = string.Empty, MinimumSize = new Size(1, 0) }; // Never 0 wide: LoadChangeLog leaves this label empty when there is no release data, and a zero-width control cannot be drawn on Mono.

            FlowLayoutPanel changeLogBody = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(pad)
            };
            changeLogBody.Controls.Add(_labelChangeLogVersion);
            changeLogBody.Controls.Add(_labelChangeLog);

            _tabPageChangeLog = new TabPage { Text = "Change Log" };
            _tabPageChangeLog.Controls.Add(changeLogBody);

            _tabControlChangeLog = new TabControl { Dock = DockStyle.Fill };
            _tabControlChangeLog.TabPages.Add(_tabPageChangeLog);

            TableLayoutPanel welcomeLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = Padding.Empty
            };
            _ = welcomeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = welcomeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = welcomeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = welcomeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            welcomeLayout.Controls.Add(_headingWelcome, 0, 0);
            welcomeLayout.Controls.Add(_labelAbout, 0, 1);
            welcomeLayout.Controls.Add(_tabControlChangeLog, 0, 2);

            _pageWelcome = new Panel { Dock = DockStyle.Fill };
            _pageWelcome.Controls.Add(welcomeLayout);
            _pageWelcome.Resize += (s, e) => WrapLabels(_pageWelcome, _labelAbout, _labelChangeLog, _labelChangeLogVersion);

            // Profiles page: heading, the tile area (or the empty message), and the action buttons.
            _headingProfiles = new Label { AutoSize = true, UseMnemonic = false, Font = headingFont, Margin = new Padding(0, 0, 0, pad), Text = "Console Profiles" };

            _panelConsoleProfiles = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty
            };

            _noConsoleProfiles = new NoProfilesItem { Dock = DockStyle.Fill, Visible = false };

            _profilesArea = new Panel { Dock = DockStyle.Fill };
            _profilesArea.Controls.Add(_panelConsoleProfiles);
            _profilesArea.Controls.Add(_noConsoleProfiles);

            _buttonAddNewProfile = new Button { AutoSize = true, Margin = new Padding(0, 0, pad, 0) };
            _buttonAddNewProfile.Click += ButtonAddNewProfile_Click;

            _buttonEditProfile = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0, 0, pad, 0) };
            _buttonEditProfile.Click += ButtonEditProfile_Click;

            _buttonDeleteProfile = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0, 0, pad, 0) };
            _buttonDeleteProfile.Click += ButtonDeleteProfile_Click;

            _buttonFindConsoles = new Button { AutoSize = true, Text = "Find Consoles", Margin = new Padding(0, 0, pad, 0) };
            _buttonFindConsoles.Click += ButtonFindConsoles_Click;

            FlowLayoutPanel profileButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(0, pad, 0, 0)
            };
            profileButtons.Controls.Add(_buttonAddNewProfile);
            profileButtons.Controls.Add(_buttonEditProfile);
            profileButtons.Controls.Add(_buttonDeleteProfile);
            profileButtons.Controls.Add(_buttonFindConsoles);

            TableLayoutPanel profilesLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = Padding.Empty
            };
            _ = profilesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = profilesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            profilesLayout.Controls.Add(_headingProfiles, 0, 0);
            profilesLayout.Controls.Add(_profilesArea, 0, 1);
            profilesLayout.Controls.Add(profileButtons, 0, 2);

            _pageProfiles = new Panel { Dock = DockStyle.Fill };
            _pageProfiles.Controls.Add(profilesLayout);

            // Complete page: heading and the closing text.
            _headingComplete = new Label { AutoSize = true, UseMnemonic = false, Font = headingFont, Margin = new Padding(0, 0, 0, pad), Text = "You've Finished Setup!" };
            _labelCompletion = new Label { AutoSize = true, UseMnemonic = false, Margin = Padding.Empty, Text = CompletionText };

            FlowLayoutPanel completeBody = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(pad)
            };
            completeBody.Controls.Add(_headingComplete);
            completeBody.Controls.Add(_labelCompletion);

            _pageComplete = new Panel { Dock = DockStyle.Fill };
            _pageComplete.Controls.Add(completeBody);
            _pageComplete.Resize += (s, e) => WrapLabels(_pageComplete, _labelCompletion);

            _pages = new[] { _pageWelcome, _pageProfiles, _pageComplete };

            // Navigation row. Right-to-left so the first control added sits at the right edge: Back, Next, Cancel.
            _buttonCancel = new Button { AutoSize = true, Margin = new Padding(pad, 0, 0, 0) };
            _buttonCancel.Click += (s, e) => Close();

            _buttonNext = new Button { AutoSize = true, Margin = new Padding(pad, 0, 0, 0) };
            _buttonNext.Click += ButtonNext_Click;

            _buttonBack = new Button { AutoSize = true, Margin = new Padding(pad, 0, 0, 0) };
            _buttonBack.Click += (s, e) => ShowPage(_pageIndex - 1);

            FlowLayoutPanel navigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(pad),
                Margin = Padding.Empty
            };
            navigation.Controls.Add(_buttonCancel);
            navigation.Controls.Add(_buttonNext);
            navigation.Controls.Add(_buttonBack);

            // Host holds the three pages; the navigation row sits below it.
            _host = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            _host.Controls.Add(_pageComplete);
            _host.Controls.Add(_pageProfiles);
            _host.Controls.Add(_pageWelcome);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_host, 0, 0);
            root.Controls.Add(navigation, 0, 1);

            Controls.Add(root);

            Load += (s, e) => OnDialogLoad();
        }

        /// <summary>
        /// The settings the wizard edits. Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings
        {
            get { return AppHost.Settings; }
        }

        /// <summary>
        /// Whether the user is editing console profiles.
        /// </summary>
        public bool IsEditing { get; set; } = false;

        /// <summary>
        /// The platform used for the default profile when the wizard closes with no profiles. Null asks the user once.
        /// </summary>
        public ArisenStudio.Platform? Platform { get; set; } = null;

        /// <summary>
        /// The profile that is selected in the list, or the last one chosen. Null when none is selected.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// The tile that is selected in the list. Null when none is selected.
        /// </summary>
        public ProfileItem SelectedConsole { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Returns the profile that is connected right now, or null when none is. Edit and Delete refuse that profile.
        /// </summary>
        public Func<ConsoleProfile> ConnectedConsoleProfile { get; set; } = () => null;

        /// <summary>
        /// Scans the network for Xbox 360 consoles and returns the profiles found. Null (the default) means scanning is not available.
        /// </summary>
        public Func<IEnumerable<ConsoleProfile>> ScanXboxConsoles { get; set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel)
            {
                return;
            }

            if (Settings.ConsoleProfiles.Count == 0)
            {
                ArisenStudio.Platform? platform = Platform ?? ShowPlatformPicker();
                Settings.ConsoleProfiles.Add(Settings.CreateDefaultProfile(platform));
            }
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

        private void OnDialogLoad()
        {
            ApplyLanguage();
            LoadChangeLog();
            LoadConsoles();
            ShowPage(0);
        }

        private void ApplyLanguage()
        {
            Text = Lookup("SETUP_WIZARD", "Setup Wizard");

            _headingWelcome.Text = Lookup("LABEL_WELCOME", "Welcome to Arisen Studio");
            _headingProfiles.Text = Lookup("CONSOLE_PROFILES", "Console Profiles");
            _headingComplete.Text = Lookup("SETUP_COMPLETE", "You've Finished Setup!");
            _tabPageChangeLog.Text = Lookup("CHANGE_LOG", "Change Log");

            _buttonAddNewProfile.Text = Lookup("PROFILE_ADD_NEW", "Add New Profile");
            _buttonEditProfile.Text = Lookup("PROFILE_EDIT", "Edit Profile");
            _buttonDeleteProfile.Text = Lookup("PROFILE_DELETE", "Delete Profile");

            _buttonBack.Text = "< &" + Lookup("LABEL_BACK", "Back");
            _buttonCancel.Text = Lookup("LABEL_CANCEL", "Cancel");
        }

        private void LoadChangeLog()
        {
            var releases = UpdateExtensions.AllReleases;
            var latest = releases != null && releases.Count > 0 ? releases[0] : null;

            _labelChangeLogVersion.Text = latest?.Name ?? string.Empty;
            _labelChangeLog.Text = (latest?.Body ?? string.Empty).Replace("- ", "• ");
        }

        private void LoadConsoles()
        {
            // Take the old tiles out and dispose them: each tile registers a message filter that only Dispose removes.
            List<Control> old = new List<Control>();
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                old.Add(control);
            }

            _panelConsoleProfiles.Controls.Clear();
            foreach (Control control in old)
            {
                control.Dispose();
            }

            ConsoleProfile = null;
            SelectedConsole = null;
            _selectedItem = null;

            foreach (ConsoleProfile consoleProfile in Settings.ConsoleProfiles)
            {
                // No console artwork exists in this build, so the tile image is empty.
                ProfileItem consoleItem = new ProfileItem(consoleProfile.Name, null) { ConsoleProfile = consoleProfile };
                consoleItem.OnClick += ConsoleItem_Click;
                consoleItem.OnDoubleClick += ConsoleItem_DoubleClick;
                _panelConsoleProfiles.Controls.Add(consoleItem);
            }

            bool hasProfiles = _panelConsoleProfiles.Controls.Count > 0;
            _noConsoleProfiles.Visible = !hasProfiles;
            _panelConsoleProfiles.Visible = hasProfiles;

            _buttonEditProfile.Enabled = false;
            _buttonDeleteProfile.Enabled = false;
        }

        private void ConsoleItem_Click(object sender, EventArgs e)
        {
            SelectTile(sender as ProfileItem);
        }

        private void ConsoleItem_DoubleClick(object sender, EventArgs e)
        {
            SelectTile(sender as ProfileItem);
        }

        private void SelectTile(ProfileItem item)
        {
            ResetConsoleItems();
            if (item == null)
            {
                return;
            }

            item.IsSelected = true;
            _selectedItem = item;
            SelectedConsole = item;
            ConsoleProfile = item.ConsoleProfile;

            _buttonEditProfile.Enabled = ConsoleProfile != null;
            _buttonDeleteProfile.Enabled = ConsoleProfile != null;
        }

        private void ResetConsoleItems()
        {
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                if (control is ProfileItem item)
                {
                    item.IsSelected = false;
                    item.Invalidate(true);
                }
            }
        }

        private void ButtonAddNewProfile_Click(object sender, EventArgs e)
        {
            SetTileHover(false);
            ConsoleProfile created = ShowProfileEditor(new ConsoleProfile(), false);
            SetTileHover(true);

            if (created != null)
            {
                Settings.ConsoleProfiles.Add(created);
                LoadConsoles();
            }
        }

        private void ButtonEditProfile_Click(object sender, EventArgs e)
        {
            try
            {
                if (IsConnectedToSelected())
                {
                    _ = MessageBox.Show(this, Lookup("EDIT_PROFILE_ERROR", "You can't edit the details while you're connected to it."), Lookup("LABEL_EXCLAMATION", "Exclamation"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                if (ConsoleProfile == null)
                {
                    return;
                }

                int selectedIndex = Settings.ConsoleProfiles.IndexOf(ConsoleProfile);
                if (selectedIndex < 0)
                {
                    return;
                }

                SetTileHover(false);
                ConsoleProfile updated = ShowProfileEditor(Settings.ConsoleProfiles[selectedIndex], true);
                SetTileHover(true);

                // Never store null: a cancelled edit keeps the old profile.
                if (updated != null)
                {
                    Settings.ConsoleProfiles[selectedIndex] = updated;
                }

                SelectedConsole = null;
                LoadConsoles();
            }
            catch (Exception ex)
            {
                AppHost.SetStatus("There was an error editing this profile.", ex);
                _ = MessageBox.Show(this, "There was an error editing the console. If you keep seeing this then please report this bug.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonDeleteProfile_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, Lookup("CONFIRM_DELETE_ITEM", "Do you really want to delete the selected item?"), Lookup("CONFIRM_DELETE", "Confirm Delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (IsConnectedToSelected())
            {
                _ = MessageBox.Show(this, Lookup("EDIT_PROFILE_ERROR", "You can't edit the details while you're connected to it."), Lookup("LABEL_EXCLAMATION", "Exclamation"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (ConsoleProfile != null)
            {
                _ = Settings.ConsoleProfiles.Remove(ConsoleProfile);
            }

            SelectedConsole = null;
            LoadConsoles();
        }

        private void ButtonFindConsoles_Click(object sender, EventArgs e)
        {
            UpdateStatus(Lookup("SCANNING_XBOX_CONSOLES", "Scanning for Xbox consoles..."));

            if (ScanXboxConsoles == null)
            {
                _ = MessageBox.Show(this, NoScannerText, Lookup("XBOX_CONSOLES", "Xbox Consoles"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateStatus(Lookup("SCANNING_XBOX_FINISHED", "Finished scanning for Xbox consoles."));
                return;
            }

            try
            {
                int count = 0;
                foreach (ConsoleProfile consoleProfile in ScanXboxConsoles())
                {
                    if (consoleProfile != null)
                    {
                        Settings.ConsoleProfiles.Add(consoleProfile);
                        count++;
                    }
                }

                if (count > 0)
                {
                    LoadConsoles();
                }

                _ = MessageBox.Show(this, string.Format(Lookup("TOTAL_ADDED_PROFILES", "A total of {0} consoles were added to your profiles."), count), Lookup("XBOX_CONSOLES", "Xbox Consoles"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus("Unable to scan for Xbox consoles.", ex);
                _ = MessageBox.Show(this, $"{Lookup("NEIGHBORHOOD_SCAN_FAILED", "Unable to find any consoles using Neighborhood.")}.\n\nError: {ex.Message}", Lookup("XBOX_CONSOLES", "Xbox Consoles"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateStatus(Lookup("SCANNING_XBOX_FINISHED", "Finished scanning for Xbox consoles."));
        }

        private void ButtonNext_Click(object sender, EventArgs e)
        {
            if (_pageIndex == 1)
            {
                LoadConsoles();
                ShowPage(2);
            }
            else if (_pageIndex == 2)
            {
                Close();
            }
            else
            {
                ShowPage(_pageIndex + 1);
            }
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Length)
            {
                return;
            }

            _pageIndex = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i].Visible = i == index;
            }

            _buttonBack.Enabled = index > 0;
            _buttonNext.Text = index == _pages.Length - 1 ? "&Finish" : "&" + Lookup("LABEL_NEXT", "Next") + " >";
        }

        /// <summary>
        /// Opens the profile editor for a new or existing profile. Returns the profile on OK, otherwise null for a new profile or the old profile for an edit.
        /// </summary>
        private ConsoleProfile ShowProfileEditor(ConsoleProfile profile, bool isEditing)
        {
            using (NewProfileDialog dialog = new NewProfileDialog
            {
                ConsoleProfile = profile,
                IsEditingProfile = isEditing,
                Language = Language
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return dialog.ConsoleProfile;
                }
            }

            return isEditing ? profile : null;
        }

        /// <summary>
        /// Asks for the platform for the default profile. Returns null when the picker is cancelled, so the default PS3 profile is used.
        /// </summary>
        private ArisenStudio.Platform? ShowPlatformPicker()
        {
            using (ListViewDialog dialog = new ListViewDialog { Language = Language })
            {
                dialog.Items = new List<ListViewDialogItem>
                {
                    new ListViewDialogItem { Name = "PlayStation 3", Value = "PS3" },
                    new ListViewDialogItem { Name = "PlayStation 4", Value = "PS4" },
                    new ListViewDialogItem { Name = "Xbox 360", Value = "XBOX360" }
                };

                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedItem != null
                    && Enum.TryParse(dialog.SelectedItem.Value, out ArisenStudio.Platform platform))
                {
                    return platform;
                }
            }

            return null;
        }

        private bool IsConnectedToSelected()
        {
            ConsoleProfile connected = ConnectedConsoleProfile?.Invoke();
            return ConsoleProfile != null && connected != null && ReferenceEquals(connected, ConsoleProfile);
        }

        private void SetTileHover(bool hover)
        {
            foreach (Control control in _panelConsoleProfiles.Controls)
            {
                if (control is ProfileItem item)
                {
                    item.ShouldHover = hover;
                }
            }
        }

        /// <summary>
        /// Keeps the wrapped labels as wide as their page, less the padding.
        /// </summary>
        private static void WrapLabels(Control page, params Label[] labels)
        {
            int width = Math.Max(1, page.ClientSize.Width - page.Font.Height);
            foreach (Label label in labels)
            {
                label.MaximumSize = new Size(width, 0);
            }
        }

        private void UpdateStatus(string status, Exception ex = null)
        {
            AppHost.SetStatus(status, ex);
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
