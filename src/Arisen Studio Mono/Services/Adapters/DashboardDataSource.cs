using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using ArisenStudio.Database;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>One "Our Favorite Games" entry from the online database (the old FavoriteGamesData.Favorite).</summary>
    public sealed class DashboardFavoriteGameEntry
    {
        public DashboardPlatform Platform { get; set; }

        public string CategoryId { get; set; }

        public string ImageUrl { get; set; }
    }

    /// <summary>One "Our Favorite Mods" entry from the online database (the old FavoriteModsData.Favorite).</summary>
    public sealed class DashboardFavoriteModEntry
    {
        public DashboardPlatform Platform { get; set; }

        public string CategoryId { get; set; }

        public int ModId { get; set; }

        public string ModName { get; set; }
    }

    /// <summary>
    /// The parts of the dashboard that come from outside the loaded database: the favourites and announcements
    /// lists, the RSS feed, the release list and the download counter. The host fills the lists after it has
    /// loaded them and supplies the loaders. This class and <see cref="DashboardDataSource"/> make no network call.
    /// </summary>
    public sealed class DashboardFeeds
    {
        public IList<DashboardFavoriteGameEntry> FavoriteGames { get; set; }

        public IList<DashboardFavoriteModEntry> FavoriteMods { get; set; }

        /// <summary>All announcements, in database order (the page applies the date window).</summary>
        public IList<AnnouncementInfo> Announcements { get; set; }

        /// <summary>Loads the news feed (RSS). Null: <see cref="DashboardDataSource.GetNewsAsync"/> fails with a clear message.</summary>
        public Func<Task<IList<NewsEntry>>> NewsLoader { get; set; }

        /// <summary>Returns the releases, newest first. Null: <see cref="DashboardDataSource.GetReleases"/> fails with a clear message.</summary>
        public Func<IList<ReleaseInfo>> ReleasesProvider { get; set; }

        /// <summary>Downloads a cover image from a web address. Null: web covers are not shown (only local files are).</summary>
        public Func<string, Task<Image>> ImageLoader { get; set; }

        /// <summary>Total download count of the database (the old UpdateExtensions.StatsData.TotalDownloads). Null counts as 0.</summary>
        public Func<long> TotalDownloads { get; set; }
    }

    /// <summary>
    /// <see cref="IDashboardDataSource"/> over the loaded catalog, the settings and the host's feeds.
    /// Real: favourites (with item counts from the catalog), announcements and dismissals (saved through the context),
    /// statistics counted from the catalog. Fails with a message: news and releases when the host gave no loader.
    /// </summary>
    public sealed class DashboardDataSource : IDashboardDataSource
    {
        private readonly AdapterContext _context;
        private readonly DashboardFeeds _feeds;

        public DashboardDataSource(AdapterContext context, DashboardFeeds feeds)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _context = context;
            _feeds = feeds ?? new DashboardFeeds();
        }

        public IList<FavoriteGameInfo> GetFavoriteGames(DashboardPlatform? platform)
        {
            List<FavoriteGameInfo> result = new List<FavoriteGameInfo>();

            if (_feeds.FavoriteGames == null)
            {
                return result;
            }

            foreach (DashboardFavoriteGameEntry entry in _feeds.FavoriteGames)
            {
                if (entry == null || (platform.HasValue && entry.Platform != platform.Value))
                {
                    continue;
                }

                result.Add(new FavoriteGameInfo
                {
                    Platform = entry.Platform,
                    CategoryId = entry.CategoryId,
                    CategoryTitle = _context.Catalog.CategoryTitle(entry.CategoryId),
                    TotalCount = CountItems(entry.Platform, entry.CategoryId),
                    ImageUrl = entry.ImageUrl
                });
            }

            return result;
        }

        public IList<FavoriteModInfo> GetFavoriteMods(DashboardPlatform? platform)
        {
            List<FavoriteModInfo> result = new List<FavoriteModInfo>();

            if (_feeds.FavoriteMods == null)
            {
                return result;
            }

            foreach (DashboardFavoriteModEntry entry in _feeds.FavoriteMods)
            {
                if (entry == null || (platform.HasValue && entry.Platform != platform.Value))
                {
                    continue;
                }

                result.Add(new FavoriteModInfo
                {
                    Platform = entry.Platform,
                    CategoryId = entry.CategoryId,
                    CategoryTitle = _context.Catalog.CategoryTitle(entry.CategoryId),
                    ModId = entry.ModId,
                    ModName = entry.ModName
                });
            }

            return result;
        }

        /// <summary>
        /// A local file path is read from disk (and copied, so the file is not locked). A web address goes to the
        /// host's <see cref="DashboardFeeds.ImageLoader"/>; without one there is no image (null), as the interface allows.
        /// </summary>
        public Task<Image> LoadImageAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Task.FromResult<Image>(null);
            }

            if (File.Exists(url))
            {
                return Task.Run<Image>(delegate ()
                {
                    using (Image original = Image.FromFile(url))
                    {
                        return new Bitmap(original);
                    }
                });
            }

            Func<string, Task<Image>> loader = _feeds.ImageLoader;
            return loader != null ? loader(url) : Task.FromResult<Image>(null);
        }

        public IList<AnnouncementInfo> GetAnnouncements()
        {
            return _feeds.Announcements != null
                ? new List<AnnouncementInfo>(_feeds.Announcements)
                : new List<AnnouncementInfo>();
        }

        public bool IsAnnouncementDismissed(int id)
        {
            SettingsData settings = _context.Settings;
            return settings.DismissedAnnouncements != null && settings.DismissedAnnouncements.Contains(id);
        }

        public void DismissAnnouncement(int id)
        {
            SettingsData settings = _context.Settings;

            if (settings.DismissedAnnouncements == null)
            {
                settings.DismissedAnnouncements = new List<int>();
            }

            if (!settings.DismissedAnnouncements.Contains(id))
            {
                settings.DismissedAnnouncements.Add(id);
            }

            _context.Save();
        }

        public Task<IList<NewsEntry>> GetNewsAsync()
        {
            Func<Task<IList<NewsEntry>>> loader = _feeds.NewsLoader;

            if (loader == null)
            {
                return Task.FromException<IList<NewsEntry>>(new NotSupportedException(
                    "News is not available right now."));
            }

            return loader();
        }

        public DashboardStats GetStatistics()
        {
            DatabaseCatalog catalog = _context.Catalog;
            DashboardStats stats = new DashboardStats();

            Func<long> downloads = _feeds.TotalDownloads;
            stats.TotalDownloads = downloads != null ? downloads() : 0;

            if (catalog.Categories != null && catalog.Categories.LastUpdated != default(DateTime))
            {
                stats.LastUpdated = catalog.Categories.LastUpdated;
            }

            StatGroup ps3 = new StatGroup("PlayStation 3");
            ps3.Lines.Add(new StatLine(CountByType(catalog.GameModsPs3, CategoryType.Game), "Game Mods"));
            ps3.Lines.Add(new StatLine(CountByType(catalog.HomebrewPs3, CategoryType.Homebrew), "Homebrew"));
            ps3.Lines.Add(new StatLine(CountByType(catalog.ResourcesPs3, CategoryType.Resource), "Resources"));
            ps3.Lines.Add(new StatLine(catalog.PackagesPs3 != null ? catalog.PackagesPs3.Count : 0, "Packages"));
            stats.Groups.Add(ps3);

            StatGroup ps4 = new StatGroup("PlayStation 4");
            int apps = catalog.AppsPs4 != null && catalog.AppsPs4.Library != null ? catalog.AppsPs4.Library.Count : 0;
            ps4.Lines.Add(new StatLine(apps, "Homebrew"));
            stats.Groups.Add(ps4);

            StatGroup xbox = new StatGroup("Xbox 360");
            xbox.Lines.Add(new StatLine(CountByType(catalog.GameModsXbox, CategoryType.Game), "Game Mods"));
            xbox.Lines.Add(new StatLine(CountByType(catalog.HomebrewXbox, CategoryType.Homebrew), "Homebrew"));
            stats.Groups.Add(xbox);

            return stats;
        }

        public IList<ReleaseInfo> GetReleases()
        {
            Func<IList<ReleaseInfo>> provider = _feeds.ReleasesProvider;

            if (provider == null)
            {
                throw new NotSupportedException(
                    "The release list is not available: no release provider is connected. The host must set DashboardFeeds.ReleasesProvider.");
            }

            return provider();
        }

        // Number of library items in a category: game mods for PS3 and Xbox 360, homebrew for PS4 (the tile text).
        private int CountItems(DashboardPlatform platform, string categoryId)
        {
            DatabaseCatalog catalog = _context.Catalog;
            int count = 0;

            if (platform == DashboardPlatform.PS4)
            {
                if (catalog.AppsPs4 != null && catalog.AppsPs4.Library != null)
                {
                    foreach (AppItemData app in catalog.AppsPs4.Library)
                    {
                        if (app != null && AdapterMap.Same(app.CategoryId, categoryId))
                        {
                            count++;
                        }
                    }
                }

                return count;
            }

            ModsData library = platform == DashboardPlatform.PS3 ? catalog.GameModsPs3 : catalog.GameModsXbox;

            if (library != null && library.Library != null)
            {
                foreach (ModItemData mod in library.Library)
                {
                    if (mod != null && AdapterMap.Same(mod.CategoryId, categoryId))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int CountByType(ModsData library, CategoryType type)
        {
            if (library == null || library.Library == null)
            {
                return 0;
            }

            int count = 0;

            foreach (ModItemData mod in library.Library)
            {
                if (mod != null && _context.Catalog.CategoryTypeOf(mod.CategoryId) == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
