using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// The shared shell of the detail dialogs: a header (section / category (region) crumbs, a bold name and a separator),
    /// a body that the host fills with its own control, and an action row of buttons ending with Close.
    /// Stock WinForms port of the old DevExpress detail dialog shell. The control opens no other form and
    /// raises CloseRequested; the host decides what to do.
    /// Set the crumbs, name, body and actions before the control is shown.
    /// </summary>
    internal class DetailLayout : UserControl
    {
        private readonly Font _boldFont;
        private readonly TableLayoutPanel _root;
        private readonly TableLayoutPanel _headerTable;
        private readonly FlowLayoutPanel _crumbs;
        private readonly Label _labelSection;
        private readonly Label _labelSlash;
        private readonly Label _labelCategory;
        private readonly Label _labelRegion;
        private readonly Label _labelName;
        private readonly Panel _separator;
        private readonly Panel _bodyHost;
        private readonly FlowLayoutPanel _actionRow;
        private readonly Button _closeButton;
        private readonly int _separatorHeight;
        private Control _body;
        private bool _showSeparator = true;
        private string _closeText = "Close";

        public DetailLayout()
        {
            Font = SystemFonts.MessageBoxFont;

            int h = Font.Height;
            int pad = h / 2;

            _boldFont = new Font(Font, FontStyle.Bold);
            _separatorHeight = Math.Max(1, h / 12);

            // Crumbs: "section / category (region)". The slash is always shown; empty parts show as empty text.
            _labelSection = CreateLabel(new Padding(0, 0, h / 4, 0));
            _labelSlash = CreateLabel(new Padding(0, 0, h / 4, 0));
            _labelSlash.Text = "/";
            _labelCategory = CreateLabel(new Padding(0, 0, h / 4, 0));
            _labelRegion = CreateLabel(Padding.Empty);

            _crumbs = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, pad / 2),
                Padding = Padding.Empty
            };
            _crumbs.Controls.Add(_labelSection);
            _crumbs.Controls.Add(_labelSlash);
            _crumbs.Controls.Add(_labelCategory);
            _crumbs.Controls.Add(_labelRegion);

            // UseMnemonic stays on for the name: the doubled "&&" in SetName then shows as one "&".
            _labelName = new Label
            {
                AutoSize = true,
                UseMnemonic = true,
                Font = _boldFont,
                Text = string.Empty,
                Margin = new Padding(0, 0, 0, pad)
            };

            _separator = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Gainsboro,
                Margin = Padding.Empty
            };

            // Header: crumbs, name, then the separator in its own absolute-height row.
            _headerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad, pad, pad, 0),
                Margin = Padding.Empty
            };
            _ = _headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _headerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, _separatorHeight));
            _headerTable.Controls.Add(_crumbs, 0, 0);
            _headerTable.Controls.Add(_labelName, 0, 1);
            _headerTable.Controls.Add(_separator, 0, 2);

            // Body: the host's control is docked into this panel; the panel fills the middle row.
            _bodyHost = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            // Action row: right to left, so the first button added sits at the right. Close is added first.
            _closeButton = CreateButton(_closeText);
            _closeButton.Click += CloseButton_Click;

            _actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad, pad / 2, pad, pad / 2)
            };
            _actionRow.Controls.Add(_closeButton);

            // Root: header (auto), body (fills), action row (auto).
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.Controls.Add(_headerTable, 0, 0);
            _root.Controls.Add(_bodyHost, 0, 1);
            _root.Controls.Add(_actionRow, 0, 2);
            Controls.Add(_root);
        }

        /// <summary>
        /// Raised when the Close button in the action row is clicked. The host closes the dialog or hides the control.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// The text of the Close button. Defaults to "Close". Changing it updates the button at once.
        /// </summary>
        public string CloseText
        {
            get { return _closeText; }
            set
            {
                _closeText = value ?? string.Empty;
                _closeButton.Text = _closeText;
            }
        }

        /// <summary>
        /// Shows or hides the separator line under the header. The header row collapses with it.
        /// </summary>
        public bool ShowSeparator
        {
            get { return _showSeparator; }
            set
            {
                _showSeparator = value;
                _separator.Visible = value;
                _headerTable.RowStyles[2].Height = value ? _separatorHeight : 0F;
            }
        }

        /// <summary>
        /// The body control, docked to fill the middle of the layout. Setting it replaces the previous body,
        /// which is removed from the layout but not disposed. Null means no body.
        /// </summary>
        public Control Body
        {
            get { return _body; }
            set
            {
                if (ReferenceEquals(_body, value))
                {
                    return;
                }

                if (_body != null)
                {
                    _bodyHost.Controls.Remove(_body);
                }

                _body = value;

                if (_body != null)
                {
                    _body.Dock = DockStyle.Fill;
                    _bodyHost.Controls.Add(_body);
                }
            }
        }

        /// <summary>
        /// Sets the crumbs shown in the header as "section / category (region)". Null or empty parts show as empty text.
        /// The region is shown in parentheses only when it is not empty.
        /// </summary>
        public void SetCrumbs(string section, string category, string region)
        {
            _labelSection.Text = section ?? string.Empty;
            _labelCategory.Text = category ?? string.Empty;
            _labelRegion.Text = string.IsNullOrEmpty(region) ? string.Empty : "(" + region + ")";
        }

        /// <summary>
        /// Sets the bold name shown below the crumbs. The name wraps to the width of the control.
        /// </summary>
        public void SetName(string name)
        {
            // "&" is doubled so the name shows it literally (UseMnemonic is on).
            _labelName.Text = (name ?? string.Empty).Replace("&", "&&");
        }

        /// <summary>
        /// Adds a button to the right-to-left action row, after the buttons already added (and after Close).
        /// The first button added sits at the right. Returns the button so the host can enable or disable it.
        /// </summary>
        public Button AddAction(string text, EventHandler onClick)
        {
            Button button = CreateButton(text);
            if (onClick != null)
            {
                button.Click += onClick;
            }

            _actionRow.Controls.Add(button);
            return button;
        }

        protected override void OnResize(EventArgs e)
        {
            // The name wraps to the width of the header, less its side padding.
            int pad = Font.Height / 2;
            _labelName.MaximumSize = new Size(Math.Max(1, ClientSize.Width - (pad * 2)), 0);
            base.OnResize(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _boldFont.Dispose();
            }
            base.Dispose(disposing);
        }

        private Label CreateLabel(Padding margin)
        {
            return new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = _boldFont,
                Text = string.Empty,
                Margin = margin
            };
        }

        private Button CreateButton(string text)
        {
            int h = Font.Height;
            int pad = h / 2;
            return new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(pad / 2),
                Text = text ?? string.Empty
            };
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
