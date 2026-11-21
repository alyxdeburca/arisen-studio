using System;
using System.Collections.Generic;
using System.Text;

namespace ArisenStudio.Services
{
    /// <summary>
    /// Read-only parser for the PARAM.SFO file format (key/value table with UTF-8 strings and 32-bit ints).
    /// Replaces the closed-source PARAM.SFO.dll that the old FtpExtensions used, for the title lookup.
    /// Pure code: bytes in, values out. No I/O, no UI.
    ///
    /// Layout (little-endian): 20-byte header (magic "\0PSF", version, key table start, data table start,
    /// entry count), then 16-byte index entries (key offset u16, format u16, length u32, max length u32,
    /// data offset u32), then the key table (NUL-terminated names), then the data table.
    /// </summary>
    public sealed class ParamSfoReader
    {
        private const int HeaderSize = 20;
        private const int IndexEntrySize = 16;

        private const ushort FormatUtf8 = 0x0004;
        private const ushort FormatUtf8Null = 0x0204;
        private const ushort FormatInt32 = 0x0404;

        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Parse <paramref name="data" />. Throws <see cref="FormatException" /> if the bytes are not a valid PARAM.SFO.
        /// </summary>
        public ParamSfoReader(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (data.Length < HeaderSize)
            {
                throw new FormatException("PARAM.SFO is too short.");
            }

            if (data[0] != 0x00 || data[1] != 0x50 || data[2] != 0x53 || data[3] != 0x46)
            {
                throw new FormatException("PARAM.SFO has a bad magic number.");
            }

            long keyTableStart = ReadU32(data, 8);
            long dataTableStart = ReadU32(data, 12);
            long entryCount = ReadU32(data, 16);

            if (keyTableStart > data.Length || dataTableStart > data.Length)
            {
                throw new FormatException("PARAM.SFO table offsets are outside the file.");
            }

            if (HeaderSize + entryCount * IndexEntrySize > data.Length)
            {
                throw new FormatException("PARAM.SFO entry count is larger than the file.");
            }

            for (long i = 0; i < entryCount; i++)
            {
                int entry = (int)(HeaderSize + i * IndexEntrySize);

                long keyOffset = keyTableStart + ReadU16(data, entry);
                ushort format = ReadU16(data, entry + 2);
                long length = ReadU32(data, entry + 4);
                long valueOffset = dataTableStart + ReadU32(data, entry + 12);

                string key = ReadKey(data, keyOffset);

                if (valueOffset > data.Length || valueOffset + length > data.Length)
                {
                    throw new FormatException("PARAM.SFO value for '" + key + "' is outside the file.");
                }

                switch (format)
                {
                    case FormatInt32:
                        if (length < 4)
                        {
                            throw new FormatException("PARAM.SFO int value for '" + key + "' is too short.");
                        }

                        _ints[key] = unchecked((int)ReadU32(data, (int)valueOffset));
                        break;

                    case FormatUtf8:
                    case FormatUtf8Null:
                        _strings[key] = ReadString(data, (int)valueOffset, (int)length);
                        break;

                    default:
                        // Unknown format: skip the entry rather than fail the whole file.
                        break;
                }
            }
        }

        /// <summary>
        /// Try to parse. Returns null instead of throwing when the bytes are not a valid PARAM.SFO.
        /// </summary>
        public static ParamSfoReader TryParse(byte[] data)
        {
            try
            {
                return new ParamSfoReader(data);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (ArgumentNullException)
            {
                return null;
            }
        }

        /// <summary>
        /// The TITLE entry, or null if the file has none.
        /// </summary>
        public string Title
        {
            get { return GetString("TITLE"); }
        }

        /// <summary>
        /// The TITLE_ID entry (for example "BLES00000"), or null if the file has none.
        /// </summary>
        public string TitleId
        {
            get { return GetString("TITLE_ID"); }
        }

        /// <summary>
        /// A string value by key, or null if there is none.
        /// </summary>
        public string GetString(string key)
        {
            string value;
            return _strings.TryGetValue(key, out value) ? value : null;
        }

        /// <summary>
        /// An integer value by key. Returns false if there is none.
        /// </summary>
        public bool TryGetInt(string key, out int value)
        {
            return _ints.TryGetValue(key, out value);
        }

        /// <summary>
        /// All string entries, by key.
        /// </summary>
        public IDictionary<string, string> Strings
        {
            get { return _strings; }
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return unchecked((uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24)));
        }

        private static string ReadKey(byte[] data, long start)
        {
            if (start >= data.Length)
            {
                throw new FormatException("PARAM.SFO key offset is outside the file.");
            }

            int begin = (int)start;
            int end = begin;

            while (end < data.Length && data[end] != 0)
            {
                end++;
            }

            if (end >= data.Length)
            {
                throw new FormatException("PARAM.SFO key is not terminated.");
            }

            return Encoding.UTF8.GetString(data, begin, end - begin);
        }

        private static string ReadString(byte[] data, int start, int length)
        {
            // Stop at the first NUL: the stored length includes padding up to the max length.
            int end = start;
            int limit = start + length;

            while (end < limit && data[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(data, start, end - start);
        }
    }
}
