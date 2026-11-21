namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Text composition for the status line, the header label and the webMAN popup of the old TransferDialog.
    /// The localised words are passed in by the caller (they come from the resource language), and the
    /// caller converts model properties such as Version, Region and Category to text before calling. Only the
    /// joining is done here.
    /// </summary>
    public static class TransferStatusText
    {
        /// <summary>
        /// Header label: first line, a newline, then the second line, with each ampersand doubled so the
        /// label does not show a mnemonic.
        /// Original: TransferDialog.cs:79, :91, :95, :275-304, :494-499, :832-837, :1214-1219, :1954-1959, :2103-2108.
        /// </summary>
        public static string HeaderLabel(string first, string second)
        {
            return (first + "\n" + second).Replace("&", "&&");
        }

        /// <summary>
        /// Header label for a package or application: the second line is "name (detail)", then the same
        /// ampersand doubling as HeaderLabel.
        /// Original: TransferDialog.cs:83 (detail = Region), :87 (detail = Version).
        /// </summary>
        public static string HeaderLabelWithDetail(string first, string name, string detail)
        {
            return (first + "\n" + name + " (" + detail + ")").Replace("&", "&&");
        }

        /// <summary>
        /// "name (index/total)" used in file progress lines.
        /// Original: TransferDialog.cs:380, :388, :550, :567, :643, :756, :1034, :1045, :1054, :1390, :1409, :1417,
        /// :1486, :1882, :1976, :2165, :2182, :2234, :2361, :2454, :2522, :2676.
        /// </summary>
        public static string FileCounter(string name, int index, int total)
        {
            return name + " (" + index + "/" + total + ")";
        }

        /// <summary>
        /// "Installing file: name (index/total) to destination", the Xbox status line.
        /// Original: TransferDialog.cs:436 and :2036 (destination = parent folder), :1131 and :2738 (destination = install path).
        /// The caller chooses which.
        /// </summary>
        public static string XboxInstallingLine(string name, int index, int total, string destination)
        {
            return "Installing file: " + name + " (" + index + "/" + total + ") to " + destination;
        }

        /// <summary>
        /// "name vVersion", used in the "already installed" and "replace" prompts.
        /// Original: TransferDialog.cs:244, :267, :291, :803, :824, :1925, :1946.
        /// </summary>
        public static string NameWithVersion(string name, string version)
        {
            return name + " v" + version;
        }

        /// <summary>
        /// "name (category)", used in the package prompts.
        /// Original: TransferDialog.cs:1579, :1701.
        /// </summary>
        public static string NameWithCategory(string name, string category)
        {
            return name + " (" + category + ")";
        }

        /// <summary>
        /// webMAN popup after an install: categoryTitle, a newline, "Installed name (n files)".
        /// Original: TransferDialog.cs:402, :1087, :1998, :2479. Note there is no colon after "Installed".
        /// </summary>
        public static string InstalledPopup(string categoryTitle, string itemName, int fileCount)
        {
            return categoryTitle + "\nInstalled " + itemName + " (" + fileCount + " files)";
        }

        /// <summary>
        /// webMAN popup after an uninstall: categoryTitle, a newline, "Uninstalled: name (n files)".
        /// Original: TransferDialog.cs:607, :1452, :2201. Note the colon after "Uninstalled".
        /// </summary>
        public static string UninstalledPopup(string categoryTitle, string itemName, int fileCount)
        {
            return categoryTitle + "\nUninstalled: " + itemName + " (" + fileCount + " files)";
        }

        /// <summary>
        /// Adds one space and a suffix to a finished-status text. An empty suffix still leaves the trailing space,
        /// as the dialog does. The caller keeps the test for whether the suffix applies (game category) and the
        /// language lookup.
        /// Original: TransferDialog.cs:405, :461, :1090, :1178, :2003, :2068, :2484, :2549; :2834 passes the
        /// "ready to start" text unconditionally.
        /// </summary>
        public static string WithSuffix(string text, string suffix)
        {
            return text + " " + suffix;
        }

        /// <summary>
        /// Opening status line for an install or download: prefix + " : name (categoryId)", formatted with
        /// string.Format as the dialog does.
        /// Original: TransferDialog.cs:685 (prefix = PREPARING_INSTALL), :1530 (prefix = PREPARING_DOWNLOAD).
        /// </summary>
        public static string PreparingLine(string prefix, string name, string categoryId)
        {
            return string.Format(prefix + " : {0} ({1})", name, categoryId);
        }
    }
}
