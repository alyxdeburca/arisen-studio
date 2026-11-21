using System;
using System.IO;
using System.Text;

namespace ArisenStudio.Services.Extra
{
    /// <summary>
    /// Result of reading a keyvault file (the old IMPORT_RESULT, minus the success value that was named ERROR_SUCCESS).
    /// </summary>
    public enum KeyvaultImportResult
    {
        Success,

        /// <summary>The file could not be opened or read in full.</summary>
        FileAccess,

        /// <summary>The size is not 0x3FF0 or 0x4000 bytes.</summary>
        BadSize,

        /// <summary>The region code is not NTSC-U, NTSC-J, NTSC-K or PAL. The keyvault is probably still encrypted.</summary>
        BadRegion
    }

    /// <summary>
    /// Serials read out of a keyvault file.
    /// </summary>
    public sealed class KeyvaultInfo
    {
        /// <summary>The 12 serial characters at 0xA0 (0xB0 in a 0x4000 byte file), as text.</summary>
        public string ConsoleSerial { get; set; }

        /// <summary>The 8 serial bytes at 0xB0 (0xC0 in a 0x4000 byte file) as 16 upper case hex digits.</summary>
        public string MotherboardSerial { get; set; }

        /// <summary>The big-endian region code that was accepted.</summary>
        public ushort RegionCode { get; set; }
    }

    /// <summary>
    /// The Xbox 360 hard drive key rules from the old XboxHDKey tool, with no form attached:
    /// serial checks, the 16-byte key layout, keyvault reading and the text file that "Save" wrote.
    /// Needs no console and no network.
    /// </summary>
    public static class XboxHdKeyCalculator
    {
        public const int ConsoleSerialLength = 12;
        public const int MotherboardSerialLength = 16;
        public const int KeyLength = 0x10;

        public const int KeyvaultSizeA = 0x3FF0;
        public const int KeyvaultSizeB = 0x4000;

        private const int ConsoleSerialOffset = 0xA0;
        private const int MotherboardSerialOffset = 0xB0;
        private const int MotherboardSerialSize = 8;
        private const int GameRegionOffset = 0xB8;

        // The 0x4000 byte layout has the same fields 0x10 bytes further on.
        private const int LargeFileShift = 0x10;

        private const ushort RegionNtscU = 0x00FF;
        private const ushort RegionNtscJ = 0x01FF;
        private const ushort RegionNtscK = 0x01FC;
        private const ushort RegionPal = 0x02FE;

        /// <summary>
        /// True for exactly 12 characters, all digits 0-9.
        /// </summary>
        public static bool IsValidConsoleSerial(string value)
        {
            if (value == null || value.Length != ConsoleSerialLength)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True for exactly 16 characters, all hex digits (either case).
        /// </summary>
        public static bool IsValidMotherboardSerial(string value)
        {
            if (value == null || value.Length != MotherboardSerialLength)
            {
                return false;
            }

            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Build the key as 32 upper case hex digits, or an empty string when either serial is not valid (as the old tool did).
        /// Layout: bytes 4 to 11 are the 8 motherboard serial bytes; bytes 12 to 15 are the first four console serial
        /// characters as ASCII; the other bytes are zero. (The old tool copied only four of the twelve console serial
        /// characters, and this keeps that.)
        /// </summary>
        public static string MakeKey(string consoleSerial, string motherboardSerial)
        {
            byte[] key = MakeKeyBytes(consoleSerial, motherboardSerial);
            if (key == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(KeyLength * 2);
            foreach (byte b in key)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// The 16 key bytes, or null when either serial is not valid.
        /// </summary>
        public static byte[] MakeKeyBytes(string consoleSerial, string motherboardSerial)
        {
            if (!IsValidConsoleSerial(consoleSerial) || !IsValidMotherboardSerial(motherboardSerial))
            {
                return null;
            }

            byte[] key = new byte[KeyLength];

            for (int i = 0; i < 8; i++)
            {
                key[4 + i] = Convert.ToByte(motherboardSerial.Substring(i * 2, 2), 16);
            }

            for (int i = 0; i < 4; i++)
            {
                key[12 + i] = (byte)consoleSerial[i];
            }

            return key;
        }

        /// <summary>
        /// Read the serials out of the bytes of a keyvault file. Pure: no file access.
        /// </summary>
        public static KeyvaultImportResult ReadKeyvault(byte[] data, out KeyvaultInfo info)
        {
            info = null;

            if (data == null)
            {
                return KeyvaultImportResult.FileAccess;
            }

            if (data.Length != KeyvaultSizeA && data.Length != KeyvaultSizeB)
            {
                return KeyvaultImportResult.BadSize;
            }

            int shift = data.Length == KeyvaultSizeA ? 0 : LargeFileShift;

            // The region code is stored big endian.
            int at = GameRegionOffset + shift;
            ushort region = (ushort)((data[at] << 8) | data[at + 1]);

            if (region != RegionNtscU && region != RegionPal && region != RegionNtscJ && region != RegionNtscK)
            {
                return KeyvaultImportResult.BadRegion;
            }

            StringBuilder mb = new StringBuilder(MotherboardSerialSize * 2);
            for (int i = 0; i < MotherboardSerialSize; i++)
            {
                mb.Append(data[MotherboardSerialOffset + shift + i].ToString("X2"));
            }

            info = new KeyvaultInfo
            {
                ConsoleSerial = Encoding.UTF8.GetString(data, ConsoleSerialOffset + shift, ConsoleSerialLength),
                MotherboardSerial = mb.ToString(),
                RegionCode = region
            };

            return KeyvaultImportResult.Success;
        }

        /// <summary>
        /// Read a keyvault file from disk. The size is checked before the content is read, as the old tool did,
        /// so a wrong file (for example a large one) is not read into memory.
        /// </summary>
        public static KeyvaultImportResult ImportFromFile(string path, out KeyvaultInfo info)
        {
            info = null;

            if (string.IsNullOrEmpty(path))
            {
                return KeyvaultImportResult.FileAccess;
            }

            byte[] data;
            try
            {
                FileInfo file = new FileInfo(path);
                if (!file.Exists)
                {
                    return KeyvaultImportResult.FileAccess;
                }

                if (file.Length != KeyvaultSizeA && file.Length != KeyvaultSizeB)
                {
                    return KeyvaultImportResult.BadSize;
                }

                data = File.ReadAllBytes(path);
            }
            catch (Exception)
            {
                return KeyvaultImportResult.FileAccess;
            }

            return ReadKeyvault(data, out info);
        }

        /// <summary>
        /// Message the old tool showed for a failed import, or null for Success.
        /// </summary>
        public static string DescribeFailure(KeyvaultImportResult result)
        {
            switch (result)
            {
                case KeyvaultImportResult.FileAccess:
                    return "Failed to access or read keyvault file.";
                case KeyvaultImportResult.BadSize:
                    return "Selected file is not a valid keyvault file." + Environment.NewLine + Environment.NewLine + "Invalid size";
                case KeyvaultImportResult.BadRegion:
                    return "Selected file is not a valid keyvault file." + Environment.NewLine + Environment.NewLine + "Invalid region. Supplied keyvault file must be decrypted";
                default:
                    return null;
            }
        }

        /// <summary>
        /// The text that "Save File" wrote: optional serial lines, a blank line, then "XboxHDKey: &lt;key&gt;".
        /// The motherboard serial is shown in upper case. Returns null when a serial or the key is not valid
        /// (the old Save button did nothing in that case).
        /// </summary>
        public static string BuildReport(string consoleSerial, string motherboardSerial, bool includeSerials)
        {
            string key = MakeKey(consoleSerial, motherboardSerial);
            if (key.Length == 0)
            {
                return null;
            }

            StringBuilder output = new StringBuilder();
            if (includeSerials)
            {
                output.AppendLine("Console serial: " + consoleSerial);
                output.AppendLine("Motherboard serial: " + motherboardSerial.ToUpperInvariant());
                output.AppendLine();
            }

            output.Append("XboxHDKey: " + key);
            return output.ToString();
        }

        /// <summary>
        /// Write the report as UTF-8 (with a BOM, as the old StreamWriter(Encoding.UTF8) did). Returns false, and writes
        /// nothing, when the serials are not valid.
        /// </summary>
        public static bool SaveReport(string path, string consoleSerial, string motherboardSerial, bool includeSerials)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string report = BuildReport(consoleSerial, motherboardSerial, includeSerials);
            if (report == null)
            {
                return false;
            }

            File.WriteAllText(path, report, new UTF8Encoding(true));
            return true;
        }
    }
}
