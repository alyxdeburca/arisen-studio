using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Forms.Pages.Library;
using ArisenStudio.Io;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.GameData.PS3;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;

namespace ArisenStudio.Shell.Wiring
{
    /// <summary>
    /// Binds the existing <see cref="ShellForm" /> to the pages, the dashboard, the dialogs and the services without
    /// editing ShellForm. One call does it all:
    /// <code>
    /// ShellForm shell = new ShellForm();
    /// ShellController controller = new ShellController();
    /// controller.Attach(shell, services);   // before Application.Run(shell)
    /// Application.Run(shell);
    /// </code>
    /// Attach loads the settings, replaces the placeholder pages with the real ones (PageHost.RegisterPage under the
    /// same keys), sets <see cref="ShellForm.ResolvePageKey" />, subscribes to <see cref="ShellForm.RibbonCommandRequested" />,
    /// <see cref="AppHost.StatusChanged" />, <see cref="AppHost.UpdateAvailableChanged" /> and the form's Shown,
    /// FormClosing and FormClosed events, and unsubscribes everything again when the form closes.
    /// <para>
    /// The shell makes no other call into the controller. Optional calls: <see cref="ApplyLanguage" /> after the host
    /// changed <see cref="ShellServices.Localizer" />, <see cref="RebindSettings" /> after the host replaced
    /// AppHost.Settings, <see cref="SaveSettings" />, <see cref="ReloadActivePage" />. UI thread only. Nothing here was
    /// compiled or run.
    /// </para>
    /// </summary>
    internal sealed class ShellController
    {
        private ShellForm _form;
        private ShellServices _services;
        private DialogContext _dialogs;
        private IConsoleConnection _connection;
        private string _settingsPath;
        private bool _attached;

        private DashboardPage _dashboard;
        private DownloadsPage _downloadsPage;
        private InstalledFilesPage _installedPage;
        private FileManagerPage _fileManagerPage;
        private SettingsPage _settingsPage;
        private readonly Dictionary<string, LibraryPageBase> _library = new Dictionary<string, LibraryPageBase>();
        private readonly Dictionary<string, Control> _pageByKey = new Dictionary<string, Control>();

        // ====================================================================================================
        // Public surface
        // ====================================================================================================

        /// <summary>True between Attach and the form closing.</summary>
        public bool IsAttached
        {
            get { return _attached; }
        }

        /// <summary>The dialog context in use (the one in the services, or the one the controller created).</summary>
        public DialogContext Dialogs
        {
            get { return _dialogs; }
        }

        /// <summary>The connection in use.</summary>
        public IConsoleConnection Connection
        {
            get { return _connection; }
        }

        /// <summary>
        /// Attaches the controller to the shell. Call once, on the UI thread, after <c>new ShellForm()</c> and before the form
        /// is shown (Application.Run), so the form's own Load handler already reloads the dashboard from the real data source.
        /// </summary>
        /// <param name="form">The shell. Its Navigation, Pages, Ribbon, About and RibbonCommandRequested members are used.</param>
        /// <param name="services">Data sources and hooks; null means all defaults (empty pages, no console, English).</param>
        /// <param name="dashboard">
        /// The dashboard page the shell created. ShellForm keeps it in a private field and has no property for it. When
        /// null the controller reads the private field "_dashboard" by reflection; when that fails too the dashboard is
        /// left unbound (stub data source) and the status strip says so. Passing it is better: add
        /// <c>internal DashboardPage Dashboard { get { return _dashboard; } }</c> to ShellForm (one line, not made here).
        /// </param>
        public void Attach(ShellForm form, ShellServices services, DashboardPage dashboard = null)
        {
            if (form == null)
            {
                throw new ArgumentNullException("form");
            }
            if (_attached)
            {
                throw new InvalidOperationException("ShellController is already attached.");
            }

            _form = form;
            _services = services ?? new ShellServices();
            _connection = _services.Connection ?? new NullConsoleConnection();
            _settingsPath = string.IsNullOrEmpty(_services.SettingsPath) ? UserFolders.SettingsData : _services.SettingsPath;

            // 1. Settings first: every page binds to AppHost.Settings.
            if (_services.LoadSettingsOnAttach)
            {
                if (!AppHost.LoadSettings(_settingsPath))
                {
                    _form.SetStatusText("Settings not loaded, using defaults.");
                }
            }

            // 2. Dialog context.
            BuildDialogContext();

            // 3. Pages and dashboard.
            CreatePages();
            BindDashboard(dashboard);

            // 4. Shell hooks and subscriptions.
            _form.ResolvePageKey = ResolvePageKey;
            _form.RibbonCommandRequested += OnRibbonCommandRequested;
            _form.Shown += OnFormShown;
            _form.FormClosing += OnFormClosing;
            _form.FormClosed += OnFormClosed;
            AppHost.StatusChanged += OnAppStatusChanged;
            AppHost.UpdateAvailableChanged += OnUpdateAvailableChanged;
            _connection.Changed += OnConnectionChanged;
            _attached = true;

            // 5. Initial state.
            ApplySettingsToPages();
            if (_services.Localizer != null)
            {
                ApplyLanguage();
            }
            ApplyConnectionState(false);
            SyncRibbonChecks();
            _form.SetUpdateAvailable(UpdateExtensions.IsUpdateAvailable);
        }

        /// <summary>
        /// Gives every page and the nav captions the current <see cref="ShellServices.Localizer" /> (old ApplyLanguage,
        /// ledger MW2-029). Call it after the host switched the language. The old resource has keys only for 11 of the
        /// 14 nav leaves (NAVIGATION_DASHBOARD, _INSTALLED_FILES, _DOWNLOADS, _FILE_MANAGER, _SETTINGS, _GAME_MODS,
        /// _HOMEBREW, _RESOURCES, _PACKAGES, _GAME_SAVES, _GAME_CHEATS); the other leaves and the two groups keep their
        /// English captions unless the host's localizer answers NAVIGATION_CUSTOM_MODS, NAVIGATION_TRAINERS,
        /// NAVIGATION_GAMES, NAVIGATION_GENERAL or NAVIGATION_LIBRARY (those four-plus keys are guesses).
        /// </summary>
        public void ApplyLanguage()
        {
            Func<string, string> localizer = _services.Localizer;

            _downloadsPage.Localizer = localizer;
            _installedPage.Localizer = localizer;
            _fileManagerPage.Localizer = localizer;
            _settingsPage.Localizer = localizer;
            foreach (LibraryPageBase page in _library.Values)
            {
                page.Localizer = localizer;
            }
            if (_dashboard != null)
            {
                _dashboard.Localizer = localizer;
            }

            _downloadsPage.ApplyLanguage();
            _installedPage.ApplyLanguage();
            _fileManagerPage.ApplyLanguage();
            _settingsPage.ApplyLanguage();
            foreach (LibraryPageBase page in _library.Values)
            {
                page.ApplyLanguage();
            }
            if (_dashboard != null)
            {
                _dashboard.ApplyLanguage();
            }

            SetLeafCaption(ShellPageKeys.Dashboard, "NAVIGATION_DASHBOARD");
            SetLeafCaption(ShellPageKeys.InstalledFiles, "NAVIGATION_INSTALLED_FILES");
            SetLeafCaption(ShellPageKeys.Downloads, "NAVIGATION_DOWNLOADS");
            SetLeafCaption(ShellPageKeys.FileManager, "NAVIGATION_FILE_MANAGER");
            SetLeafCaption(ShellPageKeys.Settings, "NAVIGATION_SETTINGS");
            SetLeafCaption(ShellPageKeys.GameMods, "NAVIGATION_GAME_MODS");
            SetLeafCaption(ShellPageKeys.Homebrew, "NAVIGATION_HOMEBREW");
            SetLeafCaption(ShellPageKeys.Resources, "NAVIGATION_RESOURCES");
            SetLeafCaption(ShellPageKeys.PkgStore, "NAVIGATION_PACKAGES");
            SetLeafCaption(ShellPageKeys.GameSaves, "NAVIGATION_GAME_SAVES");
            SetLeafCaption(ShellPageKeys.GameCheats, "NAVIGATION_GAME_CHEATS");
            SetLeafCaption(ShellPageKeys.CustomMods, "NAVIGATION_CUSTOM_MODS");
            SetLeafCaption(ShellPageKeys.Trainers, "NAVIGATION_TRAINERS");
            SetLeafCaption(ShellPageKeys.Games, "NAVIGATION_GAMES");
            SetGroupCaption(ShellForm.GroupGeneral, "NAVIGATION_GENERAL");
            SetGroupCaption(ShellForm.GroupLibrary, "NAVIGATION_LIBRARY");
        }

        /// <summary>Call after the host replaced AppHost.Settings (for example by AppHost.LoadSettings): rebinds the Settings page and the display flags.</summary>
        public void RebindSettings()
        {
            _settingsPage.Settings = AppHost.Settings;
            ApplySettingsToPages();
            ApplyConnectionState(false);
            SyncRibbonChecks();
        }

        /// <summary>Saves AppHost.Settings to the settings path. Returns false on failure (AppHost already reported the reason).</summary>
        public bool SaveSettings()
        {
            return AppHost.SaveSettings(_settingsPath);
        }

        /// <summary>Re-runs OnPageShown of the visible page (reload from its data source).</summary>
        public void ReloadActivePage()
        {
            string key = _form.Pages.ActiveKey;
            Control page;
            if (key != null && _pageByKey.TryGetValue(key, out page))
            {
                IShellPage shellPage = page as IShellPage;
                if (shellPage != null)
                {
                    shellPage.OnPageShown();
                }
            }
        }

        // ====================================================================================================
        // Setup
        // ====================================================================================================

        private void BuildDialogContext()
        {
            _dialogs = _services.Dialogs ?? new DialogContext();

            if (_dialogs.ConnectedProfile == null)
            {
                // Old MainWindow.ConsoleProfile: the connected profile, else the default one.
                _dialogs.ConnectedProfile = delegate
                {
                    ConsoleProfile connected = _connection.IsConnected ? _connection.Profile : null;
                    return connected ?? AppHost.Settings.GetDefaultProfile();
                };
            }
            if (_dialogs.IsConsoleConnected == null)
            {
                _dialogs.IsConsoleConnected = delegate { return _connection.IsConnected; };
            }
            if (_dialogs.SaveSettings == null)
            {
                _dialogs.SaveSettings = delegate { SaveSettings(); };
            }
            if (_dialogs.SetStatus == null)
            {
                _dialogs.SetStatus = delegate (string text) { _form.SetStatusText(text); };
            }
            if (_dialogs.ConnectConsole == null)
            {
                _dialogs.ConnectConsole = delegate { RunSafe(ConnectFlowAsync()); };
            }
            if (_dialogs.ScanXboxConsoles == null)
            {
                _dialogs.ScanXboxConsoles = delegate { return _connection.ScanXboxConsoles(); };
            }
            if (_dialogs.ConsoleAddress == null)
            {
                _dialogs.ConsoleAddress = delegate
                {
                    ConsoleProfile profile = _dialogs.Profile();
                    return profile == null ? null : profile.Address;
                };
            }
            if (_dialogs.Language == null)
            {
                _dialogs.Language = _services.Language;
            }
        }

        private void CreatePages()
        {
            // Library pages (LibraryPageBase subclasses). Each leaf key is also its page key.
            AddLibrary(ShellPageKeys.CustomMods, new CustomModsPage());
            AddLibrary(ShellPageKeys.GameMods, new GameModsPage());
            AddLibrary(ShellPageKeys.Homebrew, new HomebrewPage());
            AddLibrary(ShellPageKeys.Trainers, new TrainersPage());
            ResourcesPage resourcesPage = new ResourcesPage();
            AddLibrary(ShellPageKeys.Resources, resourcesPage);
            resourcesPage.RowClicked += (s, e) => OpenDetails(e.Item.Tag);
            AddLibrary(ShellPageKeys.PkgStore, new PkgStorePage());
            AddLibrary(ShellPageKeys.Games, new GamesPage());
            AddLibrary(ShellPageKeys.GameSaves, new GameSavesPage());
            AddLibrary(ShellPageKeys.GameCheats, new GameCheatsPage());

            _downloadsPage = new DownloadsPage();
            _downloadsPage.DataSource = _services.Downloads;
            _downloadsPage.OpenFolderRequested += (s, e) => OpenExternal(e.Item);
            _downloadsPage.RevealFileRequested += (s, e) => OpenExternal(SafeDirectoryName(e.Item));
            _downloadsPage.ViewDetailsRequested += OnDownloadDetailsRequested;
            _downloadsPage.DownloadDeleted += (s, e) => SaveSettings();
            _downloadsPage.StatusMessage += OnPageStatus;
            Register(ShellPageKeys.Downloads, _downloadsPage);

            _installedPage = new InstalledFilesPage();
            _installedPage.DataSource = _services.InstalledFiles;
            _installedPage.ViewDetailsRequested += OnInstalledDetailsRequested;
            _installedPage.UninstallRequested += OnInstalledUninstallRequested;
            _installedPage.RecordForgotten += (s, e) => SaveSettings();
            _installedPage.StatusMessage += OnPageStatus;
            Register(ShellPageKeys.InstalledFiles, _installedPage);

            _fileManagerPage = new FileManagerPage();
            _fileManagerPage.OpenFolderRequested += (s, e) => OpenExternal(e.Item);
            _fileManagerPage.LocalPathChanged += OnLocalPathChanged;
            _fileManagerPage.ConsolePathChanged += OnConsolePathChanged;
            _fileManagerPage.AddToModulesRequested += OnAddToModulesRequested;
            _fileManagerPage.StatusMessage += OnPageStatus;
            Register(ShellPageKeys.FileManager, _fileManagerPage);

            _settingsPage = new SettingsPage();
            _settingsPage.DiscordAvailable = _services.DiscordAvailable;
            _settingsPage.Settings = AppHost.Settings;
            _settingsPage.SettingChanged += OnSettingChanged;
            _settingsPage.LanguageChanged += OnLanguageChanged;
            _settingsPage.DiscordSettingsChanged += OnDiscordSettingsChanged;
            _settingsPage.LinkRequested += (s, e) => OpenExternal(e.Item);
            _settingsPage.ClearCacheRequested += OnClearCacheRequested;
            Register(ShellPageKeys.Settings, _settingsPage);
        }

        private void AddLibrary(string key, LibraryPageBase page)
        {
            page.DataSource = _services.Library;
            page.DetailsRequested += (s, e) => OpenDetails(e.Item.Tag);
            page.DownloadRequested += (s, e) => OnLibraryTransfer(e.Item, null);
            page.InstallRequested += (s, e) => OnLibraryTransfer(e.Item, true);
            page.UninstallRequested += (s, e) => OnLibraryTransfer(e.Item, false);
            page.AddCustomModRequested += (s, e) => { DialogLauncher.ShowNewCustomMod(_form, _dialogs, null, false); ReloadActivePage(); };
            page.EditCustomModRequested += (s, e) => { DialogLauncher.ShowNewCustomMod(_form, _dialogs, e.Item == null ? null : e.Item.Tag as CustomItemData, true); ReloadActivePage(); };
            page.CustomModDeleted += (s, e) => SaveSettings();
            page.FavoriteAdded += (s, e) => SaveSettings();
            page.StatusMessage += OnPageStatus;
            _library[key] = page;
            Register(key, page);
        }

        private void Register(string key, Control page)
        {
            _pageByKey[key] = page;
            _form.Pages.RegisterPage(key, page);
        }

        private void BindDashboard(DashboardPage dashboard)
        {
            DashboardPage page = dashboard;
            if (page == null)
            {
                // ShellForm keeps the dashboard in a private field and exposes no property (see the Attach documentation).
                FieldInfo field = typeof(ShellForm).GetField("_dashboard", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    page = field.GetValue(_form) as DashboardPage;
                }
            }
            if (page == null)
            {
                _form.SetStatusText("Dashboard not bound: ShellForm exposes no dashboard.");
                return;
            }

            _dashboard = page;
            if (_services.Dashboard != null)
            {
                _dashboard.DataSource = _services.Dashboard;
            }
            _dashboard.FavoriteGameClicked += OnFavoriteGameClicked;
            _dashboard.FavoriteModClicked += OnFavoriteModClicked;
            _dashboard.AnnouncementDismissed += (s, e) => SaveSettings();
            _dashboard.LinkClicked += (s, e) => OpenExternal(e.LinkText);
            // The shell already routes _dashboard.StatusMessage to its status strip.
        }

        // ====================================================================================================
        // Page keys, nav
        // ====================================================================================================

        /// <summary>
        /// Value of ShellForm.ResolvePageKey (old platform choice for Game Mods and Homebrew, MW1-087 and MW1-088). One page
        /// class serves every platform, so the key is returned as is; the page's Platform is set first and the page reloads
        /// when the host shows it (OnPageShown).
        /// </summary>
        private string ResolvePageKey(string leafKey)
        {
            LibraryPageBase page;
            if (leafKey != null && _library.TryGetValue(leafKey, out page))
            {
                page.Platform = CurrentLibraryPlatform();
            }
            return leafKey;
        }

        /// <summary>
        /// Shows only the nav leaves of the platform (old EnableConsoleActions and the start-up block of MainWindow_Load).
        /// PS3: Game Mods, Homebrew, Resources, Packages, Game Cheats. PS4: Homebrew, Games. Xbox 360: Game Mods, Homebrew,
        /// Trainers. Unknown platform (no profile): everything. Custom Mods, Game Saves and the General group stay visible
        /// (the old code hid Custom Mods; the Mono shell has no other way in).
        /// </summary>
        private void ApplyNavigationForPlatform(Platform? platform)
        {
            bool all = !platform.HasValue;
            bool ps3 = platform == Platform.PS3;
            bool ps4 = platform == Platform.PS4;
            bool xbox = platform == Platform.XBOX360;

            SetLeafVisible(ShellPageKeys.GameMods, all || ps3 || xbox);
            SetLeafVisible(ShellPageKeys.Homebrew, true);
            SetLeafVisible(ShellPageKeys.Trainers, all || xbox);
            SetLeafVisible(ShellPageKeys.Resources, all || ps3);
            SetLeafVisible(ShellPageKeys.PkgStore, all || ps3);
            SetLeafVisible(ShellPageKeys.Games, all || ps4);
            SetLeafVisible(ShellPageKeys.GameCheats, all || ps3);

            // If the visible page just became hidden, go home.
            string active = _form.Pages.ActiveKey;
            if (active != null && _form.Navigation.HasLeaf(active) && IsHiddenLeaf(active, all, ps3, ps4, xbox))
            {
                _form.ShowPage(ShellPageKeys.Dashboard);
            }
        }

        private static bool IsHiddenLeaf(string key, bool all, bool ps3, bool ps4, bool xbox)
        {
            if (all)
            {
                return false;
            }
            switch (key)
            {
                case ShellPageKeys.GameMods: return !(ps3 || xbox);
                case ShellPageKeys.Trainers: return !xbox;
                case ShellPageKeys.Resources: return !ps3;
                case ShellPageKeys.PkgStore: return !ps3;
                case ShellPageKeys.Games: return !ps4;
                case ShellPageKeys.GameCheats: return !ps3;
                default: return false;
            }
        }

        private void SetLeafVisible(string leafKey, bool visible)
        {
            _form.Navigation.SetLeafVisible(leafKey, visible);
        }

        private void SetLeafCaption(string leafKey, string resourceKey)
        {
            string text = _services.Localizer == null ? null : _services.Localizer(resourceKey);
            if (!string.IsNullOrEmpty(text))
            {
                _form.Navigation.SetLeafCaption(leafKey, text);
            }
        }

        private void SetGroupCaption(string groupKey, string resourceKey)
        {
            string text = _services.Localizer == null ? null : _services.Localizer(resourceKey);
            if (!string.IsNullOrEmpty(text))
            {
                _form.Navigation.SetGroupCaption(groupKey, text);
            }
        }

        // ====================================================================================================
        // Platform and connection state
        // ====================================================================================================

        private Platform? CurrentPlatform()
        {
            ConsoleProfile profile = _connection.IsConnected ? _connection.Profile : null;
            if (profile == null)
            {
                profile = AppHost.Settings.GetDefaultProfile();
            }
            return profile == null ? (Platform?)null : profile.Platform;
        }

        private LibraryPlatform CurrentLibraryPlatform()
        {
            Platform? platform = CurrentPlatform();
            if (platform == Platform.PS4)
            {
                return LibraryPlatform.PS4;
            }
            if (platform == Platform.XBOX360)
            {
                return LibraryPlatform.Xbox360;
            }
            return LibraryPlatform.PS3;
        }

        private static DashboardPlatform? ToDashboardPlatform(Platform? platform)
        {
            if (!platform.HasValue)
            {
                return null;
            }
            switch (platform.Value)
            {
                case Platform.PS4: return DashboardPlatform.PS4;
                case Platform.XBOX360: return DashboardPlatform.Xbox360;
                default: return DashboardPlatform.PS3;
            }
        }

        /// <summary>
        /// Pushes the connection and platform into the status strip, the ribbon, the nav, the pages and the dashboard.
        /// announce: put "Connected to ..." or "Disconnected" in the status text.
        /// </summary>
        private void ApplyConnectionState(bool announce)
        {
            bool connected = _connection.IsConnected;
            Platform? platform = CurrentPlatform();

            _form.SetConsoleState(platform ?? Platform.PS3, connected, connected && _connection.WebManInstalled);
            _form.SetCurrentGame(_connection.CurrentGameName);
            _form.SetCurrentGameVisible(connected && AppHost.Settings.AlwaysShowCurrentGamePlaying);

            ApplyNavigationForPlatform(platform);

            LibraryPlatform libraryPlatform = CurrentLibraryPlatform();
            foreach (LibraryPageBase page in _library.Values)
            {
                page.Platform = libraryPlatform;
            }
            if (_dashboard != null)
            {
                _dashboard.Platform = ToDashboardPlatform(platform);
            }

            _fileManagerPage.ConsoleService = connected ? _connection.FileService : null;
            _fileManagerPage.InitialLocalPath = LocalPathFor(platform);
            _fileManagerPage.ConnectionChanged();

            if (announce)
            {
                ConsoleProfile profile = _connection.Profile;
                _form.SetStatusText(connected
                    ? "Connected to " + (profile == null ? "console" : profile.Name) + "."
                    : "Disconnected.");
            }
            ReloadActivePage();
        }

        private void ApplySettingsToPages()
        {
            SettingsData settings = AppHost.Settings;
            _downloadsPage.UseRelativeTimes = settings.UseRelativeTimes;
            _downloadsPage.DownloadsFolder = DownloadsFolder();
            _installedPage.UseRelativeTimes = settings.UseRelativeTimes;
            _fileManagerPage.UseFormattedFileSizes = settings.UseFormattedFileSizes;
            _fileManagerPage.UseRelativeTimes = settings.UseRelativeTimes;
            _fileManagerPage.AutoLoadDirectoryListings = settings.AutoLoadDirectoryListings;
            foreach (LibraryPageBase page in _library.Values)
            {
                page.UseRelativeTimes = settings.UseRelativeTimes;
                page.UseFormattedFileSizes = settings.UseFormattedFileSizes;
            }
            if (_dashboard != null)
            {
                _dashboard.UseRelativeTimes = settings.UseRelativeTimes;
            }
        }

        private static string DownloadsFolder()
        {
            return AppHost.Settings.PathDownloads.GetFullPath(AppHost.Settings.PathAppData);
        }

        private static string LocalPathFor(Platform? platform)
        {
            SettingsData s = AppHost.Settings;
            if (platform == Platform.PS4) { return s.LocalPathPS4; }
            if (platform == Platform.XBOX360) { return s.LocalPathXbox; }
            return s.LocalPathPS3;
        }

        /// <summary>Checks on the two ribbon check items follow the settings (old ToggleSettings... handlers set ButtonInstallModsToUSB.Checked).</summary>
        private void SyncRibbonChecks()
        {
            _form.Ribbon.SetChecked(RibbonCommandIds.DiscordPresence, AppHost.Settings.AlwaysShowPresence);
            _form.Ribbon.SetChecked(RibbonCommandIds.InstallModsToUsb, AppHost.Settings.InstallGameModsToUsbDevice);
        }

        // ====================================================================================================
        // Events: AppHost, connection, form lifecycle
        // ====================================================================================================

        private void OnAppStatusChanged(object sender, StatusEventArgs e)
        {
            string text = e.Status;
            Post(delegate { _form.SetStatusText(text); });
        }

        private void OnUpdateAvailableChanged(bool available)
        {
            Post(delegate { _form.SetUpdateAvailable(available); });
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            Post(delegate { ApplyConnectionState(true); });
        }

        private async void OnFormShown(object sender, EventArgs e)
        {
            try
            {
                // Old first run: the setup wizard when there is no profile yet.
                if (_services.RunSetupWizardOnFirstUse && AppHost.Settings.FirstTimeUse && AppHost.Settings.ConsoleProfiles.Count == 0)
                {
                    DialogLauncher.ShowSetupWizard(_form, _dialogs, null);
                    AppHost.Settings.FirstTimeUse = false;
                    SaveSettings();
                    ApplyConnectionState(false);
                }

                if (_services.StartupAsync != null)
                {
                    _form.SetStatusText("Initializing...");
                    await _services.StartupAsync();
                    if (_form.IsDisposed)
                    {
                        return;
                    }
                    if (_dashboard != null)
                    {
                        _dashboard.ReloadAll();
                    }
                    ReloadActivePage();
                    _form.SetStatusText("Idling...");
                }

                _form.SetUpdateAvailable(UpdateExtensions.IsUpdateAvailable);
            }
            catch (Exception ex)
            {
                AppHost.SetStatus("Start-up failed: " + ex.Message, ex);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            try
            {
                SaveSettings();

                if (_connection.IsConnected)
                {
                    try
                    {
                        _connection.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        AppHost.SetStatus("Disconnect failed: " + ex.Message, ex);
                    }
                }

                if (AppHost.Settings.ClearCacheOnClose)
                {
                    try
                    {
                        IoExtensions.DeleteDirectory(CacheFolder());
                    }
                    catch (Exception ex)
                    {
                        AppHost.SetStatus("Could not clear the cache: " + ex.Message, ex);
                    }
                }

                if (_services.ShuttingDown != null)
                {
                    _services.ShuttingDown();
                }
            }
            catch (Exception ex)
            {
                AppHost.SetStatus("Close handler failed: " + ex.Message, ex);
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            Detach();
        }

        /// <summary>Removes every subscription. Called when the form closes; safe to call twice.</summary>
        public void Detach()
        {
            if (!_attached)
            {
                return;
            }
            _attached = false;
            AppHost.StatusChanged -= OnAppStatusChanged;
            AppHost.UpdateAvailableChanged -= OnUpdateAvailableChanged;
            _connection.Changed -= OnConnectionChanged;
            _form.RibbonCommandRequested -= OnRibbonCommandRequested;
            _form.Shown -= OnFormShown;
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.ResolvePageKey = null;
        }

        // ====================================================================================================
        // Ribbon commands
        // ====================================================================================================

        private void OnRibbonCommandRequested(object sender, RibbonCommandEventArgs e)
        {
            RunSafe(HandleRibbonAsync(e));
        }

        private async Task HandleRibbonAsync(RibbonCommandEventArgs e)
        {
            string id = e.CommandId;

            // Check commands: the shell already showed the Settings page; write the setting the check stands for.
            if (id == RibbonCommandIds.DiscordPresence)
            {
                AppHost.Settings.AlwaysShowPresence = e.Checked;
                AfterSettingWrite(true);
                return;
            }
            if (id == RibbonCommandIds.InstallModsToUsb)
            {
                AppHost.Settings.InstallGameModsToUsbDevice = e.Checked;
                AfterSettingWrite(false);
                return;
            }

            switch (id)
            {
                case RibbonCommandIds.HeaderUpdate:
                    OpenExternal(Urls.GitHubRepo + "releases/latest");
                    return;

                case RibbonCommandIds.ConnectConsole:
                    await ConnectFlowAsync();
                    return;

                case RibbonCommandIds.DisconnectConsole:
                    _connection.Disconnect();
                    return;

                case RibbonCommandIds.AddNewProfile:
                    AddNewProfile();
                    return;

                case RibbonCommandIds.EditProfiles:
                    DialogLauncher.ShowEditConnections(_form, _dialogs, true);
                    ApplyConnectionState(false);
                    return;

                case RibbonCommandIds.ChangeProfile:
                    ChangeProfile();
                    return;

                case RibbonCommandIds.ScanXboxConsoles:
                    // Assumption: the old Scan command ran the Xbox scan inside the Setup Wizard (the wizard has ScanXboxConsoles).
                    DialogLauncher.ShowSetupWizard(_form, _dialogs, Platform.XBOX360);
                    ApplyConnectionState(false);
                    return;

                case RibbonCommandIds.DownloadsFolder:
                    OpenDownloadsFolder();
                    return;
            }

            if (DialogLauncher.TryShowTool(_form, _dialogs, id))
            {
                // Tools can change settings (regions, game files) and the installed state.
                SaveSettings();
                return;
            }

            // webMAN and XBDM commands go to the connection. The two notify commands ask for the text first.
            if (IsConsoleCommand(id))
            {
                string argument = null;
                if (id == RibbonCommandIds.ToolsPsNotifyMessage || id == RibbonCommandIds.ToolsXboxNotifyMessage)
                {
                    argument = TextPromptForm.Show(_form, e.Caption, "Message:", string.Empty);
                    if (string.IsNullOrEmpty(argument))
                    {
                        return;
                    }
                }
                bool ok = await _connection.RunCommandAsync(id, argument);
                _form.SetStatusText(ok ? e.Caption + ": done." : e.Caption + ": not available.");
                return;
            }

            _form.SetStatusText(e.Caption + ": not available yet.");
        }

        private static bool IsConsoleCommand(string id)
        {
            switch (id)
            {
                case RibbonCommandIds.ToolsPsPowerShutdown:
                case RibbonCommandIds.ToolsPsPowerRestart:
                case RibbonCommandIds.ToolsPsPowerSoftReboot:
                case RibbonCommandIds.ToolsPsPowerHardReboot:
                case RibbonCommandIds.ToolsPsPowerQuickReboot:
                case RibbonCommandIds.ToolsPsGamesOpenGui:
                case RibbonCommandIds.ToolsPsGamesMountBd:
                case RibbonCommandIds.ToolsPsGamesMountIso:
                case RibbonCommandIds.ToolsPsGamesMountPsn:
                case RibbonCommandIds.ToolsPsGamesUnmount:
                case RibbonCommandIds.ToolsPsNotifyMessage:
                case RibbonCommandIds.ToolsPsVirtualController:
                case RibbonCommandIds.ToolsXboxPowerShutdown:
                case RibbonCommandIds.ToolsXboxPowerSoftReboot:
                case RibbonCommandIds.ToolsXboxPowerHardReboot:
                case RibbonCommandIds.ToolsXboxNotifyMessage:
                    return true;
                default:
                    return false;
            }
        }

        private void AfterSettingWrite(bool discord)
        {
            SaveSettings();
            _settingsPage.LoadSettings();
            SyncRibbonChecks();
            if (discord && _services.DiscordSettingsChanged != null)
            {
                _services.DiscordSettingsChanged();
            }
        }

        private async Task ConnectFlowAsync()
        {
            ConsoleProfile profile;

            if (_connection.IsConnected && _connection.Profile != null)
            {
                // Caption "Reconnect Console": same profile again.
                profile = _connection.Profile;
                _connection.Disconnect();
            }
            else
            {
                if (AppHost.Settings.ConsoleProfiles.Count == 0)
                {
                    DialogLauncher.ShowSetupWizard(_form, _dialogs, null);
                    ApplyConnectionState(false);
                    if (AppHost.Settings.ConsoleProfiles.Count == 0)
                    {
                        return;
                    }
                }
                profile = DialogLauncher.ShowConnections(_form, _dialogs, null);
                if (profile == null)
                {
                    return;
                }
            }

            _form.SetStatusText("Connecting to " + profile.Name + "...");
            bool ok = await _connection.ConnectAsync(profile);
            if (!ok)
            {
                _form.SetStatusText("Could not connect to " + profile.Name + ".");
            }
            // A successful connect raises IConsoleConnection.Changed, which runs ApplyConnectionState.
        }

        private void AddNewProfile()
        {
            ConsoleProfile profile = DialogLauncher.ShowNewProfile(_form, _dialogs, null, false);
            if (profile == null)
            {
                return;
            }
            if (!AppHost.Settings.ConsoleProfiles.Contains(profile))
            {
                if (AppHost.Settings.ConsoleProfiles.Count == 0)
                {
                    profile.IsDefault = true;
                }
                AppHost.Settings.ConsoleProfiles.Add(profile);
            }
            SaveSettings();
            ApplyConnectionState(false);
        }

        private void ChangeProfile()
        {
            // Assumption: the old Change Profile picked the default profile (ConnectionsDialog without connecting).
            ConsoleProfile chosen = DialogLauncher.ShowConnections(_form, _dialogs, null);
            if (chosen == null)
            {
                return;
            }
            foreach (ConsoleProfile other in AppHost.Settings.ConsoleProfiles)
            {
                other.IsDefault = ReferenceEquals(other, chosen);
            }
            SaveSettings();
            ApplyConnectionState(false);
            _form.SetStatusText("Profile: " + chosen.Name + ".");
        }

        private void OpenDownloadsFolder()
        {
            string folder = DownloadsFolder();
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch (Exception ex)
            {
                _form.SetStatusText("Could not create the downloads folder: " + ex.Message);
                return;
            }
            OpenExternal(folder);
        }

        // ====================================================================================================
        // Page events
        // ====================================================================================================

        private void OnPageStatus(object sender, PageStatusEventArgs e)
        {
            _form.SetStatusText(e.Message);
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            SaveSettings();
            switch (e.Name)
            {
                case "UseRelativeTimes":
                case "UseFormattedFileSizes":
                case "AutoLoadDirectoryListings":
                    ApplySettingsToPages();
                    ReloadActivePage();
                    break;
                case "PathDownloads":
                case "PathAppData":
                    _downloadsPage.DownloadsFolder = DownloadsFolder();
                    break;
                case "AlwaysShowCurrentGamePlaying":
                    _form.SetCurrentGameVisible(_connection.IsConnected && AppHost.Settings.AlwaysShowCurrentGamePlaying);
                    break;
                case "InstallGameModsToUsbDevice":
                case "AlwaysShowPresence":
                case "ShowCurrentGamePlaying":
                    SyncRibbonChecks();
                    break;
            }
        }

        private void OnLanguageChanged(object sender, ItemEventArgs<string> e)
        {
            string previous = AppHost.Settings.Language;

            bool supported = true;
            if (_services.SwitchLanguage != null)
            {
                supported = _services.SwitchLanguage(e.Item);
            }
            if (!supported)
            {
                _settingsPage.SelectLanguage(previous);
                _form.SetStatusText("Language not supported: " + e.Item);
                return;
            }

            AppHost.Settings.Language = e.Item;
            if (_services.Language != null)
            {
                _dialogs.Language = _services.Language;
            }
            SaveSettings();
            ApplyLanguage();
        }

        private void OnDiscordSettingsChanged(object sender, EventArgs e)
        {
            SaveSettings();
            SyncRibbonChecks();
            if (_services.DiscordSettingsChanged != null)
            {
                _services.DiscordSettingsChanged();
            }
        }

        private void OnClearCacheRequested(object sender, EventArgs e)
        {
            try
            {
                IoExtensions.DeleteDirectory(CacheFolder());
                // Application.Restart on Mono is UNVERIFIED (it relaunches the entry assembly; under "mono app.exe" it may not).
                Application.Restart();
            }
            catch (Exception ex)
            {
                _form.SetStatusText("Unable to clear local cache. Error: " + ex.Message);
            }
        }

        private static string CacheFolder()
        {
            return Path.Combine(UserFolders.AppData, "Cache");
        }

        private void OnLocalPathChanged(object sender, ItemEventArgs<string> e)
        {
            if (!AppHost.Settings.RememberLocalPath || string.IsNullOrWhiteSpace(e.Item))
            {
                return;
            }
            Platform? platform = CurrentPlatform();
            if (platform == Platform.PS4) { AppHost.Settings.LocalPathPS4 = e.Item; }
            else if (platform == Platform.XBOX360) { AppHost.Settings.LocalPathXbox = e.Item; }
            else { AppHost.Settings.LocalPathPS3 = e.Item; }
        }

        private void OnConsolePathChanged(object sender, ItemEventArgs<string> e)
        {
            if (!AppHost.Settings.RememberConsolePath || string.IsNullOrWhiteSpace(e.Item))
            {
                return;
            }
            Platform? platform = CurrentPlatform();
            if (platform == Platform.PS4) { AppHost.Settings.ConsolePathPS4 = e.Item; }
            else if (platform == Platform.XBOX360) { AppHost.Settings.ConsolePathXbox = e.Item; }
            else { AppHost.Settings.ConsolePathPS3 = e.Item; }
        }

        private void OnAddToModulesRequested(object sender, AddToModulesEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Name))
            {
                e.Error = "The module name is empty.";
                return;
            }
            foreach (ListItem item in AppHost.Settings.GameFilesXbox)
            {
                if (string.Equals(item.Name, e.Name, StringComparison.OrdinalIgnoreCase))
                {
                    e.Error = "A module named " + e.Name + " already exists.";
                    return;
                }
            }
            AppHost.Settings.GameFilesXbox.Add(new ListItem { Name = e.Name, Value = e.Path });
            SaveSettings();
        }

        // ---- downloads and installed pages ----

        private void OnDownloadDetailsRequested(object sender, ItemEventArgs<DownloadRowInfo> e)
        {
            ModItemData mod = _services.Lookup == null ? null : _services.Lookup.FindDownloadedMod(e.Item);
            if (mod == null)
            {
                _form.SetStatusText("Details not available for this download.");
                return;
            }
            DialogLauncher.ShowItemDetails(_form, _dialogs, _dialogs.Categories, mod);
            ReloadActivePage();
        }

        private void OnInstalledDetailsRequested(object sender, ItemEventArgs<InstalledRowInfo> e)
        {
            object item = _services.Lookup == null ? null : _services.Lookup.FindInstalledItem(e.Item);
            if (item == null || !OpenDetails(item))
            {
                _form.SetStatusText("Details not available for this item.");
            }
        }

        private void OnInstalledUninstallRequested(object sender, ItemEventArgs<InstalledRowInfo> e)
        {
            object item = _services.Lookup == null ? null : _services.Lookup.FindInstalledItem(e.Item);
            CustomItemData custom = item as CustomItemData;
            if (custom != null)
            {
                DialogLauncher.ShowTransferCustom(_form, _dialogs, TransferType.UninstallCustom, custom);
                ReloadActivePage();
                return;
            }
            // Mods are uninstalled per download file inside their details dialog.
            if (item == null || !OpenDetails(item))
            {
                _form.SetStatusText("Uninstall not available for this item.");
            }
        }

        // ---- dashboard ----

        private void OnFavoriteGameClicked(object sender, FavoriteGameEventArgs e)
        {
            // The pages have no category filter API, so the click only opens the matching page (PS4: Homebrew, otherwise Game Mods).
            string key = e.Game.Platform == DashboardPlatform.PS4 ? ShellPageKeys.Homebrew : ShellPageKeys.GameMods;
            _form.ShowPage(key);
        }

        private void OnFavoriteModClicked(object sender, FavoriteModEventArgs e)
        {
            ModItemData mod = _services.Lookup == null ? null : _services.Lookup.FindFavoriteMod(e.Mod);
            if (mod == null)
            {
                _form.SetStatusText("Details not available for " + e.Mod.ModName + ".");
                return;
            }
            DialogLauncher.ShowItemDetails(_form, _dialogs, _dialogs.Categories, mod);
            SaveSettings();
        }

        // ---- library pages ----

        /// <summary>
        /// Opens the details dialog for a row's Tag. The ILibraryDataSource must put the database object in LibraryRowInfo.Tag:
        /// ModItemData, AppItemData, PackageItemData, GameSaveItemData, TrainerGameItem, CustomItemData, the PS3 GameItemData
        /// (Game Cheats page) or GamePatchItemData (Games page). Returns false for anything else.
        /// </summary>
        private bool OpenDetails(object tag)
        {
            bool opened = false;
            ModItemData mod = tag as ModItemData;
            AppItemData app = tag as AppItemData;
            PackageItemData package = tag as PackageItemData;
            GameSaveItemData save = tag as GameSaveItemData;
            TrainerGameItem trainers = tag as TrainerGameItem;
            CustomItemData custom = tag as CustomItemData;
            GameItemData game = tag as GameItemData;
            GamePatchItemData patch = tag as GamePatchItemData;

            if (mod != null) { opened = DialogLauncher.ShowItemDetails(_form, _dialogs, _dialogs.Categories, mod); }
            else if (app != null) { opened = DialogLauncher.ShowItemAppDetails(_form, _dialogs, app); }
            else if (package != null) { opened = DialogLauncher.ShowItemPackageDetails(_form, _dialogs, package); }
            else if (save != null) { opened = DialogLauncher.ShowItemGameSaveDetails(_form, _dialogs, save); }
            else if (trainers != null) { opened = DialogLauncher.ShowGameTrainers(_form, _dialogs, trainers); }
            else if (custom != null) { opened = DialogLauncher.ShowCustomItemDetails(_form, _dialogs, custom); }
            else if (game != null) { opened = DialogLauncher.ShowItemGameCheats(_form, _dialogs, game); }
            else if (patch != null) { opened = DialogLauncher.ShowItemGamePatches(_form, _dialogs, patch); }

            if (opened)
            {
                ReloadActivePage();
            }
            else
            {
                _form.SetStatusText("Details not available for this item.");
            }
            return opened;
        }

        /// <summary>
        /// Download, Install and Uninstall buttons of a library page. install: true install, false uninstall, null download.
        /// A custom mod and a package go straight to the transfer dialog; a mod with exactly one file downloads it; every
        /// other case opens the details dialog, where the user picks the file.
        /// </summary>
        private void OnLibraryTransfer(LibraryRowInfo row, bool? install)
        {
            object tag = row == null ? null : row.Tag;

            CustomItemData custom = tag as CustomItemData;
            if (custom != null && install.HasValue)
            {
                DialogLauncher.ShowTransferCustom(_form, _dialogs, install.Value ? TransferType.InstallCustom : TransferType.UninstallCustom, custom);
                ReloadActivePage();
                return;
            }

            PackageItemData package = tag as PackageItemData;
            if (package != null)
            {
                TransferType type = !install.HasValue ? TransferType.DownloadPackage : (install.Value ? TransferType.InstallPackage : TransferType.UninstallPackage);
                DialogLauncher.ShowTransferPackage(_form, _dialogs, type, package);
                ReloadActivePage();
                return;
            }

            ModItemData mod = tag as ModItemData;
            if (mod != null && !install.HasValue && mod.DownloadFiles != null && mod.DownloadFiles.Count == 1)
            {
                CategoryItem category = _dialogs.Categories == null ? null : mod.GetCategory(_dialogs.Categories);
                DialogLauncher.ShowTransferMod(_form, _dialogs, TransferType.DownloadMods, category, mod, mod.DownloadFiles[0]);
                ReloadActivePage();
                return;
            }

            OpenDetails(tag);
        }

        // ====================================================================================================
        // Helpers
        // ====================================================================================================

        private void OpenExternal(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _form.SetStatusText("Could not open " + target + ": " + ex.Message);
            }
        }

        private static string SafeDirectoryName(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Runs an action on the UI thread. Dropped when the form is gone or its handle does not exist yet on another thread.</summary>
        private void Post(Action action)
        {
            Form form = _form;
            if (form == null || form.IsDisposed)
            {
                return;
            }
            if (!form.InvokeRequired)
            {
                action();
                return;
            }
            if (!form.IsHandleCreated)
            {
                return;
            }
            try
            {
                form.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The form closed between the check and the call.
            }
        }

        /// <summary>
        /// Observes an async task started from a synchronous event handler: an exception goes to AppHost.SetStatus
        /// instead of the unobserved-task void.
        /// </summary>
        private static async void RunSafe(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                AppHost.SetStatus("Command failed: " + ex.Message, ex);
            }
        }
    }
}
