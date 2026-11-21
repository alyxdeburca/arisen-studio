using ArisenStudio.Constants;
using ArisenStudio.Extensions;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using Humanizer;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Creates or edits one console profile.
    /// Stock WinForms port of the old DevExpress NewProfileDialog form (built in code, no designer file).
    /// Dialog result: OK = profile accepted (Name, Address and Id are written to <see cref="ConsoleProfile" />),
    /// Cancel = discard the dialog. Profile changes made in the Advanced panel take effect immediately, as before.
    /// </summary>
    public class NewProfileDialog : Form
    {
        private readonly GroupBox _groupProfileInfo;
        private readonly GroupBox _groupAdvancedSettings;
        private readonly Label _labelName;
        private readonly Label _labelPlatformType;
        private readonly Label _labelAddress;
        private readonly Label _labelLoginDetails;
        private readonly Label _labelOptions;
        private readonly Label _labelInfoDefaultCredentials;
        private readonly TextBox _textBoxProfileName;
        private readonly ComboBox _comboBoxPlatform;
        private readonly TextBox _textBoxAddress;
        private readonly CheckBox _checkBoxDefaultLogin;
        private readonly CheckBox _checkBoxDefaultProfile;
        private readonly CheckBox _checkBoxGoldHEN;
        private readonly Button _buttonEditLoginDetails;
        private readonly Button _buttonSave;
        private readonly Button _buttonCancel;
        private readonly ToolTip _toolTip;

        // True while a checkbox is being set back after a refused change, so its handler does nothing.
        private bool _revertingCheck;

        public NewProfileDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            HelpButton = true;

            int h = Font.Height;
            int pad = h / 2;

            _toolTip = new ToolTip { InitialDelay = 300 };

            // Profile information controls.
            _labelName = CreateLabel();
            _labelPlatformType = CreateLabel();
            _labelAddress = CreateLabel();

            _textBoxProfileName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };
            _textBoxAddress = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };

            _comboBoxPlatform = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _comboBoxPlatform.Items.AddRange(new object[]
            {
                "PlayStation 3 (Fat)",
                "PlayStation 3 (Slim)",
                "PlayStation 3 (Super Slim)",
                "PlayStation 4 (All Models)",
                "Xbox 360 (Fat/White)",
                "Xbox 360 Elite (Fat/Black)",
                "Xbox 360 S (Slim)",
                "Xbox 360 E (Slim E)"
            });
            _comboBoxPlatform.SelectedIndex = 0;

            TableLayoutPanel tableProfile = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            tableProfile.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableProfile.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableProfile.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableProfile.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableProfile.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableProfile.Controls.Add(_labelName, 0, 0);
            tableProfile.Controls.Add(_textBoxProfileName, 1, 0);
            tableProfile.Controls.Add(_labelPlatformType, 0, 1);
            tableProfile.Controls.Add(_comboBoxPlatform, 1, 1);
            tableProfile.Controls.Add(_labelAddress, 0, 2);
            tableProfile.Controls.Add(_textBoxAddress, 1, 2);

            // Advanced settings controls.
            _labelLoginDetails = CreateLabel();
            _labelOptions = CreateLabel();

            _checkBoxDefaultLogin = new CheckBox { Text = "Default Credentials", AutoSize = true, Margin = new Padding(0) };
            _labelInfoDefaultCredentials = new Label
            {
                Text = "(i)",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(pad, 0, 0, 0)
            };
            _buttonEditLoginDetails = new Button
            {
                Text = "Edit",
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(pad, 0, 0, 0)
            };
            _checkBoxDefaultProfile = new CheckBox { Text = "Default Profile", AutoSize = true, Margin = new Padding(0) };
            _checkBoxGoldHEN = new CheckBox { Text = "GoldHEN", AutoSize = true, Enabled = false, Margin = new Padding(0) };

            FlowLayoutPanel rowDefaultLogin = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            rowDefaultLogin.Controls.Add(_checkBoxDefaultLogin);
            rowDefaultLogin.Controls.Add(_labelInfoDefaultCredentials);

            TableLayoutPanel tableAdvanced = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableAdvanced.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableAdvanced.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableAdvanced.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableAdvanced.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableAdvanced.Controls.Add(_labelLoginDetails, 0, 0);
            tableAdvanced.Controls.Add(rowDefaultLogin, 1, 0);
            tableAdvanced.Controls.Add(_buttonEditLoginDetails, 2, 0);
            tableAdvanced.Controls.Add(_labelOptions, 0, 1);
            tableAdvanced.Controls.Add(_checkBoxDefaultProfile, 1, 1);
            tableAdvanced.Controls.Add(_checkBoxGoldHEN, 1, 2);

            // Group boxes.
            _groupProfileInfo = new GroupBox { Dock = DockStyle.Fill, Text = "Profile Information", Margin = new Padding(0, 0, 0, pad) };
            _groupProfileInfo.Controls.Add(tableProfile);

            _groupAdvancedSettings = new GroupBox { Dock = DockStyle.Fill, Text = "Advanced Settings", Margin = new Padding(0) };
            _groupAdvancedSettings.Controls.Add(tableAdvanced);

            // Save and Cancel. Save sits at the far right; the form has no CancelButton, as before.
            _buttonSave = new Button
            {
                Text = "Save",
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                Enabled = true
            };
            _buttonCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0, 0, pad, 0),
                DialogResult = DialogResult.Cancel
            };

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0)
            };
            buttonRow.Controls.Add(_buttonSave);
            buttonRow.Controls.Add(_buttonCancel);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(_groupProfileInfo, 0, 0);
            root.Controls.Add(_groupAdvancedSettings, 0, 1);
            root.Controls.Add(buttonRow, 0, 2);
            Controls.Add(root);

            // Tab order: name, platform, address, default credentials, edit, default profile, GoldHEN, cancel, save.
            _textBoxProfileName.TabIndex = 0;
            _comboBoxPlatform.TabIndex = 1;
            _textBoxAddress.TabIndex = 2;
            _checkBoxDefaultLogin.TabIndex = 3;
            _buttonEditLoginDetails.TabIndex = 4;
            _checkBoxDefaultProfile.TabIndex = 5;
            _checkBoxGoldHEN.TabIndex = 6;
            _buttonCancel.TabIndex = 7;
            _buttonSave.TabIndex = 8;

            _toolTip.SetToolTip(_textBoxProfileName, "Enter a unique name for your profile.");
            _toolTip.SetToolTip(_comboBoxPlatform, "Choose the type of Platform that you have.");
            _toolTip.SetToolTip(_textBoxAddress, "Enter IP Address in format: xxx.xxx.xxx.xxx");
            _toolTip.SetToolTip(_labelInfoDefaultCredentials, "PS3: Uses the default login and password.\nXbox 360: It will use the default console from Neighborhood.");

            // Handlers are attached after the initial states are set, so the constructor does not fire them.
            _buttonSave.Click += ButtonSave_Click;
            AcceptButton = _buttonSave;
            _buttonEditLoginDetails.Click += ButtonEditCredentials_Click;
            _comboBoxPlatform.SelectedIndexChanged += ComboBoxPlatformType_SelectedIndexChanged;
            _checkBoxDefaultLogin.CheckedChanged += CheckBoxDefaultLogin_CheckedChanged;
            _checkBoxDefaultProfile.CheckedChanged += CheckBoxDefaultProfile_CheckedChanged;
            _checkBoxGoldHEN.CheckedChanged += CheckBoxGoldHEN_CheckedChanged;
            HelpButtonClicked += NewProfileDialog_HelpButtonClicked;

            // Fixed size from the font height, as in EditLoginDialog. Layout is unverified on Mono.
            ClientSize = new Size(h * 26, h * 24);
        }

        /// <summary>
        /// The profile being created or edited. Read it after the dialog returns OK.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; } = new ConsoleProfile();

        /// <summary>
        /// True when editing an existing profile; false when creating a new one.
        /// </summary>
        public bool IsEditingProfile { get; set; } = false;

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            ApplyLanguage();

            _textBoxProfileName.Text = ConsoleProfile.Name;
            _comboBoxPlatform.SelectedIndex = _comboBoxPlatform.Items.IndexOf(ConsoleProfile.PlatformType.Humanize());
            _textBoxAddress.Text = ConsoleProfile.Address;

            _buttonEditLoginDetails.Enabled = ConsoleProfile.Platform == Platform.PS3 || ConsoleProfile.Platform == Platform.PS4;

            if (IsEditingProfile)
            {
                _checkBoxDefaultLogin.Checked = false;
            }
            else
            {
                _checkBoxDefaultLogin.Checked = ConsoleProfile.UseDefaultLogin;
            }

            _checkBoxGoldHEN.Enabled = ConsoleProfile.Platform == Platform.PS4;
            _checkBoxGoldHEN.Checked = ConsoleProfile.GoldHEN;

            _checkBoxDefaultProfile.Checked = CountOtherDefaults() == 0;

            _textBoxProfileName.SelectionStart = _textBoxProfileName.Text.Length;
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
            Text = IsEditingProfile
                ? Lookup("LABEL_EDIT_PROFILE_DETAILS", "Edit Profile Details")
                : Lookup("LABEL_NEW_PROFILE_DETAILS", "New Profile Details");

            // The old form assigned PROFILE_INFO and then ADVANCED_SETTINGS to the same group box,
            // so the first box shows "Advanced Settings". Kept as-is for parity.
            _groupProfileInfo.Text = Lookup("PROFILE_INFO", "Profile Information");
            _labelName.Text = Lookup("LABEL_PROFILE_NAME", "Profile Name") + ":";
            _labelPlatformType.Text = Lookup("LABEL_PLATFORM_TYPE", "Platform Type") + ":";
            _labelAddress.Text = Lookup("LABEL_IP_ADDRESS", "IP Address") + ":";
            _labelLoginDetails.Text = Lookup("LABEL_LOGIN_DETAILS", "Login Details") + ":";
            _labelOptions.Text = Lookup("OPTIONS", "Options") + ":";

            _groupProfileInfo.Text = Lookup("ADVANCED_SETTINGS", "Advanced Settings");
            _checkBoxDefaultLogin.Text = Lookup("LABEL_DEFAULT_CREDENTIALS", "Default Credentials");
            _buttonEditLoginDetails.Text = Lookup("LABEL_EDIT", "Edit");
            _checkBoxDefaultProfile.Text = Lookup("LABEL_DEFAULT_PROFILE", "Default Profile");
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

        /// <summary>
        /// Counts the other profiles (not the one with the typed name) that are marked as default.
        /// </summary>
        private int CountOtherDefaults()
        {
            int defaults = 0;
            string typedName = _textBoxProfileName.Text.Trim();

            foreach (ConsoleProfile console in AppHost.Settings.ConsoleProfiles)
            {
                if (!console.Name.EqualsIgnoreCase(typedName) && console.IsDefault)
                {
                    defaults++;
                }
            }

            return defaults;
        }

        /// <summary>
        /// Sets a checkbox back to its previous value after a refused change, without running its handler again.
        /// </summary>
        private void RevertCheckBox(CheckBox checkBox)
        {
            _revertingCheck = true;
            try
            {
                checkBox.Checked = !checkBox.Checked;
            }
            finally
            {
                _revertingCheck = false;
            }
        }

        private void NewProfileDialog_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            _ = Process.Start(new ProcessStartInfo(Urls.ProjectWebsite + "help") { UseShellExecute = true });
        }

        private void ComboBoxPlatformType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (_comboBoxPlatform.SelectedIndex)
            {
                case 0:
                default:
                    ConsoleProfile.PlatformType = PlatformType.PlayStation3Fat;
                    ConsoleProfile.Platform = Platform.PS3;
                    break;

                case 1:
                    ConsoleProfile.PlatformType = PlatformType.PlayStation3Slim;
                    ConsoleProfile.Platform = Platform.PS3;
                    break;

                case 2:
                    ConsoleProfile.PlatformType = PlatformType.PlayStation3SuperSlim;
                    ConsoleProfile.Platform = Platform.PS3;
                    break;

                case 3:
                    ConsoleProfile.PlatformType = PlatformType.PlayStation4;
                    ConsoleProfile.Platform = Platform.PS4;
                    break;

                case 4:
                    ConsoleProfile.PlatformType = PlatformType.Xbox360FatWhite;
                    ConsoleProfile.Platform = Platform.XBOX360;
                    break;

                case 5:
                    ConsoleProfile.PlatformType = PlatformType.Xbox360EliteFatBlack;
                    ConsoleProfile.Platform = Platform.XBOX360;
                    break;

                case 6:
                    ConsoleProfile.PlatformType = PlatformType.Xbox360Slim;
                    ConsoleProfile.Platform = Platform.XBOX360;
                    break;

                case 7:
                    ConsoleProfile.PlatformType = PlatformType.Xbox360SlimE;
                    ConsoleProfile.Platform = Platform.XBOX360;
                    break;
            }

            if (ConsoleProfile.Platform == Platform.PS3 || ConsoleProfile.Platform == Platform.PS4)
            {
                _labelAddress.Text = Lookup("LABEL_IP_ADDRESS", "IP Address") + ":";
            }
            else if (ConsoleProfile.Platform == Platform.XBOX360)
            {
                _labelAddress.Text = Lookup("LABEL_IP_OR_NAME", "IP/Name") + ":";
            }

            _checkBoxDefaultLogin.Checked = ConsoleProfile.Platform == Platform.PS3 || ConsoleProfile.Platform == Platform.PS4;
            _buttonEditLoginDetails.Enabled = ConsoleProfile.Platform == Platform.PS3 || ConsoleProfile.Platform == Platform.PS4;
            _checkBoxGoldHEN.Enabled = ConsoleProfile.Platform == Platform.PS4;
            _checkBoxGoldHEN.Checked = false;
        }

        private void CheckBoxDefaultLogin_CheckedChanged(object sender, EventArgs e)
        {
            if (_revertingCheck)
            {
                return;
            }

            // Only one Xbox 360 profile may use the default login. The old form refused the change in both directions.
            foreach (ConsoleProfile consoleProfile in AppHost.Settings.ConsoleProfiles.FindAll(x => x.Platform == Platform.XBOX360))
            {
                if (consoleProfile != ConsoleProfile && consoleProfile.UseDefaultLogin)
                {
                    _ = MessageBox.Show(
                        this,
                        string.Format(Lookup("DEFAULT_CONSOLE_ALREADY_EXISTS", "'{0}' already uses the default login. Only one Xbox 360 profile can use it."), consoleProfile.Name),
                        Lookup("LABEL_DEFAULT_CONSOLE", "Default Console"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                    RevertCheckBox(_checkBoxDefaultLogin);
                    return;
                }
            }

            ConsoleProfile.UseDefaultLogin = _checkBoxDefaultLogin.Checked;

            bool isXboxDefaultLogin = _checkBoxDefaultLogin.Checked && ConsoleProfile.Platform == Platform.XBOX360;

            _labelAddress.Enabled = !isXboxDefaultLogin;
            _textBoxAddress.Enabled = !isXboxDefaultLogin;
            _buttonEditLoginDetails.Enabled = !isXboxDefaultLogin;
            _textBoxAddress.Text = isXboxDefaultLogin
                ? Lookup("LABEL_DEFAULT_PROFILE", "Default Profile")
                : ConsoleProfile.Address;
        }

        private void ButtonEditCredentials_Click(object sender, EventArgs e)
        {
            using (EditLoginDialog consoleCredentials = new EditLoginDialog())
            {
                consoleCredentials.Language = Language;
                consoleCredentials.TextBoxUsername.Text = ConsoleProfile.Username;
                consoleCredentials.TextBoxPassword.Text = ConsoleProfile.Password;

                DialogResult setCredentials = consoleCredentials.ShowDialog(this);

                switch (setCredentials)
                {
                    case DialogResult.OK:
                        ConsoleProfile.Username = consoleCredentials.TextBoxUsername.Text;
                        ConsoleProfile.Password = consoleCredentials.TextBoxPassword.Text;
                        ConsoleProfile.UseDefaultLogin = false;
                        break;

                    case DialogResult.Abort:
                        ConsoleProfile.Username = string.Empty;
                        ConsoleProfile.Password = string.Empty;
                        ConsoleProfile.UseDefaultLogin = true;
                        break;
                }
            }
        }

        private void CheckBoxGoldHEN_CheckedChanged(object sender, EventArgs e)
        {
            ConsoleProfile.GoldHEN = _checkBoxGoldHEN.Checked;
        }

        private void CheckBoxDefaultProfile_CheckedChanged(object sender, EventArgs e)
        {
            if (_revertingCheck)
            {
                return;
            }

            // Before the change is accepted: if another profile is the default, ask whether to take it over.
            string typedName = _textBoxProfileName.Text.Trim();

            foreach (ConsoleProfile consoleProfile in AppHost.Settings.ConsoleProfiles)
            {
                if (!consoleProfile.Name.EqualsIgnoreCase(typedName) && consoleProfile.IsDefault)
                {
                    DialogResult answer = MessageBox.Show(
                        this,
                        string.Format(Lookup("DEFAULT_PROFILE_ALREADY_EXISTS", "You've already set '{0}' as your default profile. Would you like to replace it with this one?"), consoleProfile.Name),
                        Lookup("LABEL_DEFAULT_PROFILE", "Default Profile"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation);

                    if (answer == DialogResult.Yes)
                    {
                        consoleProfile.IsDefault = false;
                    }
                    else
                    {
                        RevertCheckBox(_checkBoxDefaultProfile);
                        return;
                    }

                    break;
                }
            }

            // After the change: at least one profile must stay the default.
            if (!_checkBoxDefaultProfile.Checked && CountOtherDefaults() == 0)
            {
                _ = MessageBox.Show(
                    this,
                    Lookup("MUST_HAVE_DEFAULT_CONSOLE", "You must have at least one profile set as your default."),
                    Lookup("LABEL_DEFAULT_CONSOLE", "Default Console"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);

                RevertCheckBox(_checkBoxDefaultProfile);
                ConsoleProfile.IsDefault = true;
                return;
            }

            ConsoleProfile.IsDefault = _checkBoxDefaultProfile.Checked;
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            string name = _textBoxProfileName.Text.Trim();
            string address = _textBoxAddress.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                _ = MessageBox.Show(
                    this,
                    Lookup("MISSING_PROFILE_NAME", "You must enter a profile name."),
                    Lookup("NO_INPUT", "No Input"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                string missingText = ConsoleProfile.Platform == Platform.PS3
                    ? Lookup("MISSING_IP_ADDRESS", "You must enter an IP Address.")
                    : Lookup("MISSING_IP_OR_NAME", "You must enter an IP Address or Console Name.");

                _ = MessageBox.Show(this, missingText, Lookup("NO_INPUT", "No Input"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            // Default login fills in the stored credentials for each platform.
            if (ConsoleProfile.UseDefaultLogin)
            {
                if (ConsoleProfile.Platform == Platform.PS3)
                {
                    ConsoleProfile.Username = "anonymous";
                    ConsoleProfile.Password = "anonymous";
                }
                else if (ConsoleProfile.Platform == Platform.PS4)
                {
                    ConsoleProfile.Username = "";
                    ConsoleProfile.Password = "";
                }
                else if (ConsoleProfile.Platform == Platform.XBOX360)
                {
                    ConsoleProfile.Username = "xboxftp";
                    ConsoleProfile.Password = "xboxftp";
                }
            }

            // Kept from the old form: the IP is checked only when NOT using the default login (the reverse of what a user expects),
            // and never for Xbox 360.
            bool isAddressValid;

            if (ConsoleProfile.UseDefaultLogin)
            {
                isAddressValid = true;
            }
            else
            {
                isAddressValid = ConsoleProfile.Platform == Platform.XBOX360 || IPAddress.TryParse(address, out _);
            }

            if (!isAddressValid)
            {
                _ = MessageBox.Show(
                    this,
                    Lookup("INCORRECT_IP_FORMAT", "IP Address isn't in the correct format."),
                    Lookup("INVALID_INPUT", "Invalid Input"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            if (IsEditingProfile)
            {
                if (ConsoleProfile.Name != name && ProfileExists(name))
                {
                    _ = MessageBox.Show(
                        this,
                        Lookup("PROFILE_NAME_EXISTS", "A console profile with this name already exists. Please choose a different name or rename the existing one."),
                        Lookup("LABEL_PROFILE_DETAILS", "Profile Details"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                    return;
                }

                ConsoleProfile.Name = name;
                ConsoleProfile.Address = address;
                DialogResult = DialogResult.OK;
                return;
            }

            if (ProfileExists(name))
            {
                _ = MessageBox.Show(
                    this,
                    Lookup("PROFILE_NAME_EXISTS", "A console profile with this name already exists. Please choose a different name or rename the existing one."),
                    Lookup("LABEL_PROFILE_DETAILS", "Profile Details"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }

            ConsoleProfile.Id = DataExtensions.GenerateUniqueId();
            ConsoleProfile.Name = name;
            ConsoleProfile.Address = address;
            DialogResult = DialogResult.OK;
        }

        /// <summary>
        /// Case-sensitive name check, as in the old form.
        /// </summary>
        private static bool ProfileExists(string name)
        {
            foreach (ConsoleProfile console in AppHost.Settings.ConsoleProfiles)
            {
                if (string.Equals(console.Name, name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
