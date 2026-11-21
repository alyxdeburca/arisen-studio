using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Placeholder shown in the announcements panel when there is nothing to display.
    /// </summary>
    public class NoAnnouncementsItem : UserControl
    {
        private const string IconName = "ArisenStudio.Icons.icons8-announcement.png";

        private string titleText = "NO ANNOUNCEMENTS";
        private string subTitleText = "No news is good news!";

        private readonly TableLayoutPanel layout = new TableLayoutPanel();
        private readonly PictureBox imageIcon = new PictureBox();
        private Font titleFont;
        private Image iconImage;

        public Label LabelTitle;
        public Label LabelSubTitle;

        public NoAnnouncementsItem()
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            titleFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(4, Font.Height / 2);
            Padding = new Padding(pad);
            Margin = new Padding(0);
            MinimumSize = new Size(Font.Height * 12, Font.Height * 12);
            DoubleBuffered = true;

            iconImage = LoadIcon(IconName);

            imageIcon.Image = iconImage;
            imageIcon.SizeMode = PictureBoxSizeMode.Zoom;
            imageIcon.Size = new Size(Font.Height * 6, Font.Height * 6);
            imageIcon.Anchor = AnchorStyles.None;
            imageIcon.Margin = new Padding(0, 0, 0, pad);
            imageIcon.Visible = iconImage != null;

            LabelTitle = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = titleFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            LabelSubTitle = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.TopCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };

            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.Controls.Add(imageIcon, 0, 1);
            layout.Controls.Add(LabelTitle, 0, 2);
            layout.Controls.Add(LabelSubTitle, 0, 3);
            Controls.Add(layout);

            LoadText();

            ResumeLayout(true);
        }

        /// <summary>
        /// Heading shown under the icon. LoadText applies it to the label.
        /// </summary>
        public string TitleText
        {
            get { return titleText; }
            set
            {
                titleText = value ?? string.Empty;
                LoadText();
            }
        }

        /// <summary>
        /// Secondary line shown under the heading. LoadText applies it to the label.
        /// </summary>
        public string SubTitleText
        {
            get { return subTitleText; }
            set
            {
                subTitleText = value ?? string.Empty;
                LoadText();
            }
        }

        /// <summary>
        /// Copies TitleText and SubTitleText into the labels.
        /// </summary>
        public void LoadText()
        {
            if (LabelTitle == null || LabelSubTitle == null)
            {
                return;
            }

            LabelTitle.Text = titleText;
            LabelSubTitle.Text = subTitleText;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadText();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (LabelSubTitle == null || LabelTitle == null)
            {
                return;
            }

            int width = ClientSize.Width - Padding.Horizontal;
            if (width <= 0)
            {
                return;
            }

            Size cap = new Size(width, 0);
            if (LabelTitle.MaximumSize != cap)
            {
                LabelTitle.MaximumSize = cap;
            }

            if (LabelSubTitle.MaximumSize != cap)
            {
                LabelSubTitle.MaximumSize = cap;
            }
        }

        private static Image LoadIcon(string resourceName)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (Image source = Image.FromStream(stream))
                    {
                        return new Bitmap(source);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            if (titleFont != null)
            {
                titleFont.Dispose();
                titleFont = null;
            }

            if (iconImage != null)
            {
                iconImage.Dispose();
                iconImage = null;
            }
        }
    }
}
