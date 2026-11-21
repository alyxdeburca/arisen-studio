using System;
using System.IO;
using System.Threading.Tasks;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Takes a screenshot of the Xbox 360 screen and shows it. Stock WinForms port of the old DevExpress TakeScreenshot form;
    /// the window itself is ScreenshotFormBase. The console call (XDevkit ScreenShot) is not ported; it sits behind
    /// IXboxScreenshotSource.
    /// Host hooks, set before the form is shown:
    ///  - ScreenshotSource: the console boundary (default: a stub that fails with a message).
    ///  - Uploader (from the base form): the image upload service. Default: a stub that fails with a message.
    /// </summary>
    public class TakeScreenshot : ScreenshotFormBase
    {
        public TakeScreenshot()
            : base("Xbox")
        {
        }

        /// <summary>
        /// Console boundary used to capture the screen.
        /// </summary>
        public IXboxScreenshotSource ScreenshotSource { get; set; } = UnavailableXboxConsoleAccess.Instance;

        protected override async Task CaptureAsync(string localFilePath, string fileNameWithoutExtension)
        {
            IXboxScreenshotSource source = ScreenshotSource;
            await Task.Run(() => source.CaptureScreenshot(localFilePath));

            if (!File.Exists(localFilePath))
            {
                throw new IOException("The console did not write a screenshot file.");
            }
        }
    }
}
