using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArisenStudio.Localization
{
    /// <summary>
    /// Lookup for the short keys the Mono code invented (CATEGORY, NAME, DASHBOARD, ...). Each short key is an alias for an
    /// existing .resx key (LABEL_CATEGORY, LABEL_NAME, NAVIGATION_DASHBOARD, ...); see <see cref="KeyAliases"/>.
    ///
    /// Order for <see cref="Get(string, string)"/>:
    ///   1. The installed dictionary (<see cref="ResourceText.SetDictionary"/>) under the short key, in case a host supplies it.
    ///   2. The installed dictionary under the alias target key (the normal case: the real .resx key).
    ///   3. The en_US text of the target (<see cref="KeyAliases.EnglishText"/>, verbatim). This is the step a language whose
    ///      .resx lacks the target lands on (<see cref="LanguagesMissing"/> lists them). For the 29 current aliases every
    ///      language has every target, so step 3 is only reached when no language dictionary is installed.
    ///   4. For a key that is not an alias, or an alias with no table text: <see cref="ResourceText.Get(string, string)"/>
    ///      (the caller's English, then the generated English table, then the key).
    /// Never throws and never returns null.
    ///
    /// Not compiled or run when written. Add this file and KeyAliases.g.cs to the csproj to use it.
    /// </summary>
    public static class AliasedText
    {
        private static readonly string[] None = new string[0];

        /// <summary>True when the key is a Mono short key with a known .resx target.</summary>
        public static bool IsAlias(string key)
        {
            return key != null && KeyAliases.Targets.ContainsKey(key);
        }

        /// <summary>The .resx key for a short key, or the key unchanged when it is not an alias.</summary>
        public static string TargetKey(string key)
        {
            string target;
            return key != null && KeyAliases.Targets.TryGetValue(key, out target) ? target : key;
        }

        /// <summary>
        /// Resx language names (en_US, es_ES, pt_BR, ro_RO, sv_SE, tr_TR) whose .resx lacks the target of this short key.
        /// For these <see cref="Get(string, string)"/> shows the en_US text. Empty for a non-alias or a fully covered alias.
        /// </summary>
        public static IList<string> LanguagesMissing(string key)
        {
            string[] languages;
            return key != null && KeyAliases.MissingIn.TryGetValue(key, out languages) ? languages : None;
        }

        /// <summary>
        /// Text for the key. See the class remarks for the order.
        /// </summary>
        public static string Get(string key)
        {
            return Get(key, null);
        }

        /// <summary>Text for the key; <paramref name="englishFallback"/> is used only when nothing above step 3 has text.</summary>
        public static string Get(string key, string englishFallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return englishFallback ?? string.Empty;
            }

            string value = ResourceText.Localizer(key);
            if (value != null)
            {
                return value;
            }

            string target;
            if (KeyAliases.Targets.TryGetValue(key, out target))
            {
                value = ResourceText.Localizer(target);
                if (value != null)
                {
                    return value;
                }

                string english;
                if (KeyAliases.EnglishText.TryGetValue(target, out english) && !string.IsNullOrEmpty(english))
                {
                    return english;
                }
            }

            return ResourceText.Get(key, englishFallback);
        }

        /// <summary>
        /// Same as <see cref="Get(string, string)"/> followed by string.Format. A bad format string in a translation
        /// returns the unformatted text instead of throwing.
        /// </summary>
        public static string Format(string key, string englishFallback, params object[] args)
        {
            string text = Get(key, englishFallback);
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
                return text;
            }
        }

        /// <summary>
        /// Delegate for the pages' <c>Func&lt;string,string&gt; Localizer</c> properties. Returns the installed language's text
        /// for a key or its alias; for an alias whose target the language lacks it returns the en_US text (so never null for
        /// an alias). Returns null for any other unknown key, which is the page's signal to use its own English.
        /// </summary>
        public static string Localizer(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            string value = ResourceText.Localizer(key);
            if (value != null)
            {
                return value;
            }

            string target;
            if (!KeyAliases.Targets.TryGetValue(key, out target))
            {
                return null;
            }

            value = ResourceText.Localizer(target);
            if (value != null)
            {
                return value;
            }

            string english;
            return KeyAliases.EnglishText.TryGetValue(target, out english) && !string.IsNullOrEmpty(english) ? english : null;
        }
    }
}
