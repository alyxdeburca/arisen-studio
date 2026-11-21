using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Controls;
using ArisenStudio.Shell;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// The Downloads page (slice S3, old PageDownloads, ledger MW1-115 to MW1-133): a command strip, a filter row and a
    /// grid of downloaded files.
    ///
    /// Use: set <see cref="DataSource"/> and <see cref="DownloadsFolder"/>, subscribe to the events, register the page
    /// under ShellPageKeys.Downloads. The page reloads every time it becomes visible (<see cref="OnPageShown"/>).
    /// The page never opens a folder or a dialog of another slice itself; it raises events and the shell acts.
    /// The filter lists (platform, category, mod type, region, version) are built from the rows, so there is no
    /// separate database dependency.
    /// </summary>
    internal sealed class DownloadsPage : UserControl, IShellPage
    {
        private const string AllText = "(All)";

        private IDownloadsDataSource dataSource;
        private string downloadsFolder = string.Empty;
        private bool useRelativeTimes = true;
        private Func<string, string> localizer;

        private readonly ActionStrip strip = new ActionStrip();
        private readonly ListGrid grid = new ListGrid();
        private readonly ComboBox comboPlatform = NewFilterCombo();
        private readonly ComboBox comboCategory = NewFilterCombo();
        private readonly TextBox textFileName = new TextBox();
        private readonly ComboBox comboModType = NewFilterCombo();
        private readonly ComboBox comboRegion = NewFilterCombo();
        private readonly ComboBox comboVersion = NewFilterCombo();
        private readonly Button buttonDateMode = new Button();
        private readonly DateTimePicker pickerDate = new DateTimePicker();
        private readonly Label labelPlatform = new Label();
        private readonly Label labelCategory = new Label();
        private readonly Label labelFileName = new Label();
        private readonly Label labelModType = new Label();
        private readonly Label labelRegion = new Label();
        private readonly Label labelVersion = new Label();
        private readonly Label labelDate = new Label();
        private readonly Label labelCount = new Label();

        private ToolStripButton buttonOpenFolder;
        private ToolStripButton buttonOpenFile;
        private ToolStripButton buttonDelete;
        private ToolStripButton buttonDetails;

        private List<DownloadRowInfo> allRows = new List<DownloadRowInfo>();
        private readonly List<DownloadRowInfo> shownRows = new List<DownloadRowInfo>();
        private CompareMode dateMode = CompareMode.Equal;
        private bool suppressFilter;

        /// <summary>Open the downloads folder in the system file browser (ledger MW1-115). Item is the folder path.</summary>
        public event EventHandler<ItemEventArgs<string>> OpenFolderRequested;

        /// <summary>
        /// Show a downloaded file in the system file browser (ledger MW1-116). Item is the file path; the page has
        /// already checked that it exists. The old code used explorer.exe /select; on Linux the shell can open the
        /// parent folder.
        /// </summary>
        public event EventHandler<ItemEventArgs<string>> RevealFileRequested;

        /// <summary>Open the details dialog of the mod behind a download (ledger MW1-119, MW1-133).</summary>
        public event EventHandler<ItemEventArgs<DownloadRowInfo>> ViewDetailsRequested;

        /// <summary>A download was deleted. The page has already told the data source; the shell may save settings.</summary>
        public event EventHandler<ItemEventArgs<DownloadRowInfo>> DownloadDeleted;

        /// <summary>A message for the shell's status strip.</summary>
        public event EventHandler<PageStatusEventArgs> StatusMessage;

        public DownloadsPage()
        {
            SuspendLayout();
            Font = System.Drawing.SystemFonts.MessageBoxFont;

            buttonOpenFolder = strip.AddAction("downloads-open-folder", "Open Folder", null, ButtonOpenFolder_Click, ActionAlign.Left);
            buttonOpenFile = strip.AddAction("downloads-open-file", "Open File", null, ButtonOpenFile_Click, ActionAlign.Left);
            buttonDelete = strip.AddAction("downloads-delete", "Delete Item", null, ButtonDelete_Click, ActionAlign.Left);
            buttonDetails = strip.AddAction("downloads-details", "View Details", null, ButtonDetails_Click, ActionAlign.Left);

            FlowLayoutPanel filters = new FlowLayoutPanel();
            filters.Dock = DockStyle.Top;
            filters.AutoSize = true;
            filters.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filters.WrapContents = true;
            filters.Padding = new Padding(4);

            AddFilter(filters, labelPlatform, comboPlatform, 130);
            AddFilter(filters, labelCategory, comboCategory, 170);
            AddFilter(filters, labelFileName, textFileName, 150);
            AddFilter(filters, labelModType, comboModType, 130);
            AddFilter(filters, labelRegion, comboRegion, 90);
            AddFilter(filters, labelVersion, comboVersion, 90);

            buttonDateMode.Width = 36;
            buttonDateMode.Text = PageUi.ModeText(dateMode);
            buttonDateMode.Click += ButtonDateMode_Click;
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
            AddFilter(filters, labelDate, dateBox, 0);

            labelCount.Dock = DockStyle.Bottom;
            labelCount.AutoSize = false;
            labelCount.Height = 20;
            labelCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            comboPlatform.SelectedIndexChanged += Filter_Changed;
            comboCategory.SelectedIndexChanged += Filter_Changed;
            textFileName.TextChanged += Filter_Changed;
            comboModType.SelectedIndexChanged += Filter_Changed;
            comboRegion.SelectedIndexChanged += Filter_Changed;
            comboVersion.SelectedIndexChanged += Filter_Changed;

            grid.Dock = DockStyle.Fill;
            grid.SelectionChanged += Grid_SelectionChanged;
            grid.RowActivated += Grid_RowActivated;

            // Fill first, edge-docked controls after it (the last control added is laid out first).
            Controls.Add(grid);
            Controls.Add(labelCount);
            Controls.Add(filters);
            Controls.Add(strip);

            ApplyTexts();
            UpdateButtons();
            ResumeLayout(true);
        }

        /// <summary>Where the downloads come from. Null shows an empty list.</summary>
        public IDownloadsDataSource DataSource
        {
            get { return dataSource; }
            set { dataSource = value; }
        }

        /// <summary>The resolved downloads folder (the old Settings.PathDownloads.GetFullPath()). The Open Folder command needs it.</summary>
        public string DownloadsFolder
        {
            get { return downloadsFolder; }
            set
            {
                downloadsFolder = value ?? string.Empty;
                UpdateButtons();
            }
        }

        /// <summary>Settings.UseRelativeTimes. Takes effect on the next reload.</summary>
        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set { useRelativeTimes = value; }
        }

        /// <summary>Optional text lookup. A null or empty answer falls back to English. Call <see cref="ApplyLanguage"/> after changing it.</summary>
        public Func<string, string> Localizer
        {
            get { return localizer; }
            set { localizer = value; }
        }

        public void ApplyLanguage()
        {
            ApplyTexts();
            ApplyFilters();
        }

        public void OnPageShown()
        {
            Reload();
        }

        /// <summary>Reads the downloads again and rebuilds the filter lists and the grid (ledger MW1-129, MW1-130).</summary>
        public void Reload()
        {
            List<DownloadRowInfo> rows = new List<DownloadRowInfo>();
            if (dataSource != null)
            {
                try
                {
                    IList<DownloadRowInfo> loaded = dataSource.GetDownloads();
                    if (loaded != null)
                    {
                        rows.AddRange(loaded);
                    }
                }
                catch (Exception ex)
                {
                    RaiseStatus("Could not load the downloads: " + ex.Message, true);
                }
            }
            allRows = rows;
            FillFilterLists();
            ApplyFilters();
        }

        // ---- filters ----

        private static ComboBox NewFilterCombo()
        {
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            return combo;
        }

        private static void AddFilter(FlowLayoutPanel parent, Label label, Control editor, int width)
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
            parent.Controls.Add(cell);
        }

        private void ApplyTexts()
        {
            labelPlatform.Text = PageUi.Text(localizer, "PLATFORM", "Platform");
            labelCategory.Text = PageUi.Text(localizer, "CATEGORY", "Category");
            labelFileName.Text = PageUi.Text(localizer, "FILE_NAME", "File Name");
            labelModType.Text = PageUi.Text(localizer, "MOD_TYPE", "Mod Type");
            labelRegion.Text = PageUi.Text(localizer, "REGION", "Region");
            labelVersion.Text = PageUi.Text(localizer, "VERSION", "Version");
            labelDate.Text = PageUi.Text(localizer, "DATE", "Date");
            buttonOpenFolder.Text = PageUi.Text(localizer, "OPEN_FOLDER", "Open Folder");
            buttonOpenFile.Text = PageUi.Text(localizer, "OPEN_FILE", "Open File");
            buttonDelete.Text = PageUi.Text(localizer, "DELETE_ITEM", "Delete Item");
            buttonDetails.Text = PageUi.Text(localizer, "VIEW_DETAILS", "View Details");
        }

        private void FillFilterLists()
        {
            suppressFilter = true;
            try
            {
                FillCombo(comboPlatform, Distinct(delegate (DownloadRowInfo r) { return r.PlatformText; }));
                FillCombo(comboCategory, Distinct(delegate (DownloadRowInfo r) { return r.CategoryTitle; }));
                FillCombo(comboModType, Distinct(delegate (DownloadRowInfo r) { return r.ModType; }));
                FillCombo(comboRegion, Distinct(delegate (DownloadRowInfo r) { return r.Region; }));
                FillCombo(comboVersion, Distinct(delegate (DownloadRowInfo r) { return r.Version; }));
            }
            finally
            {
                suppressFilter = false;
            }
        }

        private List<string> Distinct(Func<DownloadRowInfo, string> selector)
        {
            List<string> values = new List<string>();
            foreach (DownloadRowInfo row in allRows)
            {
                string value = selector(row);
                if (!string.IsNullOrEmpty(value) && !values.Contains(value))
                {
                    values.Add(value);
                }
            }
            values.Sort(StringComparer.CurrentCultureIgnoreCase);
            return values;
        }

        // Rebuilds the list and keeps the user's choice when it is still there (the old code appended duplicates, MW1-128).
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

        private static string Chosen(ComboBox combo)
        {
            return combo.SelectedIndex > 0 ? combo.SelectedItem as string : null;
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
            buttonDateMode.Text = PageUi.ModeText(dateMode);
            if (pickerDate.Checked)
            {
                ApplyFilters();
            }
        }

        private bool Matches(DownloadRowInfo row, string platform, string category, string modType, string region, string version)
        {
            if (platform != null && !PageUi.EqualsIgnoreCase(row.PlatformText, platform))
            {
                return false;
            }
            if (category != null && !PageUi.EqualsIgnoreCase(row.CategoryTitle, category))
            {
                return false;
            }
            if (!PageUi.ContainsIgnoreCase(row.FileName, textFileName.Text.Trim()))
            {
                return false;
            }
            if (modType != null && !PageUi.EqualsIgnoreCase(row.ModType, modType))
            {
                return false;
            }
            if (region != null && !PageUi.EqualsIgnoreCase(row.Region, region))
            {
                return false;
            }
            if (version != null && !PageUi.EqualsIgnoreCase(row.Version, version))
            {
                return false;
            }
            // The old date filter never took effect (ledger MW1-127, MW1-130). Here it does: the picker's check box turns it on.
            if (pickerDate.Checked && !PageUi.Compare(row.DateTime, pickerDate.Value, dateMode))
            {
                return false;
            }
            return true;
        }

        private void ApplyFilters()
        {
            string platform = Chosen(comboPlatform);
            string category = Chosen(comboCategory);
            string modType = Chosen(comboModType);
            string region = Chosen(comboRegion);
            string version = Chosen(comboVersion);

            shownRows.Clear();
            DataTable table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "PLATFORM", "Platform"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "CATEGORY", "Category"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "FILE_NAME", "File Name"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "MOD_TYPE", "Mod Type"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "REGION", "Region"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "VERSION", "Version"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "DATE", "Date"), typeof(string));

            foreach (DownloadRowInfo row in allRows)
            {
                if (!Matches(row, platform, category, modType, region, version))
                {
                    continue;
                }
                shownRows.Add(row);
                bool missing = string.IsNullOrEmpty(row.FilePath) || !File.Exists(row.FilePath);
                table.Rows.Add(
                    row.Key,
                    row.PlatformText,
                    row.CategoryTitle,
                    (row.FileName ?? string.Empty) + (missing ? " (file missing)" : string.Empty),
                    row.ModType,
                    row.Region,
                    row.Version,
                    PageUi.FormatDate(row.DateTime, useRelativeTimes));
            }

            grid.HideColumn("Key");
            grid.DataSource = table;
            if (grid.RowCount > 0)
            {
                grid.SelectFirstRow();
            }
            labelCount.Text = shownRows.Count + " / " + allRows.Count;
            UpdateButtons();
        }

        // ---- selection and commands ----

        private DownloadRowInfo SelectedRow()
        {
            string key = grid.GetSelectedValue("Key");
            if (key == null)
            {
                return null;
            }
            foreach (DownloadRowInfo row in shownRows)
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
            buttonOpenFolder.Enabled = downloadsFolder.Length > 0;
            buttonOpenFile.Enabled = hasRow;
            buttonDelete.Enabled = hasRow;
            buttonDetails.Enabled = hasRow;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void Grid_RowActivated(object sender, EventArgs e)
        {
            RaiseDetails();
        }

        private void ButtonOpenFolder_Click(object sender, EventArgs e)
        {
            if (downloadsFolder.Length == 0)
            {
                return;
            }
            EventHandler<ItemEventArgs<string>> handler = OpenFolderRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<string>(downloadsFolder));
            }
        }

        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            DownloadRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(row.FilePath) || !File.Exists(row.FilePath))
            {
                MessageBox.Show(this, "The file was not found:" + Environment.NewLine + row.FilePath, "Open File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            EventHandler<ItemEventArgs<string>> handler = RevealFileRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<string>(row.FilePath));
            }
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            DownloadRowInfo row = SelectedRow();
            if (row == null || dataSource == null)
            {
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "Delete this download?" + Environment.NewLine + row.FileName,
                "Delete Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            try
            {
                dataSource.DeleteDownload(row);
            }
            catch (Exception ex)
            {
                RaiseStatus("Could not delete the download: " + ex.Message, true);
                MessageBox.Show(this, ex.Message, "Delete Item", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            EventHandler<ItemEventArgs<DownloadRowInfo>> handler = DownloadDeleted;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<DownloadRowInfo>(row));
            }
            Reload();
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            RaiseDetails();
        }

        private void RaiseDetails()
        {
            DownloadRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            EventHandler<ItemEventArgs<DownloadRowInfo>> handler = ViewDetailsRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<DownloadRowInfo>(row));
            }
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
