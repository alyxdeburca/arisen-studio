using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Services.M7
{
    // M7 boundary: WebView2 (Microsoft.Web.WebView2, Windows-only, needs the Edge WebView2 runtime).
    // Old uses, all in forms:
    //  - ReportIssueDialog, RequestModsDialog: Source = form URL; NavigationStarting shows a progress panel;
    //    NavigationCompleted hides it and, on success, stops a retry timer.
    //  - PS3 GameLauncher: Source = "http://<console>/games.ps3?"; after a successful navigation turn off accelerator keys
    //    and the context menu, run three scripts that hide page elements (iwrap, search, menu-icon), redirect mount.ps3 to
    //    play.ps3, and close the form on play.ps3.
    //  - MainWindow poll panel: wait for init, NavigateToString(PollLink), run a script that hides scrollbars, cancel
    //    BackOrForward and Reload navigations, set body overflow hidden after each navigation.
    // The Mono dialogs already say "The Windows-only WebView2 from the old dialog is not used here", so this boundary is
    // for a later real host (for example a CEF or WebKit control) and for the poll panel and PS3 launcher.

    public enum WebNavigationKind
    {
        Other = 0,
        Reload = 1,
        BackOrForward = 2
    }

    public sealed class WebNavigationStartingEventArgs : EventArgs
    {
        public Uri Uri { get; set; }

        public WebNavigationKind Kind { get; set; }

        /// <summary>Set true to stop the navigation (the old poll panel did this for BackOrForward and Reload).</summary>
        public bool Cancel { get; set; }
    }

    public sealed class WebNavigationCompletedEventArgs : EventArgs
    {
        public bool IsSuccess { get; set; }

        /// <summary>Null on success. Why it failed, for a stub or an adapter that has a reason.</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// An embedded web page. Control is the visual to dock on a form.
    /// </summary>
    public interface IWebPageHost : IDisposable
    {
        Control Control { get; }

        /// <summary>False for a stub. A caller can show a link to the page in the system browser instead.</summary>
        bool IsAvailable { get; }

        /// <summary>The current or last requested address. Null before the first navigation.</summary>
        Uri Source { get; }

        /// <summary>Old: CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled. Applied once the page is ready.</summary>
        bool AcceleratorKeysEnabled { get; set; }

        /// <summary>Old: CoreWebView2.Settings.AreDefaultContextMenusEnabled.</summary>
        bool DefaultContextMenusEnabled { get; set; }

        event EventHandler<WebNavigationStartingEventArgs> NavigationStarting;

        event EventHandler<WebNavigationCompletedEventArgs> NavigationCompleted;

        /// <summary>Old: EnsureCoreWebView2Async. Returns false when no engine is available. Does not throw.</summary>
        Task<bool> EnsureInitializedAsync();

        /// <summary>Old: Source = new Uri(...).</summary>
        void Navigate(Uri uri);

        /// <summary>Old: CoreWebView2.NavigateToString(html).</summary>
        void NavigateToString(string html);

        /// <summary>Old: ExecuteScriptAsync. Returns the JSON text of the script result, or null.</summary>
        Task<string> ExecuteScriptAsync(string script);
    }

    /// <summary>
    /// Makes web page hosts, so a form does not name a concrete engine.
    /// </summary>
    public interface IWebPageHostFactory
    {
        IWebPageHost Create();
    }

    /// <summary>
    /// Stub host: a panel with a text label that says the embedded browser is not available. Navigate and NavigateToString
    /// record the address, raise NavigationStarting (honouring Cancel) and then NavigationCompleted with IsSuccess false and
    /// an ErrorMessage. EnsureInitializedAsync returns false. ExecuteScriptAsync returns null.
    /// </summary>
    public sealed class UnavailableWebPageHost : IWebPageHost
    {
        public const string Message = "The embedded web page is not available in this build (no WebView2 on this platform).";

        private readonly Panel _panel;
        private readonly Label _label;

        public UnavailableWebPageHost()
        {
            _label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = Message
            };
            _panel = new Panel { Dock = DockStyle.Fill };
            _panel.Controls.Add(_label);
            DefaultContextMenusEnabled = true;
            AcceleratorKeysEnabled = true;
        }

        public Control Control
        {
            get { return _panel; }
        }

        public bool IsAvailable
        {
            get { return false; }
        }

        public Uri Source { get; private set; }

        public bool AcceleratorKeysEnabled { get; set; }

        public bool DefaultContextMenusEnabled { get; set; }

        public event EventHandler<WebNavigationStartingEventArgs> NavigationStarting;

        public event EventHandler<WebNavigationCompletedEventArgs> NavigationCompleted;

        public Task<bool> EnsureInitializedAsync()
        {
            return Task.FromResult(false);
        }

        public void Navigate(Uri uri)
        {
            Source = uri;
            _label.Text = Message + Environment.NewLine + (uri == null ? string.Empty : uri.ToString());
            Raise(uri);
        }

        public void NavigateToString(string html)
        {
            Source = null;
            _label.Text = Message;
            Raise(null);
        }

        public Task<string> ExecuteScriptAsync(string script)
        {
            return Task.FromResult<string>(null);
        }

        private void Raise(Uri uri)
        {
            bool cancel = false;
            EventHandler<WebNavigationStartingEventArgs> starting = NavigationStarting;
            if (starting != null)
            {
                WebNavigationStartingEventArgs args = new WebNavigationStartingEventArgs { Uri = uri, Kind = WebNavigationKind.Other };
                starting(this, args);
                cancel = args.Cancel;
            }

            if (cancel)
            {
                return;
            }

            EventHandler<WebNavigationCompletedEventArgs> completed = NavigationCompleted;
            if (completed != null)
            {
                completed(this, new WebNavigationCompletedEventArgs { IsSuccess = false, ErrorMessage = Message });
            }
        }

        public void Dispose()
        {
            _panel.Dispose();
        }
    }

    /// <summary>
    /// Stub factory: every call returns a new UnavailableWebPageHost. Default of M7Services.WebPages.
    /// </summary>
    public sealed class UnavailableWebPageHostFactory : IWebPageHostFactory
    {
        public IWebPageHost Create()
        {
            return new UnavailableWebPageHost();
        }
    }
}
