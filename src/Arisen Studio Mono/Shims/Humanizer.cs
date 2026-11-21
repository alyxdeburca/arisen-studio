// Shim for the Humanizer NuGet package, so the old call sites compile without the package.
// Provides only the members the old tree calls (DATA-LAYER-MAP.md sections 3b and 3c).
// Namespace is Humanizer so the existing `using Humanizer;` lines compile unchanged.
// Output formats are best-effort and UNVERIFIED against the real package; see
// ~/arisen-build/integration/humanizer-shim.md. Code-only: not compiled.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Humanizer
{
    // Enum.Humanize(): [Description] text when present, otherwise the PascalCase name split into words.
    public static class ShimEnumHumanize
    {
        public static string Humanize(this Enum value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string name = value.ToString();
            System.Reflection.FieldInfo field = value.GetType().GetField(name);
            if (field != null)
            {
                object[] attributes = field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0)
                {
                    return ((DescriptionAttribute)attributes[0]).Description;
                }
            }

            return ShimStringHumanize.SplitPascalCase(name);
        }
    }

    // DateTime.Humanize() (relative text) and DateTime.ToOrdinalWords() (see note below).
    public static class ShimDateTimeHumanize
    {
        // UNVERIFIED: relative wording and thresholds are a simple approximation of Humanizer's output.
        public static string Humanize(this DateTime value)
        {
            DateTime now = value.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
            TimeSpan span = now - value;
            bool isFuture = span < TimeSpan.Zero;
            if (isFuture)
            {
                span = span.Negate();
            }

            string phrase = RelativePhrase(span);
            if (phrase == "just now")
            {
                return phrase;
            }

            return isFuture ? "in " + phrase : phrase + " ago";
        }

        // UNVERIFIED: the only call site is a commented-out change log title, plus one live UI call
        // (DialogExtensions.cs:712). Simplest behaviour: a plain long date, no ordinal words.
        public static string ToOrdinalWords(this DateTime value)
        {
            return value.ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
        }

        private static string RelativePhrase(TimeSpan span)
        {
            if (span.TotalMinutes < 1)
            {
                return "just now";
            }

            if (span.TotalHours < 1)
            {
                return Count((int)span.TotalMinutes, "minute");
            }

            if (span.TotalDays < 1)
            {
                return Count((int)span.TotalHours, "hour");
            }

            if (span.TotalDays < 30)
            {
                return Count((int)span.TotalDays, "day");
            }

            if (span.TotalDays < 365)
            {
                return Count((int)(span.TotalDays / 30), "month");
            }

            return Count((int)(span.TotalDays / 365), "year");
        }

        private static string Count(int amount, string unit)
        {
            return amount + " " + unit + (amount == 1 ? string.Empty : "s");
        }
    }

    // string.Humanize(): UNVERIFIED. Returns the input unchanged (no PascalCase split), so folder names
    // built from GameSaveItemData.Platform keep their current text. Null becomes string.Empty.
    public static class ShimStringHumanize
    {
        public static string Humanize(this string value)
        {
            return value ?? string.Empty;
        }

        internal static string SplitPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(text[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(c);
            }

            return builder.ToString();
        }
    }

    // string.DehumanizeTo<T>() for enums: inverse of Enum.Humanize(). Used by UI call sites only
    // (DialogExtensions.cs:663, NewCustomDialog.cs:113, MainWindow.cs:1078, 1660, 1759).
    public static class ShimDehumanizeExtensions
    {
        public static T DehumanizeTo<T>(this string value) where T : struct
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            Type enumType = typeof(T);
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("DehumanizeTo<T> requires an enum type.");
            }

            string wanted = value.Trim();
            Array candidates = Enum.GetValues(enumType);

            // First pass: match the Humanize() text exactly, ignoring case.
            foreach (object candidate in candidates)
            {
                Enum enumValue = (Enum)candidate;
                if (string.Equals(enumValue.Humanize(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return (T)candidate;
                }
            }

            // Second pass: match the enum name with spaces removed, ignoring case.
            string wantedNoSpaces = wanted.Replace(" ", string.Empty);
            foreach (object candidate in candidates)
            {
                string name = candidate.ToString();
                if (string.Equals(name, wantedNoSpaces, StringComparison.OrdinalIgnoreCase))
                {
                    return (T)candidate;
                }
            }

            throw new ArgumentException("No " + enumType.Name + " value matches '" + value + "'.");
        }
    }

    // ByteSize, as returned by long/int.Bytes(). Stores the raw byte count.
    // Humanize output: scaled by 1024 up to EB, formatted with the given .NET format (default "#.##").
    // UNVERIFIED: unit symbols and default format are a best guess at Humanizer's ByteSize output.
    public sealed class ByteSize
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        public ByteSize(long bytes)
        {
            Bytes = bytes;
        }

        public long Bytes { get; private set; }

        public string Humanize(string format = "")
        {
            string pattern = string.IsNullOrEmpty(format) ? "#.##" : format;
            double value = Bytes;
            int unit = 0;
            while (Math.Abs(value) >= 1024 && unit < Units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString(pattern, CultureInfo.CurrentCulture) + " " + Units[unit];
        }
    }

    // long.Bytes() and int.Bytes(). Value receivers, so no null guard is needed.
    public static class ByteSizeExtensions
    {
        public static ByteSize Bytes(this long value)
        {
            return new ByteSize(value);
        }

        public static ByteSize Bytes(this int value)
        {
            return new ByteSize(value);
        }
    }
}
