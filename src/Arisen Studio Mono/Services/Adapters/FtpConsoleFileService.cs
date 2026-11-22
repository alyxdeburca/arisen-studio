using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Runtime;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IConsoleFileService"/> over <see cref="FtpService"/> for the PS3 Mount pickers.
    /// Only <see cref="ListAsync"/> and <see cref="ExistsAsync"/> do real work; they are what
    /// ShellController.MountGameAsync calls (ExistsAsync on the library folder, then ListAsync on it and a filter
    /// on IsDirectory). Everything else is the honest unavailable result.
    ///
    /// Opens no connection on construction. <see cref="FtpService"/> takes the client from its own provider, and
    /// only when a method runs. Every method catches its own failures: the list and bool methods return an empty
    /// list or false, and the Task methods that the interface gives no result for return a faulted task
    /// (NotSupportedException), which the page shows in its status line.
    /// </summary>
    public sealed class FtpConsoleFileService : IConsoleFileService
    {
        internal const string UnavailableMessage =
            "This FTP console file service only lists the Mount pickers. Download, upload, delete, rename, folder transfer and create folder are not available through it.";

        private readonly FtpService _ftp;

        public FtpConsoleFileService(FtpService ftp)
        {
            if (ftp == null)
            {
                throw new ArgumentNullException("ftp");
            }

            _ftp = ftp;
        }

        /// <summary>
        /// False. This service holds no client, so it cannot see the connection state. The shell checks its own
        /// connection before it uses this service.
        /// </summary>
        public bool IsConnected
        {
            get { return false; }
        }

        public string PathSeparator
        {
            get { return "/"; }
        }

        /// <summary>
        /// Empty. The drive list is not part of the Mount listing and the old app had no drive list, so none is invented.
        /// </summary>
        public IList<string> GetDrives()
        {
            return new List<string>();
        }

        /// <summary>Null. The first folder is chosen by the caller, not remembered here.</summary>
        public string InitialPath
        {
            get { return null; }
        }

        public bool SupportsAddToModules
        {
            get { return false; }
        }

        public bool SupportsFolderTransfer
        {
            get { return false; }
        }

        /// <summary>
        /// Folders and files directly inside <paramref name="path"/>, as FileManagerEntry rows. Folders come from
        /// FtpService.GetFolderNamesAsync and files from FtpService.GetFileNamesAsync (non-recursive). Both give an
        /// empty list when the folder does not exist. Size and Modified are not read here, so they stay 0 and default.
        /// Returns an empty list on any error.
        /// </summary>
        public async Task<IList<FileManagerEntry>> ListAsync(string path)
        {
            List<FileManagerEntry> entries = new List<FileManagerEntry>();

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return entries;
                }

                foreach (ListItem folder in await _ftp.GetFolderNamesAsync(path))
                {
                    entries.Add(new FileManagerEntry { Name = folder.Name, IsDirectory = true });
                }

                foreach (ListItem file in await _ftp.GetFileNamesAsync(path))
                {
                    entries.Add(new FileManagerEntry { Name = file.Name, IsDirectory = false });
                }

                return entries;
            }
            catch (Exception ex)
            {
                Log(ex, "FtpConsoleFileService.ListAsync: " + path);
                return new List<FileManagerEntry>();
            }
        }

        /// <summary>
        /// True when the folder (isDirectory) or file exists on the console. Returns false on any error.
        /// </summary>
        public async Task<bool> ExistsAsync(string consolePath, bool isDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(consolePath))
                {
                    return false;
                }

                return isDirectory
                    ? await _ftp.DirectoryExistsAsync(consolePath)
                    : await _ftp.FileExistsAsync(consolePath);
            }
            catch (Exception ex)
            {
                Log(ex, "FtpConsoleFileService.ExistsAsync: " + consolePath);
                return false;
            }
        }

        public Task DownloadAsync(string consolePath, bool isDirectory, string localFolder)
        {
            return Unavailable();
        }

        public Task UploadAsync(string localPath, bool isDirectory, string consoleFolder)
        {
            return Unavailable();
        }

        public Task DeleteAsync(string consolePath, bool isDirectory)
        {
            return Unavailable();
        }

        public Task RenameAsync(string consolePath, string newName, bool isDirectory)
        {
            return Unavailable();
        }

        public Task CreateFolderAsync(string consolePath)
        {
            return Unavailable();
        }

        private static Task Unavailable()
        {
            return Task.FromException(new NotSupportedException(UnavailableMessage));
        }

        private static void Log(Exception ex, string message)
        {
            Action<Exception, string> logger = AppHost.Logger;

            if (logger != null)
            {
                logger(ex, message);
            }
        }
    }
}
