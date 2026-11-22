using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Resources;
using System.Text.RegularExpressions;

namespace ArisenStudio.Localization
{
    /// <summary>
    /// Cross-platform resource text. No WinForms, no DevExpress, no registry, no P/Invoke: it runs the same on Windows
    /// and under Mono on Linux or macOS, and it never throws for a missing key.
    ///
    /// Lookup order for <see cref="Get(string, string)"/>:
    ///   1. The dictionary the host installed with <see cref="SetDictionary"/> (non-empty value only).
    ///   2. The English fallback the caller passed (the text the Mono code already carries).
    ///   3. <see cref="EnglishKeyTable"/>: the en_US text for keys the Mono tree has no resx for, then the code-fallback table.
    ///   4. The key itself, so a missing key shows up on screen instead of as a blank caption.
    ///
    /// The caller's fallback deliberately wins over the generated table. The fallback is the English the screen was written
    /// and read against; the table is for callers that pass no fallback (<see cref="Get(string)"/>).
    ///
    /// Thread safety: the dictionary is swapped as a whole (copy on write), so reads need no lock.
    /// </summary>
    public static class ResourceText
    {
        private static readonly object SetLock = new object();
        private static IDictionary<string, string> dictionary = new Dictionary<string, string>();

        /// <summary>
        /// Install the text for the active language. Pass null or an empty dictionary to go back to English only.
        /// The dictionary is copied, so later changes to the argument have no effect.
        /// </summary>
        public static void SetDictionary(IDictionary<string, string> values)
        {
            Dictionary<string, string> copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values != null)
            {
                foreach (KeyValuePair<string, string> pair in values)
                {
                    if (pair.Key != null && !string.IsNullOrEmpty(pair.Value))
                    {
                        copy[pair.Key] = pair.Value;
                    }
                }
            }

            lock (SetLock)
            {
                dictionary = copy;
            }
        }

        /// <summary>
        /// Read every string in a ResourceManager for a culture into a dictionary for <see cref="SetDictionary"/>.
        /// Returns an empty dictionary when the manager is null or has no resource set for the culture.
        /// Non-string entries are skipped.
        /// </summary>
        public static IDictionary<string, string> ReadResourceManager(ResourceManager manager, CultureInfo culture)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (manager == null)
            {
                return result;
            }

            try
            {
                ResourceSet set = manager.GetResourceSet(culture ?? CultureInfo.CurrentUICulture, true, true);
                if (set == null)
                {
                    return result;
                }

                foreach (System.Collections.DictionaryEntry entry in set)
                {
                    string key = entry.Key as string;
                    string value = entry.Value as string;
                    if (key != null && !string.IsNullOrEmpty(value))
                    {
                        result[key] = value;
                    }
                }
            }
            catch (MissingManifestResourceException)
            {
                // No resources compiled in for this manager: English only.
            }

            return result;
        }

        /// <summary>
        /// Delegate for the pages' Localizer properties (Func of key to text). Returns null when the installed dictionary has
        /// no value, which is the "fall back to English" signal those pages already understand.
        /// </summary>
        public static string Localizer(string key)
        {
            string value;
            return TryGetLocalized(key, out value) ? value : null;
        }

        /// <summary>Text for the key. Falls back to the generated English table, then to the key. See the class remarks.</summary>
        public static string Get(string key)
        {
            return Get(key, null);
        }

        /// <summary>Text for the key, or <paramref name="englishFallback"/> when the installed language has no value.</summary>
        public static string Get(string key, string englishFallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return englishFallback ?? string.Empty;
            }

            string value;
            if (TryGetLocalized(key, out value))
            {
                return value;
            }

            if (!string.IsNullOrEmpty(englishFallback))
            {
                return englishFallback;
            }

            if (EnglishKeyTable.DefinedInEnUs.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (EnglishKeyTable.CodeFallbackOnly.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return key;
        }

        /// <summary>
        /// Text for the key with string.Format applied. If the localized text has a bad format string (for example a
        /// translator dropped a brace) the English fallback is formatted instead; if that fails too, the raw text is returned.
        /// </summary>
        public static string Format(string key, string englishFallback, params object[] args)
        {
            return SafeFormat(Get(key, englishFallback), englishFallback, args);
        }

        private static string SafeFormat(string text, string englishFallback, object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return text;
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, text, args);
            }
            catch (FormatException)
            {
                if (!string.IsNullOrEmpty(englishFallback) && !string.Equals(englishFallback, text, StringComparison.Ordinal))
                {
                    try
                    {
                        return string.Format(CultureInfo.CurrentCulture, englishFallback, args);
                    }
                    catch (FormatException)
                    {
                    }
                }

                return text;
            }
        }

        /// <summary><see cref="Get(string, string)"/> passed through <see cref="ToPlainText"/>. Use for MessageBox and Label text.</summary>
        public static string GetPlain(string key, string englishFallback)
        {
            return ToPlainText(Get(key, englishFallback));
        }

        /// <summary>
        /// <see cref="Format"/> followed by <see cref="ToPlainText"/>. The arguments are substituted first and the HTML
        /// removed afterwards, so an argument that happens to contain angle brackets (an exception message) is converted too.
        /// Use <see cref="FormatPlainKeepArguments"/> when the arguments must be left untouched.
        /// </summary>
        public static string FormatPlain(string key, string englishFallback, params object[] args)
        {
            return ToPlainText(Format(key, englishFallback, args));
        }

        /// <summary>
        /// Convert the template to plain text first, then substitute the arguments. Arguments are never altered.
        /// This is the right call for ERROR_OCCURRED, whose argument is an exception message.
        /// </summary>
        public static string FormatPlainKeepArguments(string key, string englishFallback, params object[] args)
        {
            return SafeFormat(ToPlainText(Get(key, englishFallback)), ToPlainText(englishFallback), args);
        }

        private static readonly Regex Anchor = new Regex(
            "<a\\s[^>]*?href\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))[^>]*>(.*?)</a\\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex LineBreak = new Regex("<br\\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex ParagraphEnd = new Regex("</p\\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex AnyTag = new Regex("</?[A-Za-z][^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>
        /// Plain-text version of HTML-flavoured resource text, for controls that show markup literally (a WinForms or Mono
        /// MessageBox, Label, StatusStrip). The old DevExpress XtraMessageBox rendered anchors; the stock MessageBox does not.
        ///   - &lt;a href="URL"&gt;text&lt;/a&gt; becomes "text (URL)", or just "URL" when the text is the URL.
        ///   - &lt;br&gt; and &lt;/p&gt; become a newline.
        ///   - Every other tag is removed; HTML entities (&amp;amp; &amp;lt; &amp;#39; and so on) are decoded.
        ///   - Text with no "&lt;" is returned exactly as given (no entity decoding), so ordinary captions such as
        ///     "LEDs &amp;&amp; Buzzer" (a WinForms mnemonic escape) are never touched.
        /// Null becomes an empty string. Placeholders such as {0} pass through unchanged.
        /// </summary>
        public static string ToPlainText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.IndexOf('<') < 0)
            {
                return text;
            }

            string result = Anchor.Replace(text, match =>
            {
                string url = match.Groups[1].Success ? match.Groups[1].Value
                    : match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Value;
                string label = AnyTag.Replace(match.Groups[4].Value, string.Empty).Trim();
                url = WebUtility.HtmlDecode(url).Trim();
                label = WebUtility.HtmlDecode(label);
                if (label.Length == 0 || string.Equals(label, url, StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                return label + " (" + url + ")";
            });

            result = LineBreak.Replace(result, "\n");
            result = ParagraphEnd.Replace(result, "\n");
            result = AnyTag.Replace(result, string.Empty);
            return WebUtility.HtmlDecode(result);
        }

        private static bool TryGetLocalized(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            IDictionary<string, string> current = dictionary;
            return current.TryGetValue(key, out value) && !string.IsNullOrEmpty(value);
        }
    }
}
