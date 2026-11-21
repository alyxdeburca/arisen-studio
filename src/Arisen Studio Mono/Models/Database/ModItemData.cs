using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ArisenStudio.Models.Database
{
    public class ModItemData
    {
        public string Platform { get; set; }

        public int Id { get; set; }

        public string CategoryId { get; set; }

        public string Name { get; set; }

        public string FirmwareType { get; set; } = "n/a";

        public string Region { get; set; } = "n/a";

        public string CreatedBy { get; set; } = "Unknown";

        public string SubmittedBy { get; set; }

        public string Version { get; set; }

        public string GameMode { get; set; }

        public string ModType { get; set; }

        public string Description { get; set; }

        public List<DownloadFiles> DownloadFiles { get; set; }

        public Platform GetPlatform()
        {
            switch (Platform)
            {
                case "PS3":
                    return ArisenStudio.Platform.PS3;
                case "PS4":
                    return ArisenStudio.Platform.PS4;
                case "XBOX":
                    return ArisenStudio.Platform.XBOX360;
                default:
                    return ArisenStudio.Platform.PS3;
            }
        }

        public IEnumerable<string> Creators => CreatedBy.Split(new char[] { '/', '&' }).Select(x => x.Trim());

        public List<string> FirmwareTypes => FirmwareType.Split('/').ToList();

        public IEnumerable<string> ModTypes => ModType.Split(new char[] { '/', '&' }).Select(x => x.Trim());

        public IEnumerable<string> Versions
        {
            get
            {
                if (string.IsNullOrEmpty(Version)) return new List<string> { "-" };
                return Version.Split('/').Select(version => version.Trim()).ToList();
            }
        }

        public bool IsAnyRegion => Region.Equals("ALL") || Region.Equals("-");

        public IEnumerable<string> Regions
        {
            get
            {
                return Region.Split('/').Select(region =>
                {
                    switch (region)
                    {
                        case "ALL":
                            return "All Regions";
                        case "-":
                            return "n/a";
                        default:
                            return region;
                    }
                }).ToList();
            }
        }

        public IEnumerable<string> GameModes
        {
            get
            {
                return GameMode.Split('/').Select(mode =>
                {
                    switch (mode)
                    {
                        case "ALL":
                            return "All Game Modes";
                        case "MP":
                            return "Multiplayer";
                        case "ZM":
                            return "Zombies";
                        case "SP":
                            return "Singleplayer";
                        case "CO OP":
                            return "Co-Operative";
                        case "SPEC OPS":
                            return "Special Ops";
                        case "EXT":
                            return "Extinction";
                        default:
                            return "n/a";
                    }
                }).ToList();
            }
        }

        public string GetVersion(DownloadFiles downloadFiles)
        {
            return string.IsNullOrEmpty(downloadFiles.Version) ? string.Empty : $"-{downloadFiles.Version}";
        }

        public CategoryItem GetCategory(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId)) ?? new CategoryItem { Title = "<Can't Find Name>" };
        }

        public CategoryType GetCategoryType(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId))?.CategoryType ?? CategoryType.Game;
        }

        public string GetCategoryName(CategoriesData categoriesData)
        {
            return categoriesData.Categories.FirstOrDefault(x => x.Id.EqualsIgnoreCase(CategoryId))?.Title ?? "<Can't Find Name>";
        }

        /// <summary>
        /// Get the directory for extracting modded files.
        /// </summary>
        /// <returns> </returns>
        public string DownloadDataDirectory(DownloadFiles downloadFiles, CategoryItem category)
        {
            return $@"{AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData)}\{GetPlatform().Humanize()}\{category.CategoryType.Humanize().RemoveInvalidChars()}\{category.Title.RemoveInvalidChars()}\{Name.RemoveInvalidChars()}\{downloadFiles.Name.RemoveInvalidChars()}{GetVersion(downloadFiles)}-{Id}\";
        }

        /// <summary>
        /// Get the downloaded mods archive file path.
        /// </summary>
        /// <returns> Mods Archive File Path </returns>
        public string ArchiveZipFile(DownloadFiles downloadFiles, CategoryItem category)
        {
            return $@"{AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData)}\{GetPlatform().Humanize()}\{category.CategoryType.Humanize().RemoveInvalidChars()}\{category.Title.RemoveInvalidChars()}\{Name.RemoveInvalidChars()}\{downloadFiles.Name.RemoveInvalidChars()}{GetVersion(downloadFiles)}-{Id}.zip";
        }

        /// <summary>
        /// Download the modded files archive and extracts all files to <see cref="DownloadDataDirectory" />.
        /// </summary>
        /// <param name="downloadFiles"> </param>
        public async Task DownloadArchiveFiles(DownloadFiles downloadFiles, CategoryItem category, IProgress<int> progress = null)
        {
            string archivePath = DownloadDataDirectory(downloadFiles, category);
            string archiveFilePath = ArchiveZipFile(downloadFiles, category);

            if (Directory.Exists(archivePath))
            {
                try
                {
                    Directory.Delete(archivePath, true);
                }
                catch (Exception) { }
            }

            if (!Directory.Exists(archivePath))
            {
                Directory.CreateDirectory(archivePath);
            }

            if (File.Exists(archiveFilePath))
            {
                File.Delete(archiveFilePath);
            }

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("Accept: application/zip");
                webClient.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");

                webClient.DownloadProgressChanged += (sender, e) =>
                {
                    // Report download progress (0-100)
                    if (progress != null)
                    {
                        progress.Report(e.ProgressPercentage);
                    }
                };

                await webClient.DownloadFileTaskAsync(new Uri(downloadFiles.Url), archiveFilePath);
            }

            // Extract the archive file to the archive path
            ZipFile.ExtractToDirectory(archiveFilePath, archivePath);

            DownloadedItem downloadItem = AppHost.Settings.DownloadedMods.FirstOrDefault(x => x.Platform == GetPlatform() && x.ModId == Id);

            if (downloadItem == null)
            {
                AppHost.Settings.DownloadedMods.Add(
                    new DownloadedItem()
                    {
                        Platform = GetPlatform(),
                        ModId = Id,
                        CategoryId = CategoryId,
                        DownloadFile = downloadFiles,
                        FilePath = archiveFilePath,
                        DateTime = DateTime.Now
                    });
            }
            else
            {
                AppHost.Settings.DownloadedMods[AppHost.Settings.DownloadedMods.IndexOf(downloadItem)] =
                    new DownloadedItem()
                    {
                        Platform = GetPlatform(),
                        ModId = Id,
                        CategoryId = CategoryId,
                        DownloadFile = downloadFiles,
                        FilePath = archiveFilePath,
                        DateTime = DateTime.Now
                    };
            }
        }
    }
}
