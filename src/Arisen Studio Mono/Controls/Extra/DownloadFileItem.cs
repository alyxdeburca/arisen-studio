using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ArisenStudio.Models.Database;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Arguments for <see cref="DownloadFileItem.LinkCopied"/>.
    /// </summary>
    public class LinkCopiedEventArgs : EventArgs
    {
        public LinkCopiedEventArgs(string url, bool success)
        {
            Url = url;
            Success = success;
        }

        /// <summary>
        /// The address that was put on the clipboard (or that was tried).
        /// </summary>
        public string Url { get; private set; }

        /// <summary>
        /// False when the address was empty or the clipboard was not available.
        /// </summary>
        public bool Success { get; private set; }
    }

    /// <summary>
    /// One row of a Downloads tab: file name, file count, last updated, region, version, an expandable list of
    /// install paths and Install / Download / Copy link buttons. Port of the DevExpress <c>DownloadFileItem</c>.
    /// The host sets the width (for example Dock = Top). The control does no installing or downloading: it raises
    /// <see cref="InstallClick"/> and <see cref="DownloadClick"/> and the host opens the transfer dialog. The
    /// host also decides whether the Install button shows (<see cref="ShowInstallButton"/>, the original hid it
    /// when no console was connected and the matching "install to USB" setting was off) and whether it reads
    /// Install or Uninstall (<see cref="IsInstalled"/>).
    /// </summary>
    public class DownloadFileItem : UserControl
    {
        private const int MaximumVisibleFileRows = 8;

        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private readonly TableLayoutPanel details = new TableLayoutPanel();
        private readonly FlowLayoutPanel buttons = new FlowLayoutPanel();
        private DownloadFiles downloadFiles;
        private Font nameFont;
        private bool useRelativeTimes;
        private bool isInstalled;
        private bool showInstallButton = true;
        private bool isExpanded;
        private bool showSeparator;
        private bool layingOut;

        public Label LabelName;
        public Label LabelFilesCount;
        public Label LabelHeaderLastUpdated;
        public Label LabelLastUpdated;
        public Label LabelHeaderRegion;
        public Label LabelRegion;
        public Label LabelHeaderVersion;
        public Label LabelVersion;
        public Label LabelInstallationFiles;
        public ListBox ListBoxInstallFiles;
        public Label Separator;
        public Button ButtonInstall;
        public Button ButtonDownload;
        public Button ButtonCopyLink;
        public Button ButtonExpand;

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

        public DownloadFileItem()
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            nameFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);

            Padding = new Padding(pad);
            Margin = new Padding(0);
            DoubleBuffered = true;

            LabelName = NewLabel(nameFont);
            LabelName.Anchor = AnchorStyles.Left;
            LabelFilesCount = NewLabel(null);
            LabelFilesCount.ForeColor = SystemColors.GrayText;

            LabelHeaderLastUpdated = NewLabel(null);
            LabelHeaderLastUpdated.ForeColor = SystemColors.GrayText;
            LabelHeaderLastUpdated.Text = "Last updated";
            LabelHeaderRegion = NewLabel(null);
            LabelHeaderRegion.ForeColor = SystemColors.GrayText;
            LabelHeaderRegion.Text = "Region";
            LabelHeaderVersion = NewLabel(null);
            LabelHeaderVersion.ForeColor = SystemColors.GrayText;
            LabelHeaderVersion.Text = "Version";
            LabelLastUpdated = NewLabel(null);
            LabelRegion = NewLabel(null);
            LabelVersion = NewLabel(null);

            details.AutoSize = true;
            details.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            details.ColumnCount = 3;
            details.RowCount = 2;
            details.Margin = new Padding(0, pad, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                details.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            details.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            LabelHeaderLastUpdated.Margin = new Padding(0, 0, pad * 3, 0);
            LabelLastUpdated.Margin = new Padding(0, 0, pad * 3, 0);
            LabelHeaderRegion.Margin = new Padding(0, 0, pad * 3, 0);
            LabelRegion.Margin = new Padding(0, 0, pad * 3, 0);
            details.Controls.Add(LabelHeaderLastUpdated, 0, 0);
            details.Controls.Add(LabelHeaderRegion, 1, 0);
            details.Controls.Add(LabelHeaderVersion, 2, 0);
            details.Controls.Add(LabelLastUpdated, 0, 1);
            details.Controls.Add(LabelRegion, 1, 1);
            details.Controls.Add(LabelVersion, 2, 1);

            ButtonInstall = NewButton("Install", ButtonInstall_Click);
            ButtonDownload = NewButton("Download", ButtonDownload_Click);
            ButtonCopyLink = NewButton("Copy link", ButtonCopyLink_Click);
            ButtonExpand = NewButton("Show files", ButtonExpand_Click);

            buttons.AutoSize = true;
            buttons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Anchor = AnchorStyles.Right;
            buttons.Margin = new Padding(pad, 0, 0, 0);
            buttons.Controls.Add(ButtonInstall);
            buttons.Controls.Add(ButtonDownload);
            buttons.Controls.Add(ButtonCopyLink);
            buttons.Controls.Add(ButtonExpand);

            LabelInstallationFiles = NewLabel(null);
            LabelInstallationFiles.Text = "Installation files";
            LabelInstallationFiles.Margin = new Padding(0, pad, 0, 2);
            LabelInstallationFiles.Visible = false;

            ListBoxInstallFiles = new ListBox
            {
                IntegralHeight = false,
                HorizontalScrollbar = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = Padding.Empty,
                Visible = false
            };

            Separator = new Label
            {
                AutoSize = false,
                Height = 2,
                BorderStyle = BorderStyle.Fixed3D,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, pad, 0, 0),
                Visible = false
            };

            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 2;
            layout.RowCount = 6;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 6; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            layout.Controls.Add(LabelName, 0, 0);
            layout.Controls.Add(buttons, 1, 0);
            layout.SetRowSpan(buttons, 2);
            layout.Controls.Add(LabelFilesCount, 0, 1);
            layout.Controls.Add(details, 0, 2);
            layout.SetColumnSpan(details, 2);
            layout.Controls.Add(LabelInstallationFiles, 0, 3);
            layout.SetColumnSpan(LabelInstallationFiles, 2);
            layout.Controls.Add(ListBoxInstallFiles, 0, 4);
            layout.SetColumnSpan(ListBoxInstallFiles, 2);
            layout.Controls.Add(Separator, 0, 5);
            layout.SetColumnSpan(Separator, 2);
            Controls.Add(layout);

            ApplyDownloadFiles();
            ApplyInstallState();

            ResumeLayout(true);
        }

        /// <summary>
        /// The download this row describes. Setting it refreshes every label and the install path list.
        /// </summary>
        public DownloadFiles DownloadFiles
        {
            get { return downloadFiles; }
            set
            {
                downloadFiles = value;
                ApplyDownloadFiles();
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
                ApplyDownloadFiles();
            }
        }

        /// <summary>
        /// True when this download is already installed: the button then reads Uninstall.
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
                PerformLayout();
            }
        }

        /// <summary>
        /// Whether the install path list is open.
        /// </summary>
        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                isExpanded = value;
                LabelInstallationFiles.Visible = value;
                ListBoxInstallFiles.Visible = value;
                ButtonExpand.Text = value ? "Hide files" : "Show files";
                PerformLayout();
            }
        }

        private static Label NewLabel(Font font)
        {
            Label label = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = Padding.Empty
            };
            if (font != null)
            {
                label.Font = font;
            }

            return label;
        }

        private Button NewButton(string text, EventHandler click)
        {
            Button button = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                UseVisualStyleBackColor = true,
                Text = text,
                Margin = new Padding(0, 0, 4, 0)
            };
            button.Click += click;
            return button;
        }

        private void ApplyDownloadFiles()
        {
            if (LabelName == null)
            {
                return;
            }

            List<string> paths = downloadFiles != null && downloadFiles.InstallPaths != null
                ? downloadFiles.InstallPaths
                : new List<string>();

            LabelName.Text = downloadFiles != null ? downloadFiles.Name ?? string.Empty : string.Empty;
            LabelFilesCount.Text = paths.Count + (paths.Count == 1 ? " file" : " files");
            LabelLastUpdated.Text = downloadFiles != null
                ? ExtraItemText.FormatTimeStamp(downloadFiles.LastUpdated, useRelativeTimes, "dd MMM yyyy")
                : "-";
            LabelRegion.Text = downloadFiles == null || string.IsNullOrEmpty(downloadFiles.Region) ? "-" : downloadFiles.Region;
            LabelVersion.Text = downloadFiles == null || string.IsNullOrEmpty(downloadFiles.Version) ? "-" : downloadFiles.Version;

            ListBoxInstallFiles.BeginUpdate();
            try
            {
                ListBoxInstallFiles.Items.Clear();
                foreach (string path in paths)
                {
                    ListBoxInstallFiles.Items.Add(path ?? string.Empty);
                }
            }
            finally
            {
                ListBoxInstallFiles.EndUpdate();
            }

            int rows = Math.Max(1, Math.Min(paths.Count, MaximumVisibleFileRows));
            ListBoxInstallFiles.Height = (rows * ListBoxInstallFiles.ItemHeight) + 4;
            ButtonExpand.Enabled = paths.Count > 0;
            ButtonDownload.Enabled = downloadFiles != null;
            ButtonCopyLink.Enabled = downloadFiles != null && !string.IsNullOrEmpty(downloadFiles.Url);
            PerformLayout();
        }

        private void ApplyInstallState()
        {
            if (ButtonInstall == null)
            {
                return;
            }

            ButtonInstall.Text = isInstalled ? "Uninstall" : "Install";
            ButtonInstall.Visible = showInstallButton;
            PerformLayout();
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
            string url = downloadFiles != null ? downloadFiles.Url : null;
            bool success = false;
            if (!string.IsNullOrEmpty(url))
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

        private void ButtonExpand_Click(object sender, EventArgs e)
        {
            IsExpanded = !isExpanded;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (LabelName == null || layingOut)
            {
                return;
            }

            int width = ClientSize.Width - Padding.Horizontal;
            if (width <= 0)
            {
                return;
            }

            layingOut = true;
            try
            {
                int nameWidth = Math.Max(50, width - buttons.GetPreferredSize(Size.Empty).Width - buttons.Margin.Horizontal);
                if (LabelName.MaximumSize.Width != nameWidth)
                {
                    LabelName.MaximumSize = new Size(nameWidth, 0);
                }

                int height = layout.GetPreferredSize(new Size(width, 0)).Height + Padding.Vertical;
                if (Height != height)
                {
                    Height = height;
                }
            }
            finally
            {
                layingOut = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && nameFont != null)
            {
                nameFont.Dispose();
                nameFont = null;
            }
        }
    }
}
