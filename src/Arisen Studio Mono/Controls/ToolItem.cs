using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// A tool tile: a name, a description and a Launch button that shows <see cref="ToolForm"/>.
    /// Built in code with stock controls and a TableLayoutPanel.
    /// </summary>
    public class ToolItem : UserControl, IMessageFilter
    {
        private readonly Font _nameFont;
        private readonly Label _labelName;
        private readonly Label _labelDescription;
        private readonly Button _buttonLaunch;
        private EventHandler _click;
        private EventHandler _doubleClick;

        public ToolItem()
        {
            // Lets the hover pass set BackColor = Transparent on this control without throwing.
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            Font = SystemFonts.MessageBoxFont;
            _nameFont = new Font(Font, FontStyle.Bold);
            int pad = Math.Max(2, Font.Height / 2);

            _labelName = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                Font = _nameFont,
                Margin = Padding.Empty,
                Text = "Name"
            };

            _labelDescription = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.TopCenter,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Text = "Description"
            };

            _buttonLaunch = new Button
            {
                Dock = DockStyle.Fill,
                Font = _nameFont,
                BackColor = SystemColors.Control,
                Margin = Padding.Empty,
                Text = "Launch Tool"
            };
            _buttonLaunch.Click += ButtonLaunch_Click;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = new Padding(pad),
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, _nameFont.Height * 2));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Font.Height * 2 + pad));
            layout.Controls.Add(_labelName, 0, 0);
            layout.Controls.Add(_labelDescription, 0, 1);
            layout.Controls.Add(_buttonLaunch, 0, 2);

            Controls.Add(layout);

            Size = new Size(Font.Height * 11, Font.Height * 13);

            // Click and double-click on the tile or any child (except the Launch button) raise OnClick / OnDoubleClick.
            Click += ForwardClick;
            DoubleClick += ForwardDoubleClick;
            WireDescendants(this);

            Application.AddMessageFilter(this);
        }

        public string ToolName { get; set; } = "Tool Name";

        public string Description { get; set; } = "Description";

        /// <summary>The form shown when Launch is pressed. Replaces the old DevExpress XtraForm.</summary>
        public Form ToolForm { get; set; }

        /// <summary>Colour used while the mouse hovers the tile. Replaces the old skin lookup.</summary>
        public Color HighlightColor { get; set; } = SystemColors.Highlight;

        public void ReloadControls()
        {
            _labelName.Text = ToolName;
            _labelDescription.Text = Description;
        }

        public bool IsSelected { get; set; }

        public bool ShouldHover { get; set; } = true;

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
                _nameFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private void WireDescendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child == _buttonLaunch)
                {
                    continue;
                }

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

        private void ButtonLaunch_Click(object sender, EventArgs e)
        {
            if (ToolForm != null && !ToolForm.IsDisposed)
            {
                _ = ToolForm.ShowDialog(this);
            }
        }
    }
}
