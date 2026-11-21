using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Shows the webMAN games list of the PS3 so a game can be mounted and started. When a game is mounted the form
    /// asks for the play page, and closes once the console has been told to play.
    /// Stock WinForms port of the old DevExpress GameLauncher form. The embedded web page (WebView2) is not ported;
    /// it sits behind IPs3GameBrowser.
    /// Host hooks, set before the form is shown:
    ///  - ConsoleAddress: the address (IP) of the console running webMAN.
    ///  - Browser: the embedded browser. Default: an ExternalGameBrowser stub, which only offers to open the list in
    ///    the default browser (and never closes the form by itself).
    /// </summary>
    public class GameLauncher : Form
    {
        private readonly ToolStripStatusLabel _status;
        private readonly Panel _host;
        private IPs3GameBrowser _browser;

        public GameLauncher()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;

            _host = new Panel { Dock = DockStyle.Fill };
            Controls.Add(_host);
            _status = ToolFormSupport.AddStatusBar(this);

            Text = "Game Launcher";
            MinimumSize = new Size(h * 30, h * 20);
            ClientSize = new Size(h * 50, h * 34);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Address (IP) of the console running webMAN.
        /// </summary>
        public string ConsoleAddress { get; set; }

        /// <summary>
        /// The embedded browser. Null (the default) uses the ExternalGameBrowser stub. Set it before the form is shown.
        /// </summary>
        public IPs3GameBrowser Browser { get; set; }

        private string Url(string page)
        {
            return "http://" + ConsoleAddress + "/" + page;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Text = ToolFormSupport.Lookup(Language, "GAMES_LAUNCHER", "Game Launcher");

            try
            {
                if (string.IsNullOrWhiteSpace(ConsoleAddress))
                {
                    throw new InvalidOperationException("ConsoleAddress is not set.");
                }

                SetStatus("Loading games...");

                _browser = Browser ?? new ExternalGameBrowser();
                _browser.View.Dock = DockStyle.Fill;
                _host.Controls.Add(_browser.View);
                _browser.Navigated += Browser_Navigated;
                _browser.Navigate(Url("games.ps3?"));

                SetStatus("Successfully loaded games.");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Game Launcher (PS3): unable to load games");
                SetStatus("Game Launcher (PS3): Unable to load games");
                ToolFormSupport.ShowError(this, ToolFormSupport.Lookup(Language, "ERROR", "Error"), "Unable to load games");
                Close();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_browser != null)
            {
                _browser.Navigated -= Browser_Navigated;
            }

            base.OnFormClosed(e);
        }

        private async void Browser_Navigated(object sender, GameBrowserNavigatedEventArgs e)
        {
            if (!e.Success)
            {
                return;
            }

            try
            {
                await _browser.HideElementsAsync("iwrap", "search", "menu-icon");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Game Launcher (PS3): unable to hide page elements.");
            }

            string url = e.Url ?? string.Empty;

            if (url.Contains("mount.ps3"))
            {
                _browser.Navigate(Url("play.ps3?"));
            }
            else if (url.Contains("play.ps3"))
            {
                Close();
            }
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
