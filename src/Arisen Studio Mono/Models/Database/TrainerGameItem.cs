using ArisenStudio.Database;
using ArisenStudio.Extensions;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace ArisenStudio.Models.Database
{
    /// <summary>
    /// Get the game trainer information.
    /// </summary>
    public class TrainerGameItem
    {
        public string TitleId { get; set; }

        public string Description { get; set; }

        public List<TrainerItem> Trainers { get; set; }

        public Platform GetPlatform()
        {
            return Platform.XBOX360;
        }

        /// <summary>
        /// Get the directory for extracting the files.
        /// </summary>
        /// <returns> </returns>
        public string DownloadDataDirectory(TrainerItem trainerData)
        {
            return Path.Combine(AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData), GetPlatform().Humanize(), "Trainers", TitleId.RemoveInvalidChars() ?? string.Empty, trainerData.Name.RemoveInvalidChars() ?? string.Empty) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Get the downloaded archive file path.
        /// </summary>
        /// <returns> Archive File Path </returns>
        public string ArchiveZipFile(TrainerItem trainerData)
        {
            return Path.Combine(AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData), GetPlatform().Humanize(), "Trainers", TitleId.RemoveInvalidChars() ?? string.Empty, $"{trainerData.Name.RemoveInvalidChars()}.zip");
        }

        /// <summary>
        /// Download the archive and extracts all files to <see cref="DownloadDataDirectory" />.
        /// </summary>
        /// <param name="trainerData"> </param>
        public async Task DownloadArchiveFiles(TrainerItem trainerData, IProgress<int> progress = null)
        {
            string archivePath = DownloadDataDirectory(trainerData);
            string archiveFilePath = ArchiveZipFile(trainerData);

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
                    progress?.Report(e.ProgressPercentage);
                };

                await webClient.DownloadFileTaskAsync(new Uri(trainerData.Url), archiveFilePath);
            }

            // Extract the archive file to the archive path
            ZipFile.ExtractToDirectory(archiveFilePath, archivePath);
        }
    }
}
