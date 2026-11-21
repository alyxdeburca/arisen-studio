using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Changes the console name and icon color that Xbox Neighborhood shows.
    /// Stock WinForms port of the old DevExpress NeighborhoodEditor form. The console calls are not ported;
    /// they sit behind IXboxNeighborhood.
    /// Host hook, set before the form is shown:
    ///  - Neighborhood: the console boundary (default: a stub that fails with a message).
    /// </summary>
    public class NeighborhoodEditor : Form
    {
        private readonly Label _labelName;
        private readonly Label _labelIcon;
        private readonly TextBox _textBoxName;
        private readonly Button _buttonSetName;
        private readonly ComboBox _comboIcon;

        public NeighborhoodEditor()
        {
            ToolFormSupport.ConfigureForm(this, false);

            int h = Font.Height;
            int pad = h / 2;

            _labelName = ToolFormSupport.NewLabel(string.Empty);
            _labelIcon = ToolFormSupport.NewLabel(string.Empty);
            _textBoxName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, pad, pad / 2) };
            _buttonSetName = ToolFormSupport.NewButton(this, 6);
            _buttonSetName.Margin = new Padding(0, 0, 0, pad / 2);
            _comboIcon = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, pad / 2) };
            _comboIcon.Items.AddRange(new object[] { "Black", "Blue", "BlueGray", "White" });

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            int row = ToolFormSupport.AddRow(root, _labelName, _textBoxName);
            root.Controls.Add(_buttonSetName, 2, row);
            ToolFormSupport.AddRow(root, _labelIcon, _comboIcon);
            Controls.Add(root);

            _buttonSetName.Click += ButtonSetName_Click;
            _comboIcon.SelectionChangeCommitted += ComboIcon_SelectionChangeCommitted;

            ApplyLanguage();

            ClientSize = new Size(h * 32, h * 8);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Console boundary. The default is a stub that fails with a message; see IXboxNeighborhood.
        /// </summary>
        public IXboxNeighborhood Neighborhood { get; set; } = UnavailableXboxConsoleAccess.Instance;

        /// <summary>
        /// Name to show in the box when the form opens (for example the current console name). Optional.
        /// </summary>
        public string InitialName { get; set; }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("NEIGHBORHOOD_EDITOR", "Neighborhood Editor");
            _labelName.Text = "Console Name";
            _labelIcon.Text = "Icon Color";
            _buttonSetName.Text = Text_("LABEL_SET", "Set");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();

            if (!string.IsNullOrEmpty(InitialName))
            {
                _textBoxName.Text = InitialName;
            }
        }

        private void ButtonSetName_Click(object sender, EventArgs e)
        {
            try
            {
                Neighborhood.ChangeName(_textBoxName.Text);
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Changed Xbox Name. Refresh Neighborhood to see the new name.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to change the console name.", ex);
            }
        }

        private void ComboIcon_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (_comboIcon.SelectedIndex < 0)
            {
                return;
            }

            try
            {
                Neighborhood.ChangeIcon((XboxIconColor)_comboIcon.SelectedIndex);
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Changed Xbox Icon. Refresh Neighborhood to see the new icon.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to change the console icon.", ex);
            }
        }

        private void ReportFailure(string message, Exception ex)
        {
            ToolFormSupport.LogError(ex, message);
            ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), message + "\n\nError Message: " + ex.Message);
        }
    }
}
