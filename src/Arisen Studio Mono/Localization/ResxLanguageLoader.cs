using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace ArisenStudio.Localization
{
    /// <summary>
    /// Reads the DevExpress .resx language files (en_US, es_ES, pt_BR, ro_RO, sv_SE, tr_TR) as plain XML, with no
    /// ResourceManager, no WinForms and no DevExpress, so it works the same on Windows and under Mono.
    ///
    /// Only &lt;data name="KEY"&gt;&lt;value&gt;...&lt;/value&gt;&lt;/data&gt; string entries are read. A &lt;data&gt; element with a
    /// "type" or "mimetype" attribute (icons, bitmaps, serialized objects) is skipped, &lt;metadata&gt; is never looked at,
    /// and an entry with an empty value is skipped (the same rule <see cref="ResourceText.SetDictionary"/> applies).
    /// If a name appears twice the last one wins.
    ///
    /// Nothing here throws for a missing or broken file: the result is an empty dictionary and the reason is reported
    /// through the <c>problem</c> out parameter (or <see cref="ResxTextResolver.Problems"/>).
    ///
    /// Not compiled or run when written. Needs a reference to System.Xml.Linq in the csproj (the csproj has System.Xml only).
    /// </summary>
    public static class ResxLanguageLoader
    {
        /// <summary>The language every other language falls back to.</summary>
        public const string DefaultCulture = "en_US";

        /// <summary>The resx language names shipped in the DevExpress Languages folder.</summary>
        public static readonly string[] SupportedCultures = { "en_US", "es_ES", "pt_BR", "ro_RO", "sv_SE", "tr_TR" };

        /// <summary>
        /// Maps "es-ES", "es_es", "es" and similar to a name in <see cref="SupportedCultures"/>. A bare language code picks
        /// the first supported language that starts with it. Null, empty or unknown input gives <see cref="DefaultCulture"/>.
        /// </summary>
        public static string NormalizeCulture(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return DefaultCulture;
            }

            string code = culture.Trim().Replace('-', '_');
            foreach (string supported in SupportedCultures)
            {
                if (string.Equals(supported, code, StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }

            int underscore = code.IndexOf('_');
            string language = underscore > 0 ? code.Substring(0, underscore) : code;
            foreach (string supported in SupportedCultures)
            {
                if (supported.StartsWith(language + "_", StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }

            return DefaultCulture;
        }

        /// <summary>Reads one .resx file. Missing or unreadable file: empty dictionary.</summary>
        public static IReadOnlyDictionary<string, string> LoadFile(string path)
        {
            string problem;
            return LoadFile(path, out problem);
        }

        /// <summary>Reads one .resx file. <paramref name="problem"/> is null on success, otherwise says why the result is empty.</summary>
        public static IReadOnlyDictionary<string, string> LoadFile(string path, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                problem = "No resx path given.";
                return Empty();
            }

            try
            {
                if (!File.Exists(path))
                {
                    problem = "Resx file not found: " + path;
                    return Empty();
                }

                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    IReadOnlyDictionary<string, string> result = LoadStream(stream, out problem);
                    if (problem != null)
                    {
                        problem = path + ": " + problem;
                    }

                    return result;
                }
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                problem = "Cannot read resx file " + path + ": " + ex.Message;
                return Empty();
            }
        }

        /// <summary>Reads .resx XML from a stream. The stream is not closed. Broken XML: empty dictionary.</summary>
        public static IReadOnlyDictionary<string, string> LoadStream(Stream stream)
        {
            string problem;
            return LoadStream(stream, out problem);
        }

        /// <summary>Reads .resx XML from a stream. <paramref name="problem"/> is null on success.</summary>
        public static IReadOnlyDictionary<string, string> LoadStream(Stream stream, out string problem)
        {
            problem = null;
            if (stream == null)
            {
                problem = "No resx stream given.";
                return Empty();
            }

            try
            {
                XmlReaderSettings settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    CloseInput = false
                };

                XDocument document;
                using (XmlReader reader = XmlReader.Create(stream, settings))
                {
                    document = XDocument.Load(reader);
                }

                return Parse(document);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                problem = "Cannot parse resx XML: " + ex.Message;
                return Empty();
            }
        }

        /// <summary>
        /// Reads every <see cref="SupportedCultures"/> file found in the directory. The result has one entry per supported
        /// culture; a language whose file is missing or broken maps to an empty dictionary.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadDirectory(string directory)
        {
            IList<string> problems;
            return LoadDirectory(directory, out problems);
        }

        /// <summary>As <see cref="LoadDirectory(string)"/>; <paramref name="problems"/> has one line per file that could not be read.</summary>
        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadDirectory(string directory, out IList<string> problems)
        {
            problems = new List<string>();
            Dictionary<string, IReadOnlyDictionary<string, string>> result =
                new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

            foreach (string culture in SupportedCultures)
            {
                string problem;
                result[culture] = LoadCulture(directory, culture, out problem);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            return result;
        }

        /// <summary>Reads {directory}/{culture}.resx. The culture is normalised first (see <see cref="NormalizeCulture"/>).</summary>
        public static IReadOnlyDictionary<string, string> LoadCulture(string directory, string culture, out string problem)
        {
            string name = NormalizeCulture(culture);
            if (string.IsNullOrWhiteSpace(directory))
            {
                problem = "No resx directory given (" + name + ").";
                return Empty();
            }

            string path = FindFile(directory, name + ".resx");
            return LoadFile(path, out problem);
        }

        // Exact name first; on a case-sensitive file system also accept "en_us.resx".
        private static string FindFile(string directory, string fileName)
        {
            string exact = Path.Combine(directory, fileName);
            try
            {
                if (File.Exists(exact) || !Directory.Exists(directory))
                {
                    return exact;
                }

                foreach (string candidate in Directory.GetFiles(directory, "*.resx"))
                {
                    if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                // Fall through: LoadFile reports the problem for the exact path.
            }

            return exact;
        }

        private static IReadOnlyDictionary<string, string> Parse(XDocument document)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            XElement root = document.Root;
            if (root == null)
            {
                return result;
            }

            // Only direct <data> children of the root. <metadata>, <resheader> and <assembly> are ignored.
            foreach (XElement data in root.Elements("data"))
            {
                if (data.Attribute("type") != null || data.Attribute("mimetype") != null)
                {
                    continue;
                }

                XAttribute nameAttribute = data.Attribute("name");
                if (nameAttribute == null || string.IsNullOrEmpty(nameAttribute.Value))
                {
                    continue;
                }

                XElement value = data.Element("value");
                if (value == null || string.IsNullOrEmpty(value.Value))
                {
                    continue;
                }

                result[nameAttribute.Value] = value.Value;
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> Empty()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // I/O and XML failures only. Programming errors (null reference and so on) are not swallowed.
        private static bool IsExpected(Exception ex)
        {
            return ex is IOException
                || ex is UnauthorizedAccessException
                || ex is XmlException
                || ex is NotSupportedException
                || ex is System.Security.SecurityException
                || ex is ArgumentException;
        }
    }

    /// <summary>
    /// Text for a culture code and a key or a Mono short key, read from a directory of .resx files. Languages are read
    /// on first use and kept. Never throws and never returns null.
    ///
    /// Order for <see cref="Resolve(string, string)"/>, where "target" is the .resx key a short key is an alias for
    /// (<see cref="AliasedText.TargetKey"/>, which reads <see cref="KeyAliases.Targets"/>), or the key itself when it is not an alias:
    ///   1. the culture's .resx under the key, then under the target;
    ///   2. the en_US .resx under the key, then under the target;
    ///   3. <see cref="KeyAliases.EnglishText"/> under the target;
    ///   4. <see cref="EnglishKeyTable.DefinedInEnUs"/>, then <see cref="EnglishKeyTable.CodeFallbackOnly"/>, under the key, then the target;
    ///   5. the key as given.
    /// The text is then passed through <see cref="MessageTextSanitizer.ToPlainText(string)"/> (anchors become "text (url)",
    /// other tags are dropped; text without a "&lt;" is returned unchanged). Use the overload with <c>plainText: false</c> for the raw text.
    /// </summary>
    public sealed class ResxTextResolver
    {
        private readonly string directory;
        private readonly object sync = new object();
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> loaded =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        private readonly List<string> problems = new List<string>();

        /// <summary>The directory is not touched until the first lookup. It may be null or missing; then only the built-in English is used.</summary>
        public ResxTextResolver(string directory)
        {
            this.directory = directory;
        }

        /// <summary>Why a language could not be read (one line per language, reported once). Empty when all went well so far.</summary>
        public IReadOnlyList<string> Problems
        {
            get
            {
                lock (sync)
                {
                    return problems.ToArray();
                }
            }
        }

        /// <summary>Plain text for the key in the culture. See the class remarks.</summary>
        public string Resolve(string culture, string keyOrAlias)
        {
            return Resolve(culture, keyOrAlias, true);
        }

        /// <summary>As <see cref="Resolve(string, string)"/>; <paramref name="plainText"/> false returns the text with its HTML and placeholders as stored.</summary>
        public string Resolve(string culture, string keyOrAlias, bool plainText)
        {
            if (string.IsNullOrEmpty(keyOrAlias))
            {
                return string.Empty;
            }

            string text = FindText(culture, keyOrAlias) ?? keyOrAlias;
            return plainText ? MessageTextSanitizer.ToPlainText(text) : text;
        }

        /// <summary>True when the key (or its alias target) has text from a .resx or the built-in English, i.e. Resolve would not return the key itself.</summary>
        public bool Contains(string culture, string keyOrAlias)
        {
            return !string.IsNullOrEmpty(keyOrAlias) && FindText(culture, keyOrAlias) != null;
        }

        /// <summary>
        /// The language's .resx text laid over the en_US text, both under their .resx keys, ready for
        /// <see cref="ResourceText.SetDictionary"/>. Raw text, no sanitising. A new dictionary on every call.
        /// </summary>
        public IDictionary<string, string> BuildDictionary(string culture)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            string name = ResxLanguageLoader.NormalizeCulture(culture);
            foreach (KeyValuePair<string, string> pair in GetLanguage(ResxLanguageLoader.DefaultCulture))
            {
                result[pair.Key] = pair.Value;
            }

            if (!string.Equals(name, ResxLanguageLoader.DefaultCulture, StringComparison.Ordinal))
            {
                foreach (KeyValuePair<string, string> pair in GetLanguage(name))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        private string FindText(string culture, string key)
        {
            string target = AliasedText.TargetKey(key);
            string name = ResxLanguageLoader.NormalizeCulture(culture);
            string value;

            if (TryGet(GetLanguage(name), key, target, out value))
            {
                return value;
            }

            if (!string.Equals(name, ResxLanguageLoader.DefaultCulture, StringComparison.Ordinal)
                && TryGet(GetLanguage(ResxLanguageLoader.DefaultCulture), key, target, out value))
            {
                return value;
            }

            if (KeyAliases.EnglishText.TryGetValue(target, out value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (TryGet(EnglishKeyTable.DefinedInEnUs, key, target, out value)
                || TryGet(EnglishKeyTable.CodeFallbackOnly, key, target, out value))
            {
                return value;
            }

            return null;
        }

        private static bool TryGet(IReadOnlyDictionary<string, string> source, string key, string target, out string value)
        {
            return (source.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                || (source.TryGetValue(target, out value) && !string.IsNullOrEmpty(value));
        }

        private static bool TryGet(IDictionary<string, string> source, string key, string target, out string value)
        {
            return (source.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                || (source.TryGetValue(target, out value) && !string.IsNullOrEmpty(value));
        }

        private IReadOnlyDictionary<string, string> GetLanguage(string name)
        {
            lock (sync)
            {
                IReadOnlyDictionary<string, string> values;
                if (loaded.TryGetValue(name, out values))
                {
                    return values;
                }

                string problem;
                values = ResxLanguageLoader.LoadCulture(directory, name, out problem);
                loaded[name] = values;
                if (problem != null)
                {
                    problems.Add(problem);
                }

                return values;
            }
        }
    }
}
