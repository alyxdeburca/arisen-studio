using ArisenStudio.Controls;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs.Details
{
    /// <summary>
    /// Shows the details of one custom mod: a description tab, an installation files tab and the Install and Edit actions.
    /// Stock WinForms port of the old DevExpress CustomModDialog form.
    /// The dialog opens no other form and never calls the console: Install, Uninstall and Edit raise events and the host decides what to do.
    /// Set CustomItem, Settings, ConsoleProfile, Language and IsConsoleConnected before the dialog is shown.
    /// </summary>
    internal class CustomModDialog : Form
    {
        private readonly DetailLayout _layout;
        private readonly StatItem _statPlatform;
        private readonly StatItem _statModType;
        private readonly StatItem _statVersion;
        private readonly StatItem _statCreatedBy;
        private readonly PageTabs _tabs;
        private readonly RichTextBox _description;
        private readonly Panel _filesScroll;
        private readonly TableLayoutPanel _filesList;
        private readonly Button _buttonInstall;
        private readonly Button _buttonEdit;

        public CustomModDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Custom Mod Details";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            // Stats: platform, mod type, version and created by share one row. Created by is never filled in (the old form never set it).
            _statPlatform = CreateStat(new Padding(0, 0, pad, 0));
            _statModType = CreateStat(new Padding(0, 0, pad, 0));
            _statVersion = CreateStat(new Padding(0, 0, pad, 0));
            _statCreatedBy = CreateStat(Padding.Empty);

            TableLayoutPanel statsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, pad, 0, pad),
                Padding = Padding.Empty
            };
            for (int i = 0; i < 4; i++)
            {
                _ = statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            }
            _ = statsGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsGrid.Controls.Add(_statPlatform, 0, 0);
            statsGrid.Controls.Add(_statModType, 1, 0);
            statsGrid.Controls.Add(_statVersion, 2, 0);
            statsGrid.Controls.Add(_statCreatedBy, 3, 0);

            // Description tab: a read-only, borderless rich text box. URLs in the text are detected and raise DescriptionLinkClicked.
            _description = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                Text = string.Empty
            };
            _description.LinkClicked += (s, e) => DescriptionLinkClicked?.Invoke(this, e);

            Panel descriptionPage = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(pad)
            };
            descriptionPage.Controls.Add(_description);

            // Installation files tab: one entry per file, with a separator line between entries.
            _filesList = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 0,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _filesList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _filesScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(pad)
            };
            _filesScroll.Controls.Add(_filesList);

            _tabs = new PageTabs
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };
            _ = _tabs.AddPage("description", "Description", descriptionPage);
            _ = _tabs.AddPage("files", "Installation Files", _filesScroll);

            // Body: the stats above the tabs. The tabs take the remaining height.
            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad, 0, pad, pad)
            };
            _ = body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
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

            // AddAction places each button to the left of the ones before it, after Close.
            // Edit is added first so it sits next to Close; Install is then placed to its left.
            _buttonEdit = _layout.AddAction("Edit", ButtonEdit_Click);
            _buttonInstall = _layout.AddAction("Install", ButtonInstall_Click);

            Controls.Add(_layout);
        }

        /// <summary>
        /// The custom mod to show. Read once when the dialog loads; change it before showing the dialog.
        /// </summary>
        public CustomItemData CustomItem { get; set; }

        /// <summary>
        /// The installed-mod record for this mod on the console profile, or null when it is not installed.
        /// Set when the dialog loads. Install raises UninstallCustomRequested when this is not null.
        /// </summary>
        public InstalledModInfo InstalledModInfo { get; set; }

        /// <summary>
        /// Settings that control the install button and the lookup of the installed record.
        /// Defaults to the shared application settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The console profile used to find the installed record. Passed in by the host; null means nothing is installed.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Install is enabled when this returns true or
        /// Settings.InstallHomebrewToUsbDevice is set. The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Raised when Install is clicked and the mod is not installed. The host starts the install transfer.
        /// </summary>
        public event EventHandler InstallCustomRequested;

        /// <summary>
        /// Raised when Install is clicked and the mod is already installed. The host starts the uninstall transfer.
        /// </summary>
        public event EventHandler UninstallCustomRequested;

        /// <summary>
        /// Raised when the Edit button is clicked. The old form did nothing here; the host decides.
        /// </summary>
        public event EventHandler EditRequested;

        /// <summary>
        /// Raised when a link in the description is clicked. The host opens it; this form does not start processes.
        /// </summary>
        public event EventHandler<LinkClickedEventArgs> DescriptionLinkClicked;

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            ShowCustomItem();
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
            _statPlatform.Title = Lookup("LABEL_SYSTEM_TYPE", "Platform");
            _statModType.Title = Lookup("LABEL_MOD_TYPE", "Mod Type");
            _statVersion.Title = Lookup("LABEL_VERSION", "Version");
            _statCreatedBy.Title = Lookup("LABEL_CREATED_BY", "Created By");

            _tabs.SetTitle("description", Lookup("LABEL_DESCRIPTION", "Description"));

            _buttonInstall.Text = Lookup("LABEL_INSTALL", "Install");
            _buttonEdit.Text = Lookup("LABEL_EDIT", "Edit");
        }

        private void ShowCustomItem()
        {
            SettingsData settings = Settings ?? AppHost.Settings;

            CustomItemData item = CustomItem;
            if (item == null)
            {
                _layout.SetCrumbs(string.Empty, string.Empty, string.Empty);
                _layout.SetName(string.Empty);
                _buttonInstall.Enabled = false;
                ShowFiles(new List<ListItem>());
                return;
            }

            InstalledModInfo = ConsoleProfile != null
                ? settings.GetInstalledMods(ConsoleProfile, item.Category, item.Id, true)
                : null;

            _layout.SetCrumbs(item.CategoryType.Humanize(), item.Category, string.Empty);
            _layout.SetName(item.Name);

            _statPlatform.Value = item.Platform.Humanize();
            _statModType.Value = item.ModType ?? string.Empty;
            _statVersion.Value = item.Version ?? string.Empty;
            _statCreatedBy.Value = string.Empty;

            _description.Text = string.IsNullOrWhiteSpace(item.Description)
                ? Lookup("NO_MORE_DETAILS", "No more details")
                : item.Description;

            _buttonInstall.Enabled = IsInstallAvailable(settings);

            ShowFiles(item.Files ?? new List<ListItem>());
        }

        private bool IsInstallAvailable(SettingsData settings)
        {
            Func<bool> isConnected = IsConsoleConnected;
            bool connected = isConnected != null && isConnected();
            return connected || settings.InstallHomebrewToUsbDevice;
        }

        /// <summary>
        /// Fills the installation files tab. Each file shows its local name and its install path.
        /// File size and modified date are not shown: reading them touches the disk on load, which this form does not do.
        /// </summary>
        private void ShowFiles(List<ListItem> files)
        {
            _tabs.SetTitle("files", Lookup("LABEL_INSTALLATION_FILES", "Installation Files") + " (" + files.Count + ")");

            foreach (Control control in _filesList.Controls)
            {
                control.Dispose();
            }
            _filesList.Controls.Clear();
            _filesList.RowStyles.Clear();

            int pad = Font.Height / 2;
            int separatorHeight = Math.Max(1, Font.Height / 12);
            int row = 0;

            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0)
                {
                    Panel separator = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Gainsboro,
                        Margin = Padding.Empty
                    };
                    _ = _filesList.RowStyles.Add(new RowStyle(SizeType.Absolute, separatorHeight));
                    _filesList.Controls.Add(separator, 0, row);
                    row++;
                }

                _ = _filesList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _filesList.Controls.Add(CreateFileEntry(files[i], pad), 0, row);
                row++;
            }

            _filesList.RowCount = row;
        }

        private Control CreateFileEntry(ListItem file, int pad)
        {
            TableLayoutPanel entry = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(0, pad / 2, 0, pad / 2)
            };
            _ = entry.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _ = entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = entry.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = entry.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            entry.Controls.Add(CreateFileLabel(Lookup("LABEL_LOCAL_FILE", "Local File") + ":", pad), 0, 0);
            entry.Controls.Add(CreateFileLabel(file == null ? string.Empty : file.Name ?? string.Empty, pad), 1, 0);
            entry.Controls.Add(CreateFileLabel(Lookup("LABEL_INSTALL_PATH", "Install Path") + ":", pad), 0, 1);
            entry.Controls.Add(CreateFileLabel(file == null ? string.Empty : file.Value ?? string.Empty, pad), 1, 1);

            return entry;
        }

        private static Label CreateFileLabel(string text, int pad)
        {
            return new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Text = text,
                Margin = new Padding(0, 0, pad / 2, pad / 4)
            };
        }

        /// <summary>
        /// Sizes the form 50 font heights wide and to its content plus the tab area tall, each clamped to 90% of the working area
        /// of the screen it is on. The width is set first so the header wraps the name to it.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(h * 50, (int)(work.Width * 0.9));
            ClientSize = new Size(width, ClientSize.Height);

            // The tab area gets a fixed share of the height. The layout's preferred height already includes the
            // tab control's small preferred size, so this overestimates by about one tab strip.
            int tabAreaHeight = h * 21;
            int height = Math.Min(_layout.PreferredSize.Height + tabAreaHeight, (int)(work.Height * 0.9));
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

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            if (InstalledModInfo == null)
            {
                InstallCustomRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                UninstallCustomRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ButtonEdit_Click(object sender, EventArgs e)
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
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
