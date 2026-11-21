using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Controls;

namespace ArisenStudio.Forms.Dashboard
{
    /// <summary>
    /// The dashboard page (slice S2): favourite games and mods tiles, latest news, announcements, change log and
    /// statistics. The ad banner and the poll web view are not ported (decisions D3, D4).
    ///
    /// Use: create the page, set <see cref="DataSource"/> (and optionally <see cref="Platform"/>,
    /// <see cref="UseRelativeTimes"/>, <see cref="Localizer"/>), subscribe to the events, add it to the shell's page
    /// host, then call <see cref="ReloadAll"/>. Call <see cref="Platform"/> again whenever the connected profile
    /// changes (ledger MW3-149). Every <c>Load*</c> method is safe to call again at any time.
    /// The page never opens links, dialogs or other pages itself; it raises events and the shell acts.
    /// </summary>
    public class DashboardPage : UserControl
    {
        private const string GamesGroup = "games";
        private const string ModsGroup = "mods";

        private const string RefreshText = "Refresh";
        private const string PreviousText = "Previous";
        private const string NextText = "Next";

        private IDashboardDataSource dataSource = new StubDashboardDataSource();
        private DashboardPlatform? platform;
        private bool useRelativeTimes;
        private Func<string, string> localizer;

        private readonly Panel favouritesHost = new Panel();
        private TilePanel favourites;
        private readonly List<Image> ownedImages = new List<Image>();

        private readonly Label headerNews = new Label();
        private readonly Label headerAnnouncements = new Label();
        private readonly Label headerChangeLog = new Label();
        private readonly Label headerStatistics = new Label();
        private readonly LinkLabel linkRefreshNews = new LinkLabel();
        private readonly Button buttonPrevious = new Button();
        private readonly Button buttonNext = new Button();

        private readonly Panel newsHost = new Panel();
        private readonly Panel announcementsItemsHost = new Panel();
        private readonly NoAnnouncementsItem noAnnouncements = new NoAnnouncementsItem();
        private readonly Panel changeLogHost = new Panel();
        private readonly FlowLayoutPanel statsHost = new FlowLayoutPanel();
        private readonly Label labelStatsSummary = new Label();

        private IList<ReleaseInfo> releases = new List<ReleaseInfo>();
        private int releaseIndex;
        private int newsTicket;
        private Font headerFont;

        /// <summary>A favourite game tile was clicked. The shell opens the matching page and filter (ledger MW1-097).</summary>
        public event EventHandler<FavoriteGameEventArgs> FavoriteGameClicked;

        /// <summary>A favourite mod tile was clicked. The shell opens the mod details dialog (ledger MW1-100, MW3-113).</summary>
        public event EventHandler<FavoriteModEventArgs> FavoriteModClicked;

        /// <summary>
        /// An announcement was dismissed. The page has already told <see cref="DataSource"/>; this is a notification
        /// so the shell can save its settings.
        /// </summary>
        public event EventHandler<AnnouncementDismissedEventArgs> AnnouncementDismissed;

        /// <summary>A URL in a news item, announcement or the change log was clicked. The shell opens it (LinkText is the URL).</summary>
        public event LinkClickedEventHandler LinkClicked;

        /// <summary>A load failed or similar. The shell may show Message in its status bar.</summary>
        public event EventHandler<DashboardStatusEventArgs> StatusMessage;

        public DashboardPage()
        {
            SuspendLayout();

            Font = SystemFonts.MessageBoxFont;
            headerFont = new Font(Font, FontStyle.Bold);
            int gap = Math.Max(4, Font.Height / 2);
            Padding = new Padding(gap);

            // Favourites: tile groups are rebuilt on every load.
            favouritesHost.Dock = DockStyle.Fill;

            // News card.
            ConfigureHeader(headerNews);
            linkRefreshNews.AutoSize = true;
            linkRefreshNews.Text = RefreshText;
            linkRefreshNews.LinkClicked += LinkRefreshNews_LinkClicked;
            ConfigureScrollHost(newsHost);

            // Announcements card: the placeholder shows when there are no items.
            ConfigureHeader(headerAnnouncements);
            ConfigureScrollHost(announcementsItemsHost);
            noAnnouncements.Dock = DockStyle.Fill;
            Panel announcementsBody = new Panel();
            announcementsBody.BorderStyle = BorderStyle.FixedSingle;
            announcementsBody.Controls.Add(announcementsItemsHost);
            announcementsBody.Controls.Add(noAnnouncements);
            announcementsItemsHost.BorderStyle = BorderStyle.None;

            // Change log card.
            ConfigureHeader(headerChangeLog);
            buttonPrevious.Text = PreviousText;
            buttonPrevious.AutoSize = true;
            buttonPrevious.Click += ButtonPrevious_Click;
            buttonNext.Text = NextText;
            buttonNext.AutoSize = true;
            buttonNext.Click += ButtonNext_Click;
            ConfigureScrollHost(changeLogHost);

            // Statistics card.
            ConfigureHeader(headerStatistics);
            statsHost.Dock = DockStyle.Fill;
            statsHost.AutoScroll = true;
            statsHost.FlowDirection = FlowDirection.LeftToRight;
            statsHost.WrapContents = true;
            labelStatsSummary.AutoSize = true;
            labelStatsSummary.UseMnemonic = false;
            labelStatsSummary.Dock = DockStyle.Fill;
            TableLayoutPanel statsBody = new TableLayoutPanel();
            statsBody.ColumnCount = 1;
            statsBody.RowCount = 2;
            statsBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statsBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statsBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            statsBody.Controls.Add(statsHost, 0, 0);
            statsBody.Controls.Add(labelStatsSummary, 0, 1);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 3;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            Control favouritesSection = MakeSection(null, favouritesHost, gap);
            Control newsSection = MakeSection(MakeHeaderRow(headerNews, linkRefreshNews), newsHost, gap);
            Control announcementsSection = MakeSection(MakeHeaderRow(headerAnnouncements), announcementsBody, gap);
            Control changeLogSection = MakeSection(MakeHeaderRow(headerChangeLog, buttonPrevious, buttonNext), changeLogHost, gap);
            Control statsSection = MakeSection(MakeHeaderRow(headerStatistics), statsBody, gap);

            root.Controls.Add(favouritesSection, 0, 0);
            root.SetRowSpan(favouritesSection, 2);
            root.Controls.Add(newsSection, 1, 0);
            root.Controls.Add(announcementsSection, 1, 1);
            root.Controls.Add(changeLogSection, 2, 0);
            root.Controls.Add(statsSection, 2, 1);
            Controls.Add(root);

            ApplyHeaderTexts();
            ShowEmptyChangeLog();
            UpdateAnnouncementsEmptyState();

            ResumeLayout(true);
        }

        /// <summary>
        /// Where the page reads its data. Setting null installs the empty <see cref="StubDashboardDataSource"/>.
        /// Setting it does not reload; call <see cref="ReloadAll"/>.
        /// </summary>
        public IDashboardDataSource DataSource
        {
            get { return dataSource; }
            set { dataSource = value ?? new StubDashboardDataSource(); }
        }

        /// <summary>
        /// Platform of the connected profile. Favourites are filtered by it; null shows every platform.
        /// Setting it reloads the favourites and the games header (ledger MW3-149).
        /// </summary>
        public DashboardPlatform? Platform
        {
            get { return platform; }
            set
            {
                platform = value;
                LoadFavourites();
            }
        }

        /// <summary>
        /// Show "5 minutes ago" style times (the old Settings.UseRelativeTimes). Takes effect on the next load.
        /// </summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set { useRelativeTimes = value; }
        }

        /// <summary>
        /// Optional text lookup, called with the old resource keys (OUR_FAVORITE_GAMES, OUR_FAVORITE_CATEGORIES,
        /// OUR_FAVORITE_MODS, LATEST_NEWS, ANNOUNCEMENTS, CHANGE_LOG, STATISTICS, STATS_DOWNLOADS, LAST_UPDATED,
        /// NO_ANNOUNCEMENTS). A null or empty answer falls back to the English text. Call <see cref="ApplyLanguage"/>
        /// after changing it.
        /// </summary>
        public Func<string, string> Localizer
        {
            get { return localizer; }
            set { localizer = value; }
        }

        /// <summary>
        /// Loads everything: favourites, announcements, change log, statistics and (in the background) the news
        /// (the old InitializeFinished calls, ledger MW1-105 to MW1-109).
        /// </summary>
        public void ReloadAll()
        {
            LoadFavourites();
            LoadAnnouncements();
            LoadChangeLog();
            LoadStatistics();
            StartNewsLoad();
        }

        /// <summary>Re-applies the captions from <see cref="Localizer"/> and reloads the parts that carry text (ledger MW2-029).</summary>
        public void ApplyLanguage()
        {
            ApplyHeaderTexts();
            LoadFavourites();
            LoadAnnouncements();
            LoadChangeLog();
            LoadStatistics();
            StartNewsLoad();
        }

        /// <summary>
        /// Rebuilds the "Our Favorite Games" and "Our Favorite Mods" tiles from the data source
        /// (ledger MW1-095 to MW1-100).
        /// </summary>
        public void LoadFavourites()
        {
            DisposeFavourites();

            TilePanel panel = new TilePanel();
            panel.Dock = DockStyle.Fill;
            panel.AddGroup(GamesGroup, GamesHeaderText());
            panel.AddGroup(ModsGroup, Localize("OUR_FAVORITE_MODS", "Our Favorite Mods"));
            favourites = panel;
            favouritesHost.Controls.Add(panel);

            IList<FavoriteGameInfo> games = null;
            try
            {
                games = dataSource.GetFavoriteGames(platform);
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load favourite games: " + ex.Message, ex);
            }

            if (games != null)
            {
                foreach (FavoriteGameInfo game in games)
                {
                    FavoriteGameInfo captured = game;
                    string subtitle = captured.TotalCount.ToString("N0") + (captured.Platform == DashboardPlatform.PS4 ? " Total Homebrew" : " Total Game Mods");
                    TileCard card = panel.AddTile(GamesGroup, captured.CategoryTitle, subtitle, TileSize.Wide, null,
                        delegate { FavoriteGameClicked?.Invoke(this, new FavoriteGameEventArgs(captured)); });
                    if (!string.IsNullOrWhiteSpace(captured.ImageUrl))
                    {
                        LoadTileImage(card, captured.ImageUrl, panel);
                    }
                }
            }

            IList<FavoriteModInfo> mods = null;
            try
            {
                mods = dataSource.GetFavoriteMods(platform);
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load favourite mods: " + ex.Message, ex);
            }

            if (mods != null)
            {
                foreach (FavoriteModInfo mod in mods)
                {
                    FavoriteModInfo captured = mod;
                    panel.AddTile(ModsGroup, captured.CategoryTitle, captured.ModName, TileSize.Wide, null,
                        delegate { FavoriteModClicked?.Invoke(this, new FavoriteModEventArgs(captured)); });
                }
            }
        }

        /// <summary>
        /// Reloads the news feed. The feed is read in the background; the list is replaced when it arrives and is left
        /// as it was if the read fails (ledger MW1-106, MW1-112). Awaiting the task is optional.
        /// </summary>
        public async Task LoadNewsAsync()
        {
            int ticket = ++newsTicket;
            IList<NewsEntry> entries;
            try
            {
                entries = await dataSource.GetNewsAsync();
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load the news feed: " + ex.Message, ex);
                return;
            }

            // A newer load started, or the page is gone: drop this result.
            if (ticket != newsTicket || IsDisposed)
            {
                return;
            }

            ClearHost(newsHost);
            if (entries == null)
            {
                return;
            }

            newsHost.SuspendLayout();
            foreach (NewsEntry entry in entries)
            {
                NewsItem item = new NewsItem(entry.Title, entry.Description, entry.Published);
                item.UseRelativeTimes = useRelativeTimes;
                item.Dock = DockStyle.Top;
                item.HyperlinkClick += ForwardLink;
                AddStacked(newsHost, item);
            }
            newsHost.ResumeLayout(true);
        }

        /// <summary>
        /// Rebuilds the announcements list: newest first, skipping dismissed ones and ones outside their date window
        /// (ledger MW1-105, MW1-110, MW1-111).
        /// </summary>
        public void LoadAnnouncements()
        {
            ClearHost(announcementsItemsHost);

            IList<AnnouncementInfo> all = null;
            try
            {
                all = dataSource.GetAnnouncements();
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load announcements: " + ex.Message, ex);
            }

            if (all != null)
            {
                // The old code reversed the data source's own list in place, so the order flipped on every call.
                // This reverses a copy, so the order is the same every time.
                List<AnnouncementInfo> ordered = new List<AnnouncementInfo>(all);
                ordered.Reverse();

                DateTime now = DateTime.Now;
                announcementsItemsHost.SuspendLayout();
                foreach (AnnouncementInfo announcement in ordered)
                {
                    if (dataSource.IsAnnouncementDismissed(announcement.Id))
                    {
                        continue;
                    }

                    if (now < announcement.DateStart || now > announcement.DateEnd)
                    {
                        continue;
                    }

                    AnnouncementItem item = new AnnouncementItem(announcement.Id, announcement.Title, announcement.Message, announcement.DateStart);
                    item.UseRelativeTimes = useRelativeTimes;
                    item.Dock = DockStyle.Top;
                    item.HyperlinkClick += ForwardLink;
                    item.Dismissed += AnnouncementItem_Dismissed;
                    AddStacked(announcementsItemsHost, item);
                }
                announcementsItemsHost.ResumeLayout(true);
            }

            UpdateAnnouncementsEmptyState();
        }

        /// <summary>Shows the newest release on the change log card and resets the Previous and Next buttons (ledger MW1-109).</summary>
        public void LoadChangeLog()
        {
            IList<ReleaseInfo> loaded = null;
            try
            {
                loaded = dataSource.GetReleases();
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load the change log: " + ex.Message, ex);
            }

            releases = loaded ?? new List<ReleaseInfo>();
            releaseIndex = 0;
            ShowRelease();
        }

        /// <summary>Rebuilds the statistics card (ledger MW1-107).</summary>
        public void LoadStatistics()
        {
            ClearHost(statsHost);

            DashboardStats stats = null;
            try
            {
                stats = dataSource.GetStatistics();
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load statistics: " + ex.Message, ex);
            }

            if (stats == null)
            {
                labelStatsSummary.Text = string.Empty;
                return;
            }

            statsHost.SuspendLayout();
            foreach (StatGroup group in stats.Groups)
            {
                StatItem item = new StatItem();
                item.Title = group.Title;
                List<string> lines = new List<string>();
                foreach (StatLine line in group.Lines)
                {
                    lines.Add(line.Count.ToString("N0") + " " + line.Label);
                }
                item.Value = string.Join(Environment.NewLine, lines.ToArray());
                item.Height = item.Height + Math.Max(0, lines.Count - 1) * item.Font.Height;
                statsHost.Controls.Add(item);
            }
            statsHost.ResumeLayout(true);

            string summary = string.Format(Localize("STATS_DOWNLOADS", "{0} Downloads"), stats.TotalDownloads.ToString("N0"));
            if (stats.LastUpdated.HasValue)
            {
                summary += Environment.NewLine + Localize("LAST_UPDATED", "Last Updated") + ": " + stats.LastUpdated.Value.ToLocalTime().ToShortDateString();
            }
            labelStatsSummary.Text = summary;
        }

        // ---- Header, section and host helpers -------------------------------------------------------------

        private void ConfigureHeader(Label label)
        {
            label.AutoSize = true;
            label.UseMnemonic = false;
            label.Font = headerFont;
            label.Anchor = AnchorStyles.Left;
        }

        private static void ConfigureScrollHost(Panel host)
        {
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.BorderStyle = BorderStyle.FixedSingle;
        }

        /// <summary>A one-row strip: the title takes the free width, the extras sit at the right edge.</summary>
        private static Control MakeHeaderRow(Label title, params Control[] extras)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.AutoSize = true;
            row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            row.ColumnCount = 1 + extras.Length;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.Controls.Add(title, 0, 0);
            for (int i = 0; i < extras.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                extras[i].Anchor = AnchorStyles.Right;
                row.Controls.Add(extras[i], i + 1, 0);
            }
            return row;
        }

        /// <summary>A header strip above a body. A null header gives a body-only section.</summary>
        private static Control MakeSection(Control headerRow, Control body, int gap)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Fill;
            section.Margin = new Padding(gap);
            section.ColumnCount = 1;
            section.RowCount = 2;
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            if (headerRow != null)
            {
                headerRow.Dock = DockStyle.Fill;
                section.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                section.Controls.Add(headerRow, 0, 0);
            }
            else
            {
                section.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            }
            body.Dock = DockStyle.Fill;
            section.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            section.Controls.Add(body, 0, 1);
            return section;
        }

        /// <summary>
        /// Adds a Dock = Top item below the ones already there. Each new item is sent to the back of the z-order,
        /// which for docked controls means the bottom, so items read top to bottom in the order they were added.
        /// </summary>
        private static void AddStacked(Panel host, Control item)
        {
            host.Controls.Add(item);
            item.BringToFront();
        }

        private static void ClearHost(Control host)
        {
            host.SuspendLayout();
            for (int i = host.Controls.Count - 1; i >= 0; i--)
            {
                Control child = host.Controls[i];
                host.Controls.RemoveAt(i);
                child.Dispose();
            }
            host.ResumeLayout(true);
        }

        private string Localize(string key, string fallback)
        {
            if (localizer != null)
            {
                string value = localizer(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return fallback;
        }

        private string GamesHeaderText()
        {
            // PS4 favourites are categories of homebrew, not games (ledger MW1-095).
            if (platform == DashboardPlatform.PS4)
            {
                return Localize("OUR_FAVORITE_CATEGORIES", "Our Favorite Categories");
            }
            return Localize("OUR_FAVORITE_GAMES", "Our Favorite Games");
        }

        private void ApplyHeaderTexts()
        {
            headerNews.Text = Localize("LATEST_NEWS", "Latest News");
            headerAnnouncements.Text = Localize("ANNOUNCEMENTS", "Announcements");
            headerChangeLog.Text = Localize("CHANGE_LOG", "Change Log");
            headerStatistics.Text = Localize("STATISTICS", "Statistics");
            noAnnouncements.TitleText = Localize("NO_ANNOUNCEMENTS", "NO ANNOUNCEMENTS");
        }

        private void ReportStatus(string message, Exception exception)
        {
            StatusMessage?.Invoke(this, new DashboardStatusEventArgs(message, exception));
        }

        private void ForwardLink(object sender, LinkClickedEventArgs e)
        {
            LinkClicked?.Invoke(this, e);
        }

        // ---- Favourites images ----------------------------------------------------------------------------

        /// <summary>Loads a tile cover in the background and sets it when it arrives, unless the tile is already gone.</summary>
        private async void LoadTileImage(TileCard card, string url, TilePanel owner)
        {
            try
            {
                Image image = await dataSource.LoadImageAsync(url);
                if (image == null)
                {
                    return;
                }

                if (IsDisposed || owner != favourites || card.IsDisposed)
                {
                    image.Dispose();
                    return;
                }

                ownedImages.Add(image);
                card.Image = image;
            }
            catch (Exception ex)
            {
                ReportStatus("Could not load a favourite image: " + ex.Message, ex);
            }
        }

        private void DisposeFavourites()
        {
            if (favourites != null)
            {
                favouritesHost.Controls.Remove(favourites);
                favourites.Dispose();
                favourites = null;
            }

            foreach (Image image in ownedImages)
            {
                image.Dispose();
            }
            ownedImages.Clear();
        }

        // ---- News, announcements, change log events -------------------------------------------------------

        private async void StartNewsLoad()
        {
            // LoadNewsAsync reports its own failures, so nothing can escape this async void.
            await LoadNewsAsync();
        }

        private void LinkRefreshNews_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            StartNewsLoad();
        }

        private void AnnouncementItem_Dismissed(object sender, EventArgs e)
        {
            AnnouncementItem item = sender as AnnouncementItem;
            if (item == null)
            {
                return;
            }

            try
            {
                dataSource.DismissAnnouncement(item.Id);
            }
            catch (Exception ex)
            {
                ReportStatus("Could not dismiss the announcement: " + ex.Message, ex);
                return;
            }

            int id = item.Id;
            announcementsItemsHost.Controls.Remove(item);

            // The click came from a button inside the item, so dispose it after the handler returns.
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(item.Dispose));
            }
            else
            {
                item.Dispose();
            }

            UpdateAnnouncementsEmptyState();
            AnnouncementDismissed?.Invoke(this, new AnnouncementDismissedEventArgs(id));
        }

        private void UpdateAnnouncementsEmptyState()
        {
            bool empty = announcementsItemsHost.Controls.Count < 1;
            noAnnouncements.Visible = empty;
            announcementsItemsHost.Visible = !empty;
        }

        private void ButtonPrevious_Click(object sender, EventArgs e)
        {
            // Older release. The old code did not check the bounds and relied on the button being disabled.
            if (releaseIndex < releases.Count - 1)
            {
                releaseIndex++;
                ShowRelease();
            }
        }

        private void ButtonNext_Click(object sender, EventArgs e)
        {
            if (releaseIndex > 0)
            {
                releaseIndex--;
                ShowRelease();
            }
        }

        private void ShowRelease()
        {
            ClearHost(changeLogHost);

            if (releases.Count == 0)
            {
                ShowEmptyChangeLog();
                return;
            }

            ReleaseInfo release = releases[releaseIndex];

            // Newest release shows its whole body. Older ones lose their last line (the old code did this for
            // Previous and Next but not for the first load, so going back to the newest changed the text; here the
            // newest always shows the whole body).
            ChangeLogItem item = new ChangeLogItem(release.Name, FormatReleaseBody(release.Body, releaseIndex != 0), release.PublishedAt);
            item.UseRelativeTimes = useRelativeTimes;
            item.Dock = DockStyle.Top;
            item.HyperlinkClick += ForwardLink;
            changeLogHost.Controls.Add(item);

            buttonPrevious.Enabled = releaseIndex < releases.Count - 1;
            buttonNext.Enabled = releaseIndex > 0;
        }

        private void ShowEmptyChangeLog()
        {
            ClearHost(changeLogHost);
            Label empty = new Label();
            empty.AutoSize = true;
            empty.Dock = DockStyle.Top;
            empty.UseMnemonic = false;
            empty.Padding = new Padding(Math.Max(4, Font.Height / 2));
            empty.Text = "No release information is available.";
            changeLogHost.Controls.Add(empty);
            buttonPrevious.Enabled = false;
            buttonNext.Enabled = false;
        }

        /// <summary>Replaces "- " list markers with bullets and optionally drops the last line, without throwing on a one-line body.</summary>
        private static string FormatReleaseBody(string body, bool dropLastLine)
        {
            string text = (body ?? string.Empty).Trim();
            if (dropLastLine)
            {
                int cut = text.LastIndexOf('\n');
                if (cut > 0)
                {
                    text = text.Substring(0, cut).TrimEnd('\r', '\n');
                }
            }
            return text.Replace("- ", "\u2022 ");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                newsTicket++;
                DisposeFavourites();
                if (headerFont != null)
                {
                    headerFont.Dispose();
                    headerFont = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
