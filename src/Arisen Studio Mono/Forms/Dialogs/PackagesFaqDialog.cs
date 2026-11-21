using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Frequently asked questions about activating packages.
    /// Stock WinForms port of the old DevExpress PackagesFaqDialog form.
    /// </summary>
    public class PackagesFaqDialog : Form
    {
        private const string TitleFallback = "Packages";
        private const string OkFallback = "OK";
        private const string DemoQuestionFallback = "How do I activate a C00/Demo that has a free license?";
        private const string DemoAnswerFallback = "Activating a C00/Demo package is very easy and doesn't require a rap file. It can be done using ReActPSN or PSNPatch.\n\nReActPSN : Run the homebrew while pressing and holding circle on your controller.\n\nPSNPatch : Run PSNPatch and press R1. It should displays a list of the games that have been unlocked.";
        private const string RapQuestionFallback = "How do I activate a PSN package with a RAP file?";
        private const string RapAnswerFallback = "Activating a package using a rap file requires few steps. It can be done with ReActPSN or PSNPatch. However, I will only describe how to do it with ReActPSN.\n\n1. Format a USB key in FAT32.\n2. Create the folder 'exdata' at the root of the USB key.\n3. Move the corresponding RAP file for the games you want to activate in the exdata folder.\nWarning: Do not rename rap files ! You need to leave their original ugly name.\n4. Connect the USB key in the right USB port of the PS3 (the closest to the bluray drive).\n5. Delete the ReActPSN user on your PS3 if it exists.\n6. Create the account 'aa' (without quotes) and open the account.\n7. Run ReActPSN and the screen should go black, then you should hear beeps and then the PS3 should reboot.\n8. The user 'aa' should be gone now and the account 'ReActPSN' automatically created.\n9. You're done!";

        private readonly GroupBox _groupDemo;
        private readonly Label _labelDemo;
        private readonly GroupBox _groupRap;
        private readonly Label _labelRap;
        private readonly Button _buttonClose;

        public PackagesFaqDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            _labelDemo = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false,
                Padding = new Padding(pad)
            };
            _groupDemo = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(pad),
                Margin = new Padding(0, 0, 0, pad)
            };
            _groupDemo.Controls.Add(_labelDemo);

            _labelRap = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false,
                Padding = new Padding(pad)
            };
            _groupRap = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(pad),
                Margin = new Padding(0, 0, 0, pad)
            };
            _groupRap.Controls.Add(_labelRap);

            _buttonClose = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                DialogResult = DialogResult.OK
            };
            _buttonClose.Click += (s, e) => Close();

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            buttonRow.Controls.Add(_buttonClose);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_groupDemo, 0, 0);
            root.Controls.Add(_groupRap, 0, 1);
            root.Controls.Add(buttonRow, 0, 2);
            Controls.Add(root);

            ApplyLanguage();

            // Size from font metrics, then clamp to 90% of the working area of the screen this form is on.
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int width = Math.Min(h * 44, (int)(work.Width * 0.9));
            int height = Math.Min(h * 32, (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            _ = _buttonClose.Focus();
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
            Text = Lookup("LABEL_PACKAGES", TitleFallback) + " - FAQ";

            _groupDemo.Text = Lookup("HOW_TO_ACTIVATE_DEMO_QUESTION", DemoQuestionFallback);
            _labelDemo.Text = Lookup("HOW_TO_ACTIVATE_DEMO", DemoAnswerFallback);

            _groupRap.Text = Lookup("HOW_TO_ACTIVATE_RAP_QUESTION", RapQuestionFallback);
            _labelRap.Text = Lookup("HOW_TO_ACTIVATE_RAP", RapAnswerFallback);

            _buttonClose.Text = Lookup("LABEL_OK", OkFallback);
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
