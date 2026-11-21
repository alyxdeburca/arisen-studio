namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Games page (slice S5, old PageGamesPS4, ledger MW3-056 to MW3-071): PS4 game packages.
    /// Buttons: Show Favorites, Sort, Add to Favorites, Download, Show Details. Filters: category, name, firmware, version, status.
    /// Columns: category, name, version, firmware, status. (By the ledger's code reading the old grid put the status text in the firmware column, MW3-067, MW3-068; here each value is in its own column.)
    /// Register under ShellPageKeys.Games. Download and Show Details are events the shell answers (PS4 transfer dialog, app details dialog).
    /// </summary>
    internal sealed class GamesPage : LibraryPageBase
    {
        public GamesPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.Games,
                Title = "Games",
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
            // The old default sort was by category (ledger MW3-057).
            layout.DefaultSort = LibraryField.Category;
            return layout;
        }
    }
}
