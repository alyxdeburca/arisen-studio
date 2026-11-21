using System;
using System.IO;

namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Local folder and file path text that the old TransferDialog built for USB copies, package and game-save
    /// downloads, and copy targets. Pure string building, no I/O. Callers pass names that are already cleaned
    /// with RemoveInvalidChars wherever the old code cleaned them. Backslashes are kept as the old code wrote them.
    /// </summary>
    public static class TransferLocalPaths
    {
        /// <summary>
        /// USB target folder: drive + kindFolder + \first\second\.
        /// Original: TransferDialog.cs:737 (kind "Mods"), :1869 (kind "PS4\Homebrew"), :2342 (kind "Game Saves"),
        /// :2657 (kind "Mods", first = TitleId).
        /// </summary>
        public static string UsbInstallFolder(string drive, string kindFolder, string first, string second)
        {
            return drive + kindFolder + @"\" + first + @"\" + second + @"\";
        }

        /// <summary>
        /// Download folder for a package install: downloads + \name\.
        /// Original: TransferDialog.cs:1589.
        /// </summary>
        public static string PackageInstallDownloadFolder(string downloads, string name)
        {
            return downloads + @"\" + name + @"\";
        }

        /// <summary>
        /// Download folder for a package download: downloads\Packages\name (region).
        /// Original: TransferDialog.cs:1755. The folder has no trailing backslash.
        /// </summary>
        public static string PackageDownloadFolder(string downloads, string name, string region)
        {
            return downloads + @"\Packages\" + name + " (" + region + ")";
        }

        /// <summary>
        /// A file inside a folder: folder + \ + fileName. A trailing backslash on the folder is not checked,
        /// so a folder that already ends in \ gives a doubled backslash, as the old code did.
        /// Original: TransferDialog.cs:1592, :1593 (fileName = name + ".rap" / ".pkg"), :2591 (fileName = name + ".pkg").
        /// </summary>
        public static string FileInFolder(string folder, string fileName)
        {
            return folder + @"\" + fileName;
        }

        /// <summary>
        /// Copy target used by the local copy loops: folder + the file name of localFilePath.
        /// Original: TransferDialog.cs:757, :1883, :2362, :2677. Path.GetFileName is called on the local path as before.
        /// </summary>
        public static string CopyTarget(string folder, string localFilePath)
        {
            return folder + Path.GetFileName(localFilePath);
        }
    }
}
