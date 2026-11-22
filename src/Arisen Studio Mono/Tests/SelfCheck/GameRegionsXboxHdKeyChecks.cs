#if SELFCHECK
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using GameRegionsForm = ArisenStudio.Forms.Tools.PS3.GameRegions;
using XboxHDKeyForm = ArisenStudio.Forms.Tools.XBOX.XboxHDKey;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Checks for the BUILD-REQUEST-25 port into the Tools GameRegions and XboxHDKey forms. The forms are constructed and
    /// never shown. Private members are reached by reflection. No check sets the XboxHDKey serial text boxes, because the
    /// TextChanged handlers open a message box on partial input (a known upstream behaviour kept by the port).
    /// </summary>
    internal static class GameRegionsXboxHdKeyChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("GameRegions.FindCategory", FindCategory));
            cases.Add(new SelfCheckCase("GameRegions.FindCategoryNullEntry", FindCategoryNullEntry));
            cases.Add(new SelfCheckCase("GameRegions.TitleIndexGuard", TitleIndexGuard));
            cases.Add(new SelfCheckCase("GameRegions.AddByIndex", AddByIndex));
            cases.Add(new SelfCheckCase("XboxHDKey.MakeKey", MakeKey));
            cases.Add(new SelfCheckCase("XboxHDKey.MakeKeyInvalid", MakeKeyInvalid));
            cases.Add(new SelfCheckCase("XboxHDKey.Validators", Validators));
            cases.Add(new SelfCheckCase("XboxHDKey.ImportKeyvault", ImportKeyvault));
            cases.Add(new SelfCheckCase("XboxHDKey.ImportRejects", ImportRejects));
            cases.Add(new SelfCheckCase("XboxHDKey.ImportMissingFile", ImportMissingFile));
            cases.Add(new SelfCheckCase("XboxHDKey.WriteCatchSource", WriteCatchSource));
        }

        // ---- GameRegions ----

        private static CategoryItem Game(string id, string title, params string[] regions)
        {
            return new CategoryItem { Id = id, Title = title, Type = "game", Regions = regions };
        }

        private static CategoriesData Data(params CategoryItem[] items)
        {
            return new CategoriesData { Categories = new List<CategoryItem>(items) };
        }

        private static void FindCategory()
        {
            using (GameRegionsForm form = new GameRegionsForm())
            {
                form.Settings = new SettingsData();

                // No categories at all: not found, no throw.
                form.Categories = null;
                SelfCheckAssert.True(SelfCheckReflect.Call(form, "FindCategory", "BLUS30001") == null, "null Categories should give null");
                form.Categories = new CategoriesData { Categories = null };
                SelfCheckAssert.True(SelfCheckReflect.Call(form, "FindCategory", "BLUS30001") == null, "null Categories.Categories should give null");

                CategoryItem first = Game("BLUS30001", "First Game", "USA", "EUR");
                CategoryItem second = Game("BLES00002", "Second Game", "EUR");
                form.Categories = Data(first, second);

                SelfCheckAssert.True(SelfCheckReflect.Call(form, "FindCategory", (string)null) == null, "null id should give null");
                SelfCheckAssert.True(SelfCheckReflect.Call(form, "FindCategory", "") == null, "empty id should give null");
                SelfCheckAssert.True(SelfCheckReflect.Call(form, "FindCategory", "NOPE00000") == null, "unknown id should give null");
                SelfCheckAssert.True(object.ReferenceEquals(SelfCheckReflect.Call(form, "FindCategory", "BLUS30001"), first), "exact id should find the first item");
                SelfCheckAssert.True(object.ReferenceEquals(SelfCheckReflect.Call(form, "FindCategory", "bles00002"), second), "lookup should ignore case");
            }
        }

        private static void FindCategoryNullEntry()
        {
            using (GameRegionsForm form = new GameRegionsForm())
            {
                form.Settings = new SettingsData();
                // BUILD-REQUEST-25: a null entry in the list throws inside the lookup; the guard turns that into "not found".
                form.Categories = Data(null, Game("BLUS30001", "First Game", "USA"));
                object result = SelfCheckReflect.Call(form, "FindCategory", "BLUS30001");
                SelfCheckAssert.True(result == null, "a null list entry should make the lookup return null, not throw");
            }
        }

        private static void TitleIndexGuard()
        {
            using (GameRegionsForm form = new GameRegionsForm())
            {
                form.Settings = new SettingsData();
                CategoryItem noRegions = new CategoryItem { Id = "BLUS30003", Title = "No Regions", Type = "game", Regions = null };
                form.Categories = Data(
                    Game("BLUS30001", "First Game", "USA", "EUR"),
                    Game("BLES00002", "Second Game", "EUR"),
                    noRegions,
                    new CategoryItem { Id = "HB0001", Title = "A Homebrew", Type = "homebrew", Regions = new[] { "ANY" } });

                SelfCheckReflect.Call(form, "LoadGameTitles");
                ComboBox title = SelfCheckReflect.GetField<ComboBox>(form, "_comboTitle");
                ComboBox region = SelfCheckReflect.GetField<ComboBox>(form, "_comboRegion");
                List<CategoryItem> list = SelfCheckReflect.GetField<List<CategoryItem>>(form, "_gameCategories");

                SelfCheckAssert.Equal(3, title.Items.Count, "only game categories fill the title list");
                SelfCheckAssert.Equal(3, list.Count, "the category list stays in step with the title list");

                // In step: index 0 lists its two regions, index 2 (null Regions) lists none.
                title.SelectedIndex = 0;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                SelfCheckAssert.Equal(2, region.Items.Count, "regions of the first title");
                SelfCheckAssert.True(region.Enabled, "region combo should be enabled with a title selected");

                title.SelectedIndex = 2;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                SelfCheckAssert.Equal(0, region.Items.Count, "null Regions should list no regions");

                // Guard: the combo has an index the category list does not (the two got out of step). No throw, no regions.
                list.Clear();
                title.SelectedIndex = 1;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                SelfCheckAssert.Equal(0, region.Items.Count, "an out-of-range index should list no regions");

                // Index -1 clears and disables.
                title.SelectedIndex = -1;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                SelfCheckAssert.Equal(0, region.Items.Count, "no selection should list no regions");
                SelfCheckAssert.True(!region.Enabled, "region combo should be disabled with no title selected");
            }
        }

        private static void AddByIndex()
        {
            using (GameRegionsForm form = new GameRegionsForm())
            {
                SettingsData settings = new SettingsData();
                form.Settings = settings;
                form.Categories = Data(Game("BLUS30001", "First Game", "USA", "EUR"), Game("BLES00002", "Second Game", "EUR"));

                SelfCheckReflect.Call(form, "LoadGameTitles");
                ComboBox title = SelfCheckReflect.GetField<ComboBox>(form, "_comboTitle");
                ComboBox region = SelfCheckReflect.GetField<ComboBox>(form, "_comboRegion");

                title.SelectedIndex = 1;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                SelfCheckAssert.Equal(1, region.Items.Count, "regions of the second title");
                region.SelectedIndex = 0;

                // A valid index and region: the guarded Add stores the region under the second title's id.
                // (The out-of-step branch of Add opens a message box, so it is not run here.)
                SelfCheckReflect.Call(form, "ButtonAdd_Click", null, EventArgs.Empty);
                SelfCheckAssert.Equal(1, settings.GameRegionsPs3.Count, "one saved region after Add");
                SelfCheckAssert.Equal("BLES00002", settings.GameRegionsPs3[0].GameId, "saved game id");
                SelfCheckAssert.Equal("EUR", settings.GameRegionsPs3[0].Region, "saved region");

                ListView list = SelfCheckReflect.GetField<ListView>(form, "_listRegions");
                SelfCheckAssert.Equal(1, list.Items.Count, "saved list rebuilt after Add");
                SelfCheckAssert.Equal("Second Game", list.Items[0].Text, "saved list shows the title, found through the lookup");
                SelfCheckAssert.Equal("EUR", list.Items[0].SubItems[1].Text, "saved list shows the region");

                // A second title adds a second row.
                title.SelectedIndex = 0;
                SelfCheckReflect.Call(form, "ComboTitle_SelectedIndexChanged", null, EventArgs.Empty);
                region.SelectedIndex = 1;
                SelfCheckReflect.Call(form, "ButtonAdd_Click", null, EventArgs.Empty);
                SelfCheckAssert.Equal(2, settings.GameRegionsPs3.Count, "second title adds a second row");
                SelfCheckAssert.Equal("EUR", settings.GameRegionsPs3[1].Region, "second saved region");
            }
        }

        // ---- XboxHDKey ----

        private static string Key(string consoleSerial, string motherboardSerial)
        {
            return (string)SelfCheckReflect.CallStatic(typeof(XboxHDKeyForm), "MakeXboxHDKey", consoleSerial, motherboardSerial);
        }

        private static void MakeKey()
        {
            // Layout: 16 bytes, motherboard serial bytes at 4..11, first 4 console serial ASCII bytes at 12..15, hex upper case.
            SelfCheckAssert.Equal("00000000010203040506070831323334", Key("123456789012", "0102030405060708"), "plain key");
            SelfCheckAssert.Equal("000000000A0B0C0D0E0F101131323334", Key("123456789012", "0a0b0c0d0e0f1011"), "lower case hex input");
            SelfCheckAssert.Equal("00000000FFFFFFFFFFFFFFFF30303030", Key("000099998888", "FFFFFFFFFFFFFFFF"), "all 0xFF motherboard bytes");
        }

        private static void MakeKeyInvalid()
        {
            SelfCheckAssert.Equal(string.Empty, Key("12345678901", "0102030405060708"), "11 character console serial");
            SelfCheckAssert.Equal(string.Empty, Key("1234567890123", "0102030405060708"), "13 character console serial");
            SelfCheckAssert.Equal(string.Empty, Key("12345678901A", "0102030405060708"), "non-numeric console serial");
            SelfCheckAssert.Equal(string.Empty, Key("123456789012", "010203040506070"), "15 character motherboard serial");
            SelfCheckAssert.Equal(string.Empty, Key("123456789012", "010203040506070G"), "non-hex motherboard serial");
            SelfCheckAssert.Equal(string.Empty, Key(null, "0102030405060708"), "null console serial");
            SelfCheckAssert.Equal(string.Empty, Key("123456789012", null), "null motherboard serial");
            SelfCheckAssert.Equal(string.Empty, Key("", ""), "empty serials");
        }

        private static bool Is(string method, string value)
        {
            return (bool)SelfCheckReflect.CallStatic(typeof(XboxHDKeyForm), method, value);
        }

        private static void Validators()
        {
            SelfCheckAssert.True(Is("IsNumeric", "0123456789"), "IsNumeric digits");
            SelfCheckAssert.True(!Is("IsNumeric", "12a4"), "IsNumeric letter");
            SelfCheckAssert.True(!Is("IsNumeric", "12 4"), "IsNumeric space");
            SelfCheckAssert.True(!Is("IsNumeric", ""), "IsNumeric empty");
            SelfCheckAssert.True(!Is("IsNumeric", null), "IsNumeric null");
            SelfCheckAssert.True(Is("IsHexadecimal", "0aF9bC"), "IsHexadecimal mixed case");
            SelfCheckAssert.True(!Is("IsHexadecimal", "0aG9"), "IsHexadecimal G");
            SelfCheckAssert.True(!Is("IsHexadecimal", ""), "IsHexadecimal empty");
            SelfCheckAssert.True(!Is("IsHexadecimal", null), "IsHexadecimal null");
        }

        private static string NewTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "arisen-selfcheck-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        // Builds a synthetic keyvault: console serial at 0xA0, motherboard serial at 0xB0, big-endian region code at 0xB8
        // (every offset moves up by 0x10 for the 0x4000 byte layout).
        private static byte[] BuildKeyvault(int size, string consoleSerial, byte[] motherboardSerial, ushort region)
        {
            byte[] buffer = new byte[size];
            int shift = size == 0x3FF0 ? 0 : 0x10;
            for (int i = 0; i < consoleSerial.Length; i++)
            {
                buffer[0xA0 + shift + i] = (byte)consoleSerial[i];
            }
            Array.Copy(motherboardSerial, 0, buffer, 0xB0 + shift, 8);
            buffer[0xB8 + shift] = (byte)(region >> 8);
            buffer[0xB9 + shift] = (byte)(region & 0xFF);
            return buffer;
        }

        private static string Import(string path, out string consoleSerial, out string motherboardSerial)
        {
            object[] args = new object[] { path, null, null };
            object result = SelfCheckReflect.CallStatic(typeof(XboxHDKeyForm), "ImportXboxHDKey", args);
            consoleSerial = (string)args[1];
            motherboardSerial = (string)args[2];
            return result.ToString();
        }

        private static void ImportKeyvault()
        {
            byte[] board = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
            string folder = NewTempFolder();
            try
            {
                // Both accepted sizes, and each of the four accepted region codes.
                ushort[] regions = new ushort[] { 0x00FF, 0x01FF, 0x01FC, 0x02FE };
                int[] sizes = new int[] { 0x3FF0, 0x4000 };
                foreach (int size in sizes)
                {
                    foreach (ushort region in regions)
                    {
                        string path = Path.Combine(folder, "kv-" + size.ToString("X") + "-" + region.ToString("X4") + ".bin");
                        File.WriteAllBytes(path, BuildKeyvault(size, "123456789012", board, region));

                        string consoleSerial;
                        string motherboardSerial;
                        string outcome = Import(path, out consoleSerial, out motherboardSerial);
                        string label = "size 0x" + size.ToString("X") + " region 0x" + region.ToString("X4");
                        SelfCheckAssert.Equal("Success", outcome, "result for " + label);
                        SelfCheckAssert.Equal("123456789012", consoleSerial, "console serial for " + label);
                        SelfCheckAssert.Equal("0123456789ABCDEF", motherboardSerial, "motherboard serial for " + label);
                    }
                }
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        private static void ImportRejects()
        {
            byte[] board = new byte[8];
            string folder = NewTempFolder();
            try
            {
                string consoleSerial;
                string motherboardSerial;

                string badSize = Path.Combine(folder, "short.bin");
                File.WriteAllBytes(badSize, new byte[100]);
                SelfCheckAssert.Equal("ErrorFileSize", Import(badSize, out consoleSerial, out motherboardSerial), "100 byte file");
                SelfCheckAssert.Equal(string.Empty, consoleSerial, "serials stay empty on a size error");

                string badRegion = Path.Combine(folder, "region.bin");
                File.WriteAllBytes(badRegion, BuildKeyvault(0x3FF0, "123456789012", board, 0x1234));
                SelfCheckAssert.Equal("ErrorBadRegion", Import(badRegion, out consoleSerial, out motherboardSerial), "unknown region code");
                SelfCheckAssert.Equal(string.Empty, motherboardSerial, "serials stay empty on a region error");

                // A region code that is right but little endian (bytes swapped) must be rejected too.
                string swapped = Path.Combine(folder, "swapped.bin");
                byte[] swappedBytes = BuildKeyvault(0x3FF0, "123456789012", board, 0xFF00);
                File.WriteAllBytes(swapped, swappedBytes);
                SelfCheckAssert.Equal("ErrorBadRegion", Import(swapped, out consoleSerial, out motherboardSerial), "byte-swapped region code");

                SelfCheckAssert.Equal("ErrorFileAccess", Import(string.Empty, out consoleSerial, out motherboardSerial), "empty path");
                SelfCheckAssert.Equal("ErrorFileAccess", Import(null, out consoleSerial, out motherboardSerial), "null path");
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        // Reading the source of ImportXboxHDKey (FileInfo.Length is outside its try block), a missing file is expected to
        // throw FileNotFoundException instead of returning ErrorFileAccess. This check asserts the stated intent of the
        // ImportResult.ErrorFileAccess value; it is expected to FAIL until the call is moved inside the try.
        private static void ImportMissingFile()
        {
            string folder = NewTempFolder();
            try
            {
                string consoleSerial;
                string motherboardSerial;
                string path = Path.Combine(folder, "does-not-exist.bin");
                SelfCheckAssert.Equal("ErrorFileAccess", Import(path, out consoleSerial, out motherboardSerial), "missing file");
            }
            finally
            {
                DeleteFolder(folder);
            }
        }

        // The Save File click path shows dialogs, so the write-file catch cannot be run here. This reads the source and
        // checks its shape: File.WriteAllText inside a try, a catch that logs and shows the error text and then returns
        // before the success message box.
        private static void WriteCatchSource()
        {
            string path = FindSource("Forms/Tools/XBOX/XboxHDKey.cs");
            string source = File.ReadAllText(path).Replace("\r\n", "\n");

            int write = source.IndexOf("File.WriteAllText(", StringComparison.Ordinal);
            SelfCheckAssert.True(write > 0, "File.WriteAllText call not found");

            string before = source.Substring(Math.Max(0, write - 120), Math.Min(120, write));
            SelfCheckAssert.True(before.LastIndexOf("try", StringComparison.Ordinal) >= 0, "File.WriteAllText is not inside a try block");

            int handler = source.IndexOf("catch (Exception", write, StringComparison.Ordinal);
            SelfCheckAssert.True(handler > write, "no catch (Exception ...) after File.WriteAllText");
            SelfCheckAssert.True(handler - write < 200, "the catch is not next to File.WriteAllText");

            int leave = source.IndexOf("return;", handler, StringComparison.Ordinal);
            SelfCheckAssert.True(leave > handler, "the catch does not return");

            string body = source.Substring(handler, leave - handler);
            SelfCheckAssert.True(body.Contains("AppHost.Logger"), "the catch does not log through AppHost.Logger");
            SelfCheckAssert.True(body.Contains("Unable to write the XboxHDKey file"), "the catch does not show the write error text");
            SelfCheckAssert.True(body.Contains("MessageBox.Show"), "the catch does not show a message box");

            int success = source.IndexOf("Successfully created XboxHDKey file", write, StringComparison.Ordinal);
            SelfCheckAssert.True(success > leave, "the success message is not after the catch return");
        }

        private static string FindSource(string relative)
        {
            List<string> starts = new List<string>();
            if (SelfCheckRunner.SourceRoot != null)
            {
                starts.Add(SelfCheckRunner.SourceRoot);
            }
            starts.Add(AppDomain.CurrentDomain.BaseDirectory);
            starts.Add(Directory.GetCurrentDirectory());

            foreach (string start in starts)
            {
                string folder = start;
                for (int depth = 0; depth < 8 && !string.IsNullOrEmpty(folder); depth++)
                {
                    string candidate = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                    string nested = Path.Combine(Path.Combine(folder, "Arisen Studio Mono"), relative.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(nested))
                    {
                        return nested;
                    }
                    folder = Path.GetDirectoryName(folder);
                }
            }

            throw new SelfCheckFailure("source file " + relative + " not found; pass --source-root <Arisen Studio Mono folder>");
        }

        // Removes only the temporary folder this check created.
        private static void DeleteFolder(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception)
            {
                // A leftover temp folder does not change the result.
            }
        }
    }
}
#endif
