namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Homebrew page (slice S5, old PageHomebrewPS3, PageHomebrewXbox and PageHomebrewPS4, ledger MW2-101 to MW2-115 and MW3-025 to MW3-055).
    /// Buttons: Show Favorites, Sort, Add to Favorites, Download, Show Details. The columns and filters follow <see cref="LibraryPageBase.Platform"/>:
    /// PS3 has category, name, system type, version, status; Xbox 360 has category, name, version, status; PS4 has category, name, version, firmware, status.
    /// Register under ShellPageKeys.Homebrew. Download and Show Details are events the shell answers (transfer dialog, details dialog).
    /// </summary>
    internal sealed class HomebrewPage : LibraryPageBase
    {
        public HomebrewPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.Homebrew,
                Title = "Homebrew",
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
                    layout.Column(LibraryField.Category, "CATEGORY", "Category")
                        .Column(LibraryField.Name, "NAME", "Name")
                        .Column(LibraryField.Version, "VERSION", "Version")
                        .Column(LibraryField.Status, "STATUS", "Status");
                    layout.Filter(LibraryField.Category, "CATEGORY", "Category", 200)
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
                    layout.Column(LibraryField.Category, "CATEGORY", "Category")
                        .Column(LibraryField.Name, "NAME", "Name")
                        .Column(LibraryField.SystemType, "SYSTEM_TYPE", "System Type")
                        .Column(LibraryField.Version, "VERSION", "Version")
                        .Column(LibraryField.Status, "STATUS", "Status");
                    layout.Filter(LibraryField.Category, "CATEGORY", "Category", 200)
                        .NameSearch(LibraryField.Name, "NAME", "Name");
                    layout.Filter(LibraryField.SystemType, "SYSTEM_TYPE", "System Type", 90)
                        .Filter(LibraryField.Version, "VERSION", "Version", 90)
                        .StatusFilter();
                    break;
            }
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
