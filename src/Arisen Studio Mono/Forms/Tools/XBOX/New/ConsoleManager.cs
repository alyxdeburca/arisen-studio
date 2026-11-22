using System;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Xbox 360 console manager: details and temperatures, console name and icon, disc tray, fan speed and the four ring LEDs.
    /// Stock WinForms port of the old DevExpress ConsoleManager form. The console calls (XDevkit, JRPC) are not ported;
    /// they sit behind IXboxConsoleInfo.
    /// Host hook, set before the form is shown:
    ///  - Console: the console boundary (default: a stub that fails with a message).
    /// </summary>
    public class ConsoleManager : Form
    {
        private const int FanSpeedMinimum = 0;
        private const int FanSpeedMaximum = 100;

        private readonly Label _valueIp;
        private readonly Label _valueCpuKey;
        private readonly Label _valueKernel;
        private readonly Label _valueTitleId;
        private readonly Label _valueTempCpu;
        private readonly Label _valueTempGpu;
        private readonly Label _valueTempRam;
        private readonly Label _valueTempMb;
        private readonly Label _labelIp;
        private readonly Label _labelCpuKey;
        private readonly Label _labelKernel;
        private readonly Label _labelTitleId;
        private readonly Label _labelTempCpu;
        private readonly Label _labelTempGpu;
        private readonly Label _labelTempRam;
        private readonly Label _labelTempMb;
        private readonly Button _buttonRefresh;
        private readonly GroupBox _groupDetails;

        private readonly Label _labelName;
        private readonly Label _labelIcon;
        private readonly TextBox _textBoxName;
        private readonly Button _buttonSetName;
        private readonly ComboBox _comboIcon;
        private readonly GroupBox _groupNeighborhood;

        private readonly Label _labelTray;
        private readonly Button _buttonTrayOpen;
        private readonly Button _buttonTrayClose;
        private readonly Label _labelFan;
        private readonly TrackBar _trackFan;
        private readonly Label _valueFan;
        private readonly Button _buttonSetFan;
        private readonly GroupBox _groupDiscFan;

        private readonly Label _labelTopLeft;
        private readonly Label _labelTopRight;
        private readonly Label _labelBottomLeft;
        private readonly Label _labelBottomRight;
        private readonly RadioGroupPanel _radioTopLeft;
        private readonly RadioGroupPanel _radioTopRight;
        private readonly RadioGroupPanel _radioBottomLeft;
        private readonly RadioGroupPanel _radioBottomRight;
        private readonly Button _buttonSetLeds;
        private readonly GroupBox _groupLeds;

        private readonly ToolStripStatusLabel _status;

        public ConsoleManager()
        {
            ToolFormSupport.ConfigureForm(this, false);

            int h = Font.Height;
            int pad = h / 2;

            // Details
            _labelIp = ToolFormSupport.NewLabel(string.Empty);
            _labelCpuKey = ToolFormSupport.NewLabel(string.Empty);
            _labelKernel = ToolFormSupport.NewLabel(string.Empty);
            _labelTitleId = ToolFormSupport.NewLabel(string.Empty);
            _labelTempCpu = ToolFormSupport.NewLabel(string.Empty);
            _labelTempGpu = ToolFormSupport.NewLabel(string.Empty);
            _labelTempRam = ToolFormSupport.NewLabel(string.Empty);
            _labelTempMb = ToolFormSupport.NewLabel(string.Empty);
            _valueIp = NewValueLabel();
            _valueCpuKey = NewValueLabel();
            _valueKernel = NewValueLabel();
            _valueTitleId = NewValueLabel();
            _valueTempCpu = NewValueLabel();
            _valueTempGpu = NewValueLabel();
            _valueTempRam = NewValueLabel();
            _valueTempMb = NewValueLabel();
            _buttonRefresh = ToolFormSupport.NewButton(this, 8);

            TableLayoutPanel details = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddRow(details, _labelIp, _valueIp);
            ToolFormSupport.AddRow(details, _labelCpuKey, _valueCpuKey);
            ToolFormSupport.AddRow(details, _labelKernel, _valueKernel);
            ToolFormSupport.AddRow(details, _labelTitleId, _valueTitleId);
            ToolFormSupport.AddRow(details, _labelTempCpu, _valueTempCpu);
            ToolFormSupport.AddRow(details, _labelTempGpu, _valueTempGpu);
            ToolFormSupport.AddRow(details, _labelTempRam, _valueTempRam);
            ToolFormSupport.AddRow(details, _labelTempMb, _valueTempMb);
            ToolFormSupport.AddFullRow(details, _buttonRefresh, false);
            _groupDetails = NewGroup(pad, details);

            // Neighborhood
            _labelName = ToolFormSupport.NewLabel(string.Empty);
            _labelIcon = ToolFormSupport.NewLabel(string.Empty);
            _textBoxName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, pad, pad / 2) };
            _buttonSetName = ToolFormSupport.NewButton(this, 6);
            _buttonSetName.Margin = new Padding(0, 0, 0, pad / 2);
            // Explicit height: the combo's implicit (preferred) height is 20 px here, and its drawn box needs about 28 px (old 24 px at a 16 px line plus 8).
            _comboIcon = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, pad / 2), Height = h + 8 };
            _comboIcon.Items.AddRange(new object[] { "Black", "Blue", "BlueGray", "White" });

            TableLayoutPanel neighborhood = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F), new ColumnStyle(SizeType.AutoSize));
            int row = ToolFormSupport.AddRow(neighborhood, _labelName, _textBoxName);
            neighborhood.Controls.Add(_buttonSetName, 2, row);
            row = ToolFormSupport.AddRow(neighborhood, _labelIcon, _comboIcon);
            _groupNeighborhood = NewGroup(pad, neighborhood);

            // Disc tray and fan
            _labelTray = ToolFormSupport.NewLabel(string.Empty);
            _labelFan = ToolFormSupport.NewLabel(string.Empty);
            _buttonTrayOpen = ToolFormSupport.NewButton(this, 6);
            _buttonTrayClose = ToolFormSupport.NewButton(this, 6);
            _trackFan = new TrackBar
            {
                Minimum = FanSpeedMinimum,
                Maximum = FanSpeedMaximum,
                TickFrequency = 10,
                SmallChange = 1,
                LargeChange = 10,
                Value = FanSpeedMinimum,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            _valueFan = NewValueLabel();
            _valueFan.Text = FanSpeedMinimum.ToString();
            _buttonSetFan = ToolFormSupport.NewButton(this, 8);

            TableLayoutPanel discFan = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            ToolFormSupport.AddRow(discFan, _labelTray, ToolFormSupport.FlowRow(_buttonTrayOpen, _buttonTrayClose));
            ToolFormSupport.AddRow(discFan, _labelFan, _trackFan);
            ToolFormSupport.AddRow(discFan, _valueFan, _buttonSetFan);
            _groupDiscFan = NewGroup(pad, discFan);

            // LEDs
            string[] colors = { "Green", "Orange", "Red", "Off" };
            _labelTopLeft = ToolFormSupport.NewLabel(string.Empty);
            _labelTopRight = ToolFormSupport.NewLabel(string.Empty);
            _labelBottomLeft = ToolFormSupport.NewLabel(string.Empty);
            _labelBottomRight = ToolFormSupport.NewLabel(string.Empty);
            // One row of four columns (Top Left, Top Right, Bottom Left, Bottom Right), each header above a vertical colour list, as the old designer lays it out.
            _radioTopLeft = new RadioGroupPanel(colors, 0) { FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _radioTopRight = new RadioGroupPanel(colors, 0) { FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _radioBottomLeft = new RadioGroupPanel(colors, 0) { FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _radioBottomRight = new RadioGroupPanel(colors, 0) { FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _buttonSetLeds = ToolFormSupport.NewButton(this, 8);
            _buttonSetLeds.Dock = DockStyle.Fill; // spans the four columns, as the old full-width button does

            TableLayoutPanel leds = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 25F), new ColumnStyle(SizeType.Percent, 25F), new ColumnStyle(SizeType.Percent, 25F), new ColumnStyle(SizeType.Percent, 25F));
            leds.RowCount = 2;
            leds.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leds.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leds.Controls.Add(_labelTopLeft, 0, 0);
            leds.Controls.Add(_labelTopRight, 1, 0);
            leds.Controls.Add(_labelBottomLeft, 2, 0);
            leds.Controls.Add(_labelBottomRight, 3, 0);
            leds.Controls.Add(_radioTopLeft, 0, 1);
            leds.Controls.Add(_radioTopRight, 1, 1);
            leds.Controls.Add(_radioBottomLeft, 2, 1);
            leds.Controls.Add(_radioBottomRight, 3, 1);
            ToolFormSupport.AddFullRow(leds, _buttonSetLeds, false);
            _groupLeds = NewGroup(pad, leds);

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            // Fixed row heights: group height + bottom margin (pad 10 at the 20 px font). Details 324, Neighborhood 152, Disc and fan 208, LEDs 246.
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupDetails, false), 334);
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupNeighborhood, false), 162);
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupDiscFan, false), 218);
            FixRowHeight(root, ToolFormSupport.AddFullRow(root, _groupLeds, false), 256);
            // Spare height goes to this filler row, not into a group row (as BackupFilesManager does).
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowCount = root.RowStyles.Count;

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _buttonRefresh.Click += ButtonRefresh_Click;
            _buttonSetName.Click += ButtonSetName_Click;
            _comboIcon.SelectionChangeCommitted += ComboIcon_SelectionChangeCommitted;
            _buttonTrayOpen.Click += delegate { SetTray(true); };
            _buttonTrayClose.Click += delegate { SetTray(false); };
            _trackFan.ValueChanged += delegate { _valueFan.Text = _trackFan.Value.ToString(); };
            _buttonSetFan.Click += ButtonSetFan_Click;
            _buttonSetLeds.Click += ButtonSetLeds_Click;

            ApplyLanguage();

            AutoScroll = true;
            // Fixed client size (no run-time measuring). Height 1040 = root rows 970 + root padding 20 + status bar about 30 (1020) + slack; width 840.
            ClientSize = new Size(840, 1040);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Console boundary. The default is a stub that fails with a message; see IXboxConsoleInfo.
        /// </summary>
        public IXboxConsoleInfo Console { get; set; } = UnavailableXboxConsoleAccess.Instance;

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
            _labelIp.Text = "IP Address:";
            _labelCpuKey.Text = "CPU Key:";
            _labelKernel.Text = "Kernel:";
            _labelTitleId.Text = "Title ID:";
            _labelTempCpu.Text = "CPU:";
            _labelTempGpu.Text = "GPU:";
            _labelTempRam.Text = "RAM:";
            _labelTempMb.Text = "Motherboard:";
            _buttonRefresh.Text = Text_("LABEL_REFRESH", "Refresh");

            _groupNeighborhood.Text = "Neighborhood Editor";
            _labelName.Text = "Console Name";
            _labelIcon.Text = "Icon Color";
            _buttonSetName.Text = Text_("LABEL_SET", "Set");

            _groupDiscFan.Text = "Disc Tray && Fan Speed";
            _labelTray.Text = "Disc Tray";
            _labelFan.Text = "Fan Speed";
            _buttonTrayOpen.Text = Text_("LABEL_OPEN", "Open");
            _buttonTrayClose.Text = Text_("LABEL_CLOSE", "Close");
            _buttonSetFan.Text = Text_("LABEL_SET_FAN_SPEED", "Set Fan Speed");

            _groupLeds.Text = "LED Colors";
            _labelTopLeft.Text = "Top Left:";
            _labelTopRight.Text = "Top Right:";
            _labelBottomLeft.Text = "Bottom Left:";
            _labelBottomRight.Text = "Bottom Right:";
            _buttonSetLeds.Text = Text_("LABEL_SET_LEDS", "Set LEDs");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
            LoadDetails();
        }

        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            LoadDetails();
        }

        private void LoadDetails()
        {
            try
            {
                _valueIp.Text = Console.IpAddress;
                _valueCpuKey.Text = Console.GetCpuKey();
                _valueKernel.Text = Console.GetKernelVersion();
                _valueTitleId.Text = Console.GetCurrentTitleId().ToString("X8");

                _valueTempCpu.Text = Console.GetTemperature(XboxTemperatureSensor.Cpu) + " °C";
                _valueTempGpu.Text = Console.GetTemperature(XboxTemperatureSensor.Gpu) + " °C";
                _valueTempRam.Text = Console.GetTemperature(XboxTemperatureSensor.MemoryEdram) + " °C";
                _valueTempMb.Text = Console.GetTemperature(XboxTemperatureSensor.Motherboard) + " °C";

                _textBoxName.Text = Console.Name;

                SetStatus("Successfully loaded console details.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to fetch console details.", ex);
            }
        }

        private void ButtonSetName_Click(object sender, EventArgs e)
        {
            try
            {
                Console.ChangeName(_textBoxName.Text);
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
                Console.ChangeIcon((XboxIconColor)_comboIcon.SelectedIndex);
                ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), "Changed Xbox Icon. Refresh Neighborhood to see the new icon.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to change the console icon.", ex);
            }
        }

        private void SetTray(bool open)
        {
            try
            {
                Console.SetTrayOpen(open);
                SetStatus(open ? "Disc tray opened." : "Disc tray closed.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to move the disc tray.", ex);
            }
        }

        private void ButtonSetFan_Click(object sender, EventArgs e)
        {
            try
            {
                Console.SetFanSpeed(0, _trackFan.Value);
                SetStatus("Fan speed set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the fan speed.", ex);
            }
        }

        private static XboxLedState LedState(RadioGroupPanel group)
        {
            int index = group.SelectedIndex;
            return index < 0 ? XboxLedState.Green : (XboxLedState)index;
        }

        private void ButtonSetLeds_Click(object sender, EventArgs e)
        {
            try
            {
                Console.SetLeds(LedState(_radioTopLeft), LedState(_radioTopRight), LedState(_radioBottomLeft), LedState(_radioBottomRight));
                SetStatus("Console LEDs colors set.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the console LEDs.", ex);
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
