using System;
using ArisenStudio.Forms.Dashboard;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Shell.Wiring;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IShellLookup"/> over the catalog and the settings in <see cref="AdapterContext"/>. Reads
    /// <c>AdapterContext.Catalog</c> and <c>AdapterContext.Settings</c> at lookup time only: no database load, no network,
    /// no I/O in the constructor. An empty catalog or an id it does not hold gives null, which the controller reports as
    /// "not available" (the behaviour before this adapter existed). PS4 apps and PS3/PS4 game saves return null: the
    /// interface can only carry a <see cref="ModItemData"/> or a <see cref="CustomItemData"/>. Not compiled, not wired.
    /// </summary>
    internal sealed class ShellLookupAdapter : IShellLookup
    {
        private readonly AdapterContext _context;

        public ShellLookupAdapter(AdapterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _context = context;
        }

        /// <summary>
        /// Downloads "View details". Old: MainWindow.TileItemDownloadsViewDetails_ItemClick (MainWindow.cs:2088-2100),
        /// which looks the mod up by platform, category and mod id through Database.GetModItem.
        /// </summary>
        public ModItemData FindDownloadedMod(DownloadRowInfo row)
        {
            Platform platform;

            if (row == null || !TryPlatform(row.PlatformText, out platform))
            {
                return null;
            }

            return _context.Catalog.FindMod(platform, row.ModId, row.CategoryId);
        }

        /// <summary>
        /// Installed Files "View details". A custom row gives the <see cref="CustomItemData"/> from the settings (old:
        /// MainWindow.TileItemInstalledFilesViewDetails_ItemClick, MainWindow.cs:2504-2540, Settings.GetCustomMod). A
        /// mod row gives the catalog <see cref="ModItemData"/> (same old handler, Database.GetModItem).
        /// </summary>
        public object FindInstalledItem(InstalledRowInfo row)
        {
            if (row == null)
            {
                return null;
            }

            if (row.IsCustom)
            {
                SettingsData settings = _context.Settings;
                return settings != null ? settings.GetCustomMod(row.ModId) : null;
            }

            Platform platform;

            if (!TryPlatform(row.PlatformText, out platform))
            {
                return null;
            }

            return _context.Catalog.FindMod(platform, row.ModId, row.CategoryId);
        }

        /// <summary>
        /// Dashboard favourite mod tile. Old: MainWindow.FavoriteModsItem_Click (MainWindow.cs:1755-1775), which calls
        /// ShowDetails(platform, category, modId) and so Database.GetModItem.
        /// </summary>
        public ModItemData FindFavoriteMod(FavoriteModInfo mod)
        {
            if (mod == null)
            {
                return null;
            }

            return _context.Catalog.FindMod(AdapterMap.ToPlatform(mod.Platform), mod.ModId, mod.CategoryId);
        }

        // The rows carry the platform as the text AdapterMap.PlatformName gives. Match it exactly against the three
        // known platforms; anything else is not a platform this adapter knows (null, not a default).
        private static bool TryPlatform(string text, out Platform platform)
        {
            Platform[] known = { Platform.PS3, Platform.PS4, Platform.XBOX360 };

            foreach (Platform candidate in known)
            {
                if (string.Equals(AdapterMap.PlatformName(candidate), text, StringComparison.OrdinalIgnoreCase))
                {
                    platform = candidate;
                    return true;
                }
            }

            platform = Platform.PS3;
            return false;
        }
    }
}
