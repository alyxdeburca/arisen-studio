using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArisenStudio.Services
{
    /// <summary>
    /// In-memory checks for the pure parts of the Services folder (ParamSfoReader, ConsolePaths).
    /// No files, no network, no console. Call <see cref="Run" /> from a diagnostic or test entry point;
    /// an empty list means every check passed. Optional: the app does not need this file to run.
    /// </summary>
    public static class ServicesSelfCheck
    {
        public static List<string> Run()
        {
            List<string> failures = new List<string>();

            CheckParamSfo(failures);
            CheckConsolePaths(failures);

            return failures;
        }

        private static void Expect(List<string> failures, bool condition, string what)
        {
            if (!condition)
            {
                failures.Add(what);
            }
        }

        private static void CheckParamSfo(List<string> failures)
        {
            byte[] sfo = BuildSfo("Test Game", "BLES00000", 0x2A);

            ParamSfoReader reader = ParamSfoReader.TryParse(sfo);
            Expect(failures, reader != null, "ParamSfoReader: valid file did not parse");

            if (reader != null)
            {
                Expect(failures, reader.Title == "Test Game", "ParamSfoReader: Title mismatch");
                Expect(failures, reader.TitleId == "BLES00000", "ParamSfoReader: TitleId mismatch");

                int attribute;
                Expect(failures, reader.TryGetInt("ATTRIBUTE", out attribute) && attribute == 0x2A, "ParamSfoReader: int value mismatch");
                Expect(failures, reader.GetString("MISSING") == null, "ParamSfoReader: missing key should be null");
            }

            Expect(failures, ParamSfoReader.TryParse(null) == null, "ParamSfoReader: null should give null");
            Expect(failures, ParamSfoReader.TryParse(new byte[4]) == null, "ParamSfoReader: short input should give null");
            Expect(failures, ParamSfoReader.TryParse(new byte[64]) == null, "ParamSfoReader: bad magic should give null");

            byte[] truncated = new byte[sfo.Length - 10];
            System.Array.Copy(sfo, truncated, truncated.Length);
            Expect(failures, ParamSfoReader.TryParse(truncated) == null, "ParamSfoReader: truncated data table should give null");
        }

        private static void CheckConsolePaths(List<string> failures)
        {
            Expect(failures, ConsolePaths.GetDirectory("/dev_hdd0/tmp/a.txt") == "/dev_hdd0/tmp/", "ConsolePaths.GetDirectory: plain path");
            Expect(failures, ConsolePaths.GetDirectory("\\dev_hdd0\\tmp\\a.txt") == "/dev_hdd0/tmp/", "ConsolePaths.GetDirectory: backslashes");
            Expect(failures, ConsolePaths.GetDirectory("/a.txt") == "/", "ConsolePaths.GetDirectory: root file");
            Expect(failures, ConsolePaths.GetDirectory("a.txt") == "/", "ConsolePaths.GetDirectory: no folder");

            Expect(failures, ConsolePaths.Library(null, ConsolePaths.GamesBd) == "/dev_hdd0/GAMES/", "ConsolePaths.Library: internal");
            Expect(failures, ConsolePaths.Library("dev_usb001", ConsolePaths.GamesIso) == "/dev_usb001/PS3ISO/", "ConsolePaths.Library: usb");

            Expect(failures, ConsolePaths.DeviceRoots(false).Count == 1, "ConsolePaths.DeviceRoots: internal only");
            Expect(failures, ConsolePaths.DeviceRoots(true).Count == 1 + ConsolePaths.UsbPorts.Length, "ConsolePaths.DeviceRoots: with usb");

            Expect(failures, ConsolePaths.FindUsbPort(new string[] { "dev_hdd0", "dev_usb002", "dev_usb001" }) == "dev_usb001", "ConsolePaths.FindUsbPort: probe order");
            Expect(failures, ConsolePaths.FindUsbPort(new string[] { "dev_hdd0" }) == null, "ConsolePaths.FindUsbPort: none");
            Expect(failures, ConsolePaths.LocalUserNameFile("00000001") == "/dev_hdd0/home/00000001/localusername", "ConsolePaths.LocalUserNameFile");
        }

        /// <summary>
        /// Build a small PARAM.SFO in memory: ATTRIBUTE (int), TITLE and TITLE_ID (NUL-terminated UTF-8).
        /// </summary>
        private static byte[] BuildSfo(string title, string titleId, int attribute)
        {
            byte[] titleBytes = Encoding.UTF8.GetBytes(title);
            byte[] idBytes = Encoding.UTF8.GetBytes(titleId);

            const string keyTable = "ATTRIBUTE\0TITLE\0TITLE_ID\0";
            int keyTableLength = keyTable.Length;
            int keyTablePadded = (keyTableLength + 3) & ~3;

            int entryCount = 3;
            int keyTableStart = 20 + entryCount * 16;
            int dataTableStart = keyTableStart + keyTablePadded;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Header
                writer.Write(new byte[] { 0x00, 0x50, 0x53, 0x46 });
                writer.Write(0x0101);
                writer.Write(keyTableStart);
                writer.Write(dataTableStart);
                writer.Write(entryCount);

                // Index: ATTRIBUTE at key 0, data 0
                writer.Write((ushort)0);
                writer.Write((ushort)0x0404);
                writer.Write(4);
                writer.Write(4);
                writer.Write(0);

                // Index: TITLE at key 10, data 4
                writer.Write((ushort)10);
                writer.Write((ushort)0x0204);
                writer.Write(titleBytes.Length + 1);
                writer.Write(0x80);
                writer.Write(4);

                // Index: TITLE_ID at key 16, data 4 + 0x80
                writer.Write((ushort)16);
                writer.Write((ushort)0x0204);
                writer.Write(idBytes.Length + 1);
                writer.Write(0x10);
                writer.Write(4 + 0x80);

                // Key table (padded)
                byte[] keyBytes = Encoding.ASCII.GetBytes(keyTable);
                writer.Write(keyBytes);
                writer.Write(new byte[keyTablePadded - keyTableLength]);

                // Data table
                writer.Write(attribute);

                byte[] titleField = new byte[0x80];
                System.Array.Copy(titleBytes, titleField, titleBytes.Length);
                writer.Write(titleField);

                byte[] idField = new byte[0x10];
                System.Array.Copy(idBytes, idField, idBytes.Length);
                writer.Write(idField);

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
