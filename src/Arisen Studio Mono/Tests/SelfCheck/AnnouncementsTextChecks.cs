#if SELFCHECK
using System.Collections.Generic;
using ArisenStudio.Services.Adapters;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Self-checks for AnnouncementsText.Decode (announcements.json bytes to text).
    /// Every input is a byte array built in memory: no network, no file, no console, no window.
    /// The expected text is written with \u escapes, so this file stays ASCII.
    /// </summary>
    internal static class AnnouncementsTextChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("AnnouncementsText.Utf8", CheckUtf8));
            cases.Add(new SelfCheckCase("AnnouncementsText.Cp1252", CheckCp1252));
            cases.Add(new SelfCheckCase("AnnouncementsText.UndefinedBytes", CheckUndefinedBytes));
            cases.Add(new SelfCheckCase("AnnouncementsText.Bom", CheckBom));
        }

        // Valid UTF-8: E2 80 99 is U+2019. Read as cp1252 it would be three characters, so a pass also shows the UTF-8 path ran.
        private static void CheckUtf8()
        {
            byte[] data = { 0x41, 0xE2, 0x80, 0x99, 0x42 };
            SelfCheckAssert.Equal("A\u2019B", AnnouncementsText.Decode(data), "UTF-8 input");
        }

        // 0x92 and 0x96 are not valid UTF-8 (lone continuation bytes), so the cp1252 path must run: 0x92 is U+2019, 0x96 is U+2013.
        private static void CheckCp1252()
        {
            byte[] data = { 0x41, 0x92, 0x42, 0x96, 0x43 };
            SelfCheckAssert.Equal("A\u2019B\u2013C", AnnouncementsText.Decode(data), "cp1252 0x92 and 0x96");
        }

        // 0x81, 0x8D, 0x8F, 0x90 and 0x9D are undefined in cp1252. Each must decode to the code point with the same value, and not throw.
        private static void CheckUndefinedBytes()
        {
            byte[] undefined = { 0x81, 0x8D, 0x8F, 0x90, 0x9D };
            foreach (byte b in undefined)
            {
                string label = "0x" + ((int)b).ToString("X2");
                SelfCheckAssert.Equal(((char)b).ToString(), AnnouncementsText.Decode(new byte[] { b }), "undefined byte alone " + label);
                SelfCheckAssert.Equal("A" + (char)b, AnnouncementsText.Decode(new byte[] { 0x41, b }), "undefined byte after A " + label);
            }
        }

        // A leading EF BB BF is removed, on the UTF-8 path and on the cp1252 path.
        private static void CheckBom()
        {
            SelfCheckAssert.Equal("A\u2019", AnnouncementsText.Decode(new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0xE2, 0x80, 0x99 }), "BOM then UTF-8");
            SelfCheckAssert.Equal("\u2019", AnnouncementsText.Decode(new byte[] { 0xEF, 0xBB, 0xBF, 0x92 }), "BOM then cp1252 0x92");
            SelfCheckAssert.Equal("", AnnouncementsText.Decode(new byte[] { 0xEF, 0xBB, 0xBF }), "BOM only");
        }
    }
}
#endif
