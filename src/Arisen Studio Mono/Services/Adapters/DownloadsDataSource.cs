using System;
using System.Collections.Generic;
using System.IO;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IDownloadsDataSource"/> over <c>SettingsData.DownloadedMods</c>, joined with the loaded catalog.
    /// Real. A download whose mod is not in the catalog (database not loaded, or the mod was removed online) is still
    /// listed, using what the record itself holds, so the user can delete it. No network.
    /// </summary>
    public sealed class DownloadsDataSource : IDownloadsDataSource
    {
        private readonly AdapterContext _context;

        public DownloadsDataSource(AdapterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _context = context;
        }

        public IList<DownloadRowInfo> GetDownloads()
        {
            List<DownloadRowInfo> rows = new List<DownloadRowInfo>();
            SettingsData settings = _context.Settings;

            if (settings.DownloadedMods == null)
            {
                return rows;
            }

            foreach (DownloadedItem item in settings.DownloadedMods)
            {
                if (item == null)
                {
                    continue;
                }

                rows.Add(BuildRow(item));
            }

            return rows;
        }

        private DownloadRowInfo BuildRow(DownloadedItem item)
        {
            DatabaseCatalog catalog = _context.Catalog;
            DownloadFiles file = item.DownloadFile;

            string modType = string.Empty;
            string region = file != null && !string.IsNullOrEmpty(file.Region) ? file.Region : "n/a";
            string version = file != null ? file.Version : string.Empty;
            string fileName = file != null && !string.IsNullOrEmpty(file.Name)
                ? file.Name
                : (string.IsNullOrEmpty(item.FilePath) ? string.Empty : Path.GetFileName(item.FilePath));

            ModItemData mod = catalog.FindMod(item.Platform, item.ModId, item.CategoryId);

            if (mod != null)
            {
                modType = mod.ModType ?? string.Empty;
            }
            else if (item.Platform == Platform.PS4)
            {
                AppItemData app = catalog.FindApp(item.ModId, item.CategoryId);

                if (app != null && string.IsNullOrEmpty(fileName))
                {
                    fileName = app.Name;
                }
            }
            else if (!string.IsNullOrEmpty(item.Url) && catalog.PackagesPs3 != null)
            {
                foreach (PackageItemData package in catalog.PackagesPs3)
                {
                    if (package != null && string.Equals(package.Url, item.Url, StringComparison.Ordinal))
                    {
                        if (string.IsNullOrEmpty(fileName))
                        {
                            fileName = package.Name;
                        }

                        break;
                    }
                }
            }

            // The old grid marked a download whose file was gone.
            if (!string.IsNullOrEmpty(item.FilePath) && !File.Exists(item.FilePath) && !Directory.Exists(item.FilePath))
            {
                fileName = fileName + " (file missing)";
            }

            return new DownloadRowInfo
            {
                Key = item.Platform + "|" + item.CategoryId + "|" + item.ModId + "|" + item.FilePath,
                ModId = item.ModId,
                PlatformText = AdapterMap.PlatformName(item.Platform),
                CategoryId = item.CategoryId,
                CategoryTitle = catalog.CategoryTitle(item.CategoryId),
                FileName = fileName,
                FilePath = item.FilePath,
                ModType = modType,
                Region = region,
                Version = version,
                DateTime = item.DateTime,
                Tag = item
            };
        }

        /// <summary>
        /// Deletes the downloaded archive and forgets the record. The record stays when the file cannot be deleted,
        /// so the list does not lie about what is on disk. A file that is already gone is not an error.
        /// </summary>
        public void DeleteDownload(DownloadRowInfo row)
        {
            if (row == null)
            {
                return;
            }

            SettingsData settings = _context.Settings;
            DownloadedItem item = row.Tag as DownloadedItem;

            if (item == null && settings.DownloadedMods != null)
            {
                item = settings.DownloadedMods.Find(delegate (DownloadedItem x)
                {
                    return x != null && x.ModId == row.ModId
                        && AdapterMap.Same(x.CategoryId, row.CategoryId)
                        && string.Equals(x.FilePath, row.FilePath, StringComparison.Ordinal);
                });
            }

            string path = item != null ? item.FilePath : row.FilePath;

            if (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }

            if (item != null && settings.DownloadedMods != null)
            {
                settings.DownloadedMods.Remove(item);
            }

            _context.Save();
        }
    }
}
