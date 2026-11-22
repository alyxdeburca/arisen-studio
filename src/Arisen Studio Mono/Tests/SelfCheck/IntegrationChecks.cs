#if SELFCHECK
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ArisenStudio.Database;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Forms.Pages.Library;
using ArisenStudio.Io;
using ArisenStudio.Localization;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Services.Adapters;
using ArisenStudio.Services.Transfers;
using ArisenStudio.Shell.Wiring;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Cross-area integration checks, all deterministic and offline. Not compiled or run when written.
    /// (a) Local paths contain no backslash on Linux: IoExtensions, TransferLocalPaths, SettingsData, TrainerGameItem,
    ///     GameSaveItemData. On Windows the separator is a backslash, so these checks pass without testing anything there.
    /// (b) The Services/Adapters data sources and the screenshot uploader build and answer offline queries; a counting
    ///     HttpMessageHandler proves the uploader sends nothing, and the feed loaders prove nothing is fetched.
    /// (c) DialogContext, ShellServices, ShellController construct; DialogLauncher is a static class, so its null guards are
    ///     called instead (they return false before any dialog is created).
    /// (d) AliasedText and MessageTextSanitizer give plain text for ERROR_OCCURRED and CONNECTING_TO_CONSOLE.
    /// Nothing here writes a file (file system calls are Exists and path-string building only), shows a dialog, opens a
    /// socket, or leaves the ResourceText dictionary changed.
    /// Console-side paths with a backslash (SettingsData.ConsolePathXbox, LaunchIniFilePath, AuroraFolderPath,
    /// XboxConsoleFileServiceUnavailable.PathSeparator) are meant to have one and are not tested as local paths.
    /// Registration: add <c>IntegrationChecks.AddTo(cases);</c> to SelfCheckRunner.Run after TransferDialogChecks.AddTo, and
    /// add <c>Tests\SelfCheck\IntegrationChecks.cs</c> to the csproj.
    /// </summary>
    internal static class IntegrationChecks
    {
        private const string LocalizedAnchorKey = "ERROR_OCCURRED";

        public static void AddTo(List<SelfCheckCase> cases)
        {
            // (a) local paths
            cases.Add(new SelfCheckCase("Integration.Paths.IoExtensions", PathsIoExtensions));
            cases.Add(new SelfCheckCase("Integration.Paths.TransferLocalPaths", PathsTransferLocalPaths));
            cases.Add(new SelfCheckCase("Integration.Paths.SettingsData", PathsSettingsData));
            cases.Add(new SelfCheckCase("Integration.Paths.SettingsData.PathTemp", PathsSettingsDataPathTemp));
            cases.Add(new SelfCheckCase("Integration.Paths.TrainerGameItem", PathsTrainerGameItem));
            cases.Add(new SelfCheckCase("Integration.Paths.GameSaveItemData", PathsGameSaveItemData));

            // (b) adapters, offline
            cases.Add(new SelfCheckCase("Integration.Adapters.AdapterContext.Defaults", AdapterContextDefaults));
            cases.Add(new SelfCheckCase("Integration.Adapters.DownloadsDataSource", AdapterDownloads));
            cases.Add(new SelfCheckCase("Integration.Adapters.InstalledFilesDataSource", AdapterInstalledFiles));
            cases.Add(new SelfCheckCase("Integration.Adapters.LibraryDataSource.EmptyCatalog", AdapterLibraryEmpty));
            cases.Add(new SelfCheckCase("Integration.Adapters.LibraryDataSource.CustomMods", AdapterLibraryCustomMods));
            cases.Add(new SelfCheckCase("Integration.Adapters.DashboardDataSource.Offline", AdapterDashboardOffline));
            cases.Add(new SelfCheckCase("Integration.Adapters.DashboardDataSource.Announcements", AdapterDashboardAnnouncements));
            cases.Add(new SelfCheckCase("Integration.Adapters.HttpScreenshotUploader.NoNetwork", AdapterUploaderNoNetwork));
            cases.Add(new SelfCheckCase("Integration.Adapters.XboxConsoleFileServiceUnavailable", AdapterXboxFileService));
            cases.Add(new SelfCheckCase("Integration.Adapters.Ps3TransferHooks.Defaults", AdapterTransferHooksDefaults));

            // (c) wiring
            cases.Add(new SelfCheckCase("Integration.Wiring.DialogContext.Construct", WiringDialogContext));
            cases.Add(new SelfCheckCase("Integration.Wiring.ShellServices.Construct", WiringShellServices));
            cases.Add(new SelfCheckCase("Integration.Wiring.ShellController.Construct", WiringShellController));
            cases.Add(new SelfCheckCase("Integration.Wiring.DialogLauncher.NullGuards", WiringDialogLauncher));

            // (d) text
            cases.Add(new SelfCheckCase("Integration.Text.ErrorOccurred.Fallback", TextErrorFallback));
            cases.Add(new SelfCheckCase("Integration.Text.ErrorOccurred.Anchor", TextErrorAnchor));
            cases.Add(new SelfCheckCase("Integration.Text.ConnectingToConsole", TextConnecting));
            cases.Add(new SelfCheckCase("Integration.Text.AliasedText.Plain", TextAliased));
        }

        // ====================================================================================================
        // (a) local paths
        // ====================================================================================================

        // True on Linux and macOS. On Windows a backslash is the separator, so the checks have nothing to say.
        private static bool BackslashIsNotASeparator()
        {
            return Path.DirectorySeparatorChar != '\\';
        }

        private static void NoBackslash(string path, string what)
        {
            SelfCheckAssert.NotNull(path, what);
            SelfCheckAssert.True(path.IndexOf('\\') < 0, what + " has a backslash: " + path);
        }

        private static void PathsIoExtensions()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            NoBackslash(UserFolders.AppData, "UserFolders.AppData");
            NoBackslash(UserFolders.SettingsData, "UserFolders.SettingsData");
            NoBackslash(UserFolders.BackupFilesData, "UserFolders.BackupFilesData");
            NoBackslash(UserFolders.BackupFiles, "UserFolders.BackupFiles");
            NoBackslash(UserFolders.XboxProfiles, "UserFolders.XboxProfiles");
            NoBackslash(UserFolders.Logs, "UserFolders.Logs");

            // The template is written with forward slashes, as a Linux caller would write it.
            string full = "%APPDATA%/Downloads/%PLATFORM%/%CATEGORY%/".GetFullPath("PS3", "Games");
            NoBackslash(full, "IoExtensions.GetFullPath");
            SelfCheckAssert.True(full.StartsWith(UserFolders.AppData, StringComparison.Ordinal), "GetFullPath should start with UserFolders.AppData: " + full);
            SelfCheckAssert.True(full.EndsWith("Downloads/PS3/Games/", StringComparison.Ordinal), "GetFullPath tail: " + full);
            SelfCheckAssert.True(full.IndexOf('%') < 0, "GetFullPath left a placeholder: " + full);

            // A name nothing exists under: GetNextFileName must hand it back unchanged and create nothing.
            string unused = Path.Combine(Path.GetTempPath(), "arisen-selfcheck-" + Guid.NewGuid().ToString("N"), "file.txt");
            SelfCheckAssert.Equal(unused, IoExtensions.GetNextFileName(unused), "GetNextFileName of a missing file");
            SelfCheckAssert.True(!File.Exists(unused), "GetNextFileName must not create the file");

            // DeleteDirectory of a missing folder returns without touching anything.
            IoExtensions.DeleteDirectory(Path.Combine(Path.GetTempPath(), "arisen-selfcheck-" + Guid.NewGuid().ToString("N")));
        }

        private static void PathsTransferLocalPaths()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            // The old Windows text for the kind folder is still accepted and must come out with the OS separator.
            string usb = TransferLocalPaths.UsbInstallFolder("/media/usb", "PS4\\Homebrew", "Sample", "1.00");
            NoBackslash(usb, "UsbInstallFolder");
            SelfCheckAssert.Equal("/media/usb/PS4/Homebrew/Sample/1.00/", usb, "UsbInstallFolder");

            NoBackslash(TransferLocalPaths.UsbInstallFolder("/media/usb", "Game Saves", "BLUS30001", "Save"), "UsbInstallFolder (Game Saves)");

            string install = TransferLocalPaths.PackageInstallDownloadFolder("/tmp/dl", "Sample Game");
            NoBackslash(install, "PackageInstallDownloadFolder");
            SelfCheckAssert.Equal("/tmp/dl/Sample Game/", install, "PackageInstallDownloadFolder");

            string download = TransferLocalPaths.PackageDownloadFolder("/tmp/dl", "Sample Game", "USA");
            NoBackslash(download, "PackageDownloadFolder");
            SelfCheckAssert.Equal("/tmp/dl/Packages/Sample Game (USA)", download, "PackageDownloadFolder");

            string file = TransferLocalPaths.FileInFolder(install, "Sample Game.pkg");
            NoBackslash(file, "FileInFolder");
            SelfCheckAssert.Equal("/tmp/dl/Sample Game/Sample Game.pkg", file, "FileInFolder (no doubled separator)");

            string target = TransferLocalPaths.CopyTarget("/media/usb/Mods/", "/home/user/files/patch.rap");
            NoBackslash(target, "CopyTarget");
            SelfCheckAssert.Equal("/media/usb/Mods/patch.rap", target, "CopyTarget");
        }

        // Local path settings of a fresh SettingsData. PathTemp has its own check below, because it is the one that is known
        // to hold a literal backslash.
        private static void PathsSettingsData()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            SettingsData settings = new SettingsData();
            NoBackslash(settings.PathAppData, "SettingsData.PathAppData");
            NoBackslash(settings.PathDownloads, "SettingsData.PathDownloads");
            NoBackslash(settings.LocalPathPS3, "SettingsData.LocalPathPS3");
            NoBackslash(settings.LocalPathPS4, "SettingsData.LocalPathPS4");
            NoBackslash(settings.LocalPathXbox, "SettingsData.LocalPathXbox");
            NoBackslash(settings.PathDownloads.GetFullPath(settings.PathAppData), "SettingsData.PathDownloads expanded");
        }

        private static void PathsSettingsDataPathTemp()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            SettingsData settings = new SettingsData();
            NoBackslash(settings.PathTemp, "SettingsData.PathTemp default");
            NoBackslash(settings.PathTemp.GetFullPath(settings.PathAppData), "SettingsData.PathTemp expanded");
        }

        private static TrainerGameItem SampleTrainerGame()
        {
            return new TrainerGameItem { TitleId = "4D5307E6", Description = "Sample" };
        }

        private static void PathsTrainerGameItem()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            TrainerGameItem game = SampleTrainerGame();
            TrainerItem trainer = new TrainerItem { Name = "Sample Trainer", Type = "aurora", Url = "https://example.invalid/t.zip" };

            // Both read AppHost.Settings.PathDownloads, which is the default unless an earlier check replaced the settings.
            NoBackslash(game.DownloadDataDirectory(trainer), "TrainerGameItem.DownloadDataDirectory");
            NoBackslash(game.ArchiveZipFile(trainer), "TrainerGameItem.ArchiveZipFile");
        }

        private static void PathsGameSaveItemData()
        {
            if (!BackslashIsNotASeparator())
            {
                return;
            }

            GameSaveItemData save = new GameSaveItemData
            {
                Platform = "PS3",
                Id = 1,
                CategoryId = "BLUS30001",
                Name = "Sample Save",
                CreatedBy = "Sample Author",
                Version = "1.00"
            };
            DownloadFiles files = new DownloadFiles { Name = "save.zip", Region = "USA", Version = "1.00" };
            CategoryItem category = new CategoryItem { Id = "BLUS30001", Title = "Sample Game", Type = "game" };

            NoBackslash(save.DownloadDataDirectory(files, category), "GameSaveItemData.DownloadDataDirectory");
            NoBackslash(save.ArchiveZipFile(files, category), "GameSaveItemData.ArchiveZipFile");
        }

        // ====================================================================================================
        // (b) adapters
        // ====================================================================================================

        // A context over a private SettingsData, so no check reads or changes AppHost.Settings or any file.
        // saves counts the SaveSettings calls; nothing is written.
        private static AdapterContext Context(SettingsData settings, int[] saves)
        {
            return new AdapterContext
            {
                SettingsProvider = delegate () { return settings; },
                SaveSettings = delegate () { if (saves != null) { saves[0]++; } }
            };
        }

        private static void AdapterContextDefaults()
        {
            AdapterContext context = new AdapterContext();
            SelfCheckAssert.NotNull(context.Catalog, "Catalog default");
            SelfCheckAssert.NotNull(context.Settings, "Settings falls back to AppHost.Settings");
            SelfCheckAssert.True(context.Profile == null, "Profile default should be null");
            context.Save();
            context.SaveBackups();

            context.Catalog = null;
            SelfCheckAssert.NotNull(context.Catalog, "Catalog set to null must give an empty catalog");
            SelfCheckAssert.True(context.Catalog.FindCategory("x") == null, "FindCategory on an empty catalog");
            SelfCheckAssert.Equal("BLUS30001", context.Catalog.CategoryTitle("BLUS30001"), "CategoryTitle falls back to the id");
            SelfCheckAssert.Equal(CategoryType.Game, context.Catalog.CategoryTypeOf("BLUS30001"), "CategoryTypeOf default");
            SelfCheckAssert.True(context.Catalog.FindMod(Platform.PS3, 1, null) == null, "FindMod on an empty catalog");
            SelfCheckAssert.True(context.Catalog.FindApp(1, null) == null, "FindApp on an empty catalog");

            bool threw = false;
            try
            {
                new DownloadsDataSource(null);
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }
            SelfCheckAssert.True(threw, "DownloadsDataSource(null) should throw ArgumentNullException");
        }

        private static void AdapterDownloads()
        {
            SettingsData settings = new SettingsData();
            DownloadsDataSource source = new DownloadsDataSource(Context(settings, null));

            SelfCheckAssert.Equal(0, source.GetDownloads().Count, "no downloads");

            DownloadedItem item = new DownloadedItem
            {
                Platform = Platform.PS3,
                ModId = 5,
                CategoryId = "BLUS30001",
                FilePath = "/nonexistent-arisen-selfcheck/sample.zip",
                Url = "https://example.invalid/sample.zip",
                DateTime = new DateTime(2024, 1, 1),
                DownloadFile = new DownloadFiles { Name = "sample.zip", Region = "USA", Version = "1.00" }
            };
            settings.DownloadedMods.Add(item);
            settings.DownloadedMods.Add(null);

            IList<DownloadRowInfo> rows = source.GetDownloads();
            SelfCheckAssert.Equal(1, rows.Count, "one row (the null record is skipped)");
            DownloadRowInfo row = rows[0];
            SelfCheckAssert.Equal("sample.zip (file missing)", row.FileName, "file name of a record whose file is gone");
            SelfCheckAssert.Equal("PlayStation 3", row.PlatformText, "platform text");
            SelfCheckAssert.Equal("USA", row.Region, "region");
            SelfCheckAssert.Equal("1.00", row.Version, "version");
            SelfCheckAssert.Equal(5, row.ModId, "mod id");
            SelfCheckAssert.True(object.ReferenceEquals(row.Tag, item), "Tag should be the record");
        }

        private static void AdapterInstalledFiles()
        {
            SettingsData settings = new SettingsData();
            InstalledFilesDataSource source = new InstalledFilesDataSource(Context(settings, null));

            SelfCheckAssert.Equal(0, source.GetInstalled().Count, "no profiles, no rows");

            InstalledModInfo installed = new InstalledModInfo
            {
                Platform = Platform.PS3,
                CategoryId = "BLUS30001",
                ModId = 9,
                Name = "Sample Mod",
                TotalFiles = 2,
                DateInstalled = new DateTime(2024, 1, 1)
            };
            ConsoleProfile profile = new ConsoleProfile { Id = 1, Name = "Sample Console", Platform = Platform.PS3 };
            profile.InstalledMods.Add(installed);
            settings.ConsoleProfiles.Add(profile);
            settings.ConsoleProfiles.Add(null);

            IList<InstalledRowInfo> rows = source.GetInstalled();
            SelfCheckAssert.Equal(1, rows.Count, "one installed mod");
            SelfCheckAssert.Equal("Sample Mod", rows[0].Name, "name");
            SelfCheckAssert.Equal("Sample Console", rows[0].ProfileName, "profile name");
            SelfCheckAssert.Equal(1, rows[0].ProfileId, "profile id");
            SelfCheckAssert.Equal(2, rows[0].TotalFiles, "total files");
            SelfCheckAssert.True(object.ReferenceEquals(rows[0].Tag, installed), "Tag should be the installed record");

            // Forgetting edits the settings only. The row is gone afterwards and no console is involved.
            source.ForgetInstalled(rows[0]);
            SelfCheckAssert.Equal(0, source.GetInstalled().Count, "no rows after ForgetInstalled");
            source.ForgetInstalled(null);
        }

        private static void AdapterLibraryEmpty()
        {
            LibraryDataSource source = new LibraryDataSource(Context(new SettingsData(), null));

            foreach (LibraryKind kind in Enum.GetValues(typeof(LibraryKind)))
            {
                foreach (LibraryPlatform platform in Enum.GetValues(typeof(LibraryPlatform)))
                {
                    IList<LibraryRowInfo> rows = source.GetItems(kind, platform);
                    SelfCheckAssert.NotNull(rows, "GetItems(" + kind + ", " + platform + ")");
                    SelfCheckAssert.Equal(0, rows.Count, "GetItems(" + kind + ", " + platform + ") with an empty catalog");
                }
            }

            SelfCheckAssert.True(!source.AddFavorite(LibraryKind.GameMods, null), "AddFavorite(null) should return false");
            source.DeleteCustomMod(null);
        }

        private static void AdapterLibraryCustomMods()
        {
            SettingsData settings = new SettingsData();
            settings.CustomMods.Add(new CustomItemData
            {
                Id = 7,
                Platform = Platform.PS3,
                Category = "BLUS30001",
                Name = "Sample Custom",
                ModType = "Cheat",
                Version = "1.0"
            });
            int[] saves = new int[1];
            LibraryDataSource source = new LibraryDataSource(Context(settings, saves));

            IList<LibraryRowInfo> rows = source.GetItems(LibraryKind.CustomMods, LibraryPlatform.PS3);
            SelfCheckAssert.Equal(1, rows.Count, "one custom mod");
            SelfCheckAssert.Equal("Sample Custom", rows[0].Name, "name");
            SelfCheckAssert.Equal("PS3", rows[0].Platform, "platform text");
            SelfCheckAssert.Equal(LibraryStatus.NotInstalled, rows[0].Status, "status with no console profile");
            SelfCheckAssert.Equal(7, rows[0].Id, "id");

            source.DeleteCustomMod(rows[0]);
            SelfCheckAssert.Equal(0, settings.CustomMods.Count, "custom mod removed from the settings");
            SelfCheckAssert.Equal(1, saves[0], "the SaveSettings hook is called once (no file is written here)");
        }

        private static void AdapterDashboardOffline()
        {
            int[] loaderCalls = new int[1];
            DashboardFeeds feeds = new DashboardFeeds
            {
                FavoriteGames = new List<DashboardFavoriteGameEntry>
                {
                    new DashboardFavoriteGameEntry { Platform = DashboardPlatform.PS3, CategoryId = "BLUS30001", ImageUrl = "https://example.invalid/a.png" },
                    new DashboardFavoriteGameEntry { Platform = DashboardPlatform.PS4, CategoryId = "CUSA00001", ImageUrl = "https://example.invalid/b.png" },
                    null
                },
                FavoriteMods = new List<DashboardFavoriteModEntry>
                {
                    new DashboardFavoriteModEntry { Platform = DashboardPlatform.PS3, CategoryId = "BLUS30001", ModId = 3, ModName = "Sample Mod" }
                },
                TotalDownloads = delegate () { return 123; }
            };
            DashboardDataSource source = new DashboardDataSource(Context(new SettingsData(), null), feeds);

            SelfCheckAssert.Equal(2, source.GetFavoriteGames(null).Count, "favourite games, all platforms");
            IList<FavoriteGameInfo> ps4 = source.GetFavoriteGames(DashboardPlatform.PS4);
            SelfCheckAssert.Equal(1, ps4.Count, "favourite games, PS4 only");
            SelfCheckAssert.Equal("CUSA00001", ps4[0].CategoryTitle, "title falls back to the category id");
            SelfCheckAssert.Equal(0, ps4[0].TotalCount, "no items in an empty catalog");
            SelfCheckAssert.Equal(1, source.GetFavoriteMods(null).Count, "favourite mods");
            SelfCheckAssert.Equal(0, source.GetFavoriteMods(DashboardPlatform.Xbox360).Count, "favourite mods, Xbox 360");

            DashboardStats stats = source.GetStatistics();
            SelfCheckAssert.Equal(123L, stats.TotalDownloads, "total downloads from the feed delegate");
            SelfCheckAssert.True(!stats.LastUpdated.HasValue, "no LastUpdated without a loaded catalog");
            SelfCheckAssert.Equal(3, stats.Groups.Count, "statistics groups");
            SelfCheckAssert.Equal("PlayStation 3", stats.Groups[0].Title, "first group");
            SelfCheckAssert.Equal("PlayStation 4", stats.Groups[1].Title, "second group");
            SelfCheckAssert.Equal("Xbox 360", stats.Groups[2].Title, "third group");

            // News and releases have no loader: both must fail with NotSupportedException and not try the network.
            Task<IList<NewsEntry>> news = source.GetNewsAsync();
            SelfCheckAssert.True(news.IsCompleted && news.IsFaulted, "GetNewsAsync with no loader should be a faulted task");
            SelfCheckAssert.True(news.Exception.InnerException is NotSupportedException, "GetNewsAsync should fail with NotSupportedException");

            bool threw = false;
            try
            {
                source.GetReleases();
            }
            catch (NotSupportedException)
            {
                threw = true;
            }
            SelfCheckAssert.True(threw, "GetReleases with no provider should throw NotSupportedException");

            // A web address with no image loader gives no image and no request.
            Task<System.Drawing.Image> image = source.LoadImageAsync("https://example.invalid/cover.png");
            SelfCheckAssert.True(image.IsCompleted && !image.IsFaulted, "LoadImageAsync with no loader should complete at once");
            SelfCheckAssert.True(image.Result == null, "LoadImageAsync with no loader should give null");
            SelfCheckAssert.True(source.LoadImageAsync(null).Result == null, "LoadImageAsync(null)");

            // The loaders the host gives are the only way out: a supplied one is called, nothing else is.
            feeds.ImageLoader = delegate (string url)
            {
                loaderCalls[0]++;
                return Task.FromResult<System.Drawing.Image>(null);
            };
            SelfCheckAssert.True(source.LoadImageAsync("https://example.invalid/cover.png").Result == null, "stub image loader result");
            SelfCheckAssert.Equal(1, loaderCalls[0], "the host's image loader is called once");
        }

        private static void AdapterDashboardAnnouncements()
        {
            SettingsData settings = new SettingsData();
            int[] saves = new int[1];
            DashboardFeeds feeds = new DashboardFeeds
            {
                Announcements = new List<AnnouncementInfo>
                {
                    new AnnouncementInfo { Id = 11, Title = "Hello", Message = "World", DateStart = new DateTime(2024, 1, 1), DateEnd = new DateTime(2024, 2, 1) }
                }
            };
            DashboardDataSource source = new DashboardDataSource(Context(settings, saves), feeds);

            IList<AnnouncementInfo> list = source.GetAnnouncements();
            SelfCheckAssert.Equal(1, list.Count, "announcements");
            list.Clear();
            SelfCheckAssert.Equal(1, source.GetAnnouncements().Count, "GetAnnouncements returns a copy");

            SelfCheckAssert.True(!source.IsAnnouncementDismissed(11), "not dismissed at first");
            source.DismissAnnouncement(11);
            SelfCheckAssert.True(source.IsAnnouncementDismissed(11), "dismissed after DismissAnnouncement");
            SelfCheckAssert.Equal(1, settings.DismissedAnnouncements.Count, "one dismissed id in the settings");
            SelfCheckAssert.True(saves[0] >= 1, "the SaveSettings hook is called (the hook writes nothing here)");
        }

        // A handler that counts requests and never sends one.
        private sealed class CountingHandler : HttpMessageHandler
        {
            public int Requests;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Requests);
                throw new InvalidOperationException("the self-check must not make an HTTP request");
            }
        }

        private static void AdapterUploaderNoNetwork()
        {
            CountingHandler handler = new CountingHandler();
            using (HttpClient client = new HttpClient(handler))
            {
                // No options at all, plain http, and an empty endpoint: none of them is configured.
                HttpScreenshotUploader none = new HttpScreenshotUploader(null, client);
                SelfCheckAssert.True(!none.IsConfigured, "no options should not be configured");

                HttpScreenshotUploader plainHttp = new HttpScreenshotUploader(new ScreenshotUploadOptions { Endpoint = "http://example.invalid/upload" }, client);
                SelfCheckAssert.True(!plainHttp.IsConfigured, "an http endpoint should not be configured (https only)");

                HttpScreenshotUploader https = new HttpScreenshotUploader(new ScreenshotUploadOptions { Endpoint = "https://example.invalid/upload" }, client);
                SelfCheckAssert.True(https.IsConfigured, "an https endpoint should be configured");

                // The unconfigured upload fails before its first await, so the task is already faulted.
                Task<string> upload = none.UploadAsync("/nonexistent-arisen-selfcheck/shot.png", "title");
                SelfCheckAssert.True(upload.IsCompleted && upload.IsFaulted, "an unconfigured upload should be a faulted task");
                SelfCheckAssert.True(upload.Exception.InnerException is NotSupportedException, "an unconfigured upload should fail with NotSupportedException");

                Task<string> uploadHttp = plainHttp.UploadAsync("/nonexistent-arisen-selfcheck/shot.png", "title");
                SelfCheckAssert.True(uploadHttp.IsCompleted && uploadHttp.IsFaulted, "an http upload should be a faulted task");
                SelfCheckAssert.True(uploadHttp.Exception.InnerException is NotSupportedException, "an http upload should fail with NotSupportedException");
            }

            SelfCheckAssert.Equal(0, handler.Requests, "HTTP requests made");
        }

        private static void AdapterXboxFileService()
        {
            XboxConsoleFileServiceUnavailable service = new XboxConsoleFileServiceUnavailable();
            SelfCheckAssert.True(!service.IsConnected, "IsConnected");
            SelfCheckAssert.True(service.InitialPath == null, "InitialPath");
            SelfCheckAssert.True(service.GetDrives().Count > 0, "GetDrives");

            // Console-side separator: a backslash is correct here, it is not a local path.
            SelfCheckAssert.Equal("\\", service.PathSeparator, "PathSeparator");

            Task<IList<FileManagerEntry>> list = service.ListAsync("Hdd:\\");
            SelfCheckAssert.True(list.IsCompleted && list.IsFaulted, "ListAsync should be a faulted task");
            SelfCheckAssert.True(list.Exception.InnerException is NotSupportedException, "ListAsync should fail with NotSupportedException");

            Task<bool> exists = service.ExistsAsync("Hdd:\\x", false);
            SelfCheckAssert.True(exists.IsCompleted && exists.IsFaulted, "ExistsAsync should be a faulted task");
            Exception ignored = exists.Exception;
            SelfCheckAssert.NotNull(ignored, "ExistsAsync exception");
        }

        private static void AdapterTransferHooksDefaults()
        {
            Ps3TransferHooks hooks = new Ps3TransferHooks();
            SelfCheckAssert.True(hooks.ChooseGameRegion == null, "ChooseGameRegion default");
            SelfCheckAssert.True(hooks.ChooseUserProfile == null, "ChooseUserProfile default");
            SelfCheckAssert.True(hooks.IsWebManInstalled == null, "IsWebManInstalled default");
            SelfCheckAssert.True(hooks.ModArchiveDownloader == null, "ModArchiveDownloader default");
            SelfCheckAssert.True(hooks.GameSaveArchiveDownloader == null, "GameSaveArchiveDownloader default");
            SelfCheckAssert.True(hooks.FileDownloader == null, "FileDownloader default");
            SelfCheckAssert.True(!hooks.LegacyInvertedRegionFolderCheck, "LegacyInvertedRegionFolderCheck default");
        }

        // ====================================================================================================
        // (c) wiring
        // ====================================================================================================

        private static void WiringDialogContext()
        {
            DialogContext ctx = new DialogContext();
            SelfCheckAssert.True(ctx.Profile() == null, "Profile() with no callback");
            SelfCheckAssert.True(!ctx.Connected(), "Connected() with no callback");
            SelfCheckAssert.True(ctx.Address() == null, "Address() with no callback");
            ctx.Status("ignored");
            ctx.Save();

            string status = null;
            int saved = 0;
            ctx.SetStatus = delegate (string text) { status = text; };
            ctx.SaveSettings = delegate () { saved++; };
            ctx.IsConsoleConnected = delegate () { return true; };
            ctx.ConsoleAddress = delegate () { return "192.0.2.1"; };
            ctx.Status("hello");
            ctx.Save();
            SelfCheckAssert.Equal("hello", status, "status callback");
            SelfCheckAssert.Equal(1, saved, "save callback");
            SelfCheckAssert.True(ctx.Connected(), "Connected() with a callback");
            SelfCheckAssert.Equal("192.0.2.1", ctx.Address(), "Address() with a callback");
        }

        private static void WiringShellServices()
        {
            ShellServices services = new ShellServices();
            SelfCheckAssert.True(services.LoadSettingsOnAttach, "LoadSettingsOnAttach default");
            SelfCheckAssert.True(services.RunSetupWizardOnFirstUse, "RunSetupWizardOnFirstUse default");
            SelfCheckAssert.True(!services.DiscordAvailable, "DiscordAvailable default");
            SelfCheckAssert.True(services.Connection == null && services.Dialogs == null && services.Localizer == null, "optional members default to null");

            // The default connection never connects and never starts work.
            NullConsoleConnection connection = new NullConsoleConnection();
            SelfCheckAssert.True(!connection.IsConnected, "NullConsoleConnection.IsConnected");
            SelfCheckAssert.True(connection.Profile == null && connection.FileService == null, "NullConsoleConnection profile and file service");
            Task<bool> connect = connection.ConnectAsync(new ConsoleProfile());
            SelfCheckAssert.True(connect.IsCompleted && !connect.Result, "NullConsoleConnection.ConnectAsync should complete with false");
            SelfCheckAssert.Equal(0, new List<ConsoleProfile>(connection.ScanXboxConsoles()).Count, "NullConsoleConnection.ScanXboxConsoles");
            connection.Disconnect();
        }

        private static void WiringShellController()
        {
            ShellController controller = new ShellController();
            SelfCheckAssert.True(!controller.IsAttached, "IsAttached before Attach");
            SelfCheckAssert.True(controller.Dialogs == null, "Dialogs before Attach");
            SelfCheckAssert.True(controller.Connection == null, "Connection before Attach");

            bool threw = false;
            try
            {
                controller.Attach(null, new ShellServices());
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }
            SelfCheckAssert.True(threw, "Attach(null) should throw ArgumentNullException");
            SelfCheckAssert.True(!controller.IsAttached, "IsAttached after a failed Attach");
        }

        // DialogLauncher is a static class, so there is nothing to construct. What can be checked without showing a window
        // is that it is static and that its item methods return false for a null item before any dialog is created.
        private static void WiringDialogLauncher()
        {
            Type type = typeof(DialogLauncher);
            SelfCheckAssert.True(type.IsAbstract && type.IsSealed, "DialogLauncher should be a static class");

            DialogContext ctx = new DialogContext();
            SelfCheckAssert.True(!DialogLauncher.ShowCustomItemDetails(null, ctx, null), "ShowCustomItemDetails(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemDetails(null, ctx, null, null), "ShowItemDetails(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemAppDetails(null, ctx, null), "ShowItemAppDetails(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemPackageDetails(null, ctx, null), "ShowItemPackageDetails(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemGameSaveDetails(null, ctx, null), "ShowItemGameSaveDetails(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemGameCheats(null, ctx, (ArisenStudio.Models.GameData.PS3.GameItemData)null), "ShowItemGameCheats(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowItemGamePatches(null, ctx, null), "ShowItemGamePatches(null)");
            SelfCheckAssert.True(!DialogLauncher.ShowGameTrainers(null, ctx, null), "ShowGameTrainers(null)");
        }

        // ====================================================================================================
        // (d) text
        // ====================================================================================================

        // Runs body with the ResourceText dictionary set to the given values, and puts the empty dictionary back after.
        // No other check installs a dictionary, so "empty" is the state every check starts from.
        private static void WithDictionary(IDictionary<string, string> values, Action body)
        {
            ResourceText.SetDictionary(values);
            try
            {
                body();
            }
            finally
            {
                ResourceText.SetDictionary(null);
            }
        }

        private static void AssertPlain(string text, string what)
        {
            SelfCheckAssert.True(!string.IsNullOrEmpty(text), what + " is empty");
            SelfCheckAssert.True(text.IndexOf('<') < 0 && text.IndexOf('>') < 0, what + " still has a tag: " + text);
            SelfCheckAssert.True(text.IndexOf("href", StringComparison.OrdinalIgnoreCase) < 0, what + " still has an href: " + text);
            SelfCheckAssert.True(text.IndexOf("&lt;", StringComparison.Ordinal) < 0 && text.IndexOf("&amp;", StringComparison.Ordinal) < 0, what + " still has an entity: " + text);
            SelfCheckAssert.True(text.IndexOf("{0}", StringComparison.Ordinal) < 0, what + " still has {0}: " + text);
            SelfCheckAssert.True(text.IndexOf("{{", StringComparison.Ordinal) < 0 && text.IndexOf("}}", StringComparison.Ordinal) < 0, what + " has a doubled brace: " + text);
        }

        // No dictionary: the caller's English wins, and it has no anchor.
        private static void TextErrorFallback()
        {
            WithDictionary(null, delegate ()
            {
                string text = MessageTextSanitizer.ErrorOccurred("boom");
                AssertPlain(text, "ErrorOccurred");
                SelfCheckAssert.Equal("An error occurred: boom", text, "ErrorOccurred without a dictionary");
                SelfCheckAssert.Equal(text, MessageTextSanitizer.ErrorOccurred("boom", true), "square brackets change nothing without an anchor");
                SelfCheckAssert.Equal("An error occurred: ", MessageTextSanitizer.ErrorOccurred(null), "ErrorOccurred(null)");
            });
        }

        // The real en_US text of ERROR_OCCURRED has an HTML anchor. It must come out as "text (url)".
        private static void TextErrorAnchor()
        {
            string html;
            SelfCheckAssert.True(EnglishKeyTable.DefinedInEnUs.TryGetValue(LocalizedAnchorKey, out html), "EnglishKeyTable has no ERROR_OCCURRED");
            SelfCheckAssert.True(html.IndexOf("<a ", StringComparison.OrdinalIgnoreCase) >= 0, "the en_US ERROR_OCCURRED text is expected to hold an anchor");

            Dictionary<string, string> values = new Dictionary<string, string> { { LocalizedAnchorKey, html } };
            WithDictionary(values, delegate ()
            {
                string text = MessageTextSanitizer.ErrorOccurred("boom");
                AssertPlain(text, "ErrorOccurred (round brackets)");
                SelfCheckAssert.True(text.StartsWith("An error occurred: boom", StringComparison.Ordinal), "start of the text: " + text);
                SelfCheckAssert.True(text.IndexOf("opening a new issue (https://github.com/ohhsodead/arisen-studio/issues/new?labels=bug&template=bug.yml)", StringComparison.Ordinal) >= 0, "anchor as text (url): " + text);

                string square = MessageTextSanitizer.ErrorOccurred("boom", true);
                AssertPlain(square, "ErrorOccurred (square brackets)");
                SelfCheckAssert.True(square.IndexOf("opening a new issue [https://github.com/ohhsodead/arisen-studio/issues/new?labels=bug&template=bug.yml]", StringComparison.Ordinal) >= 0, "anchor as text [url]: " + square);

                // A message that looks like markup or a format item is inserted as it is.
                string odd = MessageTextSanitizer.ErrorOccurred("bad <b>x</b> {0} {1}");
                SelfCheckAssert.True(odd.StartsWith("An error occurred: bad <b>x</b> {0} {1}", StringComparison.Ordinal), "message inserted verbatim: " + odd);

                // The shared plain-text helper agrees on the anchor.
                AssertPlain(MessageTextSanitizer.ToPlainText(html).Replace("{0}", "x"), "ToPlainText of the raw text");
            });
        }

        private static void TextConnecting()
        {
            // No dictionary: the English fallbacks.
            WithDictionary(null, delegate ()
            {
                SelfCheckAssert.Equal("Connecting to console...", MessageTextSanitizer.ConnectingToConsole(null), "no address");
                SelfCheckAssert.Equal("Connecting to console...", MessageTextSanitizer.ConnectingToConsole("   "), "blank address");
                SelfCheckAssert.Equal("Connecting to: 192.168.0.5", MessageTextSanitizer.ConnectingToConsole(" 192.168.0.5 "), "address, trimmed");
            });

            // The real en_US text has {0}; with no address the status bar must not show it.
            string template;
            SelfCheckAssert.True(EnglishKeyTable.DefinedInEnUs.TryGetValue(MessageTextSanitizer.ConnectingToConsoleKey, out template), "EnglishKeyTable has no CONNECTING_TO_CONSOLE");
            SelfCheckAssert.True(template.IndexOf("{0}", StringComparison.Ordinal) >= 0, "the en_US CONNECTING_TO_CONSOLE text is expected to hold {0}");

            Dictionary<string, string> values = new Dictionary<string, string> { { MessageTextSanitizer.ConnectingToConsoleKey, template } };
            WithDictionary(values, delegate ()
            {
                string without = MessageTextSanitizer.ConnectingToConsole(null);
                AssertPlain(without, "ConnectingToConsole(null)");
                SelfCheckAssert.True(without.IndexOf('{') < 0 && without.IndexOf('}') < 0, "no brace left: " + without);
                SelfCheckAssert.True(without.EndsWith("...", StringComparison.Ordinal), "ends with an ellipsis: " + without);

                string with = MessageTextSanitizer.ConnectingToConsole("10.0.0.1");
                AssertPlain(with, "ConnectingToConsole(address)");
                SelfCheckAssert.True(with.IndexOf("10.0.0.1", StringComparison.Ordinal) >= 0, "the address is in the text: " + with);
            });
        }

        // ERROR_OCCURRED and CONNECTING_TO_CONSOLE are not aliases, so AliasedText hands back the resource text as it is.
        // Plain text is what the sanitizer makes of it; AliasedText itself must never give null or an empty string.
        private static void TextAliased()
        {
            WithDictionary(null, delegate ()
            {
                string error = AliasedText.Get(MessageTextSanitizer.ErrorOccurredKey);
                SelfCheckAssert.True(!string.IsNullOrEmpty(error), "AliasedText.Get(ERROR_OCCURRED) is empty");
                SelfCheckAssert.True(AliasedText.Localizer(MessageTextSanitizer.ErrorOccurredKey) == null, "Localizer for a non-alias with no dictionary should be null");
                SelfCheckAssert.True(!AliasedText.IsAlias(MessageTextSanitizer.ErrorOccurredKey), "ERROR_OCCURRED is not an alias");

                // Through the sanitizer it is plain text, with a placeholder the caller still fills in.
                string plain = MessageTextSanitizer.ToPlainText(error).Replace("{0}", "boom");
                AssertPlain(plain, "plain ERROR_OCCURRED via AliasedText");
                SelfCheckAssert.True(plain.IndexOf("boom", StringComparison.Ordinal) >= 0, "the message is in the text: " + plain);

                string connecting = AliasedText.Get(MessageTextSanitizer.ConnectingToConsoleKey);
                SelfCheckAssert.True(!string.IsNullOrEmpty(connecting), "AliasedText.Get(CONNECTING_TO_CONSOLE) is empty");
                AssertPlain(MessageTextSanitizer.RemovePlaceholders(connecting), "plain CONNECTING_TO_CONSOLE via AliasedText");

                // The English fallback the caller gives is used when the table has nothing; a missing key never gives null.
                SelfCheckAssert.Equal("fallback", AliasedText.Get("NO_SUCH_KEY_FOR_SELFCHECK", "fallback"), "unknown key with a fallback");
                SelfCheckAssert.Equal(string.Empty, AliasedText.Get(null), "null key");
                SelfCheckAssert.Equal("Connecting to: 10.0.0.1", AliasedText.Format(MessageTextSanitizer.ConnectingToConsoleKey, "Connecting to: {0}", "10.0.0.1"), "AliasedText.Format");

                // An alias resolves to the en_US text of its target even with no dictionary installed.
                SelfCheckAssert.True(AliasedText.IsAlias("CATEGORY"), "CATEGORY is expected to be an alias");
                SelfCheckAssert.Equal("LABEL_CATEGORY", AliasedText.TargetKey("CATEGORY"), "alias target");
                SelfCheckAssert.True(!string.IsNullOrEmpty(AliasedText.Localizer("CATEGORY")), "Localizer(CATEGORY) should give the en_US text");
            });
        }
    }
}
#endif
