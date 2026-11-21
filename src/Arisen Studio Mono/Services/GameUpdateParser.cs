using ArisenStudio.Models.GameData.PS3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace ArisenStudio.Services
{
    /// <summary>
    /// One row of the PS3 game updates list (what the old GameUpdatesFinder put in its grid).
    /// </summary>
    public sealed class GameUpdateRow
    {
        public string Url { get; set; }

        public string Sha1 { get; set; }

        public string FileName { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// Size text, already formatted for display.
        /// </summary>
        public string Size { get; set; }

        public string MinFirmware { get; set; }
    }

    /// <summary>
    /// Pure parsing of the Sony PS3 "title patch" XML (the text of &lt;titleid&gt;-ver.xml) into display rows.
    /// Split out of the old GameUpdatesFinder form and HttpExtensions so it can be checked without a network.
    /// This class never downloads anything: the caller fetches the XML text and passes it in.
    /// </summary>
    public static class GameUpdateParser
    {
        /// <summary>
        /// Address of the update list for a title id. Only builds the string; does not connect.
        /// </summary>
        public static string GetUpdateListUrl(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId))
            {
                throw new ArgumentException("titleId is empty", "titleId");
            }

            string id = titleId.Trim();
            return "https://a0.ww.np.dl.playstation.net/tpl/np/" + id + "/" + id + "-ver.xml";
        }

        /// <summary>
        /// Parse the XML text into the shared TitlePatch model. Returns null when the text is empty
        /// or is not a valid titlepatch document (the old code threw and the form showed "invalid title id").
        /// </summary>
        public static TitlePatch TryParse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TitlePatch));

                // Prohibit DTDs: the text comes from a remote server.
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;

                using (StringReader textReader = new StringReader(xml.Trim()))
                using (XmlReader xmlReader = XmlReader.Create(textReader, settings))
                {
                    return (TitlePatch)serializer.Deserialize(xmlReader);
                }
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        /// <summary>
        /// Game title for the label: the title of the newest package, with any "(...)" removed,
        /// trimmed, and the UTF-8 mojibake for the registered sign repaired. Null when no title is present.
        /// </summary>
        public static string GetCleanTitle(TitlePatch patch)
        {
            List<Package> packages = GetPackages(patch);

            if (packages.Count == 0)
            {
                return null;
            }

            Package last = packages[packages.Count - 1];

            if (last == null || last.Paramsfo == null || last.Paramsfo.Title == null)
            {
                return null;
            }

            return CleanTitle(last.Paramsfo.Title);
        }

        public static string CleanTitle(string rawTitle)
        {
            if (rawTitle == null)
            {
                return null;
            }

            string noBrackets = Regex.Replace(rawTitle, @"\(.*?\)", string.Empty).Trim();
            // The server text can show the registered sign (U+00AE) as UTF-8 read as Latin-1 (U+00C2 U+00AE).
            return noBrackets.Replace("\u00C2\u00AE", "\u00AE");
        }

        /// <summary>
        /// Update rows, newest first (the XML lists oldest first; the old form reversed it in place).
        /// <paramref name="formatSize" /> turns a byte count into display text; when null the row shows
        /// "N bytes". Does not modify the patch.
        /// </summary>
        public static List<GameUpdateRow> ToRows(TitlePatch patch, Func<long, string> formatSize)
        {
            List<Package> packages = GetPackages(patch);
            List<GameUpdateRow> rows = new List<GameUpdateRow>();

            for (int i = packages.Count - 1; i >= 0; i--)
            {
                Package update = packages[i];

                if (update == null)
                {
                    continue;
                }

                GameUpdateRow row = new GameUpdateRow();
                row.Url = update.Url;
                row.Sha1 = update.Sha1Sum;
                row.FileName = FileNameFromUrl(update.Url);
                row.Version = TrimLeadingZeros(update.Version);
                row.Size = FormatSize(update.Size, formatSize);
                row.MinFirmware = MinFirmware(update.Ps3SystemVer);
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Minimum firmware text. The XML carries values like "04.8800": leading zeros are dropped and the
        /// last 3 characters of the original length are cut off, which gives "4.88". Never throws.
        /// </summary>
        public static string MinFirmware(string ps3SystemVer)
        {
            if (string.IsNullOrEmpty(ps3SystemVer))
            {
                return string.Empty;
            }

            string trimmed = ps3SystemVer.TrimStart('0');
            int count = Math.Min(trimmed.Length, Math.Max(0, ps3SystemVer.Length - 3));
            return trimmed.Substring(0, count);
        }

        /// <summary>
        /// File name part of a URL, without the query string. Empty if the URL is missing or not absolute.
        /// </summary>
        public static string FileNameFromUrl(string url)
        {
            Uri uri;

            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return string.Empty;
            }

            return Path.GetFileName(uri.LocalPath);
        }

        public static string TrimLeadingZeros(string version)
        {
            return version == null ? string.Empty : version.TrimStart('0');
        }

        private static string FormatSize(string sizeText, Func<long, string> formatSize)
        {
            long bytes;

            if (!long.TryParse(sizeText, out bytes))
            {
                // Not a number: show what the server sent rather than throw.
                return sizeText ?? string.Empty;
            }

            return formatSize != null ? formatSize(bytes) : bytes + " bytes";
        }

        private static List<Package> GetPackages(TitlePatch patch)
        {
            if (patch == null || patch.Tag == null || patch.Tag.Package == null)
            {
                return new List<Package>();
            }

            return patch.Tag.Package;
        }
    }
}
