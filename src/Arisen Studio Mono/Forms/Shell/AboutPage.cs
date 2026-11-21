using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dialogs;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// The About page (S7; old PageAbout, MW3-137 to MW3-147): title, version, licence line, social links, Donate button and
    /// the credits (third-party libraries, contributors, translators). Stock controls, no icons (the old page used SVG
    /// icons for the logo and the social links; D7/D8). Clicking the title opens the easter egg, as the old logo did.
    /// Links open through OpenUrl. UI thread only.
    /// </summary>
    internal sealed class AboutPage : UserControl, IShellPage
    {
        internal const string LicenseUrl = "https://github.com/OhhSoWzrd/arisen-studio/blob/main/LICENSE.md/";

        // Credit lists copied from the old MainWindow resources (LabelCredits*.Text). An empty address means plain text.
        private static readonly string[][] Libraries =
        {
            new[] { "AutoUpdater.NET", "https://github.com/ravibpatel/AutoUpdater.NET/" },
            new[] { "CodeHollow.FeedReader", "https://github.com/arminreiter/FeedReader/" },
            new[] { "Discord RPC", "https://github.com/Lachee/discord-rpc-csharp" },
            new[] { "DevExpress", "https://devexpress.com/" },
            new[] { "FluentFTP", "https://github.com/robinrodricks/FluentFTP" },
            new[] { "HtmlAgilityPack", "https://html-agility-pack.net/" },
            new[] { "Humanizer", "https://github.com/Humanizr/Humanizer" },
            new[] { "Icons8", "https://icons8.com/" },
            new[] { "INI File Parser", "https://github.com/rickyah/ini-parser" },
            new[] { "ISOLib", "https://github.com/PureIso" },
            new[] { "Newtonsoft.Json", "https://newtonsoft.com/json" },
            new[] { "NLog", "https://nlog-project.org/" },
            new[] { "PARAM.SFO", "https://www.aldostools.org/" },
            new[] { "PS3Lib", "https://github.com/iMCSx/PS3Lib" },
            new[] { "webMAN MOD", "https://github.com/aldostools/webMAN-MOD" },
            new[] { "WebView2", "https://aka.ms/webview" },
            new[] { "XDevkit", "https://microsoft.com/" }
        };

        private static readonly string[][] Contributors =
        {
            new[] { "ambientdisplay", "" },
            new[] { "asen0776", "" },
            new[] { "basimahmed", "https://www.instagram.com/basimahmed114" },
            new[] { "crystall1nedev", "https://github.com/crystall1nedev" },
            new[] { "EgoMaw", "https://github.com/EgoMaw" },
            new[] { "goldug", "http://djopposite.se/" },
            new[] { "NCKwolf", "https://twitter.com/NCKwolf" },
            new[] { "TheHiddenHour", "https://github.com/TheHiddenHour/" },
            new[] { "oSerenity", "https://github.com/oSerenity" },
            new[] { "tsuiarts", "mailto:nurishafa26@gmail.com" }
        };

        private static readonly string[][] Translators =
        {
            new[] { "goldug", "http://djopposite.se/" },
            new[] { "Kloido", "mailto:jaortiz2026@gmail.com" },
            new[] { "NCKwolf", "https://twitter.com/NCKwolf/" },
            new[] { "Serdar Sağlam", "mailto:serdarsaglam@msn.com" },
            new[] { "z1burg3", "https://twitter.com/Z1bUrG3/" }
        };

        private readonly Label _title;
        private readonly Label _version;

        public AboutPage()
        {
            AutoScroll = true;
            Font = SystemFonts.MessageBoxFont;

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(22, 16, 22, 16)
            };

            _title = new Label
            {
                Name = "LabelAboutTitle",
                Text = "Arisen Studio",
                AutoSize = true,
                Font = new Font(Font.FontFamily, Font.Size * 2f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _title.Click += (s, e) => ShowEasterEgg();
            flow.Controls.Add(_title);

            _version = new Label
            {
                Name = "LabelAboutVersion",
                Text = UpdateExtensions.CurrentVersionName,
                AutoSize = true
            };
            flow.Controls.Add(_version);

            flow.Controls.Add(new Label { Name = "LabelAboutSubTitle", Text = "©2024 OhhSoWzrd", AutoSize = true, Margin = new Padding(3, 11, 3, 0) });
            LinkLabel license = new LinkLabel { Name = "LabelAboutLicense", Text = "Released under MIT License", AutoSize = true };
            license.LinkArea = new LinkArea("Released under ".Length, "MIT".Length);
            license.LinkClicked += (s, e) => Open(LicenseUrl);
            flow.Controls.Add(license);

            FlowLayoutPanel social = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(3, 11, 3, 3) };
            AddSocial(social, "ImageSocialWebsite", "Website", Urls.ProjectWebsite);
            AddSocial(social, "ImageSocialDiscord", "Discord", Urls.DiscordServer);
            AddSocial(social, "ImageSocialTwitter", "Twitter", Urls.TwitterProfile);
            AddSocial(social, "ImageSocialGitHub", "GitHub", Urls.GitHubRepo);
            flow.Controls.Add(social);

            Button donate = new Button { Name = "ButtonDonate", Text = "Donate", AutoSize = true, Margin = new Padding(3, 11, 3, 3) };
            donate.Click += (s, e) => Open(Urls.DonatePatreon);
            flow.Controls.Add(donate);

            flow.Controls.Add(new Label { Name = "LabelHeaderAboutCredits", Text = "Credits", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 22, 3, 3) });
            AddCredits(flow, "Third-Party Libraries", Libraries);
            AddCredits(flow, "Contributors", Contributors);
            AddCredits(flow, "Translators", Translators);

            Controls.Add(flow);
        }

        /// <summary>
        /// Opens an address (http, https or mailto). Replaceable so tests never start a browser.
        /// </summary>
        public Action<string> OpenUrl { get; set; } = url =>
        {
            _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        };

        /// <summary>
        /// Sets the version line: "Beta v3.0.0 (21/11/2026)" with a release date, "Beta v3.0.0" without (MW3-137). UI thread only.
        /// </summary>
        public void SetVersion(string versionName, DateTime? releaseDate)
        {
            string name = string.IsNullOrEmpty(versionName) ? UpdateExtensions.CurrentVersionName : versionName;
            _version.Text = releaseDate.HasValue ? name + " (" + releaseDate.Value.ToShortDateString() + ")" : name;
        }

        public void OnPageShown()
        {
            // Nothing to load: the version line is set by the host through SetVersion.
        }

        private void AddSocial(FlowLayoutPanel panel, string name, string text, string url)
        {
            LinkLabel link = new LinkLabel { Name = name, Text = text, AutoSize = true, Margin = new Padding(3, 3, 14, 3) };
            link.LinkClicked += (s, e) => Open(url);
            panel.Controls.Add(link);
        }

        private void AddCredits(FlowLayoutPanel flow, string header, string[][] entries)
        {
            flow.Controls.Add(new Label { Text = header, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 11, 3, 3) });
            foreach (string[] entry in entries)
            {
                string url = entry[1];
                if (url.Length == 0)
                {
                    flow.Controls.Add(new Label { Text = entry[0], AutoSize = true, Margin = new Padding(3, 0, 3, 0) });
                    continue;
                }
                LinkLabel link = new LinkLabel { Text = entry[0], AutoSize = true, Margin = new Padding(3, 0, 3, 0) };
                link.LinkClicked += (s, e) => Open(url);
                flow.Controls.Add(link);
            }
        }

        private void Open(string url)
        {
            Action<string> open = OpenUrl;
            if (open == null)
            {
                return;
            }
            try
            {
                open(url);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, "Could not open " + url + ": " + ex.Message, "About", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowEasterEgg()
        {
            using (EasterEgg dialog = new EasterEgg())
            {
                _ = dialog.ShowDialog(FindForm());
            }
        }
    }
}
