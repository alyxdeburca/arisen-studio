using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Covers a page or panel with a centred message such as "You must have webMAN installed to use this feature."
    /// Replacement for the DevExpress <c>OverlayWindowPainterBase</c> painter of the same name, which cannot run on
    /// Mono. It is an ordinary child control, not a separate window, so it works on Mono/X11.
    /// </summary>
    /// <remarks>
    /// The overlay hides what is under it but does not block keyboard focus: tabbing can still reach the covered
    /// controls. The original disabled the page as well; the host should set Enabled = false on the page content
    /// while the overlay is shown, and Enabled = true after <see cref="Remove"/>.
    /// </remarks>
    internal class FeatureNotAvailableOverlay : Panel
    {
        private readonly Label labelMessage = new Label();
        private Font messageFont;
        private Control host;

        public FeatureNotAvailableOverlay(string message)
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            messageFont = new Font(Font.FontFamily, Font.Size * 1.5F, FontStyle.Regular);
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            TabStop = false;
            Padding = new Padding(Math.Max(8, Font.Height));

            labelMessage.Dock = DockStyle.Fill;
            labelMessage.AutoSize = false;
            labelMessage.UseMnemonic = false;
            labelMessage.TextAlign = ContentAlignment.MiddleCenter;
            labelMessage.Font = messageFont;
            labelMessage.Text = message ?? string.Empty;
            Controls.Add(labelMessage);

            ResumeLayout(true);
        }

        /// <summary>
        /// The text shown in the middle of the overlay.
        /// </summary>
        public string Message
        {
            get { return labelMessage.Text; }
            set { labelMessage.Text = value ?? string.Empty; }
        }

        /// <summary>
        /// Puts the overlay on top of <paramref name="target"/>, filling its client area and following its size.
        /// Calling it again with another control moves the overlay there.
        /// </summary>
        public void ShowOver(Control target)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (!ReferenceEquals(host, target))
            {
                Remove();
                host = target;
                host.Resize += Host_Resize;
                Dock = DockStyle.None;
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                host.Controls.Add(this);
            }

            Bounds = host.ClientRectangle;
            Visible = true;
            BringToFront();
        }

        /// <summary>
        /// Takes the overlay off its host. It can be shown again with <see cref="ShowOver"/>.
        /// </summary>
        public void Remove()
        {
            if (host == null)
            {
                return;
            }

            host.Resize -= Host_Resize;
            host.Controls.Remove(this);
            host = null;
        }

        private void Host_Resize(object sender, EventArgs e)
        {
            if (host != null)
            {
                Bounds = host.ClientRectangle;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Remove();
                if (messageFont != null)
                {
                    messageFont.Dispose();
                    messageFont = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
