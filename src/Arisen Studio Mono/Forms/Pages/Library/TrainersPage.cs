namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Trainers page (slice S5, old PageTrainersXbox, ledger MW3-099 to MW3-112): Xbox 360 games that have trainers.
    /// Buttons: Sort, Show Trainers. Filters: game, title ID, dashboard type, trainer count. Double-click or Show Trainers raises
    /// <see cref="LibraryPageBase.DetailsRequested"/>; the shell opens the game trainers dialog (GameTrainersDialog).
    /// Row mapping: Name is the game title, TitleId, DashType and Count carry the other columns. The old grid captions did not match its values
    /// (ledger MW3-107: "Region" held the title ID, "Version" the dashboard type, "Cheats" the count); here the captions follow the values.
    /// The old Add to Favorites and Download buttons did nothing (commented out, ledger MW3-100, MW3-101) and are not ported.
    /// Register under ShellPageKeys.Trainers.
    /// </summary>
    internal sealed class TrainersPage : LibraryPageBase
    {
        public TrainersPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.Trainers,
                Title = "Trainers",
                SortButton = true,
                DetailsKey = "SHOW_TRAINERS",
                DetailsFallback = "Show Trainers"
            })
        {
        }

        protected override LibraryLayout CreateLayout(LibraryPlatform platform)
        {
            LibraryLayout layout = new LibraryLayout();
            layout.Column(LibraryField.Name, "GAME", "Game")
                .Column(LibraryField.TitleId, "TITLE_ID", "Title ID")
                .Column(LibraryField.DashType, "DASHBOARD", "Dashboard")
                .Column(LibraryField.Count, "TRAINERS", "Trainers");
            layout.Filter(LibraryField.Name, "GAME", "Game", 220)
                .Filter(LibraryField.TitleId, "TITLE_ID", "Title ID", 100)
                .Filter(LibraryField.DashType, "DASHBOARD", "Dashboard", 100)
                .Filter(LibraryField.Count, "TRAINERS", "Trainers", 70);
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
