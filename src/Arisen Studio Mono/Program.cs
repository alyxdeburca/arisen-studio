using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Database;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Shell;
using Newtonsoft.Json;

namespace ArisenStudio
{
    internal static class Program
    {
        private static bool _smoke;
        private static int _exitCode;

        // Names accepted by --form. Add one entry per dialog that needs a smoke check.
        private static readonly Dictionary<string, Func<Form>> FormFactories = new Dictionary<string, Func<Form>>
        {
            { "ReportIssueDialog", () => new ReportIssueDialog() },
            { "PackagesFaqDialog", () => new PackagesFaqDialog() },
            { "EditLoginDialog", () => new EditLoginDialog() },
            { "DataViewDialog", () => new DataViewDialog() },
            { "RequestModsDialog", () => new RequestModsDialog() },
            // Smoke factories fill in sample data so the captures show a populated layout. No file or network access.
            { "SortOptionsDialog", () => new SortOptionsDialog
                {
                    SortOptions = new List<string> { "Name", "Date Added", "Size", "Game ID" },
                    SortOption = "Date Added",
                    SortOrder = System.Windows.Forms.SortOrder.Descending
                } },
            { "BackupFileDialog", () => new BackupFileDialog
                {
                    FileName = "GAME.BIN",
                    CategoryId = "BLUS30001",
                    InstallPath = "/dev_hdd0/game/BLUS30001/USRDIR/GAME.BIN",
                    LocalPath = "C:\\Backups\\BLUS30001\\GAME.BIN"
                } },
            { "ListViewDialog", () => new ListViewDialog { Items = SampleListItems() } },
            { "EasterEgg", () => new EasterEgg() },
            { "PackageDialog", () => new Forms.Dialogs.Details.PackageDialog { PackageItem = SamplePackage() } },
            { "ApplicationDialog", () => new Forms.Dialogs.Details.ApplicationDialog { AppItem = SampleApp(), CategoryType = CategoryType.Game } },
            { "CustomModDialog", () => new Forms.Dialogs.Details.CustomModDialog { CustomItem = new CustomItemData { } } },
            { "GameModDialog", () => new Forms.Dialogs.Details.GameModDialog { ModItem = SampleMod(), CategoryType = CategoryType.Game } },
            { "GameSaveDialog", () => new Forms.Dialogs.Details.GameSaveDialog { GameSaveItem = SampleSave(), CategoryType = CategoryType.Game } },
            { "GameTrainersDialog", () => new Forms.Dialogs.Details.GameTrainersDialog { TrainerGameData = SampleTrainers() } },
            { "HomebrewDialog", () => new Forms.Dialogs.Details.HomebrewDialog { ModItem = SampleMod(), CategoryType = CategoryType.Homebrew } },
            { "HomebrewXboxDialog", () => new Forms.Dialogs.Details.HomebrewXboxDialog { ModItem = SampleMod(), CategoryType = CategoryType.Homebrew } },
            { "ResourceDialog", () => new Forms.Dialogs.Details.ResourceDialog { ModItem = SampleMod(), CategoryType = CategoryType.Resource } },
            // The six dialogs below open with their defaults: empty profile list, no model, no network.
            { "ConnectionsDialog", () => new ConnectionsDialog() },
            { "NewProfileDialog", () => new NewProfileDialog() },
            { "NewCustomDialog", () => new NewCustomDialog() },
            // Platform is set so that closing with no profiles does not open the modal platform picker.
            { "SetupWizardDialog", () => new SetupWizardDialog { Platform = Platform.PS3 } },
            { "GameCheatsDialog", () => new Forms.Dialogs.Details.GameCheatsDialog() },
            { "GamePatchesDialog", () => new Forms.Dialogs.Details.GamePatchesDialog() },
            // The three tool forms below open with their defaults: no categories, no console hooks, no network.
            { "GameRegions", () => new Forms.Tools.PS3.GameRegions() },
            { "XboxHDKey", () => new Forms.Tools.XBOX.XboxHDKey() },
            { "GameUpdatesFinder", () => new Forms.Tools.PS3.GameUpdatesFinder() },
            // The new PS3 and Xbox tool forms open with their defaults: every console hook is a stub that says "not available", so no console, file or network access.
            { "Ps3BackupFilesManager", () => new Forms.Tools.PS3.BackupFilesManager() },
            { "Ps3BootPluginsEditor", () => new Forms.Tools.PS3.BootPluginsEditor() },
            { "Ps3ConsoleManager", () => new Forms.Tools.PS3.ConsoleManager() },
            // Sample address from the documentation range (192.0.2.0/24): the stub browser only shows it as text and never opens it.
            { "Ps3GameLauncher", () => new Forms.Tools.PS3.GameLauncher { ConsoleAddress = "192.0.2.1" } },
            { "Ps3PackageFilesManager", () => new Forms.Tools.PS3.PackageFilesManager() },
            { "Ps3TakeScreenshot", () => new Forms.Tools.PS3.TakeScreenshot() },
            { "XboxConsoleManager", () => new Forms.Tools.XBOX.ConsoleManager() },
            { "XboxDashlaunchEditor", () => new Forms.Tools.XBOX.DashlaunchEditor() },
            { "XboxGameLauncher", () => new Forms.Tools.XBOX.GameLauncher() },
            { "XboxGameSaveResigner", () => new Forms.Tools.XBOX.GameSaveResigner() },
            { "XboxModuleLoader", () => new Forms.Tools.XBOX.ModuleLoader() },
            { "XboxNeighborhoodEditor", () => new Forms.Tools.XBOX.NeighborhoodEditor() },
            { "XboxTakeScreenshot", () => new Forms.Tools.XBOX.TakeScreenshot() },
            { "XboxXuidGameSpoofer", () => new Forms.Tools.XBOX.XuidGameSpoofer() },
            // Sample mod and the default not-connected console operations: an install or uninstall reports "Connect to a console first" and never touches a console or the network.
            { "TransferDialog", () => new TransferDialog { TransferType = TransferType.InstallMods, ModItem = SampleMod() } },
        };

        // Sample models for the --form smoke checks. Plain values only: no network, no file access, and the URLs are never opened.
        private static List<DownloadFiles> SampleDownloadFiles()
        {
            return new List<DownloadFiles>
            {
                new DownloadFiles { Name = "sample-mod.zip", Region = "USA", Version = "1.00", LastUpdated = new DateTime(2024, 1, 1), Url = "https://example.invalid/sample-mod.zip", InstallPaths = new List<string> { "/dev_hdd0/game/" } },
                new DownloadFiles { Name = "sample-mod-patch.zip", Region = "USA", Version = "1.01", LastUpdated = new DateTime(2024, 2, 1), Url = "https://example.invalid/sample-mod-patch.zip", InstallPaths = new List<string> { "/dev_hdd0/game/" } }
            };
        }

        private static PackageItemData SamplePackage()
        {
            return new PackageItemData
            {
                Id = 1,
                Category = "Games",
                TitleId = "BLUS30001",
                Region = "USA",
                Name = "Sample Package & Friends",
                Url = "https://example.invalid/sample.pkg",
                ContentId = "UP0000-BLUS30001_00-SAMPLE000000001",
                ModifiedDate = "2024-01-01T00:00:00",
                Size = "1048576"
            };
        }

        private static AppItemData SampleApp()
        {
            return new AppItemData
            {
                Platform = "PlayStation4",
                Id = 1,
                CategoryId = "1",
                Name = "Sample Application",
                CreatedBy = "Sample Author",
                SubmittedBy = "Sample Submitter",
                Version = "1.00",
                PlayableVersion = "1.00",
                ContentId = "UP0000-BLUS30001_00-SAMPLE000000001",
                TitleId = "BLUS30001",
                Description = "A sample application description.",
                DownloadFiles = new List<AppItemFile>
                {
                    new AppItemFile { Name = "sample-app.pkg", Version = "1.00", PlayableVersion = "1.00", LastUpdated = new DateTime(2024, 1, 1), InstallPath = "/dev_hdd0/game/" }
                }
            };
        }

        private static ModItemData SampleMod()
        {
            return new ModItemData
            {
                Platform = "PlayStation3",
                Id = 1,
                CategoryId = "1",
                Name = "Sample Mod",
                FirmwareType = "CFW",
                Region = "USA",
                CreatedBy = "Sample Author",
                SubmittedBy = "Sample Submitter",
                Version = "1.00",
                GameMode = "Single Player",
                ModType = "Mod",
                Description = "A sample mod description.",
                DownloadFiles = SampleDownloadFiles()
            };
        }

        private static GameSaveItemData SampleSave()
        {
            return new GameSaveItemData
            {
                Platform = "PlayStation3",
                Id = 1,
                CategoryId = "1",
                Name = "Sample Save",
                Region = "USA",
                LastUpdated = new DateTime(2024, 1, 1),
                CreatedBy = "Sample Author",
                SubmittedBy = "Sample Submitter",
                Version = "1.00",
                GameMode = "Single Player",
                Description = "A sample save description.",
                DownloadFiles = SampleDownloadFiles()
            };
        }

        private static TrainerGameItem SampleTrainers()
        {
            return new TrainerGameItem
            {
                TitleId = "BLUS30001",
                Description = "A sample trainer game.",
                Trainers = new List<TrainerItem>
                {
                    new TrainerItem { Name = "Sample Trainer", Type = "Trainer", LastUpdated = new DateTime(2024, 1, 1), Url = "https://example.invalid/trainer", InstallPaths = new[] { "/dev_hdd0/game/" } },
                    new TrainerItem { Name = "Sample Trainer 2", Type = "Trainer", LastUpdated = new DateTime(2024, 2, 1), Url = "https://example.invalid/trainer2", InstallPaths = new[] { "/dev_hdd0/game/" } }
                }
            };
        }

        private static List<ListViewDialogItem> SampleListItems()
        {
            List<ListViewDialogItem> items = new List<ListViewDialogItem>();
            string[] names = { "Alpha Game", "Beta Game", "Gamma Game", "Delta Game", "Epsilon Game", "Zeta Game" };
            for (int i = 0; i < names.Length; i++)
            {
                items.Add(new ListViewDialogItem { Value = "ID" + (i + 1).ToString("D4"), Name = names[i] });
            }
            return items;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            // --page <key>: one page captured by PageCaptureRunner. It runs in smoke mode (temp settings, no network, no modal boxes).
            string pageKey = null;
            int pgi = Array.IndexOf(args, "--page");
            if (pgi >= 0)
            {
                if (pgi + 1 >= args.Length) { Console.WriteLine("PAGE-CAPTURE-FAIL <missing> missing key argument"); return 1; }
                pageKey = args[pgi + 1];
            }
            _smoke = args.Contains("--smoke") || pageKey != null;
            // Smoke mode must not block on a modal MessageBox from a tool form. Set before any form is created or shown.
            if (_smoke) { ArisenStudio.Forms.Tools.ToolFormSupport.NonBlockingErrors = true; }
            string capture = null;
            int ci = Array.IndexOf(args, "--capture");
            if (ci >= 0)
            {
                if (ci + 1 >= args.Length) { Console.WriteLine("CAPTURE-FAIL missing path argument"); return 2; }
                capture = args[ci + 1];
            }

            string formName = null;
            Func<Form> formFactory = null;
            int fi = Array.IndexOf(args, "--form");
            if (fi >= 0)
            {
                if (fi + 1 >= args.Length) { Console.WriteLine("FORM-FAIL missing name argument"); return 2; }
                formName = args[fi + 1];
                if (!FormFactories.TryGetValue(formName, out formFactory))
                {
                    Console.WriteLine("FORM-FAIL unknown form '" + formName + "'");
                    return 1;
                }
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Fatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Fatal(e.ExceptionObject);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Contains("--selfcheck"))
            {
#if SELFCHECK
                // Self-check run: an unhandled exception must exit with code 1 instead of opening a message box, so reuse the smoke behaviour of Fatal.
                _smoke = true;
                return ArisenStudio.Tests.SelfCheck.SelfCheckRunner.Run(args);
#else
                // The self-check files are compiled only when the SELFCHECK symbol is defined.
                Console.WriteLine("SELFCHECK-UNAVAILABLE this build does not define SELFCHECK");
                return 2;
#endif
            }

            if (pageKey != null)
            {
                // --page: one shell, one page, one PNG. PageCaptureRunner prints PAGE-CAPTURE-OK or PAGE-CAPTURE-FAIL.
                return PageCaptureRunner.Run(pageKey, capture);
            }

            if (formName == null)
            {
                using (ShellForm shell = new ShellForm())
                {
                    FullShellIntegration.Attach(shell, _smoke);
                    if (_smoke) { Arm(shell, capture); }
                    Application.Run(shell);
                }
            }
            else
            {
                using (Form form = formFactory())
                {
                    if (_smoke)
                    {
                        ReportIssueDialog dialog = form as ReportIssueDialog;
                        EasterEgg egg = form as EasterEgg;
                        if (dialog != null) { ArmReportIssue(dialog, capture); }
                        else
                        {
                            // Smoke mode: no image download and no modal story box, so the check never blocks or uses the network.
                            if (egg != null) { egg.LoadImageOnLoad = false; egg.ShowStoryOnLoad = false; }
                            ArmForm(form, capture);
                        }
                    }
                    Application.Run(form);
                }
            }

            if (_smoke) { Console.WriteLine(_exitCode == 0 ? "EXIT-OK" : "EXIT-FAIL"); }
            return _exitCode;
        }

        private static void Fatal(object ex)
        {
            Console.WriteLine("UNHANDLED " + ex);
            if (_smoke) { Environment.Exit(1); }
            _ = MessageBox.Show(ex.ToString(), "Arisen Studio Mono", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void Check(string name, Func<bool> test)
        {
            try
            {
                if (test()) { Console.WriteLine(name + "-OK"); return; }
                Console.WriteLine(name + "-FAIL assertion false");
            }
            catch (Exception ex) { Console.WriteLine(name + "-FAIL " + ex); }
            _exitCode = 1;
        }

        private static void Arm(ShellForm shell, string capture)
        {
            shell.Shown += (s, e) =>
            {
                Console.WriteLine("SHELL-SHOWN");
                Check("LINKED", () => Enum.GetValues(typeof(Platform)).Length > 0
                    && "AbC".EqualsIgnoreCase("aBc") && "a/b".RemoveInvalidChars() == "ab");
                Check("JSON", () =>
                {
                    StatsData d = JsonConvert.DeserializeObject<StatsData>("{\"totalDownloads\":5,\"totalMods\":7}");
                    return d != null && d.TotalDownloads == 5 && d.TotalMods == 7;
                });
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) =>
                {
                    t.Stop();
                    if (capture != null) { Check("CAPTURE", () => Capture(shell, capture)); }
                    Console.WriteLine("SHELL-CLOSING");
                    shell.Close();
                };
                t.Start();
            };
        }

        private static void ArmReportIssue(ReportIssueDialog dialog, string capture)
        {
            // Recorders replace the real browser and clipboard before the form is shown.
            List<string> opened = new List<string>();
            List<string> copied = new List<string>();
            dialog.OpenUrl = url => opened.Add(url);
            dialog.CopyText = text => copied.Add(text);

            dialog.Shown += (s, e) =>
            {
                Console.WriteLine("SHOWN");
                Check("ICON", () =>
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ReportIssueDialog.IconResourceName))
                    {
                        if (stream == null) { return false; }
                        using (Image decoded = Image.FromStream(stream))
                        {
                            return decoded != null && dialog.Actions.Find("open").Image != null;
                        }
                    }
                });
                Check("ACTION-OPEN", () =>
                {
                    dialog.Actions.Find("open").PerformClick();
                    return opened.Count == 1 && opened[0] == IssueLinks.ReportIssue;
                });
                Check("ACTION-COPY", () =>
                {
                    dialog.Actions.Find("copy").PerformClick();
                    return copied.Count == 1 && copied[0] == dialog.UrlBox.Text;
                });
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) =>
                {
                    t.Stop();
                    if (capture != null) { Check("CAPTURE", () => Capture(dialog, capture)); }
                    dialog.Close();
                };
                t.Start();
            };
        }

        // Generic smoke check for a dialog with no checks of its own: SHOWN, CAPTURE, then close.
        private static void ArmForm(Form form, string capture)
        {
            form.Shown += (s, e) =>
            {
                Console.WriteLine("SHOWN");
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) =>
                {
                    t.Stop();
                    if (capture != null) { Check("CAPTURE", () => Capture(form, capture)); }
                    form.Close();
                };
                t.Start();
            };
        }

        private static bool Capture(Form f, string path)
        {
            bool ok = false;
            try
            {
                using (Bitmap b = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                    b.Save(path, ImageFormat.Png);
                }
                ok = new System.IO.FileInfo(path).Length > 0;
                return ok;
            }
            finally
            {
                if (!ok) { PrintNonPositive(f); }
            }
        }

        // Diagnostic only: lists descendants with a non-positive width or height. Never throws, never changes the result.
        private static void PrintNonPositive(Form f)
        {
            try { WalkNonPositive(f, f.GetType().Name); }
            catch (Exception) { }
        }

        private static void WalkNonPositive(System.Windows.Forms.Control parent, string chain)
        {
            foreach (System.Windows.Forms.Control c in parent.Controls)
            {
                if (c.Width <= 0 || c.Height <= 0)
                {
                    Console.WriteLine("NONPOS type=" + c.GetType().Name + " name='" + c.Name + "' size=" + c.Width + "x" + c.Height + " chain=" + chain);
                }
                WalkNonPositive(c, chain + "/" + c.GetType().Name);
            }
        }
    }
}
