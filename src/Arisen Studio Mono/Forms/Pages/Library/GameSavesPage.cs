namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Game Saves page (slice S5, old PageGameSaves, ledger MW3-072 to MW3-085): saves for the connected platform.
    /// Buttons: Sort, Add to Favorites, Download, Show Details. Filters: game, name, region, version. Columns: game, name, region, version.
    /// Region and version values that hold several entries are split for the lists and the match (regions at spaces and slashes, versions at slashes), as the old page did.
    /// The old grid had no Status column although a row status was written to it on transfer (ledger MW3-131); here there is none, so SetRowStatus does nothing on this page.
    /// The old page had no Show Favorites button (the ledger lists none); none is added.
    /// The data source gets <see cref="LibraryPageBase.Platform"/>. Register under ShellPageKeys.GameSaves.
    /// </summary>
    internal sealed class GameSavesPage : LibraryPageBase
    {
        public GameSavesPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.GameSaves,
                Title = "Game Saves",
                SortButton = true,
                AddFavoriteButton = true,
                DownloadButton = true
            })
        {
        }

        protected override LibraryLayout CreateLayout(LibraryPlatform platform)
        {
            LibraryLayout layout = new LibraryLayout();
            layout.Column(LibraryField.Category, "GAME", "Game")
                .Column(LibraryField.Name, "NAME", "Name")
                .Column(LibraryField.Region, "REGION", "Region")
                .Column(LibraryField.Version, "VERSION", "Version");
            layout.Filter(LibraryField.Category, "GAME", "Game", 200)
                .NameSearch(LibraryField.Name, "NAME", "Name");
            layout.Filter(LibraryField.Region, "REGION", "Region", 90)
                .Filter(LibraryField.Version, "VERSION", "Version", 90);
            layout.SplitRegionAndVersion = true;
            // The old default sort was by game (ledger MW3-072).
            layout.DefaultSort = LibraryField.Category;
            return layout;
        }
    }
}
