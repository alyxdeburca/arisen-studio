using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Services.Transfers;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// The five download transfers of the old TransferDialog: mods, package, application, game save and trainer.
    /// They need no console, only the data-layer download methods that are already ported, so they are real code and
    /// not a stub. Each method follows the DevExpress dialog method of the same name (DownloadMods,
    /// DownloadPackageFile, DownloadApplication, DownloadGameSave, DownloadTrainer).
    /// </summary>
    public static class TransferDownloadOperations
    {
        /// <summary>
        /// Original: DownloadMods. The old dialog asked the user which download file to use with
        /// <c>modItem.GetDownloadFiles(this)</c>, which is not ported: the host passes the chosen file in
        /// <see cref="TransferRequest.DownloadFiles"/>, and a mod with exactly one file uses that file.
        /// </summary>
        public static async Task DownloadMods(TransferRequest request, ITransferHost host)
        {
            ModItemData modItem = request.ModItem;

            host.Progress.Report(0);

            try
            {
                host.UpdateStatus(TransferStatusText.PreparingLine(host.GetText("PREPARING_DOWNLOAD", "Preparing to download archive..."), modItem.Name, modItem.CategoryId));

                host.UpdateStatus(host.GetText("GETTING_DOWNLOAD_FILES", "Getting archive file download link..."));

                DownloadFiles downloadFiles = request.DownloadFiles;

                if (downloadFiles == null && modItem.DownloadFiles != null && modItem.DownloadFiles.Count == 1)
                {
                    downloadFiles = modItem.DownloadFiles[0];
                }

                if (downloadFiles == null)
                {
                    host.Progress.Report(0);
                    host.UpdateStatus(string.Join(" ", host.GetText("NO_DOWNLOAD_FILES_SELECTED", "No archive file download selected."), host.GetText("DOWNLOAD_CANCELED", "Download canceled.")));
                    return;
                }

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOADING", "Downloading archive..."));
                await modItem.DownloadArchiveFiles(downloadFiles, request.Category, host.Progress);

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOAD_SUCCESS", "Successfully downloaded archive file."));

                host.LocalPath = ParentFolder(modItem.ArchiveZipFile(downloadFiles, request.Category));

                host.Progress.Report(100);

                host.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
        }

        /// <summary>
        /// Original: DownloadPackageFile. The package goes to downloads\Packages\name (region)\name.pkg, and the
        /// RAP to the same folder when one is required and not missing.
        /// </summary>
        public static async Task DownloadPackage(TransferRequest request, ITransferHost host)
        {
            PackageItemData packageItem = request.PackageItem;

            host.Progress.Report(0);

            host.UpdateStatus(host.GetText("PREPARING_DOWNLOAD", "Preparing to download archive..."));

            try
            {
                string downloadFolder = Path.Combine(DownloadsRoot(request), "Packages", packageItem.Name.RemoveInvalidChars() + " (" + packageItem.Region + ")");

                if (!Directory.Exists(downloadFolder))
                {
                    Directory.CreateDirectory(downloadFolder);
                }

                string downloadLocationPkg = Path.Combine(downloadFolder, packageItem.Name.RemoveInvalidChars() + ".pkg");
                string downloadLocationRap = Path.Combine(downloadFolder, packageItem.ContentId.RemoveInvalidChars() + ".rap");

                string locationText = host.GetText("FILE_DOWNLOAD_LOCATION", "Downloading file: {0} to {1}");
                string errorText = host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}");

                host.UpdateStatus(string.Format(locationText, packageItem.Name + ".pkg", downloadFolder));

                try
                {
                    await HttpExtensions.DownloadFileAsync(packageItem.Url, downloadLocationPkg, host.Progress, host.SetSizeText, host.SetSizeVisible, host.CancellationToken);
                    host.UpdateStatus(host.GetText("FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));
                }
                catch (Exception ex)
                {
                    host.UpdateStatus(string.Format(errorText, ex.Message), ex);
                    return;
                }

                if (packageItem.IsRapRequired && !packageItem.IsRapMissing)
                {
                    host.UpdateStatus(string.Format(locationText, packageItem.ContentId + ".rap", downloadFolder));

                    try
                    {
                        // The old dialog passed PackageItem.Url here (the same address as the package) where a RAP
                        // address looks intended. Kept as it was: needs a decision, see PARALLEL-TD-RESULT.md.
                        await HttpExtensions.DownloadFileAsync(packageItem.Url, downloadLocationRap, host.Progress, host.SetSizeText, host.SetSizeVisible, host.CancellationToken);
                        host.UpdateStatus(host.GetText("FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));
                    }
                    catch (Exception ex)
                    {
                        host.UpdateStatus(string.Format(errorText, ex.Message), ex);
                        return;
                    }
                }

                if (request.Settings != null)
                {
                    request.Settings.DownloadedMods.Add(new DownloadedItem
                    {
                        ModId = packageItem.Id,
                        CategoryId = "package",
                        DateTime = DateTime.Now,
                        DownloadFile = new DownloadFiles
                        {
                            InstallPaths = new System.Collections.Generic.List<string> { packageItem.InstallFilePathPkg + packageItem.InstallFilePathRap },
                            LastUpdated = DateTime.Now,
                            // The old dialog read the form's own Name property here (not the package name).
                            Name = packageItem.Name.RemoveInvalidChars(),
                            Region = packageItem.Region,
                            Url = packageItem.Url,
                            Version = "n/a"
                        }
                    });
                }

                host.LocalPath = downloadFolder;

                host.Progress.Report(100);

                host.IsSuccessful = true;
            }
            catch (WebException ex)
            {
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
            catch (Exception ex)
            {
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
        }

        /// <summary>
        /// Original: DownloadApplication. Uses <see cref="TransferRequest.AppItemFile"/> when the host set it, otherwise
        /// the first download file as the old dialog did. The Open Folder path is the local download folder
        /// (the old dialog took the console install path, which is not a local folder).
        /// </summary>
        public static async Task DownloadApplication(TransferRequest request, ITransferHost host)
        {
            AppItemData appItem = request.AppItem;

            host.Progress.Report(0);

            try
            {
                host.UpdateStatus(host.GetText("PREPARING_DOWNLOAD", "Preparing to download archive..."));

                AppItemFile downloadFile = request.AppItemFile ?? appItem.DownloadFiles.First();

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOADING", "Downloading archive..."));
                await appItem.DownloadAppFile(downloadFile, host.Progress);

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOAD_SUCCESS", "Successfully downloaded archive file."));

                host.LocalPath = appItem.DownloadFilesDirectory(downloadFile);

                host.Progress.Report(100);

                host.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                // The old dialog had no handler here and the exception reached the form's Load event.
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
        }

        /// <summary>
        /// Original: DownloadGameSave. The first download file is saved as downloads\name.pkg. The download folder
        /// must already exist, as before. <see cref="TransferRequest.DownloadFiles"/> is used when the host set it.
        /// </summary>
        public static async Task DownloadGameSave(TransferRequest request, ITransferHost host)
        {
            GameSaveItemData gameSaveItem = request.GameSaveItem;

            host.Progress.Report(0);

            try
            {
                host.UpdateStatus(host.GetText("PREPARING_DOWNLOAD", "Preparing to download archive..."));

                string downloadFolder = DownloadsRoot(request);

                if (!Directory.Exists(downloadFolder))
                {
                    host.UpdateStatus(host.GetText("DOWNLOAD_FOLDER_NOT_FOUND", "Download folder doesn't exist on your computer. You can change the download location in your settings."));
                    host.Progress.Report(0);
                    return;
                }

                DownloadFiles downloadFile = request.DownloadFiles ?? gameSaveItem.DownloadFiles[0];

                string downloadLocation = Path.Combine(downloadFolder, gameSaveItem.Name.RemoveInvalidChars() + ".pkg");

                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_LOCATION", "Downloading file: {0} to {1}"), gameSaveItem.Name + ".pkg", downloadFolder));
                await HttpExtensions.DownloadFileAsync(downloadFile.Url, downloadLocation, host.Progress, host.SetSizeText, host.SetSizeVisible, host.CancellationToken);
                host.UpdateStatus(host.GetText("FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));

                host.LocalPath = downloadFolder;

                host.Progress.Report(100);

                host.IsSuccessful = true;
            }
            catch (WebException ex)
            {
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
            catch (Exception ex)
            {
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
        }

        /// <summary>
        /// Original: DownloadTrainer.
        /// </summary>
        public static async Task DownloadTrainer(TransferRequest request, ITransferHost host)
        {
            host.Progress.Report(0);

            try
            {
                host.UpdateStatus(host.GetText("PREPARING_DOWNLOAD", "Preparing to download archive..."));

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOADING", "Downloading archive..."));

                await request.TrainerGame.DownloadArchiveFiles(request.TrainerItem, host.Progress);

                host.UpdateStatus(host.GetText("ARCHIVE_DOWNLOAD_SUCCESS", "Successfully downloaded archive file."));

                host.LocalPath = ParentFolder(request.TrainerGame.ArchiveZipFile(request.TrainerItem));

                host.IsSuccessful = true;

                host.Progress.Report(100);

                host.UpdateStatus(host.GetText("DOWNLOAD_COMPLETED", "DOWNLOAD COMPLETED"));
            }
            catch (Exception ex)
            {
                // The old dialog had no handler here and the exception reached the form's Load event.
                host.Progress.Report(0);
                host.UpdateStatus(string.Format(host.GetText("FILE_DOWNLOAD_ERROR", "Unable to download file. Error: {0}"), ex.Message), ex);
            }
        }

        private static string DownloadsRoot(TransferRequest request)
        {
            return request.Settings.PathDownloads.GetFullPath(request.Settings.PathAppData);
        }

        /// <summary>
        /// The folder part of a path. Cuts at the last backslash or slash, because the ported data-layer path methods
        /// still build some paths with backslashes and Path.GetDirectoryName does not split those on Linux.
        /// </summary>
        private static string ParentFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int cut = path.LastIndexOfAny(new[] { '\\', '/' });
            return cut > 0 ? path.Substring(0, cut) : string.Empty;
        }
    }
}
