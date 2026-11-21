using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// One news feed entry: title, message text and timestamp. The host sets the width (for example Dock = Top).
    /// </summary>
    public class NewsItem : UserControl
    {
        private const int MaximumLinks = 30;

        private static readonly Regex UrlPattern = new Regex("https?://[^\\s<>\"']+", RegexOptions.Compiled);
        private static readonly Regex LineBreakPattern = new Regex("<br\\s*/?>|</p\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagPattern = new Regex("<[^>]+>", RegexOptions.Compiled);

        private readonly DateTime timeStamp;
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private Font titleFont;
        private bool useRelativeTimes;
        private bool layingOut;

        public Label LabelTitle;
        public LinkLabel LabelMessage;
        public Label LabelTimeStamp;

        /// <summary>
        /// Raised when a URL inside the message is clicked. The control does not open links itself.
        /// </summary>
        public event LinkClickedEventHandler HyperlinkClick;

        public NewsItem(string title, string message, DateTime timeStamp)
        {
            SuspendLayout();

            this.timeStamp = timeStamp;
            Font = SystemFonts.MessageBoxFont;
            titleFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);

            Padding = new Padding(pad);
            Margin = new Padding(0, 0, 0, pad);
            DoubleBuffered = true;

            LabelTitle = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = titleFont,
                Margin = Padding.Empty
            };

            LabelTimeStamp = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = Padding.Empty
            };

            LabelMessage = new LinkLabel
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            LabelMessage.LinkClicked += LabelMessage_LinkClicked;

            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 2;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(LabelTitle, 0, 0);
            layout.Controls.Add(LabelTimeStamp, 1, 0);
            layout.Controls.Add(LabelMessage, 0, 1);
            layout.SetColumnSpan(LabelMessage, 2);
            Controls.Add(layout);

            LabelTitle.Text = ToPlainTitle(title);
            SetLinkText(LabelMessage, ToPlainMessage(message));
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

        private void LabelMessage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string;
            if (!string.IsNullOrEmpty(url))
            {
                HyperlinkClick?.Invoke(this, new LinkClickedEventArgs(url));
            }
        }

        /// <summary>
        /// Converts feed HTML to plain text. Breaks become newlines, other tags are removed, and entities are decoded last
        /// so that escaped text such as &amp;lt;b&amp;gt; is not stripped as a tag.
        /// </summary>
        private static string ToPlainMessage(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            string text = LineBreakPattern.Replace(html, "\n");
            text = TagPattern.Replace(text, string.Empty);
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }

        private static string ToPlainTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            return System.Net.WebUtility.HtmlDecode(title).Trim();
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
            if (LabelMessage == null || layingOut)
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
                if (LabelMessage.MaximumSize.Width != width)
                {
                    LabelMessage.MaximumSize = new Size(width, 0);
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
            if (disposing && titleFont != null)
            {
                titleFont.Dispose();
                titleFont = null;
            }
        }
    }
}
