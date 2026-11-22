using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Everything <see cref="Ps3TransferConsoleOperations"/> cannot decide or do by itself. The host sets what it has.
    /// Every member may be left null: the default is then the one written beside it (a clear failure, or the existing
    /// data-layer code), never a guess.
    /// </summary>
    public sealed class Ps3TransferHooks
    {
        /// <summary>
        /// Picks the game region when none is saved for the game. The old code showed a list dialog (CategoryItem.GetGameRegionAsync).
        /// Return null or empty for "nothing chosen".
        /// Null here: with no saved region the install or uninstall stops with "no game region is saved ... use the Game Regions tool".
        /// </summary>
        public Func<CategoryItem, ITransferHost, Task<string>> ChooseGameRegion { get; set; }

        /// <summary>
        /// Picks one console user profile from the list read from the console (Value is the user id).
        /// The old code showed a list dialog. Return the user id, or null or empty for "nothing chosen".
        /// Null here: a console with exactly one profile uses it; with more the install stops and says a picker is needed.
        /// </summary>
        public Func<IList<ListItem>, ITransferHost, Task<string>> ChooseUserProfile { get; set; }

        /// <summary>
        /// True when webMAN is on the connected console, so "Installed ..." popups can be shown on its screen
        /// (the old MainWindow.IsWebManInstalled). Null: no popups. The popup itself is WebManExtensions.NotifyPopup.
        /// </summary>
        public Func<ConsoleProfile, bool> IsWebManInstalled { get; set; }

        /// <summary>
        /// Downloads and extracts the archive of a mod. Arguments: mod, download file, category, progress.
        /// Null: the data layer's own <c>ModItemData.DownloadArchiveFiles</c> (this is the only place an install reaches the
        /// network before the FTP upload; it re-downloads every time, as the data layer does).
        /// </summary>
        public Func<ModItemData, DownloadFiles, CategoryItem, IProgress<int>, Task> ModArchiveDownloader { get; set; }

        /// <summary>Same for a game save. Null: <c>GameSaveItemData.DownloadArchiveFiles</c>.</summary>
        public Func<GameSaveItemData, DownloadFiles, CategoryItem, IProgress<int>, Task> GameSaveArchiveDownloader { get; set; }

        /// <summary>
        /// Downloads one file (the package and its RAP). Arguments: web address, local file path, host.
        /// Null: the existing <c>HttpExtensions.DownloadFileAsync</c>, with the host's progress and cancel token.
        /// Return false (or throw) when the file did not arrive.
        /// </summary>
        public Func<string, string, ITransferHost, Task<bool>> FileDownloader { get; set; }

        /// <summary>
        /// Copies the old install check exactly: the old code cancelled an install when a game folder for the region WAS
        /// found (the message says the opposite). False (default) cancels when no folder is found. See GameRegionPlanner.CheckInstallRegion.
        /// </summary>
        public bool LegacyInvertedRegionFolderCheck { get; set; }
    }
}
