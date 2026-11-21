using System;
using System.Collections.Generic;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Extra
{
    /// <summary>
    /// One row of the saved game regions list, with what the Save All check needs to know about the game.
    /// </summary>
    public sealed class GameRegionRow
    {
        public string GameId { get; set; }

        public string GameTitle { get; set; }

        public string Region { get; set; }

        /// <summary>The regions the game supports (the category's Regions).</summary>
        public string[] SupportedRegions { get; set; }
    }

    /// <summary>
    /// A folder to look for on the console: /dev_hdd0/game/&lt;REGION&gt;, for one game and one of its regions.
    /// </summary>
    public sealed class GameRegionProbe
    {
        public string GameId { get; set; }

        public string Region { get; set; }

        public string ConsolePath { get; set; }
    }

    /// <summary>
    /// Why a region could not be used for an install.
    /// </summary>
    public enum GameRegionFailure
    {
        None,

        /// <summary>No region was chosen or found (NO_GAME_REGION_SELECTED).</summary>
        NoRegionSelected,

        /// <summary>The region is not one the game supports (NOT_SUPPORTED_GAME_REGION).</summary>
        NotSupported,

        /// <summary>The console has no game folder for the region, so the game update is not installed (NO_GAME_REGION_FOLDER_FOUND).</summary>
        NoRegionFolder
    }

    /// <summary>
    /// Outcome of <see cref="GameRegionPlanner.CheckInstallRegion" />.
    /// </summary>
    public sealed class GameRegionDecision
    {
        public bool CanInstall { get; set; }

        public GameRegionFailure Failure { get; set; }

        /// <summary>The region to use. Empty when the install does not need one, or when it failed.</summary>
        public string Region { get; set; }

        /// <summary>True when the region should be saved to the settings (RememberGameRegions was on).</summary>
        public bool RememberRegion { get; set; }
    }

    /// <summary>
    /// The game region rules of the old GameRegions tool, CategoryItem.GetGameRegionAsync and the install
    /// region check in TransferDialog, as plain methods. They take the data they need as arguments, so the
    /// FTP calls, the dialogs and the settings object stay with the caller. No console is needed to check them.
    /// </summary>
    public static class GameRegionPlanner
    {
        public const string RegionFolderRoot = "/dev_hdd0/game/";

        /// <summary>
        /// The console folder that shows a region is installed: /dev_hdd0/game/&lt;region&gt;.
        /// </summary>
        public static string ProbePath(string region)
        {
            return RegionFolderRoot + (region ?? string.Empty);
        }

        /// <summary>
        /// Folders to probe for one game, one per supported region, in the category's order.
        /// </summary>
        public static List<GameRegionProbe> PlanProbes(string gameId, IEnumerable<string> supportedRegions)
        {
            List<GameRegionProbe> probes = new List<GameRegionProbe>();
            if (supportedRegions == null)
            {
                return probes;
            }

            foreach (string region in supportedRegions)
            {
                if (string.IsNullOrEmpty(region))
                {
                    continue;
                }

                probes.Add(new GameRegionProbe { GameId = gameId, Region = region, ConsolePath = ProbePath(region) });
            }

            return probes;
        }

        /// <summary>
        /// Folders to probe for the "Detect from console" button: every region of every game, in the order given
        /// (game by game). The caller checks each path with FTP and passes the ones that exist to
        /// <see cref="ApplyDetected" />.
        /// </summary>
        public static List<GameRegionProbe> PlanDetection(IEnumerable<KeyValuePair<string, string[]>> gameRegions)
        {
            List<GameRegionProbe> probes = new List<GameRegionProbe>();
            if (gameRegions == null)
            {
                return probes;
            }

            foreach (KeyValuePair<string, string[]> game in gameRegions)
            {
                probes.AddRange(PlanProbes(game.Key, game.Value));
            }

            return probes;
        }

        /// <summary>
        /// Save the probes that were found. A game with several hits ends with the last one, as the old loop did
        /// (it called UpdateGameRegion for each hit). The old dialog warned "all saved game regions will be
        /// cleared" but did not clear anything; pass clearFirst to make the warning true. Returns the number of hits applied.
        /// </summary>
        public static int ApplyDetected(List<GameRegion> saved, IEnumerable<GameRegionProbe> found, bool clearFirst)
        {
            if (saved == null)
            {
                throw new ArgumentNullException("saved");
            }

            if (clearFirst)
            {
                saved.Clear();
            }

            int applied = 0;
            if (found == null)
            {
                return applied;
            }

            foreach (GameRegionProbe probe in found)
            {
                if (probe == null || string.IsNullOrEmpty(probe.GameId) || string.IsNullOrEmpty(probe.Region))
                {
                    continue;
                }

                Upsert(saved, probe.GameId, probe.Region);
                applied++;
            }

            return applied;
        }

        /// <summary>
        /// Same rule as SettingsData.UpdateGameRegion: replace the region of a known game id (ignoring case), else add a row.
        /// </summary>
        public static void Upsert(List<GameRegion> saved, string gameId, string region)
        {
            if (saved == null)
            {
                throw new ArgumentNullException("saved");
            }

            int index = saved.FindIndex(x => string.Equals(x.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                saved.Add(new GameRegion { GameId = gameId, Region = region });
            }
            else
            {
                saved[index].Region = region;
            }
        }

        /// <summary>
        /// The region saved for a game, or null. Returns null when remembering is off (the install then asks or detects).
        /// </summary>
        public static string FindRemembered(IEnumerable<GameRegion> saved, string gameId, bool rememberGameRegions)
        {
            if (!rememberGameRegions || saved == null)
            {
                return null;
            }

            foreach (GameRegion entry in saved)
            {
                if (string.Equals(entry.GameId, gameId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Region))
                {
                    return entry.Region;
                }
            }

            return null;
        }

        /// <summary>
        /// True when the region is one of the supported ones (ignoring case). A null or empty region is never supported.
        /// </summary>
        public static bool IsSupported(IEnumerable<string> supportedRegions, string region)
        {
            if (supportedRegions == null || string.IsNullOrEmpty(region))
            {
                return false;
            }

            foreach (string supported in supportedRegions)
            {
                if (string.Equals(supported, region, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The first row whose region is not supported by its game (the Save All check), or null when every row is fine.
        /// The old check was case sensitive; this one ignores case, like the install check.
        /// </summary>
        public static GameRegionRow FindFirstInvalid(IEnumerable<GameRegionRow> rows)
        {
            if (rows == null)
            {
                return null;
            }

            foreach (GameRegionRow row in rows)
            {
                if (row != null && !IsSupported(row.SupportedRegions, row.Region))
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// Text of the "Invalid Region" box for a row from <see cref="FindFirstInvalid" />.
        /// </summary>
        public static string InvalidRegionMessage(GameRegionRow row)
        {
            if (row == null)
            {
                return null;
            }

            return "Game Region: " + row.Region + " is not supported for Game Title: " + row.GameTitle
                + Environment.NewLine + Environment.NewLine
                + "Please change it to one that's supported for this game.";
        }

        /// <summary>
        /// Remove one saved region by row number. False when the row number is outside the list
        /// (the old code called RemoveAt on the grid's focused row and threw when none was focused).
        /// </summary>
        public static bool RemoveAt(List<GameRegion> saved, int index)
        {
            if (saved == null || index < 0 || index >= saved.Count)
            {
                return false;
            }

            saved.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// The install-time region check from TransferDialog, after the region has been found or chosen.
        /// </summary>
        /// <param name="requiresGameRegion">The download files need a region (DownloadFiles.RequiresGameRegion). When false there is nothing to check.</param>
        /// <param name="region">The region from the saved list, auto-detect or the list dialog. Null or empty means none.</param>
        /// <param name="modIsAnyRegion">The mod's IsAnyRegion flag. The support check only runs when it is set, as before.</param>
        /// <param name="supportedRegions">The category's regions.</param>
        /// <param name="consoleGameFolders">Folder names under /dev_hdd0/game/ on the console, or null to skip the folder check.</param>
        /// <param name="rememberGameRegions">Settings.RememberGameRegions.</param>
        /// <param name="legacyInvertedFolderCheck">
        /// The old code cancelled when a folder for the region WAS found, and carried on when none was (the message
        /// says the opposite: "no game region folder found"). False (the default) cancels when none is found.
        /// True copies the old behaviour exactly.
        /// </param>
        public static GameRegionDecision CheckInstallRegion(
            bool requiresGameRegion,
            string region,
            bool modIsAnyRegion,
            IEnumerable<string> supportedRegions,
            IEnumerable<string> consoleGameFolders,
            bool rememberGameRegions,
            bool legacyInvertedFolderCheck)
        {
            GameRegionDecision decision = new GameRegionDecision { CanInstall = true, Region = string.Empty };

            if (!requiresGameRegion)
            {
                return decision;
            }

            if (string.IsNullOrEmpty(region))
            {
                decision.CanInstall = false;
                decision.Failure = GameRegionFailure.NoRegionSelected;
                return decision;
            }

            if (modIsAnyRegion && !IsSupported(supportedRegions, region))
            {
                decision.CanInstall = false;
                decision.Failure = GameRegionFailure.NotSupported;
                return decision;
            }

            if (consoleGameFolders != null)
            {
                bool folderFound = false;
                foreach (string folder in consoleGameFolders)
                {
                    if (folder != null && folder.IndexOf(region, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        folderFound = true;
                        break;
                    }
                }

                bool cancel = legacyInvertedFolderCheck ? folderFound : !folderFound;
                if (cancel)
                {
                    decision.CanInstall = false;
                    decision.Failure = GameRegionFailure.NoRegionFolder;
                    return decision;
                }
            }

            decision.Region = region;
            decision.RememberRegion = rememberGameRegions;
            return decision;
        }
    }
}
