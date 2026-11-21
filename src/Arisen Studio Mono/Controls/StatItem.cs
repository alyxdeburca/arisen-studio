using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// A small statistic tile: a bold title above a value. Built in code with a stock TableLayoutPanel.
    /// </summary>
    public class StatItem : UserControl
    {
        private readonly Font _titleFont;

        /// <summary>The title label (public for parity with the old designer field).</summary>
        public Label LabelTitle;

        /// <summary>The value label (public for parity with the old designer field).</summary>
        public Label LabelValue;

        public StatItem()
        {
            Font = SystemFonts.MessageBoxFont;
            _titleFont = new Font(Font, FontStyle.Bold);

            int gap = Math.Max(2, Font.Height / 4);

            LabelTitle = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Font = _titleFont,
                Text = "Title"
            };

            LabelValue = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = "Value"
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, _titleFont.Height + gap));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(LabelTitle, 0, 0);
            layout.Controls.Add(LabelValue, 0, 1);

            Controls.Add(layout);

            Size = new Size(Font.Height * 10, _titleFont.Height + Font.Height + gap * 2);
        }

        public string Title
        {
            get => LabelTitle.Text;
            set => LabelTitle.Text = value ?? string.Empty;
        }

        public string Value
        {
            get => LabelValue.Text;
            set => LabelValue.Text = value ?? string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
