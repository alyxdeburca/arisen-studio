using System;
using System.Collections.Generic;
using System.IO;

namespace ArisenStudio.Services
{
    /// <summary>
    /// In-memory checks for the batch 2 services (GameUpdateParser, BootPluginsFile, InstallBackupPlanner,
    /// BackupDecision). No files, no network, no console. Call <see cref="Run" /> from a diagnostic or test
    /// entry point; an empty list means every check passed. Optional: the app does not need this file.
    /// </summary>
    public static class ServicesSelfCheckBatch2
    {
        private const string SampleXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<titlepatch titleid=\"BLES00000\" status=\"alive\">" +
            "<tag name=\"BLES00000\" popup=\"false\" signoff=\"false\">" +
            "<package version=\"01.00\" size=\"1000\" sha1sum=\"aa\" url=\"http://a.example/dl/UP0001-BLES00000_00-A.pkg\" ps3_system_ver=\"03.4000\">" +
            "<paramsfo><TITLE>Old Game (Update)</TITLE></paramsfo></package>" +
            "<package version=\"01.02\" size=\"2048\" sha1sum=\"bb\" url=\"http://a.example/dl/UP0001-BLES00000_00-B.pkg?x=1\" ps3_system_ver=\"04.8800\">" +
            "<paramsfo><TITLE>My Game (Demo)</TITLE></paramsfo></package>" +
            "</tag></titlepatch>";

        public static List<string> Run()
        {
            List<string> failures = new List<string>();

            CheckGameUpdateParser(failures);
            CheckBootPluginsFile(failures);
            CheckInstallBackupPlanner(failures);
            CheckBackupDecision(failures);

            return failures;
        }

        private static void Expect(List<string> failures, bool condition, string what)
        {
            if (!condition)
            {
                failures.Add(what);
            }
        }

        private static void CheckGameUpdateParser(List<string> failures)
        {
            Expect(failures,
                GameUpdateParser.GetUpdateListUrl(" BLES00000 ") == "https://a0.ww.np.dl.playstation.net/tpl/np/BLES00000/BLES00000-ver.xml",
                "GameUpdateParser.GetUpdateListUrl: wrong address");

            Expect(failures, GameUpdateParser.TryParse(null) == null, "GameUpdateParser.TryParse: null should give null");
            Expect(failures, GameUpdateParser.TryParse("not xml") == null, "GameUpdateParser.TryParse: junk should give null");
            Expect(failures, GameUpdateParser.TryParse("<other/>") == null, "GameUpdateParser.TryParse: wrong root should give null");

            ArisenStudio.Models.GameData.PS3.TitlePatch patch = GameUpdateParser.TryParse(SampleXml);
            Expect(failures, patch != null, "GameUpdateParser.TryParse: sample did not parse");

            if (patch == null)
            {
                return;
            }

            Expect(failures, GameUpdateParser.GetCleanTitle(patch) == "My Game", "GameUpdateParser.GetCleanTitle: expected the newest title without brackets");

            List<GameUpdateRow> rows = GameUpdateParser.ToRows(patch, null);
            Expect(failures, rows.Count == 2, "GameUpdateParser.ToRows: expected 2 rows");

            if (rows.Count == 2)
            {
                Expect(failures, rows[0].Version == "1.02", "GameUpdateParser.ToRows: newest first, version without leading zero");
                Expect(failures, rows[0].FileName == "UP0001-BLES00000_00-B.pkg", "GameUpdateParser.ToRows: file name without query string");
                Expect(failures, rows[0].Size == "2048 bytes", "GameUpdateParser.ToRows: default size text");
                Expect(failures, rows[0].MinFirmware == "4.88", "GameUpdateParser.ToRows: min firmware 04.8800");
                Expect(failures, rows[0].Sha1 == "bb", "GameUpdateParser.ToRows: sha1");
                Expect(failures, rows[1].MinFirmware == "3.40", "GameUpdateParser.ToRows: min firmware 03.4000");
            }

            List<GameUpdateRow> formatted = GameUpdateParser.ToRows(patch, delegate (long b) { return "<" + b + ">"; });
            Expect(failures, formatted.Count == 2 && formatted[0].Size == "<2048>", "GameUpdateParser.ToRows: custom size formatter");

            Expect(failures, GameUpdateParser.ToRows(null, null).Count == 0, "GameUpdateParser.ToRows: null patch should give no rows");
            Expect(failures, GameUpdateParser.CleanTitle("Game (A) (B) ") == "Game", "GameUpdateParser.CleanTitle: brackets");
            Expect(failures, GameUpdateParser.MinFirmware("") == "", "GameUpdateParser.MinFirmware: empty");
            Expect(failures, GameUpdateParser.MinFirmware("0000") == "", "GameUpdateParser.MinFirmware: all zeros must not throw");
            Expect(failures, GameUpdateParser.FileNameFromUrl("relative/path.pkg") == "", "GameUpdateParser.FileNameFromUrl: relative url");
        }

        private static void CheckBootPluginsFile(List<string> failures)
        {
            List<string> lines = BootPluginsFile.ParseLines("a\r\nb\n\nc\n");
            Expect(failures, lines.Count == 4 && lines[0] == "a" && lines[1] == "b" && lines[2] == "" && lines[3] == "c", "BootPluginsFile.ParseLines: mixed line endings");
            Expect(failures, BootPluginsFile.ParseLines("").Count == 0, "BootPluginsFile.ParseLines: empty text");

            Expect(failures, BootPluginsFile.Serialize(new string[] { "a", null, "b" }, "\n") == "a\n\nb\n", "BootPluginsFile.Serialize: lines and null entry");

            List<string> cleaned = BootPluginsFile.Clean(new string[] { " a ", "", null, "   ", "b" });
            Expect(failures, cleaned.Count == 2 && cleaned[0] == "a" && cleaned[1] == "b", "BootPluginsFile.Clean: trim and drop blanks");

            string[] listed = { "# off", "/dev_hdd0/plugins/p.sprx" };
            Expect(failures, BootPluginsFile.ContainsPlugin(listed, " /DEV_HDD0/plugins/P.SPRX "), "BootPluginsFile.ContainsPlugin: case-insensitive match");
            Expect(failures, !BootPluginsFile.ContainsPlugin(listed, "# off"), "BootPluginsFile.ContainsPlugin: comment lines are not entries");
            Expect(failures, !BootPluginsFile.ContainsPlugin(null, "x"), "BootPluginsFile.ContainsPlugin: null list");

            Expect(failures,
                BootPluginsFile.WorkingFilePath("app") == Path.Combine(Path.Combine("app", "Boot Plugins"), "boot_plugins.txt"),
                "BootPluginsFile.WorkingFilePath");
            Expect(failures,
                BootPluginsFile.BackupFilePath("bak") == Path.Combine(Path.Combine("bak", "Boot Plugins"), "boot_plugins.txt"),
                "BootPluginsFile.BackupFilePath");
        }

        private static void CheckInstallBackupPlanner(List<string> failures)
        {
            string template = "/{USBDEV}/x/{REGION}/{USERID}.dat";
            string resolved = InstallBackupPlanner.ResolveInstallPath(template, "BLES", "0001", "dev_usb000");
            Expect(failures, resolved == "/dev_usb000/x/BLES/0001.dat", "InstallBackupPlanner.ResolveInstallPath: all placeholders");
            Expect(failures, InstallBackupPlanner.ResolveInstallPath("/a/{REGION}/b", null, null, null) == "/a//b", "InstallBackupPlanner.ResolveInstallPath: null removes placeholder");

            Expect(failures, InstallBackupPlanner.Classify(template, resolved) == InstallTarget.UsbDevice, "InstallBackupPlanner.Classify: usb");
            Expect(failures,
                InstallBackupPlanner.Classify("/dev_hdd0/game/{REGION}/a", "/dev_hdd0/game/BLES/a") == InstallTarget.GameFolder,
                "InstallBackupPlanner.Classify: game folder");
            Expect(failures, InstallBackupPlanner.Classify("/dev_hdd0/tmp/a", "/dev_hdd0/tmp/a") == InstallTarget.Other, "InstallBackupPlanner.Classify: other");

            Expect(failures, !InstallBackupPlanner.CanInstall(InstallTarget.UsbDevice, ""), "InstallBackupPlanner.CanInstall: usb without device");
            Expect(failures, InstallBackupPlanner.CanInstall(InstallTarget.UsbDevice, "dev_usb000"), "InstallBackupPlanner.CanInstall: usb with device");
            Expect(failures, InstallBackupPlanner.CanInstall(InstallTarget.Other, null), "InstallBackupPlanner.CanInstall: other");

            Expect(failures, InstallBackupPlanner.LocalFileMatchesInstallPath("/x/a.sprx", "a.sprx"), "InstallBackupPlanner.LocalFileMatchesInstallPath: match");
            Expect(failures, !InstallBackupPlanner.LocalFileMatchesInstallPath("/x/a.sprx", "b.sprx"), "InstallBackupPlanner.LocalFileMatchesInstallPath: no match");

            Expect(failures, InstallBackupPlanner.ParentFolder("/dev_hdd0/game/X/a.sprx") == "/dev_hdd0/game/X", "InstallBackupPlanner.ParentFolder: nested");
            Expect(failures, InstallBackupPlanner.ParentFolder("/a.txt") == "/", "InstallBackupPlanner.ParentFolder: root file");
            Expect(failures, InstallBackupPlanner.ParentFolder("a.txt") == "", "InstallBackupPlanner.ParentFolder: no folder");
            Expect(failures, InstallBackupPlanner.ParentFolder("\\a\\b\\c.txt") == "/a/b", "InstallBackupPlanner.ParentFolder: backslashes");

            Expect(failures, InstallBackupPlanner.PlanBackup(false, true, true) == BackupStep.None, "InstallBackupPlanner.PlanBackup: not wanted");
            Expect(failures, InstallBackupPlanner.PlanBackup(true, false, false) == BackupStep.CreateNew, "InstallBackupPlanner.PlanBackup: new");
            Expect(failures, InstallBackupPlanner.PlanBackup(true, true, false) == BackupStep.Redownload, "InstallBackupPlanner.PlanBackup: redownload");
            Expect(failures, InstallBackupPlanner.PlanBackup(true, true, true) == BackupStep.KeepRecord, "InstallBackupPlanner.PlanBackup: keep");

            Expect(failures, InstallBackupPlanner.SizeText(null, true, null) == "No File Exists", "InstallBackupPlanner.SizeText: missing file");
            Expect(failures, InstallBackupPlanner.SizeText(1500, true, delegate (long b) { return "X" + b; }) == "X1500", "InstallBackupPlanner.SizeText: formatted");
            Expect(failures, InstallBackupPlanner.SizeText(1500, false, null).EndsWith(" bytes", StringComparison.Ordinal), "InstallBackupPlanner.SizeText: raw bytes");

            Expect(failures,
                InstallBackupPlanner.BackupLocalPath("root", "BLES", "a.sprx") == Path.Combine(Path.Combine("root", "BLES"), "a.sprx"),
                "InstallBackupPlanner.BackupLocalPath");
            Expect(failures, InstallBackupPlanner.GetLocalSize(null) == null, "InstallBackupPlanner.GetLocalSize: null path");
        }

        private static void CheckBackupDecision(List<string> failures)
        {
            int[] asked = new int[1];
            Func<bool> yes = delegate { asked[0]++; return true; };
            Func<bool> no = delegate { asked[0]++; return false; };

            BackupDecision always = new BackupDecision();
            Expect(failures, always.Evaluate(false, true, null), "BackupDecision: always-backup setting");

            BackupDecision answeredYes = new BackupDecision();
            Expect(failures, answeredYes.Evaluate(false, false, yes), "BackupDecision: user says yes");
            Expect(failures, answeredYes.Evaluate(false, false, yes), "BackupDecision: yes stays yes");
            Expect(failures, answeredYes.Evaluate(true, false, no), "BackupDecision: yes is sticky for files with a record");
            Expect(failures, asked[0] == 1, "BackupDecision: user must be asked only once");

            asked[0] = 0;
            BackupDecision answeredNo = new BackupDecision();
            Expect(failures, !answeredNo.Evaluate(false, false, no), "BackupDecision: user says no");
            Expect(failures, !answeredNo.Evaluate(false, false, yes), "BackupDecision: no is not asked again");
            Expect(failures, asked[0] == 1 && answeredNo.Asked, "BackupDecision: asked once after no");

            asked[0] = 0;
            BackupDecision hasRecord = new BackupDecision();
            Expect(failures, !hasRecord.Evaluate(true, false, yes), "BackupDecision: file with a record does not ask");
            Expect(failures, asked[0] == 0 && !hasRecord.Asked, "BackupDecision: no prompt when a record exists");
        }
    }
}
