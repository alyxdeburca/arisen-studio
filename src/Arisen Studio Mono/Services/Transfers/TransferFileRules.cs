using System;
using System.IO;

namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Small matching rules from the old TransferDialog. Rules that already have a port are not repeated here:
    /// the suffix file match is InstallBackupPlanner.LocalFileMatchesInstallPath, the game-folder and {USBDEV}
    /// tests are InstallBackupPlanner.Classify, and the USB-port test is ConsolePaths.FindUsbPort.
    /// </summary>
    public static class TransferFileRules
    {
        /// <summary>
        /// True when the install file name equals the file name of the local path, ignoring case (ordinal).
        /// The caller supplies installFileName. Path.GetFileName is applied to localFilePath, as before.
        /// Original: TransferDialog.cs:1129, :2520, :2736.
        /// </summary>
        public static bool FileNamesMatchIgnoreCase(string installFileName, string localFilePath)
        {
            return string.Equals(installFileName, Path.GetFileName(localFilePath), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when the path contains "dev_hdd0/tmp/", the test that marks a tmp folder on the console.
        /// Original: TransferDialog.cs:590 (tests the item Value), :1436 (tests the template path).
        /// A null path throws, as in the dialog.
        /// </summary>
        public static bool IsTmpFolderPath(string path)
        {
            return path.Contains("dev_hdd0/tmp/");
        }
    }
}
