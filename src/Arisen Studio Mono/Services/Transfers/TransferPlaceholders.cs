namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Single-token placeholder substitution that the old TransferDialog did on install and uninstall paths.
    /// The three-token PS3 substitution ({REGION}, {USERID}, {USBDEV}, at TransferDialog.cs:968-971 and 1368-1370)
    /// is not here: InstallBackupPlanner.ResolveInstallPath already covers it.
    /// A null replacement removes the token, as string.Replace does.
    /// </summary>
    public static class TransferPlaceholders
    {
        /// <summary>
        /// Replaces {CATEGORYID} with the category id in upper case.
        /// Original: TransferDialog.cs:1124, :1479, :2032. ToUpper() stays inside the helper, so a null id throws
        /// and the casing follows the current culture, as in the dialog. The Xbox application uninstall at :2226
        /// applies no substitution; that is the caller's behaviour and is not changed here.
        /// </summary>
        public static string ResolveCategoryId(string template, string categoryId)
        {
            return template.Replace("{CATEGORYID}", categoryId.ToUpper());
        }

        /// <summary>
        /// Replaces {AURORAPATH} with the Aurora folder path from the settings.
        /// Original: TransferDialog.cs:2729.
        /// </summary>
        public static string ResolveAuroraPath(string template, string auroraFolderPath)
        {
            return template.Replace("{AURORAPATH}", auroraFolderPath);
        }

        /// <summary>
        /// Replaces {USBDEV} only. Original: TransferDialog.cs:2447 (game save install to PS3).
        /// InstallBackupPlanner.ResolveInstallPath would also strip {REGION} and {USERID} and throws a different
        /// exception for a null template, so the two are not interchangeable.
        /// </summary>
        public static string ResolveUsbDevice(string template, string usbDevice)
        {
            return template.Replace("{USBDEV}", usbDevice);
        }
    }
}
