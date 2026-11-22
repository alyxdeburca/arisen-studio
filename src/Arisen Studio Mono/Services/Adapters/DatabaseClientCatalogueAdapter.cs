using System;
using System.Collections.Generic;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.GameData.XBOX;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Copies the lists of an already-loaded <see cref="DatabaseClient"/> (after <see cref="DatabaseClient.InitializeAsync"/>)
    /// into a <see cref="DatabaseCatalog"/> for the adapters.
    /// This is a pure mapping: it has no constructor, does no I/O and makes no network call.
    /// A null client gives an empty catalogue. A null source gives a null member, which DatabaseCatalog allows.
    /// </summary>
    public static class DatabaseClientCatalogueAdapter
    {
        /// <summary>Maps a loaded client to a new catalogue. The lists are shared with the client, not copied, except where noted.</summary>
        public static DatabaseCatalog ToCatalog(DatabaseClient client)
        {
            DatabaseCatalog catalog = new DatabaseCatalog();

            if (client == null)
            {
                return catalog;
            }

            catalog.Categories = client.CategoriesData;
            catalog.GameModsPs3 = client.GameModsPS3;
            catalog.HomebrewPs3 = client.HomebrewPS3;
            catalog.ResourcesPs3 = client.ResourcesPS3;
            catalog.GameModsXbox = client.GameModsX360;
            catalog.HomebrewXbox = client.HomebrewX360;
            catalog.AppsPs4 = client.HomebrewPS4;
            catalog.PackagesPs3 = MapPackages(client);
            catalog.GameSaves = client.GameSaves != null ? client.GameSaves.GameSaves : null;
            catalog.TrainersXbox = client.TrainersX360 != null ? client.TrainersX360.Library : null;
            catalog.GameCheats = MapGameCheats(client.GamesDataPS3);
            catalog.TitleIdToTitle = MapTitleLookup(client.TitleIdsX360);

            return catalog;
        }

        /// <summary>Joins the five PS3 package lists into one new list. Null when none of them is loaded.</summary>
        private static IList<PackageItemData> MapPackages(DatabaseClient client)
        {
            PackagesData[] sources = { client.GamesPS3, client.DemosPS3, client.DLCsPS3, client.AvatarsPS3, client.ThemesPS3 };
            List<PackageItemData> packages = null;

            foreach (PackagesData source in sources)
            {
                if (source == null || source.Packages == null)
                {
                    continue;
                }

                if (packages == null)
                {
                    packages = new List<PackageItemData>();
                }

                foreach (PackageItemData package in source.Packages)
                {
                    if (package != null)
                    {
                        packages.Add(package);
                    }
                }
            }

            return packages;
        }

        /// <summary>One cheat entry per PS3 game row. FileName is the game's cheat file; Tag keeps the row.</summary>
        private static IList<GameCheatEntry> MapGameCheats(GamesDataPS3 source)
        {
            if (source == null || source.Games == null)
            {
                return null;
            }

            List<GameCheatEntry> cheats = new List<GameCheatEntry>();

            foreach (var game in source.Games)
            {
                if (game == null)
                {
                    continue;
                }

                cheats.Add(new GameCheatEntry
                {
                    Game = game.Game,
                    Region = game.Region,
                    Version = game.Version,
                    FileName = game.File,
                    Tag = game
                });
            }

            return cheats;
        }

        /// <summary>
        /// Title lookup for Xbox 360 title ids. Built once from the loaded list, so it does not throw on a miss.
        /// Unknown ids give null, which the catalogue documents as "show the title id".
        /// </summary>
        private static Func<string, string> MapTitleLookup(GameTitleIds source)
        {
            if (source == null || source.Games == null)
            {
                return null;
            }

            Dictionary<string, string> titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var game in source.Games)
            {
                if (game == null || string.IsNullOrEmpty(game.TitleId) || game.GameTitle == null)
                {
                    continue;
                }

                if (!titles.ContainsKey(game.TitleId))
                {
                    titles.Add(game.TitleId, game.GameTitle);
                }
            }

            return titleId =>
            {
                if (string.IsNullOrEmpty(titleId))
                {
                    return null;
                }

                string title;
                return titles.TryGetValue(titleId, out title) ? title : null;
            };
        }
    }
}
