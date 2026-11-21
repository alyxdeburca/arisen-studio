using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Edits the username and password of a console profile.
    /// Stock WinForms port of the old DevExpress EditLoginDialog form.
    /// Dialog result: OK = save, Abort = use the default login, Cancel = discard.
    /// </summary>
    public class EditLoginDialog : Form
    {
        private readonly Label _labelUsername;
        private readonly Label _labelPassword;
        private readonly TextBox _textBoxUsername;
        private readonly TextBox _textBoxPassword;
        private readonly Button _buttonUseDefault;
        private readonly Button _buttonCancel;
        private readonly Button _buttonSave;

        public EditLoginDialog()
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

            _labelUsername = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, pad, pad / 2)
            };
            _labelPassword = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, pad, pad / 2)
            };

            _textBoxUsername = new TextBox
            {
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _textBoxPassword = new TextBox
            {
                Dock = DockStyle.Fill,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _textBoxUsername.TextChanged += (s, e) => UpdateSaveState();
            _textBoxPassword.TextChanged += (s, e) => UpdateSaveState();

            _buttonUseDefault = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0),
                DialogResult = DialogResult.Abort,
                TabIndex = 0
            };
            _buttonCancel = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0),
                DialogResult = DialogResult.Cancel,
                TabIndex = 1
            };
            _buttonSave = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                DialogResult = DialogResult.OK,
                Enabled = false,
                TabIndex = 2
            };

            // Right-to-left flow: Save sits at the far right, Use Default at the left.
            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            buttonRow.Controls.Add(_buttonSave);
            buttonRow.Controls.Add(_buttonCancel);
            buttonRow.Controls.Add(_buttonUseDefault);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_labelUsername, 0, 0);
            root.Controls.Add(_textBoxUsername, 1, 0);
            root.Controls.Add(_labelPassword, 0, 1);
            root.Controls.Add(_textBoxPassword, 1, 1);
            root.Controls.Add(buttonRow, 0, 2);
            root.SetColumnSpan(buttonRow, 2);
            Controls.Add(root);

            _textBoxUsername.TabIndex = 0;
            _textBoxPassword.TabIndex = 1;
            buttonRow.TabIndex = 2;

            ApplyLanguage();

            ClientSize = new Size(Math.Min(h * 26, (int)(Screen.FromControl(this).WorkingArea.Width * 0.9)), h * 9);
        }

        /// <summary>
        /// Username box. Read Text after the dialog returns OK or Abort.
        /// </summary>
        public TextBox TextBoxUsername
        {
            get { return _textBoxUsername; }
        }

        /// <summary>
        /// Password box. Read Text after the dialog returns OK. It is not masked, as in the old form.
        /// </summary>
        public TextBox TextBoxPassword
        {
            get { return _textBoxPassword; }
        }

        /// <summary>
        /// Closes the dialog with DialogResult.Abort, meaning "use the default login".
        /// </summary>
        public Button ButtonUseDefault
        {
            get { return _buttonUseDefault; }
        }

        /// <summary>
        /// Closes the dialog with DialogResult.Cancel.
        /// </summary>
        public Button ButtonCancel
        {
            get { return _buttonCancel; }
        }

        /// <summary>
        /// Closes the dialog with DialogResult.OK. Enabled only when both boxes contain text.
        /// </summary>
        public Button ButtonSave
        {
            get { return _buttonSave; }
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
        }

        private void UpdateSaveState()
        {
            _buttonSave.Enabled = !string.IsNullOrWhiteSpace(_textBoxUsername.Text) &&
                                  !string.IsNullOrWhiteSpace(_textBoxPassword.Text);
        }

        private void ApplyLanguage()
        {
            Text = Lookup("LABEL_EDIT_LOGIN_CREDENTIALS", "Edit Login Credentials");

            _labelUsername.Text = Lookup("LABEL_USERNAME", "Username") + ":";
            _labelPassword.Text = Lookup("LABEL_PASSWORD", "Password") + ":";

            _buttonUseDefault.Text = Lookup("LABEL_USE_DEFAULT", "Use Default");
            _buttonSave.Text = Lookup("LABEL_SAVE", "Save");
            _buttonCancel.Text = Lookup("LABEL_CANCEL", "Cancel");
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
