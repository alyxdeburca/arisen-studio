using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Size of a tile. Every size is a multiple of the tile's font height, so tiles scale with the font.
    /// </summary>
    internal enum TileSize
    {
        /// <summary>3 x 3 font heights. An icon, or an icon with a title underneath.</summary>
        Normal,

        /// <summary>6 x 3 font heights. An icon on the left, title and subtitle on the right.</summary>
        Wide,

        /// <summary>6 x 6 font heights. A larger icon above a title and subtitle.</summary>
        Large
    }

    /// <summary>
    /// A clickable tile with an optional title, subtitle and image. Pointer and keyboard (Space or Enter) both raise <see cref="Control.Click"/>.
    /// Default look: a window-coloured background with a 1 pixel border, so an idle tile is visible on the form.
    /// Set <see cref="Control.BackColor"/> and <see cref="Control.ForeColor"/> to restyle a tile.
    /// Titles are kept to one line and end with an ellipsis when they are too long.
    /// </summary>
    internal class TileCard : Control
    {
        private Font _titleFont;
        private string _id = string.Empty;
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private Image _image;
        private TileSize _sizeKind = TileSize.Normal;
        private bool _hovered;
        private bool _pressed;
        private bool _mouseArmed;
        private bool _keyArmed;

        public TileCard()
        {
            SetStyle(ControlStyles.StandardClick, false);
            SetStyle(ControlStyles.Selectable
                | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            BackColor = SystemColors.Window;
            AccessibleRole = AccessibleRole.PushButton;
            RebuildFonts();
            ApplyMetrics();
        }

        /// <summary>
        /// Identifier used by <see cref="TilePanel.Find"/>. Defaults to the title, or to "group:index" when there is no title.
        /// </summary>
        public string Id
        {
            get { return _id; }
            set { _id = value ?? string.Empty; }
        }

        /// <summary>
        /// Main text of the tile. Empty means no title is drawn.
        /// </summary>
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value ?? string.Empty;
                AccessibleName = _title;
                Invalidate();
            }
        }

        /// <summary>
        /// Secondary text, drawn in the normal font under the title. Empty means none.
        /// </summary>
        public string Subtitle
        {
            get { return _subtitle; }
            set
            {
                _subtitle = value ?? string.Empty;
                Invalidate();
            }
        }

        /// <summary>
        /// Icon drawn in the tile. The caller owns it; the tile never disposes it. Null means no icon.
        /// </summary>
        public Image Image
        {
            get { return _image; }
            set
            {
                _image = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Size of the tile. Changing it resizes the control.
        /// </summary>
        public TileSize SizeKind
        {
            get { return _sizeKind; }
            set
            {
                _sizeKind = value;
                ApplyMetrics();
                Invalidate();
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RebuildFonts();
            ApplyMetrics();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _hovered = false;
            _pressed = false;
            _mouseArmed = false;
            _keyArmed = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            _mouseArmed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled)
            {
                return;
            }
            _mouseArmed = ClientRectangle.Contains(e.Location);
            _pressed = _mouseArmed;
            if (CanFocus)
            {
                Focus();
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            bool fire = _mouseArmed && e.Button == MouseButtons.Left && Enabled && ClientRectangle.Contains(e.Location);
            _mouseArmed = false;
            _pressed = false;
            Invalidate();
            if (fire)
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _pressed = false;
            _mouseArmed = false;
            _keyArmed = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Enter || key == Keys.Space)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) && Enabled && !_keyArmed)
            {
                _keyArmed = true;
                _pressed = true;
                e.Handled = true;
                e.SuppressKeyPress = true;
                Invalidate();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space)
            {
                return;
            }
            bool fire = _keyArmed && Enabled;
            _keyArmed = false;
            _pressed = false;
            Invalidate();
            e.Handled = true;
            if (fire)
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            // Background, then the hover or press wash, then a 1 pixel border so the tile shows even when idle.
            using (SolidBrush fill = new SolidBrush(BackColor))
            {
                g.FillRectangle(fill, bounds);
            }

            int alpha = _pressed ? 70 : (_hovered ? 40 : 0);
            if (alpha > 0 && Enabled)
            {
                using (SolidBrush wash = new SolidBrush(Color.FromArgb(alpha, ForeColor)))
                {
                    g.FillRectangle(wash, bounds);
                }
            }

            using (Pen border = new Pen(SystemColors.ControlDark))
            {
                g.DrawRectangle(border, bounds);
            }

            int inset = Math.Max(4, Font.Height / 2);
            Rectangle content = Rectangle.Inflate(ClientRectangle, -inset, -inset);
            Color textColor = Enabled ? ForeColor : SystemColors.GrayText;
            bool hasTitle = Title.Length > 0;
            bool hasSubtitle = Subtitle.Length > 0;

            if (_sizeKind == TileSize.Normal)
            {
                PaintNormal(g, content, textColor, hasTitle);
            }
            else if (_sizeKind == TileSize.Wide)
            {
                PaintWide(g, content, textColor);
            }
            else
            {
                PaintLarge(g, content, textColor, hasTitle, hasSubtitle);
            }

            if (Focused)
            {
                int focusInset = Math.Max(2, Font.Height / 4);
                Rectangle focusRect = Rectangle.Inflate(ClientRectangle, -focusInset, -focusInset);
                using (Pen focusPen = new Pen(textColor))
                {
                    focusPen.DashStyle = DashStyle.Dot;
                    g.DrawRectangle(focusPen, focusRect);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeTitleFont();
            }
            base.Dispose(disposing);
        }

        private void PaintNormal(Graphics g, Rectangle content, Color color, bool hasTitle)
        {
            if (_image == null)
            {
                DrawTextBlock(g, content, color, true, Title, Subtitle);
                return;
            }

            int unit = Font.Height;
            Rectangle textArea = hasTitle ? new Rectangle(content.X, content.Bottom - unit, content.Width, unit) : Rectangle.Empty;
            Rectangle iconArea = hasTitle
                ? new Rectangle(content.X, content.Y, content.Width, Math.Max(0, content.Height - unit))
                : content;
            DrawImageFitted(g, iconArea);
            if (hasTitle)
            {
                DrawTextBlock(g, textArea, color, true, Title, string.Empty);
            }
        }

        private void PaintWide(Graphics g, Rectangle content, Color color)
        {
            int unit = Font.Height;
            Rectangle textArea = content;
            if (_image != null)
            {
                int iconWidth = Math.Min(content.Width, unit * 2);
                Rectangle iconArea = new Rectangle(content.X, content.Y, iconWidth, content.Height);
                DrawImageFitted(g, iconArea);
                int textX = content.X + iconWidth + unit / 2;
                textArea = new Rectangle(textX, content.Y, Math.Max(0, content.Right - textX), content.Height);
            }
            DrawTextBlock(g, textArea, color, false, Title, Subtitle);
        }

        private void PaintLarge(Graphics g, Rectangle content, Color color, bool hasTitle, bool hasSubtitle)
        {
            int unit = Font.Height;
            Rectangle textArea = content;
            if (_image != null)
            {
                int textHeight = (hasTitle || hasSubtitle) ? unit * 2 : 0;
                int iconHeight = Math.Max(0, content.Height - textHeight - unit / 2);
                Rectangle iconArea = new Rectangle(content.X, content.Y, content.Width, iconHeight);
                DrawImageFitted(g, iconArea);
                int textY = content.Y + iconHeight;
                textArea = new Rectangle(content.X, textY, content.Width, Math.Max(0, content.Bottom - textY));
            }
            DrawTextBlock(g, textArea, color, false, Title, Subtitle);
        }

        /// <summary>
        /// Draws the title (top half) and subtitle (bottom half) of an area, or whichever is present in the whole area.
        /// </summary>
        private void DrawTextBlock(Graphics g, Rectangle area, Color color, bool centred, string title, string subtitle)
        {
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }
            bool hasTitle = !string.IsNullOrEmpty(title);
            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            if (!hasTitle && !hasSubtitle)
            {
                return;
            }

            Font titleFont = _titleFont ?? Font;
            if (hasTitle && hasSubtitle)
            {
                int half = area.Height / 2;
                DrawString(g, title, titleFont, color, new Rectangle(area.X, area.Y, area.Width, half), centred);
                DrawString(g, subtitle, Font, color, new Rectangle(area.X, area.Y + half, area.Width, area.Height - half), centred);
            }
            else if (hasTitle)
            {
                DrawString(g, title, titleFont, color, area, centred);
            }
            else
            {
                DrawString(g, subtitle, Font, color, area, centred);
            }
        }

        private static void DrawString(Graphics g, string text, Font font, Color color, Rectangle area, bool centred)
        {
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = centred ? StringAlignment.Center : StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                // One line only: a long title ends with an ellipsis instead of spilling into a clipped second line.
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.DrawString(text, font, brush, new RectangleF(area.X, area.Y, area.Width, area.Height), format);
                }
            }
        }

        /// <summary>
        /// Scales the image to fit the area, keeping its aspect ratio and centring it. Disabled tiles draw it faded.
        /// </summary>
        private void DrawImageFitted(Graphics g, Rectangle area)
        {
            if (_image == null || area.Width <= 0 || area.Height <= 0 || _image.Width <= 0 || _image.Height <= 0)
            {
                return;
            }
            double scale = Math.Min((double)area.Width / _image.Width, (double)area.Height / _image.Height);
            int w = Math.Max(1, (int)Math.Round(_image.Width * scale));
            int h = Math.Max(1, (int)Math.Round(_image.Height * scale));
            Rectangle target = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);
            if (Enabled)
            {
                g.DrawImage(_image, target);
                return;
            }
            ColorMatrix fade = new ColorMatrix();
            fade.Matrix33 = 0.4f;
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(fade, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(_image, target, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private void RebuildFonts()
        {
            DisposeTitleFont();
            _titleFont = new Font(Font, FontStyle.Bold);
        }

        private void DisposeTitleFont()
        {
            if (_titleFont != null)
            {
                _titleFont.Dispose();
                _titleFont = null;
            }
        }

        /// <summary>
        /// Sets the tile's size and spacing from the font height.
        /// </summary>
        private void ApplyMetrics()
        {
            int unit = Math.Max(8, Font.Height);
            int gap = Math.Max(2, unit / 4);
            Margin = new Padding(gap);
            switch (_sizeKind)
            {
                case TileSize.Wide:
                    Size = new Size(unit * 6, unit * 3);
                    break;
                case TileSize.Large:
                    Size = new Size(unit * 6, unit * 6);
                    break;
                default:
                    Size = new Size(unit * 3, unit * 3);
                    break;
            }
        }
    }
}
