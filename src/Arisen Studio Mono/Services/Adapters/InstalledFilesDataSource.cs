using System;
using System.Collections.Generic;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IInstalledFilesDataSource"/> over <c>ConsoleProfile.InstalledMods</c> of every profile.
    /// Real. It reads and edits the settings only; "forget" removes the record, never a file on the console.
    /// An installed mod that is not in the catalog (or a custom mod that was deleted) is still listed.
    /// No network and no console access.
    /// </summary>
    public sealed class InstalledFilesDataSource : IInstalledFilesDataSource
    {
        private readonly AdapterContext _context;

        public InstalledFilesDataSource(AdapterContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _context = context;
        }

        public IList<InstalledRowInfo> GetInstalled()
        {
            List<InstalledRowInfo> rows = new List<InstalledRowInfo>();
            SettingsData settings = _context.Settings;

            if (settings.ConsoleProfiles == null)
            {
                return rows;
            }

            foreach (ConsoleProfile profile in settings.ConsoleProfiles)
            {
                if (profile == null || profile.InstalledMods == null)
                {
                    continue;
                }

                foreach (InstalledModInfo installed in profile.InstalledMods)
                {
                    if (installed != null)
                    {
                        rows.Add(BuildRow(settings, profile, installed));
                    }
                }
            }

            return rows;
        }

        private InstalledRowInfo BuildRow(SettingsData settings, ConsoleProfile profile, InstalledModInfo installed)
        {
            string name = installed.Name;

            if (string.IsNullOrEmpty(name))
            {
                name = installed.DownloadFiles != null ? installed.DownloadFiles.Name : null;
            }

            if (string.IsNullOrEmpty(name))
            {
                if (installed.IsCustom)
                {
                    CustomItemData custom = settings.GetCustomMod(installed.ModId);
                    name = custom != null ? custom.Name : null;
                }
                else
                {
                    ModItemData mod = _context.Catalog.FindMod(installed.Platform, installed.ModId, installed.CategoryId);
                    name = mod != null ? mod.Name : null;
                }
            }

            return new InstalledRowInfo
            {
                Key = profile.Id + "|" + installed.CategoryId + "|" + installed.ModId + "|" + (installed.IsCustom ? "custom" : "mod"),
                ProfileId = profile.Id.HasValue ? profile.Id.Value : 0,
                ProfileName = profile.Name,
                PlatformText = AdapterMap.PlatformName(installed.Platform),
                CategoryId = installed.CategoryId,
                CategoryTitle = _context.Catalog.CategoryTitle(installed.CategoryId),
                ModId = installed.ModId,
                IsCustom = installed.IsCustom,
                Name = name ?? string.Empty,
                TotalFiles = installed.TotalFiles,
                DateInstalled = installed.DateInstalled,
                Tag = installed
            };
        }

        public void ForgetInstalled(InstalledRowInfo row)
        {
            if (row == null)
            {
                return;
            }

            SettingsData settings = _context.Settings;

            if (settings.ConsoleProfiles == null)
            {
                return;
            }

            InstalledModInfo target = row.Tag as InstalledModInfo;

            foreach (ConsoleProfile profile in settings.ConsoleProfiles)
            {
                if (profile == null || profile.InstalledMods == null)
                {
                    continue;
                }

                if (target != null && profile.InstalledMods.Remove(target))
                {
                    _context.Save();
                    return;
                }

                // No usable handle: match the profile and the record by the same fields the key is made from.
                if (profile.Id.HasValue && profile.Id.Value == row.ProfileId)
                {
                    int removed = profile.InstalledMods.RemoveAll(delegate (InstalledModInfo x)
                    {
                        return x != null && x.ModId == row.ModId && x.IsCustom == row.IsCustom
                            && AdapterMap.Same(x.CategoryId, row.CategoryId);
                    });

                    if (removed > 0)
                    {
                        _context.Save();
                        return;
                    }
                }
            }
        }
    }
}
