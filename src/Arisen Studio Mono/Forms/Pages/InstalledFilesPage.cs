using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using ArisenStudio.Controls;
using ArisenStudio.Shell;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// The Installed Files page (slice S3, old PageInstalledFiles, ledger MW1-134 to MW1-152): a command strip, a filter
    /// row and a grid of the mods installed on every console profile.
    ///
    /// Use: set <see cref="DataSource"/>, set <see cref="ConnectedProfileId"/> whenever the connection changes,
    /// subscribe to the events, register under ShellPageKeys.InstalledFiles. The page reloads every time it becomes
    /// visible. Uninstalling needs the transfer dialogs and a live connection, so the page only raises
    /// <see cref="UninstallRequested"/>; call <see cref="Reload"/> when the shell is done.
    /// </summary>
    internal sealed class InstalledFilesPage : UserControl, IShellPage
    {
        private const string AllText = "(All)";

        private IInstalledFilesDataSource dataSource;
        private int? connectedProfileId;
        private bool useRelativeTimes = true;
        private Func<string, string> localizer;

        private readonly ActionStrip strip = new ActionStrip();
        private readonly ListGrid grid = new ListGrid();
        private readonly ComboBox comboPlatform = new ComboBox();
        private readonly ComboBox comboCategory = new ComboBox();
        private readonly ComboBox comboModType = new ComboBox();
        private readonly ComboBox comboRegion = new ComboBox();
        private readonly ComboBox comboVersion = new ComboBox();
        private readonly TextBox textName = new TextBox();
        private readonly CheckBox checkTotalFiles = new CheckBox();
        private readonly Button buttonFilesMode = new Button();
        private readonly NumericUpDown numberTotalFiles = new NumericUpDown();
        private readonly Button buttonDateMode = new Button();
        private readonly DateTimePicker pickerDate = new DateTimePicker();
        private readonly Label labelPlatform = new Label();
        private readonly Label labelCategory = new Label();
        private readonly Label labelName = new Label();
        private readonly Label labelModType = new Label();
        private readonly Label labelRegion = new Label();
        private readonly Label labelVersion = new Label();
        private readonly Label labelDate = new Label();
        private readonly Label labelCount = new Label();

        private ToolStripButton buttonDelete;
        private ToolStripButton buttonUninstall;
        private ToolStripButton buttonDetails;

        private List<InstalledRowInfo> allRows = new List<InstalledRowInfo>();
        private readonly List<InstalledRowInfo> shownRows = new List<InstalledRowInfo>();
        private CompareMode filesMode = CompareMode.Equal;
        private CompareMode dateMode = CompareMode.Equal;
        private bool suppressFilter;

        /// <summary>Open the details dialog for the mod (ledger MW1-135). The row says whether it is a custom mod.</summary>
        public event EventHandler<ItemEventArgs<InstalledRowInfo>> ViewDetailsRequested;

        /// <summary>
        /// Uninstall the mod from the console (ledger MW1-136). Only raised when the connected profile is the row's
        /// profile. The shell opens the transfer dialog, then calls <see cref="Reload"/>.
        /// </summary>
        public event EventHandler<ItemEventArgs<InstalledRowInfo>> UninstallRequested;

        /// <summary>The record was forgotten (ledger MW1-134). The page already told the data source; the shell may save settings.</summary>
        public event EventHandler<ItemEventArgs<InstalledRowInfo>> RecordForgotten;

        public event EventHandler<PageStatusEventArgs> StatusMessage;

        public InstalledFilesPage()
        {
            SuspendLayout();
            Font = System.Drawing.SystemFonts.MessageBoxFont;

            buttonDelete = strip.AddAction("installed-delete", "Delete Item", null, ButtonDelete_Click, ActionAlign.Left);
            buttonUninstall = strip.AddAction("installed-uninstall", "Uninstall Item", null, ButtonUninstall_Click, ActionAlign.Left);
            buttonDetails = strip.AddAction("installed-details", "View Details", null, ButtonDetails_Click, ActionAlign.Left);

            comboPlatform.DropDownStyle = ComboBoxStyle.DropDownList;
            comboPlatform.Width = 130;
            comboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            comboCategory.Width = 170;
            comboModType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboModType.Width = 110;
            comboRegion.DropDownStyle = ComboBoxStyle.DropDownList;
            comboRegion.Width = 91;
            comboVersion.DropDownStyle = ComboBoxStyle.DropDownList;
            comboVersion.Width = 71;
            textName.Width = 150;

            checkTotalFiles.AutoSize = true;
            checkTotalFiles.CheckedChanged += CheckTotalFiles_CheckedChanged;
            buttonFilesMode.Width = 36;
            buttonFilesMode.Text = PageUi.ModeText(filesMode);
            buttonFilesMode.Click += ButtonFilesMode_Click;
            numberTotalFiles.Minimum = 0;
            numberTotalFiles.Maximum = 100000;
            numberTotalFiles.Width = 70;
            numberTotalFiles.Enabled = false;
            numberTotalFiles.ValueChanged += Filter_Changed;

            buttonDateMode.Width = 36;
            buttonDateMode.Text = PageUi.ModeText(dateMode);
            buttonDateMode.Click += ButtonDateMode_Click;
            pickerDate.Format = DateTimePickerFormat.Short;
            pickerDate.ShowCheckBox = true;
            pickerDate.Checked = false;
            pickerDate.Width = 120;
            pickerDate.ValueChanged += Filter_Changed;

            FlowLayoutPanel filters = new FlowLayoutPanel();
            filters.Dock = DockStyle.Top;
            filters.AutoSize = true;
            filters.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            filters.WrapContents = true;
            filters.Padding = new Padding(4);

            AddFilter(filters, labelPlatform, comboPlatform);
            AddFilter(filters, labelCategory, comboCategory);
            AddFilter(filters, labelName, textName);
            AddFilter(filters, labelModType, comboModType);
            AddFilter(filters, labelRegion, comboRegion);
            AddFilter(filters, labelVersion, comboVersion);

            FlowLayoutPanel filesBox = new FlowLayoutPanel();
            filesBox.AutoSize = true;
            filesBox.WrapContents = false;
            filesBox.Controls.Add(buttonFilesMode);
            filesBox.Controls.Add(numberTotalFiles);
            AddFilter(filters, checkTotalFiles, filesBox);

            FlowLayoutPanel dateBox = new FlowLayoutPanel();
            dateBox.AutoSize = true;
            dateBox.WrapContents = false;
            dateBox.Controls.Add(buttonDateMode);
            dateBox.Controls.Add(pickerDate);
            AddFilter(filters, labelDate, dateBox);

            comboPlatform.SelectedIndexChanged += Filter_Changed;
            comboCategory.SelectedIndexChanged += Filter_Changed;
            comboModType.SelectedIndexChanged += Filter_Changed;
            comboRegion.SelectedIndexChanged += Filter_Changed;
            comboVersion.SelectedIndexChanged += Filter_Changed;
            textName.TextChanged += Filter_Changed;

            labelCount.Dock = DockStyle.Bottom;
            labelCount.AutoSize = false;
            labelCount.Height = 20;
            labelCount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            grid.Dock = DockStyle.Fill;
            grid.SelectionChanged += Grid_SelectionChanged;
            grid.RowActivated += Grid_RowActivated;

            Controls.Add(grid);
            Controls.Add(labelCount);
            Controls.Add(filters);
            Controls.Add(strip);

            ApplyTexts();
            UpdateButtons();
            ResumeLayout(true);
        }

        public IInstalledFilesDataSource DataSource
        {
            get { return dataSource; }
            set { dataSource = value; }
        }

        /// <summary>
        /// The id of the console profile the app is connected to, or null when not connected. Uninstall is only
        /// enabled while connected (ledger MW1-136, MW1-149).
        /// </summary>
        public int? ConnectedProfileId
        {
            get { return connectedProfileId; }
            set
            {
                connectedProfileId = value;
                UpdateButtons();
            }
        }

        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set { useRelativeTimes = value; }
        }

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

        /// <summary>Reads the installed mods again (ledger MW1-148) and re-applies the filters.</summary>
        public void Reload()
        {
            List<InstalledRowInfo> rows = new List<InstalledRowInfo>();
            if (dataSource != null)
            {
                try
                {
                    IList<InstalledRowInfo> loaded = dataSource.GetInstalled();
                    if (loaded != null)
                    {
                        rows.AddRange(loaded);
                    }
                }
                catch (Exception ex)
                {
                    RaiseStatus("Could not load the installed files: " + ex.Message, true);
                }
            }
            allRows = rows;
            FillFilterLists();
            ApplyFilters();
        }

        // ---- filters ----

        private static void AddFilter(FlowLayoutPanel parent, Control label, Control editor)
        {
            FlowLayoutPanel cell = new FlowLayoutPanel();
            cell.FlowDirection = FlowDirection.TopDown;
            cell.WrapContents = false;
            cell.AutoSize = true;
            cell.Margin = new Padding(2);
            label.AutoSize = true;
            cell.Controls.Add(label);
            cell.Controls.Add(editor);
            parent.Controls.Add(cell);
        }

        private void ApplyTexts()
        {
            labelPlatform.Text = PageUi.Text(localizer, "PLATFORM", "Platform");
            labelCategory.Text = PageUi.Text(localizer, "CATEGORY", "Category");
            labelName.Text = PageUi.Text(localizer, "NAME", "Name");
            labelModType.Text = PageUi.Text(localizer, "MOD_TYPE", "Mod Type");
            labelRegion.Text = PageUi.Text(localizer, "REGION", "Region");
            labelVersion.Text = PageUi.Text(localizer, "VERSION", "Version");
            checkTotalFiles.Text = PageUi.Text(localizer, "TOTAL_FILES", "Total Files");
            labelDate.Text = PageUi.Text(localizer, "INSTALLED_ON", "Installed On");
            buttonDelete.Text = PageUi.Text(localizer, "DELETE_ITEM", "Delete Item");
            buttonUninstall.Text = PageUi.Text(localizer, "UNINSTALL_ITEM", "Uninstall Item");
            buttonDetails.Text = PageUi.Text(localizer, "VIEW_DETAILS", "View Details");
        }

        private void FillFilterLists()
        {
            suppressFilter = true;
            try
            {
                List<string> platforms = new List<string>();
                List<string> categories = new List<string>();
                foreach (InstalledRowInfo row in allRows)
                {
                    if (!string.IsNullOrEmpty(row.PlatformText) && !platforms.Contains(row.PlatformText))
                    {
                        platforms.Add(row.PlatformText);
                    }
                    if (!string.IsNullOrEmpty(row.CategoryTitle) && !categories.Contains(row.CategoryTitle))
                    {
                        categories.Add(row.CategoryTitle);
                    }
                }
                platforms.Sort(StringComparer.CurrentCultureIgnoreCase);
                categories.Sort(StringComparer.CurrentCultureIgnoreCase);
                FillCombo(comboPlatform, platforms);
                FillCombo(comboCategory, categories);
                // The old type, region and version lists were never filled (MainWindow.Designer.cs has no items); they hold (All) only.
                FillCombo(comboModType, new List<string>());
                FillCombo(comboRegion, new List<string>());
                FillCombo(comboVersion, new List<string>());
            }
            finally
            {
                suppressFilter = false;
            }
        }

        // Rebuilds the list without duplicating entries on every reload (ledger MW1-147) and keeps the user's choice.
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

        private void CheckTotalFiles_CheckedChanged(object sender, EventArgs e)
        {
            numberTotalFiles.Enabled = checkTotalFiles.Checked;
            ApplyFilters();
        }

        private void ButtonFilesMode_Click(object sender, EventArgs e)
        {
            filesMode = PageUi.NextMode(filesMode);
            buttonFilesMode.Text = PageUi.ModeText(filesMode);
            if (checkTotalFiles.Checked)
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

        // The old filter compared with == and so showed nothing until both combos were set (ledger MW1-149); an
        // unset filter now means "all". The type, region and version combos (ledger MW1-140 to MW1-142) are ported as in
        // the old app: they hold (All) only and are stored but never applied to the rows. See OPEN-QUESTIONS Q-009.
        private void ApplyFilters()
        {
            string platform = Chosen(comboPlatform);
            string category = Chosen(comboCategory);
            string name = textName.Text.Trim();

            shownRows.Clear();
            DataTable table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "PLATFORM", "Platform"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "PROFILE", "Profile"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "CATEGORY", "Category"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "NAME", "Name"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "TOTAL_FILES", "Total Files"), typeof(string));
            table.Columns.Add(PageUi.Text(localizer, "INSTALLED_ON", "Installed On"), typeof(string));

            foreach (InstalledRowInfo row in allRows)
            {
                if (platform != null && !PageUi.EqualsIgnoreCase(row.PlatformText, platform))
                {
                    continue;
                }
                if (category != null && !PageUi.EqualsIgnoreCase(row.CategoryTitle, category))
                {
                    continue;
                }
                if (!PageUi.ContainsIgnoreCase(row.Name, name))
                {
                    continue;
                }
                if (checkTotalFiles.Checked && !PageUi.Compare(row.TotalFiles, (int)numberTotalFiles.Value, filesMode))
                {
                    continue;
                }
                if (pickerDate.Checked && !PageUi.Compare(row.DateInstalled, pickerDate.Value, dateMode))
                {
                    continue;
                }
                shownRows.Add(row);
                table.Rows.Add(
                    row.Key,
                    row.PlatformText,
                    row.ProfileName,
                    row.CategoryTitle,
                    row.Name,
                    row.TotalFiles + (row.TotalFiles == 1 ? " file" : " files"),
                    PageUi.FormatDate(row.DateInstalled, useRelativeTimes));
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

        private InstalledRowInfo SelectedRow()
        {
            string key = grid.GetSelectedValue("Key");
            if (key == null)
            {
                return null;
            }
            foreach (InstalledRowInfo row in shownRows)
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
            buttonDelete.Enabled = hasRow;
            buttonDetails.Enabled = hasRow;
            buttonUninstall.Enabled = hasRow && connectedProfileId.HasValue;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        // Double-click opens details here; in the old app it did nothing on this grid (ledger MW1-152, never wired).
        private void Grid_RowActivated(object sender, EventArgs e)
        {
            RaiseDetails();
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            RaiseDetails();
        }

        private void RaiseDetails()
        {
            InstalledRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            EventHandler<ItemEventArgs<InstalledRowInfo>> handler = ViewDetailsRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<InstalledRowInfo>(row));
            }
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            InstalledRowInfo row = SelectedRow();
            if (row == null || dataSource == null)
            {
                return;
            }
            DialogResult answer = MessageBox.Show(this,
                "Remove this item from the installed list? Nothing is removed from the console." + Environment.NewLine + row.Name,
                "Delete Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            try
            {
                dataSource.ForgetInstalled(row);
            }
            catch (Exception ex)
            {
                RaiseStatus("Could not remove the record: " + ex.Message, true);
                MessageBox.Show(this, ex.Message, "Delete Item", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            EventHandler<ItemEventArgs<InstalledRowInfo>> handler = RecordForgotten;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<InstalledRowInfo>(row));
            }
            Reload();
        }

        private void ButtonUninstall_Click(object sender, EventArgs e)
        {
            InstalledRowInfo row = SelectedRow();
            if (row == null)
            {
                return;
            }
            if (!connectedProfileId.HasValue)
            {
                MessageBox.Show(this, "Connect to the console first.", "Uninstall Item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (connectedProfileId.Value != row.ProfileId)
            {
                MessageBox.Show(this,
                    "This item was installed with the profile \"" + row.ProfileName + "\". Connect to that profile to uninstall it.",
                    "Uninstall Item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            EventHandler<ItemEventArgs<InstalledRowInfo>> handler = UninstallRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<InstalledRowInfo>(row));
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
