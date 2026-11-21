using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ArisenStudio.Extensions;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// One release entry: version, change log body and release timestamp.
    /// </summary>
    public class ChangeLogItem : UserControl
    {
        private const int MaximumLinks = 30;

        private static readonly Regex UrlPattern = new Regex("https?://[^\\s<>\"']+", RegexOptions.Compiled);

        private readonly DateTime timeStamp;
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private Font versionFont;
        private bool useRelativeTimes;
        private bool layingOut;

        public Label LabelVersion;
        public LinkLabel LabelChangeLog;
        public Label LabelTimeStamp;

        /// <summary>
        /// Raised when a URL inside the change log body is clicked. The control does not open links itself.
        /// </summary>
        public event LinkClickedEventHandler HyperlinkClick;

        public ChangeLogItem(string version, string changeLog, DateTime timeStamp)
        {
            SuspendLayout();

            this.timeStamp = timeStamp;
            Font = SystemFonts.MessageBoxFont;
            versionFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);

            Padding = new Padding(pad);
            Margin = new Padding(0, 0, 0, pad);
            DoubleBuffered = true;
            ResizeRedraw = true;

            LabelVersion = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = versionFont,
                Margin = Padding.Empty
            };

            LabelTimeStamp = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = Padding.Empty
            };

            LabelChangeLog = new LinkLabel
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            LabelChangeLog.LinkClicked += LabelChangeLog_LinkClicked;

            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 2;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(LabelVersion, 0, 0);
            layout.Controls.Add(LabelTimeStamp, 1, 0);
            layout.Controls.Add(LabelChangeLog, 0, 1);
            layout.SetColumnSpan(LabelChangeLog, 2);
            Controls.Add(layout);

            LabelVersion.Text = version ?? string.Empty;
            SetLinkText(LabelChangeLog, changeLog);
            LabelTimeStamp.Text = FormatTimeStamp();

            ResumeLayout(true);
        }

        /// <summary>
        /// When true the timestamp reads as "5 minutes ago" for recent entries; otherwise it is shown as MM/dd/yyyy.
        /// </summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set
            {
                useRelativeTimes = value;
                if (LabelTimeStamp != null)
                {
                    LabelTimeStamp.Text = FormatTimeStamp();
                }
            }
        }

        private void LabelChangeLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string;
            if (!string.IsNullOrEmpty(url))
            {
                HyperlinkClick?.Invoke(this, new LinkClickedEventArgs(url));
            }
        }

        private static void SetLinkText(LinkLabel label, string text)
        {
            text = text ?? string.Empty;
            label.Links.Clear();
            label.Text = text;

            int count = 0;
            foreach (Match match in UrlPattern.Matches(text))
            {
                if (count >= MaximumLinks)
                {
                    break;
                }

                string url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
                if (url.Length == 0)
                {
                    continue;
                }

                try
                {
                    label.Links.Add(match.Index, url.Length, url);
                    count++;
                }
                catch (ArgumentException)
                {
                    // A bad range must never escape the constructor or a paint pass.
                }
            }
        }

        private string FormatTimeStamp()
        {
            string absolute = timeStamp.ToString("MM/dd/yyyy", CultureInfo.CurrentCulture);
            if (!useRelativeTimes)
            {
                return absolute;
            }

            DateTime now = timeStamp.Kind == DateTimeKind.Local ? DateTime.Now : DateTime.UtcNow;
            TimeSpan span = now - timeStamp;
            if (span < TimeSpan.Zero || span.TotalDays >= 30)
            {
                return absolute;
            }

            if (span.TotalMinutes < 1)
            {
                return "just now";
            }

            if (span.TotalHours < 1)
            {
                return Plural((int)span.TotalMinutes, "minute");
            }

            if (span.TotalDays < 1)
            {
                return Plural((int)span.TotalHours, "hour");
            }

            return Plural((int)span.TotalDays, "day");
        }

        private static string Plural(int count, string unit)
        {
            return count == 1 ? "1 " + unit + " ago" : count + " " + unit + "s ago";
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (LabelChangeLog == null || layingOut)
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
                if (LabelChangeLog.MaximumSize.Width != width)
                {
                    LabelChangeLog.MaximumSize = new Size(width, 0);
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

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath path = GraphicExtensions.GetRoundedRectanglePath(ClientRectangle, 4))
            using (Brush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && versionFont != null)
            {
                versionFont.Dispose();
                versionFont = null;
            }
        }
    }
}
