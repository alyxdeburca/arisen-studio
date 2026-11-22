using System;
using System.Collections.Generic;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;

namespace ArisenStudio.Services.Adapters
{
    // Shared pieces for the adapters in this folder (data sources, FTP file transfer, install and uninstall,
    // screenshot upload). Nothing here opens a connection, reads the network or touches a control.
    // The host builds ONE AdapterContext at start-up and hands it to each adapter.

    /// <summary>
    /// One game that has cheat files (the Game Cheats page). The host fills these from its GamesDataPS3 / game cheats
    /// data; this folder does not depend on those types, so they cannot hold the build up.
    /// </summary>
    public sealed class GameCheatEntry
    {
        public string Game { get; set; }

        public string Region { get; set; }

        public string Version { get; set; }

        /// <summary>The cheat file name. Used as the row key.</summary>
        public string FileName { get; set; }

        /// <summary>Opaque handle handed back on <see cref="ArisenStudio.Forms.Pages.Library.LibraryRowInfo.Tag"/>.</summary>
        public object Tag { get; set; }
    }

    /// <summary>
    /// The loaded online database, as plain references the host fills in after it has fetched or read the cache.
    /// Every member may be null (not loaded yet): the data sources then return empty lists.
    /// This class does no loading, so it makes no network call.
    /// </summary>
    public sealed class DatabaseCatalog
    {
        public CategoriesData Categories { get; set; }

        public ModsData GameModsPs3 { get; set; }

        public ModsData HomebrewPs3 { get; set; }

        public ModsData ResourcesPs3 { get; set; }

        public ModsData GameModsXbox { get; set; }

        public ModsData HomebrewXbox { get; set; }

        /// <summary>PS4 homebrew and games (one list; the category type tells them apart).</summary>
        public AppsData AppsPs4 { get; set; }

        /// <summary>All PS3 packages (games, demos, DLC, avatars, themes) as one flat list.</summary>
        public IList<PackageItemData> PackagesPs3 { get; set; }

        public IList<GameSaveItemData> GameSaves { get; set; }

        public IList<TrainerGameItem> TrainersXbox { get; set; }

        public IList<GameCheatEntry> GameCheats { get; set; }

        /// <summary>Gives a title for an Xbox 360 title id (the trainers page). Null shows the title id.</summary>
        public Func<string, string> TitleIdToTitle { get; set; }

        /// <summary>Category by id, or null. Never throws (CategoriesData.GetCategoryById does).</summary>
        public CategoryItem FindCategory(string categoryId)
        {
            if (Categories == null || Categories.Categories == null || string.IsNullOrEmpty(categoryId))
            {
                return null;
            }

            foreach (CategoryItem category in Categories.Categories)
            {
                if (category != null && string.Equals(category.Id, categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }

            return null;
        }

        /// <summary>The category title, or the id when the category is unknown.</summary>
        public string CategoryTitle(string categoryId)
        {
            CategoryItem category = FindCategory(categoryId);
            return category != null && category.Title != null ? category.Title : (categoryId ?? string.Empty);
        }

        /// <summary>The category type, Game when the category is unknown (as ModItemData.GetCategoryType does).</summary>
        public CategoryType CategoryTypeOf(string categoryId)
        {
            CategoryItem category = FindCategory(categoryId);
            return category != null ? category.CategoryType : CategoryType.Game;
        }

        /// <summary>The mod libraries that hold mods for a platform (PS4 has none: it uses <see cref="AppsPs4"/>).</summary>
        public IEnumerable<ModsData> ModLibraries(Platform platform)
        {
            List<ModsData> list = new List<ModsData>();

            if (platform == Platform.PS3)
            {
                list.Add(GameModsPs3);
                list.Add(HomebrewPs3);
                list.Add(ResourcesPs3);
            }
            else if (platform == Platform.XBOX360)
            {
                list.Add(GameModsXbox);
                list.Add(HomebrewXbox);
            }

            list.RemoveAll(delegate (ModsData x) { return x == null || x.Library == null; });
            return list;
        }

        /// <summary>Finds a mod by platform and id (and category id when given). Null when not found.</summary>
        public ModItemData FindMod(Platform platform, int modId, string categoryId)
        {
            foreach (ModsData library in ModLibraries(platform))
            {
                foreach (ModItemData mod in library.Library)
                {
                    if (mod != null
                        && mod.Id == modId
                        && mod.GetPlatform() == platform
                        && (string.IsNullOrEmpty(categoryId) || string.Equals(mod.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return mod;
                    }
                }
            }

            return null;
        }

        /// <summary>Finds a PS4 app by id (and category id when given). Null when not found.</summary>
        public AppItemData FindApp(int id, string categoryId)
        {
            if (AppsPs4 == null || AppsPs4.Library == null)
            {
                return null;
            }

            foreach (AppItemData app in AppsPs4.Library)
            {
                if (app != null
                    && app.Id == id
                    && (string.IsNullOrEmpty(categoryId) || string.Equals(app.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase)))
                {
                    return app;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// What every adapter needs from the host: the catalog, the settings, the connected profile and the save hooks.
    /// Create it once and share it. All members may be left null; the adapters then use AppHost.Settings, report
    /// "no console profile" for console work, and skip saving.
    /// </summary>
    public sealed class AdapterContext
    {
        private DatabaseCatalog _catalog = new DatabaseCatalog();

        public DatabaseCatalog Catalog
        {
            get { return _catalog; }
            set { _catalog = value ?? new DatabaseCatalog(); }
        }

        /// <summary>Returns the settings object. Null uses <see cref="AppHost.Settings"/> (it is replaced by LoadSettings, so it is read each time).</summary>
        public Func<SettingsData> SettingsProvider { get; set; }

        /// <summary>Returns the connected console profile, or null when none is connected.</summary>
        public Func<ConsoleProfile> ProfileProvider { get; set; }

        /// <summary>Writes the settings file (for example <c>() =&gt; AppHost.SaveSettings(UserFolders.SettingsData)</c>). Null: the change stays in memory only.</summary>
        public Action SaveSettings { get; set; }

        /// <summary>The backup file records. Needed only by installs that overwrite game files.</summary>
        public BackupFilesData BackupFiles { get; set; }

        /// <summary>Writes the backup records file. Null: the records stay in memory only.</summary>
        public Action SaveBackupFiles { get; set; }

        public SettingsData Settings
        {
            get
            {
                Func<SettingsData> provider = SettingsProvider;
                SettingsData settings = provider != null ? provider() : null;
                return settings ?? AppHost.Settings;
            }
        }

        public ConsoleProfile Profile
        {
            get
            {
                Func<ConsoleProfile> provider = ProfileProvider;
                return provider != null ? provider() : null;
            }
        }

        public void Save()
        {
            Action save = SaveSettings;
            if (save != null)
            {
                save();
            }
        }

        public void SaveBackups()
        {
            Action save = SaveBackupFiles;
            if (save != null)
            {
                save();
            }
        }
    }

    /// <summary>Small conversions the adapters share.</summary>
    internal static class AdapterMap
    {
        public static string PlatformName(Platform platform)
        {
            switch (platform)
            {
                case Platform.PS4:
                    return "PlayStation 4";
                case Platform.XBOX360:
                    return "Xbox 360";
                default:
                    return "PlayStation 3";
            }
        }

        /// <summary>"PS3", "PS4", "XBOX360": the short text the Custom Mods page shows.</summary>
        public static string PlatformShort(Platform platform)
        {
            switch (platform)
            {
                case Platform.PS4:
                    return "PS4";
                case Platform.XBOX360:
                    return "XBOX360";
                default:
                    return "PS3";
            }
        }

        public static Platform ToPlatform(ArisenStudio.Forms.Pages.Library.LibraryPlatform platform)
        {
            switch (platform)
            {
                case ArisenStudio.Forms.Pages.Library.LibraryPlatform.PS4:
                    return Platform.PS4;
                case ArisenStudio.Forms.Pages.Library.LibraryPlatform.Xbox360:
                    return Platform.XBOX360;
                default:
                    return Platform.PS3;
            }
        }

        public static Platform ToPlatform(ArisenStudio.Forms.Dashboard.DashboardPlatform platform)
        {
            switch (platform)
            {
                case ArisenStudio.Forms.Dashboard.DashboardPlatform.PS4:
                    return Platform.PS4;
                case ArisenStudio.Forms.Dashboard.DashboardPlatform.Xbox360:
                    return Platform.XBOX360;
                default:
                    return Platform.PS3;
            }
        }

        /// <summary>Removes the characters a file or folder name cannot hold (the old RemoveInvalidChars).</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c.ToString(), string.Empty);
            }

            return name.Trim();
        }

        public static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
