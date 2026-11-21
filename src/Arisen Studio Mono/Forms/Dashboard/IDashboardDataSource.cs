using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace ArisenStudio.Forms.Dashboard
{
    /// <summary>
    /// Everything the dashboard page reads. The shell supplies an implementation backed by the real database,
    /// settings, RSS feed and release list. All methods are called on the UI thread; the async ones may do their
    /// work on other threads. Exceptions are caught by the page and reported through
    /// <see cref="DashboardPage.StatusMessage"/>.
    /// </summary>
    public interface IDashboardDataSource
    {
        /// <summary>
        /// Favourite games for a platform, or for every platform when <paramref name="platform"/> is null
        /// (ledger MW1-095: the old code filtered by the connected profile's platform).
        /// </summary>
        IList<FavoriteGameInfo> GetFavoriteGames(DashboardPlatform? platform);

        /// <summary>Favourite mods for a platform, or for every platform when null (ledger MW1-098).</summary>
        IList<FavoriteModInfo> GetFavoriteMods(DashboardPlatform? platform);

        /// <summary>
        /// Downloads a cover image. Return null when there is none. The page owns and disposes the returned image.
        /// The old code did this synchronously on the UI thread (ledger MW1-096); this one is async.
        /// </summary>
        Task<Image> LoadImageAsync(string url);

        /// <summary>All announcements, dismissed or not, in database order. The page applies the date window.</summary>
        IList<AnnouncementInfo> GetAnnouncements();

        /// <summary>True when the user already dismissed this announcement (the old Settings.DismissedAnnouncements).</summary>
        bool IsAnnouncementDismissed(int id);

        /// <summary>Remembers a dismissal. Saving the settings is up to the implementation.</summary>
        void DismissAnnouncement(int id);

        /// <summary>News feed entries in feed order (first entry is shown at the top) (ledger MW1-106).</summary>
        Task<IList<NewsEntry>> GetNewsAsync();

        /// <summary>Counts for the statistics card (ledger MW1-107).</summary>
        DashboardStats GetStatistics();

        /// <summary>Releases, newest first, for the change log card (ledger MW1-109).</summary>
        IList<ReleaseInfo> GetReleases();
    }
}
