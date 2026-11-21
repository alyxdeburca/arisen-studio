using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ArisenStudio.Controls;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Shell;

namespace ArisenStudio.Forms.Pages.Library
{
    /// <summary>
    /// The shared template of the nine library and catalog pages (slice S5; the old PageCustomMods, PageGameMods*, PageHomebrew*,
    /// PageTrainers, PageResources, PagePackages, PageGames, PageGameSaves, PageGameCheats). A command strip, a filter bar and a
    /// <see cref="ListGrid"/>. A page class only says which columns, filters and buttons it has (<see cref="CreateLayout"/> and
    /// the <see cref="LibraryPageConfig"/>); everything else is here.
    ///
    /// Use: set <see cref="DataSource"/> and, for pages that depend on it, <see cref="Platform"/>; subscribe to the events;
    /// register the page under its ShellPageKeys key. The page reloads every time it becomes visible (<see cref="OnPageShown"/>).
    /// The page never opens a dialog of another slice or starts a transfer itself; it raises events and the shell acts.
    /// The filter lists are built from the rows, so there is no separate database dependency.
    /// </summary>
    internal abstract class LibraryPageBase : UserControl, IShellPage
    {
        private const string AllText = "(All)";
        private const string MissingText = "Missing";
        private const string KeyColumn = "Key";

        private readonly LibraryPageConfig config;
        private ILibraryDataSource dataSource = new StubLibraryDataSource();
        private LibraryPlatform platform = LibraryPlatform.PS3;
        private bool useRelativeTimes = true;
        private bool useFormattedFileSizes = true;
        private Func<string, string> localizer;

        private readonly ActionStrip strip = new ActionStrip();
        private readonly FlowLayoutPanel filterPanel = new FlowLayoutPanel();
        private readonly ListGrid grid = new ListGrid();
        private readonly Label labelCount = new Label();

        private ToolStripButton buttonFavorites;
        private ToolStripButton buttonSort;
        private ToolStripButton buttonAddFavorite;
        private ToolStripButton buttonDownload;
        private ToolStripButton buttonAdd;
        private ToolStripButton buttonEdit;
        private ToolStripButton buttonDelete;
        private ToolStripButton buttonDetails;
        private ToolStripButton buttonInstall;
        private ToolStripButton buttonUninstall;

        // Built from the layout on the first reload and again when the platform changes.
        private LibraryLayout layout;
        private bool layoutDirty = true;
        private readonly List<FilterBox> filterBoxes = new List<FilterBox>();
        private TextBox textFilter;
        private Label labelTextFilter;
        private Button buttonDateMode;
        private DateTimePicker pickerDate;
        private Label labelDate;
        private CompareMode dateMode = CompareMode.Equal;

        private List<LibraryRowInfo> allRows = new List<LibraryRowInfo>();
        private readonly List<LibraryRowInfo> shownRows = new List<LibraryRowInfo>();
        private LibraryField sortField = LibraryField.Name;
        private System.Windows.Forms.SortOrder sortOrder = System.Windows.Forms.SortOrder.Ascending;
        private bool showFavorites;
        private bool suppressFilter;

        private sealed class FilterBox
        {
            public LibraryFilterDef Def;
            public ComboBox Combo;
            public Label Caption;
        }

        /// <summary>Show the details of the row: its details dialog (double-click or the details button; ledger MW2-083, MW2-088, MW3-009, MW3-098, MW3-112).</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> DetailsRequested;

        /// <summary>Download the row's last download file through the transfer dialog (ledger MW2-087, MW3-013, MW3-059, MW3-074). Not raised by the pages that have no Download button.</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> DownloadRequested;

        /// <summary>Custom Mods "Add": open the New Custom Mod dialog with an empty item (ledger MW2-067).</summary>
        public event EventHandler AddCustomModRequested;

        /// <summary>Custom Mods "Edit": open the dialog on the row's custom mod; the shell then calls <see cref="Reload"/> (ledger MW2-068).</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> EditCustomModRequested;

        /// <summary>Custom Mods "Install" through the transfer dialog (ledger MW2-071).</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> InstallRequested;

        /// <summary>Custom Mods "Uninstall" through the transfer dialog (ledger MW2-072).</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> UninstallRequested;

        /// <summary>A custom mod was deleted. The page has already told the data source; the shell may save settings (ledger MW2-069).</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> CustomModDeleted;

        /// <summary>A row was added to the favourites. The page has already told the data source; the shell may save settings and refresh the dashboard.</summary>
        public event EventHandler<ItemEventArgs<LibraryRowInfo>> FavoriteAdded;

        /// <summary>A message for the shell's status strip.</summary>
        public event EventHandler<PageStatusEventArgs> StatusMessage;

        protected LibraryPageBase(LibraryPageConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            this.config = config;

            SuspendLayout();
            Font = SystemFonts.MessageBoxFont;

            // Same order as the old tiles: favourites, sort, add favourite, download, details; the custom mod page has its own set.
            if (config.CustomModCommands)
            {
                buttonSort = strip.AddAction("library-sort", "Sort", null, ButtonSort_Click, ActionAlign.Left);
                buttonAdd = strip.AddAction("library-add", "Add", null, ButtonAdd_Click, ActionAlign.Left);
                buttonEdit = strip.AddAction("library-edit", "Edit", null, ButtonEdit_Click, ActionAlign.Left);
                buttonDelete = strip.AddAction("library-delete", "Delete", null, ButtonDelete_Click, ActionAlign.Left);
                buttonDetails = strip.AddAction("library-details", config.DetailsFallback, null, ButtonDetails_Click, ActionAlign.Left);
                buttonInstall = strip.AddAction("library-install", "Install", null, ButtonInstall_Click, ActionAlign.Left);
                buttonUninstall = strip.AddAction("library-uninstall", "Uninstall", null, ButtonUninstall_Click, ActionAlign.Left);
            }
            else
            {
                if (config.FavoritesToggle)
                {
                    buttonFavorites = strip.AddAction("library-favorites", "Show Favorites", null, ButtonFavorites_Click, ActionAlign.Left);
                    buttonFavorites.CheckOnClick = true;
                }
                if (config.SortButton)
                {
                    buttonSort = strip.AddAction("library-sort", "Sort", null, ButtonSort_Click, ActionAlign.Left);
                }
                if (config.AddFavoriteButton)
                {
                    buttonAddFavorite = strip.AddAction("library-add-favorite", "Add to Favorites", null, ButtonAddFavorite_Click, ActionAlign.Left);
                }
                if (config.DownloadButton)
                {
                    buttonDownload = strip.AddAction("library-download", "Download", null, ButtonDownload_Click, ActionAlign.Left);
                }
                buttonDetails = strip.AddAction("library-details", config.DetailsFallback, null, ButtonDetails_Click, ActionAlign.Left);
            }

            filterPanel.Dock = DockStyle.Top;
            filterPanel.AutoSize = true;
            filterPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filterPanel.WrapContents = true;
            filterPanel.Padding = new Padding(4);

            labelCount.Dock = DockStyle.Bottom;
            labelCount.AutoSize = false;
            labelCount.Height = 20;
            labelCount.TextAlign = ContentAlignment.MiddleLeft;

            grid.Dock = DockStyle.Fill;
            grid.SelectionChanged += Grid_SelectionChanged;
            grid.RowActivated += Grid_RowActivated;

            // Fill first, edge-docked controls after it (the last control added is laid out first).
            Controls.Add(grid);
            Controls.Add(labelCount);
            Controls.Add(filterPanel);
            Controls.Add(strip);

            ApplyButtonTexts();
            UpdateButtons();
            ResumeLayout(true);
        }

        /// <summary>The columns and filters of this page on the given platform. Called on each reload after the platform changed; must return a new layout.</summary>
        protected abstract LibraryLayout CreateLayout(LibraryPlatform platform);

        protected LibraryKind Kind
        {
            get { return config.Kind; }
        }

        /// <summary>English page title (the navigation item text).</summary>
        public string Title
        {
            get { return config.Title; }
        }

        /// <summary>Where the rows come from. Null installs the empty <see cref="StubLibraryDataSource"/>. Takes effect on the next reload.</summary>
        public ILibraryDataSource DataSource
        {
            get { return dataSource; }
            set { dataSource = value ?? new StubLibraryDataSource(); }
        }

        /// <summary>
        /// The platform of the connected console profile. Game Mods and Homebrew show different columns and filters per platform.
        /// Takes effect on the next reload (page show, or call <see cref="Reload"/>).
        /// </summary>
        public LibraryPlatform Platform
        {
            get { return platform; }
            set
            {
                if (platform != value)
                {
                    platform = value;
                    layoutDirty = true;
                }
            }
        }

        /// <summary>Settings.UseRelativeTimes. Takes effect on the next reload.</summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set { useRelativeTimes = value; }
        }

        /// <summary>Settings.UseFormattedFileSizes. Takes effect on the next reload.</summary>
        public bool UseFormattedFileSizes
        {
            get { return useFormattedFileSizes; }
            set { useFormattedFileSizes = value; }
        }

        /// <summary>Optional text lookup. A null or empty answer falls back to English. Call <see cref="ApplyLanguage"/> after changing it.</summary>
        public Func<string, string> Localizer
        {
            get { return localizer; }
            set { localizer = value; }
        }

        /// <summary>Applies the localizer to the buttons and, by reloading, to the filter captions and the column headers.</summary>
        public void ApplyLanguage()
        {
            ApplyButtonTexts();
            if (layout != null)
            {
                layoutDirty = true;
                Reload();
            }
        }

        public void OnPageShown()
        {
            Reload();
        }

        /// <summary>
        /// Reads the rows again and rebuilds the filter lists and the grid. The page keeps the filter choices that still exist.
        /// Call it after the connection, the platform or the data changed. UI thread only.
        /// </summary>
        public void Reload()
        {
            List<LibraryRowInfo> rows = new List<LibraryRowInfo>();
            try
            {
                IList<LibraryRowInfo> loaded = dataSource.GetItems(config.Kind, platform);
                if (loaded != null)
                {
                    foreach (LibraryRowInfo row in loaded)
                    {
                        if (row != null)
                        {
                            rows.Add(row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseStatus("Could not load the " + config.Title + " list: " + ex.Message, true);
            }
            allRows = rows;

            if (layout == null || layoutDirty)
            {
                RebuildLayout();
            }
            FillFilterLists();
            ApplyFilters();
        }

        /// <summary>
        /// Shows a new status in one row, for example Downloading..., Installing..., then Installed (ledger MW3-124 to MW3-131,
        /// the old UpdateModsRowStatus*). Safe to call from any thread. Does nothing when the page has no Status column or the row is not listed.
        /// A row that no longer matches the Status filter stays until the next reload.
        /// </summary>
        public void SetRowStatus(string key, string status)
        {
            if (key == null)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(SetRowStatus), key, status);
                return;
            }
            foreach (LibraryRowInfo row in allRows)
            {
                if (row.Key == key)
                {
                    row.Status = status;
                }
            }
            string column = StatusColumnName();
            DataTable table = grid.DataSource;
            if (column == null || table == null || !table.Columns.Contains(column))
            {
                return;
            }
            foreach (DataRow dataRow in table.Rows)
            {
                if (string.Equals(dataRow[KeyColumn] as string, key, StringComparison.Ordinal))
                {
                    dataRow[column] = status ?? string.Empty;
                }
            }
        }

        // ---- layout ----

        private string T(string key, string fallback)
        {
            return PageUi.Text(localizer, key, fallback);
        }

        private string CaptionOf(LibraryColumnDef column)
        {
            return T(column.CaptionKey, column.CaptionFallback);
        }

        private string StatusColumnName()
        {
            if (layout == null)
            {
                return null;
            }
            foreach (LibraryColumnDef column in layout.Columns)
            {
                if (column.Field == LibraryField.Status)
                {
                    return CaptionOf(column);
                }
            }
            return null;
        }

        private void RebuildLayout()
        {
            layout = CreateLayout(platform);
            layoutDirty = false;
            sortField = layout.DefaultSort;
            sortOrder = System.Windows.Forms.SortOrder.Ascending;

            suppressFilter = true;
            try
            {
                SuspendLayout();
                filterPanel.SuspendLayout();
                foreach (Control old in filterPanel.Controls.Cast<Control>().ToList())
                {
                    filterPanel.Controls.Remove(old);
                    old.Dispose();
                }
                filterBoxes.Clear();
                textFilter = null;
                labelTextFilter = null;
                buttonDateMode = null;
                pickerDate = null;
                labelDate = null;

                // The old filter bar order: the drop-downs first, the name box among them. Here the name box comes after the first drop-down.
                bool textAdded = layout.TextFilterField == null;
                for (int i = 0; i < layout.Filters.Count; i++)
                {
                    LibraryFilterDef def = layout.Filters[i];
                    FilterBox box = new FilterBox();
                    box.Def = def;
                    box.Combo = new ComboBox();
                    box.Combo.DropDownStyle = ComboBoxStyle.DropDownList;
                    box.Combo.SelectedIndexChanged += Filter_Changed;
                    box.Caption = new Label();
                    box.Caption.Text = T(def.CaptionKey, def.CaptionFallback);
                    filterBoxes.Add(box);
                    AddFilterCell(box.Caption, box.Combo, def.Width);
                    if (!textAdded)
                    {
                        AddTextFilter();
                        textAdded = true;
                    }
                }
                if (!textAdded)
                {
                    AddTextFilter();
                }

                if (layout.DateFilter)
                {
                    labelDate = new Label();
                    labelDate.Text = T("MODIFIED_DATE", "Modified Date");
                    buttonDateMode = new Button();
                    buttonDateMode.Width = 36;
                    buttonDateMode.Text = PageUi.ModeText(dateMode);
                    buttonDateMode.Click += ButtonDateMode_Click;
                    pickerDate = new DateTimePicker();
                    pickerDate.Format = DateTimePickerFormat.Short;
                    pickerDate.ShowCheckBox = true;
                    pickerDate.Checked = false;
                    pickerDate.Width = 120;
                    pickerDate.ValueChanged += Filter_Changed;
                    FlowLayoutPanel dateBox = new FlowLayoutPanel();
                    dateBox.AutoSize = true;
                    dateBox.WrapContents = false;
                    dateBox.Controls.Add(buttonDateMode);
                    dateBox.Controls.Add(pickerDate);
                    AddFilterCell(labelDate, dateBox, 0);
                }
                filterPanel.ResumeLayout(true);
                ResumeLayout(true);
            }
            finally
            {
                suppressFilter = false;
            }
        }

        private void AddTextFilter()
        {
            labelTextFilter = new Label();
            labelTextFilter.Text = T(layout.TextFilterKey, layout.TextFilterFallback);
            textFilter = new TextBox();
            textFilter.TextChanged += Filter_Changed;
            AddFilterCell(labelTextFilter, textFilter, 150);
        }

        private void AddFilterCell(Label label, Control editor, int width)
        {
            FlowLayoutPanel cell = new FlowLayoutPanel();
            cell.FlowDirection = FlowDirection.TopDown;
            cell.WrapContents = false;
            cell.AutoSize = true;
            cell.Margin = new Padding(2);
            label.AutoSize = true;
            if (width > 0)
            {
                editor.Width = width;
            }
            cell.Controls.Add(label);
            cell.Controls.Add(editor);
            filterPanel.Controls.Add(cell);
        }

        private void ApplyButtonTexts()
        {
            SetText(buttonFavorites, "SHOW_FAVORITES", "Show Favorites");
            SetText(buttonSort, "SORT", "Sort");
            SetText(buttonAddFavorite, "ADD_TO_FAVORITES", "Add to Favorites");
            SetText(buttonDownload, "DOWNLOAD", "Download");
            SetText(buttonAdd, "ADD", "Add");
            SetText(buttonEdit, "EDIT", "Edit");
            SetText(buttonDelete, "DELETE", "Delete");
            SetText(buttonDetails, config.DetailsKey, config.DetailsFallback);
            SetText(buttonInstall, "INSTALL", "Install");
            SetText(buttonUninstall, "UNINSTALL", "Uninstall");
        }

        private void SetText(ToolStripButton button, string key, string fallback)
        {
            if (button != null)
            {
                button.Text = T(key, fallback);
            }
        }

        // ---- filters ----

        private void FillFilterLists()
        {
            suppressFilter = true;
            try
            {
                foreach (FilterBox box in filterBoxes)
                {
                    List<string> values;
                    if (box.Def.FixedValues != null)
                    {
                        values = new List<string>(box.Def.FixedValues);
                    }
                    else
                    {
                        values = Distinct(box.Def.Field);
                    }
                    FillCombo(box.Combo, values);
                }
            }
            finally
            {
                suppressFilter = false;
            }
        }

        private List<string> Distinct(LibraryField field)
        {
            List<string> values = new List<string>();
            foreach (LibraryRowInfo row in allRows)
            {
                foreach (string token in LibraryFields.Tokens(row, field, layout.SplitRegionAndVersion))
                {
                    if (!values.Contains(token))
                    {
                        values.Add(token);
                    }
                }
            }
            values.Sort(StringComparer.CurrentCultureIgnoreCase);
            return values;
        }

        // Rebuilds the list and keeps the user's choice when it is still there (the old code appended duplicates, ledger MW3-108).
        private static void FillCombo(ComboBox combo, List<string> values)
        {
            string previous = combo.SelectedIndex > 0 ? combo.SelectedItem as string : null;
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(AllText);
            foreach (string value in values)
            {
                combo.Items.Add(value);
            }
            int index = previous == null ? 0 : combo.Items.IndexOf(previous);
            combo.SelectedIndex = index < 0 ? 0 : index;
            combo.EndUpdate();
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!suppressFilter)
            {
                ApplyFilters();
            }
        }

        private void ButtonDateMode_Click(object sender, EventArgs e)
        {
            dateMode = PageUi.NextMode(dateMode);
            if (buttonDateMode != null)
            {
                buttonDateMode.Text = PageUi.ModeText(dateMode);
            }
            if (pickerDate != null && pickerDate.Checked)
            {
                ApplyFilters();
            }
        }

        private bool Matches(LibraryRowInfo row, List<KeyValuePair<LibraryField, string>> chosen)
        {
            if (showFavorites && !row.IsFavorite)
            {
                return false;
            }
            foreach (KeyValuePair<LibraryField, string> pair in chosen)
            {
                bool found = false;
                foreach (string token in LibraryFields.Tokens(row, pair.Key, layout.SplitRegionAndVersion))
                {
                    if (PageUi.EqualsIgnoreCase(token, pair.Value))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
            if (textFilter != null && layout.TextFilterField.HasValue
                && !PageUi.ContainsIgnoreCase(LibraryFields.Text(row, layout.TextFilterField.Value), textFilter.Text.Trim()))
            {
                return false;
            }
            if (pickerDate != null && pickerDate.Checked)
            {
                // A row without a date cannot match a date filter (old behaviour, ledger MW3-005).
                if (!row.Modified.HasValue || !PageUi.Compare(row.Modified.Value, pickerDate.Value, dateMode))
                {
                    return false;
                }
            }
            return true;
        }

        private sealed class RowComparer : IComparer<LibraryRowInfo>
        {
            private readonly LibraryField field;
            private readonly bool descending;

            public RowComparer(LibraryField field, bool descending)
            {
                this.field = field;
                this.descending = descending;
            }

            public int Compare(LibraryRowInfo a, LibraryRowInfo b)
            {
                int result = LibraryFields.Compare(a, b, field);
                return descending ? -result : result;
            }
        }

        private string DisplayText(LibraryRowInfo row, LibraryField field)
        {
            switch (field)
            {
                case LibraryField.Size:
                    return row.SizeBytes < 0 ? T("MISSING", MissingText) : PageUi.FormatSize(row.SizeBytes, useFormattedFileSizes);
                case LibraryField.Modified:
                    return row.Modified.HasValue ? PageUi.FormatDate(row.Modified.Value, useRelativeTimes) : T("MISSING", MissingText);
                case LibraryField.Count:
                    return row.Count.ToString();
                default:
                    return LibraryFields.Text(row, field) ?? string.Empty;
            }
        }

        private void ApplyFilters()
        {
            if (layout == null)
            {
                return;
            }
            List<KeyValuePair<LibraryField, string>> chosen = new List<KeyValuePair<LibraryField, string>>();
            foreach (FilterBox box in filterBoxes)
            {
                if (box.Combo.SelectedIndex > 0)
                {
                    string value = box.Combo.SelectedItem as string;
                    if (value != null)
                    {
                        chosen.Add(new KeyValuePair<LibraryField, string>(box.Def.Field, value));
                    }
                }
            }

            List<LibraryRowInfo> matching = new List<LibraryRowInfo>();
            foreach (LibraryRowInfo row in allRows)
            {
                if (Matches(row, chosen))
                {
                    matching.Add(row);
                }
            }

            // OrderBy is stable, so rows that compare equal keep the order the data source gave them.
            shownRows.Clear();
            shownRows.AddRange(matching.OrderBy(r => r, new RowComparer(sortField, sortOrder == System.Windows.Forms.SortOrder.Descending)));

            DataTable table = new DataTable();
            table.Columns.Add(KeyColumn, typeof(string));
            foreach (LibraryColumnDef column in layout.Columns)
            {
                table.Columns.Add(CaptionOf(column), typeof(string));
            }
            foreach (LibraryRowInfo row in shownRows)
            {
                object[] cells = new object[layout.Columns.Count + 1];
                cells[0] = row.Key ?? string.Empty;
                for (int i = 0; i < layout.Columns.Count; i++)
                {
                    cells[i + 1] = DisplayText(row, layout.Columns[i].Field);
                }
                table.Rows.Add(cells);
            }

            grid.HideColumn(KeyColumn);
            grid.DataSource = table;
            if (grid.RowCount > 0)
            {
                grid.SelectFirstRow();
            }
            labelCount.Text = shownRows.Count + " / " + allRows.Count;
            UpdateButtons();
        }

        // ---- selection and commands ----

        private LibraryRowInfo SelectedRow()
        {
            string key = grid.GetSelectedValue(KeyColumn);
            if (key == null)
            {
                return null;
            }
            foreach (LibraryRowInfo row in shownRows)
            {
                if (row.Key == key)
                {
                    return row;
                }
            }
            return null;
        }

        private void UpdateButtons()
        {
            bool hasRow = SelectedRow() != null;
            SetEnabled(buttonAddFavorite, hasRow);
            SetEnabled(buttonDownload, hasRow);
            SetEnabled(buttonEdit, hasRow);
            SetEnabled(buttonDelete, hasRow);
            SetEnabled(buttonDetails, hasRow);
            SetEnabled(buttonInstall, hasRow);
            SetEnabled(buttonUninstall, hasRow);
        }

        private static void SetEnabled(ToolStripButton button, bool enabled)
        {
            if (button != null)
            {
                button.Enabled = enabled;
            }
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void Grid_RowActivated(object sender, EventArgs e)
        {
            RaiseForSelected(DetailsRequested);
        }

        private void ButtonFavorites_Click(object sender, EventArgs e)
        {
            showFavorites = buttonFavorites != null && buttonFavorites.Checked;
            ApplyFilters();
        }

        private void ButtonSort_Click(object sender, EventArgs e)
        {
            if (layout == null)
            {
                return;
            }
            // The old dialog offered a "Creator" option that had no column; only the shown columns are offered here.
            string current = null;
            using (SortOptionsDialog dialog = new SortOptionsDialog())
            {
                foreach (LibraryColumnDef column in layout.Columns)
                {
                    dialog.SortOptions.Add(CaptionOf(column));
                    if (column.Field == sortField)
                    {
                        current = CaptionOf(column);
                    }
                }
                dialog.SortOption = current;
                dialog.SortOrder = sortOrder;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                foreach (LibraryColumnDef column in layout.Columns)
                {
                    if (CaptionOf(column) == dialog.SortOption)
                    {
                        sortField = column.Field;
                    }
                }
                sortOrder = dialog.SortOrder == System.Windows.Forms.SortOrder.Descending
                    ? System.Windows.Forms.SortOrder.Descending
                    : System.Windows.Forms.SortOrder.Ascending;
            }
            ApplyFilters();
        }

        private void ButtonAddFavorite_Click(object sender, EventArgs e)
        {
            LibraryRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            bool added;
            try
            {
                added = dataSource.AddFavorite(config.Kind, row);
            }
            catch (Exception ex)
            {
                RaiseStatus("Could not add the favorite: " + ex.Message, true);
                MessageBox.Show(this, ex.Message, T("ADD_TO_FAVORITES", "Add to Favorites"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            row.IsFavorite = true;
            if (added)
            {
                RaiseStatus("Added to favorites: " + row.Name, false);
                EventHandler<ItemEventArgs<LibraryRowInfo>> handler = FavoriteAdded;
                if (handler != null)
                {
                    handler(this, new ItemEventArgs<LibraryRowInfo>(row));
                }
            }
            else
            {
                RaiseStatus("Already in favorites: " + row.Name, false);
            }
        }

        private void ButtonDownload_Click(object sender, EventArgs e)
        {
            RaiseForSelected(DownloadRequested);
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            RaiseForSelected(DetailsRequested);
        }

        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            EventHandler handler = AddCustomModRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void ButtonEdit_Click(object sender, EventArgs e)
        {
            RaiseForSelected(EditCustomModRequested);
        }

        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            RaiseForSelected(InstallRequested);
        }

        private void ButtonUninstall_Click(object sender, EventArgs e)
        {
            RaiseForSelected(UninstallRequested);
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            LibraryRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "Delete this custom mod?" + Environment.NewLine + row.Name,
                T("DELETE", "Delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            try
            {
                dataSource.DeleteCustomMod(row);
            }
            catch (Exception ex)
            {
                RaiseStatus("Could not delete the custom mod: " + ex.Message, true);
                MessageBox.Show(this, ex.Message, T("DELETE", "Delete"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RaiseStatus("Deleted: " + row.Name, false);
            EventHandler<ItemEventArgs<LibraryRowInfo>> handler = CustomModDeleted;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<LibraryRowInfo>(row));
            }
            // The old page did not refresh the grid after a delete (ledger MW2-069); here the row goes away.
            Reload();
        }

        private void RaiseForSelected(EventHandler<ItemEventArgs<LibraryRowInfo>> handler)
        {
            LibraryRowInfo row = SelectedRow();
            if (row == null || handler == null)
            {
                return;
            }
            handler(this, new ItemEventArgs<LibraryRowInfo>(row));
        }

        private void RaiseStatus(string message, bool isError)
        {
            EventHandler<PageStatusEventArgs> handler = StatusMessage;
            if (handler != null)
            {
                handler(this, new PageStatusEventArgs(message, isError));
            }
        }
    }
}
