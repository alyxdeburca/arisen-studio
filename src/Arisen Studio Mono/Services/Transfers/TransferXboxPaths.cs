using System.IO;

namespace ArisenStudio.Services.Transfers
{
    /// <summary>
    /// Xbox 360 path text that the old TransferDialog built for XDevkit MakeDirectory/SendFile calls, and the
    /// Aurora folder split and backslash clean-up it did on the way. Pure string work, no XDevkit calls here.
    /// Xbox paths use backslashes and "Hdd:\"; these helpers keep that text exactly as the dialog wrote it.
    /// </summary>
    public static class TransferXboxPaths
    {
        /// <summary>
        /// Category folder under Hdd:\ArisenStudio\, upper-cased, with a trailing backslash.
        /// Original: TransferDialog.cs:445 (customItem.Category), :2042 (appItem.CategoryId).
        /// </summary>
        public static string CategoryFolder(string category)
        {
            return @"Hdd:\ArisenStudio\" + category.ToUpper() + @"\";
        }

        /// <summary>
        /// Trainers folder for one title, with a trailing backslash: Hdd:\ArisenStudio\Trainers\titleId\.
        /// Original: TransferDialog.cs:2796 (MakeDirectory argument).
        /// </summary>
        public static string TrainersTitleFolder(string titleId)
        {
            return @"Hdd:\ArisenStudio\Trainers\" + titleId + @"\";
        }

        /// <summary>
        /// Trainers folder for one title, no trailing backslash: Hdd:\ArisenStudio\Trainers\titleId.
        /// Original: TransferDialog.cs:2799 (first argument of PathExists). Not the same text as
        /// TrainersTitleFolder; both are kept as the dialog wrote them.
        /// </summary>
        public static string TrainersTitleFolderNoSlash(string titleId)
        {
            return @"Hdd:\ArisenStudio\Trainers\" + titleId;
        }

        /// <summary>
        /// Trainer name folder: Hdd:\ArisenStudio\ + titleId upper-cased + \ + trainerName + \.
        /// Original: TransferDialog.cs:2801. This string has no "Trainers\" part and upper-cases the id, unlike
        /// :2796 and :2799. That mismatch is in the old code and is kept here as it was written.
        /// </summary>
        public static string TrainerNameFolder(string titleId, string trainerName)
        {
            return @"Hdd:\ArisenStudio\" + titleId.ToUpper() + @"\" + trainerName + @"\";
        }

        /// <summary>
        /// Folder part of a path, then a backslash: Path.GetDirectoryName(path) + \.
        /// Original: TransferDialog.cs:434, :2034. Under Mono on Linux, Path.GetDirectoryName does not split on
        /// backslashes, so this result differs from Windows for Xbox paths. Needs a decision by the launching session.
        /// </summary>
        public static string ParentFolderWithBackslash(string path)
        {
            return Path.GetDirectoryName(path) + @"\";
        }

        /// <summary>
        /// Replaces each pair of backslashes with one, once.
        /// Original: TransferDialog.cs:1133, :2733, :2740.
        /// </summary>
        public static string CollapseBackslashesOnce(string path)
        {
            return path.Replace("\\\\", "\\");
        }

        /// <summary>
        /// Replaces each pair of backslashes with one, applied twice in a row, as the dialog chains it.
        /// Original: TransferDialog.cs:448, :1139, :2044, :2774, :2804.
        /// </summary>
        public static string CollapseBackslashesTwice(string path)
        {
            return path.Replace("\\\\", "\\").Replace("\\\\", "\\");
        }

        /// <summary>
        /// Splits an Aurora folder path such as Hdd:\Aurora\ into its last folder name and the path that leads to it.
        /// Original: TransferDialog.cs:2746-2755. Trailing backslashes and slashes are trimmed, then only a
        /// backslash is searched for. When none is found, both outputs are empty and the function returns false;
        /// the dialog logs "Invalid Aurora Path" at that point, and that log call stays in the dialog.
        /// No null guard: a null path throws, as it did before.
        /// </summary>
        public static bool TrySplitAuroraFolder(string auroraFolderPath, out string parentFolder, out string parentFolderPath)
        {
            parentFolder = string.Empty;
            parentFolderPath = string.Empty;

            string normalizedPath = auroraFolderPath.TrimEnd('\\', '/');

            int lastSlashIndex = normalizedPath.LastIndexOf('\\');

            if (lastSlashIndex < 0)
            {
                return false;
            }

            parentFolder = normalizedPath.Substring(lastSlashIndex + 1);
            parentFolderPath = normalizedPath.Substring(0, lastSlashIndex) + "\\";
            return true;
        }
    }
}
