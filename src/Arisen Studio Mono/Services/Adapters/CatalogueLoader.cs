using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArisenStudio.Constants;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.GameData.XBOX;
using ArisenStudio.Runtime;
using Newtonsoft.Json;

namespace ArisenStudio.Services.Adapters
{
    // Loads the online database the way the old DatabaseClient.InitializeAsync did (Database/Client/DatabaseClient.cs:315-367):
    // the same Urls constants, the same models, the same Categories ordering by Title. Each source is fetched and parsed on
    // its own, and a failure is recorded and logged without stopping the other sources.
    //
    // Not covered here: the old local file cache and ETag handling (SimpleCache, GetFromCacheOrFetchAsync). A failed
    // download leaves that source empty instead of falling back to a cached copy.
    //
    // No constructor work. The network is used only inside LoadAsync, which the caller must await. Smoke mode never calls it.

    /// <summary>One source that could not be downloaded or parsed.</summary>
    public sealed class CatalogueSourceError
    {
        public CatalogueSourceError(string source, string url, string message)
        {
            Source = source;
            Url = url;
            Message = message;
        }

        /// <summary>Short name of the file, for example "categories" or "PS3 game mods".</summary>
        public string Source { get; }

        /// <summary>The URL that was requested.</summary>
        public string Url { get; }

        /// <summary>The exception message.</summary>
        public string Message { get; }
    }

    /// <summary>The result of <see cref="CatalogueLoader.LoadAsync" />. The catalog is never null; sources that failed are left empty.</summary>
    public sealed class CatalogueLoadResult
    {
        internal CatalogueLoadResult(DatabaseCatalog catalog, IList<CatalogueSourceError> errors)
        {
            Catalog = catalog ?? new DatabaseCatalog();
            Errors = new List<CatalogueSourceError>(errors ?? new List<CatalogueSourceError>()).AsReadOnly();
        }

        /// <summary>The loaded catalogue. Set it on <c>AdapterContext.Catalog</c>.</summary>
        public DatabaseCatalog Catalog { get; }

        /// <summary>Sources that failed. Empty when every source loaded.</summary>
        public IReadOnlyList<CatalogueSourceError> Errors { get; }

        /// <summary>True when the categories file loaded. The old app treated its absence as a fatal load error (DatabaseClient.cs:363).</summary>
        public bool HasCategories
        {
            get { return Catalog.Categories != null && Catalog.Categories.Categories != null; }
        }
    }

    /// <summary>Downloads and parses the online database catalogue.</summary>
    public sealed class CatalogueLoader
    {
        // Same user agent and timeout as the old DatabaseClient constructor (DatabaseClient.cs:178-180).
        private const string UserAgent = "ArisenStudio-Database";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Downloads and parses every catalogue file. Never throws for a download or parse failure; those are returned in
        /// <see cref="CatalogueLoadResult.Errors" />. Throws only when <paramref name="cancellationToken" /> is cancelled.
        /// </summary>
        public async Task<CatalogueLoadResult> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            List<CatalogueSourceError> errors = new List<CatalogueSourceError>();
            DatabaseCatalog catalog = new DatabaseCatalog();

            using (HttpClient client = new HttpClient { Timeout = RequestTimeout })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

                // Categories (DatabaseClient.cs:409-414, Urls.cs:87). Sorted by Title as in DatabaseClient.cs:356-359.
                CategoriesData categories = await FetchAsync<CategoriesData>(client, "categories", Urls.CategoriesData, errors, cancellationToken);
                if (categories != null)
                {
                    if (categories.Categories != null)
                    {
                        categories.Categories = categories.Categories.OrderBy(o => o.Title).ToList();
                    }
                    catalog.Categories = categories;
                }

                // PS3 mod libraries (DatabaseClient.cs:422-457, Urls.cs:107, 112, 117).
                catalog.GameModsPs3 = await FetchAsync<ModsData>(client, "PS3 game mods", Urls.GameModsDataPS3, errors, cancellationToken);
                catalog.HomebrewPs3 = await FetchAsync<ModsData>(client, "PS3 homebrew", Urls.HomebrewDataPS3, errors, cancellationToken);
                catalog.ResourcesPs3 = await FetchAsync<ModsData>(client, "PS3 resources", Urls.ResourcesDataPS3, errors, cancellationToken);

                // Xbox 360 mod libraries (DatabaseClient.cs:596-620, Urls.cs:172, 177).
                catalog.GameModsXbox = await FetchAsync<ModsData>(client, "Xbox 360 game mods", Urls.GameModsDataXbox, errors, cancellationToken);
                catalog.HomebrewXbox = await FetchAsync<ModsData>(client, "Xbox 360 homebrew", Urls.HomebrewDataXbox, errors, cancellationToken);

                // PS4 homebrew (DatabaseClient.cs:583-592, Urls.cs:167).
                catalog.AppsPs4 = await FetchAsync<AppsData>(client, "PS4 homebrew", Urls.HomebrewDataPS4, errors, cancellationToken);

                // PS3 packages, flattened in the old order: games, demos, DLCs, avatars, themes (DatabaseClient.cs:341-347 order;
                // ids from the static pkgId counter at DatabaseClient.cs:473 and onward).
                catalog.PackagesPs3 = await FetchPackagesAsync(client, errors, cancellationToken);

                // Game saves (DatabaseClient.cs:635-641, Urls.cs:102).
                GameSavesData gameSaves = await FetchAsync<GameSavesData>(client, "game saves", Urls.GameSavesData, errors, cancellationToken);
                catalog.GameSaves = gameSaves != null && gameSaves.GameSaves != null ? gameSaves.GameSaves : new List<GameSaveItemData>();

                // Xbox 360 trainers (DatabaseClient.cs:622-632, Urls.cs:182).
                TrainersData trainers = await FetchAsync<TrainersData>(client, "Xbox 360 trainers", Urls.TrainersDataXbox, errors, cancellationToken);
                catalog.TrainersXbox = trainers != null && trainers.Library != null ? trainers.Library : new List<TrainerGameItem>();

                // Xbox 360 title ids (DatabaseClient.cs:661-665, Urls.cs:197). Used for the trainer headers.
                GameTitleIds titleIds = await FetchAsync<GameTitleIds>(client, "Xbox 360 title ids", Urls.GameTitleIdsXbox, errors, cancellationToken);
                catalog.TitleIdToTitle = BuildTitleLookup(titleIds);
            }

            return new CatalogueLoadResult(catalog, errors);
        }

        /// <summary>Fetches the five PS3 package lists in order and gives each package a running id, as the old code did.</summary>
        private static async Task<IList<PackageItemData>> FetchPackagesAsync(HttpClient client, IList<CatalogueSourceError> errors, CancellationToken cancellationToken)
        {
            KeyValuePair<string, string>[] sources =
            {
                new KeyValuePair<string, string>("PS3 games (packages)", Urls.PackagesGamesDataPS3),
                new KeyValuePair<string, string>("PS3 demos (packages)", Urls.PackagesDemosDataPS3),
                new KeyValuePair<string, string>("PS3 DLCs (packages)", Urls.PackagesDLCsDataPS3),
                new KeyValuePair<string, string>("PS3 avatars (packages)", Urls.PackagesAvatarsDataPS3),
                new KeyValuePair<string, string>("PS3 themes (packages)", Urls.PackagesThemesDataPS3),
            };

            List<PackageItemData> packages = new List<PackageItemData>();
            int nextId = 0;

            foreach (KeyValuePair<string, string> source in sources)
            {
                PackagesData data = await FetchAsync<PackagesData>(client, source.Key, source.Value, errors, cancellationToken);
                if (data == null || data.Packages == null)
                {
                    continue;
                }

                foreach (PackageItemData package in data.Packages)
                {
                    if (package == null)
                    {
                        continue;
                    }

                    package.Id = nextId++;
                    packages.Add(package);
                }
            }

            return packages;
        }

        /// <summary>Title id to title lookup, case-insensitive, first entry wins (as GameTitleIds.GetTitleFromTitleId does). Null for an unknown id.</summary>
        private static Func<string, string> BuildTitleLookup(GameTitleIds titleIds)
        {
            Dictionary<string, string> titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (titleIds != null && titleIds.Games != null)
            {
                foreach (GameItemData game in titleIds.Games)
                {
                    if (game == null || string.IsNullOrEmpty(game.TitleId))
                    {
                        continue;
                    }

                    titles.TryAdd(game.TitleId, game.GameTitle);
                }
            }

            return delegate (string titleId)
            {
                string title;
                return titleId != null && titles.TryGetValue(titleId, out title) ? title : null;
            };
        }

        /// <summary>
        /// Downloads one file and parses it. Returns null on any failure, after adding it to <paramref name="errors" /> and
        /// logging it through <see cref="AppBootstrapper.LogError" />. A cancellation that was requested is rethrown.
        /// </summary>
        private static async Task<T> FetchAsync<T>(HttpClient client, string source, string url, IList<CatalogueSourceError> errors, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                string json = await client.GetStringAsync(url, cancellationToken);
                T value = JsonConvert.DeserializeObject<T>(json);

                if (value == null)
                {
                    throw new InvalidDataException("The file is empty or is not a " + typeof(T).Name + ".");
                }

                return value;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                errors.Add(new CatalogueSourceError(source, url, ex.Message));
                AppBootstrapper.LogError(ex, "Unable to load the " + source + " catalogue from " + url + ".");
                return null;
            }
        }
    }
}
