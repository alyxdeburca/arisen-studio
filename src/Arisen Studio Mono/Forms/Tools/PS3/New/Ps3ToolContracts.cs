using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    // Boundaries between the PS3 tool forms and the parts that talk to a console.
    // The forms call only these interfaces. The defaults ("Unavailable...") are stubs that fail with a clear message,
    // so a form opens and shows that message instead of crashing. The host replaces them before showing a form.

    /// <summary>
    /// One entry of a console folder listing (a file or a folder).
    /// </summary>
    public sealed class Ps3FileEntry
    {
        public string Name { get; set; }

        public long Size { get; set; }

        public DateTime Modified { get; set; }

        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// File access on the PS3 over FTP. A host adapter can wrap Services\FtpService
    /// (UploadFileAsync, DownloadFileAsync, FileExistsAsync, DeleteFileAsync, and a listing built from the FluentFTP listing).
    /// Paths are console paths with forward slashes ("/dev_hdd0/boot_plugins.txt"). Methods throw on failure.
    /// </summary>
    public interface IPs3FileTransfer
    {
        Task<bool> FileExistsAsync(string consoleFile);

        /// <summary>Download a console file to the exact local file path, overwriting.</summary>
        Task DownloadFileAsync(string localFile, string consoleFile);

        /// <summary>Upload a local file to the exact console file path, overwriting.</summary>
        Task UploadFileAsync(string localFile, string consoleFile);

        Task DeleteFileAsync(string consoleFile);

        /// <summary>List the files and folders directly inside a console folder.</summary>
        Task<IList<Ps3FileEntry>> ListFilesAsync(string consoleFolder);
    }

    /// <summary>
    /// Stub: every call fails with NotSupportedException. Replace with a real FTP adapter.
    /// </summary>
    public sealed class UnavailablePs3FileTransfer : IPs3FileTransfer
    {
        private static Exception NotAvailable()
        {
            return new NotSupportedException("PS3 file transfer is not available in this build. No FTP adapter is connected.");
        }

        public Task<bool> FileExistsAsync(string consoleFile)
        {
            return Task.FromException<bool>(NotAvailable());
        }

        public Task DownloadFileAsync(string localFile, string consoleFile)
        {
            return Task.FromException(NotAvailable());
        }

        public Task UploadFileAsync(string localFile, string consoleFile)
        {
            return Task.FromException(NotAvailable());
        }

        public Task DeleteFileAsync(string consoleFile)
        {
            return Task.FromException(NotAvailable());
        }

        public Task<IList<Ps3FileEntry>> ListFilesAsync(string consoleFolder)
        {
            return Task.FromException<IList<Ps3FileEntry>>(NotAvailable());
        }
    }

    public enum Ps3LedColor
    {
        Red = 0,
        Green = 1
    }

    public enum Ps3LedMode
    {
        Off = 0,
        On = 1,
        BlinkFast = 2,
        BlinkSlow = 3
    }

    public enum Ps3BuzzerMode
    {
        Single = 0,
        Double = 1,
        Triple = 2
    }

    /// <summary>
    /// Console control on the PS3 (the old form used PS3Lib / PS3 Manager API: PS3MAPI). Not ported.
    /// A host adapter can use PS3MAPI, or webMAN calls (Extensions\WebManExtensions has LED, buzzer, IDPS and PSID
    /// calls; it has no temperature or firmware read, so the details panel would stay empty on that route).
    /// The enum values above follow the radio order on the form and WebManExtensions.LedColor / LedMode;
    /// the adapter must map them to its own values (the PS3MAPI numbers were not checked).
    /// Connect and the other methods throw on failure.
    /// </summary>
    public interface IPs3ConsoleControl
    {
        bool IsConnected { get; }

        void Connect(string address);

        void Disconnect();

        void GetTemperatures(out uint cellCelsius, out uint rsxCelsius);

        string GetFirmwareVersion();

        string GetFirmwareType();

        string GetCoreVersion();

        void SetConsoleLed(Ps3LedColor color, Ps3LedMode mode);

        void RingBuzzer(Ps3BuzzerMode mode);

        /// <summary>Set the IDPS (32 hex characters).</summary>
        void SetConsoleId(string idps);

        /// <summary>Set the PSID (32 hex characters).</summary>
        void SetPsid(string psid);
    }

    /// <summary>
    /// Stub: not connected, and every call fails with NotSupportedException.
    /// </summary>
    public sealed class UnavailablePs3ConsoleControl : IPs3ConsoleControl
    {
        private static Exception NotAvailable()
        {
            return new NotSupportedException("PS3 console control is not available in this build. No PS3 Manager API adapter is connected.");
        }

        public bool IsConnected
        {
            get { return false; }
        }

        public void Connect(string address)
        {
            throw NotAvailable();
        }

        public void Disconnect()
        {
        }

        public void GetTemperatures(out uint cellCelsius, out uint rsxCelsius)
        {
            throw NotAvailable();
        }

        public string GetFirmwareVersion()
        {
            throw NotAvailable();
        }

        public string GetFirmwareType()
        {
            throw NotAvailable();
        }

        public string GetCoreVersion()
        {
            throw NotAvailable();
        }

        public void SetConsoleLed(Ps3LedColor color, Ps3LedMode mode)
        {
            throw NotAvailable();
        }

        public void RingBuzzer(Ps3BuzzerMode mode)
        {
            throw NotAvailable();
        }

        public void SetConsoleId(string idps)
        {
            throw NotAvailable();
        }

        public void SetPsid(string psid)
        {
            throw NotAvailable();
        }
    }

    public sealed class GameBrowserNavigatedEventArgs : EventArgs
    {
        public GameBrowserNavigatedEventArgs(string url, bool success)
        {
            Url = url;
            Success = success;
        }

        public string Url { get; private set; }

        public bool Success { get; private set; }
    }

    /// <summary>
    /// An embedded web page for the webMAN games list. The old form used a WebView2 control (Windows-only, not ported).
    /// <see cref="View" /> is docked into the form. A real implementation raises Navigated after each page load and
    /// hides the page elements by id (the old form hid "iwrap", "search" and "menu-icon" with scripts).
    /// </summary>
    public interface IPs3GameBrowser
    {
        Control View { get; }

        string CurrentUrl { get; }

        event EventHandler<GameBrowserNavigatedEventArgs> Navigated;

        void Navigate(string url);

        Task HideElementsAsync(params string[] elementIds);
    }

    /// <summary>
    /// Stub browser: a panel that shows the address and a button that opens it in the default browser.
    /// It never raises Navigated, so the form cannot close itself after a game starts; the user closes it.
    /// </summary>
    public sealed class ExternalGameBrowser : Panel, IPs3GameBrowser
    {
        private readonly Label _label;
        private readonly Button _button;
        private string _currentUrl;

        public ExternalGameBrowser()
        {
            _label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 60,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "An embedded browser is not available in this build."
            };
            _button = new Button
            {
                AutoSize = true,
                Text = "Open games list in the default browser",
                Anchor = AnchorStyles.Top
            };
            _button.Click += delegate
            {
                if (!ToolFormSupport.OpenUrl(_currentUrl))
                {
                    ToolFormSupport.ShowError(this, "Error", "Unable to open the games list.");
                }
            };

            Controls.Add(_button);
            Controls.Add(_label);
        }

        public Control View
        {
            get { return this; }
        }

        public string CurrentUrl
        {
            get { return _currentUrl; }
        }

#pragma warning disable 0067
        public event EventHandler<GameBrowserNavigatedEventArgs> Navigated;
#pragma warning restore 0067

        public void Navigate(string url)
        {
            _currentUrl = url;
            _label.Text = "An embedded browser is not available in this build.\n" + url;
            _button.Left = (ClientSize.Width - _button.Width) / 2;
            _button.Top = _label.Height + 8;
        }

        public Task HideElementsAsync(params string[] elementIds)
        {
            return Task.CompletedTask;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            _button.Left = (ClientSize.Width - _button.Width) / 2;
        }
    }
}
