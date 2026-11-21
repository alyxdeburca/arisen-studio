using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// A console profile tile: a picture above the console name. Built in code with stock controls.
    /// </summary>
    public class ProfileItem : UserControl, IMessageFilter
    {
        private EventHandler _click;
        private EventHandler _doubleClick;

        /// <summary>The console picture. Public for parity with the old designer field.</summary>
        public PictureBox ImageConsole;

        /// <summary>The console name label. Public for parity with the old designer field.</summary>
        public Label LabelConsoleName;

        public ProfileItem(string consoleName, Image image)
        {
            // Lets the hover pass set BackColor on this control without throwing.
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            Font = SystemFonts.MessageBoxFont;
            int pad = Math.Max(2, Font.Height / 4);

            // A shared icon box may replace this PictureBox later.
            ImageConsole = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false,
                Margin = Padding.Empty,
                Image = image
            };

            LabelConsoleName = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.TopCenter,
                Margin = Padding.Empty,
                Text = consoleName ?? string.Empty
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Font.Height + pad));
            layout.Controls.Add(ImageConsole, 0, 0);
            layout.Controls.Add(LabelConsoleName, 0, 1);

            Controls.Add(layout);

            Size = new Size(Font.Height * 9, Font.Height * 10 + pad * 3);

            ConsoleImage = image;
            ConsoleName = consoleName;

            // Click and double-click on the tile or any descendant raise OnClick / OnDoubleClick with this as sender.
            Click += ForwardClick;
            DoubleClick += ForwardDoubleClick;
            WireDescendants(this);

            Application.AddMessageFilter(this);
        }

        public Image ConsoleImage { get; set; }

        /// <summary>
        /// The profile this tile represents. Typed as the profile model, as in the old control.
        /// </summary>
        public ArisenStudio.Models.Resources.ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>The name shown under the picture.</summary>
        public string ConsoleName { get; set; }

        public bool IsSelected { get; set; }

        public bool ShouldHover { get; set; } = true;

        /// <summary>Colour used while the mouse hovers the tile. Replaces the old skin lookup.</summary>
        public Color HighlightColor { get; set; } = SystemColors.Highlight;

        public void ReloadControls()
        {
            ImageConsole.Image = ConsoleImage;
            LabelConsoleName.Text = ConsoleName ?? string.Empty;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!ShouldHover || IsDisposed || !IsHandleCreated || !Visible)
            {
                return false;
            }

            Color target;
            if (ClientRectangle.Contains(PointToClient(MousePosition)))
            {
                target = HighlightColor;
            }
            else
            {
                target = IsSelected ? BackColor : Color.Transparent;
            }

            if (BackColor != target)
            {
                BackColor = target;
                Invalidate(true);
            }

            return false;
        }

        public new event EventHandler OnClick
        {
            add => _click += value;
            remove => _click -= value;
        }

        public new event EventHandler OnDoubleClick
        {
            add => _doubleClick += value;
            remove => _doubleClick -= value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
            }
            base.Dispose(disposing);
        }

        private void WireDescendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                child.Click += ForwardClick;
                child.DoubleClick += ForwardDoubleClick;
                WireDescendants(child);
            }
        }

        private void ForwardClick(object sender, EventArgs e)
        {
            _click?.Invoke(this, e);
        }

        private void ForwardDoubleClick(object sender, EventArgs e)
        {
            _doubleClick?.Invoke(this, e);
        }
    }
}
