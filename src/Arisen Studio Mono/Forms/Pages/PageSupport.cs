using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArisenStudio.Forms.Pages
{
    // Shared small types for the list pages (S3) and the settings page (S4): event args, row models, data-source
    // interfaces, and formatting helpers. Nothing here touches a control except where noted.

    /// <summary>Carries one item (a row model, a URL, a language name).</summary>
    public sealed class ItemEventArgs<T> : EventArgs
    {
        private readonly T item;

        public ItemEventArgs(T item)
        {
            this.item = item;
        }

        public T Item
        {
            get { return item; }
        }
    }

    /// <summary>A status line from a page. The shell may show it in its status strip. IsError is true for failures.</summary>
    public sealed class PageStatusEventArgs : EventArgs
    {
        private readonly string message;
        private readonly bool isError;

        public PageStatusEventArgs(string message, bool isError)
        {
            this.message = message ?? string.Empty;
            this.isError = isError;
        }

        public string Message
        {
            get { return message; }
        }

        public bool IsError
        {
            get { return isError; }
        }
    }

    /// <summary>A setting changed. Name is the SettingsData property name. The host saves the settings.</summary>
    public sealed class SettingChangedEventArgs : EventArgs
    {
        private readonly string name;

        public SettingChangedEventArgs(string name)
        {
            this.name = name;
        }

        public string Name
        {
            get { return name; }
        }
    }

    /// <summary>
    /// "Add to Modules" on an Xbox .xex file (ledger MW2-019). The host checks its module list and sets
    /// <see cref="Error"/> to refuse (empty or duplicate name); the page then shows the text as a warning.
    /// </summary>
    public sealed class AddToModulesEventArgs : EventArgs
    {
        private readonly string name;
        private readonly string path;

        public AddToModulesEventArgs(string name, string path)
        {
            this.name = name;
            this.path = path;
        }

        public string Name
        {
            get { return name; }
        }

        public string Path
        {
            get { return path; }
        }

        /// <summary>Set by the host to refuse the request. Leave null when the module was added.</summary>
        public string Error { get; set; }
    }

    // ------------------------------------------------------------------------------------------------------------
    // Downloads (ledger MW1-115 to MW1-133)
    // ------------------------------------------------------------------------------------------------------------

    /// <summary>One downloaded file, already resolved against the database by the data source.</summary>
    public sealed class DownloadRowInfo
    {
        /// <summary>Unique within the list (for example platform + category + mod id). Used to find the row again.</summary>
        public string Key { get; set; }

        public int ModId { get; set; }
        public string PlatformText { get; set; }
        public string CategoryId { get; set; }
        public string CategoryTitle { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ModType { get; set; }
        public string Region { get; set; }
        public string Version { get; set; }
        public DateTime DateTime { get; set; }

        /// <summary>Opaque handle the data source may use to find its own record again (the DownloadedItem).</summary>
        public object Tag { get; set; }
    }

    public interface IDownloadsDataSource
    {
        /// <summary>All downloads (the old Settings.DownloadedMods joined with the database). Called on every reload.</summary>
        IList<DownloadRowInfo> GetDownloads();

        /// <summary>Delete the file and forget the record (ledger MW1-117). Saving settings is up to the implementation.</summary>
        void DeleteDownload(DownloadRowInfo row);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Installed files (ledger MW1-134 to MW1-152)
    // ------------------------------------------------------------------------------------------------------------

    /// <summary>One installed mod of one console profile.</summary>
    public sealed class InstalledRowInfo
    {
        /// <summary>Unique within the list (for example profile id + category + mod id + custom flag).</summary>
        public string Key { get; set; }

        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public string PlatformText { get; set; }
        public string CategoryId { get; set; }
        public string CategoryTitle { get; set; }
        public int ModId { get; set; }
        public bool IsCustom { get; set; }
        public string Name { get; set; }
        public int TotalFiles { get; set; }
        public DateTime DateInstalled { get; set; }

        /// <summary>Opaque handle for the data source (the InstalledModInfo).</summary>
        public object Tag { get; set; }
    }

    public interface IInstalledFilesDataSource
    {
        /// <summary>Installed mods of every profile (ledger MW1-148).</summary>
        IList<InstalledRowInfo> GetInstalled();

        /// <summary>Forget the record (settings only, nothing is removed from the console) (ledger MW1-134).</summary>
        void ForgetInstalled(InstalledRowInfo row);
    }

    // ------------------------------------------------------------------------------------------------------------
    // File manager (ledger MW1-153 to MW1-173, MW2-001 to MW2-026)
    // ------------------------------------------------------------------------------------------------------------

    /// <summary>One line of a local or console directory listing.</summary>
    public sealed class FileManagerEntry
    {
        /// <summary>Real name, used to build paths.</summary>
        public string Name { get; set; }

        /// <summary>Text to show instead of Name (PS3 profile and game folders, ledger MW2-007). Null shows Name.</summary>
        public string DisplayName { get; set; }

        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
    }

    /// <summary>
    /// What the console half of the file manager needs. The shell supplies an implementation over the connected
    /// profile (FTP for PS3 and PS4, the devkit for Xbox). All members are called on the UI thread; the async ones
    /// may work on other threads. Exceptions are caught by the page and shown in its console status line.
    /// Download and upload must overwrite what is already there (the old code deleted the target first).
    /// </summary>
    public interface IConsoleFileService
    {
        bool IsConnected { get; }

        /// <summary>"/" for PlayStation, "\" for Xbox.</summary>
        string PathSeparator { get; }

        /// <summary>Entries for the drive combo, already in path form (for example "/dev_hdd0/" or "Hdd:\").</summary>
        IList<string> GetDrives();

        /// <summary>First folder to list (the remembered path, or a default). Null or empty means the first drive.</summary>
        string InitialPath { get; }

        /// <summary>True for Xbox: .xex rows get the "Add to Modules" button.</summary>
        bool SupportsAddToModules { get; }

        /// <summary>False when folders cannot be moved in one go (old Xbox code could not).</summary>
        bool SupportsFolderTransfer { get; }

        /// <summary>Lists a folder. Do not include the ".." row; the page adds it.</summary>
        Task<IList<FileManagerEntry>> ListAsync(string path);

        Task DownloadAsync(string consolePath, bool isDirectory, string localFolder);
        Task UploadAsync(string localPath, bool isDirectory, string consoleFolder);
        Task DeleteAsync(string consolePath, bool isDirectory);
        Task RenameAsync(string consolePath, string newName, bool isDirectory);
        Task<bool> ExistsAsync(string consolePath, bool isDirectory);
        Task CreateFolderAsync(string consolePath);
    }

    // ------------------------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------------------------

    /// <summary>The old equal, at-least, at-most picture button (ledger MW1-126, MW1-143, MW1-145).</summary>
    internal enum CompareMode
    {
        Equal,
        AtLeast,
        AtMost
    }

    internal static class PageUi
    {
        /// <summary>Asks the localizer first; a null or empty answer (or no localizer) gives the English fallback.</summary>
        public static string Text(Func<string, string> localizer, string key, string fallback)
        {
            if (localizer != null)
            {
                string text = localizer(key);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            return fallback;
        }

        public static CompareMode NextMode(CompareMode mode)
        {
            switch (mode)
            {
                case CompareMode.Equal:
                    return CompareMode.AtLeast;
                case CompareMode.AtLeast:
                    return CompareMode.AtMost;
                default:
                    return CompareMode.Equal;
            }
        }

        public static string ModeText(CompareMode mode)
        {
            switch (mode)
            {
                case CompareMode.AtLeast:
                    return ">=";
                case CompareMode.AtMost:
                    return "<=";
                default:
                    return "=";
            }
        }

        public static bool Compare(int value, int filter, CompareMode mode)
        {
            switch (mode)
            {
                case CompareMode.AtLeast:
                    return value >= filter;
                case CompareMode.AtMost:
                    return value <= filter;
                default:
                    return value == filter;
            }
        }

        /// <summary>Compares calendar days, ignoring the time of day.</summary>
        public static bool Compare(DateTime value, DateTime filter, CompareMode mode)
        {
            return Compare(value.Date.CompareTo(filter.Date), 0, mode);
        }

        public static bool ContainsIgnoreCase(string text, string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return true;
            }
            return text != null && text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatSize(long bytes, bool formatted)
        {
            if (!formatted)
            {
                return bytes.ToString("N0") + " bytes";
            }
            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? bytes.ToString("N0") + " bytes" : value.ToString("0.##") + " " + units[unit];
        }

        /// <summary>"3 months ago" style text, or the general date and time format ("g") when relative is false.</summary>
        public static string FormatDate(DateTime date, bool relative)
        {
            if (!relative)
            {
                return date.ToString("g");
            }
            TimeSpan span = DateTime.Now - date;
            if (span.TotalSeconds < 0)
            {
                return date.ToString("g");
            }
            if (span.TotalSeconds < 45)
            {
                return "just now";
            }
            if (span.TotalMinutes < 60)
            {
                return Count((int)Math.Max(1, Math.Round(span.TotalMinutes)), "minute");
            }
            if (span.TotalHours < 24)
            {
                return Count((int)Math.Round(span.TotalHours), "hour");
            }
            if (span.TotalDays < 30)
            {
                return Count((int)Math.Round(span.TotalDays), "day");
            }
            if (span.TotalDays < 365)
            {
                return Count((int)Math.Max(1, Math.Round(span.TotalDays / 30)), "month");
            }
            return Count((int)Math.Max(1, Math.Round(span.TotalDays / 365)), "year");
        }

        private static string Count(int number, string unit)
        {
            return number + " " + unit + (number == 1 ? string.Empty : "s") + " ago";
        }

        /// <summary>Removes characters a file name cannot hold (the old RemoveInvalidChars).</summary>
        public static string CleanFileName(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c.ToString(), string.Empty);
            }
            return name.Trim();
        }
    }
}
