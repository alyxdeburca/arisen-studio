using ArisenStudio.Extensions;
using ArisenStudio.Runtime;
using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace ArisenStudio.Services
{
    /// <summary>
    /// FTP operations against a PS3 (FluentFTP). Port of the old static FtpExtensions, without the
    /// DevExpress message boxes, the list dialog and the static MainWindow.FtpClient.
    ///
    /// The client is supplied by the host, so this class never opens a connection itself.
    /// Differences from the original (all deliberate):
    ///  - Instance class; the connection and the "show games from external devices" flag come from the host.
    ///  - DeleteFile / DeleteDirectory / RenameFileOrFolder / DownloadDirectory return a Task and are awaited
    ///    (the original was async void or dropped the Task, so errors were lost).
    ///  - GetUserProfileIdAsync (dialog) is replaced by GetUserProfilesAsync, which returns the list; the caller
    ///    shows the picker. Names are now awaited (the original put a Task's text in the label).
    ///  - GetUsbPathAsync returns null when no USB is found; the caller shows the message.
    ///  - PARAM.SFO is read with <see cref="ParamSfoReader" /> instead of the closed PARAM.SFO.dll.
    ///  - Errors go to AppHost.Logger instead of Program.Log.
    /// </summary>
    public sealed class FtpService
    {
        private readonly Func<AsyncFtpClient> _clientProvider;
        private readonly Func<bool> _includeExternalDevices;

        /// <summary>
        /// Use a fixed client. The external-device flag follows AppHost.Settings.ShowGamesFromExternalDevices.
        /// </summary>
        public FtpService(AsyncFtpClient client)
            : this(() => client, null)
        {
        }

        /// <summary>
        /// Use a client provider (so the host can swap the connection when the profile changes) and an
        /// optional flag provider. When <paramref name="includeExternalDevices" /> is null the flag is read from
        /// AppHost.Settings.ShowGamesFromExternalDevices each time.
        /// </summary>
        public FtpService(Func<AsyncFtpClient> clientProvider, Func<bool> includeExternalDevices)
        {
            if (clientProvider == null)
            {
                throw new ArgumentNullException("clientProvider");
            }

            _clientProvider = clientProvider;
            _includeExternalDevices = includeExternalDevices ?? DefaultIncludeExternal;
        }

        private static bool DefaultIncludeExternal()
        {
            return AppHost.Settings.ShowGamesFromExternalDevices;
        }

        private AsyncFtpClient Client
        {
            get
            {
                AsyncFtpClient client = _clientProvider();

                if (client == null)
                {
                    throw new InvalidOperationException("There is no FTP connection to the console.");
                }

                return client;
            }
        }

        private static void Log(Exception ex, string message)
        {
            Action<Exception, string> logger = AppHost.Logger;

            if (logger != null)
            {
                logger(ex, message);
            }
        }

        /// <summary>
        /// Point AppHost.FtpDownloadFile (used by BackupFilesData) at this service.
        /// </summary>
        public void RegisterWithAppHost()
        {
            AppHost.FtpDownloadFile = DownloadForAppHostAsync;
        }

        /// <summary>
        /// Shape that AppHost.FtpDownloadFile expects: (localPath, installPath).
        /// </summary>
        public Task DownloadForAppHostAsync(string localPath, string installPath)
        {
            return DownloadFileAsync(localPath, installPath);
        }

        /// <summary>
        /// Upload a local file to the console, creating the console folder if needed and overwriting.
        /// </summary>
        public async Task<FtpStatus> UploadFileAsync(string localFile, string consoleFile, IProgress<int> progress = null)
        {
            AsyncFtpClient ftpClient = Client;

            string consolePath = ConsolePaths.GetDirectory(consoleFile);

            if (await DirectoryExistsAsync(consolePath) == false)
            {
                await CreateDirectoryAsync(consolePath);
            }

            await ftpClient.SetWorkingDirectory(consolePath);

            return await ftpClient.UploadFile(localFile, consoleFile, FtpRemoteExists.Overwrite, true, FtpVerify.None, progress: new Progress<FtpProgress>(p =>
            {
                if (progress != null)
                {
                    progress.Report((int)p.Progress);
                }
            }));
        }

        /// <summary>
        /// Download a console file to the computer, overwriting. Throws if the console folder does not exist.
        /// </summary>
        public async Task<FtpStatus> DownloadFileAsync(string localFile, string consoleFile, IProgress<int> progress = null)
        {
            AsyncFtpClient ftpClient = Client;

            string consolePath = ConsolePaths.GetDirectory(consoleFile);

            if (await DirectoryExistsAsync(consolePath) == false)
            {
                throw new Exception("Directory does not exist on FTP server: " + consolePath);
            }

            await ftpClient.SetWorkingDirectory(consolePath);

            return await ftpClient.DownloadFile(localFile, consoleFile, FtpLocalExists.Overwrite, FtpVerify.None, progress: new Progress<FtpProgress>(p =>
            {
                if (progress != null)
                {
                    progress.Report((int)p.Progress);
                }
            }));
        }

        /// <summary>
        /// Delete a file from the console, if it exists.
        /// </summary>
        public async Task DeleteFileAsync(string consoleFile)
        {
            AsyncFtpClient ftpClient = Client;

            if (await ftpClient.FileExists(consoleFile))
            {
                await ftpClient.DeleteFile(consoleFile);
            }
        }

        /// <summary>
        /// Delete a directory from the console, if it exists.
        /// </summary>
        public async Task DeleteDirectoryAsync(string consolePath)
        {
            AsyncFtpClient ftpClient = Client;

            if (await ftpClient.DirectoryExists(consolePath))
            {
                await ftpClient.DeleteDirectory(consolePath);
            }
        }

        /// <summary>
        /// Check whether a directory exists on the console. Returns false (and logs) on any error.
        /// </summary>
        public async Task<bool> DirectoryExistsAsync(string consolePath)
        {
            try
            {
                return await Client.DirectoryExists(consolePath);
            }
            catch (Exception ex)
            {
                Log(ex, "Error checking directory " + consolePath);
                return false;
            }
        }

        /// <summary>
        /// Check whether a console directory has no files or folders in it (links do not count).
        /// </summary>
        public async Task<bool> IsDirectoryEmptyAsync(string consolePath)
        {
            int itemCount = 0;

            foreach (FtpListItem listItem in await Client.GetListing(consolePath))
            {
                if (listItem.Type == FtpObjectType.Directory || listItem.Type == FtpObjectType.File)
                {
                    itemCount++;
                }
            }

            return itemCount == 0;
        }

        /// <summary>
        /// Check whether a file exists on the console.
        /// </summary>
        public async Task<bool> FileExistsAsync(string filePath)
        {
            return await Client.FileExists(filePath);
        }

        /// <summary>
        /// Rename a console file or folder.
        /// </summary>
        public Task RenameFileOrFolderAsync(string filePath, string newFilePath)
        {
            return Client.Rename(filePath, newFilePath);
        }

        /// <summary>
        /// Create a console directory path, including parents. Returns whether it was created.
        /// </summary>
        public async Task<bool> CreateDirectoryAsync(string consolePath)
        {
            return await Client.CreateDirectory(consolePath, true);
        }

        /// <summary>
        /// Download a console directory to <paramref name="localPath" />, overwriting existing files.
        /// </summary>
        public async Task DownloadDirectoryAsync(string consolePath, string localPath)
        {
            await Client.DownloadDirectory(localPath, consolePath, FtpFolderSyncMode.Update, FtpLocalExists.Overwrite);
        }

        /// <summary>
        /// Read the game title from a PARAM.SFO file on the console. Returns "Not Recognized" if the file
        /// parses but has no title or is not a PARAM.SFO. Logs and throws if the file is missing or the
        /// transfer fails.
        /// </summary>
        public async Task<string> GetParamTitleAsync(string paramFilePath)
        {
            AsyncFtpClient ftpClient = Client;

            try
            {
                if (await ftpClient.FileExists(paramFilePath) == false)
                {
                    throw new FileNotFoundException("File doesn't exist: " + paramFilePath);
                }

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await ftpClient.DownloadStream(memoryStream, paramFilePath);

                    ParamSfoReader paramSfo = ParamSfoReader.TryParse(memoryStream.ToArray());

                    if (paramSfo == null || string.IsNullOrEmpty(paramSfo.Title))
                    {
                        return "Not Recognized";
                    }

                    return paramSfo.Title;
                }
            }
            catch (WebException ex)
            {
                Log(ex, "Unable to fetch game title from PARAM.SFO file: " + paramFilePath);
                throw new Exception("Unable to fetch game title from PARAM.SFO file: " + paramFilePath + " - Error: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                Log(ex, "Unable to fetch game title from PARAM.SFO file: " + paramFilePath);
                throw new Exception("Unable to fetch game title from PARAM.SFO file: " + paramFilePath + " - Error: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Folders directly inside a console path. Empty list if the path does not exist.
        /// </summary>
        public async Task<List<ListItem>> GetFolderNamesAsync(string consolePath)
        {
            AsyncFtpClient ftpClient = Client;

            List<ListItem> folderNames = new List<ListItem>();

            if (await ftpClient.DirectoryExists(consolePath) == false)
            {
                return folderNames;
            }

            await ftpClient.SetWorkingDirectory(consolePath);

            foreach (FtpListItem listItem in await ftpClient.GetListing(consolePath))
            {
                if (listItem.Type == FtpObjectType.Directory)
                {
                    folderNames.Add(new ListItem { Value = listItem.FullName, Name = listItem.Name });
                }
            }

            return folderNames;
        }

        /// <summary>
        /// Files inside a console path, optionally including subfolders. Empty list if the path does not exist.
        /// </summary>
        public async Task<List<ListItem>> GetFileNamesAsync(string consolePath, bool recursive = false)
        {
            AsyncFtpClient ftpClient = Client;

            List<ListItem> fileNames = new List<ListItem>();

            if (await ftpClient.DirectoryExists(consolePath) == false)
            {
                return fileNames;
            }

            await ftpClient.SetWorkingDirectory(consolePath);

            foreach (FtpListItem listItem in await ftpClient.GetListing(consolePath, recursive ? FtpListOption.Recursive : FtpListOption.AllFiles))
            {
                if (listItem.Type == FtpObjectType.File)
                {
                    fileNames.Add(new ListItem { Value = listItem.FullName, Name = listItem.Name });
                }
            }

            return fileNames;
        }

        /// <summary>
        /// The console user profiles under /dev_hdd0/home/. Value is the user id; Name is "id (user name)".
        /// The caller shows the picker (the old code showed a dialog here).
        /// </summary>
        public async Task<List<ListItem>> GetUserProfilesAsync()
        {
            List<ListItem> userIds = await GetFolderNamesAsync(ConsolePaths.HomeFolder);

            List<ListItem> userNames = new List<ListItem>();

            foreach (ListItem userId in userIds)
            {
                string userName = await GetUserNameFromUserIdAsync(userId.Name);

                userNames.Add(new ListItem
                {
                    Value = userId.Name,
                    Name = userId.Name + " (" + userName + ")"
                });
            }

            return userNames;
        }

        /// <summary>
        /// The profile name for a user id, or "No Username" if the console has no name file for it.
        /// </summary>
        public async Task<string> GetUserNameFromUserIdAsync(string userId)
        {
            AsyncFtpClient ftpClient = Client;

            string usernameFile = ConsolePaths.LocalUserNameFile(userId);

            if (await ftpClient.FileExists(usernameFile) == false)
            {
                return "No Username";
            }

            using (Stream stream = await ftpClient.OpenRead(usernameFile))
            using (StreamReader streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }

        /// <summary>
        /// The first USB port (for example "dev_usb000") that is present on the console, or null if none.
        /// The caller shows the "no USB device" message (the old code showed a message box here).
        /// </summary>
        public async Task<string> GetUsbPathAsync()
        {
            List<ListItem> folderNames = await GetFolderNamesAsync("/");

            List<string> names = new List<string>();
            foreach (ListItem folder in folderNames)
            {
                names.Add(folder.Name);
            }

            return ConsolePaths.FindUsbPort(names);
        }

        /// <summary>
        /// Games (folders) from the internal drive, and the USB ports if external devices are enabled:
        /// GAMES and GAMEZ.
        /// </summary>
        public async Task<List<ListItem>> GetGamesBdAsync()
        {
            List<ListItem> games = new List<ListItem>();

            foreach (string root in ConsolePaths.DeviceRoots(_includeExternalDevices()))
            {
                games.AddRange(await GetFolderNamesAsync(ConsolePaths.Library(root, ConsolePaths.GamesBd)));
                games.AddRange(await GetFolderNamesAsync(ConsolePaths.Library(root, ConsolePaths.GamesBdAlt)));
            }

            return games;
        }

        /// <summary>
        /// PS3 ISO games (files) from PS3ISO on the internal drive and, if enabled, the USB ports.
        /// </summary>
        public async Task<List<ListItem>> GetGamesIsoAsync()
        {
            List<ListItem> games = new List<ListItem>();

            foreach (string root in ConsolePaths.DeviceRoots(_includeExternalDevices()))
            {
                games.AddRange(await GetFileNamesAsync(ConsolePaths.Library(root, ConsolePaths.GamesIso)));
            }

            return games;
        }

        /// <summary>
        /// PSN games (folders) from GAMEI on the internal drive and, if enabled, the USB ports.
        /// </summary>
        public async Task<List<ListItem>> GetGamesPsnAsync()
        {
            List<ListItem> games = new List<ListItem>();

            foreach (string root in ConsolePaths.DeviceRoots(_includeExternalDevices()))
            {
                games.AddRange(await GetFolderNamesAsync(ConsolePaths.Library(root, ConsolePaths.GamesPsn)));
            }

            return games;
        }

        /// <summary>
        /// PS2 games from PS2ISO. Kept as the original: files on the internal drive, folders on the USB ports
        /// (probably an upstream inconsistency, left as is so behaviour matches).
        /// </summary>
        public async Task<List<ListItem>> GetGamesPs2Async()
        {
            List<ListItem> games = new List<ListItem>();

            foreach (string root in ConsolePaths.DeviceRoots(_includeExternalDevices()))
            {
                string path = ConsolePaths.Library(root, ConsolePaths.GamesPs2Iso);

                if (string.IsNullOrEmpty(root))
                {
                    games.AddRange(await GetFileNamesAsync(path));
                }
                else
                {
                    games.AddRange(await GetFolderNamesAsync(path));
                }
            }

            return games;
        }

        /// <summary>
        /// PS1/PSX games: PSXISO plus the .bin files inside each PSXGAMES folder (name shown without ".bin").
        /// Same inconsistency as the original: PSXISO is read as files on the internal drive and folders on USB.
        /// </summary>
        public async Task<List<ListItem>> GetGamesPsxAsync()
        {
            List<ListItem> games = new List<ListItem>();

            foreach (string root in ConsolePaths.DeviceRoots(_includeExternalDevices()))
            {
                string isoPath = ConsolePaths.Library(root, ConsolePaths.GamesPsxIso);

                if (string.IsNullOrEmpty(root))
                {
                    games.AddRange(await GetFileNamesAsync(isoPath));
                }
                else
                {
                    games.AddRange(await GetFolderNamesAsync(isoPath));
                }

                string gamesPath = ConsolePaths.Library(root, ConsolePaths.GamesPsxFolders);

                foreach (ListItem folder in await GetFolderNamesAsync(gamesPath))
                {
                    foreach (ListItem item in await GetFileNamesAsync(gamesPath + folder.Name))
                    {
                        if (item.Name.EndsWith(".bin"))
                        {
                            item.Name = item.Name.Replace(".bin", "");
                            games.Add(item);
                        }
                    }
                }
            }

            return games;
        }
    }
}
