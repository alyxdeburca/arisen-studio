using System;
using System.Collections.Generic;
using System.Resources;
using System.Threading.Tasks;
using ArisenStudio.Database;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Forms.Tools;
using ArisenStudio.Forms.Tools.PS3;
using ArisenStudio.Forms.Tools.XBOX;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Shell.Wiring
{
    /// <summary>
    /// Everything <see cref="DialogLauncher" /> needs from the host to open a dialog or tool form: the data the old
    /// MainWindow held (categories, connected profile, database client) and the console boundaries the ported forms
    /// expose as properties. Every member is optional. A null or unset member leaves the form on its own default
    /// (an "Unavailable..." stub for the console boundaries, a no-op for the callbacks). UI thread only.
    /// This type is new and lives only in the Wiring folder; no existing file defines it.
    /// </summary>
    internal sealed class DialogContext
    {
        // ---- data the old MainWindow held ----

        /// <summary>Old MainWindow.Database.CategoriesData. Passed to the details dialogs, GameRegions and BackupFilesManager.</summary>
        public CategoriesData Categories { get; set; }

        /// <summary>Old MainWindow.Database (a DatabaseClient). Needed by NewCustomDialog and GameTrainersDialog.</summary>
        public DatabaseClient Database { get; set; }

        /// <summary>Profile of the connected console, or null (old MainWindow.ConsoleProfile). Called every time a form opens.</summary>
        public Func<ConsoleProfile> ConnectedProfile { get; set; }

        /// <summary>True while a console is connected (old MainWindow.IsConsoleConnected). Null means false.</summary>
        public Func<bool> IsConsoleConnected { get; set; }

        /// <summary>The old MainWindow.ResourceLanguage. Null leaves each form on its English fallbacks.</summary>
        public ResourceManager Language { get; set; }

        /// <summary>The backup data shown by the PS3 Backup Files Manager. Null leaves the form's own empty BackupFilesData.</summary>
        public BackupFilesData BackupFiles { get; set; }

        // ---- callbacks ----

        /// <summary>Opens a URL in the system browser. Null: the launcher uses Process.Start with UseShellExecute.</summary>
        public Action<string> OpenUrl { get; set; }

        /// <summary>Saves AppHost.Settings. Called after a favourite changed or a profile or custom mod was edited. Null: nothing is saved.</summary>
        public Action SaveSettings { get; set; }

        /// <summary>Saves the backup file data after the Backup Files Manager changed it (its BackupFilesChanged event). Null: nothing is saved.</summary>
        public Action SaveBackupFiles { get; set; }

        /// <summary>Shows a status line in the shell status strip. Null: the status is dropped.</summary>
        public Action<string> SetStatus { get; set; }

        /// <summary>Asks the shell to start the connection flow (the Cheats dialog "Connect" button). Null: nothing happens.</summary>
        public Action ConnectConsole { get; set; }

        /// <summary>Called by GameCheatsDialog to write one cheat to the console. Null leaves the dialog default.</summary>
        public Func<Cheats, bool> ApplyCheat { get; set; }

        /// <summary>Called by GamePatchesDialog to apply one patch. Null leaves the dialog default.</summary>
        public Func<Patch, bool> ApplyPatch { get; set; }

        /// <summary>Xbox title id to title for the trainer header of TransferDialog (old Database.TitleIdsX360). Null: the header shows the title id.</summary>
        public Func<string, string> TitleIdToTitle { get; set; }

        /// <summary>Xbox scan for the Setup Wizard (ribbon Scan Xbox Consoles). Null: the wizard has no scan.</summary>
        public Func<IEnumerable<ConsoleProfile>> ScanXboxConsoles { get; set; }

        // ---- console boundaries (types from the Tools and Transfer folders) ----

        /// <summary>The install and uninstall code of TransferDialog. Null leaves TransferConsoleOperationsStub.</summary>
        public ITransferConsoleOperations TransferOperations { get; set; }

        /// <summary>Address the PS3 tools connect to (old profile Address). Null or empty: the tool shows its own "no address" message.</summary>
        public Func<string> ConsoleAddress { get; set; }

        public IPs3FileTransfer Ps3FileTransfer { get; set; }
        public IPs3ConsoleControl Ps3ConsoleControl { get; set; }
        public IPs3GameBrowser Ps3GameBrowser { get; set; }

        /// <summary>PS3 GameRegions: does a directory exist on the console. Null leaves the form default.</summary>
        public Func<string, Task<bool>> Ps3DirectoryExists { get; set; }

        /// <summary>PS3 GameUpdatesFinder: uploads (localFile, consoleFile). Null leaves the form default.</summary>
        public Func<string, string, Task> Ps3UploadFile { get; set; }

        /// <summary>Screenshot upload for both Take Screenshot tools. Null leaves UnavailableScreenshotUploader.</summary>
        public IScreenshotUploader ScreenshotUploader { get; set; }

        /// <summary>One object that implements every Xbox boundary (IXboxConsoleAccess). Null leaves UnavailableXboxConsoleAccess.</summary>
        public IXboxConsoleAccess XboxConsole { get; set; }

        /// <summary>STFS package reader for the Xbox Game Save Resigner. Null leaves UnavailableStfsPackageFactory.</summary>
        public IStfsPackageFactory XboxPackageFactory { get; set; }

        // ---- helpers ----

        internal ConsoleProfile Profile()
        {
            return ConnectedProfile == null ? null : ConnectedProfile();
        }

        internal bool Connected()
        {
            return IsConsoleConnected != null && IsConsoleConnected();
        }

        internal string Address()
        {
            return ConsoleAddress == null ? null : ConsoleAddress();
        }

        internal void Status(string text)
        {
            if (SetStatus != null)
            {
                SetStatus(text);
            }
        }

        internal void Save()
        {
            if (SaveSettings != null)
            {
                SaveSettings();
            }
        }
    }
}
