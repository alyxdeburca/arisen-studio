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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Shows the details of one homebrew application: its stats, description and downloadable files.
    /// Stock WinForms port of the old DevExpress ApplicationDialog form.
    /// The dialog opens no other form and never calls the console: Download, Install and Uninstall are
    /// passed to the host through TransferRequested, and the report and help buttons raise events.
    /// Set AppItem, CategoryType, Settings, ConsoleProfile, Categories and Language before the dialog is shown.
    /// </summary>
    internal class ApplicationDialog : Form
    {
        private readonly DetailLayout _layout;
        private readonly PageTabs _tabs;
        private readonly StatItem _statTitleId;
        private readonly StatItem _statFwVersions;
        private readonly StatItem _statVersion;
        private readonly StatItem _statCreatedBy;
        private readonly StatItem _statSubmittedBy;
        private readonly StatItem _statLastUpdated;
        private readonly TextBox _description;
        private readonly TableLayoutPanel _downloadList;
        private readonly Button _buttonDownload;
        private readonly Button _buttonInstall;
        private readonly Button _buttonFavorite;
        private readonly Font _boldFont;
        private bool IsFavorite;

        public ApplicationDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Homebrew Details";
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

            // Stats: three columns, two rows. The right-hand column and the bottom row carry no extra margin.
            _statTitleId = CreateStat(new Padding(0, 0, pad, pad));
            _statFwVersions = CreateStat(new Padding(0, 0, pad, pad));
            _statVersion = CreateStat(new Padding(0, 0, 0, pad));
            _statCreatedBy = CreateStat(new Padding(0, 0, pad, 0));
            _statSubmittedBy = CreateStat(new Padding(0, 0, pad, 0));
            _statLastUpdated = CreateStat(Padding.Empty);

            TableLayoutPanel statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad, pad, pad, 0)
            };
            for (int i = 0; i < 3; i++)
            {
                _ = statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsGrid.Controls.Add(_statTitleId, 0, 0);
            statsGrid.Controls.Add(_statFwVersions, 1, 0);
            statsGrid.Controls.Add(_statVersion, 2, 0);
            statsGrid.Controls.Add(_statCreatedBy, 0, 1);
            statsGrid.Controls.Add(_statSubmittedBy, 1, 1);
            statsGrid.Controls.Add(_statLastUpdated, 2, 1);

            // Description: a read-only multi-line text box. It scrolls itself, so no scroll handling is needed.
            _description = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                TabStop = false
            };

            // Downloads: one row per file, in a container that scrolls when the rows do not fit.
            _downloadList = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _downloadList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _tabs = new PageTabs
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(pad, 0, pad, pad)
            };
            _ = _tabs.AddPage("description", "Description", _description);
            _ = _tabs.AddPage("downloads", "Downloads", _downloadList);

            // Body: the stats above the tabs. The tab area has a height from the font, so the form has a sensible size.
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = body.RowStyles.Add(new RowStyle(SizeType.Absolute, h * 18));
            body.Controls.Add(statsGrid, 0, 0);
            body.Controls.Add(_tabs, 0, 1);

            // Shared shell: header with crumbs and name, the body, and the action row ending with Close.
            _layout = new DetailLayout
            {
                Dock = DockStyle.Fill,
                CloseText = "Close"
            };
            _layout.CloseRequested += (s, e) => Close();
            _layout.Body = body;

            // AddAction places each button left of the ones added before it, after Close.
            // Added Help, Report, Favorite, Install, Download, so the visual order is Download, Install, Favorite, Report Issue, Help & Support, Close.
            _ = _layout.AddAction("Help && Support", ButtonHelpSupport_Click);
            _ = _layout.AddAction("Report Issue", ButtonReportIssue_Click);
            _buttonFavorite = _layout.AddAction("Add to Favorites", ButtonFavorite_Click);
            _buttonInstall = _layout.AddAction("Install", ButtonInstall_Click);
            _buttonDownload = _layout.AddAction("Download", ButtonDownload_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The application to show. Read once when the dialog loads; change it before showing the dialog.
        /// </summary>
        public AppItemData AppItem { get; set; }

        /// <summary>
        /// The category type of the item. The Install button of a file is only offered for homebrew
        /// when no console is connected and InstallHomebrewToUsbDevice is set.
        /// </summary>
        public CategoryType CategoryType { get; set; }

        /// <summary>
        /// The installed-mod record for this item on the console profile, or null. Set when the dialog loads.
        /// </summary>
        public InstalledModInfo InstalledModInfo { get; set; }

        /// <summary>
        /// Settings for relative times, file sizes, favorites and the USB install option.
        /// Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The console profile used to find the installed-mod record. Passed in by the host; null means no profile.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// The categories data, used for the category title in the header and for the category of a transfer.
        /// Null (the default) shows the category id in place of the title.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Returns true when a console is connected. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Called when a Download, Install or Uninstall button is clicked. The host starts the transfer.
        /// Arguments: the transfer type, the application, and the file to transfer.
        /// The default does nothing.
        /// </summary>
        public Action<TransferType, AppItemData, AppItemFile> TransferRequested { get; set; } = (transferType, appItem, appFile) => { };

        /// <summary>
        /// Raised when the Report Issue button is clicked. The host opens the report page.
        /// </summary>
        public event EventHandler ReportIssueRequested;

        /// <summary>
        /// Raised when the Help &amp; Support button is clicked. The host opens the help page.
        /// </summary>
        public event EventHandler HelpRequested;

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            ShowAppDetails();
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

        private SettingsData CurrentSettings
        {
            get { return Settings ?? AppHost.Settings; }
        }

        private void ApplyLanguage()
        {
            _statTitleId.Title = Lookup("LABEL_TITLE_ID", "Title ID");
            _statFwVersions.Title = Lookup("LABEL_SYSTEM_TYPE", "Firmware Versions");
            _statVersion.Title = Lookup("LABEL_VERSION", "Version");
            _statCreatedBy.Title = Lookup("LABEL_CREATED_BY", "Created By");
            _statSubmittedBy.Title = Lookup("LABEL_SUBMITTED_BY", "Submitted By");
            _statLastUpdated.Title = Lookup("LABEL_LAST_UPDATED", "Last Updated");

            _tabs.SetTitle("description", Lookup("LABEL_DESCRIPTION", "Description"));

            _buttonFavorite.Text = Lookup("LABEL_ADD_TO_FAVORITES", "Add to Favorites");
        }

        private void ShowAppDetails()
        {
            if (AppItem == null)
            {
                _layout.SetCrumbs("Applications", string.Empty, string.Empty);
                _layout.SetName(string.Empty);
                _buttonDownload.Enabled = false;
                _buttonInstall.Enabled = false;
                _buttonFavorite.Enabled = false;
                return;
            }

            SettingsData settings = CurrentSettings;
            InstalledModInfo = FindInstalledMod();
            IsFavorite = settings.FavoriteMods.Exists(IsThisFavorite);

            _layout.SetCrumbs("Applications", CategoryTitle(), string.Empty);
            _layout.SetName(AppItem.Name ?? string.Empty);

            _statTitleId.Value = AppItem.TitleId ?? string.Empty;
            _statFwVersions.Value = string.IsNullOrEmpty(AppItem.PlayableVersion) ? string.Empty : string.Join("/", AppItem.FirmwareVersions);
            _statVersion.Value = string.IsNullOrEmpty(AppItem.Version) ? string.Empty : Escape(string.Join(" & ", AppItem.Versions));
            _statCreatedBy.Value = string.IsNullOrWhiteSpace(AppItem.CreatedBy) ? "Unknown" : Escape(AppItem.CreatedBy);
            _statSubmittedBy.Value = Escape(AppItem.SubmittedBy);

            List<AppItemFile> files = AppItem.DownloadFiles ?? new List<AppItemFile>();
            _statLastUpdated.Value = files.Count == 0 ? string.Empty : LastUpdatedText(files[0], settings);

            _description.Text = string.IsNullOrWhiteSpace(AppItem.Description)
                ? Lookup("NO_MORE_DETAILS", "No more details")
                : NormaliseLineBreaks(AppItem.Description);

            _tabs.SetTitle("downloads", Lookup("LABEL_DOWNLOADS", "Downloads") + " (" + files.Count + ")");

            bool hasFiles = files.Count > 0;
            _buttonDownload.Text = files.Count > 1 ? Lookup("LABEL_DOWNLOAD_LATEST", "Download Latest") : Lookup("LABEL_DOWNLOAD", "Download");
            _buttonInstall.Text = files.Count > 1 ? Lookup("LABEL_INSTALL_LATEST", "Install Latest") : Lookup("LABEL_INSTALL", "Install");
            _buttonDownload.Enabled = hasFiles;
            _buttonInstall.Enabled = hasFiles && (ConsoleConnected() || settings.InstallHomebrewToUsbDevice);
            UpdateFavoriteText();

            _downloadList.RowCount = 0;
            _downloadList.Controls.Clear();
            _downloadList.RowStyles.Clear();
            for (int i = 0; i < files.Count; i++)
            {
                bool showSeparator = files.Count > 1 && i > 0;
                AddRow(_downloadList, CreateDownloadRow(files[i], showSeparator, settings), new RowStyle(SizeType.AutoSize));
            }
        }

        /// <summary>
        /// One downloadable file: an optional separator, the name, last updated and version, the playable versions, and the actions.
        /// </summary>
        private TableLayoutPanel CreateDownloadRow(AppItemFile file, bool showSeparator, SettingsData settings)
        {
            int h = Font.Height;
            int pad = h / 2;

            TableLayoutPanel row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            _ = row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            if (showSeparator)
            {
                Panel separator = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Gainsboro,
                    Margin = new Padding(0, 0, 0, pad)
                };
                AddRow(row, separator, new RowStyle(SizeType.Absolute, Math.Max(1, h / 12)));
            }

            Label name = new Label
            {
                AutoSize = true,
                Font = _boldFont,
                Text = Escape(file.Name),
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            AddRow(row, name, new RowStyle(SizeType.AutoSize));

            TableLayoutPanel details = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, pad / 2),
                Padding = Padding.Empty
            };
            _ = details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            _ = details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            _ = details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            details.Controls.Add(new StatItem
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, pad, 0),
                Title = Lookup("LABEL_LAST_UPDATED", "Last Updated"),
                Value = LastUpdatedText(file, settings)
            }, 0, 0);
            details.Controls.Add(new StatItem
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Title = Lookup("LABEL_VERSION", "Version"),
                Value = string.IsNullOrEmpty(file.Version) ? "-" : Escape(file.Version)
            }, 1, 0);
            AddRow(row, details, new RowStyle(SizeType.AutoSize));

            Label playable = new Label
            {
                AutoSize = true,
                Text = Lookup("LABEL_PLAYABLE_VERSION", "Playable Versions") + ": " + Escape(file.PlayableVersion),
                Margin = new Padding(0, 0, 0, pad)
            };
            AddRow(row, playable, new RowStyle(SizeType.AutoSize));

            bool installed = FindInstalledMod() != null;
            bool installAvailable = ConsoleConnected() || (CategoryType == CategoryType.Homebrew && settings.InstallHomebrewToUsbDevice);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(CreateFileButton(Lookup("LABEL_DOWNLOAD", "Download"), (s, e) => RaiseTransfer(TransferType.DownloadApplication, file)));
            if (installAvailable)
            {
                actions.Controls.Add(installed
                    ? CreateFileButton(Lookup("LABEL_UNINSTALL", "Uninstall"), (s, e) => RaiseTransfer(TransferType.UninstallApplication, file))
                    : CreateFileButton(Lookup("LABEL_INSTALL", "Install"), (s, e) => RaiseTransfer(TransferType.InstallApplication, file)));
            }
            actions.Controls.Add(CreateFileButton(Lookup("LABEL_COPY_LINK", "Copy Link"), (s, e) => CopyLink(file)));
            AddRow(row, actions, new RowStyle(SizeType.AutoSize));

            return row;
        }

        private static void AddRow(TableLayoutPanel table, Control control, RowStyle style)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            _ = table.RowStyles.Add(style);
            table.Controls.Add(control, 0, row);
        }

        private Button CreateFileButton(string text, EventHandler onClick)
        {
            int h = Font.Height;
            Button button = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 4, 0),
                Margin = new Padding(0, 0, h / 4, 0),
                Text = text
            };
            button.Click += onClick;
            return button;
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            AppItemFile last = LastFile();
            if (last != null)
            {
                RaiseTransfer(TransferType.DownloadApplication, last);
            }
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            AppItemFile last = LastFile();
            if (last != null)
            {
                RaiseTransfer(TransferType.InstallApplication, last);
            }
        }

        private void ButtonReportIssue_Click(object sender, EventArgs e)
        {
            ReportIssueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonHelpSupport_Click(object sender, EventArgs e)
        {
            HelpRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonFavorite_Click(object sender, EventArgs e)
        {
            if (AppItem == null)
            {
                return;
            }

            SettingsData settings = CurrentSettings;
            if (IsFavorite)
            {
                _ = settings.FavoriteMods.RemoveAll(IsThisFavorite);
                IsFavorite = false;
            }
            else
            {
                settings.FavoriteMods.Add(new FavoriteItem { CategoryType = CategoryType, CategoryId = AppItem.CategoryId, ModId = AppItem.Id, Platform = AppItem.GetPlatform() });
                IsFavorite = true;
            }
            UpdateFavoriteText();
        }

        private void RaiseTransfer(TransferType transferType, AppItemFile file)
        {
            Action<TransferType, AppItemData, AppItemFile> handler = TransferRequested;
            if (handler != null && AppItem != null)
            {
                handler(transferType, AppItem, file);
            }
        }

        private void CopyLink(AppItemFile file)
        {
            if (AppItem == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(file.GetFileUrl(AppItem.TitleId));
            }
            catch (ExternalException)
            {
                // The clipboard is busy in another program. Nothing else to do.
            }
        }

        private AppItemFile LastFile()
        {
            List<AppItemFile> files = AppItem == null ? null : AppItem.DownloadFiles;
            return files == null || files.Count == 0 ? null : files[files.Count - 1];
        }

        private InstalledModInfo FindInstalledMod()
        {
            if (ConsoleProfile == null || AppItem == null)
            {
                return null;
            }

            return CurrentSettings.GetInstalledMods(ConsoleProfile, AppItem.CategoryId, AppItem.Id, false);
        }

        private bool IsThisFavorite(FavoriteItem item)
        {
            return item.CategoryType == CategoryType
                && item.CategoryId == AppItem.CategoryId
                && item.ModId == AppItem.Id
                && item.Platform == AppItem.GetPlatform();
        }

        private void UpdateFavoriteText()
        {
            _buttonFavorite.Text = IsFavorite
                ? Lookup("LABEL_REMOVE_FROM_FAVORITES", "Remove from Favorites")
                : Lookup("LABEL_ADD_TO_FAVORITES", "Add to Favorites");
        }

        private string CategoryTitle()
        {
            if (Categories == null || Categories.Categories == null)
            {
                return AppItem.CategoryId ?? string.Empty;
            }

            return AppItem.GetCategory(Categories).Title ?? string.Empty;
        }

        private string LastUpdatedText(AppItemFile file, SettingsData settings)
        {
            return settings.UseRelativeTimes
                ? file.LastUpdated.Humanize()
                : file.LastUpdated.ToString("MM/dd/yyyy", CultureInfo.CurrentCulture);
        }

        private bool ConsoleConnected()
        {
            Func<bool> isConnected = IsConsoleConnected;
            return isConnected != null && isConnected();
        }

        /// <summary>
        /// Sizes the form 48 font heights wide and to its content tall, each clamped to 90% of the working area
        /// of the screen it is on. The width is set first so the header wraps the name to it.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 48, (int)(work.Width * 0.9));
            ClientSize = new Size(width, ClientSize.Height);

            int height = Math.Min(Math.Max(_layout.PreferredSize.Height, h * 32), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private static StatItem CreateStat(Padding margin)
        {
            return new StatItem
            {
                Dock = DockStyle.Fill,
                Margin = margin
            };
        }

        /// <summary>
        /// Doubles "&amp;" so a label with mnemonics shows it literally.
        /// </summary>
        private static string Escape(string text)
        {
            return (text ?? string.Empty).Replace("&", "&&");
        }

        /// <summary>
        /// Uses CRLF line breaks, which the multi-line text box needs to show new lines.
        /// </summary>
        private static string NormaliseLineBreaks(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\n", "\r\n");
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
