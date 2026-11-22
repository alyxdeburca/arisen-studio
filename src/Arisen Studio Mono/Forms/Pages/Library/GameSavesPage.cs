namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Game Saves page (slice S5, old PageGameSaves, ledger MW3-072 to MW3-085): saves for the connected platform.
    /// Buttons: Sort, Add to Favorites, Download, Show Details. Filters: game, name, region, version. Columns: game, name, region, version, status.
    /// Region and version values that hold several entries are split for the lists and the match (regions at spaces and slashes, versions at slashes), as the old page did.
    /// The old grid's table had no Status column (Id, Game, Name, Region, Version) although the transfer code wrote a "Status" cell (ledger MW3-131).
    /// The Status column is added here as requested and shows the row's status from the data source.
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
                .Column(LibraryField.Version, "VERSION", "Version")
                .Column(LibraryField.Status, "STATUS", "Status");
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
