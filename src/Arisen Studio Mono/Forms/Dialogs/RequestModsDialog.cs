using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Controls;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Explains the mod request form and offers to open it in the browser or copy its link.
    /// The Windows-only WebView2 from the old dialog is not used here.
    /// </summary>
    internal class RequestModsDialog : Form
    {
        /// <summary>
        /// Manifest resource name of the icon for the "Open in browser" action. Must match the csproj LogicalName.
        /// </summary>
        internal const string IconResourceName = "ArisenStudio.Icons.new-window.png";

        private readonly ActionStrip _actions;
        private readonly TextBox _urlBox;
        private readonly Image _icon;
        private string _formUrl = RequestLinks.FormSubmitMods;

        public RequestModsDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Request Mods";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            int h = Font.Height;
            int pad = h / 2;

            _icon = LoadIcon();

            _actions = new ActionStrip();
            _ = _actions.AddAction("open", "Open in browser", _icon, (s, e) => OpenUrl(RequestLinks.RequestForm), ActionAlign.Right);
            _ = _actions.AddAction("copy", "Copy link", null, (s, e) => CopyText(UrlBox.Text), ActionAlign.Right);

            Label explanation = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                Text = "Arisen Studio takes mod requests through an online form in your web browser. " +
                       "Use \"Open in browser\" to open the request page, or copy the link below and paste it into any browser. " +
                       "Name the game and the mod you would like added, and include a link to it if you have one.",
                Margin = new Padding(0, 0, 0, pad)
            };

            _urlBox = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Text = _formUrl
            };

            TableLayoutPanel body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(explanation, 0, 0);
            body.Controls.Add(_urlBox, 0, 1);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(_actions, 0, 0);
            root.Controls.Add(body, 0, 1);
            Controls.Add(root);

            // Size from font metrics, then clamp to 90% of the working area of the screen this form is on.
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(h * 34, (int)(work.Width * 0.9));
            int height = Math.Min(h * 14, (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
            MinimumSize = new Size(Math.Min(h * 20, width), Math.Min(h * 8, height));
        }

        /// <summary>
        /// The form URL. Shown in the read-only text box and copied by "Copy link".
        /// </summary>
        public string FormUrl
        {
            get { return _formUrl; }
            set
            {
                _formUrl = value ?? string.Empty;
                if (_urlBox != null)
                {
                    _urlBox.Text = _formUrl;
                }
            }
        }

        /// <summary>
        /// Opens a URL in the default browser. Replaceable so smoke tests never start a browser.
        /// </summary>
        public Action<string> OpenUrl { get; set; } = url =>
        {
            _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };

        /// <summary>
        /// Copies text to the clipboard. Replaceable so smoke tests never touch the clipboard.
        /// </summary>
        public Action<string> CopyText { get; set; } = text =>
        {
            Clipboard.SetText(text);
        };

        internal ActionStrip Actions
        {
            get { return _actions; }
        }

        internal TextBox UrlBox
        {
            get { return _urlBox; }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _icon != null)
            {
                _icon.Dispose();
            }
        }

        /// <summary>
        /// Loads the embedded icon. Returns null (never throws) if the resource is missing or cannot be decoded.
        /// </summary>
        private static Image LoadIcon()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }
                    using (Image decoded = Image.FromStream(stream))
                    {
                        return new Bitmap(decoded);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
