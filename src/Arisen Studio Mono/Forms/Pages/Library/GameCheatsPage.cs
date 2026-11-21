namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Game Cheats page (slice S5, old PageGameCheats, ledger MW3-086 to MW3-098): PS3 games that have cheat files.
    /// Buttons: Sort, Show Cheats. Filters: game, region, version. Columns: game, region, version. Double-click or Show Cheats raises
    /// <see cref="LibraryPageBase.DetailsRequested"/>; the shell opens GameCheatsDialog for the row (its Key is the game file, its Tag may hold the game item).
    /// Row mapping: Name is the game title. The old Add to Favorites and Download buttons did nothing (commented out, ledger MW3-087, MW3-088) and are not ported.
    /// The old Show Cheats button tested the wrong selection (ledger MW3-089); here it works from the selected row.
    /// Register under ShellPageKeys.GameCheats.
    /// </summary>
    internal sealed class GameCheatsPage : LibraryPageBase
    {
        public GameCheatsPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.GameCheats,
                Title = "Game Cheats",
                SortButton = true,
                DetailsKey = "SHOW_CHEATS",
                DetailsFallback = "Show Cheats"
            })
        {
        }

        protected override LibraryLayout CreateLayout(LibraryPlatform platform)
        {
            LibraryLayout layout = new LibraryLayout();
            layout.Column(LibraryField.Name, "GAME", "Game")
                .Column(LibraryField.Region, "REGION", "Region")
                .Column(LibraryField.Version, "VERSION", "Version");
            layout.Filter(LibraryField.Name, "GAME", "Game", 220)
                .Filter(LibraryField.Region, "REGION", "Region", 90)
                .Filter(LibraryField.Version, "VERSION", "Version", 90);
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
