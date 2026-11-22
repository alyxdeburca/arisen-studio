using System;
using System.Text;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Turns the raw bytes of announcements.json into text.
    /// Rule: strip a leading UTF-8 BOM, try strict UTF-8, and if that fails
    /// decode as Windows-1252. The five bytes Windows-1252 leaves undefined
    /// (0x81, 0x8D, 0x8F, 0x90, 0x9D) map to U+0081, U+008D, U+008F, U+0090,
    /// U+009D, so this decoder never throws on them.
    /// The 1252 table is written out by hand; Encoding.GetEncoding(1252) is not used,
    /// because its behaviour on Mono is not verified.
    /// </summary>
    internal static class AnnouncementsText
    {
        // Windows-1252 code points for bytes 0x80 to 0x9F, in byte order.
        private static readonly char[] Cp1252High =
        {
            '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
            '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
            '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
            '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178'
        };

        /// <summary>
        /// Decodes the announcements.json response body.
        /// </summary>
        /// <param name="data">Raw response bytes. Must not be null.</param>
        /// <returns>The decoded text, without a leading BOM.</returns>
        internal static string Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            int start = 0;
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                start = 3;
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(data, start, data.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return DecodeWindows1252(data, start, data.Length - start);
            }
        }

        /// <summary>
        /// Decodes bytes as Windows-1252 with the hand-written table. Never throws.
        /// </summary>
        internal static string DecodeWindows1252(byte[] data, int index, int count)
        {
            var sb = new StringBuilder(count);
            int end = index + count;
            for (int i = index; i < end; i++)
            {
                byte b = data[i];
                if (b >= 0x80 && b <= 0x9F)
                {
                    sb.Append(Cp1252High[b - 0x80]);
                }
                else
                {
                    // 0x00 to 0x7F and 0xA0 to 0xFF: same code point as the byte value.
                    sb.Append((char)b);
                }
            }
            return sb.ToString();
        }
    }
}

