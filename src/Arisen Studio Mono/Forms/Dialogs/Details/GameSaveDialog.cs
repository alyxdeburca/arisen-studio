using ArisenStudio.Controls;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Shows the details of one game save: the stats, the description, the download files, and the actions
    /// Download, Install, Favorite, Report Issue and Help.
    /// Stock WinForms port of the old DevExpress GameSaveDialog form.
    /// The dialog opens no other form and never calls the console: the buttons raise events and the host decides.
    /// Set GameSaveItem, Categories, Settings, Language, ConsoleProfile and IsConsoleConnected before the dialog is shown.
    /// </summary>
    internal class GameSaveDialog : Form
    {
        private readonly Font _boldFont;
        private readonly DetailLayout _layout;
        private readonly StatItem _statLastUpdated;
        private readonly StatItem _statVersion;
        private readonly StatItem _statGameMode;
        private readonly StatItem _statCreatedBy;
        private readonly StatItem _statSubmittedBy;
        private readonly PageTabs _tabs;
        private readonly RichTextBox _description;
        private readonly TableLayoutPanel _downloadsList;
        private readonly Button _buttonDownload;
        private readonly Button _buttonInstall;
        private readonly Button _buttonFavorite;
        private readonly Button _buttonReport;
        private readonly Button _buttonHelp;

        public GameSaveDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Save Details";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            _boldFont = new Font(Font, FontStyle.Bold);

            int h = Font.Height;
            int pad = h / 2;

            // Stats: last updated, version and game mode on the first row; created by and submitted by on the second.
            _statLastUpdated = CreateStat(new Padding(0, 0, pad, pad));
            _statVersion = CreateStat(new Padding(0, 0, pad, pad));
            _statGameMode = CreateStat(new Padding(0, 0, 0, pad));
            _statCreatedBy = CreateStat(new Padding(0, 0, pad, 0));
            _statSubmittedBy = CreateStat(Padding.Empty);

            TableLayoutPanel statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < 3; i++)
            {
                _ = statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsGrid.Controls.Add(_statLastUpdated, 0, 0);
            statsGrid.Controls.Add(_statVersion, 1, 0);
            statsGrid.Controls.Add(_statGameMode, 2, 0);
            statsGrid.Controls.Add(_statCreatedBy, 0, 1);
            statsGrid.Controls.Add(_statSubmittedBy, 1, 1);
            statsGrid.SetColumnSpan(_statSubmittedBy, 2);

            // Description tab: read-only plain text. Links are reported through DescriptionLinkClicked.
            _description = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                Margin = Padding.Empty
            };
            _description.LinkClicked += (s, e) => DescriptionLinkClicked?.Invoke(this, e);

            // Downloads tab: one row per download file, in a scrolling table.
            _downloadsList = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _downloadsList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _tabs = new PageTabs
            {
                Dock = DockStyle.Fill
            };
            // The English titles are replaced in ApplyLanguage, once the host has set Language.
            _ = _tabs.AddPage("description", "Description", _description);
            _ = _tabs.AddPage("downloads", "Downloads", _downloadsList);

            // Body: the stats above the tabs, in a padded container. The header separator already sits above it.
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad, pad, pad, pad)
            };
            _ = body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
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
            // Added Help, Report, Favorite, Install, Download, so the visual order is Download, Install, Favorite, Report, Help, Close.
            _buttonHelp = _layout.AddAction("Help && Support", ButtonHelp_Click);
            _buttonReport = _layout.AddAction("Report Issue", ButtonReport_Click);
            _buttonFavorite = _layout.AddAction("Add to Favorites", ButtonFavorite_Click);
            _buttonInstall = _layout.AddAction("Install", ButtonInstall_Click);
            _buttonDownload = _layout.AddAction("Download", ButtonDownload_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The game save to show. Read once when the dialog loads; set it before showing the dialog.
        /// </summary>
        public GameSaveItemData GameSaveItem { get; set; }

        /// <summary>
        /// The category type the favourite is stored under. Defaults to the enum's first value (Game) as the old form did;
        /// the old caller never set it, so set it to CategoryType.GameSave from the host if game save favourites should use that type.
        /// </summary>
        public CategoryType CategoryType { get; set; }

        /// <summary>
        /// True when the game save is in the favourites. Set when the dialog loads; the Favorite button updates it.
        /// </summary>
        public bool IsFavorite { get; private set; }

        /// <summary>
        /// The categories, used for the category crumb and for the category passed in transfer requests.
        /// Null (the default) shows no category text and passes a null category.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings that control the date text, the favourites and whether Install is enabled for a USB install.
        /// Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The console profile. Kept for parity with the old static; this form does not read it.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Install is enabled when this returns true or
        /// Settings.InstallGameSavesToUsbDevice is set. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised when the Download button, or a Download button on a download row, is clicked.
        /// The host starts the transfer, for example with DialogExtensions.ShowTransferGameSavesDialog.
        /// </summary>
        public event EventHandler<TransferEventArgs> DownloadRequested;

        /// <summary>
        /// Raised when the Install button, or an Install button on a download row, is clicked.
        /// The host starts the transfer, for example with DialogExtensions.ShowTransferGameSavesDialog.
        /// </summary>
        public event EventHandler<TransferEventArgs> InstallRequested;

        /// <summary>
        /// Raised when a link in the description is clicked. The host opens the link.
        /// </summary>
        public event EventHandler<LinkClickedEventArgs> DescriptionLinkClicked;

        /// <summary>
        /// Raised when the Report Issue button is clicked. The host shows the report dialog.
        /// </summary>
        public event EventHandler ReportIssueRequested;

        /// <summary>
        /// Raised when the Help and Support button is clicked. The host shows the help.
        /// </summary>
        public event EventHandler HelpRequested;

        /// <summary>
        /// The arguments of a download or install request: the transfer, the category, the game save and the download file.
        /// The category is null when Categories has no match.
        /// </summary>
        public class TransferEventArgs : EventArgs
        {
            public TransferEventArgs(TransferType transferType, CategoryItem category, GameSaveItemData gameSaveItem, DownloadFiles downloadFiles)
            {
                TransferType = transferType;
                Category = category;
                GameSaveItem = gameSaveItem;
                DownloadFiles = downloadFiles;
            }

            public TransferType TransferType { get; private set; }

            public CategoryItem Category { get; private set; }

            public GameSaveItemData GameSaveItem { get; private set; }

            public DownloadFiles DownloadFiles { get; private set; }
        }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            ShowGameSaveDetails();
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
            _statVersion.Title = Lookup("LABEL_VERSION", "Version");
            _statGameMode.Title = Lookup("LABEL_GAME_MODE", "Game Mode");
            _statCreatedBy.Title = Lookup("LABEL_CREATED_BY", "Created By");
            _statSubmittedBy.Title = Lookup("LABEL_SUBMITTED_BY", "Submitted By");

            _ = _tabs.SetTitle("description", Lookup("LABEL_DESCRIPTION", "Description"));
            _ = _tabs.SetTitle("downloads", Lookup("LABEL_DOWNLOADS", "Downloads"));

            _buttonReport.Text = Lookup("LABEL_REPORT_ISSUE", "Report Issue");
            _buttonHelp.Text = Lookup("LABEL_HELP_SUPPORT", "Help && Support");
        }

        private void ShowGameSaveDetails()
        {
            GameSaveItemData item = GameSaveItem;
            if (item == null)
            {
                _layout.SetCrumbs(Lookup("LABEL_GAME_SAVES", "Game Saves"), string.Empty, string.Empty);
                _layout.SetName(string.Empty);
                _statLastUpdated.Value = string.Empty;
                _statVersion.Value = string.Empty;
                _statGameMode.Value = string.Empty;
                _statCreatedBy.Value = string.Empty;
                _statSubmittedBy.Value = string.Empty;
                _description.Text = string.Empty;
                _ = _tabs.SetTitle("downloads", Lookup("LABEL_DOWNLOADS", "Downloads") + " (0)");
                _buttonDownload.Enabled = false;
                _buttonInstall.Enabled = false;
                _buttonFavorite.Enabled = false;
                return;
            }

            SettingsData settings = Settings ?? AppHost.Settings;
            bool connected = IsConnected();
            List<DownloadFiles> files = item.DownloadFiles ?? new List<DownloadFiles>();
            DownloadFiles latest = files.Count > 0 ? files[files.Count - 1] : null;
            CategoryItem category = FindCategory(item);

            IsFavorite = settings.FavoriteGameSaves != null && settings.FavoriteGameSaves.Exists(x => IsSameFavorite(x, item));

            _layout.SetCrumbs(Lookup("LABEL_GAME_SAVES", "Game Saves"), category == null ? string.Empty : category.Title, item.Region);
            _layout.SetName(item.Name);

            _statLastUpdated.Value = settings.UseRelativeTimes
                ? item.LastUpdated.Humanize()
                : item.LastUpdated.ToString("MM/dd/yyyy", CultureInfo.CurrentCulture);
            _statVersion.Value = item.Version ?? string.Empty;
            _statGameMode.Value = item.GameMode ?? string.Empty;
            _statCreatedBy.Value = string.IsNullOrWhiteSpace(item.CreatedBy) ? "Unknown" : item.CreatedBy;
            _statSubmittedBy.Value = item.SubmittedBy ?? string.Empty;

            string description = string.IsNullOrWhiteSpace(item.Description)
                ? Lookup("NO_MORE_DETAILS", "No other details are available, yet... Please let us know if you can provide more!")
                : item.Description;
            _description.Text = (description + ExtraInformation()).Replace("\r\n", "\n").Replace("\n", "\r\n");

            _ = _tabs.SetTitle("downloads", Lookup("LABEL_DOWNLOADS", "Downloads") + " (" + files.Count + ")");

            _buttonDownload.Text = files.Count > 1 ? Lookup("LABEL_DOWNLOAD_LATEST", "Download Latest") : Lookup("LABEL_DOWNLOAD", "Download");
            _buttonDownload.Enabled = latest != null;
            _buttonInstall.Text = files.Count > 1 ? Lookup("LABEL_INSTALL_LATEST", "Install Latest") : Lookup("LABEL_INSTALL", "Install");
            _buttonInstall.Enabled = latest != null && (connected || settings.InstallGameSavesToUsbDevice);

            _buttonFavorite.Text = IsFavorite ? Lookup("LABEL_REMOVE_FROM_FAVORITES", "Remove from Favorites") : Lookup("LABEL_ADD_TO_FAVORITES", "Add to Favorites");
            _buttonFavorite.Enabled = true;

            _downloadsList.RowStyles.Clear();
            _downloadsList.RowCount = files.Count;
            for (int i = 0; i < files.Count; i++)
            {
                // A separator sits above every download file except the first, when there is more than one.
                bool showSeparator = files.Count > 1 && i != 0;
                _ = _downloadsList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _downloadsList.Controls.Add(CreateDownloadRow(files[i], showSeparator, connected, settings.UseRelativeTimes), 0, i);
            }
        }

        /// <summary>
        /// Builds one download file row: a separator, the name, the stats, the actions, and a collapsed list of install paths.
        /// The per-row Install button is shown only when a console is connected, as the old row did.
        /// </summary>
        private Control CreateDownloadRow(DownloadFiles file, bool showSeparator, bool connected, bool useRelativeTimes)
        {
            int h = Font.Height;
            int pad = h / 2;
            List<string> installPaths = file.InstallPaths ?? new List<string>();
            int separatorHeight = showSeparator ? Math.Max(1, h / 12) : 0;
            int listHeight = h * Math.Min(Math.Max(1, installPaths.Count), 8) + pad;

            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            _ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = row.RowStyles.Add(new RowStyle(SizeType.Absolute, separatorHeight));
            _ = row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = row.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));

            Panel separator = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Gainsboro,
                Margin = Padding.Empty
            };

            Label name = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = _boldFont,
                Text = file.Name ?? string.Empty,
                Margin = new Padding(0, pad / 2, 0, pad / 2)
            };

            StatItem statFiles = CreateStat(new Padding(0, 0, pad, 0));
            StatItem statRegion = CreateStat(new Padding(0, 0, pad, 0));
            StatItem statVersion = CreateStat(new Padding(0, 0, pad, 0));
            StatItem statUpdated = CreateStat(Padding.Empty);
            statFiles.Title = Lookup("LABEL_FILES", "Files");
            statFiles.Value = installPaths.Count.ToString(CultureInfo.CurrentCulture);
            statRegion.Title = Lookup("LABEL_REGION", "Region");
            statRegion.Value = string.IsNullOrEmpty(file.Region) ? "-" : file.Region;
            statVersion.Title = Lookup("LABEL_VERSION", "Version");
            statVersion.Value = string.IsNullOrEmpty(file.Version) ? "-" : file.Version;
            statUpdated.Title = Lookup("LABEL_LAST_UPDATED", "Last Updated");
            statUpdated.Value = useRelativeTimes
                ? file.LastUpdated.Humanize()
                : file.LastUpdated.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);

            TableLayoutPanel stats = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
            {
                _ = stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            _ = stats.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stats.Controls.Add(statFiles, 0, 0);
            stats.Controls.Add(statRegion, 1, 0);
            stats.Controls.Add(statVersion, 2, 0);
            stats.Controls.Add(statUpdated, 3, 0);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            Button download = CreateRowButton(Lookup("LABEL_DOWNLOAD", "Download"));
            download.Click += (s, e) => RaiseTransfer(DownloadRequested, TransferType.DownloadGameSave, file);
            actions.Controls.Add(download);

            if (connected)
            {
                Button install = CreateRowButton(Lookup("LABEL_INSTALL", "Install"));
                install.Click += (s, e) => RaiseTransfer(InstallRequested, TransferType.InstallGameSave, file);
                actions.Controls.Add(install);
            }

            Button copyLink = CreateRowButton(Lookup("LABEL_COPY_LINK", "Copy Link"));
            copyLink.Enabled = !string.IsNullOrEmpty(file.Url);
            copyLink.Click += (s, e) => Clipboard.SetText(file.Url);
            actions.Controls.Add(copyLink);

            ListBox installFiles = new ListBox
            {
                Dock = DockStyle.Fill,
                Visible = false,
                ItemHeight = h,
                Margin = Padding.Empty
            };
            installFiles.Items.AddRange(installPaths.ToArray());

            bool expanded = false;
            Button toggleFiles = CreateRowButton(Lookup("LABEL_INSTALLATION_FILES", "Installation Files"));
            toggleFiles.Click += (s, e) =>
            {
                expanded = !expanded;
                installFiles.Visible = expanded;
                row.RowStyles[4].Height = expanded ? listHeight : 0F;
            };
            actions.Controls.Add(toggleFiles);

            row.Controls.Add(separator, 0, 0);
            row.Controls.Add(name, 0, 1);
            row.Controls.Add(stats, 0, 2);
            row.Controls.Add(actions, 0, 3);
            row.Controls.Add(installFiles, 0, 4);
            return row;
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            RaiseTransfer(DownloadRequested, TransferType.DownloadGameSave, LatestDownloadFile());
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            RaiseTransfer(InstallRequested, TransferType.InstallGameSave, LatestDownloadFile());
        }

        private void ButtonFavorite_Click(object sender, EventArgs e)
        {
            GameSaveItemData item = GameSaveItem;
            if (item == null)
            {
                return;
            }

            SettingsData settings = Settings ?? AppHost.Settings;
            if (IsFavorite)
            {
                _ = settings.FavoriteGameSaves.RemoveAll(x => IsSameFavorite(x, item));
                IsFavorite = false;
            }
            else
            {
                settings.FavoriteGameSaves.Add(new FavoriteItem
                {
                    CategoryType = CategoryType,
                    CategoryId = item.CategoryId,
                    ModId = item.Id,
                    Platform = item.GetPlatform()
                });
                IsFavorite = true;
            }

            _buttonFavorite.Text = IsFavorite
                ? Lookup("LABEL_REMOVE_FROM_FAVORITES", "Remove from Favorites")
                : Lookup("LABEL_ADD_TO_FAVORITES", "Add to Favorites");
        }

        private void ButtonReport_Click(object sender, EventArgs e)
        {
            ReportIssueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonHelp_Click(object sender, EventArgs e)
        {
            HelpRequested?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseTransfer(EventHandler<TransferEventArgs> handler, TransferType transferType, DownloadFiles file)
        {
            GameSaveItemData item = GameSaveItem;
            if (handler == null || item == null || file == null)
            {
                return;
            }

            handler(this, new TransferEventArgs(transferType, FindCategory(item), item, file));
        }

        private DownloadFiles LatestDownloadFile()
        {
            List<DownloadFiles> files = GameSaveItem?.DownloadFiles;
            return files == null || files.Count == 0 ? null : files[files.Count - 1];
        }

        private CategoryItem FindCategory(GameSaveItemData item)
        {
            return Categories == null || item == null ? null : Categories.GetCategoryById(item.CategoryId);
        }

        private bool IsConnected()
        {
            Func<bool> isConnected = IsConsoleConnected;
            return isConnected != null && isConnected();
        }

        private bool IsSameFavorite(FavoriteItem favorite, GameSaveItemData item)
        {
            return favorite.CategoryType == CategoryType
                && favorite.CategoryId == item.CategoryId
                && favorite.ModId == item.Id
                && favorite.Platform == item.GetPlatform();
        }

        private static string ExtraInformation()
        {
            StringBuilder extra = new StringBuilder("\n---------------------------------------------\n")
                .AppendLine("Important Information:\n")
                .AppendLine("- You must have at least one USB device connected to the console before installing the game save files.\n")
                .Append("- It's suggested to use 'Apollo Tool' (Homebrew Applications) for patching and resigning game save files directly on your console.");
            return extra.ToString();
        }

        /// <summary>
        /// Sizes the form 48 font heights wide and 32 tall, each clamped to 90% of the working area
        /// of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 48, (int)(work.Width * 0.9));
            int height = Math.Min(h * 32, (int)(work.Height * 0.9));
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

        private Button CreateRowButton(string text)
        {
            int pad = Font.Height / 4;
            return new Button
            {
                AutoSize = true,
                Text = text,
                Margin = new Padding(0, 0, pad, pad)
            };
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
