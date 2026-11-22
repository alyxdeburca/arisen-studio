namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The PKG Store page (slice S5, old PagePackagesPS3, ledger MW3-001 to MW3-009): PS3 packages (games, demos, DLC, avatars, themes).
    /// Buttons: Sort, Add to Favorites, Download, Show Details. Filters: category, name, region, status, modified date (equal, at least, at most), file size.
    /// Columns: category, name, title ID, modified date, file size, status; the row's Url is its identity (use it as the row Key).
    /// The old file size filter never took effect (ledger MW3-001: its value was never set, so its size test never ran) and the old status filter did
    /// nothing (MW3-003). The size filter is here as the same inert control: the operator button cycles, the number box takes a number, no row changes.
    /// The status filter works here.
    /// The old Add to Favorites handler (ledger MW2-133) had no tile in the old designer, so the old page never showed the button; it is added here as requested.
    /// Register under ShellPageKeys.PkgStore. Download and Show Details are events the shell answers (package transfer, package details dialog).
    /// </summary>
    internal sealed class PkgStorePage : LibraryPageBase
    {
        public PkgStorePage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.Packages,
                Title = "PKG Store",
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
                .Column(LibraryField.TitleId, "TITLE_ID", "Title ID")
                .Column(LibraryField.Modified, "MODIFIED_DATE", "Modified Date")
                .Column(LibraryField.Size, "FILE_SIZE", "File Size")
                .Column(LibraryField.Status, "STATUS", "Status");
            layout.Filter(LibraryField.Category, "CATEGORY", "Category", 130)
                .NameSearch(LibraryField.Name, "NAME", "Name");
            layout.Filter(LibraryField.Region, "REGION", "Region", 90)
                .StatusFilter();
            layout.DateFilter = true;
            layout.SizeFilter = true;
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
