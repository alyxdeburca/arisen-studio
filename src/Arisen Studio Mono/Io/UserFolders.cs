using System;
using System.IO;

namespace ArisenStudio.Io
{
    public static class UserFolders
    {
        /// <summary>
        /// Get the user's application data folder for Arisen Studio.
        /// </summary>
        internal static readonly string AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arisen Studio") + Path.DirectorySeparatorChar;

        /// <summary>
        /// Get the user's settings details file.
        /// </summary>
        internal static readonly string SettingsData = Path.Combine(AppData, "SettingsData.json");

        /// <summary>
        /// Get the backup files details file.
        /// </summary>
        internal static readonly string BackupFilesData = Path.Combine(AppData, "BackupFilesData.json");

        /// <summary>
        /// Get the game backup files directory path.
        /// </summary>
        internal static readonly string BackupFiles = Path.Combine(AppData, "Backup Files") + Path.DirectorySeparatorChar;

        /// <summary>
        /// Get the directory path of the downloaded packages.
        /// </summary>
        internal static readonly string XboxProfiles = Path.Combine(AppData, "Xbox Profiles") + Path.DirectorySeparatorChar;

        /// <summary>
        /// Get the Logs directory where the app's logs reside.
        /// </summary>
        internal static readonly string Logs = Path.Combine(AppData, "Logs") + Path.DirectorySeparatorChar;
    }
}
