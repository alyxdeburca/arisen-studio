using System;
using System.Collections.Generic;
using System.Text;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Services.Extra
{
    /// <summary>
    /// In-memory checks for LaunchIniFile, XboxHdKeyCalculator and GameRegionPlanner. No files, no console, no network.
    /// Call <see cref="Run" /> from a test harness; it returns one line per failed check, or an empty list.
    /// Nothing calls it from the app.
    /// </summary>
    public static class ServicesExtraSelfCheck
    {
        public static List<string> Run()
        {
            List<string> failures = new List<string>();
            CheckIni(failures);
            CheckKey(failures);
            CheckKeyvault(failures);
            CheckRegions(failures);
            return failures;
        }

        private static void Expect(List<string> failures, bool ok, string name)
        {
            if (!ok)
            {
                failures.Add(name);
            }
        }

        private static void CheckIni(List<string> f)
        {
            string text = "; head\r\n[Settings]\r\nliveblock = false ; note\r\nlivestrong=true\r\n\r\n[Plugins]\r\nplugin1 = Hdd:\\a.xex\r\n";
            LaunchIniFile ini = LaunchIniFile.Parse(text);

            Expect(f, ini.SectionNames.Count == 2, "ini: two sections");
            Expect(f, ini.SectionNames[0] == "Settings" && ini.SectionNames[1] == "Plugins", "ini: section order");
            Expect(f, ini.Get("Settings", "liveblock") == "false", "ini: value without trailing comment");
            Expect(f, ini.Get("settings", "LIVESTRONG") == "true", "ini: lookups ignore case");
            Expect(f, ini.GetBool("Settings", "livestrong"), "ini: GetBool true");
            Expect(f, !ini.GetBool("Settings", "missing"), "ini: GetBool missing is false");
            Expect(f, ini.Get("Plugins", "plugin1") == "Hdd:\\a.xex", "ini: path value kept");
            Expect(f, ini.NewLine == "\r\n", "ini: CRLF detected");

            ini.SetBool("Settings", "liveblock", true);
            string written = ini.ToText();
            Expect(f, written.Contains("liveblock = true ; note"), "ini: trailing comment kept after Set");
            Expect(f, written.StartsWith("; head\r\n[Settings]\r\n"), "ini: comment above header kept");
            Expect(f, written.Contains("\r\n\r\n[Plugins]\r\n"), "ini: blank line before second header");

            LaunchIniFile again = LaunchIniFile.Parse(written);
            Expect(f, again.Get("Settings", "liveblock") == "true", "ini: round trip value");
            Expect(f, again.ToText() == written, "ini: text is stable after a second parse");

            ini.Set("Paths", "default", "Hdd:\\Default.xex");
            Expect(f, ini.HasSection("Paths") && ini.Get("Paths", "default") == "Hdd:\\Default.xex", "ini: Set creates a section and key");
            Expect(f, ini.Remove("Paths", "default") && ini.Get("Paths", "default") == null, "ini: Remove");
            Expect(f, LaunchIniFile.Parse(string.Empty).SectionNames.Count == 0, "ini: empty text");
        }

        private static void CheckKey(List<string> f)
        {
            Expect(f, XboxHdKeyCalculator.IsValidConsoleSerial("123456789012"), "key: console serial ok");
            Expect(f, !XboxHdKeyCalculator.IsValidConsoleSerial("12345678901"), "key: console serial too short");
            Expect(f, !XboxHdKeyCalculator.IsValidConsoleSerial("12345678901A"), "key: console serial letter");
            Expect(f, XboxHdKeyCalculator.IsValidMotherboardSerial("0102030405060AbF"), "key: motherboard serial ok");
            Expect(f, !XboxHdKeyCalculator.IsValidMotherboardSerial("0102030405060AbG"), "key: motherboard serial bad char");

            string key = XboxHdKeyCalculator.MakeKey("123456789012", "0102030405060708");
            Expect(f, key == "00000000010203040506070831323334", "key: layout");
            Expect(f, XboxHdKeyCalculator.MakeKey("123", "0102030405060708") == string.Empty, "key: invalid gives empty");

            string report = XboxHdKeyCalculator.BuildReport("123456789012", "0a0b0c0d0e0f1011", true);
            Expect(f, report != null && report.Contains("Motherboard serial: 0A0B0C0D0E0F1011"), "key: report upper-cases the serial");
            Expect(f, report != null && report.EndsWith("XboxHDKey: " + XboxHdKeyCalculator.MakeKey("123456789012", "0a0b0c0d0e0f1011")), "key: report ends with the key");
            Expect(f, XboxHdKeyCalculator.BuildReport("x", "y", false) == null, "key: report null for bad serials");
        }

        private static void CheckKeyvault(List<string> f)
        {
            foreach (int size in new[] { XboxHdKeyCalculator.KeyvaultSizeA, XboxHdKeyCalculator.KeyvaultSizeB })
            {
                int shift = size == XboxHdKeyCalculator.KeyvaultSizeA ? 0 : 0x10;
                byte[] data = new byte[size];
                byte[] serial = Encoding.ASCII.GetBytes("123456789012");
                Array.Copy(serial, 0, data, 0xA0 + shift, serial.Length);
                for (int i = 0; i < 8; i++)
                {
                    data[0xB0 + shift + i] = (byte)(0x10 + i);
                }

                data[0xB8 + shift] = 0x00; // NTSC-U, big endian 0x00FF
                data[0xB8 + shift + 1] = 0xFF;

                KeyvaultInfo info;
                KeyvaultImportResult result = XboxHdKeyCalculator.ReadKeyvault(data, out info);
                Expect(f, result == KeyvaultImportResult.Success, "keyvault: success " + size);
                Expect(f, info != null && info.ConsoleSerial == "123456789012", "keyvault: console serial " + size);
                Expect(f, info != null && info.MotherboardSerial == "1011121314151617", "keyvault: motherboard serial " + size);

                data[0xB8 + shift] = 0x12;
                Expect(f, XboxHdKeyCalculator.ReadKeyvault(data, out info) == KeyvaultImportResult.BadRegion, "keyvault: bad region " + size);
            }

            KeyvaultInfo none;
            Expect(f, XboxHdKeyCalculator.ReadKeyvault(new byte[100], out none) == KeyvaultImportResult.BadSize, "keyvault: bad size");
            Expect(f, XboxHdKeyCalculator.ReadKeyvault(null, out none) == KeyvaultImportResult.FileAccess, "keyvault: null data");
        }

        private static void CheckRegions(List<string> f)
        {
            Expect(f, GameRegionPlanner.ProbePath("BLES01807") == "/dev_hdd0/game/BLES01807", "region: probe path");

            List<GameRegionProbe> probes = GameRegionPlanner.PlanDetection(new List<KeyValuePair<string, string[]>>
            {
                new KeyValuePair<string, string[]>("g1", new[] { "BLUS1", "BLES1" }),
                new KeyValuePair<string, string[]>("g2", new[] { "BLUS2" })
            });
            Expect(f, probes.Count == 3 && probes[2].GameId == "g2" && probes[1].ConsolePath == "/dev_hdd0/game/BLES1", "region: detection plan");

            List<GameRegion> saved = new List<GameRegion>();
            int applied = GameRegionPlanner.ApplyDetected(saved, new[] { probes[0], probes[1], probes[2] }, false);
            Expect(f, applied == 3 && saved.Count == 2, "region: two games saved");
            Expect(f, saved[0].Region == "BLES1", "region: last hit for a game wins");
            Expect(f, GameRegionPlanner.FindRemembered(saved, "G1", true) == "BLES1", "region: remembered ignores case");
            Expect(f, GameRegionPlanner.FindRemembered(saved, "g1", false) == null, "region: not remembered when off");
            Expect(f, GameRegionPlanner.RemoveAt(saved, 0) && !GameRegionPlanner.RemoveAt(saved, 5), "region: RemoveAt bounds");

            GameRegionRow bad = GameRegionPlanner.FindFirstInvalid(new[]
            {
                new GameRegionRow { GameId = "g1", GameTitle = "One", Region = "BLES1", SupportedRegions = new[] { "BLES1" } },
                new GameRegionRow { GameId = "g2", GameTitle = "Two", Region = "BLJM2", SupportedRegions = new[] { "BLUS2" } }
            });
            Expect(f, bad != null && bad.GameId == "g2", "region: first invalid row");
            Expect(f, GameRegionPlanner.InvalidRegionMessage(bad).StartsWith("Game Region: BLJM2 is not supported for Game Title: Two"), "region: invalid message");

            string[] supported = { "BLUS2", "BLES2" };
            string[] folders = { "BLUS2", "NPEA00001" };
            GameRegionDecision ok = GameRegionPlanner.CheckInstallRegion(true, "BLUS2", true, supported, folders, true, false);
            Expect(f, ok.CanInstall && ok.Region == "BLUS2" && ok.RememberRegion, "region: install ok");
            GameRegionDecision none = GameRegionPlanner.CheckInstallRegion(true, "", true, supported, folders, true, false);
            Expect(f, !none.CanInstall && none.Failure == GameRegionFailure.NoRegionSelected, "region: none selected");
            GameRegionDecision unsupported = GameRegionPlanner.CheckInstallRegion(true, "BLJM2", true, supported, folders, true, false);
            Expect(f, !unsupported.CanInstall && unsupported.Failure == GameRegionFailure.NotSupported, "region: unsupported");
            GameRegionDecision noFolder = GameRegionPlanner.CheckInstallRegion(true, "BLES2", true, supported, folders, true, false);
            Expect(f, !noFolder.CanInstall && noFolder.Failure == GameRegionFailure.NoRegionFolder, "region: folder missing cancels");
            GameRegionDecision legacy = GameRegionPlanner.CheckInstallRegion(true, "BLUS2", true, supported, folders, true, true);
            Expect(f, !legacy.CanInstall && legacy.Failure == GameRegionFailure.NoRegionFolder, "region: legacy inverted check cancels when found");
            GameRegionDecision notNeeded = GameRegionPlanner.CheckInstallRegion(false, null, false, null, null, true, false);
            Expect(f, notNeeded.CanInstall && notNeeded.Region.Length == 0, "region: not required");
        }
    }
}
