using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Shows a title and a read-only block of text (for example release notes) with clickable links.
    /// Stock WinForms port of the old DevExpress DataViewDialog form.
    /// The form Text is the window title; <see cref="LabelTitle"/> is the subtitle; <see cref="Body"/> is the text.
    /// </summary>
    public class DataViewDialog : Form
    {
        private readonly System.Drawing.Font _titleFont;
        private readonly Label _labelTitle;
        private readonly RichTextBox _body;

        public DataViewDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Title";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            _titleFont = new System.Drawing.Font(Font, FontStyle.Bold);

            _labelTitle = new Label
            {
                AutoSize = true,
                UseMnemonic = false,
                Font = _titleFont,
                Text = "Title",
                Margin = new Padding(0, 0, 0, pad)
            };

            _body = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                Text = "Body",
                Margin = new Padding(0)
            };
            _body.LinkClicked += (s, e) =>
            {
                if (OpenUrl != null)
                {
                    OpenUrl(e.LinkText);
                }
            };

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(_labelTitle, 0, 0);
            root.Controls.Add(_body, 0, 1);
            Controls.Add(root);

            Rectangle work = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(h * 30, (int)(work.Width * 0.9));
            ClientSize = new Size(width, h * 10);
        }

        /// <summary>
        /// Opens a link clicked in the body in the default browser. Replaceable so tests never start a browser.
        /// </summary>
        public Action<string> OpenUrl { get; set; } = url =>
        {
            _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };

        /// <summary>
        /// Subtitle shown in bold above the body.
        /// </summary>
        public Label LabelTitle
        {
            get { return _labelTitle; }
        }

        /// <summary>
        /// The text shown in the read-only body. Shown as given (markdown or markup is not rendered).
        /// Line breaks are kept as typed.
        /// </summary>
        public string Body
        {
            get { return _body.Text; }
            set { _body.Text = value ?? string.Empty; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Size the form to the body text, within limits, so the whole text is visible where possible.
            int h = Font.Height;
            int pad = h / 2;
            int wrapWidth = ClientSize.Width - (pad * 2) - SystemInformation.VerticalScrollBarWidth;
            Size measured = TextRenderer.MeasureText(_body.Text, _body.Font, new Size(wrapWidth, int.MaxValue), TextFormatFlags.WordBreak);

            int wanted = measured.Height + _labelTitle.PreferredHeight + (pad * 3) + h;
            int maxHeight = (int)(Screen.FromControl(this).WorkingArea.Height * 0.9);
            int height = Math.Max(h * 6, Math.Min(wanted, maxHeight));
            ClientSize = new Size(ClientSize.Width, height);

            // Centre last, after the final size, as the old form did in its Load handler.
            CenterToParent();
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
