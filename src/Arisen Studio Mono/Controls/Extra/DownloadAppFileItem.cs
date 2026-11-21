using System;
using System.Drawing;
using System.Windows.Forms;
using ArisenStudio.Models.Database;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// One row of an application's Downloads tab: file name, playable version, last updated, version and
    /// Install / Download / Copy link buttons. Port of the DevExpress <c>DownloadAppFileItem</c>.
    /// The host sets the width (for example Dock = Top); the height is worked out by the control.
    /// The control does no installing or downloading: it raises <see cref="InstallClick"/> and
    /// <see cref="DownloadClick"/> and the host opens the transfer dialog. The original read
    /// <c>MainWindow</c>, <c>Settings</c> and the console connection; here the host decides instead through
    /// <see cref="ShowInstallButton"/> (hidden when no console is connected and USB install is off),
    /// <see cref="IsInstalled"/> (Install or Uninstall) and <see cref="UseRelativeTimes"/>.
    /// The original also needed the whole <c>AppItemData</c> and a <c>CategoryType</c>; only the title id is
    /// used by the control itself (to build the download address), so that is all it asks for
    /// (<see cref="TitleId"/>). The host keeps its own item data for the dialogs.
    /// Copy link raises <see cref="LinkCopied"/> (it reuses <see cref="LinkCopiedEventArgs"/> from
    /// DownloadFileItem.cs in the same folder) so the host can show its own "link copied" message.
    /// </summary>
    public class DownloadAppFileItem : UserControl
    {
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel();
        private AppItemFile appItemFile;
        private string titleId;
        private Font nameFont;
        private Font valueFont;
        private bool useRelativeTimes;
        private bool isInstalled;
        private bool showInstallButton = true;
        private bool showSeparator;

        public Label LabelName;
        public Label LabelPlayableVersions;
        public Label LabelHeaderLastUpdated;
        public Label LabelLastUpdated;
        public Label LabelHeaderVersion;
        public Label LabelVersion;
        public Label Separator;
        public Button ButtonInstall;
        public Button ButtonDownload;
        public Button ButtonCopyLink;

        /// <summary>
        /// Raised when Install (or Uninstall) is clicked. Check <see cref="IsInstalled"/> to see which one it was.
        /// </summary>
        public event EventHandler InstallClick;

        /// <summary>
        /// Raised when Download is clicked.
        /// </summary>
        public event EventHandler DownloadClick;

        /// <summary>
        /// Raised after Copy link tried to put the download address on the clipboard.
        /// </summary>
        public event EventHandler<LinkCopiedEventArgs> LinkCopied;

        public DownloadAppFileItem()
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            nameFont = new Font(Font, FontStyle.Bold);
            valueFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);

            Padding = new Padding(pad);
            Margin = new Padding(0);
            DoubleBuffered = true;

            LabelName = NewLabel(nameFont, true);
            LabelPlayableVersions = NewLabel(null, true);
            LabelPlayableVersions.ForeColor = SystemColors.GrayText;

            LabelHeaderLastUpdated = NewLabel(null, false);
            LabelHeaderLastUpdated.ForeColor = SystemColors.GrayText;
            LabelHeaderLastUpdated.Text = "Last updated";
            LabelHeaderVersion = NewLabel(null, false);
            LabelHeaderVersion.ForeColor = SystemColors.GrayText;
            LabelHeaderVersion.Text = "Version";
            LabelLastUpdated = NewLabel(valueFont, false);
            LabelVersion = NewLabel(valueFont, false);

            LabelHeaderLastUpdated.Margin = new Padding(pad, 0, pad * 2, 0);
            LabelLastUpdated.Margin = new Padding(pad, 0, pad * 2, 0);
            LabelHeaderVersion.Margin = new Padding(0, 0, pad, 0);
            LabelVersion.Margin = new Padding(0, 0, pad, 0);

            ButtonInstall = NewButton("Install", ButtonInstall_Click, pad);
            ButtonDownload = NewButton("Download", ButtonDownload_Click, pad);
            ButtonCopyLink = NewButton("Copy link", ButtonCopyLink_Click, pad);

            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Anchor = AnchorStyles.Right;
            buttons.Margin = Padding.Empty;
            buttons.Controls.Add(ButtonInstall);
            buttons.Controls.Add(ButtonDownload);
            buttons.Controls.Add(ButtonCopyLink);

            Separator = new Label
            {
                AutoSize = false,
                Height = 2,
                BorderStyle = BorderStyle.Fixed3D,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, pad, 0, 0),
                Visible = false
            };

            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 4;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(LabelName, 0, 0);
            layout.Controls.Add(LabelPlayableVersions, 0, 1);
            layout.Controls.Add(LabelHeaderLastUpdated, 1, 0);
            layout.Controls.Add(LabelLastUpdated, 1, 1);
            layout.Controls.Add(LabelHeaderVersion, 2, 0);
            layout.Controls.Add(LabelVersion, 2, 1);
            layout.Controls.Add(buttons, 3, 0);
            layout.SetRowSpan(buttons, 2);
            layout.Controls.Add(Separator, 0, 2);
            layout.SetColumnSpan(Separator, 4);
            Controls.Add(layout);

            ApplyAppItemFile();
            ApplyInstallState();

            ResumeLayout(true);
            UpdateHeight();
        }

        /// <summary>
        /// The file this row describes. Setting it refreshes every label.
        /// </summary>
        public AppItemFile AppItemFile
        {
            get { return appItemFile; }
            set
            {
                appItemFile = value;
                ApplyAppItemFile();
            }
        }

        /// <summary>
        /// The title id of the application. Used to build the download address for Copy link.
        /// </summary>
        public string TitleId
        {
            get { return titleId; }
            set
            {
                titleId = value;
                ApplyAppItemFile();
            }
        }

        /// <summary>
        /// When true the last updated date reads as "5 days ago" for recent entries; otherwise it is dd MMM yyyy.
        /// </summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set
            {
                useRelativeTimes = value;
                ApplyAppItemFile();
            }
        }

        /// <summary>
        /// True when this file is already installed: the button then reads Uninstall.
        /// </summary>
        public bool IsInstalled
        {
            get { return isInstalled; }
            set
            {
                isInstalled = value;
                ApplyInstallState();
            }
        }

        /// <summary>
        /// Whether the Install/Uninstall button is visible. The host turns it off when installing is not possible.
        /// </summary>
        public bool ShowInstallButton
        {
            get { return showInstallButton; }
            set
            {
                showInstallButton = value;
                ApplyInstallState();
            }
        }

        /// <summary>
        /// Shows a line under the row, so stacked rows read as a list.
        /// </summary>
        public bool ShowSeparator
        {
            get { return showSeparator; }
            set
            {
                showSeparator = value;
                Separator.Visible = value;
                UpdateHeight();
            }
        }

        /// <summary>
        /// The address Copy link would put on the clipboard, or an empty string when there is no file or title id.
        /// </summary>
        public string FileUrl
        {
            get
            {
                if (appItemFile == null || string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(appItemFile.Version))
                {
                    return string.Empty;
                }

                return appItemFile.GetFileUrl(titleId);
            }
        }

        private static Label NewLabel(Font font, bool fillWidth)
        {
            Label label = new Label
            {
                AutoSize = !fillWidth,
                UseMnemonic = false,
                Margin = Padding.Empty
            };
            if (fillWidth)
            {
                // Fills the stretchy first column and cuts long text with "..." instead of growing the row.
                label.AutoEllipsis = true;
                label.Dock = DockStyle.Fill;
            }

            if (font != null)
            {
                label.Font = font;
            }

            return label;
        }

        private static Button NewButton(string text, EventHandler click, int pad)
        {
            Button button = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                UseVisualStyleBackColor = true,
                Text = text,
                Margin = new Padding(0, 0, pad / 2, 0)
            };
            button.Click += click;
            return button;
        }

        private void ApplyAppItemFile()
        {
            if (LabelName == null)
            {
                return;
            }

            if (appItemFile == null)
            {
                LabelName.Text = string.Empty;
                LabelPlayableVersions.Text = string.Empty;
                LabelLastUpdated.Text = "-";
                LabelVersion.Text = "-";
            }
            else
            {
                LabelName.Text = appItemFile.Name ?? string.Empty;
                LabelPlayableVersions.Text = "Playable version: "
                    + (string.IsNullOrEmpty(appItemFile.PlayableVersion) ? "-" : appItemFile.PlayableVersion);
                LabelLastUpdated.Text = ExtraItemText.FormatTimeStamp(appItemFile.LastUpdated, useRelativeTimes, "dd MMM yyyy");
                LabelVersion.Text = string.IsNullOrEmpty(appItemFile.Version) ? "-" : appItemFile.Version;
            }

            ButtonDownload.Enabled = appItemFile != null;
            ButtonCopyLink.Enabled = FileUrl.Length > 0;
            UpdateHeight();
        }

        private void ApplyInstallState()
        {
            if (ButtonInstall == null)
            {
                return;
            }

            ButtonInstall.Text = isInstalled ? "Uninstall" : "Install";
            ButtonInstall.Visible = showInstallButton;
            UpdateHeight();
        }

        private void UpdateHeight()
        {
            if (layout.Controls.Count == 0)
            {
                return;
            }

            // Every row is a single line (the name labels do not wrap), so the preferred height does not
            // depend on the width and can be worked out without knowing it.
            int height = layout.GetPreferredSize(Size.Empty).Height + Padding.Vertical;
            if (Height != height)
            {
                Height = height;
            }
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            InstallClick?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            DownloadClick?.Invoke(this, EventArgs.Empty);
        }

        private void ButtonCopyLink_Click(object sender, EventArgs e)
        {
            string url = FileUrl;
            bool success = false;
            if (url.Length > 0)
            {
                try
                {
                    Clipboard.SetText(url);
                    success = true;
                }
                catch (Exception ex)
                {
                    // The clipboard can be locked by another process or missing on a bare X11 session.
                    if (!(ex is System.Runtime.InteropServices.ExternalException
                        || ex is System.Threading.ThreadStateException
                        || ex is ArgumentException
                        || ex is InvalidOperationException
                        || ex is NotSupportedException))
                    {
                        throw;
                    }
                }
            }

            LinkCopied?.Invoke(this, new LinkCopiedEventArgs(url, success));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (nameFont != null)
                {
                    nameFont.Dispose();
                    nameFont = null;
                }

                if (valueFont != null)
                {
                    valueFont.Dispose();
                    valueFont = null;
                }
            }
        }
    }
}
