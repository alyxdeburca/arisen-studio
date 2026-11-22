using System;
using System.IO;

namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Local folder and file path text that the old TransferDialog built for USB copies, package and game-save
    /// downloads, and copy targets. Pure string building, no I/O. Callers pass names that are already cleaned
    /// with RemoveInvalidChars wherever the old code cleaned them.
    /// Paths are built with Path.Combine, so they use the separator of the running OS (backslash on Windows, slash on
    /// Linux). On Windows the text is the same as the old code gave for normal input. Two differences: a folder that
    /// already ends in a separator no longer gets a doubled one, and an empty part is skipped.
    /// Path.Combine drops everything before a part that is itself rooted, so callers must not pass a rooted name part.
    /// </summary>
    public static class TransferLocalPaths
    {
        /// <summary>
        /// USB target folder: drive + kindFolder + first + second, with a trailing separator.
        /// kindFolder may hold the old backslash text (for example "PS4\Homebrew"); its backslashes become the OS separator.
        /// Original: TransferDialog.cs:737 (kind "Mods"), :1869 (kind "PS4\Homebrew"), :2342 (kind "Game Saves"),
        /// :2657 (kind "Mods", first = TitleId).
        /// </summary>
        public static string UsbInstallFolder(string drive, string kindFolder, string first, string second)
        {
            return Path.Combine(drive, NormalizeSeparators(kindFolder), first, second) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Download folder for a package install: downloads + name, with a trailing separator.
        /// Original: TransferDialog.cs:1589.
        /// </summary>
        public static string PackageInstallDownloadFolder(string downloads, string name)
        {
            return Path.Combine(downloads, name) + Path.DirectorySeparatorChar;
        }

        /// <summary>
        /// Download folder for a package download: downloads/Packages/name (region).
        /// Original: TransferDialog.cs:1755. The folder has no trailing separator.
        /// </summary>
        public static string PackageDownloadFolder(string downloads, string name, string region)
        {
            return Path.Combine(downloads, "Packages", name + " (" + region + ")");
        }

        /// <summary>
        /// A file inside a folder: Path.Combine(folder, fileName). A trailing separator on the folder is fine and is not doubled
        /// (the old code doubled the backslash).
        /// Original: TransferDialog.cs:1592 (fileName = name + ".pkg"), :1593 (fileName = ContentId + ".rap"), :2591 (fileName = name + ".pkg").
        /// </summary>
        public static string FileInFolder(string folder, string fileName)
        {
            return Path.Combine(folder, fileName);
        }

        /// <summary>
        /// Copy target used by the local copy loops: folder + the file name of localFilePath. The folder may or may not end in a
        /// separator (the old code needed the trailing backslash). Path.GetFileName is called on the local path as before.
        /// Original: TransferDialog.cs:757, :1883, :2362, :2677.
        /// </summary>
        public static string CopyTarget(string folder, string localFilePath)
        {
            return Path.Combine(folder, Path.GetFileName(localFilePath));
        }

        private static string NormalizeSeparators(string path)
        {
            return path == null ? null : path.Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
