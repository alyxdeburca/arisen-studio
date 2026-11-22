using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Models.Resources;
using FluentFTP;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IConsoleFileService"/> for a PS3 over <see cref="FtpService"/>. Real FTP code; it opens no
    /// connection itself (the host owns the client, see <see cref="Ps3FileTransfer"/>).
    /// Choices made here, since the interface leaves them open:
    ///  - Drives: the PS3 mount points a file manager is useful on (dev_hdd0, dev_hdd1, dev_flash, dev_flash2,
    ///    dev_flash3, dev_bdvd and the four USB ports). A mount that is not there fails on ListAsync with the FTP error.
    ///  - Display names: user folders under /dev_hdd0/home/ show the profile name. Game folders under /dev_hdd0/game/
    ///    show the PARAM.SFO title only when <c>Settings.AutoDetectGameTitles</c> is on, because that reads one file
    ///    per folder from the console. A folder whose title cannot be read keeps its folder name.
    ///  - Folder upload and download move the whole tree and overwrite what is there.
    /// </summary>
    public sealed class Ps3ConsoleFileService : IConsoleFileService
    {
        private const string HomeRoot = "/dev_hdd0/home/";
        private const string GameRoot = "/dev_hdd0/game/";

        private readonly FtpService _ftp;
        private readonly Func<AsyncFtpClient> _clientProvider;
        private readonly AdapterContext _context;

        public Ps3ConsoleFileService(FtpService ftp, Func<AsyncFtpClient> clientProvider, AdapterContext context)
        {
            if (ftp == null)
            {
                throw new ArgumentNullException("ftp");
            }

            if (clientProvider == null)
            {
                throw new ArgumentNullException("clientProvider");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _ftp = ftp;
            _clientProvider = clientProvider;
            _context = context;
        }

        public bool IsConnected
        {
            get
            {
                AsyncFtpClient client = _clientProvider();
                return client != null && client.IsConnected;
            }
        }

        public string PathSeparator
        {
            get { return "/"; }
        }

        public IList<string> GetDrives()
        {
            List<string> drives = new List<string>
            {
                "/dev_hdd0/",
                "/dev_hdd1/",
                "/dev_flash/",
                "/dev_flash2/",
                "/dev_flash3/",
                "/dev_bdvd/"
            };

            foreach (string port in ConsolePaths.UsbPorts)
            {
                drives.Add("/" + port + "/");
            }

            return drives;
        }

        public string InitialPath
        {
            get
            {
                SettingsData settings = _context.Settings;
                return settings.RememberConsolePath ? settings.ConsolePathPS3 : null;
            }
        }

        public bool SupportsAddToModules
        {
            get { return false; }
        }

        public bool SupportsFolderTransfer
        {
            get { return true; }
        }

        public async Task<IList<FileManagerEntry>> ListAsync(string path)
        {
            AsyncFtpClient client = Client();

            List<FileManagerEntry> entries = new List<FileManagerEntry>();

            foreach (FtpListItem item in await client.GetListing(path))
            {
                if (item.Name == "." || item.Name == "..")
                {
                    continue;
                }

                bool isDirectory = item.Type == FtpObjectType.Directory
                    || (item.Type == FtpObjectType.Link && item.LinkObject != null && item.LinkObject.Type == FtpObjectType.Directory);

                if (!isDirectory && item.Type != FtpObjectType.File && item.Type != FtpObjectType.Link)
                {
                    continue;
                }

                entries.Add(new FileManagerEntry
                {
                    Name = item.Name,
                    IsDirectory = isDirectory,
                    Size = isDirectory || item.Size < 0 ? 0 : item.Size,
                    Modified = item.Modified
                });
            }

            await FillDisplayNames(path, entries);
            return entries;
        }

        public async Task DownloadAsync(string consolePath, bool isDirectory, string localFolder)
        {
            string name = NameOf(consolePath);
            string target = Path.Combine(localFolder, name);

            if (isDirectory)
            {
                Directory.CreateDirectory(target);
                await _ftp.DownloadDirectoryAsync(consolePath, target);
                return;
            }

            Directory.CreateDirectory(localFolder);

            if (await _ftp.DownloadFileAsync(target, consolePath) == FtpStatus.Failed)
            {
                throw new IOException("The console file could not be downloaded: " + consolePath);
            }
        }

        public async Task UploadAsync(string localPath, bool isDirectory, string consoleFolder)
        {
            if (isDirectory)
            {
                string name = new DirectoryInfo(localPath).Name;
                await Client().UploadDirectory(localPath, consoleFolder + name, FtpFolderSyncMode.Update, FtpRemoteExists.Overwrite);
                return;
            }

            if (await _ftp.UploadFileAsync(localPath, consoleFolder + Path.GetFileName(localPath)) == FtpStatus.Failed)
            {
                throw new IOException("The file could not be uploaded to the console: " + consoleFolder);
            }
        }

        public Task DeleteAsync(string consolePath, bool isDirectory)
        {
            return isDirectory ? _ftp.DeleteDirectoryAsync(consolePath) : _ftp.DeleteFileAsync(consolePath);
        }

        public Task RenameAsync(string consolePath, string newName, bool isDirectory)
        {
            if (string.IsNullOrEmpty(newName) || newName.IndexOf('/') >= 0)
            {
                throw new ArgumentException("The new name must be a name, not a path.", "newName");
            }

            return _ftp.RenameFileOrFolderAsync(consolePath, ConsolePaths.GetDirectory(consolePath) + newName);
        }

        public Task<bool> ExistsAsync(string consolePath, bool isDirectory)
        {
            return isDirectory ? _ftp.DirectoryExistsAsync(consolePath) : _ftp.FileExistsAsync(consolePath);
        }

        public Task CreateFolderAsync(string consolePath)
        {
            return _ftp.CreateDirectoryAsync(consolePath);
        }

        private AsyncFtpClient Client()
        {
            AsyncFtpClient client = _clientProvider();

            if (client == null)
            {
                throw new InvalidOperationException("There is no FTP connection to the console.");
            }

            return client;
        }

        private static string NameOf(string consolePath)
        {
            string trimmed = (consolePath ?? string.Empty).TrimEnd('/');
            return trimmed.Substring(trimmed.LastIndexOf('/') + 1);
        }

        private async Task FillDisplayNames(string path, List<FileManagerEntry> entries)
        {
            bool isHome = string.Equals(path, HomeRoot, StringComparison.OrdinalIgnoreCase);
            bool isGames = string.Equals(path, GameRoot, StringComparison.OrdinalIgnoreCase) && _context.Settings.AutoDetectGameTitles;

            if (!isHome && !isGames)
            {
                return;
            }

            foreach (FileManagerEntry entry in entries)
            {
                if (!entry.IsDirectory)
                {
                    continue;
                }

                try
                {
                    if (isHome)
                    {
                        string user = await _ftp.GetUserNameFromUserIdAsync(entry.Name);
                        entry.DisplayName = entry.Name + " (" + user + ")";
                    }
                    else
                    {
                        string title = await _ftp.GetParamTitleAsync(path + entry.Name + "/PARAM.SFO");

                        if (!string.IsNullOrEmpty(title) && title != "Not Recognized")
                        {
                            entry.DisplayName = entry.Name + " (" + title + ")";
                        }
                    }
                }
                catch (Exception)
                {
                    // Keep the folder name. FtpService has already logged the reason.
                }
            }
        }
    }

    /// <summary>
    /// <see cref="IConsoleFileService"/> for an Xbox 360. NOT AVAILABLE: Xbox file access needs XDevkit (the M7 boundary,
    /// not decided yet), so this reports "not connected" and every call fails with one clear message.
    /// It lets the file manager open on an Xbox profile and show why nothing is listed, instead of crashing.
    /// </summary>
    public sealed class XboxConsoleFileServiceUnavailable : IConsoleFileService
    {
        internal const string Message =
            "Xbox 360 file access is not available in this build. It needs the Xbox development kit library (XDevkit), which is not part of this port yet.";

        public bool IsConnected
        {
            get { return false; }
        }

        public string PathSeparator
        {
            get { return "\\"; }
        }

        public IList<string> GetDrives()
        {
            return new List<string> { "Hdd:\\", "Usb0:\\", "Usb1:\\" };
        }

        public string InitialPath
        {
            get { return null; }
        }

        public bool SupportsAddToModules
        {
            get { return true; }
        }

        public bool SupportsFolderTransfer
        {
            get { return false; }
        }

        public Task<IList<FileManagerEntry>> ListAsync(string path)
        {
            return Task.FromException<IList<FileManagerEntry>>(new NotSupportedException(Message));
        }

        public Task DownloadAsync(string consolePath, bool isDirectory, string localFolder)
        {
            return Task.FromException(new NotSupportedException(Message));
        }

        public Task UploadAsync(string localPath, bool isDirectory, string consoleFolder)
        {
            return Task.FromException(new NotSupportedException(Message));
        }

        public Task DeleteAsync(string consolePath, bool isDirectory)
        {
            return Task.FromException(new NotSupportedException(Message));
        }

        public Task RenameAsync(string consolePath, string newName, bool isDirectory)
        {
            return Task.FromException(new NotSupportedException(Message));
        }

        public Task<bool> ExistsAsync(string consolePath, bool isDirectory)
        {
            return Task.FromException<bool>(new NotSupportedException(Message));
        }

        public Task CreateFolderAsync(string consolePath)
        {
            return Task.FromException(new NotSupportedException(Message));
        }
    }
}
