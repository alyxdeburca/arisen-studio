namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Game Mods page (slice S5, old PageGameModsPS3 and PageGameModsXbox, ledger MW2-084 to MW2-100 and MW3-010 to MW3-024).
    /// Buttons: Show Favorites, Sort, Add to Favorites, Download, Show Details. The columns and filters follow <see cref="LibraryPageBase.Platform"/>:
    /// PS3 has game, name, system type, mod type, region, version, status; Xbox 360 has game, name, version, status.
    /// For PS4 the old navigation item opened the PS4 app list (ledger MWD-134), so PS4 gets the app layout: category, name, version, firmware, status.
    /// Register under ShellPageKeys.GameMods. Download and Show Details are events the shell answers (transfer dialog, details dialog).
    /// </summary>
    internal sealed class GameModsPage : LibraryPageBase
    {
        public GameModsPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.GameMods,
                Title = "Game Mods",
                FavoritesToggle = true,
                SortButton = true,
                AddFavoriteButton = true,
                DownloadButton = true
            })
        {
        }

        protected override LibraryLayout CreateLayout(LibraryPlatform platform)
        {
            LibraryLayout layout = new LibraryLayout();
            switch (platform)
            {
                case LibraryPlatform.Xbox360:
                    layout.Column(LibraryField.Category, "GAME", "Game")
                        .Column(LibraryField.Name, "NAME", "Name")
                        .Column(LibraryField.Version, "VERSION", "Version")
                        .Column(LibraryField.Status, "STATUS", "Status");
                    layout.Filter(LibraryField.Category, "GAME", "Game", 200)
                        .NameSearch(LibraryField.Name, "NAME", "Name");
                    layout.Filter(LibraryField.Version, "VERSION", "Version", 90)
                        .StatusFilter();
                    break;
                case LibraryPlatform.PS4:
                    layout.Column(LibraryField.Category, "CATEGORY", "Category")
                        .Column(LibraryField.Name, "NAME", "Name")
                        .Column(LibraryField.Version, "VERSION", "Version")
                        .Column(LibraryField.Firmware, "FIRMWARE_VERSION", "FW Version")
                        .Column(LibraryField.Status, "STATUS", "Status");
                    layout.Filter(LibraryField.Category, "CATEGORY", "Category", 200)
                        .NameSearch(LibraryField.Name, "NAME", "Name");
                    layout.Filter(LibraryField.Firmware, "FIRMWARE_VERSION", "FW Version", 90)
                        .Filter(LibraryField.Version, "VERSION", "Version", 90)
                        .StatusFilter();
                    break;
                default:
                    layout.Column(LibraryField.Category, "GAME", "Game")
                        .Column(LibraryField.Name, "NAME", "Name")
                        .Column(LibraryField.SystemType, "SYSTEM_TYPE", "System Type")
                        .Column(LibraryField.ModType, "MOD_TYPE", "Mod Type")
                        .Column(LibraryField.Region, "REGION", "Region")
                        .Column(LibraryField.Version, "VERSION", "Version")
                        .Column(LibraryField.Status, "STATUS", "Status");
                    layout.Filter(LibraryField.Category, "GAME", "Game", 200)
                        .NameSearch(LibraryField.Name, "NAME", "Name");
                    layout.Filter(LibraryField.SystemType, "SYSTEM_TYPE", "System Type", 90)
                        .Filter(LibraryField.ModType, "MOD_TYPE", "Mod Type", 120)
                        .Filter(LibraryField.Region, "REGION", "Region", 90)
                        .Filter(LibraryField.Version, "VERSION", "Version", 90)
                        .StatusFilter();
                    break;
            }
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
