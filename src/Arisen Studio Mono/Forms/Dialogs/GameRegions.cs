using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// Lists, adds, changes and deletes the saved PS3 game regions, and can detect regions from the console.
    /// Stock WinForms port of the old DevExpress GameRegions form. Changes are made in AppHost.Settings in memory;
    /// the host saves settings after the dialog closes.
    /// Detect Regions needs two host hooks: IsConsoleConnected and DirectoryExists. While either is null, or the probe returns false, the button is disabled.
    /// Set Categories (and Language, when localised texts are wanted) before the dialog is shown.
    /// </summary>
    internal class GameRegions : Form
    {
        private readonly TableLayoutPanel _root;
        private readonly Label _labelGameTitle;
        private readonly Label _labelGameRegion;
        private readonly ComboBox _comboGameTitle;
        private readonly ComboBox _comboGameRegion;
        private readonly Button _buttonAddGameRegion;
        private readonly Button _buttonDelete;
        private readonly Button _buttonDeleteAll;
        private readonly Button _buttonDetectRegions;
        private readonly Button _buttonSaveAll;
        private readonly ListView _listRegions;
        private readonly ColumnHeader _columnTitle;
        private readonly ColumnHeader _columnRegion;

        private bool _busy;

        public GameRegions()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Game Regions";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            int pad = Font.Height / 2;

            _labelGameTitle = new Label { AutoSize = true, UseMnemonic = false, Text = "Game Title:" };
            _comboGameTitle = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _comboGameTitle.SelectedIndexChanged += ComboGameTitle_SelectedIndexChanged;

            _labelGameRegion = new Label { AutoSize = true, UseMnemonic = false, Text = "Game Region:" };
            _comboGameRegion = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = false };

            _buttonAddGameRegion = new Button { AutoSize = true, Text = "Add Game Region", Margin = new Padding(0, 0, pad, 0) };
            _buttonAddGameRegion.Click += ButtonAddGameRegion_Click;

            FlowLayoutPanel newRegion = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            newRegion.Controls.Add(_labelGameTitle);
            newRegion.Controls.Add(_comboGameTitle);
            newRegion.Controls.Add(_labelGameRegion);
            newRegion.Controls.Add(_comboGameRegion);
            newRegion.Controls.Add(_buttonAddGameRegion);

            _columnTitle = new ColumnHeader { Text = "Game Title", Width = Font.Height * 20 };
            _columnRegion = new ColumnHeader { Text = "Game Region", Width = Font.Height * 8 };

            _listRegions = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                GridLines = false,
                Margin = Padding.Empty
            };
            _listRegions.Columns.AddRange(new[] { _columnTitle, _columnRegion });
            _listRegions.SelectedIndexChanged += ListRegions_SelectedIndexChanged;
            _listRegions.Resize += (s, e) => ResizeColumns();

            _buttonDelete = new Button { AutoSize = true, Enabled = false, Text = "Delete", Margin = new Padding(0, 0, pad, 0) };
            _buttonDelete.Click += ButtonDelete_Click;
            _buttonDeleteAll = new Button { AutoSize = true, Enabled = false, Text = "Delete All", Margin = new Padding(0, 0, pad, 0) };
            _buttonDeleteAll.Click += ButtonDeleteAll_Click;
            _buttonDetectRegions = new Button { AutoSize = true, Enabled = false, Text = "Detect Regions", Margin = new Padding(0, 0, pad, 0) };
            _buttonDetectRegions.Click += ButtonDetectRegions_Click;
            _buttonSaveAll = new Button { AutoSize = true, Text = "Save All", Margin = new Padding(0) };
            _buttonSaveAll.Click += ButtonSaveAll_Click;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            actions.Controls.Add(_buttonSaveAll);
            actions.Controls.Add(_buttonDetectRegions);
            actions.Controls.Add(_buttonDeleteAll);
            actions.Controls.Add(_buttonDelete);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.Controls.Add(newRegion, 0, 0);
            _root.Controls.Add(_listRegions, 0, 1);
            _root.Controls.Add(actions, 0, 2);

            Controls.Add(_root);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The game categories used for the title and region lists. Null (the default) leaves the lists empty and shows game ids instead of titles.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// Returns true when a PS3 console is connected. Null (the default) means no connection can be checked, so Detect Regions stays disabled.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; }

        /// <summary>
        /// Returns true when a directory exists on the console. Null (the default) means no console probe is attached, so Detect Regions stays disabled.
        /// </summary>
        public Func<string, Task<bool>> DirectoryExists { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            LoadGameTitles();
            RefreshRegionList();
            SizeToContent();
            ResizeColumns();
            base.OnLoad(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        private void ApplyLanguage()
        {
            _buttonDetectRegions.Text = "Detect Regions";
            _buttonSaveAll.Text = "Save All";
        }

        /// <summary>
        /// Sizes the form to its content, at least 30 font heights wide and 28 high, clamped to 90% of the working area of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Size preferred = _root.PreferredSize;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 30, preferred.Width), (int)(work.Width * 0.9));
            int height = Math.Min(Math.Max(preferred.Height, h * 28), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void ResizeColumns()
        {
            _columnTitle.Width = Math.Max(Font.Height * 8, _listRegions.ClientSize.Width - _columnRegion.Width - 4);
        }

        private bool IsConnected()
        {
            return DirectoryExists != null && IsConsoleConnected != null && IsConsoleConnected();
        }

        /// <summary>
        /// Enables the actions for the current state. Delete needs a selected row, Delete All needs any row, and Detect needs the console hooks.
        /// </summary>
        private void UpdateActionState()
        {
            bool hasRows = _listRegions.Items.Count > 0;
            _buttonDelete.Enabled = !_busy && _listRegions.SelectedItems.Count > 0;
            _buttonDeleteAll.Enabled = !_busy && hasRows;
            _buttonDetectRegions.Enabled = !_busy && IsConnected();
            _buttonSaveAll.Enabled = !_busy;
            _buttonAddGameRegion.Enabled = !_busy;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            UpdateActionState();
        }

        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        /// <summary>
        /// Returns the category for a game id, or null when no categories are attached or the id is unknown.
        /// </summary>
        private CategoryItem CategoryById(string gameId)
        {
            if (Categories == null)
            {
                return null;
            }

            try
            {
                return Categories.GetCategoryById(gameId);
            }
            catch (Exception)
            {
                // The linked lookup throws when nothing matches.
                return null;
            }
        }

        /// <summary>
        /// Returns the category for a game title, or null when no categories are attached or the title is unknown.
        /// </summary>
        private CategoryItem CategoryByTitle(string gameTitle)
        {
            if (Categories == null)
            {
                return null;
            }

            try
            {
                return Categories.GetCategoryByTitle(gameTitle);
            }
            catch (Exception)
            {
                // The linked lookup throws when nothing matches.
                return null;
            }
        }

        private string TitleFor(string gameId)
        {
            CategoryItem category = CategoryById(gameId);
            return category?.Title ?? gameId;
        }

        private void LoadGameTitles()
        {
            _comboGameTitle.BeginUpdate();
            _comboGameTitle.Items.Clear();
            if (Categories != null)
            {
                foreach (CategoryItem category in Categories.GetCategoriesByType(CategoryType.Game))
                {
                    _ = _comboGameTitle.Items.Add(category.Title);
                }
            }
            _comboGameTitle.EndUpdate();
        }

        /// <summary>
        /// Rebuilds the list from AppHost.Settings.GameRegionsPs3. Each item keeps its GameRegion in Tag.
        /// </summary>
        private void RefreshRegionList()
        {
            _listRegions.BeginUpdate();
            _listRegions.Items.Clear();

            List<ListViewItem> rows = new List<ListViewItem>();
            foreach (GameRegion gameRegion in AppHost.Settings.GameRegionsPs3)
            {
                rows.Add(new ListViewItem(new[] { TitleFor(gameRegion.GameId), gameRegion.Region }) { Tag = gameRegion });
            }

            _listRegions.Items.AddRange(rows.ToArray());
            _listRegions.EndUpdate();

            if (_listRegions.Items.Count > 0)
            {
                _listRegions.Items[0].Selected = true;
            }

            UpdateActionState();
        }

        private GameRegion SelectedRegion()
        {
            return _listRegions.SelectedItems.Count > 0 ? _listRegions.SelectedItems[0].Tag as GameRegion : null;
        }

        private void ListRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listRegions.SelectedItems.Count > 0)
            {
                ListViewItem item = _listRegions.SelectedItems[0];
                // Set the title first: changing it refills the region list.
                _comboGameTitle.SelectedItem = item.SubItems[0].Text;
                _comboGameRegion.SelectedItem = item.SubItems[1].Text;
            }

            UpdateActionState();
        }

        private void ComboGameTitle_SelectedIndexChanged(object sender, EventArgs e)
        {
            _comboGameRegion.BeginUpdate();
            _comboGameRegion.Items.Clear();

            if (_comboGameTitle.SelectedIndex != -1)
            {
                CategoryItem category = CategoryByTitle(_comboGameTitle.SelectedItem.ToString());
                if (category?.Regions != null)
                {
                    foreach (string region in category.Regions)
                    {
                        _ = _comboGameRegion.Items.Add(region);
                    }
                }
            }

            _comboGameRegion.EndUpdate();
            _comboGameRegion.Enabled = _comboGameTitle.SelectedIndex != -1;
        }

        private void ButtonAddGameRegion_Click(object sender, EventArgs e)
        {
            if (_comboGameTitle.SelectedIndex == -1)
            {
                _ = MessageBox.Show(this, "You must first specify a game title.", "No Game Title", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (_comboGameRegion.SelectedIndex == -1)
            {
                _ = MessageBox.Show(this, "You must specify a game region for this game title.", "No Game Region", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            string gameTitle = _comboGameTitle.SelectedItem.ToString();
            string gameRegion = _comboGameRegion.SelectedItem.ToString();

            CategoryItem category = CategoryByTitle(gameTitle);
            if (category == null)
            {
                _ = MessageBox.Show(this, "The selected game title could not be found.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppHost.Settings.UpdateGameRegion(category.Id, gameRegion);
            RefreshRegionList();
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            GameRegion selected = SelectedRegion();
            if (selected == null)
            {
                return;
            }

            if (MessageBox.Show(this, "Do you really want to delete the selected saved game region?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _ = AppHost.Settings.GameRegionsPs3.Remove(selected);
                RefreshRegionList();
                _ = MessageBox.Show(this, "Saved game region has now been deleted.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ButtonDeleteAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Do you really want to delete all of your saved game regions?", Lookup("CONFIRM", "Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                AppHost.Settings.GameRegionsPs3.Clear();
                RefreshRegionList();
                _ = MessageBox.Show(this, "All saved game regions have now been deleted.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ButtonSaveAll_Click(object sender, EventArgs e)
        {
            // Validate every row first, so a bad row changes nothing.
            List<GameRegion> regions = new List<GameRegion>();
            foreach (ListViewItem item in _listRegions.Items)
            {
                if (item.Tag is GameRegion gameRegion)
                {
                    regions.Add(gameRegion);
                }
            }

            foreach (GameRegion gameRegion in regions)
            {
                CategoryItem category = CategoryById(gameRegion.GameId);
                if (category?.Regions != null && !category.Regions.Contains(gameRegion.Region))
                {
                    _ = MessageBox.Show(this,
                        "Game Region: " + gameRegion.Region + " is not supported for Game Title: " + category.Title + "\n\nPlease change it to one that's supported for this game.",
                        "Invalid Region", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }

            foreach (GameRegion gameRegion in regions)
            {
                AppHost.Settings.UpdateGameRegion(gameRegion.GameId, gameRegion.Region);
            }

            _ = MessageBox.Show(this, "All game regions have now been saved.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private async void ButtonDetectRegions_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "All saved game regions will be cleared. Do you want to continue?", "Game Region", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (!IsConnected() || Categories == null)
            {
                _ = MessageBox.Show(this, "Detecting game regions needs a connection to the console, which is not available in this build.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            SetBusy(true);
            try
            {
                List<CategoryItem> games = Categories.Categories.Where(x => x.CategoryType == CategoryType.Game).ToList();
                foreach (CategoryItem category in games)
                {
                    if (category.Regions == null)
                    {
                        continue;
                    }

                    foreach (string region in category.Regions)
                    {
                        bool exists = await DirectoryExists("/dev_hdd0/game/" + region);
                        if (IsDisposed)
                        {
                            return;
                        }

                        if (exists)
                        {
                            AppHost.Settings.UpdateGameRegion(category.Id, region);
                        }
                    }
                }

                RefreshRegionList();
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to detect game regions.");
                _ = MessageBox.Show(this, string.Format(Lookup("ERROR_OCCURRED", "An error occurred: {0}"), ex.Message), Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                }
            }
        }
    }
}
