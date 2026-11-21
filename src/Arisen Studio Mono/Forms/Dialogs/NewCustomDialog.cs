using ArisenStudio.Database;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using Humanizer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Creates or edits a user-defined mod.
    /// Stock WinForms port of the old DevExpress NewCustomDialog form (built in code, no designer file).
    /// Save copies the files and the id only, as the old form did. Dialog result: OK after Save, Cancel otherwise.
    /// </summary>
    public class NewCustomDialog : Form
    {
        private readonly Label _labelPlatform;
        private readonly Label _labelCategoryType;
        private readonly Label _labelCategory;
        private readonly Label _labelName;
        private readonly Label _labelModType;
        private readonly Label _labelVersion;
        private readonly Label _labelDescription;
        private readonly ComboBox _comboBoxPlatform;
        private readonly ComboBox _comboBoxCategoryType;
        private readonly ComboBox _comboBoxCategory;
        private readonly ComboBox _comboBoxModType;
        private readonly TextBox _textBoxName;
        private readonly TextBox _textBoxVersion;
        private readonly TextBox _textBoxDescription;
        private readonly DataGridView _gridFiles;
        private readonly GroupBox _groupInstallationFiles;
        private readonly Button _buttonChooseFile;
        private readonly Button _buttonDeleteFile;
        private readonly Button _buttonDeleteAll;
        private readonly Button _buttonHelp;
        private readonly Button _buttonSave;
        private readonly Button _buttonCancel;

        public NewCustomDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            // Labels. The old form had no colons on these captions.
            _labelPlatform = CreateLabel();
            _labelCategoryType = CreateLabel();
            _labelCategory = CreateLabel();
            _labelName = CreateLabel();
            _labelModType = CreateLabel();
            _labelVersion = CreateLabel();
            _labelDescription = CreateLabel();

            // Platform and Category Type are fixed lists. Category and Mod Type accept typed text, as before.
            _comboBoxPlatform = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, pad, pad / 2)
            };
            _comboBoxPlatform.Items.AddRange(new object[] { "PlayStation 3", "PlayStation 4", "Xbox 360" });

            _comboBoxCategoryType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, pad, pad / 2)
            };
            _comboBoxCategoryType.Items.AddRange(new object[] { "Game", "Homebrew", "Package", "Resource", "Application", "Plugin" });

            _comboBoxCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            _comboBoxModType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, pad, pad / 2)
            };
            _comboBoxModType.Items.AddRange(new object[] { "EBOOT", "SPRX", "CFG", "OTHER" });

            _textBoxName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, pad, pad / 2) };
            _textBoxVersion = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };

            _textBoxDescription = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            TableLayoutPanel fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 6,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            for (int row = 0; row < 5; row++)
            {
                fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            fields.Controls.Add(_labelPlatform, 0, 0);
            fields.Controls.Add(_labelCategoryType, 1, 0);
            fields.Controls.Add(_labelCategory, 2, 0);
            fields.SetColumnSpan(_labelCategory, 2);

            fields.Controls.Add(_comboBoxPlatform, 0, 1);
            fields.Controls.Add(_comboBoxCategoryType, 1, 1);
            fields.Controls.Add(_comboBoxCategory, 2, 1);
            fields.SetColumnSpan(_comboBoxCategory, 2);

            fields.Controls.Add(_labelName, 0, 2);
            fields.SetColumnSpan(_labelName, 2);
            fields.Controls.Add(_labelModType, 2, 2);
            fields.Controls.Add(_labelVersion, 3, 2);

            fields.Controls.Add(_textBoxName, 0, 3);
            fields.SetColumnSpan(_textBoxName, 2);
            fields.Controls.Add(_comboBoxModType, 2, 3);
            fields.Controls.Add(_textBoxVersion, 3, 3);

            fields.Controls.Add(_labelDescription, 0, 4);
            fields.SetColumnSpan(_labelDescription, 4);
            fields.Controls.Add(_textBoxDescription, 0, 5);
            fields.SetColumnSpan(_textBoxDescription, 4);

            // File table: two unbound text columns. Rows can be added and deleted by the user, as in the old grid.
            _gridFiles = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Margin = new Padding(0)
            };
            _gridFiles.Columns.Add(new DataGridViewTextBoxColumn { Name = "LocalFile" });
            _gridFiles.Columns.Add(new DataGridViewTextBoxColumn { Name = "InstallPath" });

            // The Choose, Delete and Delete All buttons are disabled with no handlers in the old form. Help is disabled but wired.
            _buttonChooseFile = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0, 0, pad / 2, 0) };
            _buttonDeleteFile = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0, 0, pad / 2, 0) };
            _buttonDeleteAll = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0, 0, pad / 2, 0) };
            _buttonHelp = new Button { AutoSize = true, Enabled = false, Margin = new Padding(0) };
            _buttonHelp.Click += ButtonHelp_Click;

            FlowLayoutPanel fileButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            fileButtons.Controls.Add(_buttonChooseFile);
            fileButtons.Controls.Add(_buttonDeleteFile);
            fileButtons.Controls.Add(_buttonDeleteAll);
            fileButtons.Controls.Add(_buttonHelp);

            TableLayoutPanel groupTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            groupTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            groupTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            groupTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            groupTable.Controls.Add(_gridFiles, 0, 0);
            groupTable.Controls.Add(fileButtons, 0, 1);

            _groupInstallationFiles = new GroupBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            _groupInstallationFiles.Controls.Add(groupTable);

            // Save sits to the left of Cancel. Save has no DialogResult: its click handler sets OK. Cancel closes with Cancel.
            _buttonCancel = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                DialogResult = DialogResult.Cancel
            };
            _buttonSave = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0)
            };
            _buttonSave.Click += ButtonSave_Click;

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            buttonRow.Controls.Add(_buttonCancel);
            buttonRow.Controls.Add(_buttonSave);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(fields, 0, 0);
            root.Controls.Add(_groupInstallationFiles, 0, 1);
            root.Controls.Add(buttonRow, 0, 2);
            Controls.Add(root);

            // Tab order: platform, category type, category, name, version, mod type, description, grid, file buttons, Save, Cancel.
            _comboBoxPlatform.TabIndex = 0;
            _comboBoxCategoryType.TabIndex = 1;
            _comboBoxCategory.TabIndex = 2;
            _textBoxName.TabIndex = 3;
            _textBoxVersion.TabIndex = 4;
            _comboBoxModType.TabIndex = 5;
            _textBoxDescription.TabIndex = 6;
            _gridFiles.TabIndex = 7;
            _buttonChooseFile.TabIndex = 8;
            _buttonDeleteFile.TabIndex = 9;
            _buttonDeleteAll.TabIndex = 10;
            _buttonHelp.TabIndex = 11;
            _buttonSave.TabIndex = 12;
            _buttonCancel.TabIndex = 13;

            // Initial selections are set before the handler is attached, so the constructor does not fire it.
            _comboBoxPlatform.SelectedIndex = 0;
            _comboBoxCategoryType.SelectedIndex = 0;
            _comboBoxCategoryType.SelectedIndexChanged += ComboBoxCategoryType_SelectedIndexChanged;

            // Fixed size from the font height, as in the other ported dialogs. Layout is unverified on Mono.
            ClientSize = new Size(h * 32, h * 34);
        }

        /// <summary>
        /// The mod being created or edited. Read it after the dialog returns OK.
        /// </summary>
        public CustomItemData CustomMod { get; set; } = new CustomItemData();

        /// <summary>
        /// True when editing an existing mod. Controls whether Save makes a new id.
        /// </summary>
        public bool IsEditing { get; set; } = false;

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Database used to fill the category and mod type lists. Null (the default) leaves those lists empty.
        /// </summary>
        public DatabaseClient Database { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The old form crashed on a null model. An empty model keeps the dialog usable.
            if (CustomMod == null)
            {
                CustomMod = new CustomItemData();
            }

            ApplyLanguage();

            try
            {
                _comboBoxPlatform.SelectedIndex = _comboBoxPlatform.Items.IndexOf(CustomMod.Platform.Humanize());
                _comboBoxCategoryType.SelectedIndex = _comboBoxCategoryType.Items.IndexOf(CustomMod.CategoryType.Humanize());
                _comboBoxCategory.SelectedIndex = _comboBoxCategory.Items.IndexOf(CustomMod.Category ?? string.Empty);
                _textBoxName.Text = CustomMod.Name;
                _comboBoxModType.SelectedIndex = _comboBoxModType.Items.IndexOf(CustomMod.ModType ?? string.Empty);
                _textBoxVersion.Text = CustomMod.Version;
            }
            catch (Exception)
            {
                // The old form ignored every error in this block. Kept, so one bad value does not stop the dialog opening.
            }

            _textBoxName.SelectionStart = _textBoxName.Text.Length;

            _comboBoxCategory.BeginUpdate();
            if (Database?.CategoriesData?.Categories != null)
            {
                foreach (var category in Database.CategoriesData.Categories.Distinct())
                {
                    _comboBoxCategory.Items.Add(category.Title);
                }
            }
            _comboBoxCategory.EndUpdate();

            _comboBoxModType.BeginUpdate();
            if (Database?.GameModsPS3?.Library != null)
            {
                // Kept from the old form: duplicates are added when several mods share a mod type.
                foreach (var modTypes in Database.GameModsPS3.Library.Select(x => x.ModTypes.Distinct()).Distinct())
                {
                    foreach (var modType in modTypes.Distinct().ToList())
                    {
                        _comboBoxModType.Items.Add(modType);
                    }
                }
            }
            _ = _comboBoxModType.Items.Add("XEX");
            _comboBoxModType.EndUpdate();

            LoadFiles();
        }

        private static Label CreateLabel()
        {
            return new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 3, 6, 3)
            };
        }

        private void ApplyLanguage()
        {
            Text = Lookup("LABEL_MOD_DETAILS", "Mod Details");

            _labelPlatform.Text = Lookup("LABEL_PLATFORM", "Platform");
            _labelName.Text = Lookup("LABEL_NAME", "Name");
            _labelCategoryType.Text = Lookup("LABEL_CATEGORY_TYPE", "Category Type");
            _labelCategory.Text = Lookup("LABEL_CATEGORY", "Category");
            _labelModType.Text = Lookup("LABEL_MOD_TYPE", "Mod Type");
            _labelVersion.Text = Lookup("LABEL_VERSION", "Version");

            // The old form set the description label from LABEL_VERSION. Kept for parity.
            _labelDescription.Text = Lookup("LABEL_VERSION", "Version");

            _groupInstallationFiles.Text = Lookup("LABEL_INSTALLATION_FILES", "Installation Files");
            _buttonChooseFile.Text = Lookup("LABEL_CHOOSE_FILE", "Choose File");
            _buttonDeleteFile.Text = Lookup("LABEL_DELETE", "Delete");
            _buttonDeleteAll.Text = Lookup("LABEL_DELETE_ALL", "Delete All");
            _buttonHelp.Text = Lookup("HELP", "Help");

            _buttonSave.Text = Lookup("LABEL_SAVE", "Save");
            _buttonCancel.Text = Lookup("LABEL_CANCEL", "Cancel");

            _gridFiles.Columns["LocalFile"].HeaderText = Lookup("LABEL_LOCAL_FILE", "Local File");
            _gridFiles.Columns["InstallPath"].HeaderText = Lookup("LABEL_INSTALL_PATH", "Install Path");
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private void LoadFiles()
        {
            _gridFiles.Rows.Clear();

            foreach (ListItem file in CustomMod.Files)
            {
                _ = _gridFiles.Rows.Add(file.Name, file.Value);
            }
        }

        /// <summary>
        /// Maps the Category Type list position to its type. The list has six entries; Application and Plugin have no type here.
        /// </summary>
        private static CategoryType? GetCategoryTypeForIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return CategoryType.Game;
                case 1:
                    return CategoryType.Homebrew;
                case 2:
                    return CategoryType.Package;
                case 3:
                    return CategoryType.Resource;
                default:
                    return null;
            }
        }

        private void ComboBoxCategoryType_SelectedIndexChanged(object sender, EventArgs e)
        {
            _comboBoxCategory.Items.Clear();

            CategoryType? selectedType = GetCategoryTypeForIndex(_comboBoxCategoryType.SelectedIndex);
            if (selectedType == null)
            {
                return;
            }

            CustomMod.CategoryType = selectedType.Value;

            if (Database?.CategoriesData == null)
            {
                return;
            }

            foreach (var category in Database.CategoriesData.GetCategoriesByType(selectedType.Value))
            {
                _ = _comboBoxCategory.Items.Add(category.Title);
            }
        }

        private void ButtonHelp_Click(object sender, EventArgs e)
        {
            _ = MessageBox.Show(
                this,
                "Each row contains the files that are installed on your console for the mods to work. First, browse or enter the path to the file on your computer in the first column. Next, enter the path of where you want the file to be on your console, including the file name.",
                Lookup("INSTALLATION_FILES", "Installation Files"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Question);
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            if (!IsEditing)
            {
                List<int> modIds = new List<int>();

                if (Database != null)
                {
                    if (Database.GameModsPS3?.Library != null)
                    {
                        modIds.AddRange(Database.GameModsPS3.Library.Select(x => x.Id));
                    }

                    if (Database.HomebrewPS3?.Library != null)
                    {
                        modIds.AddRange(Database.HomebrewPS3.Library.Select(x => x.Id));
                    }

                    if (Database.ResourcesPS3?.Library != null)
                    {
                        modIds.AddRange(Database.ResourcesPS3.Library.Select(x => x.Id));
                    }

                    if (Database.HomebrewX360?.Library != null)
                    {
                        modIds.AddRange(Database.HomebrewX360.Library.Select(x => x.Id));
                    }

                    if (Database.HomebrewPS4?.Library != null)
                    {
                        modIds.AddRange(Database.HomebrewPS4.Library.Select(x => x.Id));
                    }
                }

                CustomMod.Id = StringExtensions.GetRandomExcept(1, 10000000, modIds);
            }

            // The old form showed these messages and carried on. Kept: the save still completes.
            if (_comboBoxCategory.Text.Length == 0)
            {
                _ = MessageBox.Show(this, "You must choose a category/game.", Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            if (_textBoxName.Text.Length == 0)
            {
                _ = MessageBox.Show(this, "You must enter a name.", Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            if (_comboBoxModType.Text.Length == 0)
            {
                _ = MessageBox.Show(this, "You must choose a mod type.", Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            if (_textBoxVersion.Text.Length == 0)
            {
                _ = MessageBox.Show(this, "You must enter a version. Enter '1.0' if it doesn't have one.", Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            if (_textBoxDescription.Text.Length == 0)
            {
                CustomMod.Description = Lookup("NO_INPUT", "No Input");
            }

            int fileRows = 0;
            foreach (DataGridViewRow row in _gridFiles.Rows)
            {
                if (!row.IsNewRow)
                {
                    fileRows++;
                }
            }

            if (fileRows == 0)
            {
                _ = MessageBox.Show(this, "You must have at least one file.", "No Files", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            // Kept from the old form: Files is cleared on every row, so only the last row is kept.
            foreach (DataGridViewRow row in _gridFiles.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string localFilePath = row.Cells[0].Value as string;
                string installFilePath = row.Cells[1].Value as string;

                CustomMod.Files.Clear();

                CustomMod.Files.Add(new ListItem
                {
                    Name = localFilePath,
                    Value = installFilePath
                });
            }

            DialogResult = DialogResult.OK;
        }
    }
}
