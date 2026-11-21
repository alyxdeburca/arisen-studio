using System;
using System.Collections.Generic;

namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>A field of <see cref="LibraryRowInfo"/> that a page can show as a column or offer as a filter.</summary>
    internal enum LibraryField
    {
        Platform,
        Category,
        Name,
        SystemType,
        ModType,
        Region,
        Version,
        Firmware,
        Creator,
        Status,
        Size,
        Modified,
        TitleId,
        DashType,
        Count
    }

    /// <summary>One grid column: which field, and its caption (a localizer key with an English fallback).</summary>
    internal sealed class LibraryColumnDef
    {
        public LibraryField Field { get; set; }
        public string CaptionKey { get; set; }
        public string CaptionFallback { get; set; }
    }

    /// <summary>
    /// One filter box above the grid. A drop-down of the distinct values of the field, or, when
    /// <see cref="FixedValues"/> is set, of those values (used for Status).
    /// </summary>
    internal sealed class LibraryFilterDef
    {
        public LibraryField Field { get; set; }
        public string CaptionKey { get; set; }
        public string CaptionFallback { get; set; }
        public int Width { get; set; }
        public string[] FixedValues { get; set; }
    }

    /// <summary>
    /// The columns and filters of one page on one platform. The old pages built a different DataTable and filter bar per
    /// platform; a page returns the right layout from CreateLayout.
    /// </summary>
    internal sealed class LibraryLayout
    {
        private readonly List<LibraryColumnDef> columns = new List<LibraryColumnDef>();
        private readonly List<LibraryFilterDef> filters = new List<LibraryFilterDef>();

        public IList<LibraryColumnDef> Columns
        {
            get { return columns; }
        }

        public IList<LibraryFilterDef> Filters
        {
            get { return filters; }
        }

        /// <summary>The field of the name search box (it matches anywhere in the text), or null for no search box.</summary>
        public LibraryField? TextFilterField { get; set; }

        public string TextFilterKey { get; set; }
        public string TextFilterFallback { get; set; }

        /// <summary>Shows the Modified date filter (equal, at least, at most), as in the old Packages filter bar.</summary>
        public bool DateFilter { get; set; }

        /// <summary>
        /// Region values like "PAL NTSC" and version values like "1.00/1.01" are split into single entries for the filter lists
        /// and the match (the old Game Saves page did this, ledger MW3-081).
        /// </summary>
        public bool SplitRegionAndVersion { get; set; }

        /// <summary>The field the list is sorted by when the page opens (the old default sort option).</summary>
        public LibraryField DefaultSort { get; set; }

        public LibraryLayout()
        {
            DefaultSort = LibraryField.Name;
        }

        public LibraryLayout Column(LibraryField field, string captionKey, string captionFallback)
        {
            columns.Add(new LibraryColumnDef { Field = field, CaptionKey = captionKey, CaptionFallback = captionFallback });
            return this;
        }

        public LibraryLayout Filter(LibraryField field, string captionKey, string captionFallback, int width)
        {
            filters.Add(new LibraryFilterDef { Field = field, CaptionKey = captionKey, CaptionFallback = captionFallback, Width = width });
            return this;
        }

        /// <summary>The usual Status filter: all, Not installed, Installed, Downloaded.</summary>
        public LibraryLayout StatusFilter()
        {
            filters.Add(new LibraryFilterDef
            {
                Field = LibraryField.Status,
                CaptionKey = "STATUS",
                CaptionFallback = "Status",
                Width = 120,
                FixedValues = new[] { LibraryStatus.NotInstalled, LibraryStatus.Installed, LibraryStatus.Downloaded }
            });
            return this;
        }

        public LibraryLayout NameSearch(LibraryField field, string captionKey, string captionFallback)
        {
            TextFilterField = field;
            TextFilterKey = captionKey;
            TextFilterFallback = captionFallback;
            return this;
        }
    }

    /// <summary>What a page shows besides the grid: which command buttons it has, and their texts.</summary>
    internal sealed class LibraryPageConfig
    {
        public LibraryKind Kind { get; set; }

        /// <summary>English page title (the navigation item text).</summary>
        public string Title { get; set; }

        /// <summary>"Show favorites" check button (ledger MW2-084, MW3-010).</summary>
        public bool FavoritesToggle { get; set; }

        /// <summary>"Sort" button that opens the sort options dialog.</summary>
        public bool SortButton { get; set; }

        /// <summary>"Add to favorites" button.</summary>
        public bool AddFavoriteButton { get; set; }

        /// <summary>"Download" button.</summary>
        public bool DownloadButton { get; set; }

        /// <summary>The Custom Mods commands: Add, Edit, Delete, Install, Uninstall.</summary>
        public bool CustomModCommands { get; set; }

        public string DetailsKey { get; set; }
        public string DetailsFallback { get; set; }

        public LibraryPageConfig()
        {
            DetailsKey = "SHOW_DETAILS";
            DetailsFallback = "Show Details";
        }
    }

    /// <summary>Text and comparison helpers for <see cref="LibraryField"/>.</summary>
    internal static class LibraryFields
    {
        /// <summary>The raw text of a text field (Size and Modified give their number and date as text).</summary>
        public static string Text(LibraryRowInfo row, LibraryField field)
        {
            switch (field)
            {
                case LibraryField.Platform:
                    return row.Platform;
                case LibraryField.Category:
                    return row.Category;
                case LibraryField.Name:
                    return row.Name;
                case LibraryField.SystemType:
                    return row.SystemType;
                case LibraryField.ModType:
                    return row.ModType;
                case LibraryField.Region:
                    return row.Region;
                case LibraryField.Version:
                    return row.Version;
                case LibraryField.Firmware:
                    return row.Firmware;
                case LibraryField.Creator:
                    return row.Creator;
                case LibraryField.Status:
                    return row.Status;
                case LibraryField.Size:
                    return row.SizeBytes.ToString();
                case LibraryField.Modified:
                    return row.Modified.HasValue ? row.Modified.Value.ToString("s") : null;
                case LibraryField.TitleId:
                    return row.TitleId;
                case LibraryField.DashType:
                    return row.DashType;
                case LibraryField.Count:
                    return row.Count.ToString();
                default:
                    return null;
            }
        }

        /// <summary>Compares two rows by one field: numbers as numbers, dates as dates, everything else as text ignoring case.</summary>
        public static int Compare(LibraryRowInfo a, LibraryRowInfo b, LibraryField field)
        {
            switch (field)
            {
                case LibraryField.Size:
                    return a.SizeBytes.CompareTo(b.SizeBytes);
                case LibraryField.Modified:
                    return (a.Modified ?? DateTime.MinValue).CompareTo(b.Modified ?? DateTime.MinValue);
                case LibraryField.Count:
                    return a.Count.CompareTo(b.Count);
                default:
                    return string.Compare(Text(a, field), Text(b, field), StringComparison.CurrentCultureIgnoreCase);
            }
        }

        /// <summary>Values the old filter lists left out ("n/a", "-", "all"; ledger MW2-079, MW2-096).</summary>
        public static bool IsIgnorable(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            string v = value.Trim();
            return v == "-" || PageUi.EqualsIgnoreCase(v, "n/a") || PageUi.EqualsIgnoreCase(v, "all");
        }

        /// <summary>
        /// The filterable values of a field of one row. Usually the single text; with <paramref name="split"/> a region is cut at
        /// spaces and slashes and a version at slashes. Empty and ignorable values give nothing.
        /// </summary>
        public static List<string> Tokens(LibraryRowInfo row, LibraryField field, bool split)
        {
            List<string> result = new List<string>();
            string text = Text(row, field);
            if (IsIgnorable(text))
            {
                return result;
            }
            string[] parts;
            if (split && field == LibraryField.Region)
            {
                parts = text.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (split && field == LibraryField.Version)
            {
                parts = text.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                parts = new[] { text };
            }
            foreach (string part in parts)
            {
                string token = part.Trim();
                if (!IsIgnorable(token) && !result.Contains(token))
                {
                    result.Add(token);
                }
            }
            return result;
        }
    }
}
