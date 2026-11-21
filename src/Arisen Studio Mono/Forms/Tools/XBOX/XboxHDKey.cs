using System;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Derives an Xbox 360 HDD key from a console serial and a motherboard serial, or imports both serials
    /// from a keyvault dump. Stock WinForms port of the old DevExpress XboxHDKey form.
    /// This tool needs no console connection, so it exposes no host hooks; only the optional Language source is set by the host.
    /// </summary>
    public class XboxHDKey : Form
    {
        private const int KeyvaultSizeA = 0x3FF0;
        private const int KeyvaultSizeB = 0x4000;
        private const int ConsoleSerialOffset = 0xA0;
        private const int ConsoleSerialSize = 12;
        private const int MotherboardSerialOffset = 0xB0;
        private const int MotherboardSerialSize = 8;
        private const int GameRegionOffset = 0xB8;
        private const ushort RegionCodeNtscU = 0x00FF;
        private const ushort RegionCodeNtscJ = 0x01FF;
        private const ushort RegionCodeNtscK = 0x01FC;
        private const ushort RegionCodePal = 0x02FE;

        private readonly Label _labelConsoleSerial;
        private readonly Label _labelMotherboardSerial;
        private readonly Label _labelXboxHDKey;
        private readonly TextBox _textBoxConsoleSerial;
        private readonly TextBox _textBoxMotherboardSerial;
        private readonly TextBox _textBoxXboxHDKey;
        private readonly Button _buttonImportFromFile;
        private readonly Button _buttonSaveFile;

        private enum ImportResult
        {
            Success,
            ErrorFileAccess,
            ErrorFileSize,
            ErrorBadRegion
        }

        public XboxHDKey()
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

            _labelConsoleSerial = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            _labelMotherboardSerial = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            _labelXboxHDKey = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };

            _textBoxConsoleSerial = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 12,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _textBoxMotherboardSerial = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 16,
                Margin = new Padding(0, 0, 0, pad / 2)
            };
            _textBoxXboxHDKey = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, pad / 2)
            };

            _buttonImportFromFile = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 8, 0),
                Margin = new Padding(0, 0, pad, 0)
            };
            _buttonSaveFile = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(h * 5, 0),
                Margin = new Padding(0),
                Enabled = false
            };

            FlowLayoutPanel buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, pad / 2, 0, 0)
            };
            buttonRow.Controls.Add(_buttonImportFromFile);
            buttonRow.Controls.Add(_buttonSaveFile);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++)
            {
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            root.Controls.Add(_labelConsoleSerial, 0, 0);
            root.Controls.Add(_textBoxConsoleSerial, 0, 1);
            root.Controls.Add(_labelMotherboardSerial, 0, 2);
            root.Controls.Add(_textBoxMotherboardSerial, 0, 3);
            root.Controls.Add(_labelXboxHDKey, 0, 4);
            root.Controls.Add(_textBoxXboxHDKey, 0, 5);
            root.Controls.Add(buttonRow, 0, 6);
            Controls.Add(root);

            _textBoxConsoleSerial.TabIndex = 0;
            _textBoxMotherboardSerial.TabIndex = 1;
            _textBoxXboxHDKey.TabIndex = 2;
            buttonRow.TabIndex = 3;

            _buttonImportFromFile.Click += ButtonImportFromFile_Click;
            _buttonSaveFile.Click += ButtonSaveFile_Click;
            _textBoxConsoleSerial.TextChanged += TextBoxConsoleSerial_TextChanged;
            _textBoxMotherboardSerial.TextChanged += TextBoxMotherboardSerial_TextChanged;

            ApplyLanguage();

            ClientSize = new Size(Math.Min(h * 34, (int)(Screen.FromControl(this).WorkingArea.Width * 0.9)), h * 14);
        }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Text = "XboxHDKey";

            _labelConsoleSerial.Text = "Console Serial Number (12 digit number)";
            _labelMotherboardSerial.Text = "Motherboard Serial Number (16 character hexadecimal string)";
            _labelXboxHDKey.Text = "XboxHDKey";

            _buttonImportFromFile.Text = Lookup("LABEL_IMPORT_FROM_KV_FILE", "Import from KV file");
            _buttonSaveFile.Text = Lookup("LABEL_SAVE_FILE", "Save File");
        }

        private void ButtonImportFromFile_Click(object sender, EventArgs e)
        {
            string fileName = ShowOpenFileDialog("Keyvault File", "Keyvault (*.bin)|*.bin|All Files (*.*)|*.*");

            if (!string.IsNullOrEmpty(fileName))
            {
                string consoleSerial;
                string motherboardSerial;
                ImportResult result = ImportXboxHDKey(fileName, out consoleSerial, out motherboardSerial);

                if (result == ImportResult.ErrorFileAccess)
                {
                    _ = MessageBox.Show(this, "Failed to access or read keyvault file.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result == ImportResult.ErrorFileSize)
                {
                    _ = MessageBox.Show(this, "Selected file is not a valid keyvault file.\n\nInvalid size", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (result == ImportResult.ErrorBadRegion)
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
            if (!string.IsNullOrEmpty(_textBoxConsoleSerial.Text) && _textBoxConsoleSerial.Text.Length == 12 && IsNumeric(_textBoxConsoleSerial.Text))
            {
                if (!string.IsNullOrEmpty(_textBoxMotherboardSerial.Text) && _textBoxMotherboardSerial.Text.Length == 16 && IsHexadecimal(_textBoxMotherboardSerial.Text))
                {
                    if (!string.IsNullOrEmpty(_textBoxXboxHDKey.Text))
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
                            output.AppendLine("Console serial: " + _textBoxConsoleSerial.Text);
                            output.AppendLine("Motherboard serial: " + _textBoxMotherboardSerial.Text.ToUpperInvariant());
                            output.AppendLine();
                        }
                        output.Append("XboxHDKey: " + _textBoxXboxHDKey.Text);

                        string fileName = ShowSaveFileDialog("Save File", "Text file (*.txt)|*.txt|All files (*.*)|*.*");

                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            File.WriteAllText(fileName, output.ToString(), Encoding.UTF8);
                        }

                        // Preserved from the original: this success box also appears when the save dialog was cancelled.
                        _ = MessageBox.Show(this, "Successfully created XboxHDKey file.", Lookup("SUCCESS", "Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void TextBoxConsoleSerial_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxConsoleSerial.Text) && _textBoxConsoleSerial.Text.Length == 12)
            {
                if (IsNumeric(_textBoxConsoleSerial.Text))
                {
                    _textBoxXboxHDKey.Text = MakeXboxHDKey(_textBoxConsoleSerial.Text, _textBoxMotherboardSerial.Text);
                }
                else
                {
                    _ = MessageBox.Show(this, "Invalid console serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Preserved from the original: this error fires on every partial keystroke.
                _ = MessageBox.Show(this, "Invalid console serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TextBoxMotherboardSerial_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_textBoxMotherboardSerial.Text) && _textBoxMotherboardSerial.Text.Length == 16)
            {
                if (IsHexadecimal(_textBoxMotherboardSerial.Text))
                {
                    _textBoxXboxHDKey.Text = MakeXboxHDKey(_textBoxConsoleSerial.Text, _textBoxMotherboardSerial.Text);
                }
                else
                {
                    _ = MessageBox.Show(this, "Invalid motherboard serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Preserved from the original: this error fires on every partial keystroke.
                _ = MessageBox.Show(this, "Invalid motherboard serial value.", Lookup("ERROR", "Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Builds the 16-byte key as 32 hex characters: motherboard serial bytes at offset 4 (8 bytes),
        /// the first 4 console serial ASCII bytes at offset 12. Returns an empty string for invalid input.
        /// </summary>
        private static string MakeXboxHDKey(string consoleSerial, string motherboardSerial)
        {
            if (!string.IsNullOrEmpty(consoleSerial) && consoleSerial.Length == 12 && IsNumeric(consoleSerial))
            {
                if (!string.IsNullOrEmpty(motherboardSerial) && motherboardSerial.Length == 16 && IsHexadecimal(motherboardSerial))
                {
                    byte[] key = new byte[0x10];
                    byte[] consoleSerialBuffer = new byte[12];
                    byte[] motherboardSerialBuffer = new byte[16 / 2];

                    for (int i = 0; i < 12; i++)
                    {
                        consoleSerialBuffer[i] = (byte)consoleSerial[i];
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

        private static ImportResult ImportXboxHDKey(string keyvaultFile, out string consoleSerial, out string motherboardSerial)
        {
            consoleSerial = "";
            motherboardSerial = "";

            if (string.IsNullOrEmpty(keyvaultFile))
            {
                return ImportResult.ErrorFileAccess;
            }

            FileInfo file = new FileInfo(keyvaultFile);
            int fileSize = (int)file.Length;

            if (fileSize != KeyvaultSizeA && fileSize != KeyvaultSizeB)
            {
                return ImportResult.ErrorFileSize;
            }

            byte[] buffer = new byte[fileSize];

            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    if (stream.Read(buffer, 0, fileSize) != fileSize)
                    {
                        return ImportResult.ErrorFileAccess;
                    }
                }
            }
            catch (Exception)
            {
                return ImportResult.ErrorFileAccess;
            }

            int keyvaultTypeOffset = fileSize == KeyvaultSizeA ? 0 : 0x10;
            ushort currentRegionCode = BitConverter.ToUInt16(buffer, GameRegionOffset + keyvaultTypeOffset);
            currentRegionCode = (ushort)((currentRegionCode >> 8) | (currentRegionCode << 8)); // byte swap for big-endian

            if (currentRegionCode != RegionCodeNtscU &&
                currentRegionCode != RegionCodePal &&
                currentRegionCode != RegionCodeNtscJ &&
                currentRegionCode != RegionCodeNtscK)
            {
                return ImportResult.ErrorBadRegion;
            }

            consoleSerial = Encoding.UTF8.GetString(buffer, ConsoleSerialOffset + keyvaultTypeOffset, ConsoleSerialSize);
            motherboardSerial = BitConverter.ToString(buffer, MotherboardSerialOffset + keyvaultTypeOffset, MotherboardSerialSize).Replace("-", "");

            return ImportResult.Success;
        }

        private string ShowOpenFileDialog(string title, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private string ShowSaveFileDialog(string title, string filter)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private static bool IsNumeric(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHexadecimal(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char c in value)
            {
                bool isDigit = c >= '0' && c <= '9';
                bool isUpper = c >= 'A' && c <= 'F';
                bool isLower = c >= 'a' && c <= 'f';
                if (!isDigit && !isUpper && !isLower)
                {
                    return false;
                }
            }

            return true;
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
