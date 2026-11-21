using System;
using System.IO;

namespace ArisenStudio.Services
{
    /// <summary>
    /// Where an install path points, which decides what the installer does with it.
    /// </summary>
    public enum InstallTarget
    {
        /// <summary>Anything else on the console: upload straight away.</summary>
        Other,

        /// <summary>A game folder ("dev_hdd0/game/"): back up the original first.</summary>
        GameFolder,

        /// <summary>A path built from the {USBDEV} placeholder: only valid if a USB device was found.</summary>
        UsbDevice
    }

    /// <summary>
    /// What to do about the backup of one game file before overwriting it.
    /// </summary>
    public enum BackupStep
    {
        /// <summary>Do not back up.</summary>
        None,

        /// <summary>No record yet: download the original and add a record.</summary>
        CreateNew,

        /// <summary>A record exists but its local file is gone: download it again and re-link the record.</summary>
        Redownload,

        /// <summary>A record exists and its local file is there: keep the record as it is.</summary>
        KeepRecord
    }

    /// <summary>
    /// Pure rules the old TransferDialog used while installing files to a PS3: placeholder substitution,
    /// which local file matches which install path, and when to back up the original game file.
    /// No FTP, no dialogs and no file reads: checkable without a console.
    /// </summary>
    public static class InstallBackupPlanner
    {
        public const string RegionPlaceholder = "{REGION}";
        public const string UserIdPlaceholder = "{USERID}";
        public const string UsbDevicePlaceholder = "{USBDEV}";

        public const string GameFolderMarker = "dev_hdd0/game/";

        public const string NoFileText = "No File Exists";

        /// <summary>
        /// Replace {REGION}, {USERID} and {USBDEV} in an install path template.
        /// A null value removes its placeholder (what string.Replace did in the old code).
        /// </summary>
        public static string ResolveInstallPath(string template, string gameRegion, string userId, string usbDevice)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            return template
                .Replace(RegionPlaceholder, gameRegion ?? string.Empty)
                .Replace(UserIdPlaceholder, userId ?? string.Empty)
                .Replace(UsbDevicePlaceholder, usbDevice ?? string.Empty);
        }

        /// <summary>
        /// Classify an install. <paramref name="template" /> is the path before substitution and
        /// <paramref name="resolvedPath" /> after, as the old code checked the game folder on the resolved
        /// path and the USB placeholder on the template.
        /// </summary>
        public static InstallTarget Classify(string template, string resolvedPath)
        {
            if (resolvedPath != null && resolvedPath.Contains(GameFolderMarker))
            {
                return InstallTarget.GameFolder;
            }

            if (template != null && template.Contains(UsbDevicePlaceholder))
            {
                return InstallTarget.UsbDevice;
            }

            return InstallTarget.Other;
        }

        /// <summary>
        /// Whether an install of this kind can go ahead. USB installs need a device name; the old code skipped
        /// them silently when none was found.
        /// </summary>
        public static bool CanInstall(InstallTarget target, string usbDevice)
        {
            return target != InstallTarget.UsbDevice || !string.IsNullOrEmpty(usbDevice);
        }

        /// <summary>
        /// Whether a local file (by name) is the one that the resolved install path wants.
        /// This is the same test the old code used: the install path ends with the file name. It is a suffix
        /// test, so "a.txt" also matches "/x/data.txt"; kept as it was so behaviour does not change.
        /// </summary>
        public static bool LocalFileMatchesInstallPath(string resolvedInstallPath, string localFileName)
        {
            if (string.IsNullOrEmpty(resolvedInstallPath) || string.IsNullOrEmpty(localFileName))
            {
                return false;
            }

            return resolvedInstallPath.EndsWith(localFileName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Folder of an install path with forward slashes and no trailing slash, for status text.
        /// "/dev_hdd0/game/X/USRDIR/a.sprx" gives "/dev_hdd0/game/X/USRDIR". A path with no folder gives "".
        /// </summary>
        public static string ParentFolder(string installPath)
        {
            if (installPath == null)
            {
                return string.Empty;
            }

            string normalized = installPath.Replace('\\', '/');
            int lastSlash = normalized.LastIndexOf('/');

            return lastSlash <= 0 ? (lastSlash == 0 ? "/" : string.Empty) : normalized.Substring(0, lastSlash);
        }

        /// <summary>
        /// Local path for a backup copy: &lt;backupRoot&gt;/&lt;folderId&gt;/&lt;fileName&gt;.
        /// <paramref name="folderId" /> is the game id (category id) for mods or the custom item id.
        /// </summary>
        public static string BackupLocalPath(string backupRoot, string folderId, string fileName)
        {
            if (string.IsNullOrEmpty(backupRoot))
            {
                throw new ArgumentException("backupRoot is empty", "backupRoot");
            }

            if (string.IsNullOrEmpty(folderId))
            {
                throw new ArgumentException("folderId is empty", "folderId");
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("fileName is empty", "fileName");
            }

            return Path.Combine(Path.Combine(backupRoot, folderId), fileName);
        }

        /// <summary>
        /// The decision made once per file about the backup, given whether a record exists and whether its
        /// local copy is on disk.
        /// </summary>
        public static BackupStep PlanBackup(bool shouldBackup, bool hasRecord, bool localFileExists)
        {
            if (!shouldBackup)
            {
                return BackupStep.None;
            }

            if (!hasRecord)
            {
                return BackupStep.CreateNew;
            }

            return localFileExists ? BackupStep.KeepRecord : BackupStep.Redownload;
        }

        /// <summary>
        /// Text for the size column of the backup list: "No File Exists" when the local copy is missing
        /// (<paramref name="fileBytes" /> is null), otherwise the formatted size or "N bytes".
        /// </summary>
        public static string SizeText(long? fileBytes, bool useFormattedSizes, Func<long, string> formatSize)
        {
            if (!fileBytes.HasValue)
            {
                return NoFileText;
            }

            if (useFormattedSizes && formatSize != null)
            {
                return formatSize(fileBytes.Value);
            }

            // "N0" gives thousands separators; the old detail pane used ToString("{0:n}"), which printed that text literally.
            return fileBytes.Value.ToString("N0") + " bytes";
        }

        /// <summary>
        /// Size of a local file, or null if it does not exist.
        /// </summary>
        public static long? GetLocalSize(string localPath)
        {
            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                return null;
            }

            return new FileInfo(localPath).Length;
        }
    }

    /// <summary>
    /// Remembers the answer to "back up the original game files?" across the files of one install.
    /// Port of the askedToBackupFiles / shouldBackupFiles pair in the old TransferDialog, with its behaviour:
    /// the user is asked at most once, only for a file that has no backup record yet, and once the answer
    /// is Yes it stays Yes for the rest of the install (including files that already have a record).
    /// Create one per install, not one per file.
    /// </summary>
    public sealed class BackupDecision
    {
        private bool _asked;
        private bool _should;

        /// <summary>Current answer after the last call to <see cref="Evaluate" />.</summary>
        public bool ShouldBackup
        {
            get { return _should; }
        }

        /// <summary>Whether the user has been asked yet.</summary>
        public bool Asked
        {
            get { return _asked; }
        }

        /// <summary>
        /// Update the answer for the next game file.
        /// </summary>
        /// <param name="hasRecord">A backup record already exists for this file.</param>
        /// <param name="alwaysBackup">The "always back up game files" setting.</param>
        /// <param name="askUser">Asks the user; returns true for Yes. Called at most once, and only if needed.</param>
        public bool Evaluate(bool hasRecord, bool alwaysBackup, Func<bool> askUser)
        {
            if (!hasRecord)
            {
                if (alwaysBackup)
                {
                    _should = true;
                }
                else if (!_asked)
                {
                    bool yes = askUser != null && askUser();
                    _asked = true;
                    _should = yes;
                }
            }

            return _should;
        }
    }
}
