using System;
using System.Diagnostics;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Database;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Forms.Dialogs.Details;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.GameData.PS3;
using ArisenStudio.Models.Resources;
using Ps3 = ArisenStudio.Forms.Tools.PS3;
using Xbox = ArisenStudio.Forms.Tools.XBOX;

namespace ArisenStudio.Shell.Wiring
{
    /// <summary>
    /// Opens every dialog and tool form of the Mono port. Port of the DevExpress DialogExtensions
    /// (src/Arisen Studio/Extensions/DialogExtensions.cs), minus the XtraForm dim overlay behind each dialog
    /// (stock WinForms, decisions D7 and D8) and minus the DevExpress input boxes and message boxes (use MessageBox,
    /// TextPromptForm or a page-level control instead).
    /// <para>
    /// Every method takes the owner form and a <see cref="DialogContext" /> and runs the dialog modally with
    /// ShowDialog(owner), so the call returns when the dialog closes. The details dialogs raise events for their
    /// buttons; the launcher subscribes to them here and opens the transfer dialog, the Report Issue dialog, the FAQ
    /// or the browser, as the old MainWindow handlers did. UI thread only. Nothing here was compiled or run.
    /// </para>
    /// <para>
    /// Skipped on purpose: forms that exist but are outside the requested set (DataViewDialog, ListViewDialog,
    /// SortOptionsDialog, EasterEgg, EditLoginDialog, BackupFileDialog, which BackupFilesManager opens itself) and
    /// the old PS3 virtual controller and webMAN or XBDM notify-message forms (no Mono form exists). Forms/Dialogs/GameRegions.cs
    /// and Forms/Dialogs/XboxHDKey.cs are older copies of the Forms/Tools versions (same namespace and class name);
    /// the launcher targets the Forms/Tools copies, which are the ones in the csproj. See the result file.
    /// </para>
    /// </summary>
    internal static class DialogLauncher
    {
        // ====================================================================================================
        // Details dialogs (DialogExtensions region Details)
        // ====================================================================================================

        /// <summary>Old ShowCustomItemDetailsDialog. Install and Uninstall open the transfer dialog, Edit opens NewCustomDialog.</summary>
        public static bool ShowCustomItemDetails(Form owner, DialogContext ctx, CustomItemData customItem)
        {
            if (customItem == null)
            {
                return false;
            }

            using (CustomModDialog dialog = new CustomModDialog())
            {
                dialog.CustomItem = customItem;
                dialog.ConsoleProfile = ctx.Profile();
                dialog.IsConsoleConnected = ctx.Connected;
                ApplyLanguage(dialog, ctx);
                dialog.InstallCustomRequested += (s, e) => ShowTransferCustom(owner, ctx, TransferType.InstallCustom, customItem);
                dialog.UninstallCustomRequested += (s, e) => ShowTransferCustom(owner, ctx, TransferType.UninstallCustom, customItem);
                dialog.EditRequested += (s, e) => ShowNewCustomMod(owner, ctx, customItem, true);
                dialog.DescriptionLinkClicked += (s, e) => OpenUrl(ctx, e.LinkText);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>
        /// Old ShowItemDetailsDialog. PS3: Game, Homebrew or Resource dialog by category type; Xbox 360: HomebrewXboxDialog;
        /// PS4 mods have no details dialog (the old code opened an empty form), so this returns false and opens nothing.
        /// </summary>
        public static bool ShowItemDetails(Form owner, DialogContext ctx, CategoriesData categories, ModItemData modItem)
        {
            if (modItem == null)
            {
                return false;
            }

            CategoryType type = modItem.GetCategoryType(categories);

            switch (modItem.GetPlatform())
            {
                case Platform.PS3:
                    if (type == CategoryType.Game)
                    {
                        using (GameModDialog dialog = new GameModDialog())
                        {
                            dialog.CategoryType = CategoryType.Game;
                            dialog.ModItem = modItem;
                            dialog.Categories = categories;
                            dialog.ConsoleProfile = ctx.Profile();
                            dialog.IsConsoleConnected = ctx.Connected;
                            ApplyLanguage(dialog, ctx);
                            dialog.TransferRequested += (s, e) => ShowTransferMod(owner, ctx, e.TransferType, e.Category, e.ModItem, e.DownloadFiles);
                            dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                            dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                            dialog.LinkRequested += (s, e) => OpenUrl(ctx, e.Url);
                            dialog.FavoriteChanged += (s, e) => ctx.Save();
                            dialog.ShowDialog(owner);
                        }
                        return true;
                    }
                    if (type == CategoryType.Homebrew)
                    {
                        using (HomebrewDialog dialog = new HomebrewDialog())
                        {
                            dialog.CategoryType = CategoryType.Homebrew;
                            dialog.ModItem = modItem;
                            dialog.Categories = categories;
                            dialog.ConsoleProfile = ctx.Profile();
                            dialog.IsConsoleConnected = ctx.Connected;
                            ApplyLanguage(dialog, ctx);
                            dialog.TransferRequested += (s, e) => ShowTransferMod(owner, ctx, e.TransferType, e.Category, e.ModItem, e.DownloadFiles);
                            dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                            dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                            dialog.LinkRequested += (s, e) => OpenUrl(ctx, e.Url);
                            dialog.FavoriteChanged += (s, e) => ctx.Save();
                            dialog.ShowDialog(owner);
                        }
                        return true;
                    }
                    if (type == CategoryType.Resource)
                    {
                        using (ResourceDialog dialog = new ResourceDialog())
                        {
                            dialog.CategoryType = CategoryType.Resource;
                            dialog.ModItem = modItem;
                            dialog.Categories = categories;
                            dialog.ConsoleProfile = ctx.Profile();
                            dialog.IsConsoleConnected = ctx.Connected;
                            ApplyLanguage(dialog, ctx);
                            dialog.TransferRequested += (s, e) => ShowTransferMod(owner, ctx, e.TransferType, e.Category, e.ModItem, e.DownloadFiles);
                            dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                            dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                            dialog.LinkRequested += (s, e) => OpenUrl(ctx, e.Url);
                            dialog.FavoriteChanged += (s, e) => ctx.Save();
                            dialog.ShowDialog(owner);
                        }
                        return true;
                    }
                    return false;

                case Platform.XBOX360:
                    using (HomebrewXboxDialog dialog = new HomebrewXboxDialog())
                    {
                        dialog.CategoryType = type;
                        dialog.ModItem = modItem;
                        dialog.Categories = categories;
                        dialog.ConsoleProfile = ctx.Profile();
                        dialog.IsConsoleConnected = ctx.Connected;
                        ApplyLanguage(dialog, ctx);
                        dialog.TransferRequested += (s, e) => ShowTransferMod(owner, ctx, e.TransferType, e.Category, e.ModItem, e.DownloadFiles);
                        dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                        dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                        dialog.LinkRequested += (s, e) => OpenUrl(ctx, e.Url);
                        dialog.FavoriteChanged += (s, e) => ctx.Save();
                        dialog.ShowDialog(owner);
                    }
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Old ShowItemAppDetailsDialog. Only PS4 apps have a dialog (ApplicationDialog); other platforms return false.</summary>
        public static bool ShowItemAppDetails(Form owner, DialogContext ctx, AppItemData appItem)
        {
            if (appItem == null || appItem.GetPlatform() != Platform.PS4)
            {
                return false;
            }

            using (ApplicationDialog dialog = new ApplicationDialog())
            {
                dialog.CategoryType = CategoryType.Homebrew;
                dialog.AppItem = appItem;
                dialog.Categories = ctx.Categories;
                dialog.ConsoleProfile = ctx.Profile();
                dialog.IsConsoleConnected = ctx.Connected;
                ApplyLanguage(dialog, ctx);
                dialog.TransferRequested = (transferType, app, file) =>
                    ShowTransferApp(owner, ctx, transferType, ctx.Categories == null ? null : appItem.GetCategory(ctx.Categories), app, file);
                dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>Old ShowItemPackageDetailsDialog (PS3 package store). Install and Download open the transfer dialog, FAQ opens PackagesFaqDialog.</summary>
        public static bool ShowItemPackageDetails(Form owner, DialogContext ctx, PackageItemData packageItem)
        {
            if (packageItem == null)
            {
                return false;
            }

            using (PackageDialog dialog = new PackageDialog())
            {
                dialog.PackageItem = packageItem;
                dialog.Categories = ctx.Categories;
                dialog.ConsoleProfile = ctx.Profile();
                dialog.IsConsoleConnected = ctx.Connected;
                ApplyLanguage(dialog, ctx);
                dialog.InstallRequested += (s, e) => ShowTransferPackage(owner, ctx, TransferType.InstallPackage, packageItem);
                dialog.DownloadRequested += (s, e) => ShowTransferPackage(owner, ctx, TransferType.DownloadPackage, packageItem);
                dialog.FaqRequested += (s, e) => ShowPackagesFaq(owner, ctx);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>Old ShowItemGameSaveDetailsDialog.</summary>
        public static bool ShowItemGameSaveDetails(Form owner, DialogContext ctx, GameSaveItemData gameSaveItem)
        {
            if (gameSaveItem == null)
            {
                return false;
            }

            using (GameSaveDialog dialog = new GameSaveDialog())
            {
                dialog.GameSaveItem = gameSaveItem;
                dialog.Categories = ctx.Categories;
                dialog.ConsoleProfile = ctx.Profile();
                dialog.IsConsoleConnected = ctx.Connected;
                ApplyLanguage(dialog, ctx);
                dialog.DownloadRequested += (s, e) => ShowTransferGameSave(owner, ctx, e.TransferType, e.Category, e.GameSaveItem, e.DownloadFiles);
                dialog.InstallRequested += (s, e) => ShowTransferGameSave(owner, ctx, e.TransferType, e.Category, e.GameSaveItem, e.DownloadFiles);
                dialog.DescriptionLinkClicked += (s, e) => OpenUrl(ctx, e.LinkText);
                dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                dialog.HelpRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>
        /// Old ShowItemGameCheatsDialog. The dialog loads the cheats itself from GameItem when GameCheats is not set
        /// (see GameCheatsDialog.GetGameCheatsPS3), so only GameItem and the hooks are set here.
        /// </summary>
        public static bool ShowItemGameCheats(Form owner, DialogContext ctx, GameItemData gameItem)
        {
            if (gameItem == null)
            {
                return false;
            }

            using (GameCheatsDialog dialog = new GameCheatsDialog())
            {
                dialog.GameItem = gameItem;
                ApplyLanguage(dialog, ctx);
                dialog.ConnectConsole = ctx.ConnectConsole;
                if (ctx.ApplyCheat != null)
                {
                    dialog.ApplyCheat = ctx.ApplyCheat;
                }
                dialog.OpenUrl = url => OpenUrl(ctx, url);
                dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>Old ShowItemGamePatchesDialog. The old code never set the patch item; this one does.</summary>
        public static bool ShowItemGamePatches(Form owner, DialogContext ctx, GamePatchItemData gamePatchItem)
        {
            if (gamePatchItem == null)
            {
                return false;
            }

            using (GamePatchesDialog dialog = new GamePatchesDialog())
            {
                dialog.GamePatchItem = gamePatchItem;
                ApplyLanguage(dialog, ctx);
                if (ctx.ApplyPatch != null)
                {
                    dialog.ApplyPatch = ctx.ApplyPatch;
                }
                dialog.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>Old ShowGameTrainers (Xbox 360). Download and Install read dialog.SelectedTrainerItem.</summary>
        public static bool ShowGameTrainers(Form owner, DialogContext ctx, TrainerGameItem trainerItem)
        {
            if (trainerItem == null)
            {
                return false;
            }

            using (GameTrainersDialog dialog = new GameTrainersDialog())
            {
                dialog.TrainerGameData = trainerItem;
                dialog.Database = ctx.Database;
                dialog.ConsoleProfile = ctx.Profile();
                dialog.IsConsoleConnected = ctx.Connected;
                ApplyLanguage(dialog, ctx);
                dialog.DownloadTrainerRequested += (s, e) => ShowTransferTrainer(owner, ctx, TransferType.DownloadTrainer, trainerItem, dialog.SelectedTrainerItem);
                dialog.InstallTrainerRequested += (s, e) => ShowTransferTrainer(owner, ctx, TransferType.InstallTrainer, trainerItem, dialog.SelectedTrainerItem);
                dialog.ReportIssueRequested += (s, e) => ShowReportIssue(owner, ctx, null);
                dialog.HelpSupportRequested += (s, e) => OpenUrl(ctx, Urls.ProjectWebsite);
                dialog.LoadFailed += (s, e) => ctx.Status("Could not load the trainers: " + e.GetException().Message);
                dialog.InstallFailed += (s, e) => MessageBox.Show(owner, e.GetException().Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dialog.ShowDialog(owner);
            }
            return true;
        }

        // ====================================================================================================
        // Transfer dialog (the five old ShowTransfer... overloads)
        // ====================================================================================================

        /// <summary>Old ShowTransferFilesDialog for custom mods. Returns TransferDialog.IsSuccessful.</summary>
        public static bool ShowTransferCustom(Form owner, DialogContext ctx, TransferType transferType, CustomItemData customItem)
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.CustomMod = customItem;
                d.IsCustom = true;
            });
        }

        /// <summary>Old ShowTransferFilesDialog for normal mods. region is the PS3 game region (may be empty).</summary>
        public static bool ShowTransferMod(Form owner, DialogContext ctx, TransferType transferType, CategoryItem category, ModItemData modItem, DownloadFiles downloadFiles, string region = "")
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.Category = category;
                d.ModItem = modItem;
                d.DownloadFiles = downloadFiles;
                d.GameRegion = region;
            });
        }

        /// <summary>Old ShowTransferFilesDialog for PS4 homebrew applications.</summary>
        public static bool ShowTransferApp(Form owner, DialogContext ctx, TransferType transferType, CategoryItem category, AppItemData appData, AppItemFile appFile)
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.Category = category;
                d.AppItem = appData;
                d.AppItemFile = appFile;
            });
        }

        /// <summary>Old ShowTransferPackagesDialog (PS3 package store).</summary>
        public static bool ShowTransferPackage(Form owner, DialogContext ctx, TransferType transferType, PackageItemData packageItem)
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.PackageItem = packageItem;
            });
        }

        /// <summary>Old ShowTransferFilesDialog for Xbox 360 trainers.</summary>
        public static bool ShowTransferTrainer(Form owner, DialogContext ctx, TransferType transferType, TrainerGameItem trainersGame, TrainerItem trainerItem)
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.TrainerGame = trainersGame;
                d.TrainerItem = trainerItem;
            });
        }

        /// <summary>Old ShowTransferGameSavesDialog.</summary>
        public static bool ShowTransferGameSave(Form owner, DialogContext ctx, TransferType transferType, CategoryItem category, GameSaveItemData gameSaveItem, DownloadFiles downloadFiles = null)
        {
            return RunTransfer(owner, ctx, d =>
            {
                d.TransferType = transferType;
                d.Category = category;
                d.GameSaveItem = gameSaveItem;
                d.DownloadFiles = downloadFiles;
            });
        }

        private static bool RunTransfer(Form owner, DialogContext ctx, Action<TransferDialog> configure)
        {
            using (TransferDialog dialog = new TransferDialog())
            {
                dialog.ConsoleProfile = ctx.Profile();
                if (ctx.Language != null)
                {
                    dialog.Language = ctx.Language;
                }
                if (ctx.TitleIdToTitle != null)
                {
                    dialog.TitleIdToTitle = ctx.TitleIdToTitle;
                }
                if (ctx.TransferOperations != null)
                {
                    dialog.ConsoleOperations = ctx.TransferOperations;
                }
                configure(dialog);
                dialog.ShowDialog(owner);
                // The transfer updates the installed and downloaded lists in AppHost.Settings.
                ctx.Save();
                return dialog.IsSuccessful;
            }
        }

        // ====================================================================================================
        // Connections, profiles, custom mods, setup wizard
        // ====================================================================================================

        /// <summary>Old ShowConnectionsDialog. Returns the profile picked by double click, or null. Pass platform null for every platform.</summary>
        public static ConsoleProfile ShowConnections(Form owner, DialogContext ctx, Platform? platform)
        {
            using (ConnectionsDialog dialog = new ConnectionsDialog())
            {
                dialog.Platform = platform;
                dialog.IsConsoleConnected = ctx.Connected;
                dialog.ConnectedConsoleProfile = ctx.Profile;
                ApplyLanguage(dialog, ctx);
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ConsoleProfile : null;
            }
        }

        /// <summary>Old ShowEditConnectionsDialog (ribbon Edit Profiles): the dialog in edit mode. Saves the settings afterwards.</summary>
        public static void ShowEditConnections(Form owner, DialogContext ctx, bool isEditing)
        {
            using (ConnectionsDialog dialog = new ConnectionsDialog())
            {
                dialog.IsEditing = isEditing;
                dialog.IsConsoleConnected = ctx.Connected;
                dialog.ConnectedConsoleProfile = ctx.Profile;
                ApplyLanguage(dialog, ctx);
                dialog.ShowDialog(owner);
            }
            ctx.Save();
        }

        /// <summary>
        /// Old ShowNewConnectionWindow. OK: the edited or new profile (the caller adds a new one to
        /// AppHost.Settings.ConsoleProfiles). Cancel: the original profile when editing, otherwise null. The old
        /// createDefaultIfNull branch (platform picker then CreateDefaultProfile) is left to the caller:
        /// AppHost.Settings.CreateDefaultProfile(platform) is public.
        /// </summary>
        public static ConsoleProfile ShowNewProfile(Form owner, DialogContext ctx, ConsoleProfile profile, bool isEditing)
        {
            using (NewProfileDialog dialog = new NewProfileDialog())
            {
                if (profile != null)
                {
                    dialog.ConsoleProfile = profile;
                }
                dialog.IsEditingProfile = isEditing;
                ApplyLanguage(dialog, ctx);
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    return dialog.ConsoleProfile;
                }
                return isEditing ? profile : null;
            }
        }

        /// <summary>Old ShowNewCustomModsDialog. Saves the settings afterwards.</summary>
        public static void ShowNewCustomMod(Form owner, DialogContext ctx, CustomItemData customItem, bool isEditing)
        {
            using (NewCustomDialog dialog = new NewCustomDialog())
            {
                dialog.IsEditing = isEditing;
                if (customItem != null)
                {
                    dialog.CustomMod = customItem;
                }
                dialog.Database = ctx.Database;
                ApplyLanguage(dialog, ctx);
                dialog.ShowDialog(owner);
            }
            ctx.Save();
        }

        /// <summary>
        /// Old ShowSetupWizardDialog. Platform null lets the wizard ask for a platform itself when it closes with no profiles.
        /// Returns the profile the wizard chose (DialogResult.OK) or null. Saves the settings afterwards.
        /// </summary>
        public static ConsoleProfile ShowSetupWizard(Form owner, DialogContext ctx, Platform? platform)
        {
            using (SetupWizardDialog dialog = new SetupWizardDialog())
            {
                dialog.Platform = platform;
                dialog.ConnectedConsoleProfile = ctx.Profile;
                if (ctx.ScanXboxConsoles != null)
                {
                    dialog.ScanXboxConsoles = ctx.ScanXboxConsoles;
                }
                ApplyLanguage(dialog, ctx);
                DialogResult result = dialog.ShowDialog(owner);
                ctx.Save();
                return result == DialogResult.OK ? dialog.ConsoleProfile : null;
            }
        }

        // ====================================================================================================
        // Help dialogs
        // ====================================================================================================

        /// <summary>Old ShowReportIssueDialog. With an exception the form URL is prefilled (Urls.GenerateIssueFormUrl).</summary>
        public static void ShowReportIssue(Form owner, DialogContext ctx, Exception exception)
        {
            using (ReportIssueDialog dialog = new ReportIssueDialog())
            {
                if (exception != null)
                {
                    try
                    {
                        dialog.FormUrl = Urls.GenerateIssueFormUrl(exception, false);
                    }
                    catch (Exception)
                    {
                        // Keep the default URL when the form URL cannot be built.
                    }
                    dialog.ExceptionThrown = exception;
                }
                dialog.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowRequestModsDialog.</summary>
        public static void ShowRequestMods(Form owner)
        {
            using (RequestModsDialog dialog = new RequestModsDialog())
            {
                dialog.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowPackagesFaqDialog.</summary>
        public static void ShowPackagesFaq(Form owner, DialogContext ctx)
        {
            using (PackagesFaqDialog dialog = new PackagesFaqDialog())
            {
                ApplyLanguage(dialog, ctx);
                dialog.ShowDialog(owner);
            }
        }

        // ====================================================================================================
        // PS3 tools (forms in Forms/Tools/PS3 and Forms/Tools/PS3/New, namespace ArisenStudio.Forms.Tools.PS3)
        // ====================================================================================================

        /// <summary>Old ShowGameBackupFileManager. Saves the backup data when the form reports a change.</summary>
        public static void ShowPs3BackupFilesManager(Form owner, DialogContext ctx)
        {
            using (Ps3.BackupFilesManager form = new Ps3.BackupFilesManager())
            {
                if (ctx.BackupFiles != null)
                {
                    form.BackupFiles = ctx.BackupFiles;
                }
                form.Categories = ctx.Categories;
                form.IsConsoleConnected = ctx.Connected;
                if (ctx.Ps3FileTransfer != null)
                {
                    form.FileTransfer = ctx.Ps3FileTransfer;
                }
                ApplyLanguage(form, ctx);
                form.BackupFilesChanged += (s, e) =>
                {
                    if (ctx.SaveBackupFiles != null)
                    {
                        ctx.SaveBackupFiles();
                    }
                };
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowPs3ConsoleManager.</summary>
        public static void ShowPs3ConsoleManager(Form owner, DialogContext ctx)
        {
            using (Ps3.ConsoleManager form = new Ps3.ConsoleManager())
            {
                form.ConsoleAddress = ctx.Address();
                if (ctx.Ps3ConsoleControl != null)
                {
                    form.ConsoleControl = ctx.Ps3ConsoleControl;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Ribbon Launch Game (webMAN). No old DialogExtensions method; the form exists in Tools/PS3/New.</summary>
        public static void ShowPs3GameLauncher(Form owner, DialogContext ctx)
        {
            using (Ps3.GameLauncher form = new Ps3.GameLauncher())
            {
                form.ConsoleAddress = ctx.Address();
                if (ctx.Ps3GameBrowser != null)
                {
                    form.Browser = ctx.Ps3GameBrowser;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowPackageManager.</summary>
        public static void ShowPs3PackageManager(Form owner, DialogContext ctx)
        {
            using (Ps3.PackageFilesManager form = new Ps3.PackageFilesManager())
            {
                if (ctx.Ps3FileTransfer != null)
                {
                    form.FileTransfer = ctx.Ps3FileTransfer;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowBootPluginsEditor.</summary>
        public static void ShowPs3BootPluginsEditor(Form owner, DialogContext ctx)
        {
            using (Ps3.BootPluginsEditor form = new Ps3.BootPluginsEditor())
            {
                if (ctx.Ps3FileTransfer != null)
                {
                    form.FileTransfer = ctx.Ps3FileTransfer;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowPs3TakeScreenshot.</summary>
        public static void ShowPs3TakeScreenshot(Form owner, DialogContext ctx)
        {
            using (Ps3.TakeScreenshot form = new Ps3.TakeScreenshot())
            {
                form.ConsoleAddress = ctx.Address();
                if (ctx.Ps3FileTransfer != null)
                {
                    form.FileTransfer = ctx.Ps3FileTransfer;
                }
                if (ctx.ScreenshotUploader != null)
                {
                    form.Uploader = ctx.ScreenshotUploader;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowGameRegionsDialog (compiled version: Forms/Tools/PS3/GameRegions.cs).</summary>
        public static void ShowPs3GameRegions(Form owner, DialogContext ctx)
        {
            using (Ps3.GameRegions form = new Ps3.GameRegions())
            {
                form.Categories = ctx.Categories;
                form.IsConsoleConnected = ctx.Connected;
                if (ctx.Ps3DirectoryExists != null)
                {
                    form.ConsoleDirectoryExists = ctx.Ps3DirectoryExists;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
                ctx.Save();
            }
        }

        /// <summary>Old ShowGameUpdatesFinder (compiled version: Forms/Dialogs/GameUpdatesFinder.cs).</summary>
        public static void ShowPs3GameUpdatesFinder(Form owner, DialogContext ctx)
        {
            using (Ps3.GameUpdatesFinder form = new Ps3.GameUpdatesFinder())
            {
                form.IsConsoleConnected = ctx.Connected;
                if (ctx.Ps3UploadFile != null)
                {
                    form.UploadFile = ctx.Ps3UploadFile;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        // ====================================================================================================
        // Xbox 360 tools (Forms/Tools/XBOX and Forms/Tools/XBOX/New, namespace ArisenStudio.Forms.Tools.XBOX)
        // ====================================================================================================

        /// <summary>Old ShowXboxGameSaveResigner.</summary>
        public static void ShowXboxGameSaveResigner(Form owner, DialogContext ctx)
        {
            using (Xbox.GameSaveResigner form = new Xbox.GameSaveResigner())
            {
                form.IsConsoleConnected = ctx.Connected;
                if (ctx.XboxPackageFactory != null)
                {
                    form.PackageFactory = ctx.XboxPackageFactory;
                }
                if (ctx.XboxConsole != null)
                {
                    form.FileTransfer = ctx.XboxConsole;
                    form.ProfilePicker = ctx.XboxConsole;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxGameLauncher. Saves the settings (the form edits the game file list).</summary>
        public static void ShowXboxGameLauncher(Form owner, DialogContext ctx)
        {
            using (Xbox.GameLauncher form = new Xbox.GameLauncher())
            {
                if (ctx.XboxConsole != null)
                {
                    form.Launcher = ctx.XboxConsole;
                }
                form.GameFilesChanged += (s, e) => ctx.Save();
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxModuleLoader.</summary>
        public static void ShowXboxModuleLoader(Form owner, DialogContext ctx)
        {
            using (Xbox.ModuleLoader form = new Xbox.ModuleLoader())
            {
                if (ctx.XboxConsole != null)
                {
                    form.Modules = ctx.XboxConsole;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxXuidGameSpoofer.</summary>
        public static void ShowXboxXuidGameSpoofer(Form owner, DialogContext ctx)
        {
            using (Xbox.XuidGameSpoofer form = new Xbox.XuidGameSpoofer())
            {
                if (ctx.XboxConsole != null)
                {
                    form.Memory = ctx.XboxConsole;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxTakeScreenshot.</summary>
        public static void ShowXboxTakeScreenshot(Form owner, DialogContext ctx)
        {
            using (Xbox.TakeScreenshot form = new Xbox.TakeScreenshot())
            {
                if (ctx.XboxConsole != null)
                {
                    form.ScreenshotSource = ctx.XboxConsole;
                }
                if (ctx.ScreenshotUploader != null)
                {
                    form.Uploader = ctx.ScreenshotUploader;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxNeighborhoodEditor. initialName prefills the name box (may be null).</summary>
        public static void ShowXboxNeighborhoodEditor(Form owner, DialogContext ctx, string initialName)
        {
            using (Xbox.NeighborhoodEditor form = new Xbox.NeighborhoodEditor())
            {
                if (ctx.XboxConsole != null)
                {
                    form.Neighborhood = ctx.XboxConsole;
                }
                form.InitialName = initialName;
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxDashlaunchEditor.</summary>
        public static void ShowXboxDashlaunchEditor(Form owner, DialogContext ctx)
        {
            using (Xbox.DashlaunchEditor form = new Xbox.DashlaunchEditor())
            {
                if (ctx.XboxConsole != null)
                {
                    form.FileTransfer = ctx.XboxConsole;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Old ShowXboxHDKey (compiled version: Forms/Tools/XBOX/XboxHDKey.cs).</summary>
        public static void ShowXboxHdKey(Form owner, DialogContext ctx)
        {
            using (Xbox.XboxHDKey form = new Xbox.XboxHDKey())
            {
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        /// <summary>Xbox Console Manager (form exists in Tools/XBOX/New; the old DialogExtensions had no method and the ribbon has no id for it).</summary>
        public static void ShowXboxConsoleManager(Form owner, DialogContext ctx)
        {
            using (Xbox.ConsoleManager form = new Xbox.ConsoleManager())
            {
                if (ctx.XboxConsole != null)
                {
                    form.Console = ctx.XboxConsole;
                }
                ApplyLanguage(form, ctx);
                form.ShowDialog(owner);
            }
        }

        // ====================================================================================================
        // Ribbon command routing
        // ====================================================================================================

        /// <summary>
        /// Opens the tool form for a ribbon command id (RibbonCommandIds). Returns true when the command id is a form
        /// command and the form was opened, false when the id is not one of the form commands (the caller keeps it).
        /// The webMAN and XBDM power, mount and notify commands are not forms and are not handled here.
        /// </summary>
        public static bool TryShowTool(Form owner, DialogContext ctx, string commandId)
        {
            switch (commandId)
            {
                case RibbonCommandIds.ToolsPsGameUpdates: ShowPs3GameUpdatesFinder(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsBackupFilesManager: ShowPs3BackupFilesManager(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsPackageManager: ShowPs3PackageManager(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsConsoleManager: ShowPs3ConsoleManager(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsGameRegions: ShowPs3GameRegions(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsBootPluginsEditor: ShowPs3BootPluginsEditor(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsScreenshot: ShowPs3TakeScreenshot(owner, ctx); return true;
                case RibbonCommandIds.ToolsPsGamesLaunchGame: ShowPs3GameLauncher(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxGameSaveResigner: ShowXboxGameSaveResigner(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxHdKey: ShowXboxHdKey(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxGamesLauncher: ShowXboxGameLauncher(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxModuleLoader: ShowXboxModuleLoader(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxXuidSpoofer: ShowXboxXuidGameSpoofer(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxDashlaunchEditor: ShowXboxDashlaunchEditor(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxTakeScreenshot: ShowXboxTakeScreenshot(owner, ctx); return true;
                case RibbonCommandIds.ToolsXboxNeighborhoodEditor: ShowXboxNeighborhoodEditor(owner, ctx, null); return true;
                default: return false;
            }
        }

        // ====================================================================================================
        // Helpers
        // ====================================================================================================

        private static void OpenUrl(DialogContext ctx, string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            if (ctx.OpenUrl != null)
            {
                ctx.OpenUrl(url);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ctx.Status("Could not open " + url + ": " + ex.Message);
            }
        }

        // Every ported form has "public ResourceManager Language { get; set; }" but no shared base type, so set it by reflection-free
        // overloads. Each overload only runs when the host supplied a ResourceManager.
        private static void ApplyLanguage(CustomModDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(GameModDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(HomebrewDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(HomebrewXboxDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(ResourceDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(ApplicationDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(PackageDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(GameSaveDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(GameCheatsDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(GamePatchesDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(GameTrainersDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(ConnectionsDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(NewProfileDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(NewCustomDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(SetupWizardDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(PackagesFaqDialog d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.BackupFilesManager d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.ConsoleManager d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.GameLauncher d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.PackageFilesManager d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.BootPluginsEditor d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.TakeScreenshot d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.GameRegions d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Ps3.GameUpdatesFinder d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.GameSaveResigner d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.GameLauncher d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.ModuleLoader d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.XuidGameSpoofer d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.TakeScreenshot d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.NeighborhoodEditor d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.DashlaunchEditor d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.XboxHDKey d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
        private static void ApplyLanguage(Xbox.ConsoleManager d, DialogContext c) { if (c.Language != null) { d.Language = c.Language; } }
    }
}
