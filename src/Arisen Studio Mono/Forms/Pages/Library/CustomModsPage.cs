namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The Custom Mods page (slice S5, old PageCustomMods, ledger MW2-066 to MW2-083): the user's own mods, with Sort, Add, Edit,
    /// Delete, Show Details, Install and Uninstall. Filters: platform, category, name, mod type, version, status (Installed or Not installed).
    /// Register under ShellPageKeys.CustomMods. Add, Edit, Install, Uninstall and Show Details are events the shell answers with the
    /// New Custom Mod dialog and the transfer dialog (the old details call used PS3 for every row, ledger MW2-083; here the row carries its platform).
    /// </summary>
    internal sealed class CustomModsPage : LibraryPageBase
    {
        public CustomModsPage()
            : base(new LibraryPageConfig
            {
                Kind = LibraryKind.CustomMods,
                Title = "Custom Mods",
                SortButton = true,
                CustomModCommands = true
            })
        {
        }

        protected override LibraryLayout CreateLayout(LibraryPlatform platform)
        {
            LibraryLayout layout = new LibraryLayout();
            layout.Column(LibraryField.Platform, "PLATFORM", "Platform")
                .Column(LibraryField.Category, "CATEGORY", "Category")
                .Column(LibraryField.Name, "NAME", "Name")
                .Column(LibraryField.ModType, "MOD_TYPE", "Mod Type")
                .Column(LibraryField.Version, "VERSION", "Version")
                .Column(LibraryField.Status, "STATUS", "Status");
            layout.Filter(LibraryField.Platform, "PLATFORM", "Platform", 100)
                .Filter(LibraryField.Category, "CATEGORY", "Category", 170)
                .NameSearch(LibraryField.Name, "NAME", "Name");
            layout.Filter(LibraryField.ModType, "MOD_TYPE", "Mod Type", 120)
                .Filter(LibraryField.Version, "VERSION", "Version", 90);
            // The old page offered Installed and Not installed only (ledger MW2-078).
            layout.Filters.Add(new LibraryFilterDef
            {
                Field = LibraryField.Status,
                CaptionKey = "STATUS",
                CaptionFallback = "Status",
                Width = 120,
                FixedValues = new[] { LibraryStatus.NotInstalled, LibraryStatus.Installed }
            });
            layout.DefaultSort = LibraryField.Name;
            return layout;
        }
    }
}
