using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Controls;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Shell;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// The Settings page (slice S4, old PageSettings, ledger MW2-027 to MW2-065): six tabs (Interface, Mods, Console,
    /// Downloads, Cache, Discord) over a SettingsData object. Every toggle writes its property the moment it changes,
    /// as the old toggles did, then raises <see cref="SettingChanged"/> so the shell can save the settings and react
    /// (status bar, ribbon sync, Discord, language).
    ///
    /// Use: set <see cref="Settings"/> (it defaults to AppHost.Settings), subscribe to the events, register under
    /// ShellPageKeys.Settings. The page fills its controls from the settings every time it becomes visible
    /// (ledger MW2-032, the old NavigationItemSettings_Click calling LoadSettings).
    /// The page never starts Discord, switches the language, deletes the cache or opens a link itself.
    /// </summary>
    internal sealed class SettingsPage : UserControl, IShellPage
    {
        private const string CrowdinUrl = "https://crowdin.com/project/arisenstudio";

        // The six supported languages, by the names the old combo held (ledger MW2-027). Non-ASCII letters are escapes.
        private static readonly string[] Languages =
        {
            "English",
            "Espa\u00f1ol",
            "Portugu\u00eas (Brasil)",
            "Rom\u00e2n\u0103",
            "Svenska",
            "T\u00fcrk\u00e7e"
        };

        // One check box bound to a bool property of SettingsData.
        private sealed class ToggleBinding
        {
            public string Name;
            public CheckBox Box;
            public Func<bool> Get;
            public Action<bool> Set;
        }

        // One text box bound to a string property of SettingsData.
        private sealed class PathBinding
        {
            public string Name;
            public TextBox Box;
            public Func<string> Get;
            public Action<string> Set;
            public Func<string, string> Normalize;
        }

        private SettingsData settings = AppHost.Settings;
        private bool loading;
        private bool discordAvailable = true;
        private Func<string, string> localizer;

        private readonly PageTabs tabs = new PageTabs();
        private readonly List<ToggleBinding> toggles = new List<ToggleBinding>();
        private readonly List<PathBinding> paths = new List<PathBinding>();
        private readonly ComboBox comboLanguage = new ComboBox();
        private readonly LinkLabel linkTranslate = new LinkLabel();
        private readonly Button buttonClearCache = new Button();
        private readonly Label labelDiscordNote = new Label();
        private readonly List<Control> discordControls = new List<Control>();

        private ToggleBinding togglePresence;
        private ToggleBinding toggleShowGame;

        /// <summary>
        /// A setting was written. Name is the SettingsData property name (for example "UseRelativeTimes",
        /// "AlwaysShowCurrentGamePlaying", "InstallGameModsToUsbDevice"). The shell saves the settings and, for the
        /// ones it cares about, updates the status bar or the ribbon check button (ledger MW2-039, MW2-046).
        /// </summary>
        public event EventHandler<SettingChangedEventArgs> SettingChanged;

        /// <summary>
        /// The user picked a language (ledger MW2-030). Item is the name from the list. The shell switches the UI
        /// language and, if the language is not supported, calls <see cref="SelectLanguage"/> to put the old one back.
        /// </summary>
        public event EventHandler<ItemEventArgs<string>> LanguageChanged;

        /// <summary>
        /// A Discord toggle changed (ledger MW2-064, MW2-065). The settings are already written, including the knock-on
        /// change to the other toggle. The shell starts or stops the presence.
        /// </summary>
        public event EventHandler DiscordSettingsChanged;

        /// <summary>A link was clicked (ledger MW2-031). Item is the URL; the shell opens it in the system browser.</summary>
        public event EventHandler<ItemEventArgs<string>> LinkRequested;

        /// <summary>
        /// "Clear &amp; Restart" was confirmed (ledger MW2-063). The shell deletes the cache folder and restarts the
        /// application. Application.Restart on Mono is UNVERIFIED.
        /// </summary>
        public event EventHandler ClearCacheRequested;

        public SettingsPage()
        {
            SuspendLayout();
            Font = SystemFonts.MessageBoxFont;

            tabs.Dock = DockStyle.Fill;
            tabs.AddPage("interface", "Interface", BuildInterfaceTab());
            tabs.AddPage("mods", "Mods", BuildModsTab());
            tabs.AddPage("console", "Console", BuildConsoleTab());
            tabs.AddPage("downloads", "Downloads", BuildDownloadsTab());
            tabs.AddPage("cache", "Cache", BuildCacheTab());
            tabs.AddPage("discord", "Discord", BuildDiscordTab());
            Controls.Add(tabs);

            ResumeLayout(true);
        }

        // ---- public surface ----

        /// <summary>The settings being edited. Setting it reloads the controls.</summary>
        public SettingsData Settings
        {
            get { return settings; }
            set
            {
                settings = value ?? AppHost.Settings;
                LoadSettings();
            }
        }

        /// <summary>
        /// False greys out the Discord tab with a note (design decision D5 recommends dropping Discord presence for
        /// the first Linux release). Default true.
        /// </summary>
        public bool DiscordAvailable
        {
            get { return discordAvailable; }
            set
            {
                discordAvailable = value;
                foreach (Control control in discordControls)
                {
                    control.Enabled = value;
                }
                labelDiscordNote.Visible = !value;
            }
        }

        public Func<string, string> Localizer
        {
            get { return localizer; }
            set { localizer = value; }
        }

        /// <summary>Re-captions the tabs, the grouped captions and the buttons from <see cref="Localizer"/> (ledger MW2-029, Settings part).</summary>
        public void ApplyLanguage()
        {
            tabs.SetTitle("interface", PageUi.Text(localizer, "INTERFACE", "Interface"));
            tabs.SetTitle("mods", PageUi.Text(localizer, "MODS", "Mods"));
            tabs.SetTitle("console", PageUi.Text(localizer, "CONSOLE", "Console"));
            tabs.SetTitle("downloads", PageUi.Text(localizer, "DOWNLOADS", "Downloads"));
            tabs.SetTitle("cache", PageUi.Text(localizer, "CACHE", "Cache"));
            tabs.SetTitle("discord", PageUi.Text(localizer, "DISCORD", "Discord"));
            buttonClearCache.Text = PageUi.Text(localizer, "CLEAR_RESTART", "Clear & Restart");
            linkTranslate.Text = PageUi.Text(localizer, "HELP_TRANSLATE", "Help translate Arisen Studio on Crowdin");
        }

        public void OnPageShown()
        {
            LoadSettings();
        }

        /// <summary>Puts the language combo on the given language without raising <see cref="LanguageChanged"/>.</summary>
        public void SelectLanguage(string language)
        {
            bool wasLoading = loading;
            loading = true;
            try
            {
                int index = comboLanguage.Items.IndexOf(language ?? string.Empty);
                comboLanguage.SelectedIndex = index;
            }
            finally
            {
                loading = wasLoading;
            }
        }

        /// <summary>
        /// Copies every setting into its control (ledger MW2-032). The old code did not load InstallPackagesToUsbDevice
        /// (MW2-049); this one does. Does not write anything back and does not raise <see cref="SettingChanged"/>.
        /// </summary>
        public void LoadSettings()
        {
            if (settings == null)
            {
                return;
            }
            loading = true;
            try
            {
                foreach (ToggleBinding binding in toggles)
                {
                    binding.Box.Checked = binding.Get();
                }
                foreach (PathBinding binding in paths)
                {
                    binding.Box.Text = binding.Get() ?? string.Empty;
                }
                SelectLanguage(settings.Language);
            }
            finally
            {
                loading = false;
            }
        }

        // ---- building helpers ----

        private static Panel NewTabPanel()
        {
            Panel panel = new Panel();
            panel.AutoScroll = true;
            panel.Padding = new Padding(8);
            return panel;
        }

        private static FlowLayoutPanel NewStack()
        {
            FlowLayoutPanel stack = new FlowLayoutPanel();
            stack.Dock = DockStyle.Top;
            stack.AutoSize = true;
            stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stack.FlowDirection = FlowDirection.TopDown;
            stack.WrapContents = false;
            return stack;
        }

        private static GroupBox NewGroup(FlowLayoutPanel stack, string title)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            // The group is sized from its content by FitGroup. GroupBox.AutoSize with a docked, auto-sized child did not grow on Mono,
            // so the groups were cut off below their first rows (b104). The old page (MainWindow.Designer.cs) was a flat list of rows with no group.
            group.AutoSize = false;
            group.Padding = new Padding(8);
            group.Margin = new Padding(0, 0, 0, 8);
            group.MinimumSize = new Size(520, 0);

            FlowLayoutPanel inner = new FlowLayoutPanel();
            inner.Dock = DockStyle.Top;
            inner.AutoSize = true;
            inner.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            inner.FlowDirection = FlowDirection.TopDown;
            inner.WrapContents = false;
            group.Controls.Add(inner);
            inner.Layout += delegate { FitGroup(group, inner); };
            inner.SizeChanged += delegate { FitGroup(group, inner); };
            inner.ControlAdded += delegate (object sender, ControlEventArgs e)
            {
                // A child can finish sizing after the inner list measured itself (a LinkLabel did), and the inner list then stays too short.
                e.Control.SizeChanged += delegate { FitGroup(group, inner); };
                e.Control.VisibleChanged += delegate { FitGroup(group, inner); };
                FitGroup(group, inner);
            };
            group.Size = new Size(520, 40);
            stack.Controls.Add(group);
            return group;
        }

        // Group height = the inner list's preferred height plus the group's own frame, title and padding. Width is at least 520.
        private static void FitGroup(GroupBox group, FlowLayoutPanel inner)
        {
            Size wanted = inner.GetPreferredSize(Size.Empty);
            // The preferred size can be stale while a child (a LinkLabel) is still sizing itself: also reach the lowest child's bottom edge.
            foreach (Control child in inner.Controls)
            {
                if (child.Visible)
                {
                    wanted.Height = Math.Max(wanted.Height, child.Bottom + child.Margin.Bottom);
                    wanted.Width = Math.Max(wanted.Width, child.Right + child.Margin.Right);
                }
            }
            int chromeHeight = group.Height - group.DisplayRectangle.Height;
            int chromeWidth = group.Width - group.DisplayRectangle.Width;
            int height = wanted.Height + chromeHeight;
            int width = Math.Max(520, wanted.Width + chromeWidth);
            if (group.Height != height || group.Width != width)
            {
                group.Size = new Size(width, height);
            }
        }

        private static FlowLayoutPanel InnerOf(GroupBox group)
        {
            return (FlowLayoutPanel)group.Controls[0];
        }

        private ToggleBinding AddToggle(GroupBox group, string text, string name, Func<bool> get, Action<bool> set)
        {
            ToggleBinding binding = new ToggleBinding();
            binding.Name = name;
            binding.Get = get;
            binding.Set = set;
            binding.Box = new CheckBox();
            binding.Box.Text = text;
            binding.Box.AutoSize = true;
            binding.Box.Margin = new Padding(3, 3, 3, 3);
            binding.Box.CheckedChanged += delegate { Toggle_Changed(binding); };
            InnerOf(group).Controls.Add(binding.Box);
            toggles.Add(binding);
            return binding;
        }

        // The old Toggled handlers just copied IsOn into the setting (MW2-037 to MW2-054, MW2-062).
        private void Toggle_Changed(ToggleBinding binding)
        {
            if (loading || settings == null)
            {
                return;
            }
            binding.Set(binding.Box.Checked);
            RaiseSettingChanged(binding.Name);
        }

        private PathBinding AddPath(GroupBox group, string caption, string name, Func<string> get, Action<string> set,
            Func<string, string> normalize, bool withBrowse)
        {
            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;

            PathBinding binding = new PathBinding();
            binding.Name = name;
            binding.Get = get;
            binding.Set = set;
            binding.Normalize = normalize;
            binding.Box = new TextBox();
            binding.Box.Width = withBrowse ? 440 : 480;

            FlowLayoutPanel row = new FlowLayoutPanel();
            row.AutoSize = true;
            row.WrapContents = false;
            row.Margin = new Padding(0, 0, 0, 6);
            row.Controls.Add(binding.Box);

            if (withBrowse)
            {
                Button browse = new Button();
                browse.Text = "...";
                browse.Width = 36;
                browse.Click += delegate { Browse_Click(binding); };
                row.Controls.Add(browse);
            }

            // Commit when the user leaves the box or presses Enter (the old ButtonEdit commit button, MW2-058 to MW2-061).
            binding.Box.Validated += delegate { CommitPath(binding); };
            binding.Box.KeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    CommitPath(binding);
                }
            };

            InnerOf(group).Controls.Add(label);
            InnerOf(group).Controls.Add(row);
            paths.Add(binding);
            return binding;
        }

        private void CommitPath(PathBinding binding)
        {
            if (loading || settings == null)
            {
                return;
            }
            string text = binding.Box.Text.Trim();
            if (text.Length == 0)
            {
                // Empty means "put the saved path back" (ledger MW2-058).
                binding.Box.Text = binding.Get() ?? string.Empty;
                return;
            }
            string value = binding.Normalize != null ? binding.Normalize(text) : text;
            binding.Box.Text = value;
            if (value == binding.Get())
            {
                return;
            }
            binding.Set(value);
            RaiseSettingChanged(binding.Name);
        }

        // Folder browse (ledger MW2-055 to MW2-057).
        private void Browse_Click(PathBinding binding)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                string current = binding.Box.Text;
                try
                {
                    if (current.Length > 0 && Directory.Exists(Environment.ExpandEnvironmentVariables(current)))
                    {
                        dialog.SelectedPath = Environment.ExpandEnvironmentVariables(current);
                    }
                }
                catch (Exception)
                {
                    // An odd path is not worth stopping for; the dialog opens at its default.
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                binding.Box.Text = dialog.SelectedPath;
                CommitPath(binding);
            }
        }

        // Folder values end in one separator and have no doubled ones (the old code did this with backslashes).
        private static string FolderPath(string text)
        {
            char separator = Path.DirectorySeparatorChar;
            string doubled = separator.ToString() + separator;
            while (text.IndexOf(doubled, StringComparison.Ordinal) > 0)
            {
                text = text.Replace(doubled, separator.ToString());
            }
            return text.EndsWith(separator.ToString(), StringComparison.Ordinal) ? text : text + separator;
        }

        // Console paths keep the console's own separators. A PS3 folder ends in "/" (ledger MW2-058).
        private static string Ps3FolderPath(string text)
        {
            return text.EndsWith("/", StringComparison.Ordinal) ? text : text + "/";
        }

        private void RaiseSettingChanged(string name)
        {
            EventHandler<SettingChangedEventArgs> handler = SettingChanged;
            if (handler != null)
            {
                handler(this, new SettingChangedEventArgs(name));
            }
        }

        private static Label NewNote(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.MaximumSize = new Size(500, 0);
            label.Margin = new Padding(3, 0, 3, 6);
            return label;
        }

        // ---- tabs ----

        // Interface (ledger MW2-027 to MW2-031, MW2-037 to MW2-045)
        private Control BuildInterfaceTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox language = NewGroup(stack, "Language");
            Label labelLanguage = new Label();
            labelLanguage.Text = "Language";
            labelLanguage.AutoSize = true;
            comboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            comboLanguage.Width = 240;
            foreach (string name in Languages)
            {
                comboLanguage.Items.Add(name);
            }
            comboLanguage.SelectedIndexChanged += ComboLanguage_SelectedIndexChanged;
            linkTranslate.Text = "Help translate Arisen Studio on Crowdin";
            linkTranslate.AutoSize = true;
            linkTranslate.Padding = new Padding(0, 0, 0, 4); // Mono sized the link too short and cut the descenders of its text
            linkTranslate.LinkClicked += LinkTranslate_LinkClicked;
            InnerOf(language).Controls.Add(labelLanguage);
            InnerOf(language).Controls.Add(comboLanguage);
            InnerOf(language).Controls.Add(linkTranslate);

            GroupBox customization = NewGroup(stack, "Customization");
            AddToggle(customization, "Use formatted file sizes (for example \"3 MB\")", "UseFormattedFileSizes",
                () => settings.UseFormattedFileSizes, v => settings.UseFormattedFileSizes = v);
            AddToggle(customization, "Use relative times (for example \"3 months ago\")", "UseRelativeTimes",
                () => settings.UseRelativeTimes, v => settings.UseRelativeTimes = v);

            GroupBox automation = NewGroup(stack, "Automation");
            AddToggle(automation, "Always show the current game playing", "AlwaysShowCurrentGamePlaying",
                () => settings.AlwaysShowCurrentGamePlaying, v => settings.AlwaysShowCurrentGamePlaying = v);
            AddToggle(automation, "Show the games list from external devices", "ShowGamesFromExternalDevices",
                () => settings.ShowGamesFromExternalDevices, v => settings.ShowGamesFromExternalDevices = v);
            AddToggle(automation, "Automatically detect game titles", "AutoDetectGameTitles",
                () => settings.AutoDetectGameTitles, v => settings.AutoDetectGameTitles = v);
            AddToggle(automation, "Automatically load directory listings", "AutoLoadDirectoryListings",
                () => settings.AutoLoadDirectoryListings, v => settings.AutoLoadDirectoryListings = v);
            AddToggle(automation, "Remember the last console directory path", "RememberConsolePath",
                () => settings.RememberConsolePath, v => settings.RememberConsolePath = v);
            AddToggle(automation, "Remember the last local directory path", "RememberLocalPath",
                () => settings.RememberLocalPath, v => settings.RememberLocalPath = v);
            AddToggle(automation, "Automatically detect game regions", "AutoDetectGameRegions",
                () => settings.AutoDetectGameRegions, v => settings.AutoDetectGameRegions = v);

            panel.Controls.Add(stack);
            return panel;
        }

        // Mods (ledger MW2-046 to MW2-054). The grouping of the last four is this port's own.
        private Control BuildModsTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox transfer = NewGroup(stack, "Transferring Files");
            AddToggle(transfer, "Install Mods to a local USB device", "InstallGameModsToUsbDevice",
                () => settings.InstallGameModsToUsbDevice, v => settings.InstallGameModsToUsbDevice = v);
            AddToggle(transfer, "Install Homebrew to a local USB device", "InstallHomebrewToUsbDevice",
                () => settings.InstallHomebrewToUsbDevice, v => settings.InstallHomebrewToUsbDevice = v);
            AddToggle(transfer, "Install Resources to a local USB device", "InstallResourcesToUsbDevice",
                () => settings.InstallResourcesToUsbDevice, v => settings.InstallResourcesToUsbDevice = v);
            AddToggle(transfer, "Install Packages to a local USB device", "InstallPackagesToUsbDevice",
                () => settings.InstallPackagesToUsbDevice, v => settings.InstallPackagesToUsbDevice = v);
            AddToggle(transfer, "Install Game Saves to a local USB device", "InstallGameSavesToUsbDevice",
                () => settings.InstallGameSavesToUsbDevice, v => settings.InstallGameSavesToUsbDevice = v);

            GroupBox installing = NewGroup(stack, "Installing");
            AddToggle(installing, "Clean up local files after installing mods", "CleanUpLocalFilesAfterInstalling",
                () => settings.CleanUpLocalFilesAfterInstalling, v => settings.CleanUpLocalFilesAfterInstalling = v);
            AddToggle(installing, "Remember game regions when installing", "RememberGameRegions",
                () => settings.RememberGameRegions, v => settings.RememberGameRegions = v);
            AddToggle(installing, "Always back up the current game files", "AlwaysBackupGameFiles",
                () => settings.AlwaysBackupGameFiles, v => settings.AlwaysBackupGameFiles = v);
            AddToggle(installing, "Force install unsupported regions*", "IgnoreSupportedRegionsAndForceInstall",
                () => settings.IgnoreSupportedRegionsAndForceInstall, v => settings.IgnoreSupportedRegionsAndForceInstall = v);
            InnerOf(installing).Controls.Add(NewNote("* Installing for a region the mod does not list can leave the game unable to start."));

            panel.Controls.Add(stack);
            return panel;
        }

        // Console (ledger MW2-058 to MW2-061). The old boxes had a commit button; here the value is committed when the box is left.
        private Control BuildConsoleTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox ps3 = NewGroup(stack, "PlayStation 3");
            AddPath(ps3, "Packages File Install Path", "PackageInstallPathPS3",
                () => settings.PackageInstallPathPS3, v => settings.PackageInstallPathPS3 = v, Ps3FolderPath, false);

            // The PS4 default is a file name template ("{TitleId}/{Name}-{Version}.pkg"), so no trailing slash is forced here.
            GroupBox ps4 = NewGroup(stack, "PlayStation 4");
            AddPath(ps4, "Packages File Install Path", "PackageInstallPathPS4",
                () => settings.PackageInstallPathPS4, v => settings.PackageInstallPathPS4 = v, null, false);

            GroupBox xbox = NewGroup(stack, "Xbox 360");
            AddPath(xbox, "launch.ini File Path", "LaunchIniFilePath",
                () => settings.LaunchIniFilePath, v => settings.LaunchIniFilePath = v, null, false);
            AddPath(xbox, "Aurora Folder Path", "AuroraFolderPath",
                () => settings.AuroraFolderPath, v => settings.AuroraFolderPath = v, null, false);

            panel.Controls.Add(stack);
            return panel;
        }

        // Downloads (ledger MW2-055 to MW2-057)
        private Control BuildDownloadsTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox locations = NewGroup(stack, "Locations");
            AddPath(locations, "App Data", "PathAppData",
                () => settings.PathAppData, v => settings.PathAppData = v, FolderPath, true);
            AddPath(locations, "Downloads", "PathDownloads",
                () => settings.PathDownloads, v => settings.PathDownloads = v, FolderPath, true);
            AddPath(locations, "Temp Folder", "PathTemp",
                () => settings.PathTemp, v => settings.PathTemp = v, FolderPath, true);

            panel.Controls.Add(stack);
            return panel;
        }

        // Cache (ledger MW2-062, MW2-063)
        private Control BuildCacheTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox cache = NewGroup(stack, "Cache");
            AddToggle(cache, "Clear the local cache on close", "ClearCacheOnClose",
                () => settings.ClearCacheOnClose, v => settings.ClearCacheOnClose = v);
            buttonClearCache.Text = "Clear & Restart";
            buttonClearCache.UseMnemonic = false;
            buttonClearCache.AutoSize = true;
            buttonClearCache.Click += ButtonClearCache_Click;
            InnerOf(cache).Controls.Add(buttonClearCache);

            panel.Controls.Add(stack);
            return panel;
        }

        // Discord (ledger MW2-064, MW2-065)
        private Control BuildDiscordTab()
        {
            Panel panel = NewTabPanel();
            FlowLayoutPanel stack = NewStack();

            GroupBox discord = NewGroup(stack, "Discord");
            togglePresence = AddToggle(discord, "Always show presence for Arisen Studio", "AlwaysShowPresence",
                () => settings.AlwaysShowPresence, v => settings.AlwaysShowPresence = v);
            toggleShowGame = AddToggle(discord, "Show the current game playing", "ShowCurrentGamePlaying",
                () => settings.ShowCurrentGamePlaying, v => settings.ShowCurrentGamePlaying = v);

            // These two toggles are coupled, so they get their own handlers on top of the plain one.
            togglePresence.Box.CheckedChanged += TogglePresence_CheckedChanged;
            toggleShowGame.Box.CheckedChanged += ToggleShowGame_CheckedChanged;

            labelDiscordNote.Text = "Discord presence is not available in this build.";
            labelDiscordNote.AutoSize = true;
            labelDiscordNote.Visible = false;
            InnerOf(discord).Controls.Add(labelDiscordNote);

            discordControls.Add(togglePresence.Box);
            discordControls.Add(toggleShowGame.Box);

            panel.Controls.Add(stack);
            return panel;
        }

        // ---- handlers ----

        private void ComboLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (loading || comboLanguage.SelectedIndex < 0)
            {
                return;
            }
            string language = comboLanguage.SelectedItem as string;
            EventHandler<ItemEventArgs<string>> handler = LanguageChanged;
            if (handler != null && language != null)
            {
                handler(this, new ItemEventArgs<string>(language));
            }
        }

        private void LinkTranslate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            EventHandler<ItemEventArgs<string>> handler = LinkRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<string>(CrowdinUrl));
            }
        }

        // MW2-064: presence off also turns "show current game" off. The plain toggle handler has already written the
        // presence value by the time this runs, so only the knock-on is handled here.
        private void TogglePresence_CheckedChanged(object sender, EventArgs e)
        {
            if (loading || settings == null)
            {
                return;
            }
            if (!togglePresence.Box.Checked && toggleShowGame.Box.Checked)
            {
                SetSilently(toggleShowGame, false);
            }
            RaiseDiscordChanged();
        }

        // MW2-065: "show current game" on forces presence on.
        private void ToggleShowGame_CheckedChanged(object sender, EventArgs e)
        {
            if (loading || settings == null)
            {
                return;
            }
            if (toggleShowGame.Box.Checked && !togglePresence.Box.Checked)
            {
                SetSilently(togglePresence, true);
            }
            RaiseDiscordChanged();
        }

        // Changes a toggle and its setting without running its handlers again.
        private void SetSilently(ToggleBinding binding, bool value)
        {
            bool wasLoading = loading;
            loading = true;
            try
            {
                binding.Box.Checked = value;
                binding.Set(value);
            }
            finally
            {
                loading = wasLoading;
            }
            RaiseSettingChanged(binding.Name);
        }

        private void RaiseDiscordChanged()
        {
            EventHandler handler = DiscordSettingsChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ButtonClearCache_Click(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(this,
                "Clear the local cache and restart Arisen Studio?", "Clear & Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            EventHandler handler = ClearCacheRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
