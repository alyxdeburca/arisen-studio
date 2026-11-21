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
    /// Stores a region for each PS3 game title, so the installer picks the matching game folder.
    /// Stock WinForms port of the old DevExpress GameRegions form.
    /// Host hooks: Settings and Categories (data), IsConsoleConnected (enables Detect Regions),
    /// and ConsoleDirectoryExists (the FTP probe used by Detect Regions; unset by default).
    /// </summary>
    public class GameRegions : Form
    {
        private const string GameDirectoryPrefix = "/dev_hdd0/game/";

        private readonly Label _labelTitle;
        private readonly Label _labelRegion;
        private readonly ComboBox _comboTitle;
        private readonly ComboBox _comboRegion;
        private readonly Button _buttonAdd;
        private readonly ListView _listRegions;
        private readonly ColumnHeader _columnTitle;
        private readonly ColumnHeader _columnRegion;
        private readonly Button _buttonDelete;
        private readonly Button _buttonDeleteAll;
        private readonly Button _buttonDetect;
        private readonly Button _buttonSaveAll;

        // Game categories in the same order as the title combo, so the selected index maps straight to a category.
        private readonly List<CategoryItem> _gameCategories = new List<CategoryItem>();

        public GameRegions()
        {
            Font = SystemFonts.MessageBoxFont;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            _labelTitle = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, pad, pad / 2)
            };
            _labelRegion = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, pad, pad / 2)
            };

            _comboTitle = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _comboRegion = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            _buttonAdd = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 10, 0),
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            _columnTitle = new ColumnHeader { Text = "Game Title" };
            _columnRegion = new ColumnHeader { Text = "Game Region" };
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

            _buttonDelete = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 6, 0),
                Margin = new Padding(0, 0, pad, 0),
                Enabled = false
            };
            _buttonDeleteAll = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 6, 0),
                Margin = new Padding(0, 0, pad, 0),
                Enabled = false
            };
            _buttonDetect = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 6, 0),
                Margin = new Padding(0, 0, pad, 0)
            };
            _buttonSaveAll = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 6, 0),
                Margin = new Padding(0)
            };

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            buttonRow.Controls.Add(_buttonDelete);
            buttonRow.Controls.Add(_buttonDeleteAll);
            buttonRow.Controls.Add(_buttonDetect);
            buttonRow.Controls.Add(_buttonSaveAll);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_labelTitle, 0, 0);
            root.Controls.Add(_comboTitle, 1, 0);
            root.Controls.Add(_labelRegion, 0, 1);
            root.Controls.Add(_comboRegion, 1, 1);
            root.Controls.Add(_buttonAdd, 0, 2);
            root.SetColumnSpan(_buttonAdd, 2);
            root.Controls.Add(_listRegions, 0, 3);
            root.SetColumnSpan(_listRegions, 2);
            root.Controls.Add(buttonRow, 0, 4);
            root.SetColumnSpan(buttonRow, 2);
            Controls.Add(root);

            _comboTitle.TabIndex = 0;
            _comboRegion.TabIndex = 1;
            _buttonAdd.TabIndex = 2;
            _listRegions.TabIndex = 3;
            buttonRow.TabIndex = 4;

            _comboTitle.SelectedIndexChanged += ComboTitle_SelectedIndexChanged;
            _buttonAdd.Click += ButtonAdd_Click;
            _listRegions.SelectedIndexChanged += ListRegions_SelectedIndexChanged;
            _listRegions.Resize += (s, e) => ResizeColumns();
            _buttonDelete.Click += ButtonDelete_Click;
            _buttonDeleteAll.Click += ButtonDeleteAll_Click;
            _buttonDetect.Click += ButtonDetectRegions_Click;
            _buttonSaveAll.Click += ButtonSaveAll_Click;

            ApplyLanguage();

            ClientSize = new Size(Math.Min(h * 30, (int)(Screen.FromControl(this).WorkingArea.Width * 0.9)), h * 24);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// The settings whose GameRegionsPs3 list this form edits in memory. Defaults to the shared application settings.
        /// Saving to disk is the host's job.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The game categories with their supported regions. Without it the title list is empty.
        /// </summary>
        public CategoriesData Categories { get; set; }

        /// <summary>
        /// Returns true when a console is connected. Detect Regions is enabled only when this returns true.
        /// The default reports no console.
        /// </summary>
        public Func<bool> IsConsoleConnected { get; set; } = () => false;

        /// <summary>
        /// Probes a console path (for example /dev_hdd0/game/BLUS30001) and returns true when it exists.
        /// Detect Regions uses it. It is unset by default, so Detect Regions shows a "no console" message.
        /// </summary>
        public Func<string, Task<bool>> ConsoleDirectoryExists { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            _buttonDetect.Enabled = IsConsoleConnectedNow();
            LoadGameTitles();
            LoadSavedGameRegions();
            ResizeColumns();
        }

        private bool IsConsoleConnectedNow()
        {
            return IsConsoleConnected != null && IsConsoleConnected();
        }

        private void ApplyLanguage()
        {
            Text = Lookup("LABEL_GAME_REGIONS", "Game Regions");

            _labelTitle.Text = Lookup("LABEL_GAME_TITLE", "Game Title:");
            _labelRegion.Text = Lookup("LABEL_GAME_REGION", "Game Region:");

            _buttonAdd.Text = "Add Game Region";
            _buttonDelete.Text = "Delete";
            _buttonDeleteAll.Text = "Delete All";
            _buttonDetect.Text = "Detect Regions";
            _buttonSaveAll.Text = "Save All";
        }

        private void LoadGameTitles()
        {
            _gameCategories.Clear();
            _comboTitle.Items.Clear();

            if (Categories?.Categories == null)
            {
                return;
            }

            foreach (CategoryItem category in Categories.Categories)
            {
                if (category.CategoryType != CategoryType.Game)
                {
                    continue;
                }

                _gameCategories.Add(category);
                _ = _comboTitle.Items.Add(category.Title ?? category.Id ?? "");
            }
        }

        /// <summary>
        /// Rebuilds the saved-region list from Settings and selects the first row.
        /// </summary>
        public void LoadSavedGameRegions()
        {
            _listRegions.BeginUpdate();
            _listRegions.Items.Clear();

            if (Settings?.GameRegionsPs3 != null)
            {
                foreach (GameRegion gameRegion in Settings.GameRegionsPs3)
                {
                    CategoryItem category = FindCategory(gameRegion.GameId);
                    string title = category != null && category.Title != null ? category.Title : gameRegion.GameId;

                    ListViewItem item = new ListViewItem(title ?? "");
                    _ = item.SubItems.Add(gameRegion.Region ?? "");
                    item.Tag = gameRegion;
                    _ = _listRegions.Items.Add(item);
                }
            }

            _listRegions.EndUpdate();

            if (_listRegions.Items.Count > 0)
            {
                _listRegions.Items[0].Selected = true;
                _listRegions.Items[0].Focused = true;
            }

            UpdateButtons();
        }

        private void ResizeColumns()
        {
            int width = _listRegions.ClientSize.Width;
            _columnTitle.Width = (int)(width * 0.7);
            _columnRegion.Width = width - _columnTitle.Width;
        }

        private void UpdateButtons()
        {
            _buttonDelete.Enabled = _listRegions.SelectedItems.Count > 0;
            _buttonDeleteAll.Enabled = _listRegions.Items.Count > 0;
        }

        private CategoryItem FindCategory(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || Categories?.Categories == null)
            {
                return null;
            }

            return Categories.Categories.FirstOrDefault(c => string.Equals(c.Id, gameId, StringComparison.OrdinalIgnoreCase));
        }

        private void ListRegions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_listRegions.SelectedItems.Count > 0)
            {
                GameRegion gameRegion = _listRegions.SelectedItems[0].Tag as GameRegion;
                if (gameRegion != null)
                {
                    SelectGameTitleAndRegion(gameRegion);
                }
            }

            UpdateButtons();
        }

        private void SelectGameTitleAndRegion(GameRegion gameRegion)
        {
            int titleIndex = _gameCategories.FindIndex(c => string.Equals(c.Id, gameRegion.GameId, StringComparison.OrdinalIgnoreCase));
            _comboTitle.SelectedIndex = titleIndex;

            int regionIndex = -1;
            if (titleIndex != -1)
            {
                regionIndex = _comboRegion.Items.IndexOf(gameRegion.Region);
            }
            _comboRegion.SelectedIndex = regionIndex;
        }

        private void ComboTitle_SelectedIndexChanged(object sender, EventArgs e)
        {
            _comboRegion.Items.Clear();

            int index = _comboTitle.SelectedIndex;
            if (index != -1)
            {
                string[] regions = _gameCategories[index].Regions ?? new string[0];
                foreach (string region in regions)
                {
                    _ = _comboRegion.Items.Add(region);
                }
            }

            _comboRegion.Enabled = index != -1;
        }

        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            if (_comboTitle.SelectedIndex == -1)
            {
                _ = MessageBox.Show(this, "You must first specify a game title.", "No Game Title", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (_comboRegion.SelectedIndex == -1)
            {
                _ = MessageBox.Show(this, "You must specify a game region for this game title.", "No Game Region", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            CategoryItem category = _gameCategories[_comboTitle.SelectedIndex];
            Settings.UpdateGameRegion(category.Id, _comboRegion.SelectedItem.ToString());
            LoadSavedGameRegions();
        }

        private void ButtonDelete_Click(object sender, EventArgs e)
        {
            if (_listRegions.SelectedItems.Count == 0)
            {
                return;
            }

            if (MessageBox.Show(this, "Do you really want to delete the selected saved game region?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                GameRegion gameRegion = _listRegions.SelectedItems[0].Tag as GameRegion;
                if (gameRegion != null)
                {
                    _ = Settings.GameRegionsPs3.Remove(gameRegion);
                }

                LoadSavedGameRegions();
                _ = MessageBox.Show(this, "Saved game region has now been deleted.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ButtonDeleteAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Do you really want to delete all of your saved game regions?", Lookup("CONFIRM", "Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                Settings.GameRegionsPs3.Clear();
                LoadSavedGameRegions();
                _ = MessageBox.Show(this, "All saved game regions have now been deleted.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ButtonSaveAll_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in _listRegions.Items)
            {
                GameRegion gameRegion = item.Tag as GameRegion;
                if (gameRegion == null)
                {
                    continue;
                }

                // Rows whose game is missing from the category list are skipped; they are already stored.
                CategoryItem category = FindCategory(gameRegion.GameId);
                if (category == null)
                {
                    continue;
                }

                string[] regions = category.Regions ?? new string[0];
                if (!regions.Contains(gameRegion.Region))
                {
                    _ = MessageBox.Show(this,
                        "Game Region: " + gameRegion.Region + " is not supported for Game Title: " + item.Text + "\n\nPlease change it to one that's supported for this game.",
                        "Invalid Region", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                Settings.UpdateGameRegion(category.Id, gameRegion.Region);
            }

            _ = MessageBox.Show(this, "All game regions have now been saved.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private async void ButtonDetectRegions_Click(object sender, EventArgs e)
        {
            if (ConsoleDirectoryExists == null)
            {
                _ = MessageBox.Show(this, "Detecting regions needs a connection to the console, which is not available in this build.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            // The original warning says saved regions will be cleared, but it never cleared them; that is preserved here.
            if (MessageBox.Show(this, "All saved game regions will be cleared. Do you want to continue?", "Game Region", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _buttonDetect.Enabled = false;
            try
            {
                foreach (CategoryItem category in _gameCategories)
                {
                    foreach (string region in category.Regions ?? new string[0])
                    {
                        bool exists = await ConsoleDirectoryExists(GameDirectoryPrefix + region);
                        if (IsDisposed)
                        {
                            return;
                        }

                        if (exists)
                        {
                            Settings.UpdateGameRegion(category.Id, region);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _ = MessageBox.Show(this, "Detecting regions failed: " + ex.Message, Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    _buttonDetect.Enabled = IsConsoleConnectedNow();
                    LoadSavedGameRegions();
                }
            }
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
