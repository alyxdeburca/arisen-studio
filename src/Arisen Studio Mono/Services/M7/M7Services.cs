using System;

namespace ArisenStudio.Services.M7
{
    /// <summary>
    /// One place for the five Windows-only boundaries (XDevkit/JRPC, AutoUpdater, WebView2, ads). Each property holds the
    /// documented stub until the host assigns a real implementation at startup (for example in Program.cs). Callers read
    /// the property when they need it; none of them names a concrete implementation.
    /// A null assignment is refused, so a caller never has to check for null.
    /// </summary>
    public static class M7Services
    {
        private static IXboxConsoleConnector _xbox = UnavailableXboxConsoleConnector.Instance;
        private static IApplicationUpdater _updater = new NoOpApplicationUpdater();
        private static IWebPageHostFactory _webPages = new UnavailableWebPageHostFactory();
        private static IAdBannerPolicy _ads = new AdsDisabledBannerPolicy();

        /// <summary>Finds and connects to Xbox 360 consoles. Default: fails with a message.</summary>
        public static IXboxConsoleConnector Xbox
        {
            get { return _xbox; }
            set { _xbox = value ?? throw new ArgumentNullException("value"); }
        }

        /// <summary>Update check and install. Default: reports "no update available".</summary>
        public static IApplicationUpdater Updater
        {
            get { return _updater; }
            set { _updater = value ?? throw new ArgumentNullException("value"); }
        }

        /// <summary>Makes embedded web page hosts. Default: a panel saying it is not available.</summary>
        public static IWebPageHostFactory WebPages
        {
            get { return _webPages; }
            set { _webPages = value ?? throw new ArgumentNullException("value"); }
        }

        /// <summary>Ad banner policy. Default: ads off.</summary>
        public static IAdBannerPolicy Ads
        {
            get { return _ads; }
            set { _ads = value ?? throw new ArgumentNullException("value"); }
        }
    }
}
