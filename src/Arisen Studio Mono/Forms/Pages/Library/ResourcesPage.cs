namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Resources page (slice S5, old PageResourcesPS3, ledger MW2-116 to MW2-128): PS3 resources such as themes and tools.
    /// Buttons: Show Favorites, Sort, Add to Favorites, Download, Show Details. Filters: category, name, system type, mod type, version, status (ledger MW2-121 to MW2-126).
    /// Columns: category, name, system type, mod type, version, status. The old list was sorted by name (ledger MW2-128).
    /// Register under ShellPageKeys.Resources. Download and Show Details are events the shell answers.
    /// </summary>
    internal sealed class ResourcesPage : LibraryPageBase
    {
        public ResourcesPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.Resources,
                Title = "Resources",
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
                .Column(LibraryField.SystemType, "SYSTEM_TYPE", "System Type")
                .Column(LibraryField.ModType, "MOD_TYPE", "Mod Type")
                .Column(LibraryField.Version, "VERSION", "Version")
                .Column(LibraryField.Status, "STATUS", "Status");
            layout.Filter(LibraryField.Category, "CATEGORY", "Category", 200)
                .NameSearch(LibraryField.Name, "NAME", "Name");
            layout.Filter(LibraryField.SystemType, "SYSTEM_TYPE", "System Type", 90)
                .Filter(LibraryField.ModType, "MOD_TYPE", "Mod Type", 120)
                .Filter(LibraryField.Version, "VERSION", "Version", 90)
                .StatusFilter();
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
