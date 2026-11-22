using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Forms.Tools.PS3;
using ArisenStudio.Runtime;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Managed <see cref="IPs3GameBrowser" /> for the PS3 Game Launcher. Replaces the old WebView2 form
    /// (Forms/Tools/PS3/GameLauncher.cs), which set the page source to http://{address}/games.ps3? and, after a
    /// load, hid "iwrap", "search" and "menu-icon", moved to play.ps3 after mount.ps3 and closed after play.ps3.
    ///
    /// The constructor opens no connection and does no I/O. Navigate does not fetch the page and sends no webMAN
    /// command: it validates the address, records it as <see cref="CurrentUrl" /> and shows it with a button that
    /// opens it in the default browser. Each member catches its exceptions and returns its failure value.
    ///
    /// Gaps (not implemented, see the build request): no page engine, so <see cref="Navigated" /> is never raised
    /// and <see cref="HideElementsAsync" /> does nothing. The FTP service is kept but no member uses it.
    /// Not compiled, not wired.
    /// </summary>
    public sealed class WebManPs3GameBrowser : IPs3GameBrowser
    {
        private readonly string _address;
        private readonly FtpService _ftp;
        private readonly object _gate = new object();
        private string _currentUrl;
        private Panel _view;
        private Label _label;
        private Button _openButton;

#pragma warning disable 0067
        /// <summary>Never raised: there is no page engine to report a page load (gap).</summary>
        public event EventHandler<GameBrowserNavigatedEventArgs> Navigated;
#pragma warning restore 0067

        /// <param name="address">Console address, for example 192.168.0.69. Stored only; no connection is made.</param>
        /// <param name="ftp">Optional FTP service. Stored for later use; no member needs it yet.</param>
        public WebManPs3GameBrowser(string address, FtpService ftp = null)
        {
            _address = address;
            _ftp = ftp;
        }

        /// <summary>The console address this browser was built for.</summary>
        public string Address
        {
            get { return _address; }
        }

        /// <summary>The FTP service passed to the constructor, or null.</summary>
        public FtpService Ftp
        {
            get { return _ftp; }
        }

        /// <summary>
        /// Panel to dock into the form. Created on first use. Returns null if the controls cannot be created.
        /// </summary>
        public Control View
        {
            get
            {
                try
                {
                    lock (_gate)
                    {
                        if (_view == null)
                        {
                            BuildView();
                        }

                        return _view;
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, "Unable to create the PS3 game browser view.");
                    return null;
                }
            }
        }

        /// <summary>The last address passed to Navigate, or null.</summary>
        public string CurrentUrl
        {
            get
            {
                try
                {
                    lock (_gate)
                    {
                        return _currentUrl;
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, "Unable to read the PS3 game browser address.");
                    return null;
                }
            }
        }

        /// <summary>
        /// Records an http or https address and shows it. No request is sent. A value that is not an absolute
        /// http(s) address leaves <see cref="CurrentUrl" /> unchanged and is logged.
        /// </summary>
        public void Navigate(string url)
        {
            try
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException("Not an absolute http or https address: " + url, "url");
                }

                lock (_gate)
                {
                    _currentUrl = uri.AbsoluteUri;

                    if (_label != null)
                    {
                        _label.Text = "Games list (no embedded browser in this build)\n" + _currentUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex, "Unable to set the PS3 games list address.");
            }
        }

        /// <summary>
        /// Gap: the element ids are not hidden, because there is no page to change. Completes at once, as the
        /// existing ExternalGameBrowser does.
        /// </summary>
        public Task HideElementsAsync(params string[] elementIds)
        {
            return Task.CompletedTask;
        }

        private void BuildView()
        {
            _label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 60,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "Games list (no embedded browser in this build)\nhttp://" + _address + "/games.ps3?"
            };

            _openButton = new Button
            {
                AutoSize = true,
                Text = "Open games list in the default browser",
                Anchor = AnchorStyles.Top
            };

            _openButton.Click += delegate
            {
                string url;

                lock (_gate)
                {
                    url = _currentUrl;
                }

                if (string.IsNullOrEmpty(url) || !ToolFormSupport.OpenUrl(url))
                {
                    ToolFormSupport.ShowError(_view, "Error", "Unable to open the games list.");
                }
            };

            _view = new Panel();
            _view.Controls.Add(_openButton);
            _view.Controls.Add(_label);
            _view.Resize += delegate
            {
                _openButton.Left = (_view.ClientSize.Width - _openButton.Width) / 2;
                _openButton.Top = _label.Height + 8;
            };
        }

        private static void Log(Exception ex, string message)
        {
            Action<Exception, string> logger = AppHost.Logger;

            if (logger != null)
            {
                logger(ex, message);
            }
        }
    }
}
