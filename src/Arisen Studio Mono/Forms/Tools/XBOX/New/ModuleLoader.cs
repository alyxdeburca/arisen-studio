using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Lists the modules loaded on the Xbox 360, loads a plugin module from a drive path, and unloads a module.
    /// Stock WinForms port of the old DevExpress ModuleLoader form (the old grid is a ListView here).
    /// The console calls (XDevkit debug target, JRPC calls) are not ported; they sit behind IXboxModuleControl,
    /// whose notes list the kernel ordinals the old form used.
    /// Host hooks, set before the form is shown:
    ///  - Modules: the console boundary (default: a stub that fails with a message, which closes the form).
    ///  - Settings: UseFormattedFileSizes sets the size column text.
    /// </summary>
    public class ModuleLoader : Form
    {
        private readonly ComboBox _comboDrives;
        private readonly TextBox _textBoxModuleName;
        private readonly Button _buttonInject;
        private readonly ListView _listModules;
        private readonly Button _buttonRefresh;
        private readonly Button _buttonUnload;
        private readonly ToolStripStatusLabel _status;

        public ModuleLoader()
        {
            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _comboDrives = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, pad, 0), Width = h * 6 };
            _comboDrives.Items.AddRange(new object[] { "HDD:\\", "USB0:\\", "USB1:\\", "USB2:\\", "USB3:\\", "USB4:\\" });
            _comboDrives.SelectedIndex = 0;
            _textBoxModuleName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, pad, 0) };
            _buttonInject = ToolFormSupport.NewButton(this, 8);
            _buttonInject.Margin = Padding.Empty;

            TableLayoutPanel top = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
            top.Margin = new Padding(0, 0, 0, pad);
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.RowCount = 1;
            top.Controls.Add(_comboDrives, 0, 0);
            top.Controls.Add(_textBoxModuleName, 1, 0);
            top.Controls.Add(_buttonInject, 2, 0);

            _listModules = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Margin = new Padding(0, 0, 0, pad)
            };
            _listModules.Columns.Add("Name", h * 24);
            _listModules.Columns.Add("Size", h * 10);

            _buttonRefresh = ToolFormSupport.NewButton(this, 9);
            _buttonUnload = ToolFormSupport.NewButton(this, 9);
            _buttonUnload.Margin = Padding.Empty;
            _buttonUnload.Enabled = false;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddFullRow(root, top, false);
            ToolFormSupport.AddFullRow(root, _listModules, true);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonRefresh, _buttonUnload), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _buttonInject.Click += ButtonInject_Click;
            _buttonRefresh.Click += ButtonRefresh_Click;
            _buttonUnload.Click += ButtonUnload_Click;
            _listModules.SelectedIndexChanged += ListModules_SelectedIndexChanged;

            ApplyLanguage();

            MinimumSize = new Size(h * 34, h * 22);
            ClientSize = new Size(h * 44, h * 28);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (UseFormattedFileSizes). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// Console boundary used to list, load and unload modules.
        /// </summary>
        public IXboxModuleControl Modules { get; set; } = UnavailableXboxConsoleAccess.Instance;

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("MODULES_LOADER", "Modules Loader");
            _listModules.Columns[0].Text = Text_("LABEL_NAME", "Name");
            _listModules.Columns[1].Text = Text_("LABEL_SIZE", "Size");
            _buttonInject.Text = Text_("LABEL_INJECT_PLUGIN", "Inject Plugin");
            _buttonRefresh.Text = Text_("LABEL_REFRESH_MODULES", "Refresh Modules");
            _buttonUnload.Text = Text_("LABEL_UNLOAD_MODULE", "Unload Module");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                SetStatus("Connecting to the console...");
                await Task.Run(() => Modules.ConnectAsDebugger());
                await LoadModulesAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to connect to the console debugger.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to load modules.\n\nError: " + ex.Message);
                Close();
            }
        }

        private async Task LoadModulesAsync()
        {
            try
            {
                IList<XboxModuleEntry> modules = await Task.Run(() => Modules.GetModules());

                _listModules.BeginUpdate();
                _listModules.Items.Clear();

                foreach (XboxModuleEntry module in modules)
                {
                    ListViewItem item = new ListViewItem(module.Name);
                    item.SubItems.Add(ToolFormSupport.FormatBytes(module.Size, Settings.UseFormattedFileSizes, Text_("LABEL_BYTES", "bytes")));
                    item.Tag = module;
                    _listModules.Items.Add(item);
                }

                _listModules.EndUpdate();
                _buttonUnload.Enabled = _listModules.SelectedItems.Count > 0;

                SetStatus("Loaded " + modules.Count + " modules.");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to load modules. Error: " + ex.Message);
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to load modules.\n\nError: " + ex.Message);
                Close();
            }
        }

        private XboxModuleEntry Selected()
        {
            if (_listModules.SelectedItems.Count == 0)
            {
                return null;
            }

            return _listModules.SelectedItems[0].Tag as XboxModuleEntry;
        }

        private void ListModules_SelectedIndexChanged(object sender, EventArgs e)
        {
            _buttonUnload.Enabled = _listModules.SelectedItems.Count > 0;
        }

        private async void ButtonRefresh_Click(object sender, EventArgs e)
        {
            await LoadModulesAsync();
        }

        private async void ButtonInject_Click(object sender, EventArgs e)
        {
            string name = _textBoxModuleName.Text.Trim();

            if (name.Length == 0)
            {
                return;
            }

            string path = _comboDrives.Text + name;

            try
            {
                await Task.Run(() => Modules.LoadModule(path));

                SetStatus("Module " + path + " has been loaded.");
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("MODULE_LOAD_SUCCESS", "The module was loaded."));
                await LoadModulesAsync();
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to load module " + path);
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Format(Text_("MODULE_LOAD_ERROR", "Unable to load the module. Error: {0}"), ex.Message));
            }
        }

        private async void ButtonUnload_Click(object sender, EventArgs e)
        {
            XboxModuleEntry selected = Selected();

            if (selected == null)
            {
                return;
            }

            try
            {
                string name = selected.Name;
                bool unloaded = await Task.Run(() => Modules.UnloadModule(name, true));

                if (unloaded)
                {
                    SetStatus("Module has been unloaded.");
                    ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("MODULE_UNLOAD_SUCCESS", "The module was unloaded."));
                    await LoadModulesAsync();
                }
                else
                {
                    SetStatus("Unable to unload module.");
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Format(Text_("MODULE_UNLOAD_ERROR", "Unable to unload the module. Error: {0}"), "the module is not loaded"));
                }
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to unload module.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Format(Text_("MODULE_UNLOAD_ERROR", "Unable to unload the module. Error: {0}"), ex.Message));
            }
        }

        private static string Format(string format, string argument)
        {
            try
            {
                return string.Format(format, argument);
            }
            catch (FormatException)
            {
                return format + " " + argument;
            }
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
