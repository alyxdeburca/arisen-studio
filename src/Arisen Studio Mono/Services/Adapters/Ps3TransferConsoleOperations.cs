using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Io;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Services.Extra;
using ArisenStudio.Services.Transfers;
using FluentFTP;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="ITransferConsoleOperations"/> for a PS3 over FTP: install and uninstall of custom mods, mods,
    /// packages and game saves. Port of the PS3 branches of the old TransferDialog, using <see cref="FtpService"/>,
    /// WebManExtensions, <see cref="InstallBackupPlanner"/>, <see cref="ConsolePaths"/>, <see cref="GameRegionPlanner"/>
    /// and the helpers in <c>Services.Transfers</c>. Nothing here opens a connection: the FTP client is the host's.
    ///
    /// NOT AVAILABLE, reported with one status line and no console access:
    ///  - Xbox 360 profiles (needs XDevkit, the M7 decision), PS4 profiles, and PS4 applications / Xbox trainers.
    ///  - The "install to a local USB device" settings (they copy to a computer drive and need the local USB picker, not ported).
    ///  - No console profile connected.
    ///
    /// Differences from the old dialog, all deliberate:
    ///  - Uploads, deletes and backups are awaited (the old code dropped several Tasks, so errors were lost and an
    ///    uninstall could finish before its files were restored).
    ///  - An install that matched no local file fails instead of "succeeding" with 0 files and recording an install.
    ///  - The game folder region check cancels when NO folder is found (see <see cref="Ps3TransferHooks.LegacyInvertedRegionFolderCheck"/>).
    ///  - Folder deletes use the resolved path and refuse a path with an unresolved placeholder or a bare mount point.
    ///  - A game file is backed up from the console only if it exists there; a missing original is reported, not an error.
    ///  - Installed-mod and package records are edited with null-safe code (the old SettingsData helpers throw on null fields).
    /// </summary>
    public sealed class Ps3TransferConsoleOperations : ITransferConsoleOperations
    {
        private const string XboxMessage =
            "Installing to an Xbox 360 is not available in this build. It needs the Xbox development kit library (XDevkit), which is not part of this port yet.";

        private const string Ps4Message =
            "Installing to a PS4 is not part of the PS3 transfer adapter, so nothing was changed on the console.";

        private readonly AdapterContext _context;
        private readonly FtpService _ftp;
        private readonly Ps3TransferHooks _hooks;

        public Ps3TransferConsoleOperations(AdapterContext context, FtpService ftp, Ps3TransferHooks hooks)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (ftp == null)
            {
                throw new ArgumentNullException("ftp");
            }

            _context = context;
            _ftp = ftp;
            _hooks = hooks ?? new Ps3TransferHooks();
        }

        // ------------------------------------------------------------------------------------------------------
        // Session
        // ------------------------------------------------------------------------------------------------------

        private sealed class Session
        {
            public TransferRequest Request;
            public ITransferHost Host;
            public SettingsData Settings;
            public ConsoleProfile Profile;
        }

        private sealed class Resolved
        {
            public string Region = string.Empty;
            public string UserId = string.Empty;
            public string Usb = string.Empty;
        }

        private sealed class BackupOwner
        {
            public Platform Platform;

            /// <summary>The category id stored on the backup record.</summary>
            public string CategoryId;

            /// <summary>The folder name under the backup root (the category id for mods, the custom item id for custom mods).</summary>
            public string FolderId;
        }

        private sealed class UninstallPlan
        {
            public string CategoryId;
            public string CategoryTitle;
            public int ModId;
            public bool IsCustom;
            public string Name;
            public List<string> Templates = new List<string>();
            public DownloadFiles Files;
            public BackupOwner Backup;
        }

        private bool Begin(TransferRequest request, ITransferHost host, out Session session)
        {
            session = null;
            host.Progress.Report(0);
            host.IsSuccessful = false;

            ConsoleProfile profile = request.ConsoleProfile ?? _context.Profile;

            if (profile == null)
            {
                host.UpdateStatus(Text(host, "NO_CONSOLE_PROFILE", "No console is connected. Connect to a console first; nothing was changed."));
                return false;
            }

            if (profile.Platform == Platform.XBOX360)
            {
                host.UpdateStatus(XboxMessage);
                return false;
            }

            if (profile.Platform != Platform.PS3)
            {
                host.UpdateStatus(Ps4Message);
                return false;
            }

            session = new Session
            {
                Request = request,
                Host = host,
                Settings = request.Settings ?? _context.Settings,
                Profile = profile
            };

            return true;
        }

        private static Task Done()
        {
            return Task.FromResult(0);
        }

        // ------------------------------------------------------------------------------------------------------
        // ITransferConsoleOperations
        // ------------------------------------------------------------------------------------------------------

        public async Task InstallMods(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await InstallModsCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "INSTALL_CANCELED", "Install canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_INSTALL_ERROR", "Unable to install files. Error: {0}");
            }
        }

        public async Task UninstallMods(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await UninstallModsCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "UNINSTALL_CANCELED", "Uninstall canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_UNINSTALL_ERROR", "Unable to uninstall files. Error: {0}");
            }
        }

        public async Task InstallCustom(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await InstallCustomCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "INSTALL_CANCELED", "Install canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_INSTALL_ERROR", "Unable to install files. Error: {0}");
            }
        }

        public async Task UninstallCustom(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await UninstallCustomCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "UNINSTALL_CANCELED", "Uninstall canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_UNINSTALL_ERROR", "Unable to uninstall files. Error: {0}");
            }
        }

        public async Task InstallPackage(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await InstallPackageCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "INSTALL_CANCELED", "Install canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_INSTALL_ERROR", "Unable to install files. Error: {0}");
            }
        }

        public async Task UninstallPackage(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await UninstallPackageCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "UNINSTALL_CANCELED", "Uninstall canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILE_UNINSTALL_ERROR", "Unable to uninstall file. Error: {0}");
            }
        }

        public async Task InstallGameSave(TransferRequest request, ITransferHost host)
        {
            Session s;

            if (!Begin(request, host, out s))
            {
                return;
            }

            try
            {
                await InstallGameSaveCore(s);
            }
            catch (OperationCanceledException)
            {
                Canceled(s, "INSTALL_CANCELED", "Install canceled.");
            }
            catch (Exception ex)
            {
                Failed(s, ex, "FILES_INSTALL_ERROR", "Unable to install files. Error: {0}");
            }
        }

        // PS4 applications and Xbox trainers are not PS3 work. They report themselves the same way for every platform.
        public Task InstallApplication(TransferRequest request, ITransferHost host)
        {
            return NotPs3(request, host, "Installing PS4 applications");
        }

        public Task UninstallApplication(TransferRequest request, ITransferHost host)
        {
            return NotPs3(request, host, "Uninstalling PS4 applications");
        }

        public Task InstallTrainer(TransferRequest request, ITransferHost host)
        {
            host.Progress.Report(0);
            host.IsSuccessful = false;
            host.UpdateStatus("Installing Xbox 360 trainers is not available in this build. " + XboxMessage);
            return Done();
        }

        private Task NotPs3(TransferRequest request, ITransferHost host, string what)
        {
            host.Progress.Report(0);
            host.IsSuccessful = false;
            host.UpdateStatus(what + " is not part of the PS3 transfer adapter yet, so nothing was changed on the console.");
            return Done();
        }

        // ------------------------------------------------------------------------------------------------------
        // Mods
        // ------------------------------------------------------------------------------------------------------

        private async Task InstallModsCore(Session s)
        {
            ITransferHost host = s.Host;
            ModItemData mod = s.Request.ModItem;

            if (mod == null)
            {
                throw new InvalidOperationException("No mod was given to install.");
            }

            CategoryItem category = s.Request.Category ?? _context.Catalog.FindCategory(mod.CategoryId);

            if (category == null)
            {
                throw new InvalidOperationException("The category of this mod is not in the loaded database (" + mod.CategoryId + ").");
            }

            host.SetHeader(category.Title, mod.Name);
            host.UpdateStatus(TransferStatusText.PreparingLine(Text(host, "PREPARING_INSTALL", "Preparing to install"), mod.Name, mod.CategoryId));

            DownloadFiles files = PickFiles(s.Request, mod.DownloadFiles);

            if (files == null)
            {
                Canceled(s, "NO_DOWNLOAD_FILES_SELECTED", "No archive file download selected.", "INSTALL_CANCELED", "Install canceled.");
                return;
            }

            RequireInstallPaths(files);

            if (s.Settings.InstallGameModsToUsbDevice)
            {
                host.Progress.Report(0);
                host.UpdateStatus("Installing to a local USB device is not available in this build (it needs the local USB picker, which is not ported). Turn off the 'install game mods to a USB device' setting to install to the console.");
                return;
            }

            host.UpdateStatus(Text(host, "DOWNLOADING_EXTRACTING_ARCHIVE", "Downloading and extracting archive..."));

            Func<ModItemData, DownloadFiles, CategoryItem, IProgress<int>, Task> download = _hooks.ModArchiveDownloader;

            if (download != null)
            {
                await download(mod, files, category, host.Progress);
            }
            else
            {
                await mod.DownloadArchiveFiles(files, category, host.Progress);
            }

            string dataDirectory = ExistingLocalDirectory(mod.DownloadDataDirectory(files, category));

            // Installed already: ask before overwriting.
            if (FindInstalled(s.Profile, mod.CategoryId, mod.Id, false) != null)
            {
                string question = Format(host, "REINSTALL_FILES", "{0} is already installed ({1}). Install it again and overwrite its files?", TransferStatusText.NameWithVersion(mod.Name, mod.Version), category.Title);

                if (host.Ask(question, Text(host, "OVERWRITE_MODS", "Overwrite mods"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                {
                    Canceled(s, "INSTALL_CANCELED", "Install canceled.");
                    return;
                }
            }

            // Another mod of the same game is installed: offer to remove it first.
            InstalledModInfo other = FindOtherInCategory(s.Profile, mod.CategoryId, mod.Id, false);

            if (other != null)
            {
                string otherName = NameOfRecord(s, other);
                string question = Format(host, "ANOTHER_MOD_INSTALLED", "Another mod is installed for this game: {0} ({1}). Uninstall it first?", otherName, category.Title);

                if (host.Ask(question, Text(host, "OVERWRITE_MODS", "Overwrite mods"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    int removed = await UninstallRecordAsync(s, other);

                    if (removed < 0)
                    {
                        return;
                    }

                    host.SetHeader(category.Title, mod.Name);
                }
            }

            if (files.InstallsToRebugFolder)
            {
                string notice = Text(host, "NOTICE_INSTALL_REBUG_FILES", "This mod installs files into the firmware (dev_rebug) folder. Continue?");

                if (host.Ask(notice, Text(host, "IMPORTANT_MESSAGE", "Important"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    Canceled(s, "INSTALL_CANCELED", "Install canceled.");
                    return;
                }
            }

            Resolved resolved = new Resolved();

            if (files.RequiresGameRegion)
            {
                host.UpdateStatus(Text(host, "GETTING_GAME_REGION", "Getting game region..."));

                string region = await FindRegionAsync(s, category, mod.CategoryId);

                IList<ListItem> gameFolders = await _ftp.GetFolderNamesAsync(GameRegionPlanner.RegionFolderRoot);
                List<string> gameFolderNames = new List<string>();

                foreach (ListItem folder in gameFolders)
                {
                    gameFolderNames.Add(folder.Name);
                }

                GameRegionDecision decision = GameRegionPlanner.CheckInstallRegion(
                    true, region, mod.IsAnyRegion, category.Regions, gameFolderNames, s.Settings.RememberGameRegions, _hooks.LegacyInvertedRegionFolderCheck);

                if (!decision.CanInstall)
                {
                    RegionRefused(s, decision.Failure);
                    return;
                }

                resolved.Region = decision.Region;

                if (decision.RememberRegion)
                {
                    GameRegionPlanner.Upsert(s.Settings.GameRegionsPs3, mod.CategoryId, decision.Region);
                    _context.Save();
                }

                host.UpdateStatus(Format(host, "FOUND_GAME_REGION", "Found game region: {0}", resolved.Region));
            }

            if (files.RequiresUserId)
            {
                resolved.UserId = await FindUserIdAsync(s);

                if (string.IsNullOrEmpty(resolved.UserId))
                {
                    Canceled(s, "NO_USER_PROFILE_SELECTED", "No user profile selected.", "INSTALL_CANCELED", "Install canceled.");
                    return;
                }

                host.UpdateStatus(Format(host, "FOUND_USER_PROFILE", "Found user profile: {0}", resolved.UserId));
            }

            if (files.RequiresUsbDevice)
            {
                string ask = Text(host, "INSERT_USB_DEVICE_TO_CONSOLE", "Insert a USB device into the console, then click Yes. Click No to skip the files that go to the USB device.");

                if (host.Ask(ask, Text(host, "INSTALL_FILES_TO_USB_DEVICE", "Install files to USB device"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    host.UpdateStatus(Text(host, "GETTING_USB_DEVICE", "Getting USB device..."));
                    resolved.Usb = await _ftp.GetUsbPathAsync();

                    if (string.IsNullOrEmpty(resolved.Usb))
                    {
                        Canceled(s, "NO_USB_DEVICES_CONNECTED", "No USB devices connected.", "INSTALL_CANCELED", "Install canceled.");
                        return;
                    }

                    host.UpdateStatus(Format(host, "FOUND_USB_DEVICE", "Found USB device: {0}", resolved.Usb));
                }
            }

            host.UpdateStatus(Text(host, "STARTING_INSTALL", "Starting install..."));

            BackupOwner owner = new BackupOwner { Platform = mod.GetPlatform(), CategoryId = mod.CategoryId, FolderId = mod.CategoryId };
            int count = await UploadFilesAsync(s, files.InstallPaths, dataDirectory, resolved, owner);

            if (count == 0)
            {
                throw new InvalidOperationException("None of the downloaded files matched the install paths of this mod, so nothing was installed.");
            }

            host.Progress.Report(90);

            if (s.Settings.CleanUpLocalFilesAfterInstalling)
            {
                host.UpdateStatus(Text(host, "CLEANING_INSTALL_FILES", "Cleaning up install files..."));
                CleanUp(dataDirectory, mod.ArchiveZipFile(files, category));
                host.Progress.Report(95);
            }

            UpdateInstalled(s.Settings, s.Profile, category.Id, category.CategoryType, mod.Id, mod.Name, count, files, false);
            _context.Save();

            host.Progress.Report(100);
            host.IsSuccessful = true;

            await PopupAsync(s, TransferStatusText.InstalledPopup(category.Title, files.Name, count));

            string done = Format(host, "FILES_INSTALL_SUCCESS", "Successfully installed {0} files.", count);

            if (category.CategoryType == CategoryType.Game)
            {
                done = TransferStatusText.WithSuffix(done, Text(host, "READY_TO_START_GAME", "Ready to start the game."));
            }

            host.UpdateStatus(done);
        }

        private async Task UninstallModsCore(Session s)
        {
            ITransferHost host = s.Host;
            ModItemData mod = s.Request.ModItem;

            if (mod == null)
            {
                throw new InvalidOperationException("No mod was given to uninstall.");
            }

            CategoryItem category = s.Request.Category ?? _context.Catalog.FindCategory(mod.CategoryId);

            if (category == null)
            {
                throw new InvalidOperationException("The category of this mod is not in the loaded database (" + mod.CategoryId + ").");
            }

            host.SetHeader(category.Title, mod.Name);
            host.UpdateStatus(Text(host, "PREPARING_UNINSTALL", "Preparing to uninstall..."));
            host.UpdateStatus(Text(host, "GETTING_PREVIOUS_INSTALL", "Getting previous install details..."));

            InstalledModInfo installed = FindInstalled(s.Profile, mod.CategoryId, mod.Id, false);
            DownloadFiles files;

            if (installed != null && installed.DownloadFiles != null)
            {
                files = installed.DownloadFiles;
                host.UpdateStatus(Text(host, "FOUND_PREVIOUS_INSTALL", "Found the previous install."));
            }
            else
            {
                host.UpdateStatus(Text(host, "NO_PREVIOUS_INSTALL_FOUND", "No previous install was found.") + " " + Text(host, "GETTING_DOWNLOAD_FILES", "Getting download files..."));
                files = PickFiles(s.Request, mod.DownloadFiles);
            }

            if (files == null)
            {
                Canceled(s, "NO_DOWNLOAD_FILES_SELECTED", "No archive file download selected.", "UNINSTALL_CANCELED", "Uninstall canceled.");
                return;
            }

            RequireInstallPaths(files);

            UninstallPlan plan = new UninstallPlan
            {
                CategoryId = mod.CategoryId,
                CategoryTitle = category.Title,
                ModId = mod.Id,
                IsCustom = false,
                Name = files.Name,
                Files = files,
                Backup = new BackupOwner { Platform = mod.GetPlatform(), CategoryId = mod.CategoryId, FolderId = mod.CategoryId }
            };
            plan.Templates.AddRange(files.InstallPaths);

            int removed = await RunUninstallPlanAsync(s, plan, category, s.Request.GameRegion);

            if (removed >= 0)
            {
                host.Progress.Report(100);
                host.IsSuccessful = true;
                host.UpdateStatus(Format(host, "FILES_UNINSTALL_SUCCESS", "Successfully uninstalled {0} files.", removed));
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Custom mods
        // ------------------------------------------------------------------------------------------------------

        private async Task InstallCustomCore(Session s)
        {
            ITransferHost host = s.Host;
            CustomItemData custom = s.Request.CustomMod;

            if (custom == null)
            {
                throw new InvalidOperationException("No custom mod was given to install.");
            }

            CategoryItem category = s.Request.Category ?? _context.Catalog.FindCategory(custom.Category);
            string categoryTitle = category != null ? category.Title : custom.Category;
            CategoryType categoryType = category != null ? category.CategoryType : custom.CategoryType;

            host.SetHeader(categoryTitle, custom.Name);
            host.UpdateStatus(Text(host, "PREPARING_INSTALL", "Preparing to install..."));

            List<ListItem> files = custom.Files ?? new List<ListItem>();

            if (files.Count == 0)
            {
                throw new InvalidOperationException("This custom mod has no files.");
            }

            host.UpdateStatus(Text(host, "CHECKING_FILES", "Checking files..."));

            foreach (ListItem file in files)
            {
                // Name is the local file, Value is the console path (as NewCustomDialog stores them).
                if (string.IsNullOrEmpty(file.Name) || !File.Exists(file.Name))
                {
                    host.Progress.Report(0);
                    string missing = Format(host, "FILES_INSTALL_ERROR", "Unable to install files. Error: {0}", Text(host, "FILE_UPLOAD_NOT_FOUND", "A local file was not found: " + file.Name));
                    host.Ask(missing, Text(host, "ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    host.UpdateStatus(Text(host, "INSTALL_CANCELED", "Install canceled."));
                    return;
                }

                if (string.IsNullOrEmpty(file.Value))
                {
                    throw new InvalidOperationException("A file of this custom mod has no console path: " + file.Name);
                }
            }

            if (FindInstalled(s.Profile, custom.Category, custom.Id, true) != null)
            {
                string question = Format(host, "REINSTALL_FILES", "{0} is already installed ({1}). Install it again and overwrite its files?", TransferStatusText.NameWithVersion(custom.Name, custom.Version), categoryTitle);

                if (host.Ask(question, Text(host, "OVERWRITE_MODS", "Overwrite mods"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                {
                    Canceled(s, "INSTALL_CANCELED", "Install canceled.");
                    return;
                }
            }

            InstalledModInfo other = FindOtherInCategory(s.Profile, custom.Category, custom.Id, true);

            if (other != null)
            {
                string question = Format(host, "ANOTHER_MOD_INSTALLED", "Another mod is installed for this game: {0} ({1}). Uninstall it first?", NameOfRecord(s, other), categoryTitle);

                if (host.Ask(question, Text(host, "OVERWRITE_MODS", "Overwrite mods"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    if (await UninstallRecordAsync(s, other) < 0)
                    {
                        return;
                    }

                    host.SetHeader(categoryTitle, custom.Name);
                }
            }

            host.UpdateStatus(Text(host, "STARTING_INSTALL", "Starting install..."));

            BackupOwner owner = new BackupOwner { Platform = custom.Platform, CategoryId = custom.Category, FolderId = custom.Id.ToString() };
            BackupDecision decision = new BackupDecision();
            int index = 1;

            foreach (ListItem file in files)
            {
                host.CancellationToken.ThrowIfCancellationRequested();

                string fileName = Path.GetFileName(file.Value);
                string consolePath = file.Value;

                if (consolePath.Contains(InstallBackupPlanner.GameFolderMarker))
                {
                    await BackupOriginalAsync(s, owner, decision, fileName, consolePath);
                }

                host.UpdateStatus(Format(host, "FILE_INSTALL_LOCATION", "Installing file: {0} to {1}", TransferStatusText.FileCounter(fileName, index, files.Count), InstallBackupPlanner.ParentFolder(consolePath)));
                await UploadAsync(s, file.Name, consolePath);
                host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));
                index++;
            }

            UpdateInstalled(s.Settings, s.Profile, custom.Category, categoryType, custom.Id, custom.Name, files.Count, null, true);
            _context.Save();

            host.Progress.Report(100);
            host.IsSuccessful = true;

            await PopupAsync(s, TransferStatusText.InstalledPopup(categoryTitle, custom.Name, files.Count));

            string done = Format(host, "FILES_INSTALL_SUCCESS", "Successfully installed {0} files.", files.Count);

            if (categoryType == CategoryType.Game)
            {
                done = TransferStatusText.WithSuffix(done, Text(host, "READY_TO_START_GAME", "Ready to start the game."));
            }

            host.UpdateStatus(done);
        }

        private async Task UninstallCustomCore(Session s)
        {
            ITransferHost host = s.Host;
            CustomItemData custom = s.Request.CustomMod;

            if (custom == null)
            {
                throw new InvalidOperationException("No custom mod was given to uninstall.");
            }

            CategoryItem category = s.Request.Category ?? _context.Catalog.FindCategory(custom.Category);

            host.SetHeader(category != null ? category.Title : custom.Category, custom.Name);
            host.UpdateStatus(Text(host, "PREPARING_UNINSTALL", "Preparing to uninstall..."));

            UninstallPlan plan = PlanForCustom(custom, category != null ? category.Title : custom.Category);

            int removed = await RunUninstallPlanAsync(s, plan, category, null);

            if (removed >= 0)
            {
                host.Progress.Report(100);
                host.IsSuccessful = true;
                host.UpdateStatus(Format(host, "FILES_UNINSTALL_SUCCESS", "Successfully uninstalled {0} files.", removed));
            }
        }

        private static UninstallPlan PlanForCustom(CustomItemData custom, string categoryTitle)
        {
            UninstallPlan plan = new UninstallPlan
            {
                CategoryId = custom.Category,
                CategoryTitle = categoryTitle,
                ModId = custom.Id,
                IsCustom = true,
                Name = custom.Name,
                Backup = new BackupOwner { Platform = custom.Platform, CategoryId = custom.Category, FolderId = custom.Id.ToString() }
            };

            if (custom.Files != null)
            {
                foreach (ListItem file in custom.Files)
                {
                    if (file != null && !string.IsNullOrEmpty(file.Value))
                    {
                        plan.Templates.Add(file.Value);
                    }
                }
            }

            return plan;
        }

        // ------------------------------------------------------------------------------------------------------
        // Packages
        // ------------------------------------------------------------------------------------------------------

        private async Task InstallPackageCore(Session s)
        {
            ITransferHost host = s.Host;
            PackageItemData package = s.Request.PackageItem;

            if (package == null)
            {
                throw new InvalidOperationException("No package was given to install.");
            }

            host.SetHeader(package.Category, package.Name);
            host.UpdateStatus(Text(host, "PREPARING_INSTALL", "Preparing to install..."));

            if (string.IsNullOrEmpty(package.Url) || package.IsUrlMissing)
            {
                throw new InvalidOperationException("This package has no download address.");
            }

            if (FindInstalledPackage(s.Settings, package) != null)
            {
                string question = Format(host, "FILE_REINSTALL", "{0} is already installed. Install it again?", TransferStatusText.NameWithCategory(package.Name, package.Category));

                if (host.Ask(question, Text(host, "REINSTALL_FILE", "Reinstall file"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                {
                    Canceled(s, "INSTALL_CANCELED", "Install canceled.");
                    return;
                }
            }

            string cleanName = AdapterMap.CleanName(package.Name);
            string downloadFolder = Path.Combine(s.Settings.PathDownloads.GetFullPath(s.Settings.PathAppData), cleanName);
            Directory.CreateDirectory(downloadFolder);

            string localPkg = Path.Combine(downloadFolder, cleanName + ".pkg");
            string localRap = Path.Combine(downloadFolder, AdapterMap.CleanName(package.ContentId) + ".rap");

            host.UpdateStatus(Format(host, "FILE_DOWNLOAD_LOCATION", "Downloading file: {0} to {1}", package.Name + ".pkg", downloadFolder));
            await DownloadFileAsync(s, package.Url, localPkg);
            host.UpdateStatus(Text(host, "FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));

            bool includeRap = package.IsRapRequired && !package.IsRapMissing;

            if (includeRap)
            {
                host.UpdateStatus(Format(host, "FILE_DOWNLOAD_LOCATION", "Downloading file: {0} to {1}", package.ContentId + ".rap", downloadFolder));
                await DownloadFileAsync(s, package.RapUrl, localRap);
                host.UpdateStatus(Text(host, "FILE_DOWNLOAD_SUCCESS", "Successfully downloaded file."));
            }

            host.UpdateStatus(Text(host, "STARTING_INSTALL", "Starting install..."));

            string consolePkg = package.InstallFilePathPkg;
            host.UpdateStatus(Format(host, "FILE_INSTALL_LOCATION", "Installing file: {0} to {1}", package.Name + ".pkg", InstallBackupPlanner.ParentFolder(consolePkg)));
            await UploadAsync(s, localPkg, consolePkg);
            host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));

            if (includeRap)
            {
                string consoleRap = package.InstallFilePathRap;
                host.UpdateStatus(Format(host, "FILE_INSTALL_LOCATION", "Installing file: {0} to {1}", package.ContentId + ".rap", InstallBackupPlanner.ParentFolder(consoleRap)));
                await UploadAsync(s, localRap, consoleRap);
                host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));
            }

            RemovePackageRecord(s.Settings, package);
            s.Settings.InstalledPackages.Add(new InstalledPackageInfo
            {
                Category = package.Category,
                Name = package.Name,
                TitleId = package.TitleId,
                Region = package.Region,
                Url = package.Url,
                LocalFile = localPkg,
                DateTime = DateTime.Now
            });
            _context.Save();

            if (s.Settings.CleanUpLocalFilesAfterInstalling)
            {
                host.UpdateStatus(Text(host, "CLEANING_INSTALL_FILES", "Cleaning up install files..."));
                CleanUp(downloadFolder, null);
            }

            host.LocalPath = downloadFolder;
            host.Progress.Report(100);
            host.IsSuccessful = true;
            host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));
        }

        private async Task UninstallPackageCore(Session s)
        {
            ITransferHost host = s.Host;
            PackageItemData package = s.Request.PackageItem;

            if (package == null)
            {
                throw new InvalidOperationException("No package was given to uninstall.");
            }

            host.SetHeader(package.Category, package.Name);
            host.UpdateStatus(Text(host, "PREPARING_UNINSTALL", "Preparing to uninstall..."));

            if (FindInstalledPackage(s.Settings, package) == null)
            {
                string question = Format(host, "MOD_NOT_INSTALLED", "{0} is not recorded as installed. Delete it from the console anyway?", TransferStatusText.NameWithCategory(package.Name, package.Category));

                if (host.Ask(question, Text(host, "NOT_INSTALLED", "Not installed"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                {
                    Canceled(s, "UNINSTALL_CANCELED", "Uninstall canceled.");
                    return;
                }
            }

            string consolePkg = package.InstallFilePathPkg;
            host.UpdateStatus(Format(host, "FILE_UNINSTALL_LOCATION", "Uninstalling file: {0} from {1}", package.Name + ".pkg", InstallBackupPlanner.ParentFolder(consolePkg)));
            await _ftp.DeleteFileAsync(consolePkg);
            host.UpdateStatus(Text(host, "FILE_UNINSTALL_SUCCESS", "Successfully uninstalled file."));

            RemovePackageRecord(s.Settings, package);
            _context.Save();

            host.Progress.Report(100);
            host.IsSuccessful = true;
            host.UpdateStatus(Text(host, "FILE_UNINSTALL_SUCCESS", "Successfully uninstalled file."));
        }

        // ------------------------------------------------------------------------------------------------------
        // Game saves
        // ------------------------------------------------------------------------------------------------------

        private async Task InstallGameSaveCore(Session s)
        {
            ITransferHost host = s.Host;
            GameSaveItemData save = s.Request.GameSaveItem;

            if (save == null)
            {
                throw new InvalidOperationException("No game save was given to install.");
            }

            if (save.GetPlatform() != Platform.PS3)
            {
                host.UpdateStatus("This game save is not for a PS3, so nothing was installed.");
                return;
            }

            CategoryItem category = s.Request.Category ?? _context.Catalog.FindCategory(save.CategoryId);

            if (category == null)
            {
                throw new InvalidOperationException("The category of this game save is not in the loaded database (" + save.CategoryId + ").");
            }

            host.SetHeader(category.Title, save.Name);
            host.UpdateStatus(Text(host, "PREPARING_INSTALL", "Preparing to install..."));

            DownloadFiles files = PickFiles(s.Request, save.DownloadFiles);

            if (files == null)
            {
                Canceled(s, "NO_DOWNLOAD_FILES_SELECTED", "No archive file download selected.", "INSTALL_CANCELED", "Install canceled.");
                return;
            }

            RequireInstallPaths(files);

            if (s.Settings.InstallGameSavesToUsbDevice)
            {
                host.UpdateStatus("Installing to a local USB device is not available in this build (it needs the local USB picker, which is not ported). Turn off the 'install game saves to a USB device' setting to install to the console.");
                return;
            }

            host.UpdateStatus(Text(host, "ARCHIVE_DOWNLOADING_EXTRACTING_ARCHIVE", "Downloading and extracting archive..."));

            Func<GameSaveItemData, DownloadFiles, CategoryItem, IProgress<int>, Task> download = _hooks.GameSaveArchiveDownloader;

            if (download != null)
            {
                await download(save, files, category, host.Progress);
            }
            else
            {
                await save.DownloadArchiveFiles(files, category, host.Progress);
            }

            host.UpdateStatus(Text(host, "ARCHIVE_DOWNLOAD_SUCCESS", "Successfully downloaded archive file."));

            string dataDirectory = ExistingLocalDirectory(save.DownloadDataDirectory(files, category));
            Resolved resolved = new Resolved();

            if (files.RequiresUsbDevice)
            {
                string ask = "Before installing this game save you must have completed the following steps:\n- Insert your USB device to any port on the console.\n- Install the Apollo Tool PKG from the Homebrew Packages category on your console to unlock, patch and resign save-game files directly on your PS3.\nThis will only work if you have your USB device connected. Click 'Yes' if you have done this. Otherwise click 'No' and this game-save will not be installed.";

                if (host.Ask(ask, "Installing Game Saves", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    Canceled(s, "INSTALL_CANCELED", "Install canceled.");
                    return;
                }

                host.UpdateStatus(Text(host, "GETTING_USB_DEVICE", "Getting USB device..."));
                resolved.Usb = await _ftp.GetUsbPathAsync();

                if (string.IsNullOrEmpty(resolved.Usb))
                {
                    Canceled(s, "NO_USB_DEVICES_CONNECTED", "No USB devices connected.", "INSTALL_CANCELED", "Install canceled.");
                    return;
                }

                host.UpdateStatus(Format(host, "FOUND_USB_DEVICE", "Found USB device: {0}", resolved.Usb));
            }

            host.UpdateStatus(Text(host, "STARTING_INSTALL", "Starting install..."));

            // Game saves never go to a game folder on the console, so there is no backup step (owner null).
            int count = await UploadFilesAsync(s, files.InstallPaths, dataDirectory, resolved, null);

            if (count == 0)
            {
                throw new InvalidOperationException("None of the downloaded files matched the install paths of this game save, so nothing was installed.");
            }

            if (s.Settings.CleanUpLocalFilesAfterInstalling)
            {
                host.UpdateStatus(Text(host, "CLEANING_INSTALL_FILES", "Cleaning up install files..."));
                CleanUp(dataDirectory, save.ArchiveZipFile(files, category));
            }

            host.Progress.Report(100);
            host.IsSuccessful = true;

            await PopupAsync(s, TransferStatusText.InstalledPopup(category.Title, files.Name, count));
            host.UpdateStatus(Format(host, "FILES_INSTALL_SUCCESS", "Successfully installed {0} files.", count));
        }

        // ------------------------------------------------------------------------------------------------------
        // Upload loop and backups
        // ------------------------------------------------------------------------------------------------------

        private async Task<int> UploadFilesAsync(Session s, IList<string> templates, string localDirectory, Resolved r, BackupOwner owner)
        {
            ITransferHost host = s.Host;
            string[] localFiles = Directory.GetFiles(localDirectory, "*.*", SearchOption.AllDirectories);
            BackupDecision decision = new BackupDecision();
            int index = 1;
            int count = 0;

            foreach (string template in templates)
            {
                foreach (string localFile in localFiles)
                {
                    host.CancellationToken.ThrowIfCancellationRequested();

                    string fileName = Path.GetFileName(localFile);
                    string installPath = InstallBackupPlanner.ResolveInstallPath(template, r.Region, r.UserId, r.Usb);

                    if (!InstallBackupPlanner.LocalFileMatchesInstallPath(installPath, fileName))
                    {
                        continue;
                    }

                    InstallTarget target = InstallBackupPlanner.Classify(template, installPath);

                    // A USB install with no USB device found is skipped, as the old code did.
                    if (!InstallBackupPlanner.CanInstall(target, r.Usb))
                    {
                        continue;
                    }

                    if (target == InstallTarget.GameFolder && owner != null)
                    {
                        await BackupOriginalAsync(s, owner, decision, fileName, installPath);
                    }

                    host.UpdateStatus(Format(host, "FILE_INSTALL_LOCATION", "Installing file: {0} to {1}", TransferStatusText.FileCounter(fileName, index, templates.Count), InstallBackupPlanner.ParentFolder(installPath)));
                    await UploadAsync(s, localFile, installPath);
                    host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));

                    index++;
                    count++;
                }
            }

            return count;
        }

        private async Task UploadAsync(Session s, string localFile, string consolePath)
        {
            FtpStatus status = await _ftp.UploadFileAsync(localFile, consolePath, s.Host.Progress);

            if (status == FtpStatus.Failed)
            {
                throw new IOException("The file could not be uploaded to the console: " + consolePath);
            }
        }

        // Backs up the original game file before it is overwritten (rules: InstallBackupPlanner.PlanBackup).
        private async Task BackupOriginalAsync(Session s, BackupOwner owner, BackupDecision decision, string fileName, string installPath)
        {
            ITransferHost host = s.Host;
            BackupFilesData store = _context.BackupFiles;
            BackupFile record = store != null ? store.GetGameFileBackup(owner.Platform, owner.CategoryId, fileName, installPath) : null;
            bool hasRecord = record != null;

            bool shouldBackup = decision.Evaluate(hasRecord, s.Settings.AlwaysBackupGameFiles, delegate ()
            {
                return host.Ask(Text(host, "BACKUP_GAME_FILES", "Back up the original game files before they are overwritten?"), Text(host, "BACKUP_FILES", "Backup files"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            });

            bool localExists = hasRecord && File.Exists(record.LocalPath);
            BackupStep step = InstallBackupPlanner.PlanBackup(shouldBackup, hasRecord, localExists);

            if (step == BackupStep.None || step == BackupStep.KeepRecord)
            {
                return;
            }

            if (store == null)
            {
                throw new InvalidOperationException("The original game file must be backed up before it is overwritten, but no backup store is connected (AdapterContext.BackupFiles). Nothing was changed on the console.");
            }

            string source = step == BackupStep.Redownload ? record.InstallPath : installPath;

            if (!await _ftp.FileExistsAsync(source))
            {
                host.UpdateStatus("There is no original file on the console to back up: " + fileName);
                return;
            }

            string localPath = InstallBackupPlanner.BackupLocalPath(UserFolders.BackupFiles, owner.FolderId, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));

            host.UpdateStatus(Format(host, "FILE_BACKUP_CREATING", "Creating backup of file: {0}", fileName));

            if (await _ftp.DownloadFileAsync(localPath, source) == FtpStatus.Failed)
            {
                throw new IOException("The original file could not be backed up: " + source);
            }

            if (step == BackupStep.CreateNew)
            {
                store.UpdateBackupFile(null, new BackupFile
                {
                    Platform = owner.Platform,
                    CategoryId = owner.CategoryId,
                    FileName = fileName,
                    LocalPath = localPath,
                    InstallPath = installPath,
                    CreatedDate = DateTime.Now
                });
            }
            else
            {
                record.LocalPath = localPath;
                record.InstallPath = installPath;
                store.UpdateBackupFile(store.BackupFiles.IndexOf(record), record);
            }

            _context.SaveBackups();
            host.UpdateStatus(Text(host, "FILE_BACKUP_CREATED", "Backup created."));
        }

        // ------------------------------------------------------------------------------------------------------
        // Uninstall
        // ------------------------------------------------------------------------------------------------------

        // Uninstalls the mod of an installed-mods record (used to replace "another mod"). Returns the file count, or -1 when canceled.
        private async Task<int> UninstallRecordAsync(Session s, InstalledModInfo record)
        {
            CategoryItem category = _context.Catalog.FindCategory(record.CategoryId);
            string categoryTitle = category != null ? category.Title : record.CategoryId;
            UninstallPlan plan;

            if (record.IsCustom)
            {
                CustomItemData custom = s.Settings.GetCustomMod(record.ModId);

                if (custom == null)
                {
                    throw new InvalidOperationException("The custom mod of the installed record no longer exists. Remove the record from Installed Files, then try again.");
                }

                plan = PlanForCustom(custom, categoryTitle);
            }
            else
            {
                if (record.DownloadFiles == null || record.DownloadFiles.InstallPaths == null)
                {
                    throw new InvalidOperationException("The installed record has no file list, so it cannot be uninstalled from here. Remove the record from Installed Files, then try again.");
                }

                plan = new UninstallPlan
                {
                    CategoryId = record.CategoryId,
                    CategoryTitle = categoryTitle,
                    ModId = record.ModId,
                    IsCustom = false,
                    Name = record.DownloadFiles.Name,
                    Files = record.DownloadFiles,
                    Backup = new BackupOwner { Platform = record.Platform, CategoryId = record.CategoryId, FolderId = record.CategoryId }
                };
                plan.Templates.AddRange(record.DownloadFiles.InstallPaths);
            }

            s.Host.UpdateStatus(Text(s.Host, "PREPARING_UNINSTALL", "Preparing to uninstall..."));
            return await RunUninstallPlanAsync(s, plan, category, null);
        }

        // Does the file work of an uninstall and removes the installed record. Returns the number of files handled, or -1 when canceled.
        private async Task<int> RunUninstallPlanAsync(Session s, UninstallPlan plan, CategoryItem category, string requestedRegion)
        {
            ITransferHost host = s.Host;
            DownloadFiles files = plan.Files;

            if (files != null && files.InstallsToRebugFolder)
            {
                host.Ask(Text(host, "CANT_UNINSTALL_CFW_FILES", "Files in the firmware (dev_rebug) folders cannot be uninstalled."), Text(host, "FORBIDDEN", "Forbidden"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Canceled(s, "UNINSTALL_CANCELED", "Uninstall canceled.");
                return -1;
            }

            Resolved r = new Resolved();
            bool uninstallUsbFiles = false;

            if (files != null && files.RequiresGameRegion)
            {
                string region = requestedRegion;

                if (string.IsNullOrEmpty(region))
                {
                    host.UpdateStatus(Text(host, "GETTING_GAME_REGION", "Getting game region..."));
                    region = await FindRegionAsync(s, category, plan.CategoryId);
                }

                if (string.IsNullOrEmpty(region))
                {
                    Canceled(s, "NO_GAME_REGION_SELECTED", "No game region selected.", "UNINSTALL_CANCELED", "Uninstall canceled.");
                    return -1;
                }

                r.Region = region;
                host.UpdateStatus(Format(host, "FOUND_GAME_REGION", "Found game region: {0}", region));

                if (s.Settings.RememberGameRegions)
                {
                    GameRegionPlanner.Upsert(s.Settings.GameRegionsPs3, plan.CategoryId, region);
                    _context.Save();
                }
            }

            host.Progress.Report(50);

            if (files != null && files.RequiresUserId)
            {
                r.UserId = await FindUserIdAsync(s);

                if (string.IsNullOrEmpty(r.UserId))
                {
                    Canceled(s, "NO_USER_PROFILE_SELECTED", "No user profile selected.", "UNINSTALL_CANCELED", "Uninstall canceled.");
                    return -1;
                }

                host.UpdateStatus(Format(host, "FOUND_USER_PROFILE", "Found user profile: {0}", r.UserId));
            }

            if (files != null && files.RequiresUsbDevice)
            {
                string ask = Text(host, "REQUIRES_PREVIOUS_USB_DEVICE", "The files were installed to a USB device. Insert the same USB device into the console, then click Yes. Click No to leave those files.");

                if (host.Ask(ask, Text(host, "UNINSTALL_USB_FILES", "Uninstall USB files"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    host.UpdateStatus(Text(host, "GETTING_USB_DEVICE", "Getting USB device..."));
                    r.Usb = await _ftp.GetUsbPathAsync();

                    if (string.IsNullOrEmpty(r.Usb))
                    {
                        Canceled(s, "NO_USB_DEVICE_FOUND", "No USB device found.", "UNINSTALL_CANCELED", "Uninstall canceled.");
                        return -1;
                    }

                    host.UpdateStatus(Format(host, "FOUND_USB_DEVICE", "Found USB device: {0}", r.Usb));
                    uninstallUsbFiles = true;
                }
            }

            int index = 1;
            int count = 0;
            int total = plan.Templates.Count;

            // Files first.
            foreach (string template in plan.Templates)
            {
                host.CancellationToken.ThrowIfCancellationRequested();

                string installPath = InstallBackupPlanner.ResolveInstallPath(template, r.Region, r.UserId, r.Usb);

                if (!Path.HasExtension(installPath))
                {
                    continue;
                }

                string name = Path.GetFileName(installPath);
                string parent = InstallBackupPlanner.ParentFolder(installPath);
                InstallTarget target = InstallBackupPlanner.Classify(template, installPath);

                if (target == InstallTarget.GameFolder)
                {
                    // A game file is put back from the backup; there is nothing to delete.
                    BackupFilesData store = _context.BackupFiles;
                    BackupFile backup = store != null ? store.GetGameFileBackup(plan.Backup.Platform, plan.Backup.CategoryId, name, installPath) : null;

                    if (backup != null && File.Exists(backup.LocalPath))
                    {
                        host.UpdateStatus(Format(host, "INSTALLING_BACKUP_FILE_LOCATION", "Restoring backup file: {0} to {1}", TransferStatusText.FileCounter(name, index, total), parent));
                        await UploadAsync(s, backup.LocalPath, installPath);
                        host.UpdateStatus(Text(host, "FILE_INSTALL_SUCCESS", "Successfully installed file."));
                        index++;
                        count++;
                    }
                    else if (backup != null)
                    {
                        host.Ask(Format(host, "CANT_FIND_BACKUP_FILE", "The backup file for {0} is missing from this computer, so the original could not be restored.", name), Text(host, "NO_BACKUP_FILE", "No backup file"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    else
                    {
                        host.Ask("There is no backup of " + name + ", so the original game file could not be restored.", Text(host, "NO_BACKUP_FILE", "No backup file"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                else if (target == InstallTarget.UsbDevice)
                {
                    if (uninstallUsbFiles)
                    {
                        host.UpdateStatus(Format(host, "FILE_UNINSTALL_LOCATION", "Uninstalling file: {0} from {1}", TransferStatusText.FileCounter(name, index, total), parent));
                        await _ftp.DeleteFileAsync(installPath);
                        host.UpdateStatus(Text(host, "FILE_UNINSTALL_SUCCESS", "Successfully uninstalled file."));
                        index++;
                        count++;
                    }
                }
                else
                {
                    host.UpdateStatus(Format(host, "FILE_UNINSTALL_LOCATION", "Uninstalling file: {0} from {1}", TransferStatusText.FileCounter(name, index, total), parent));
                    await _ftp.DeleteFileAsync(installPath);
                    host.UpdateStatus(Text(host, "FILE_UNINSTALL_SUCCESS", "Successfully uninstalled file."));
                    index++;
                    count++;
                }
            }

            // Then folders (a path with no file extension is a folder).
            foreach (string template in plan.Templates)
            {
                string installPath = InstallBackupPlanner.ResolveInstallPath(template, r.Region, r.UserId, r.Usb);

                if (Path.HasExtension(installPath))
                {
                    continue;
                }

                if (!IsSafeToDeleteFolder(installPath))
                {
                    host.UpdateStatus("Skipped deleting the folder " + installPath + " (the path is incomplete or is a whole drive).");
                    continue;
                }

                host.UpdateStatus(Format(host, "FOLDER_DELETING", "Deleting folder: {0}", installPath));
                await _ftp.DeleteDirectoryAsync(installPath);
                host.UpdateStatus(Text(host, "FOLDER_DELETE_SUCCESS", "Successfully deleted folder."));
            }

            // Empty folders left in /dev_hdd0/tmp/.
            foreach (string template in plan.Templates)
            {
                string installPath = InstallBackupPlanner.ResolveInstallPath(template, r.Region, r.UserId, r.Usb);

                if (!TransferFileRules.IsTmpFolderPath(installPath) || !IsSafeToDeleteFolder(installPath))
                {
                    continue;
                }

                if (await _ftp.DirectoryExistsAsync(installPath) && await _ftp.IsDirectoryEmptyAsync(installPath))
                {
                    host.UpdateStatus(Format(host, "FOLDER_DELETING", "Deleting folder: {0}", installPath));
                    await _ftp.DeleteDirectoryAsync(installPath);
                    host.UpdateStatus(Text(host, "FOLDER_DELETE_SUCCESS", "Successfully deleted folder."));
                }
            }

            RemoveInstalled(s.Profile, plan.CategoryId, plan.ModId, plan.IsCustom);
            _context.Save();

            await PopupAsync(s, TransferStatusText.UninstalledPopup(plan.CategoryTitle, plan.Name, count));

            return count;
        }

        // A folder delete is refused for an unresolved placeholder or a path with fewer than two parts ("/dev_hdd0/game/").
        private static bool IsSafeToDeleteFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('{') >= 0 || path.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            string[] parts = path.Replace('\\', '/').Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        // ------------------------------------------------------------------------------------------------------
        // Region, user, files, records
        // ------------------------------------------------------------------------------------------------------

        private async Task<string> FindRegionAsync(Session s, CategoryItem category, string categoryId)
        {
            string saved = GameRegionPlanner.FindRemembered(s.Settings.GameRegionsPs3, categoryId, s.Settings.RememberGameRegions);

            if (!string.IsNullOrEmpty(saved))
            {
                return saved;
            }

            Func<CategoryItem, ITransferHost, Task<string>> choose = _hooks.ChooseGameRegion;

            if (choose == null)
            {
                throw new InvalidOperationException("No game region is saved for this game, and no region picker is connected. Save the region with the Game Regions tool first. Nothing was changed on the console.");
            }

            return await choose(category, s.Host);
        }

        private async Task<string> FindUserIdAsync(Session s)
        {
            s.Host.UpdateStatus(Text(s.Host, "GETTING_USER_PROFILE", "Getting user profile..."));

            List<ListItem> profiles = await _ftp.GetUserProfilesAsync();

            if (profiles.Count == 0)
            {
                throw new InvalidOperationException("No user profiles were found on the console under " + ConsolePaths.HomeFolder + ".");
            }

            Func<IList<ListItem>, ITransferHost, Task<string>> choose = _hooks.ChooseUserProfile;

            if (choose != null)
            {
                return await choose(profiles, s.Host);
            }

            if (profiles.Count == 1)
            {
                return profiles[0].Value;
            }

            throw new InvalidOperationException("The console has " + profiles.Count + " user profiles and no profile picker is connected, so the install cannot choose one. Nothing was changed on the console.");
        }

        private static void RegionRefused(Session s, GameRegionFailure failure)
        {
            ITransferHost host = s.Host;
            host.Progress.Report(0);

            switch (failure)
            {
                case GameRegionFailure.NotSupported:
                    host.UpdateStatus(Text(host, "NOT_SUPPORTED_GAME_REGION", "This game region is not supported by the mod.") + " " + Text(host, "INSTALL_CANCELED", "Install canceled."));
                    break;
                case GameRegionFailure.NoRegionFolder:
                    host.UpdateStatus(Text(host, "NO_GAME_REGION_FOUND", "No game region folder was found.") + " " + Text(host, "INSTALL_CANCELED", "Install canceled."));
                    host.Ask(Text(host, "NO_GAME_REGION_FOLDER_FOUND", "The console has no game folder for this region, so the game update is not installed."), Text(host, "ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    break;
                default:
                    host.UpdateStatus(Text(host, "NO_GAME_REGION_SELECTED", "No game region selected.") + " " + Text(host, "INSTALL_CANCELED", "Install canceled."));
                    break;
            }
        }

        private static DownloadFiles PickFiles(TransferRequest request, IList<DownloadFiles> list)
        {
            if (request.DownloadFiles != null)
            {
                return request.DownloadFiles;
            }

            return list != null && list.Count == 1 ? list[0] : null;
        }

        private static void RequireInstallPaths(DownloadFiles files)
        {
            if (files.InstallPaths == null || files.InstallPaths.Count == 0)
            {
                throw new InvalidOperationException("The download file '" + files.Name + "' has no install paths.");
            }
        }

        // The data layer builds some local paths with backslashes, which on Linux are not separators. Use the path as built
        // when it exists, else the same path with the platform separator.
        private static string ExistingLocalDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            string alternative = path.Replace('\\', Path.DirectorySeparatorChar);

            if (Directory.Exists(alternative))
            {
                return alternative;
            }

            throw new DirectoryNotFoundException("The downloaded files were not found at: " + path);
        }

        private static void CleanUp(string directory, string archiveFile)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                if (!string.IsNullOrEmpty(archiveFile))
                {
                    if (File.Exists(archiveFile))
                    {
                        File.Delete(archiveFile);
                    }
                    else
                    {
                        string alternative = archiveFile.Replace('\\', Path.DirectorySeparatorChar);

                        if (File.Exists(alternative))
                        {
                            File.Delete(alternative);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Clean-up is best effort, as it was in the old dialog.
            }
        }

        private async Task DownloadFileAsync(Session s, string url, string localFile)
        {
            Func<string, string, ITransferHost, Task<bool>> download = _hooks.FileDownloader;
            bool ok;

            if (download != null)
            {
                ok = await download(url, localFile, s.Host);
            }
            else
            {
                ok = await HttpExtensions.DownloadFileAsync(url, localFile, s.Host.Progress, s.Host.SetSizeText, s.Host.SetSizeVisible, s.Host.CancellationToken);
            }

            if (!ok)
            {
                throw new OperationCanceledException("The download did not finish.");
            }
        }

        private static InstalledModInfo FindInstalled(ConsoleProfile profile, string categoryId, int modId, bool isCustom)
        {
            if (profile == null || profile.InstalledMods == null)
            {
                return null;
            }

            return profile.InstalledMods.Find(delegate (InstalledModInfo x)
            {
                return x != null && x.ModId == modId && x.IsCustom == isCustom && AdapterMap.Same(x.CategoryId, categoryId);
            });
        }

        // A record of the same game that is not the mod being installed.
        private static InstalledModInfo FindOtherInCategory(ConsoleProfile profile, string categoryId, int modId, bool isCustom)
        {
            if (profile == null || profile.InstalledMods == null)
            {
                return null;
            }

            return profile.InstalledMods.Find(delegate (InstalledModInfo x)
            {
                return x != null && AdapterMap.Same(x.CategoryId, categoryId) && !(x.ModId == modId && x.IsCustom == isCustom);
            });
        }

        private string NameOfRecord(Session s, InstalledModInfo record)
        {
            if (!string.IsNullOrEmpty(record.Name))
            {
                return record.Name;
            }

            if (record.IsCustom)
            {
                CustomItemData custom = s.Settings.GetCustomMod(record.ModId);
                return custom != null ? custom.Name : "(custom mod " + record.ModId + ")";
            }

            ModItemData mod = _context.Catalog.FindMod(record.Platform, record.ModId, record.CategoryId);
            return mod != null ? mod.Name : (record.DownloadFiles != null ? record.DownloadFiles.Name : "(mod " + record.ModId + ")");
        }

        // Same effect as SettingsData.UpdateInstalledMods, plus the mod name, and null-safe.
        private static void UpdateInstalled(SettingsData settings, ConsoleProfile profile, string categoryId, CategoryType categoryType, int modId, string name, int totalFiles, DownloadFiles files, bool isCustom)
        {
            RemoveInstalled(profile, categoryId, modId, isCustom);

            profile.InstalledMods.Add(new InstalledModInfo
            {
                Platform = profile.Platform,
                CategoryId = categoryId,
                CategoryType = categoryType,
                ModId = modId,
                Name = name,
                TotalFiles = totalFiles,
                DateInstalled = DateTime.Now,
                DownloadFiles = files,
                IsCustom = isCustom
            });
        }

        private static void RemoveInstalled(ConsoleProfile profile, string categoryId, int modId, bool isCustom)
        {
            if (profile.InstalledMods == null)
            {
                profile.InstalledMods = new List<InstalledModInfo>();
                return;
            }

            profile.InstalledMods.RemoveAll(delegate (InstalledModInfo x)
            {
                return x != null && x.ModId == modId && x.IsCustom == isCustom && AdapterMap.Same(x.CategoryId, categoryId);
            });
        }

        private static InstalledPackageInfo FindInstalledPackage(SettingsData settings, PackageItemData package)
        {
            if (settings.InstalledPackages == null)
            {
                return null;
            }

            return settings.InstalledPackages.Find(delegate (InstalledPackageInfo x)
            {
                return x != null && string.Equals(x.Category, package.Category, StringComparison.Ordinal)
                    && string.Equals(x.Url, package.Url, StringComparison.Ordinal);
            });
        }

        private static void RemovePackageRecord(SettingsData settings, PackageItemData package)
        {
            if (settings.InstalledPackages == null)
            {
                settings.InstalledPackages = new List<InstalledPackageInfo>();
                return;
            }

            settings.InstalledPackages.RemoveAll(delegate (InstalledPackageInfo x)
            {
                return x != null && string.Equals(x.Category, package.Category, StringComparison.Ordinal)
                    && string.Equals(x.Url, package.Url, StringComparison.Ordinal);
            });
        }

        // webMAN popup on the console screen. Best effort and off the calling thread (the call is a plain HTTP request).
        private async Task PopupAsync(Session s, string message)
        {
            Func<ConsoleProfile, bool> present = _hooks.IsWebManInstalled;

            if (present == null || !present(s.Profile) || string.IsNullOrEmpty(s.Profile.Address))
            {
                return;
            }

            string address = s.Profile.Address;

            try
            {
                await Task.Run(delegate () { WebManExtensions.NotifyPopup(address, message); });
            }
            catch (Exception)
            {
                // A popup that does not show is not a failed install.
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Text
        // ------------------------------------------------------------------------------------------------------

        private static string Text(ITransferHost host, string key, string fallback)
        {
            return host.GetText(key, fallback);
        }

        // string.Format on a language string; falls back to the English pattern if the language string has the wrong placeholders.
        private static string Format(ITransferHost host, string key, string fallback, params object[] args)
        {
            try
            {
                return string.Format(host.GetText(key, fallback), args);
            }
            catch (FormatException)
            {
                return string.Format(fallback, args);
            }
        }

        private static void Canceled(Session s, string key, string fallback)
        {
            s.Host.Progress.Report(0);
            s.Host.UpdateStatus(Text(s.Host, key, fallback));
        }

        private static void Canceled(Session s, string key1, string fallback1, string key2, string fallback2)
        {
            s.Host.Progress.Report(0);
            s.Host.UpdateStatus(Text(s.Host, key1, fallback1) + " " + Text(s.Host, key2, fallback2));
        }

        private static void Failed(Session s, Exception ex, string key, string fallback)
        {
            s.Host.Progress.Report(0);
            s.Host.IsSuccessful = false;
            s.Host.UpdateStatus(Format(s.Host, key, fallback, ex.Message), ex);
        }
    }
}
