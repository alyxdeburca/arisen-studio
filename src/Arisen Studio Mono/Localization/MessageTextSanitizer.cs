using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ArisenStudio.Localization
{
    /// <summary>
    /// Plain-text versions of the two resource strings that cannot be shown as they are in a stock MessageBox or status bar:
    ///   - ERROR_OCCURRED holds an HTML anchor (DevExpress XtraMessageBox rendered it, the stock MessageBox prints the tag).
    ///   - CONNECTING_TO_CONSOLE holds a {0} placeholder in every .resx, but the Mono call site passes no argument.
    ///
    /// An anchor becomes "text (url)" (or "text [url]" with <c>squareBrackets</c>), or just the url when the text is the url.
    /// Other tags are dropped, &lt;br&gt; and &lt;/p&gt; become a newline, entities are decoded, but only for text that contained
    /// a "&lt;" (so a caption such as "LEDs &amp;&amp; Buzzer" is left alone). Does not depend on WinForms or DevExpress.
    ///
    /// Not compiled or run when written. It overlaps <see cref="ResourceText.ToPlainText"/> on purpose: that one always uses
    /// round brackets; this one lets the caller pick and also protects braces in a URL from string.Format.
    /// </summary>
    public static class MessageTextSanitizer
    {
        /// <summary>Resource key of the error text.</summary>
        public const string ErrorOccurredKey = "ERROR_OCCURRED";

        /// <summary>English used when no language text exists. No anchor, as in the Mono code.</summary>
        public const string ErrorOccurredFallback = "An error occurred: {0}";

        /// <summary>Resource key of the connecting text.</summary>
        public const string ConnectingToConsoleKey = "CONNECTING_TO_CONSOLE";

        private const string ConnectingWithAddressFallback = "Connecting to: {0}";
        private const string ConnectingWithoutAddressFallback = "Connecting to console...";

        private static readonly Regex Anchor = new Regex(
            "<a\\s[^>]*?href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))[^>]*>(.*?)</a\\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex LineBreak = new Regex("<br\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ParagraphEnd = new Regex("</p\\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AnyTag = new Regex("</?[A-Za-z][^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        // ": {0}", " {0}", "{0:N0}" and so on, with the separator in front of it.
        private static readonly Regex Placeholder = new Regex("\\s*:?\\s*\\{\\d+(?:[,:][^}]*)?\\}", RegexOptions.CultureInvariant);

        /// <summary>
        /// The ERROR_OCCURRED text as plain text with the exception message in place of {0}.
        /// The template is converted first and the message inserted afterwards, so a message containing "&lt;" is never altered.
        /// </summary>
        public static string ErrorOccurred(string message)
        {
            return ErrorOccurred(message, false);
        }

        /// <summary>See <see cref="ErrorOccurred(string)"/>; <paramref name="squareBrackets"/> puts the URL in [ ] instead of ( ).</summary>
        public static string ErrorOccurred(string message, bool squareBrackets)
        {
            string template = ToPlainText(ResourceText.Get(ErrorOccurredKey, ErrorOccurredFallback), squareBrackets, true);
            return SafeFormat(template, message ?? string.Empty);
        }

        /// <summary>
        /// The CONNECTING_TO_CONSOLE text for a status line. With an address, the language text is formatted with it
        /// ("Connecting to: 192.168.1.5"). Without one, the {0} (and the colon before it) is removed and "..." added
        /// ("Connecting to..."), so the status bar never shows a literal "{0}". When the installed language has no text,
        /// the English "Connecting to console..." (no address) or "Connecting to: {0}" (address) is used.
        /// </summary>
        public static string ConnectingToConsole(string consoleAddress)
        {
            bool hasAddress = !string.IsNullOrWhiteSpace(consoleAddress);
            string template = ToPlainText(
                ResourceText.Get(ConnectingToConsoleKey, hasAddress ? ConnectingWithAddressFallback : ConnectingWithoutAddressFallback),
                false,
                true);

            if (hasAddress)
            {
                return SafeFormat(template, consoleAddress.Trim());
            }

            return RemovePlaceholders(template);
        }

        /// <summary>
        /// Plain text for HTML-flavoured text. Anchor becomes "text (url)"; see the class remarks.
        /// </summary>
        public static string ToPlainText(string text)
        {
            return ToPlainText(text, false, false);
        }

        /// <summary>As <see cref="ToPlainText(string)"/>, with the URL in [ ] when <paramref name="squareBrackets"/> is true.</summary>
        public static string ToPlainText(string text, bool squareBrackets)
        {
            return ToPlainText(text, squareBrackets, false);
        }

        /// <summary>
        /// Remove {0}-style placeholders and the ":" or space before them. Ends the result with "..." unless it already ends
        /// with a full stop. Used when a text is shown without its arguments.
        /// </summary>
        public static string RemovePlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (!Placeholder.IsMatch(text))
            {
                return text;
            }

            string result = Placeholder.Replace(text, string.Empty).TrimEnd();
            return result.EndsWith(".", StringComparison.Ordinal) ? result : result + "...";
        }

        // escapeBraces: double any { or } that comes from an anchor, so the result can go through string.Format.
        private static string ToPlainText(string text, bool squareBrackets, bool escapeBraces)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.IndexOf('<') < 0)
            {
                return text;
            }

            string open = squareBrackets ? " [" : " (";
            string close = squareBrackets ? "]" : ")";

            // Anchors first. Label and URL are decoded here, then "&" is re-encoded as "&amp;" so the single whole-string
            // HtmlDecode at the end gives back the same text and nothing is decoded twice.
            string result = Anchor.Replace(text, match =>
            {
                string url = match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Value;
                url = WebUtility.HtmlDecode(url).Trim();
                string label = WebUtility.HtmlDecode(AnyTag.Replace(match.Groups[4].Value, string.Empty)).Trim();
                string piece = label.Length == 0 || string.Equals(label, url, StringComparison.OrdinalIgnoreCase)
                    ? url
                    : label + open + url + close;
                if (escapeBraces)
                {
                    piece = piece.Replace("{", "{{").Replace("}", "}}");
                }

                // Encode "&" so the closing HtmlDecode below turns it back into the same "&" and does not decode twice.
                return piece.Replace("&", "&amp;");
            });

            result = LineBreak.Replace(result, "\n");
            result = ParagraphEnd.Replace(result, "\n");
            result = AnyTag.Replace(result, string.Empty);
            return WebUtility.HtmlDecode(result);
        }

        private static string SafeFormat(string template, string argument)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, argument);
            }
            catch (FormatException)
            {
                return template.Replace("{0}", argument);
            }
        }
    }
}
