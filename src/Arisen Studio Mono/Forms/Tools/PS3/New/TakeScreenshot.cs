using ArisenStudio.Extensions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Takes a screenshot of the PS3 screen through webMAN (xmb.ps3$screenshot), downloads it over HTTP and shows it.
    /// Stock WinForms port of the old DevExpress TakeScreenshot form; the window itself is ScreenshotFormBase.
    /// Host hooks, set before the form is shown:
    ///  - ConsoleAddress: the address (IP) of the console running webMAN.
    ///  - FileTransfer: optional FTP boundary used by Delete Image to remove the screenshot left on the console. Null leaves it there.
    ///  - Uploader (from the base form): the image upload service. Default: a stub that fails with a message.
    /// </summary>
    public class TakeScreenshot : ScreenshotFormBase
    {
        private string _lastConsoleFile;

        public TakeScreenshot()
            : base("PS3")
        {
        }

        /// <summary>
        /// Address (IP) of the console running webMAN.
        /// </summary>
        public string ConsoleAddress { get; set; }

        /// <summary>
        /// Folder on the console where the screenshot is written first. The default is the path the old form used,
        /// including its doubled "dev_hdd0" (looks like a typo; the old form was never checked against a console here).
        /// </summary>
        public string ConsoleScreenshotFolder { get; set; } = "/dev_hdd0/dev_hdd0/tmp/screenshots/";

        /// <summary>
        /// Optional. When set, Delete Image also deletes the screenshot file on the console through it.
        /// </summary>
        public IPs3FileTransfer FileTransfer { get; set; }

        protected override async Task CaptureAsync(string localFilePath, string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(ConsoleAddress))
            {
                throw new InvalidOperationException("ConsoleAddress is not set.");
            }

            // Spaces would need escaping in the webMAN command and in the URL, so the console copy uses underscores.
            string consoleFile = ConsoleScreenshotFolder + fileNameWithoutExtension.Replace(' ', '_') + ".bmp";

            await WebManExtensions.ScreenshotAsync(ConsoleAddress, consoleFile, false);
            _lastConsoleFile = consoleFile;

            bool downloaded = await HttpExtensions.DownloadFileAsync("http://" + ConsoleAddress + consoleFile, localFilePath);

            if (!downloaded || !File.Exists(localFilePath))
            {
                throw new IOException("Unable to download the screenshot from the console.");
            }
        }

        protected override async Task DeleteConsoleCopyAsync()
        {
            string consoleFile = _lastConsoleFile;
            _lastConsoleFile = null;

            if (FileTransfer != null && !string.IsNullOrEmpty(consoleFile))
            {
                if (await FileTransfer.FileExistsAsync(consoleFile))
                {
                    await FileTransfer.DeleteFileAsync(consoleFile);
                }
            }
        }
    }
}
