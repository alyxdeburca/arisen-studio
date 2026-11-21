using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Arguments for <see cref="CustomFileItem.OpenFolderClick"/>.
    /// </summary>
    public class OpenFolderEventArgs : EventArgs
    {
        public OpenFolderEventArgs(string folderPath, bool fileExists)
        {
            FolderPath = folderPath;
            FileExists = fileExists;
        }

        /// <summary>
        /// The folder that holds the local file, or an empty string when it could not be worked out.
        /// </summary>
        public string FolderPath { get; private set; }

        /// <summary>
        /// False when the local file is missing. The original showed a "file not found" error in that case;
        /// the host does that here.
        /// </summary>
        public bool FileExists { get; private set; }
    }

    /// <summary>
    /// One row of a custom mod's file list: the local file, where it installs to, when it was last modified and
    /// how big it is, with an "Open folder" button. Port of the DevExpress <c>CustomFileItem</c>.
    /// The host sets the width (for example Dock = Top); the height is worked out by the control.
    /// The original read <c>MainWindow</c> (settings, status bar, message boxes) and took a <c>ListItem</c>. This
    /// control takes two plain strings (<see cref="LocalFilePath"/> and <see cref="InstallPath"/>), reads the file
    /// information itself, and reports through <see cref="OpenFolderClick"/> and <see cref="FileInfoFailed"/> so the
    /// host decides how to show messages. The original's Install and Download handlers were commented out, so
    /// they are not ported. Open folder does not start a process: the host does that (on Linux it is usually
    /// <c>xdg-open</c>) from <see cref="OpenFolderEventArgs.FolderPath"/>.
    /// </summary>
    public class CustomFileItem : UserControl
    {
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private string localFilePath;
        private string installPath;
        private Font valueFont;
        private bool useRelativeTimes;
        private bool useFormattedFileSizes = true;
        private bool showSeparator;

        public Label LabelHeaderLocalFile;
        public Label LabelLocalFile;
        public Label LabelHeaderInstallPath;
        public Label LabelInstallPath;
        public Label LabelHeaderLastModified;
        public Label LabelLastModified;
        public Label LabelHeaderSize;
        public Label LabelSize;
        public Label Separator;
        public Button ButtonOpenFolder;

        /// <summary>
        /// Raised when Open folder is clicked.
        /// </summary>
        public event EventHandler<OpenFolderEventArgs> OpenFolderClick;

        /// <summary>
        /// Raised when the file's date or size could not be read (access denied, bad path). The text is the
        /// message the original wrote to the status bar. The row shows "File missing" in both columns.
        /// </summary>
        public event EventHandler<CustomFileInfoFailedEventArgs> FileInfoFailed;

        public CustomFileItem()
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            valueFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);

            Padding = new Padding(pad);
            Margin = new Padding(0);
            DoubleBuffered = true;

            LabelHeaderLocalFile = NewLabel(null, false);
            LabelHeaderLocalFile.Text = "Local file:";
            LabelHeaderLocalFile.ForeColor = SystemColors.GrayText;
            LabelHeaderLocalFile.Margin = new Padding(0, 0, pad, 0);
            LabelLocalFile = NewLabel(valueFont, true);

            LabelHeaderInstallPath = NewLabel(null, false);
            LabelHeaderInstallPath.Text = "Install path:";
            LabelHeaderInstallPath.ForeColor = SystemColors.GrayText;
            LabelHeaderInstallPath.Margin = new Padding(0, pad / 2, pad, 0);
            LabelInstallPath = NewLabel(null, true);
            LabelInstallPath.Margin = new Padding(0, pad / 2, 0, 0);

            LabelHeaderLastModified = NewLabel(null, false);
            LabelHeaderLastModified.Text = "Last modified";
            LabelHeaderLastModified.ForeColor = SystemColors.GrayText;
            LabelHeaderLastModified.Margin = new Padding(pad, 0, pad * 2, 0);
            LabelLastModified = NewLabel(valueFont, false);
            LabelLastModified.Margin = new Padding(pad, 0, pad * 2, 0);

            LabelHeaderSize = NewLabel(null, false);
            LabelHeaderSize.Text = "Size";
            LabelHeaderSize.ForeColor = SystemColors.GrayText;
            LabelHeaderSize.Margin = new Padding(0, 0, pad, 0);
            LabelSize = NewLabel(valueFont, false);
            LabelSize.Margin = new Padding(0, 0, pad, 0);

            ButtonOpenFolder = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                UseVisualStyleBackColor = true,
                Text = "Open folder",
                Anchor = AnchorStyles.Right,
                Margin = Padding.Empty
            };
            ButtonOpenFolder.Click += ButtonOpenFolder_Click;

            Separator = new Label
            {
                AutoSize = false,
                Height = 2,
                BorderStyle = BorderStyle.Fixed3D,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, pad, 0, 0),
                Visible = false
            };

            // Columns: 0 header (local file / install path), 1 value (stretches), 2 last modified, 3 size, 4 button.
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 5;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(LabelHeaderLocalFile, 0, 0);
            layout.Controls.Add(LabelLocalFile, 1, 0);
            layout.Controls.Add(LabelHeaderInstallPath, 0, 1);
            layout.Controls.Add(LabelInstallPath, 1, 1);
            layout.Controls.Add(LabelHeaderLastModified, 2, 0);
            layout.Controls.Add(LabelLastModified, 2, 1);
            layout.Controls.Add(LabelHeaderSize, 3, 0);
            layout.Controls.Add(LabelSize, 3, 1);
            layout.Controls.Add(ButtonOpenFolder, 4, 0);
            layout.SetRowSpan(ButtonOpenFolder, 2);
            layout.Controls.Add(Separator, 0, 2);
            layout.SetColumnSpan(Separator, 5);
            Controls.Add(layout);

            ApplyFile();

            ResumeLayout(true);
            UpdateHeight();
        }

        /// <summary>
        /// Full path of the local file. Setting it re-reads the date and size.
        /// </summary>
        public string LocalFilePath
        {
            get { return localFilePath; }
            set
            {
                localFilePath = value;
                ApplyFile();
            }
        }

        /// <summary>
        /// Where the file is installed on the console (the <c>Value</c> of the original <c>ListItem</c>).
        /// </summary>
        public string InstallPath
        {
            get { return installPath; }
            set
            {
                installPath = value;
                ApplyFile();
            }
        }

        /// <summary>
        /// When true the last modified date reads as "5 days ago" for recent files; otherwise it is dd MMM yyyy.
        /// </summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set
            {
                useRelativeTimes = value;
                ApplyFile();
            }
        }

        /// <summary>
        /// When true the size reads as "1.5 MB"; otherwise it is the exact byte count.
        /// </summary>
        public bool UseFormattedFileSizes
        {
            get { return useFormattedFileSizes; }
            set
            {
                useFormattedFileSizes = value;
                ApplyFile();
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
        /// Formats a byte count: "512 bytes", "1.5 KB", "3.25 MB" (1024 based, two decimals at most).
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return unit == 0
                ? bytes.ToString(CultureInfo.CurrentCulture) + " bytes"
                : size.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[unit];
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
                // Fills the stretchy column and cuts long paths with "..." instead of growing the row.
                label.AutoEllipsis = true;
                label.Dock = DockStyle.Fill;
            }

            if (font != null)
            {
                label.Font = font;
            }

            return label;
        }

        private void ApplyFile()
        {
            if (LabelLocalFile == null)
            {
                return;
            }

            LabelLocalFile.Text = localFilePath ?? string.Empty;
            LabelInstallPath.Text = installPath ?? string.Empty;
            ButtonOpenFolder.Enabled = !string.IsNullOrEmpty(localFilePath);

            if (string.IsNullOrEmpty(localFilePath))
            {
                LabelLastModified.Text = "-";
                LabelSize.Text = "-";
                UpdateHeight();
                return;
            }

            try
            {
                FileInfo info = new FileInfo(localFilePath);
                if (info.Exists)
                {
                    LabelLastModified.Text = ExtraItemText.FormatTimeStamp(info.LastWriteTime, useRelativeTimes, "dd MMM yyyy");
                    LabelSize.Text = useFormattedFileSizes ? FormatFileSize(info.Length) : info.Length.ToString(CultureInfo.CurrentCulture) + " bytes";
                }
                else
                {
                    // Not an exception: the file was moved or deleted. Same text the original showed on failure.
                    LabelLastModified.Text = "File missing";
                    LabelSize.Text = "File missing";
                }
            }
            catch (Exception ex)
            {
                if (!(ex is SecurityException
                    || ex is UnauthorizedAccessException
                    || ex is PathTooLongException
                    || ex is NotSupportedException
                    || ex is ArgumentException
                    || ex is IOException))
                {
                    throw;
                }

                LabelLastModified.Text = "File missing";
                LabelSize.Text = "File missing";
                FileInfoFailed?.Invoke(this, new CustomFileInfoFailedEventArgs(
                    "Unable to fetch file information for file: " + localFilePath, ex));
            }

            UpdateHeight();
        }

        private void UpdateHeight()
        {
            if (layout.Controls.Count == 0)
            {
                return;
            }

            // Every row is a single line (the path labels do not wrap), so the preferred height does not
            // depend on the width and can be worked out without knowing it.
            int height = layout.GetPreferredSize(Size.Empty).Height + Padding.Vertical;
            if (Height != height)
            {
                Height = height;
            }
        }

        private void ButtonOpenFolder_Click(object sender, EventArgs e)
        {
            string folder = string.Empty;
            bool exists = false;
            if (!string.IsNullOrEmpty(localFilePath))
            {
                try
                {
                    folder = Path.GetDirectoryName(localFilePath) ?? string.Empty;
                    exists = File.Exists(localFilePath);
                }
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException || ex is PathTooLongException || ex is NotSupportedException || ex is SecurityException))
                    {
                        throw;
                    }

                    folder = string.Empty;
                    exists = false;
                }
            }

            OpenFolderClick?.Invoke(this, new OpenFolderEventArgs(folder, exists));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && valueFont != null)
            {
                valueFont.Dispose();
                valueFont = null;
            }
        }
    }

    /// <summary>
    /// Arguments for <see cref="CustomFileItem.FileInfoFailed"/>.
    /// </summary>
    public class CustomFileInfoFailedEventArgs : EventArgs
    {
        public CustomFileInfoFailedEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; private set; }

        public Exception Exception { get; private set; }
    }
}
