using ArisenStudio.Constants;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Release_Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ArisenStudio.Extensions
{
    public abstract class UpdateExtensions
    {
        /// <summary>
        /// Check if there is a new update available found on startup.
        /// </summary>
        public static bool IsUpdateAvailable { get; set; } = false;

        /// <summary>
        /// Get the application current version.
        /// </summary>
        public static Version CurrentVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// Get the current version name.
        /// </summary>
        public static string CurrentVersionName { get; } = $"Beta v{CurrentVersion.ToString().TrimStart('0', '.')}";

        /// <summary>
        /// All release data from GitHub. Null until the host assigns it from <see cref="GetAllReleasesData"/>.
        /// </summary>
        public static List<GitHubReleaseData> AllReleases { get; set; }

        /// <summary>
        /// Download stats from GitHub. Null until the host assigns it from <see cref="GetDownloadStats"/>.
        /// </summary>
        public static StatsData StatsData { get; set; }

        /// <summary>
        /// Get all release information from the GitHub API.
        /// </summary>
        /// <param name="getStream"> Opens a stream for a URL, e.g. HttpExtensions.GetStream. </param>
        /// <returns> The releases. </returns>
        public static List<GitHubReleaseData> GetAllReleasesData(Func<string, Stream> getStream)
        {
            using (StreamReader streamReader = new StreamReader(getStream(Urls.GitHubReleases)))
            {
                return JsonConvert.DeserializeObject<List<GitHubReleaseData>>(streamReader.ReadToEnd());
            }
        }

        /// <summary>
        /// Fills <see cref="AllReleases"/> from GitHub, off the calling thread. Does nothing when it is already filled.
        /// On failure it throws and leaves <see cref="AllReleases"/> null.
        /// </summary>
        public static async Task LoadAllReleasesAsync()
        {
            if (AllReleases != null)
            {
                return;
            }

            AllReleases = await Task.Run(() => GetAllReleasesData(HttpExtensions.GetStream));
        }

        /// <summary>
        /// Get download stats from GitHub.
        /// </summary>
        /// <param name="getStream"> Opens a stream for a URL, e.g. HttpExtensions.GetStream. </param>
        /// <returns> The download stats. </returns>
        public static StatsData GetDownloadStats(Func<string, Stream> getStream)
        {
            using (StreamReader streamReader = new StreamReader(getStream(Urls.StatsData)))
            {
                return JsonConvert.DeserializeObject<StatsData>(streamReader.ReadToEnd());
            }
        }
    }
}
