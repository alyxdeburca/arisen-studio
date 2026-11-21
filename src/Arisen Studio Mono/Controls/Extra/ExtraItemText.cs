using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Text helpers shared by the controls in Controls/Extra. They replace the Humanizer calls and the DevExpress
    /// HTML label of the original controls. Internal on purpose: nothing outside Controls/Extra should depend on it.
    /// </summary>
    internal static class ExtraItemText
    {
        private static readonly Regex UrlPattern = new Regex("https?://[^\\s<>\"']+", RegexOptions.Compiled);
        private static readonly Regex LineBreakPattern = new Regex("<br\\s*/?>|</p\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagPattern = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex AnchorPattern = new Regex(
            "<a\\s[^>]*?href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')[^>]*>(.*?)</a\\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// A clickable range inside a piece of plain text.
        /// </summary>
        internal sealed class LinkSpan
        {
            public int Start;
            public int Length;
            public string Url;
        }

        /// <summary>
        /// Plain text plus the link ranges found in it.
        /// </summary>
        internal sealed class LinkedText
        {
            public string Text = string.Empty;
            public readonly List<LinkSpan> Links = new List<LinkSpan>();
        }

        /// <summary>
        /// Converts the small HTML subset used by announcements to plain text. Breaks become newlines,
        /// anchors become link ranges, other tags are removed, and entities are decoded per segment so that
        /// escaped text such as &amp;lt;b&amp;gt; is not stripped as a tag. Only http and https targets become links.
        /// </summary>
        public static LinkedText FromHtml(string html, int maximumLinks)
        {
            LinkedText result = new LinkedText();
            if (string.IsNullOrEmpty(html))
            {
                return result;
            }

            string source = LineBreakPattern.Replace(html, "\n");
            StringBuilder text = new StringBuilder();
            List<LinkSpan> links = new List<LinkSpan>();
            int position = 0;

            foreach (Match match in AnchorPattern.Matches(source))
            {
                text.Append(PlainSegment(source.Substring(position, match.Index - position)));
                position = match.Index + match.Length;

                string url = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                url = System.Net.WebUtility.HtmlDecode(url).Trim();
                string inner = PlainSegment(match.Groups[3].Value);
                int start = text.Length;
                text.Append(inner);

                if (inner.Length > 0 && IsWebUrl(url))
                {
                    links.Add(new LinkSpan { Start = start, Length = inner.Length, Url = url });
                }
            }

            text.Append(PlainSegment(source.Substring(position)));

            string full = text.ToString();
            string trimmed = full.Trim();
            int lead = trimmed.Length == 0 ? 0 : full.Length - full.TrimStart().Length;

            // Bare URLs that are not already inside an anchor.
            foreach (Match match in UrlPattern.Matches(full))
            {
                string url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
                if (url.Length == 0 || Overlaps(links, match.Index, url.Length))
                {
                    continue;
                }

                links.Add(new LinkSpan { Start = match.Index, Length = url.Length, Url = url });
            }

            links.Sort((a, b) => a.Start.CompareTo(b.Start));
            result.Text = trimmed;
            foreach (LinkSpan link in links)
            {
                if (result.Links.Count >= maximumLinks)
                {
                    break;
                }

                int start = link.Start - lead;
                if (start < 0 || start + link.Length > trimmed.Length)
                {
                    continue;
                }

                result.Links.Add(new LinkSpan { Start = start, Length = link.Length, Url = link.Url });
            }

            return result;
        }

        /// <summary>
        /// Formats a time stamp. With relative times on, entries younger than 30 days read as "5 minutes ago";
        /// everything else uses the absolute format.
        /// </summary>
        public static string FormatTimeStamp(DateTime timeStamp, bool useRelativeTimes, string absoluteFormat)
        {
            string absolute = timeStamp.ToString(absoluteFormat, CultureInfo.CurrentCulture);
            if (!useRelativeTimes)
            {
                return absolute;
            }

            DateTime now = timeStamp.Kind == DateTimeKind.Local ? DateTime.Now : DateTime.UtcNow;
            TimeSpan span = now - timeStamp;
            if (span < TimeSpan.Zero || span.TotalDays >= 30)
            {
                return absolute;
            }

            if (span.TotalMinutes < 1)
            {
                return "just now";
            }

            if (span.TotalHours < 1)
            {
                return Plural((int)span.TotalMinutes, "minute");
            }

            if (span.TotalDays < 1)
            {
                return Plural((int)span.TotalHours, "hour");
            }

            return Plural((int)span.TotalDays, "day");
        }

        private static string Plural(int count, string unit)
        {
            return count == 1 ? "1 " + unit + " ago" : count + " " + unit + "s ago";
        }

        private static string PlainSegment(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            return System.Net.WebUtility.HtmlDecode(TagPattern.Replace(html, string.Empty));
        }

        private static bool IsWebUrl(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Overlaps(List<LinkSpan> links, int start, int length)
        {
            foreach (LinkSpan link in links)
            {
                if (start < link.Start + link.Length && link.Start < start + length)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
