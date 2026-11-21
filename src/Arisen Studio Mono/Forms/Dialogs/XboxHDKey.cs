using ArisenStudio.Extensions;
using ArisenStudio.Runtime;
using System;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Computes an Xbox 360 HDD key (XboxHDKey) from the console serial and motherboard serial, or imports both serials from a keyvault file.
    /// Stock WinForms port of the old DevExpress XboxHDKey form. It needs no console connection and has no host hooks.
    /// Set Language before the dialog is shown when localised texts are wanted; otherwise the English texts are used.
    /// </summary>
    internal class XboxHDKey : Form
    {
        private readonly TableLayoutPanel _root;
        private readonly Label _labelConsoleSerial;
        private readonly Label _labelMotherboardSerial;
        private readonly Label _labelKey;
        private readonly TextBox _textBoxConsoleSerial;
        private readonly TextBox _textBoxMotherboardSerial;
        private readonly TextBox _textBoxKey;
        private readonly Button _buttonImportFromFile;
        private readonly Button _buttonSaveFile;

        public XboxHDKey()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "XboxHDKey";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            int pad = Font.Height / 2;

            _labelConsoleSerial = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, pad / 2), Text = "Console Serial Number (12 digit number)" };
            _textBoxConsoleSerial = new TextBox { Dock = DockStyle.Fill, MaxLength = 12, Margin = new Padding(0, 0, 0, pad) };
            _textBoxConsoleSerial.TextChanged += TextBoxConsoleSerial_TextChanged;

            _labelMotherboardSerial = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, pad / 2), Text = "Motherboard Serial Number (16 character hexadecimal string)" };
            _textBoxMotherboardSerial = new TextBox { Dock = DockStyle.Fill, MaxLength = 16, Margin = new Padding(0, 0, 0, pad) };
            _textBoxMotherboardSerial.TextChanged += TextBoxMotherboardSerial_TextChanged;

            _labelKey = new Label { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, pad / 2), Text = "XboxHDKey" };
            _textBoxKey = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad) };

            _buttonImportFromFile = new Button { AutoSize = true, Text = "Import from KV file", Margin = new Padding(0, 0, pad, 0) };
            _buttonImportFromFile.Click += ButtonImportFromFile_Click;

            // Starts disabled: only a successful keyvault import enables saving, so typed-in serials cannot be saved.
            _buttonSaveFile = new Button { AutoSize = true, Enabled = false, Text = "Save File", Margin = new Padding(0) };
            _buttonSaveFile.Click += ButtonSaveFile_Click;

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            actions.Controls.Add(_buttonImportFromFile);
            actions.Controls.Add(_buttonSaveFile);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Margin = Padding.Empty,
                Padding = new Padding(pad)
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++)
            {
                _ = _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            _root.Controls.Add(_labelConsoleSerial, 0, 0);
            _root.Controls.Add(_textBoxConsoleSerial, 0, 1);
            _root.Controls.Add(_labelMotherboardSerial, 0, 2);
            _root.Controls.Add(_textBoxMotherboardSerial, 0, 3);
            _root.Controls.Add(_labelKey, 0, 4);
            _root.Controls.Add(_textBoxKey, 0, 5);
            _root.Controls.Add(actions, 0, 6);

            Controls.Add(_root);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// </summary>
        public ResourceManager Language { get; set; }

        private enum IMPORT_RESULT
        {
            ERROR_SUCCESS,
            ERROR_FILE_ACCESS,
            ERROR_FILE_SIZE,
            ERROR_BAD_REGION,
        }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            SizeToContent();
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
            _buttonImportFromFile.Text = Lookup("LABEL_IMPORT_FROM_KV_FILE", "Import from KV file");
            _buttonSaveFile.Text = Lookup("LABEL_SAVE_FILE", "Save File");
        }

        /// <summary>
        /// Sizes the form to its content, at least 26 font heights wide and 14 high, clamped to 90% of the working area of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            Size preferred = _root.PreferredSize;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 26, preferred.Width), (int)(work.Width * 0.9));
            int height = Math.Min(Math.Max(preferred.Height, h * 14), (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void ButtonImportFromFile_Click(object sender, EventArgs e)
        {
            string fileName = ShowOpenFileDialog(Lookup("LABEL_OPEN_FILE", "Keyvault File"), "Keyvault (*.bin)|*.bin|All Files (*.*)|*.*");

            if (!string.IsNullOrEmpty(fileName))
            {
                IMPORT_RESULT result = ImportXboxHDKey(fileName, out string consoleSerial, out string motherboardSerial);

                if (result == IMPORT_RESULT.ERROR_FILE_ACCESS)
                {
                    _ = MessageBox.Show(this, "Failed to access or read keyvault file.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result == IMPORT_RESULT.ERROR_FILE_SIZE)
                {
                    _ = MessageBox.Show(this, "Selected file is not a valid keyvault file.\n\nInvalid size", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result == IMPORT_RESULT.ERROR_BAD_REGION)
                {
                    _ = MessageBox.Show(this, "Selected file is not a valid keyvault file.\n\nInvalid region. Supplied keyvault file must be decrypted", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    _textBoxConsoleSerial.Text = consoleSerial;
                    _textBoxMotherboardSerial.Text = motherboardSerial;
                    _buttonSaveFile.Enabled = true;
                }
            }
        }

        private void ButtonSaveFile_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxConsoleSerial.Text) && _textBoxConsoleSerial.Text.Length == 12 && StringExtensions.ValidNumericString(_textBoxConsoleSerial.Text))
            {
                if (!string.IsNullOrEmpty(_textBoxMotherboardSerial.Text) && _textBoxMotherboardSerial.Text.Length == 16 && StringExtensions.ValidHexadecimalString(_textBoxMotherboardSerial.Text))
                {
                    if (!string.IsNullOrEmpty(_textBoxKey.Text))
                    {
                        DialogResult result = MessageBox.Show(this, "Save the console/motherboard serial numbers to the file as well?", "Save to text file", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                        if (result == DialogResult.Cancel)
                        {
                            return;
                        }

                        bool saveSerials = result == DialogResult.Yes;
                        StringBuilder output = new StringBuilder();
                        if (saveSerials)
                        {
                            _ = output.AppendLine("Console serial: " + _textBoxConsoleSerial.Text);
                            _ = output.AppendLine("Motherboard serial: " + _textBoxMotherboardSerial.Text.ToUpperInvariant());
                            _ = output.AppendLine();
                        }
                        _ = output.Append("XboxHDKey: " + _textBoxKey.Text);

                        string fileName = ShowSaveFileDialog(Lookup("LABEL_SAVE_FILE", "Save File"), "Text file (*.txt)|*.txt|All files (*.*)|*.*");
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            return;
                        }

                        try
                        {
                            using (FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                            {
                                writer.Write(output.ToString());
                                writer.Flush();
                            }
                        }
                        catch (Exception ex)
                        {
                            AppHost.Logger?.Invoke(ex, "Unable to write the XboxHDKey file.");
                            _ = MessageBox.Show(this, "Unable to write the XboxHDKey file. Error Message: " + ex.Message, Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        // Success is shown only after the file is written.
                        _ = MessageBox.Show(this, "Successfully created XboxHDKey file.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void TextBoxConsoleSerial_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxConsoleSerial.Text) && _textBoxConsoleSerial.Text.Length == 12)
            {
                if (StringExtensions.ValidNumericString(_textBoxConsoleSerial.Text))
                {
                    _textBoxKey.Text = MakeXboxHDKey(_textBoxConsoleSerial.Text, _textBoxMotherboardSerial.Text);
                }
                else
                {
                    _ = MessageBox.Show(this, "Invalid console serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                _ = MessageBox.Show(this, "Invalid console serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TextBoxMotherboardSerial_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxMotherboardSerial.Text) && _textBoxMotherboardSerial.Text.Length == 16)
            {
                if (StringExtensions.ValidHexadecimalString(_textBoxMotherboardSerial.Text))
                {
                    _textBoxKey.Text = MakeXboxHDKey(_textBoxConsoleSerial.Text, _textBoxMotherboardSerial.Text);
                }
                else
                {
                    _ = MessageBox.Show(this, "Invalid motherboard serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                _ = MessageBox.Show(this, "Invalid motherboard serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string MakeXboxHDKey(string consoleSerial, string motherboardSerial)
        {
            if (!string.IsNullOrEmpty(consoleSerial) && consoleSerial.Length == 12 && StringExtensions.ValidNumericString(consoleSerial))
            {
                if (!string.IsNullOrEmpty(motherboardSerial) && motherboardSerial.Length == 16 && StringExtensions.ValidHexadecimalString(motherboardSerial))
                {
                    byte[] key = new byte[0x10];
                    byte[] consoleSerialBuffer = new byte[12];
                    byte[] motherboardSerialBuffer = new byte[16 / 2];

                    for (int i = 0; i < 12; i++)
                    {
                        consoleSerialBuffer[i] = Convert.ToByte(consoleSerial.Substring(i, 1)[0]);
                    }

                    for (int i = 0; i < 16; i += 2)
                    {
                        motherboardSerialBuffer[i / 2] = Convert.ToByte(motherboardSerial.Substring(i, 2), 16);
                    }

                    Array.Copy(motherboardSerialBuffer, 0, key, 4, 8);
                    Array.Copy(consoleSerialBuffer, 0, key, 12, 4);

                    return BitConverter.ToString(key, 0).Replace("-", "");
                }
            }

            return "";
        }

        private static IMPORT_RESULT ImportXboxHDKey(string keyvaultFile, out string consoleSerial, out string motherboardSerial)
        {
            const ushort REGION_CODE_NTSCU = 0x00FF;
            const ushort REGION_CODE_NTSCJ = 0x01FF;
            const ushort REGION_CODE_NTSCK = 0x01FC;
            const ushort REGION_CODE_PAL = 0x02FE;

            const int KEYVAULT_FILE_SIZE_A = 0x3FF0;
            const int KEYVAULT_FILE_SIZE_B = 0x4000;

            const int CONSOLE_SERIAL_OFFSET = 0xA0;
            const int CONSOLE_SERIAL_SIZE = 12;

            const int MOTHERBOARD_SERIAL_OFFSET = 0xB0;
            const int MOTHERBOARD_SERIAL_SIZE = 8;

            const int GAME_REGION_OFFSET = 0xB8;

            consoleSerial = "";
            motherboardSerial = "";

            if (string.IsNullOrEmpty(keyvaultFile))
            {
                return IMPORT_RESULT.ERROR_FILE_ACCESS;
            }

            FileInfo file = new FileInfo(keyvaultFile);
            int fileSize = (int)file.Length;

            if (fileSize != KEYVAULT_FILE_SIZE_A && fileSize != KEYVAULT_FILE_SIZE_B)
            {
                return IMPORT_RESULT.ERROR_FILE_SIZE;
            }

            byte[] buffer = new byte[fileSize];

            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    if (stream.Read(buffer, 0, fileSize) != fileSize)
                    {
                        return IMPORT_RESULT.ERROR_FILE_ACCESS;
                    }
                }
            }
            catch
            {
                return IMPORT_RESULT.ERROR_FILE_ACCESS;
            }

            int keyvaultTypeOffset = fileSize == KEYVAULT_FILE_SIZE_A ? 0 : 0x10;
            ushort currentRegionCode = BitConverter.ToUInt16(buffer, GAME_REGION_OFFSET + keyvaultTypeOffset);
            currentRegionCode = (ushort)((currentRegionCode >> 8) | (currentRegionCode << 8)); // byte swap for BE

            if (currentRegionCode != REGION_CODE_NTSCU &&
                currentRegionCode != REGION_CODE_PAL &&
                currentRegionCode != REGION_CODE_NTSCJ &&
                currentRegionCode != REGION_CODE_NTSCK)
            {
                return IMPORT_RESULT.ERROR_BAD_REGION;
            }

            consoleSerial = Encoding.UTF8.GetString(buffer, CONSOLE_SERIAL_OFFSET + keyvaultTypeOffset, CONSOLE_SERIAL_SIZE);
            motherboardSerial = BitConverter.ToString(buffer, MOTHERBOARD_SERIAL_OFFSET + keyvaultTypeOffset, MOTHERBOARD_SERIAL_SIZE).Replace("-", "");

            return IMPORT_RESULT.ERROR_SUCCESS;
        }

        private string ShowOpenFileDialog(string title, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = title, Filter = filter })
            {
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private string ShowSaveFileDialog(string title, string filter)
        {
            using (SaveFileDialog dialog = new SaveFileDialog { Title = title, Filter = filter })
            {
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
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
