using System;
using System.Collections.Generic;
using System.Globalization;
using ArisenStudio.Database;
using ArisenStudio.Forms.Pages.Library;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="ILibraryDataSource"/> over the loaded catalog and the settings. Real, and reads from memory only:
    /// the catalog is filled by the host, so there is no network and no console access here.
    /// Status (Installed, Downloaded, Not installed) comes from the connected profile's installed mods and from
    /// <c>SettingsData.DownloadedMods</c>; with no profile everything is "Not installed" or "Downloaded".
    /// Favourites are written to <c>SettingsData.FavoriteMods</c> and <c>FavoriteGameSaves</c>.
    /// Kinds with nothing behind them yet return an empty list (for example Games on PS3).
    /// </summary>
    public sealed class LibraryDataSource : ILibraryDataSource
    {
        private readonly AdapterContext _context;

        public LibraryDataSource(AdapterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _context = context;
        }

        public IList<LibraryRowInfo> GetItems(LibraryKind kind, LibraryPlatform platform)
        {
            List<LibraryRowInfo> rows = new List<LibraryRowInfo>();
            Platform target = AdapterMap.ToPlatform(platform);
            DatabaseCatalog catalog = _context.Catalog;

            switch (kind)
            {
                case LibraryKind.CustomMods:
                    AddCustomMods(rows);
                    break;
                case LibraryKind.GameMods:
                    if (target == Platform.PS3)
                    {
                        AddMods(rows, kind, catalog.GameModsPs3, CategoryType.Game);
                    }
                    else if (target == Platform.XBOX360)
                    {
                        AddMods(rows, kind, catalog.GameModsXbox, CategoryType.Game);
                    }
                    break;
                case LibraryKind.Homebrew:
                    if (target == Platform.PS3)
                    {
                        AddMods(rows, kind, catalog.HomebrewPs3, CategoryType.Homebrew);
                    }
                    else if (target == Platform.XBOX360)
                    {
                        AddMods(rows, kind, catalog.HomebrewXbox, CategoryType.Homebrew);
                    }
                    else
                    {
                        AddApps(rows, kind, false);
                    }
                    break;
                case LibraryKind.Resources:
                    AddMods(rows, kind, catalog.ResourcesPs3, CategoryType.Resource);
                    break;
                case LibraryKind.Packages:
                    AddPackages(rows);
                    break;
                case LibraryKind.Games:
                    if (target == Platform.PS4)
                    {
                        AddApps(rows, kind, true);
                    }
                    break;
                case LibraryKind.GameSaves:
                    AddGameSaves(rows, target);
                    break;
                case LibraryKind.Trainers:
                    AddTrainers(rows);
                    break;
                case LibraryKind.GameCheats:
                    AddGameCheats(rows);
                    break;
            }

            return rows;
        }

        public bool AddFavorite(LibraryKind kind, LibraryRowInfo row)
        {
            if (row == null)
            {
                return false;
            }

            SettingsData settings = _context.Settings;
            List<FavoriteItem> list;

            switch (kind)
            {
                case LibraryKind.GameMods:
                case LibraryKind.Homebrew:
                case LibraryKind.Resources:
                case LibraryKind.Games:
                    if (settings.FavoriteMods == null)
                    {
                        settings.FavoriteMods = new List<FavoriteItem>();
                    }

                    list = settings.FavoriteMods;
                    break;
                case LibraryKind.Packages:
                    // The old TileItemPackagesAddFavorite_ItemClick (MainWindow.cs:7063-7071) stored Platform PS3, the package's category text as
                    // CategoryId, CategoryType Package and the package id, in FavoriteMods. Package rows have no CategoryId, so the Category text is used.
                    if (settings.FavoriteMods == null)
                    {
                        settings.FavoriteMods = new List<FavoriteItem>();
                    }

                    list = settings.FavoriteMods;
                    break;
                case LibraryKind.GameSaves:
                    if (settings.FavoriteGameSaves == null)
                    {
                        settings.FavoriteGameSaves = new List<FavoriteItem>();
                    }

                    list = settings.FavoriteGameSaves;
                    break;
                default:
                    throw new NotSupportedException("Favorites are not available for this list (" + kind + ").");
            }

            bool isPackage = kind == LibraryKind.Packages;
            Platform platform = isPackage ? Platform.PS3 : ParsePlatform(row.Platform);
            string categoryId = isPackage ? row.Category : row.CategoryId;
            CategoryType categoryType = isPackage ? CategoryType.Package : _context.Catalog.CategoryTypeOf(row.CategoryId);

            // SettingsData.AddRemoveFavoriteModItem compares by reference, so it would add a copy every time. Compare by value.
            // For a package the type is part of the match, so a mod with the same id and category text is not taken for it.
            foreach (FavoriteItem existing in list)
            {
                if (existing != null && existing.Platform == platform && existing.ModId == row.Id
                    && AdapterMap.Same(existing.CategoryId, categoryId)
                    && (!isPackage || existing.CategoryType == CategoryType.Package))
                {
                    return false;
                }
            }

            list.Add(new FavoriteItem
            {
                Platform = platform,
                ModId = row.Id,
                CategoryId = categoryId,
                CategoryType = categoryType
            });

            _context.Save();
            return true;
        }

        public void DeleteCustomMod(LibraryRowInfo row)
        {
            if (row == null)
            {
                return;
            }

            SettingsData settings = _context.Settings;

            if (settings.CustomMods == null)
            {
                return;
            }

            if (settings.CustomMods.RemoveAll(delegate (CustomItemData x) { return x != null && x.Id == row.Id; }) > 0)
            {
                _context.Save();
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Row builders
        // ------------------------------------------------------------------------------------------------------

        private void AddCustomMods(List<LibraryRowInfo> rows)
        {
            SettingsData settings = _context.Settings;

            if (settings.CustomMods == null)
            {
                return;
            }

            foreach (CustomItemData custom in settings.CustomMods)
            {
                if (custom == null)
                {
                    continue;
                }

                rows.Add(new LibraryRowInfo
                {
                    Key = "custom|" + custom.Id,
                    Id = custom.Id,
                    Platform = AdapterMap.PlatformShort(custom.Platform),
                    CategoryId = custom.Category,
                    Category = _context.Catalog.CategoryTitle(custom.Category),
                    Name = custom.Name,
                    ModType = custom.ModType,
                    Version = custom.Version,
                    Status = StatusOf(custom.Platform, custom.Category, custom.Id, true),
                    SizeBytes = -1,
                    Tag = custom
                });
            }
        }

        private void AddMods(List<LibraryRowInfo> rows, LibraryKind kind, ModsData library, CategoryType type)
        {
            if (library == null || library.Library == null)
            {
                return;
            }

            foreach (ModItemData mod in library.Library)
            {
                if (mod == null || _context.Catalog.CategoryTypeOf(mod.CategoryId) != type)
                {
                    continue;
                }

                Platform platform = mod.GetPlatform();

                rows.Add(new LibraryRowInfo
                {
                    Key = kind + "|" + AdapterMap.PlatformShort(platform) + "|" + mod.Id,
                    Id = mod.Id,
                    Platform = AdapterMap.PlatformShort(platform),
                    CategoryId = mod.CategoryId,
                    Category = _context.Catalog.CategoryTitle(mod.CategoryId),
                    Name = mod.Name,
                    SystemType = mod.FirmwareType,
                    ModType = mod.ModType,
                    Region = mod.Region,
                    Version = string.Join(" & ", mod.Versions),
                    Creator = mod.CreatedBy,
                    Status = StatusOf(platform, mod.CategoryId, mod.Id, false),
                    SizeBytes = -1,
                    IsFavorite = IsFavorite(_context.Settings.FavoriteMods, platform, mod.CategoryId, mod.Id),
                    Tag = mod
                });
            }
        }

        // PS4: one list holds homebrew and games; the category type tells them apart.
        private void AddApps(List<LibraryRowInfo> rows, LibraryKind kind, bool games)
        {
            AppsData library = _context.Catalog.AppsPs4;

            if (library == null || library.Library == null)
            {
                return;
            }

            foreach (AppItemData app in library.Library)
            {
                if (app == null)
                {
                    continue;
                }

                bool isGame = _context.Catalog.CategoryTypeOf(app.CategoryId) == CategoryType.Game;

                if (isGame != games)
                {
                    continue;
                }

                rows.Add(new LibraryRowInfo
                {
                    Key = kind + "|PS4|" + app.Id,
                    Id = app.Id,
                    Platform = "PS4",
                    CategoryId = app.CategoryId,
                    Category = _context.Catalog.CategoryTitle(app.CategoryId),
                    Name = app.Name,
                    Version = app.Version ?? string.Empty,
                    Firmware = app.PlayableVersion,
                    Creator = app.CreatedBy,
                    TitleId = app.TitleId,
                    Status = StatusOf(Platform.PS4, app.CategoryId, app.Id, false),
                    SizeBytes = -1,
                    IsFavorite = IsFavorite(_context.Settings.FavoriteMods, Platform.PS4, app.CategoryId, app.Id),
                    Tag = app
                });
            }
        }

        private void AddPackages(List<LibraryRowInfo> rows)
        {
            IList<PackageItemData> packages = _context.Catalog.PackagesPs3;

            if (packages == null)
            {
                return;
            }

            SettingsData settings = _context.Settings;

            foreach (PackageItemData package in packages)
            {
                if (package == null)
                {
                    continue;
                }

                string status = LibraryStatus.NotInstalled;

                if (IsPackageInstalled(settings, package))
                {
                    status = LibraryStatus.Installed;
                }
                else if (IsUrlDownloaded(settings, package.Url))
                {
                    status = LibraryStatus.Downloaded;
                }

                long size;
                DateTime modified;

                rows.Add(new LibraryRowInfo
                {
                    Key = package.Url ?? ("package|" + package.Id),
                    Id = package.Id,
                    Platform = "PS3",
                    Category = package.Category,
                    Name = package.Name,
                    Region = package.Region,
                    TitleId = package.TitleId,
                    Url = package.Url,
                    Status = status,
                    // The database holds both as text. Anything that is not a plain number or date counts as missing.
                    SizeBytes = long.TryParse(package.Size, NumberStyles.Integer, CultureInfo.InvariantCulture, out size) ? size : -1,
                    Modified = DateTime.TryParse(package.ModifiedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out modified)
                        ? (DateTime?)modified
                        : null,
                    Tag = package
                });
            }
        }

        private void AddGameSaves(List<LibraryRowInfo> rows, Platform platform)
        {
            IList<GameSaveItemData> saves = _context.Catalog.GameSaves;

            if (saves == null)
            {
                return;
            }

            SettingsData settings = _context.Settings;

            foreach (GameSaveItemData save in saves)
            {
                if (save == null || save.GetPlatform() != platform)
                {
                    continue;
                }

                bool downloaded = settings.DownloadedMods != null
                    && settings.DownloadedMods.Exists(delegate (DownloadedItem x)
                    {
                        return x != null && x.Platform == platform && x.ModId == save.Id && AdapterMap.Same(x.CategoryId, save.CategoryId);
                    });

                rows.Add(new LibraryRowInfo
                {
                    Key = "save|" + AdapterMap.PlatformShort(platform) + "|" + save.Id,
                    Id = save.Id,
                    Platform = AdapterMap.PlatformShort(platform),
                    CategoryId = save.CategoryId,
                    Category = _context.Catalog.CategoryTitle(save.CategoryId),
                    Name = save.Name,
                    Region = save.Region,
                    Version = save.Version,
                    Creator = save.CreatedBy,
                    Modified = save.LastUpdated == default(DateTime) ? (DateTime?)null : save.LastUpdated,
                    Status = downloaded ? LibraryStatus.Downloaded : LibraryStatus.NotInstalled,
                    SizeBytes = -1,
                    IsFavorite = IsFavorite(settings.FavoriteGameSaves, platform, save.CategoryId, save.Id),
                    Tag = save
                });
            }
        }

        private void AddTrainers(List<LibraryRowInfo> rows)
        {
            IList<TrainerGameItem> games = _context.Catalog.TrainersXbox;

            if (games == null)
            {
                return;
            }

            Func<string, string> titleOf = _context.Catalog.TitleIdToTitle;

            foreach (TrainerGameItem game in games)
            {
                if (game == null)
                {
                    continue;
                }

                string title = titleOf != null ? titleOf(game.TitleId) : null;

                // The page says: trainers store the game title in Name.
                rows.Add(new LibraryRowInfo
                {
                    Key = "trainer|" + game.TitleId,
                    Platform = "XBOX360",
                    Name = string.IsNullOrEmpty(title) ? game.TitleId : title,
                    TitleId = game.TitleId,
                    Count = game.Trainers != null ? game.Trainers.Count : 0,
                    DashType = DashTypes(game),
                    SizeBytes = -1,
                    Tag = game
                });
            }
        }

        private static string DashTypes(TrainerGameItem game)
        {
            if (game.Trainers == null)
            {
                return string.Empty;
            }

            List<string> types = new List<string>();

            foreach (TrainerItem trainer in game.Trainers)
            {
                if (trainer != null && !string.IsNullOrEmpty(trainer.Type) && !types.Contains(trainer.Type))
                {
                    types.Add(trainer.Type);
                }
            }

            return string.Join("/", types);
        }

        private void AddGameCheats(List<LibraryRowInfo> rows)
        {
            IList<GameCheatEntry> cheats = _context.Catalog.GameCheats;

            if (cheats == null)
            {
                return;
            }

            foreach (GameCheatEntry cheat in cheats)
            {
                if (cheat == null)
                {
                    continue;
                }

                rows.Add(new LibraryRowInfo
                {
                    Key = cheat.FileName ?? (cheat.Game + "|" + cheat.Region + "|" + cheat.Version),
                    Platform = "PS3",
                    Name = cheat.Game,
                    Region = cheat.Region,
                    Version = cheat.Version,
                    SizeBytes = -1,
                    Tag = cheat.Tag
                });
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Status and favourites (null-safe: the settings come from a JSON file)
        // ------------------------------------------------------------------------------------------------------

        private string StatusOf(Platform platform, string categoryId, int modId, bool isCustom)
        {
            SettingsData settings = _context.Settings;
            ConsoleProfile profile = _context.Profile;

            bool installed = false;

            if (profile != null && profile.InstalledMods != null)
            {
                installed = profile.InstalledMods.Exists(delegate (InstalledModInfo x)
                {
                    return x != null && x.ModId == modId && x.IsCustom == isCustom && AdapterMap.Same(x.CategoryId, categoryId);
                });
            }

            bool downloaded = settings.DownloadedMods != null
                && settings.DownloadedMods.Exists(delegate (DownloadedItem x)
                {
                    return x != null && x.Platform == platform && x.ModId == modId;
                });

            if (installed)
            {
                return LibraryStatus.Installed;
            }

            return downloaded ? LibraryStatus.Downloaded : LibraryStatus.NotInstalled;
        }

        private static bool IsFavorite(List<FavoriteItem> list, Platform platform, string categoryId, int modId)
        {
            return list != null && list.Exists(delegate (FavoriteItem x)
            {
                return x != null && x.Platform == platform && x.ModId == modId && AdapterMap.Same(x.CategoryId, categoryId);
            });
        }

        private static bool IsPackageInstalled(SettingsData settings, PackageItemData package)
        {
            return settings.InstalledPackages != null && settings.InstalledPackages.Exists(delegate (InstalledPackageInfo x)
            {
                return x != null && string.Equals(x.Category, package.Category, StringComparison.Ordinal)
                    && string.Equals(x.Url, package.Url, StringComparison.Ordinal);
            });
        }

        private static bool IsUrlDownloaded(SettingsData settings, string url)
        {
            return !string.IsNullOrEmpty(url) && settings.DownloadedMods != null && settings.DownloadedMods.Exists(delegate (DownloadedItem x)
            {
                return x != null && string.Equals(x.Url, url, StringComparison.Ordinal);
            });
        }

        private static Platform ParsePlatform(string text)
        {
            if (string.Equals(text, "PS4", StringComparison.OrdinalIgnoreCase))
            {
                return Platform.PS4;
            }

            if (string.Equals(text, "XBOX360", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "XBOX", StringComparison.OrdinalIgnoreCase))
            {
                return Platform.XBOX360;
            }

            return Platform.PS3;
        }
    }
}
