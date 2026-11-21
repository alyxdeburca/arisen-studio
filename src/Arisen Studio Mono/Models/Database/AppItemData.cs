using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ArisenStudio.Extensions;

namespace ArisenStudio.Models.Database
{
    public partial class AppItemData
    {
        // Characters removed from any path segment built from user-supplied text.
        // Fixed set on every OS (Windows set plus the Windows-only separators), so Linux output matches Windows.
        private const string InvalidSegmentChars = "\\/:*?\"<>|";

        public string Platform { get; set; }

        public int Id { get; set; }

        public string CategoryId { get; set; }

        public string Name { get; set; }

        public string CreatedBy { get; set; } = "Unknown";

        public string SubmittedBy { get; set; }

        public string Version { get; set; }

        public string PlayableVersion { get; set; }

        public string ContentId { get; set; }

        public string TitleId { get; set; }

        public string Description { get; set; }

        public List<AppItemFile> DownloadFiles { get; set; }

        public IEnumerable<string> Creators => CreatedBy.Split(new char[] { '/', '&' }).Select(x => x.Trim());

        public List<string> Versions => Version.Split('/').ToList();

        public List<string> FirmwareVersions => PlayableVersion.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public Platform GetPlatform()
        {
            return ArisenStudio.Platform.PS4;
        }

        public string GetVersion(AppItemFile appFile)
        {
            return appFile.Version.IsNullOrEmpty() ? string.Empty : appFile.Version;
        }

        /// <summary>
        /// Get the category data.
        /// </summary>
        /// <param name="categoriesData"> </param>
        /// <returns> </returns>
        public CategoryItem GetCategory(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId)) ?? new CategoryItem() { Title = "<Can't Find Name>" };
        }

        /// <summary>
        /// Get the category type.
        /// </summary>
        /// <param name="categoriesData"> </param>
        /// <returns> </returns>
        public CategoryType GetCategoryType(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId))?.CategoryType ?? CategoryType.Game;
        }

        /// <summary>
        /// Get the category name.
        /// </summary>
        /// <param name="categoriesData"> </param>
        /// <returns> </returns>
        public string GetCategoryName(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId))?.Title ?? "<Can't Find Name>";
        }

        /// <summary>
        /// Get the directory for extracting modded files.
        /// </summary>
        /// <returns> </returns>
        public string DownloadFilesDirectory(AppItemFile appFile)
        {
            string root = ArisenStudio.Runtime.AppHost.Settings.PathDownloads.GetFullPath(ArisenStudio.Runtime.AppHost.Settings.PathAppData);
            string folderName = SafeSegment(appFile.Name) + "-" + SafeSegment(GetVersion(appFile)) + "-" + Id;

            return JoinPath(root, SafeSegment(Platform), "Homebrew", SafeSegment(Name), SafeSegment(CreatedBy), folderName) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Get the downloaded files path.
        /// </summary>
        /// <returns> Mods Archive File Path </returns>
        public string LocalFilePath(AppItemFile appFile)
        {
            string root = ArisenStudio.Runtime.AppHost.Settings.PathDownloads.GetFullPath(ArisenStudio.Runtime.AppHost.Settings.PathAppData);
            string fileName = SafeSegment(appFile.Name) + SafeSegment(GetVersion(appFile)) + "-" + Id + ".pkg";

            return JoinPath(root, SafeSegment(Platform), "Homebrew", SafeSegment(Name), SafeSegment(CreatedBy), fileName);
        }

        /// <summary>
        /// Download the modded files archive and extracts all files to <see cref="DownloadDataDirectory" />.
        /// </summary>
        /// <param name="appFile"> </param>
        public async Task DownloadAppFile(AppItemFile appFile, IProgress<int> progress = null)
        {
            string folderPath = DownloadFilesDirectory(appFile);
            string filePath = LocalFilePath(appFile);

            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch (Exception) { }
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Accept: application/zip");
                webClient.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");

                webClient.DownloadProgressChanged += (sender, e) =>
                {
                    progress?.Report(e.ProgressPercentage);
                };

                await webClient.DownloadFileTaskAsync(new Uri(appFile.GetFileUrl(TitleId)), filePath);
            }
        }

        /// <summary>
        /// Make one user-supplied text value safe to use as a single path segment.
        /// Null or blank becomes empty; the separator and invalid characters are removed; "." and ".." are dropped.
        /// </summary>
        private static string SafeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c < ' ' || InvalidSegmentChars.IndexOf(c) >= 0)
                {
                    continue;
                }

                builder.Append(c);
            }

            string result = builder.ToString();
            if (result == "." || result == "..")
            {
                return string.Empty;
            }

            return result;
        }

        /// <summary>
        /// Join path parts with <see cref="Path.Combine(string, string)" />, skipping empty parts.
        /// Used instead of the three-or-more argument overloads, which the .NET Framework does not have.
        /// </summary>
        private static string JoinPath(params string[] parts)
        {
            string result = string.Empty;
            foreach (string part in parts)
            {
                if (part.Length == 0)
                {
                    continue;
                }

                result = result.Length == 0 ? part : Path.Combine(result, part);
            }

            return result;
        }
    }
}
