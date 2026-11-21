using ArisenStudio.Controls;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Shows the details of one package and offers Download, Install and FAQ.
    /// Stock WinForms port of the old DevExpress PackageDialog form.
    /// The dialog opens no other form: the buttons raise events and the host decides what to show.
    /// Set PackageItem, Settings and Language before the dialog is shown.
    /// </summary>
    internal class PackageDialog : Form
    {
        private readonly DetailLayout _layout;
        private readonly TableLayoutPanel _statsGrid;
        private readonly StatItem _statModifiedDate;
        private readonly StatItem _statFileSize;
        private readonly StatItem _statTitleId;
        private readonly StatItem _statContentId;
        private readonly StatItem _statSha256;
        private readonly Button _buttonDownload;
        private readonly Button _buttonInstall;
        private readonly Button _buttonFaq;

        public PackageDialog()
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

            // Stat rows: modified date, file size and title ID share the first row; content ID and SHA-256 take a full row each.
            _statModifiedDate = CreateStat(new Padding(0, 0, pad, pad));
            _statFileSize = CreateStat(new Padding(0, 0, pad, pad));
            _statTitleId = CreateStat(new Padding(0, 0, 0, pad));
            _statContentId = CreateStat(new Padding(0, 0, 0, pad));
            _statSha256 = CreateStat(new Padding(0, 0, 0, pad));

            _statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 3,
                Margin = new Padding(0, pad, 0, 0),
                Padding = Padding.Empty
            };
            _ = _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _ = _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _ = _statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int i = 0; i < 3; i++)
            {
                _ = _statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            _statsGrid.Controls.Add(_statModifiedDate, 0, 0);
            _statsGrid.Controls.Add(_statFileSize, 1, 0);
            _statsGrid.Controls.Add(_statTitleId, 2, 0);
            _statsGrid.Controls.Add(_statContentId, 0, 1);
            _statsGrid.SetColumnSpan(_statContentId, 3);
            _statsGrid.Controls.Add(_statSha256, 0, 2);
            _statsGrid.SetColumnSpan(_statSha256, 3);

            // Body: the stats grid in a padded container. The header separator already sits above it, so no top padding.
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            _ = body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(_statsGrid, 0, 0);

            // Shared shell: header with crumbs, name and separator; the body; and the action row ending with Close.
            _layout = new DetailLayout
            {
                Dock = DockStyle.Fill,
                CloseText = "Close"
            };
            _layout.CloseRequested += (s, e) => Close();
            _layout.Body = body;

            // AddAction places each button to the left of the ones before it, after Close.
            // Added FAQ, Install, Download, so the visual order is Download, Install, FAQ, Close.
            _buttonFaq = _layout.AddAction("FAQ", ButtonFaq_Click);
            _buttonInstall = _layout.AddAction("Install", ButtonInstall_Click);
            _buttonDownload = _layout.AddAction("Download", ButtonDownload_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The package to show. Read once when the dialog loads; change it before showing the dialog.
        /// </summary>
        public PackageItemData PackageItem { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings that control the date and size text, and whether Install is enabled for a USB install.
        /// Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The console profile. Kept for parity with the old static; this form does not read it.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// The categories data. Kept for parity with the old static; this form does not read it.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Install is enabled when this returns true or
        /// Settings.InstallPackagesToUsbDevice is set. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised when the Install button is clicked. The host starts the install.
        /// </summary>
        public event EventHandler InstallRequested;

        /// <summary>
        /// Raised when the Download button is clicked. The host starts the download.
        /// </summary>
        public event EventHandler DownloadRequested;

        /// <summary>
        /// Raised when the FAQ button is clicked. The host shows the packages FAQ.
        /// </summary>
        public event EventHandler FaqRequested;

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            ShowPackageDetails();
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

        private void ApplyLanguage()
        {
            _statModifiedDate.Title = Lookup("LABEL_MODIFIED_DATE", "Modified Date");
            _statFileSize.Title = Lookup("LABEL_FILE_SIZE", "File Size");
            _statTitleId.Title = Lookup("LABEL_TITLE_ID", "Title ID");
            _statContentId.Title = Lookup("LABEL_CONTENT_ID", "Content ID");
            _statSha256.Title = "SHA-256";

            _buttonDownload.Text = Lookup("LABEL_DOWNLOAD", "Download");
            _buttonInstall.Text = Lookup("LABEL_INSTALL", "Install");
            _buttonFaq.Text = "FAQ";
        }

        private void ShowPackageDetails()
        {
            SettingsData settings = Settings ?? AppHost.Settings;
            _buttonInstall.Enabled = IsInstallAvailable(settings);

            PackageItemData item = PackageItem;
            if (item == null)
            {
                _layout.SetCrumbs("Packages", string.Empty, string.Empty);
                _layout.SetName(string.Empty);
                _statModifiedDate.Value = string.Empty;
                _statFileSize.Value = string.Empty;
                _statTitleId.Value = string.Empty;
                _statContentId.Value = string.Empty;
                _statSha256.Value = string.Empty;
                return;
            }

            _layout.SetCrumbs("Packages", item.Category, item.Region);
            _layout.SetName(item.Name);

            _statModifiedDate.Value = ModifiedDateText(item, settings);
            _statFileSize.Value = FileSizeText(item, settings);
            _statTitleId.Value = item.TitleId ?? string.Empty;
            _statContentId.Value = item.ContentId ?? string.Empty;
            _statSha256.Value = item.IsSha256Missing ? Lookup("DATA_MISSING", "Missing") : item.Sha256;
        }

        private string ModifiedDateText(PackageItemData item, SettingsData settings)
        {
            if (item.IsDateMissing)
            {
                return Lookup("DATA_MISSING", "Missing");
            }

            DateTime modified = DateTime.Parse(item.ModifiedDate);
            return settings.UseRelativeTimes ? modified.Humanize() : modified.ToLongDateString();
        }

        private string FileSizeText(PackageItemData item, SettingsData settings)
        {
            if (item.IsSizeMissing)
            {
                return Lookup("DATA_MISSING", "Missing");
            }

            if (settings.UseFormattedFileSizes)
            {
                return long.Parse(item.Size).Bytes().Humanize("#.##");
            }

            return item.Size + " " + Lookup("LABEL_BYTES", "bytes");
        }

        private bool IsInstallAvailable(SettingsData settings)
        {
            Func<bool> isConnected = IsConsoleConnected;
            bool connected = isConnected != null && isConnected();
            return connected || settings.InstallPackagesToUsbDevice;
        }

        /// <summary>
        /// Sizes the form 34 font heights wide and to its content tall, each clamped to 90% of the working area
        /// of the screen it is on. The width is set first so the header wraps the name to it.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 34, (int)(work.Width * 0.9));
            ClientSize = new Size(width, ClientSize.Height);

            int height = Math.Min(Math.Max(_layout.PreferredSize.Height, h * 18), (int)(work.Height * 0.9));
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

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            DownloadRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            InstallRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonFaq_Click(object sender, EventArgs e)
        {
            FaqRequested?.Invoke(this, EventArgs.Empty);
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
