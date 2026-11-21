using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace ArisenStudio.Forms.Dashboard
{
    /// <summary>
    /// STUB. This is not a real data source: every list is empty and every count is zero. It exists so that the
    /// dashboard page can be created and shown before the shell wires in the real database, settings, RSS feed and
    /// GitHub release list. Replace it by setting <see cref="DashboardPage.DataSource"/>.
    /// </summary>
    public sealed class StubDashboardDataSource : IDashboardDataSource
    {
        private readonly HashSet<int> dismissed = new HashSet<int>();

        public IList<FavoriteGameInfo> GetFavoriteGames(DashboardPlatform? platform)
        {
            return new List<FavoriteGameInfo>();
        }

        public IList<FavoriteModInfo> GetFavoriteMods(DashboardPlatform? platform)
        {
            return new List<FavoriteModInfo>();
        }

        public Task<Image> LoadImageAsync(string url)
        {
            return Task.FromResult<Image>(null);
        }

        public IList<AnnouncementInfo> GetAnnouncements()
        {
            return new List<AnnouncementInfo>();
        }

        public bool IsAnnouncementDismissed(int id)
        {
            return dismissed.Contains(id);
        }

        public void DismissAnnouncement(int id)
        {
            dismissed.Add(id);
        }

        public Task<IList<NewsEntry>> GetNewsAsync()
        {
            return Task.FromResult<IList<NewsEntry>>(new List<NewsEntry>());
        }

        public DashboardStats GetStatistics()
        {
            return new DashboardStats();
        }

        public IList<ReleaseInfo> GetReleases()
        {
            return new List<ReleaseInfo>();
        }
    }
}
