using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Shows the slots event image and its story when the user finds the easter egg.
    /// Stock WinForms port of the old DevExpress EasterEgg form.
    /// </summary>
    public class EasterEgg : Form
    {
        /// <summary>
        /// Image shown in the dialog. Clicking it calls <see cref="OpenUrl"/> with this address.
        /// </summary>
        public const string ImageUrl = "https://db.arisen.studio/images/slots-event.gif";

        private readonly PictureBox _image;

        public EasterEgg()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Homebrew Details";
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            ClientSize = new Size(h * 24, h * 20);

            _image = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            _image.Click += (s, e) =>
            {
                if (OpenUrl != null)
                {
                    OpenUrl(ImageUrl);
                }
            };
            Controls.Add(_image);
        }

        /// <summary>
        /// Opens a URL in the default browser. Replaceable so tests never start a browser.
        /// </summary>
        public Action<string> OpenUrl { get; set; } = url =>
        {
            _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };

        /// <summary>
        /// When true (the default), OnLoad starts the network fetch of <see cref="ImageUrl"/>.
        /// Smoke tests set it to false so no network call is made.
        /// </summary>
        public bool LoadImageOnLoad { get; set; } = true;

        /// <summary>
        /// When true (the default), OnLoad shows the story in a modal message box.
        /// Smoke tests set it to false so the form does not block.
        /// </summary>
        public bool ShowStoryOnLoad { get; set; } = true;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (LoadImageOnLoad)
            {
                try
                {
                    _image.LoadAsync(ImageUrl);
                }
                catch (Exception)
                {
                    // The image is optional. The message below still shows.
                }
            }

            if (!ShowStoryOnLoad)
            {
                return;
            }

            _ = MessageBox.Show(this, "Congrats! You found our little easter egg!\n\nFor those who don't know, on this date our Arisen Studio Discord Server hosted a first of its kind game using the simple concept of the Slots Machine. You type and enter the '.slot' command to roll your slot machine and the our bot responds with your randomly generated results.\nSo, for the next 10 minutes it was complete carnage and we felt like we nearly broke Discord, but it was so much fun!\nWe still do run the game sometimes so if you would like to play (or just watch) the mess of the channel then you're welcome to join us!", "Found Easter Egg <3");
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }
    }
}
