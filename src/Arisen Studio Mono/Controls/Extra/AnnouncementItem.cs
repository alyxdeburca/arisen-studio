using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// One announcement: title, message with clickable links, time stamp and a Dismiss button.
    /// Port of the DevExpress <c>AccouncementItem</c> (the original name is misspelled). The host sets the width
    /// (for example Dock = Top). The control does not touch settings and does not open links: it raises
    /// <see cref="Dismissed"/> and <see cref="HyperlinkClick"/> and the host acts on them.
    /// </summary>
    public class AnnouncementItem : UserControl
    {
        private const int MaximumLinks = 30;

        private readonly DateTime timeStamp;
        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private Font titleFont;
        private bool useRelativeTimes;
        private bool layingOut;

        public Label LabelTitle;
        public LinkLabel LabelMessage;
        public Label LabelTimeStamp;
        public Button ButtonDismiss;

        /// <summary>
        /// Raised when a link inside the message is clicked. The control does not open links itself.
        /// </summary>
        public event LinkClickedEventHandler HyperlinkClick;

        /// <summary>
        /// Raised after the Dismiss button hid the item. The host records <see cref="Id"/> in its dismissed list.
        /// </summary>
        public event EventHandler Dismissed;

        public AnnouncementItem(int id, string title, string message, DateTime timeStamp)
        {
            SuspendLayout();

            Id = id;
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
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };

            LabelTimeStamp = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(pad, 0, pad, 0)
            };

            ButtonDismiss = new Button
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                UseVisualStyleBackColor = true,
                Text = "Dismiss",
                Anchor = AnchorStyles.Right,
                Margin = Padding.Empty
            };
            ButtonDismiss.Click += ButtonDismiss_Click;

            LabelMessage = new LinkLabel
            {
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, pad, 0, 0)
            };
            LabelMessage.LinkClicked += LabelMessage_LinkClicked;

            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layout.ColumnCount = 3;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(LabelTitle, 0, 0);
            layout.Controls.Add(LabelTimeStamp, 1, 0);
            layout.Controls.Add(ButtonDismiss, 2, 0);
            layout.Controls.Add(LabelMessage, 0, 1);
            layout.SetColumnSpan(LabelMessage, 3);
            Controls.Add(layout);

            LabelTitle.Text = string.IsNullOrEmpty(title) ? string.Empty : System.Net.WebUtility.HtmlDecode(title).Trim();
            SetMessage(message);
            LabelTimeStamp.Text = FormatTimeStamp();

            ResumeLayout(true);
        }

        /// <summary>
        /// The announcement id the host stores when the item is dismissed.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// When true the time stamp reads as "5 minutes ago" for recent entries; otherwise it is shown as MM/dd/yyyy.
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

        private void SetMessage(string message)
        {
            ExtraItemText.LinkedText parsed = ExtraItemText.FromHtml(message, MaximumLinks);
            LabelMessage.Links.Clear();
            LabelMessage.Text = parsed.Text;
            foreach (ExtraItemText.LinkSpan link in parsed.Links)
            {
                try
                {
                    LabelMessage.Links.Add(link.Start, link.Length, link.Url);
                }
                catch (ArgumentException)
                {
                    // A bad range must never escape the constructor or a paint pass.
                }
            }
        }

        private string FormatTimeStamp()
        {
            return ExtraItemText.FormatTimeStamp(timeStamp, useRelativeTimes, "MM/dd/yyyy");
        }

        private void LabelMessage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string;
            if (!string.IsNullOrEmpty(url))
            {
                HyperlinkClick?.Invoke(this, new LinkClickedEventArgs(url));
            }
        }

        private void ButtonDismiss_Click(object sender, EventArgs e)
        {
            Hide();
            Dismissed?.Invoke(this, EventArgs.Empty);
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
