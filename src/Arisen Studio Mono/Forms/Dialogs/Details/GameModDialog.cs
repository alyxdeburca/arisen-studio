using ArisenStudio.Controls;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Shows the details of one game mod: the stats, the description and the downloads, with Download, Install,
    /// Favorite, Report Issue and Help. Stock WinForms port of the old DevExpress GameModDialog form.
    /// The dialog opens no other form and never calls the console: the buttons raise events and the host decides what to do.
    /// Set ModItem, CategoryType, Settings, Categories, ConsoleProfile, Language and IsConsoleConnected before the dialog is shown.
    /// </summary>
    internal class GameModDialog : Form
    {
        private const string SectionName = "Game Mods";
        private const string TabDescriptionId = "description";
        private const string TabDownloadsId = "downloads";

        private static readonly Regex LinkPattern = new Regex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase);

        private readonly Font _boldFont;
        private readonly int _separatorHeight;
        private readonly DetailLayout _layout;
        private readonly StatItem _statSystemType;
        private readonly StatItem _statModType;
        private readonly StatItem _statVersion;
        private readonly StatItem _statCreatedBy;
        private readonly StatItem _statSubmittedBy;
        private readonly StatItem _statGameMode;
        private readonly StatItem _statLastUpdated;
        private readonly PageTabs _tabs;
        private readonly TextBox _description;
        private readonly FlowLayoutPanel _links;
        private readonly TableLayoutPanel _downloadsTable;
        private readonly Button _buttonDownload;
        private readonly Button _buttonInstall;
        private readonly Button _buttonFavorite;
        private readonly Button _buttonReport;
        private readonly Button _buttonHelp;

        public GameModDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Mod Details";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;
            _boldFont = new Font(Font, FontStyle.Bold);
            _separatorHeight = Math.Max(1, h / 12);

            // Stats: the old five-column grid. Row 0 holds System Type, Mod Type, Version, Created By and Submitted By;
            // row 1 holds Game Mode and Last Updated.
            Padding statMargin = new Padding(0, 0, pad, pad);
            _statSystemType = CreateStat(statMargin);
            _statModType = CreateStat(statMargin);
            _statVersion = CreateStat(statMargin);
            _statCreatedBy = CreateStat(statMargin);
            _statSubmittedBy = CreateStat(statMargin);
            _statGameMode = CreateStat(statMargin);
            _statLastUpdated = CreateStat(statMargin);

            TableLayoutPanel statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 5,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < 5; i++)
            {
                _ = statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            }
            for (int i = 0; i < 2; i++)
            {
                _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            statsGrid.Controls.Add(_statSystemType, 0, 0);
            statsGrid.Controls.Add(_statModType, 1, 0);
            statsGrid.Controls.Add(_statVersion, 2, 0);
            statsGrid.Controls.Add(_statCreatedBy, 3, 0);
            statsGrid.Controls.Add(_statSubmittedBy, 4, 0);
            statsGrid.Controls.Add(_statGameMode, 0, 1);
            statsGrid.Controls.Add(_statLastUpdated, 1, 1);

            // Description page: the text, then the web links found in it. The text box scrolls itself.
            _description = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                TabStop = false,
                Margin = Padding.Empty
            };
            _links = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, pad / 2, 0, 0),
                Padding = Padding.Empty
            };
            TableLayoutPanel descriptionPage = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = descriptionPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = descriptionPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = descriptionPage.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            descriptionPage.Controls.Add(_description, 0, 0);
            descriptionPage.Controls.Add(_links, 0, 1);

            // Downloads page: one row per download file, in a panel that scrolls.
            _downloadsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _downloadsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Panel downloadsHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            downloadsHost.Controls.Add(_downloadsTable);

            _tabs = new PageTabs { Dock = DockStyle.Fill };
            _ = _tabs.AddPage(TabDescriptionId, "Description", descriptionPage);
            _ = _tabs.AddPage(TabDownloadsId, "Downloads", downloadsHost);

            // Body: the stats above the tabs. The tab area is about 20 text lines tall, in place of the old fixed height.
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            _ = body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = body.RowStyles.Add(new RowStyle(SizeType.Absolute, h * 20));
            body.Controls.Add(statsGrid, 0, 0);
            body.Controls.Add(_tabs, 0, 1);

            // Shared shell: header with crumbs, name and separator; the body; and the action row ending with Close.
            _layout = new DetailLayout
            {
                Dock = DockStyle.Fill,
                CloseText = "Close"
            };
            _layout.CloseRequested += (s, e) => Close();
            _layout.Body = body;

            // AddAction places each button to the left of the ones before it, after Close.
            // Added Help first, then Report, Favorite, Install, Download, so the visual order is Download, Install, Favorite, Report, Help, Close.
            _buttonHelp = _layout.AddAction(string.Empty, ButtonHelp_Click);
            _buttonReport = _layout.AddAction(string.Empty, ButtonReport_Click);
            _buttonFavorite = _layout.AddAction(string.Empty, ButtonFavorite_Click);
            _buttonInstall = _layout.AddAction(string.Empty, ButtonInstall_Click);
            _buttonDownload = _layout.AddAction(string.Empty, ButtonDownload_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The mod to show. Read once when the dialog loads; change it before showing the dialog.
        /// </summary>
        public ModItemData ModItem { get; set; }

        /// <summary>
        /// The category of the mod. Game mods use CategoryType.Game; it decides the per-file Install button and the favorite key.
        /// </summary>
        public CategoryType CategoryType { get; set; } = CategoryType.Game;

        /// <summary>
        /// The installed copy of this mod for the console profile, or null. Set when the dialog loads.
        /// </summary>
        public InstalledModInfo InstalledModInfo { get; set; }

        /// <summary>
        /// True when the mod is in the favorites list. Set when the dialog loads and toggled by the Favorite button.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Settings that control the date text, the favorites list and whether Install is enabled for a USB install.
        /// Defaults to the shared application settings. Null falls back to the shared settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The categories data, used to look up the category title and the category of a transfer.
        /// Null (the default) shows the category ID as the category title.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// The console profile. Null (the default) means no installed-mod state is shown.
        /// Passed in by the host.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Install is enabled when this returns true or
        /// Settings.InstallGameModsToUsbDevice is set. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised when a Download or Install (or Uninstall) button is clicked. The host starts the transfer.
        /// Replaces the old DialogExtensions.ShowTransferFilesDialog calls.
        /// </summary>
        public event EventHandler<TransferRequestedEventArgs> TransferRequested;

        /// <summary>
        /// Raised when Report Issue is clicked. The host shows the report-issue dialog.
        /// </summary>
        public event EventHandler ReportIssueRequested;

        /// <summary>
        /// Raised when Help and Support is clicked. The host opens the help page.
        /// </summary>
        public event EventHandler HelpRequested;

        /// <summary>
        /// Raised when a web link in the description is clicked. The host opens the link.
        /// </summary>
        public event EventHandler<LinkRequestedEventArgs> LinkRequested;

        /// <summary>
        /// Raised when the Favorite button changes the favorites list. The host decides whether to save the settings.
        /// </summary>
        public event EventHandler FavoriteChanged;

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            ShowModDetails();
            SizeToContent();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _boldFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ApplyLanguage()
        {
            _statLastUpdated.Title = Lookup("LABEL_LAST_UPDATED", "Last Updated");
            _statSystemType.Title = Lookup("LABEL_SYSTEM_TYPE", "System Type");
            _statModType.Title = Lookup("LABEL_MOD_TYPE", "Mod Type");
            _statVersion.Title = Lookup("LABEL_VERSION", "Version");
            _statCreatedBy.Title = Lookup("LABEL_CREATED_BY", "Created By");
            _statSubmittedBy.Title = Lookup("LABEL_SUBMITTED_BY", "Submitted By");
            _statGameMode.Title = Lookup("LABEL_GAME_MODE", "Game Mode");

            _ = _tabs.SetTitle(TabDescriptionId, Lookup("LABEL_DESCRIPTION", "Description"));

            _buttonReport.Text = EscapeMnemonic(Lookup("LABEL_REPORT_ISSUE", "Report Issue"));
            _buttonHelp.Text = EscapeMnemonic(Lookup("LABEL_HELP_SUPPORT", "Help & Support"));
        }

        private void ShowModDetails()
        {
            ModItemData item = ModItem;
            SettingsData settings = CurrentSettings();

            if (item == null)
            {
                _layout.SetCrumbs(SectionName, string.Empty, string.Empty);
                _layout.SetName(string.Empty);
                foreach (StatItem stat in StatItems())
                {
                    stat.Value = string.Empty;
                }
                _description.Text = string.Empty;
                ShowLinks(string.Empty);
                ClearDownloadRows();
                SetDownloadsTitle(0);
                _buttonDownload.Enabled = false;
                _buttonInstall.Enabled = false;
                _buttonFavorite.Enabled = false;
                return;
            }

            List<DownloadFiles> files = item.DownloadFiles ?? new List<DownloadFiles>();
            bool hasFiles = files.Count > 0;

            InstalledModInfo = ConsoleProfile != null ? settings.GetInstalledMods(ConsoleProfile, item.CategoryId, item.Id, false) : null;
            IsFavorite = settings.FavoriteMods.Exists(x => IsFavoriteOf(x, item));

            // Header: crumbs (section / category (regions)) and the name.
            _layout.SetCrumbs(SectionName, CategoryTitle(item), string.Join(", ", item.Regions));
            _layout.SetName(item.Name);

            // Stats. Each value is escaped because StatItem's value label treats "&" as a mnemonic marker.
            _statSystemType.Value = EscapeMnemonic(item.FirmwareType);
            _statModType.Value = EscapeMnemonic(item.ModType);
            _statVersion.Value = EscapeMnemonic(string.Join(" & ", item.Versions));
            _statCreatedBy.Value = string.IsNullOrWhiteSpace(item.CreatedBy) ? "Unknown" : EscapeMnemonic(item.CreatedBy);
            _statSubmittedBy.Value = EscapeMnemonic(item.SubmittedBy);
            _statGameMode.Value = EscapeMnemonic(string.Join(", ", item.GameModes));
            _statLastUpdated.Value = hasFiles ? RelativeOrDate(files[0].LastUpdated, settings, "MM/dd/yyyy") : string.Empty;

            // Description. The text box shows plain text, so no escaping; line ends are CRLF for the text box.
            string text = string.IsNullOrWhiteSpace(item.Description)
                ? Lookup("NO_MORE_DETAILS", "No more details are available.")
                : item.Description;
            text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            if (string.Equals(item.ModType, "SCRIPT", StringComparison.Ordinal))
            {
                text += "\r\n\r\n" + Lookup("SCRIPT_CANT_BE_INSTALLED", "Script mods cannot be installed.");
            }
            _description.Text = text;
            ShowLinks(text);

            // Downloads: one row per file. The last file is the "latest" one for the header buttons.
            SetDownloadsTitle(files.Count);
            ShowDownloads(files, settings);

            _buttonDownload.Text = EscapeMnemonic(files.Count > 1 ? Lookup("LABEL_DOWNLOAD_LATEST", "Download Latest") : Lookup("LABEL_DOWNLOAD", "Download"));
            _buttonDownload.Enabled = hasFiles;
            _buttonInstall.Text = EscapeMnemonic(files.Count > 1 ? Lookup("LABEL_INSTALL_LATEST", "Install Latest") : Lookup("LABEL_INSTALL", "Install"));
            _buttonInstall.Enabled = hasFiles && IsInstallAvailable(settings);
            _buttonFavorite.Enabled = true;
            UpdateFavoriteButton();
        }

        private void ShowDownloads(List<DownloadFiles> files, SettingsData settings)
        {
            ClearDownloadRows();

            for (int i = 0; i < files.Count; i++)
            {
                // The separator sits above every file except the first, when there is more than one.
                TableLayoutPanel row = CreateDownloadRow(files[i], i > 0 && files.Count > 1, settings);
                int index = _downloadsTable.RowCount;
                _ = _downloadsTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _downloadsTable.RowCount = index + 1;
                _downloadsTable.Controls.Add(row, 0, index);
            }
        }

        private void ClearDownloadRows()
        {
            while (_downloadsTable.Controls.Count > 0)
            {
                Control control = _downloadsTable.Controls[0];
                _downloadsTable.Controls.Remove(control);
                control.Dispose();
            }
            _downloadsTable.RowStyles.Clear();
            _downloadsTable.RowCount = 0;
        }

        /// <summary>
        /// One download file: its name, file count, last updated, region and version, then the buttons,
        /// and a list of the install paths that the toggle button shows.
        /// </summary>
        private TableLayoutPanel CreateDownloadRow(DownloadFiles file, bool showSeparator, SettingsData settings)
        {
            int h = Font.Height;
            int pad = h / 2;
            List<string> installPaths = file.InstallPaths ?? new List<string>();
            int fileCount = installPaths.Count;

            TableLayoutPanel rowTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 6,
                Margin = Padding.Empty,
                Padding = new Padding(pad, pad / 2, pad, pad / 2)
            };
            _ = rowTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
            {
                _ = rowTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            // Row 4 is the install path list, 20 high until the toggle opens it (never 0: a zero-height ListBox has a negative client height and cannot be drawn on Mono). Row 5 is the separator.
            _ = rowTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            _ = rowTable.RowStyles.Add(new RowStyle(SizeType.Absolute, showSeparator ? _separatorHeight : 0F));

            Label name = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = _boldFont,
                Text = file.Name ?? string.Empty,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            Label fileCountLabel = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Text = fileCount + " " + (fileCount == 1 ? Lookup("LABEL_FILE", "File") : Lookup("LABEL_FILES", "Files")),
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            TableLayoutPanel facts = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, pad / 2),
                Padding = Padding.Empty
            };
            for (int i = 0; i < 3; i++)
            {
                _ = facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            _ = facts.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            facts.Controls.Add(CreateFact("LABEL_LAST_UPDATED", "Last Updated", RelativeOrDate(file.LastUpdated, settings, "dd MMM yyyy")), 0, 0);
            facts.Controls.Add(CreateFact("LABEL_REGION", "Region", string.IsNullOrEmpty(file.Region) ? "-" : file.Region), 1, 0);
            facts.Controls.Add(CreateFact("LABEL_VERSION", "Version", string.IsNullOrEmpty(file.Version) ? "-" : file.Version), 2, 0);

            Button download = CreateRowButton(Lookup("LABEL_DOWNLOAD", "Download"));
            download.Click += (s, e) => RaiseTransfer(TransferType.DownloadMods, file);

            // Install is only offered when a console is connected, as the old per-file row did for game mods.
            Button install = CreateRowButton(Lookup("LABEL_INSTALL", "Install"));
            install.Visible = IsConsoleConnectedNow();
            install.Click += (s, e) => RaiseTransfer(IsInstalled(settings) ? TransferType.UninstallMods : TransferType.InstallMods, file);

            Button copyLink = CreateRowButton(Lookup("LABEL_COPY_LINK", "Copy Link"));
            copyLink.Click += (s, e) => CopyLink(file.Url);

            ListBox pathList = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                TabStop = false,
                Margin = Padding.Empty
            };
            pathList.MinimumSize = new Size(1, 1);
            foreach (string path in installPaths)
            {
                _ = pathList.Items.Add(path);
            }
            int listHeight = Math.Min(Math.Max(1, fileCount), 8) * h + pad;

            bool expanded = false;
            Button toggle = CreateRowButton(Lookup("LABEL_INSTALLATION_FILES", "Installation Files"));
            toggle.Click += (s, e) =>
            {
                expanded = !expanded;
                rowTable.RowStyles[4].Height = expanded ? listHeight : 20F;
            };

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(download);
            actions.Controls.Add(install);
            actions.Controls.Add(copyLink);
            actions.Controls.Add(toggle);

            Panel separator = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Gainsboro,
                Margin = Padding.Empty
            };
            separator.MinimumSize = new Size(1, 1);

            rowTable.Controls.Add(name, 0, 0);
            rowTable.Controls.Add(fileCountLabel, 0, 1);
            rowTable.Controls.Add(facts, 0, 2);
            rowTable.Controls.Add(actions, 0, 3);
            rowTable.Controls.Add(pathList, 0, 4);
            rowTable.Controls.Add(separator, 0, 5);
            return rowTable;
        }

        private void ShowLinks(string text)
        {
            while (_links.Controls.Count > 0)
            {
                Control control = _links.Controls[0];
                _links.Controls.Remove(control);
                control.Dispose();
            }

            int pad = Font.Height / 2;
            IEnumerable<string> urls = LinkPattern.Matches(text ?? string.Empty)
                .Cast<Match>()
                .Select(match => match.Value.TrimEnd('.', ',', ';', ')', ']'))
                .Distinct();

            foreach (string url in urls)
            {
                LinkLabel link = new LinkLabel
                {
                    AutoSize = true,
                    UseMnemonic = false,
                    Text = url,
                    LinkArea = new LinkArea(0, url.Length),
                    Margin = new Padding(0, 0, pad, 0)
                };
                link.LinkClicked += LinkLabel_LinkClicked;
                _links.Controls.Add(link);
            }
        }

        private void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel link = sender as LinkLabel;
            if (link != null)
            {
                LinkRequested?.Invoke(this, new LinkRequestedEventArgs(link.Text));
            }
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            DownloadFiles latest = LatestDownload();
            if (latest != null)
            {
                RaiseTransfer(TransferType.DownloadMods, latest);
            }
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            DownloadFiles latest = LatestDownload();
            if (latest == null)
            {
                return;
            }

            if (InstalledModInfo == null)
            {
                RaiseTransfer(TransferType.InstallMods, latest);
            }
            else
            {
                RaiseTransfer(TransferType.UninstallMods, InstalledModInfo.DownloadFiles);
            }
        }

        private void ButtonFavorite_Click(object sender, EventArgs e)
        {
            ModItemData item = ModItem;
            if (item == null)
            {
                return;
            }

            List<FavoriteItem> favorites = CurrentSettings().FavoriteMods;
            if (IsFavorite)
            {
                _ = favorites.RemoveAll(x => IsFavoriteOf(x, item));
                IsFavorite = false;
            }
            else
            {
                favorites.Add(new FavoriteItem
                {
                    CategoryType = CategoryType,
                    CategoryId = item.CategoryId,
                    ModId = item.Id,
                    Platform = item.GetPlatform()
                });
                IsFavorite = true;
            }

            UpdateFavoriteButton();
            FavoriteChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonReport_Click(object sender, EventArgs e)
        {
            ReportIssueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonHelp_Click(object sender, EventArgs e)
        {
            HelpRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFavoriteButton()
        {
            _buttonFavorite.Text = EscapeMnemonic(IsFavorite
                ? Lookup("LABEL_REMOVE_FROM_FAVORITES", "Remove from Favorites")
                : Lookup("LABEL_ADD_TO_FAVORITES", "Add to Favorites"));
        }

        private void RaiseTransfer(TransferType transferType, DownloadFiles downloadFiles)
        {
            EventHandler<TransferRequestedEventArgs> handler = TransferRequested;
            if (handler != null && ModItem != null)
            {
                handler(this, new TransferRequestedEventArgs(transferType, ResolveCategory(), ModItem, downloadFiles));
            }
        }

        private CategoryItem ResolveCategory()
        {
            return Categories != null && ModItem != null ? ModItem.GetCategory(Categories) : null;
        }

        private DownloadFiles LatestDownload()
        {
            List<DownloadFiles> files = ModItem?.DownloadFiles;
            return files != null && files.Count > 0 ? files[files.Count - 1] : null;
        }

        private bool IsInstalled(SettingsData settings)
        {
            return ConsoleProfile != null && ModItem != null && settings.GetInstalledMods(ConsoleProfile, CategoryType, ModItem.Id) != null;
        }

        private bool IsFavoriteOf(FavoriteItem favorite, ModItemData item)
        {
            return favorite.CategoryType == CategoryType
                && favorite.CategoryId == item.CategoryId
                && favorite.ModId == item.Id
                && favorite.Platform == item.GetPlatform();
        }

        private bool IsInstallAvailable(SettingsData settings)
        {
            return IsConsoleConnectedNow() || settings.InstallGameModsToUsbDevice;
        }

        private bool IsConsoleConnectedNow()
        {
            Func<bool> isConnected = IsConsoleConnected;
            return isConnected != null && isConnected();
        }

        private SettingsData CurrentSettings()
        {
            return Settings ?? AppHost.Settings;
        }

        private string CategoryTitle(ModItemData item)
        {
            return Categories != null ? item.GetCategoryName(Categories) : (item.CategoryId ?? string.Empty);
        }

        private void SetDownloadsTitle(int count)
        {
            _ = _tabs.SetTitle(TabDownloadsId, Lookup("LABEL_DOWNLOADS", "Downloads") + " (" + count + ")");
        }

        private IEnumerable<StatItem> StatItems()
        {
            return new[]
            {
                _statSystemType,
                _statModType,
                _statVersion,
                _statCreatedBy,
                _statSubmittedBy,
                _statGameMode,
                _statLastUpdated
            };
        }

        /// <summary>
        /// Sizes the form 50 font heights wide and to its content tall, each clamped to 90% of the working area
        /// of the screen it is on. The width is set first so the header wraps the name to it.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 50, (int)(work.Width * 0.9));
            ClientSize = new Size(width, ClientSize.Height);

            int height = Math.Min(Math.Max(_layout.PreferredSize.Height, h * 34), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private StatItem CreateStat(Padding margin)
        {
            return new StatItem
            {
                Dock = DockStyle.Fill,
                Margin = margin
            };
        }

        private StatItem CreateFact(string key, string fallback, string value)
        {
            StatItem fact = CreateStat(new Padding(0, 0, Font.Height / 2, 0));
            fact.Title = Lookup(key, fallback);
            fact.Value = EscapeMnemonic(value);
            return fact;
        }

        private Button CreateRowButton(string text)
        {
            return new Button
            {
                AutoSize = true,
                Text = EscapeMnemonic(text),
                Margin = new Padding(0, 0, Font.Height / 4, 0)
            };
        }

        private string RelativeOrDate(DateTime value, SettingsData settings, string format)
        {
            return settings.UseRelativeTimes ? value.Humanize() : value.ToString(format, CultureInfo.CurrentCulture);
        }

        private static void CopyLink(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                Clipboard.SetText(url);
            }
            catch (ExternalException)
            {
                // The clipboard was busy. The user can click again.
            }
        }

        /// <summary>
        /// Doubles "&amp;" so a label or button shows it literally rather than as a mnemonic.
        /// </summary>
        private static string EscapeMnemonic(string text)
        {
            return (text ?? string.Empty).Replace("&", "&&");
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        /// <summary>
        /// Arguments for TransferRequested. Category is null when the host gave no categories data.
        /// </summary>
        public class TransferRequestedEventArgs : EventArgs
        {
            public TransferRequestedEventArgs(TransferType transferType, CategoryItem category, ModItemData modItem, DownloadFiles downloadFiles)
            {
                TransferType = transferType;
                Category = category;
                ModItem = modItem;
                DownloadFiles = downloadFiles;
            }

            public TransferType TransferType { get; }

            public CategoryItem Category { get; }

            public ModItemData ModItem { get; }

            public DownloadFiles DownloadFiles { get; }
        }

        /// <summary>
        /// Arguments for LinkRequested: the web link that was clicked.
        /// </summary>
        public class LinkRequestedEventArgs : EventArgs
        {
            public LinkRequestedEventArgs(string url)
            {
                Url = url;
            }

            public string Url { get; }
        }
    }
}
