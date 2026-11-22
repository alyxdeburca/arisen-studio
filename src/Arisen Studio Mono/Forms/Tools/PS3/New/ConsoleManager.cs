using System;
using System.Drawing;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.PS3
{
    /// <summary>
    /// PS3 console manager: temperatures, firmware details, console LEDs, buzzer, and setting the IDPS and PSID.
    /// Stock WinForms port of the old DevExpress ConsoleManager form. The console calls (PS3 Manager API) are not ported;
    /// they sit behind IPs3ConsoleControl.
    /// Host hooks, set before the form is shown:
    ///  - ConsoleControl: the console boundary (default: a stub whose Connect fails, so the form shows that message and closes).
    ///  - ConsoleAddress: the address of the console to connect to.
    /// </summary>
    public class ConsoleManager : Form
    {
        private readonly Label _valueTempCell;
        private readonly Label _valueTempRsx;
        private readonly Label _valueFirmwareVersion;
        private readonly Label _valueFirmwareType;
        private readonly Label _valueCoreVersion;
        private readonly Button _buttonRefresh;
        private readonly GroupBox _groupDetails;
        private readonly GroupBox _groupLeds;
        private readonly GroupBox _groupIds;
        private readonly Label _labelTemps;
        private readonly Label _labelCell;
        private readonly Label _labelRsx;
        private readonly Label _labelFirmwareVersion;
        private readonly Label _labelFirmwareType;
        private readonly Label _labelCoreVersion;
        private readonly Label _labelRed;
        private readonly Label _labelGreen;
        private readonly Label _labelBuzzer;
        private readonly Label _labelIdps;
        private readonly Label _labelPsid;
        private readonly RadioGroupPanel _radioRed;
        private readonly RadioGroupPanel _radioGreen;
        private readonly RadioGroupPanel _radioBuzzer;
        private readonly Button _buttonSetLeds;
        private readonly Button _buttonRingBuzzer;
        private readonly TextBox _textBoxIdps;
        private readonly TextBox _textBoxPsid;
        private readonly Button _buttonSetIdps;
        private readonly Button _buttonSetPsid;
        private readonly ToolStripStatusLabel _status;

        public ConsoleManager()
        {
            ToolFormSupport.ConfigureForm(this, false);

            int h = Font.Height;
            int pad = h / 2;

            // Details
            _labelTemps = ToolFormSupport.NewLabel(string.Empty);
            _labelCell = ToolFormSupport.NewLabel(string.Empty);
            _labelRsx = ToolFormSupport.NewLabel(string.Empty);
            _labelFirmwareVersion = ToolFormSupport.NewLabel(string.Empty);
            _labelFirmwareType = ToolFormSupport.NewLabel(string.Empty);
            _labelCoreVersion = ToolFormSupport.NewLabel(string.Empty);
            _valueTempCell = NewValueLabel();
            _valueTempRsx = NewValueLabel();
            _valueFirmwareVersion = NewValueLabel();
            _valueFirmwareType = NewValueLabel();
            _valueCoreVersion = NewValueLabel();
            _buttonRefresh = ToolFormSupport.NewButton(this, 8);

            TableLayoutPanel details = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddFullRow(details, _labelTemps, false);
            ToolFormSupport.AddRow(details, _labelCell, _valueTempCell);
            ToolFormSupport.AddRow(details, _labelRsx, _valueTempRsx);
            ToolFormSupport.AddRow(details, _labelFirmwareVersion, _valueFirmwareVersion);
            ToolFormSupport.AddRow(details, _labelFirmwareType, _valueFirmwareType);
            ToolFormSupport.AddRow(details, _labelCoreVersion, _valueCoreVersion);
            ToolFormSupport.AddFullRow(details, _buttonRefresh, false);
            _groupDetails = NewGroup(pad, details);

            // LEDs and buzzer
            string[] ledModes = { "Off", "On", "Blink Fast", "Blink Slow" };
            _labelRed = ToolFormSupport.NewLabel(string.Empty);
            _labelGreen = ToolFormSupport.NewLabel(string.Empty);
            _labelBuzzer = ToolFormSupport.NewLabel(string.Empty);
            _radioRed = new RadioGroupPanel(ledModes, 0);
            _radioGreen = new RadioGroupPanel(ledModes, 0);
            _radioBuzzer = new RadioGroupPanel(new[] { "Single", "Double", "Triple" }, 0);
            _buttonSetLeds = ToolFormSupport.NewButton(this, 8);
            _buttonRingBuzzer = ToolFormSupport.NewButton(this, 8);

            TableLayoutPanel leds = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddRow(leds, _labelRed, _radioRed);
            ToolFormSupport.AddRow(leds, _labelGreen, _radioGreen);
            ToolFormSupport.AddFullRow(leds, _buttonSetLeds, false);
            ToolFormSupport.AddRow(leds, _labelBuzzer, _radioBuzzer);
            ToolFormSupport.AddFullRow(leds, _buttonRingBuzzer, false);
            _groupLeds = NewGroup(pad, leds);

            // IDPS and PSID
            _labelIdps = ToolFormSupport.NewLabel(string.Empty);
            _labelPsid = ToolFormSupport.NewLabel(string.Empty);
            // Text boxes stretch across their cell and keep their own height; labels and Set buttons centre on the same row, as the old designer's PSID label sits level with its box.
            _textBoxIdps = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 32, Margin = new Padding(0, pad / 4, pad, pad / 4) };
            _textBoxPsid = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 32, Margin = new Padding(0, pad / 4, pad, pad / 4) };
            _buttonSetIdps = ToolFormSupport.NewButton(this, 6);
            _buttonSetPsid = ToolFormSupport.NewButton(this, 6);
            _buttonSetIdps.Margin = new Padding(0, pad / 4, 0, pad / 4);
            _buttonSetPsid.Margin = new Padding(0, pad / 4, 0, pad / 4);

            TableLayoutPanel ids = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
            int row = ToolFormSupport.AddRow(ids, _labelIdps, _textBoxIdps);
            ids.Controls.Add(_buttonSetIdps, 2, row);
            row = ToolFormSupport.AddRow(ids, _labelPsid, _textBoxPsid);
            ids.Controls.Add(_buttonSetPsid, 2, row);
            _groupIds = NewGroup(pad, ids);

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            // Fixed row heights: group height + bottom margin (pad 10 at the 20 px font). Details 272, LEDs and buzzer 308, Console IDs 152.
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupDetails, false), 282);
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupLeds, false), 318);
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupIds, false), 162);
            // Spare height goes to this filler row, not into a group row (as BackupFilesManager does).
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowCount = root.RowStyles.Count;

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _buttonRefresh.Click += ButtonRefresh_Click;
            _buttonSetLeds.Click += ButtonSetLeds_Click;
            _buttonRingBuzzer.Click += ButtonRingBuzzer_Click;
            _buttonSetIdps.Click += ButtonSetIdps_Click;
            _buttonSetPsid.Click += ButtonSetPsid_Click;

            ApplyLanguage();
            SetControlsEnabled(false);

            // Fixed client size (no run-time measuring). Height 840 = root rows 762 + root padding 20 + status bar about 30 + slack; width 800 keeps the radio groups on one line.
            ClientSize = new Size(800, 840);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Console boundary. The default is a stub that fails with a message; see IPs3ConsoleControl.
        /// </summary>
        public IPs3ConsoleControl ConsoleControl { get; set; } = new UnavailablePs3ConsoleControl();

        /// <summary>
        /// Address (IP) of the console to connect to.
        /// </summary>
        public string ConsoleAddress { get; set; }

        private static Label NewValueLabel()
        {
            return new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 3, 0, 3),
                Text = "-"
            };
        }

        private static GroupBox NewGroup(int pad, Control content)
        {
            // Fixed height: the group is sized by its root row (Absolute), so it does not measure its content.
            GroupBox group = new GroupBox
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Padding = new Padding(pad),
                Margin = new Padding(0, 0, 0, pad)
            };
            // The content fills the group. Its rows keep their own heights; a trailing Percent row takes the spare height.
            content.Dock = DockStyle.Fill;
            TableLayoutPanel table = content as TableLayoutPanel;
            if (table != null)
            {
                table.AutoSize = false;
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                table.RowCount = table.RowStyles.Count;
            }
            group.Controls.Add(content);
            return group;
        }

        /// <summary>
        /// Fix a row to an exact height in pixels.
        /// </summary>
        private static int FixRowHeight(TableLayoutPanel table, int row, int height)
        {
            RowStyle style = table.RowStyles[row];
            style.SizeType = SizeType.Absolute;
            style.Height = height;
            return row;
        }

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            Text = Text_("CONSOLE_MANAGER", "Console Manager");

            _groupDetails.Text = Text_("LABEL_DETAILS", "Details");
            _labelTemps.Text = Text_("LABEL_TEMPERATURES", "Temperatures");
            _labelCell.Text = "CELL:";
            _labelRsx.Text = "RSX:";
            _labelFirmwareVersion.Text = Text_("LABEL_FIRMWARE_VERSION", "Firmware Version:");
            _labelFirmwareType.Text = Text_("LABEL_FIRMWARE_TYPE", "Firmware Type:");
            _labelCoreVersion.Text = Text_("LABEL_CORE_VERSION", "Core Version:");
            _buttonRefresh.Text = Text_("LABEL_REFRESH", "Refresh");

            _groupLeds.Text = Text_("LABEL_LEDS_AND_BUZZER", "LEDs && Buzzer");
            _labelRed.Text = Text_("LABEL_RED", "Red:");
            _labelGreen.Text = Text_("LABEL_GREEN", "Green:");
            _labelBuzzer.Text = Text_("LABEL_BUZZER", "Buzzer:");
            _buttonSetLeds.Text = Text_("LABEL_SET_LEDS", "Set LEDs");
            _buttonRingBuzzer.Text = Text_("LABEL_RING_BUZZER", "Ring Buzzer");

            _groupIds.Text = "IDPS && PSID";
            _labelIdps.Text = "IDPS:";
            _labelPsid.Text = "PSID:";
            _buttonSetIdps.Text = Text_("LABEL_SET", "Set");
            _buttonSetPsid.Text = Text_("LABEL_SET", "Set");
        }

        private void SetControlsEnabled(bool enabled)
        {
            _buttonRefresh.Enabled = enabled;
            _buttonSetLeds.Enabled = enabled;
            _buttonRingBuzzer.Enabled = enabled;
            _buttonSetIdps.Enabled = enabled;
            _buttonSetPsid.Enabled = enabled;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                SetStatus(Text_("CONNECTING_TO_CONSOLE", "Connecting to console..."));

                string address = ConsoleAddress;
                await Task.Run(() => ConsoleControl.Connect(address));

                if (ConsoleControl.IsConnected)
                {
                    SetStatus(Text_("SUCCESS_CONNECTED", "Successfully connected."));
                    SetControlsEnabled(true);
                    LoadDetails();
                }
                else
                {
                    SetStatus("Console Manager: " + Text_("UNABLE_TO_CONNECT", "Unable to connect to the console."));
                    ToolFormSupport.ShowErrorAndClose(this, Text_("ERROR", "Error"), Text_("UNABLE_TO_CONNECT", "Unable to connect to the console."));
                }
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Console Manager: unable to connect.");
                SetStatus("Console Manager: " + Text_("UNABLE_TO_CONNECT", "Unable to connect to the console."));
                ToolFormSupport.ShowErrorAndClose(this, Text_("ERROR", "Error"), Text_("UNABLE_TO_CONNECT", "Unable to connect to the console.") + "\n\n" + ex.Message);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (ConsoleControl.IsConnected)
                {
                    ConsoleControl.Disconnect();
                }
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Console Manager: unable to disconnect.");
            }

            base.OnFormClosing(e);
        }

        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            LoadDetails();
        }

        private void LoadDetails()
        {
            try
            {
                uint cell;
                uint rsx;
                ConsoleControl.GetTemperatures(out cell, out rsx);

                _valueTempCell.Text = cell + " °C";
                _valueTempRsx.Text = rsx + " °C";
                _valueFirmwareVersion.Text = ConsoleControl.GetFirmwareVersion();
                _valueFirmwareType.Text = ConsoleControl.GetFirmwareType();
                _valueCoreVersion.Text = ConsoleControl.GetCoreVersion();

                SetStatus("Successfully loaded console details.");
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to fetch console details.");
                SetStatus("Unable to fetch console details.");
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "Unable to fetch console details.\n\nError Message: " + ex.Message);
            }
        }

        private void ButtonSetLeds_Click(object sender, EventArgs e)
        {
            try
            {
                ConsoleControl.SetConsoleLed(Ps3LedColor.Green, (Ps3LedMode)Math.Max(0, _radioGreen.SelectedIndex));
                ConsoleControl.SetConsoleLed(Ps3LedColor.Red, (Ps3LedMode)Math.Max(0, _radioRed.SelectedIndex));
                SetStatus("Console LEDs have been set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the console LEDs.", ex);
            }
        }

        private void ButtonRingBuzzer_Click(object sender, EventArgs e)
        {
            try
            {
                ConsoleControl.RingBuzzer((Ps3BuzzerMode)Math.Max(0, _radioBuzzer.SelectedIndex));
                SetStatus("Ring buzzer set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to ring the buzzer.", ex);
            }
        }

        private void ButtonSetIdps_Click(object sender, EventArgs e)
        {
            if (ToolFormSupport.TryHexToBytes(_textBoxIdps.Text) == null || _textBoxIdps.Text.Length != 32)
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "The IDPS must be 32 hexadecimal characters.");
                return;
            }

            try
            {
                ConsoleControl.SetConsoleId(_textBoxIdps.Text);
                SetStatus("IDPS has been set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the IDPS.", ex);
            }
        }

        private void ButtonSetPsid_Click(object sender, EventArgs e)
        {
            if (ToolFormSupport.TryHexToBytes(_textBoxPsid.Text) == null || _textBoxPsid.Text.Length != 32)
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), "The PSID must be 32 hexadecimal characters.");
                return;
            }

            try
            {
                ConsoleControl.SetPsid(_textBoxPsid.Text);
                SetStatus("PSID has been set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the PSID.", ex);
            }
        }

        private void ReportFailure(string message, Exception ex)
        {
            ToolFormSupport.LogError(ex, message);
            SetStatus(message);
            ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), message + "\n\nError Message: " + ex.Message);
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
