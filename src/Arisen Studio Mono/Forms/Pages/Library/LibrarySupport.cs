using System;
using System.Collections.Generic;

namespace ArisenStudio.Forms.Pages.Library
{
    // Shared types for the library and catalog pages (design slice S5): the nine pages that list what the online
    // database offers (custom mods, game mods, homebrew, trainers, resources, packages, games, game saves, game cheats).
    // Nothing here touches a control. Like the S3 pages, these types do not depend on the shell's Platform type or on
    // the database classes; the shell maps its own data into LibraryRowInfo.

    /// <summary>Which library page a data request is for.</summary>
    public enum LibraryKind
    {
        CustomMods,
        GameMods,
        Homebrew,
        Trainers,
        Resources,
        Packages,
        Games,
        GameSaves,
        GameCheats
    }

    /// <summary>
    /// The console platform a library page shows (the old pages were split per platform: PageGameModsPS3, PageGameModsXbox,
    /// PageHomebrewPS3, PageHomebrewXbox, PageHomebrewPS4). The shell sets it from the connected profile.
    /// </summary>
    public enum LibraryPlatform
    {
        PS3,
        PS4,
        Xbox360
    }

    /// <summary>The status texts of the Status column and the Status filter (old LABEL_INSTALLED and so on).</summary>
    public static class LibraryStatus
    {
        public const string Installed = "Installed";
        public const string Downloaded = "Downloaded";
        public const string NotInstalled = "Not installed";
        public const string Downloading = "Downloading...";
        public const string Installing = "Installing...";
        public const string Uninstalling = "Uninstalling...";
    }

    /// <summary>
    /// One line of a library page. One class serves all nine pages: each page shows the fields it needs and ignores the rest
    /// (the old pages each built their own DataTable). The data source fills what the page needs; every text may be null.
    /// </summary>
    public sealed class LibraryRowInfo
    {
        /// <summary>Unique within the list (for example kind + platform + mod id, or the package URL, or the cheat file). Used to find the row again.</summary>
        public string Key { get; set; }

        /// <summary>The mod, app, save or custom mod id (0 when the item has none).</summary>
        public int Id { get; set; }

        /// <summary>"PS3", "PS4", "XBOX360" and so on, as the old Platform text. Shown by the Custom Mods page.</summary>
        public string Platform { get; set; }

        public string CategoryId { get; set; }

        /// <summary>The game or category title (the "Game" column of game mods and saves, "Category" elsewhere).</summary>
        public string Category { get; set; }

        /// <summary>The mod, homebrew, package or save name. Trainers and cheats store the game title here.</summary>
        public string Name { get; set; }

        /// <summary>CEX, DEX and so on.</summary>
        public string SystemType { get; set; }

        public string ModType { get; set; }
        public string Region { get; set; }
        public string Version { get; set; }

        /// <summary>The PS4 firmware version.</summary>
        public string Firmware { get; set; }

        public string Creator { get; set; }

        /// <summary>One of <see cref="LibraryStatus"/> (Installed, Downloaded, Not installed), or a transfer text.</summary>
        public string Status { get; set; }

        /// <summary>Package size in bytes. Negative means the size is missing.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Package modified date. Null means the date is missing.</summary>
        public DateTime? Modified { get; set; }

        /// <summary>Trainers and packages.</summary>
        public string TitleId { get; set; }

        /// <summary>Trainers: the dashboard type.</summary>
        public string DashType { get; set; }

        /// <summary>Trainers: how many trainers the game has.</summary>
        public int Count { get; set; }

        /// <summary>Packages: the download URL.</summary>
        public string Url { get; set; }

        /// <summary>True when the item is in the favourites (Settings.FavoriteMods). Set by the data source on every load.</summary>
        public bool IsFavorite { get; set; }

        /// <summary>Opaque handle the data source or the shell may use to find its own record again (ModItemData, CustomItemData, ...).</summary>
        public object Tag { get; set; }
    }

    /// <summary>
    /// Where the library pages get their rows and where they send their few writes. The shell supplies an implementation over
    /// the database, the settings and the connected profile (status Installed or Downloaded comes from the settings).
    /// All members run on the UI thread, so a real implementation should read from memory (the old pages did).
    /// </summary>
    public interface ILibraryDataSource
    {
        /// <summary>
        /// All rows of a page, unfiltered. The page filters, sorts and shows them. Called on every reload.
        /// Platform is the page's platform; kinds that have no platform (packages, resources, cheats, trainers, custom mods) ignore it.
        /// Return an empty list when there is nothing (also when no console profile is chosen).
        /// </summary>
        IList<LibraryRowInfo> GetItems(LibraryKind kind, LibraryPlatform platform);

        /// <summary>
        /// Adds the item to the favourites (ledger MW2-086, MW3-012). Returns false when it already was a favourite.
        /// Saving the settings is up to the implementation or the shell.
        /// </summary>
        bool AddFavorite(LibraryKind kind, LibraryRowInfo row);

        /// <summary>Removes a custom mod from the settings (ledger MW2-069). Only the Custom Mods page calls it.</summary>
        void DeleteCustomMod(LibraryRowInfo row);
    }

    /// <summary>
    /// STUB. Not a real data source: every list is empty and nothing is saved (favourites are only remembered in memory).
    /// It exists so that the library pages can be created and shown before the shell wires in the database and the settings.
    /// Replace it by setting the page's DataSource property.
    /// </summary>
    public sealed class StubLibraryDataSource : ILibraryDataSource
    {
        private readonly HashSet<string> favorites = new HashSet<string>();

        public IList<LibraryRowInfo> GetItems(LibraryKind kind, LibraryPlatform platform)
        {
            return new List<LibraryRowInfo>();
        }

        public bool AddFavorite(LibraryKind kind, LibraryRowInfo row)
        {
            if (row == null)
            {
                return false;
            }
            return favorites.Add(kind + "|" + row.Key);
        }

        public void DeleteCustomMod(LibraryRowInfo row)
        {
            // Nothing is stored, so there is nothing to delete.
        }
    }
}
