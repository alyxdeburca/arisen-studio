using System;
using System.Drawing;
using System.Linq;
using System.Resources;
using System.Text;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools.XBOX
{
    /// <summary>
    /// Looks up the XUID of a gamertag on a connected Xbox 360 (XUserFindUserByGamertag in the console), and writes a gamertag
    /// and XUID into the memory of the running game (for the supported Call of Duty titles).
    /// Stock WinForms port of the old DevExpress XuidGameSpoofer form. The console calls (JRPC function call, memory read
    /// and write) are not ported; they sit behind IXboxMemoryAccess. The addresses and the title list are the old ones, unchanged.
    /// Host hook, set before the form is shown:
    ///  - Memory: the console boundary (default: a stub that is never connected, so the form shows the "must be connected" message).
    /// </summary>
    public class XuidGameSpoofer : Form
    {
        // Address of XUserFindUserByGamertag in the kernel (the old code left "17502 0x81829018" beside it for another build) and a scratch buffer.
        private const uint XUserFindUserAddress = 0x81829158;
        private const uint XamFreeMemory = 0x81AA2000;

        // Extra addresses written for Black Ops II only.
        private const uint Bo2ReversedGamertagAddress = 0x81AA2C8C;
        private const uint Bo2XuidCheckAddress = 0x825DE218;

        private enum XuidAddr : uint
        {
            Cod4 = 0x84C24BBC,
            Waw = 0x852336B5,
            Mw2 = 0x838BA824,
            Bo1 = 0x841987D5,
            Mw3 = 0x839691AC,
            Bo2 = 0x841E1B30,
            Ghosts = 0x83F0A35C,
            Aw = 0x84493A94
        }

        public enum Title : uint
        {
            Dashboard = 0xFFFE07D1,
            XexMenu = 0xFFFF0055,
            XexMenuAlt = 0xC0DE9999,
            Xshell = 0xFFFE07FF,
            GtaV = 0x545408A7,
            Cod4 = 0x415607E6,
            Mw2 = 0x41560817,
            Bo1 = 0x41560855,
            Mw3 = 0x415608CB,
            Bo2 = 0x415608C3,
            Ghosts = 0x415608FC,
            Aw = 0x41560914,
            Bo3 = 0x4156091D
        }

        private enum AddressStatus
        {
            Found,
            NotConnected,
            GameNotSupported,
            Failed
        }

        private readonly Label _labelGamertag;
        private readonly Label _labelXuid;
        private readonly TextBox _textBoxGamertag;
        private readonly TextBox _textBoxXuid;
        private readonly Button _buttonGetXuid;
        private readonly Button _buttonSetXuid;
        private readonly ToolStripStatusLabel _status;

        public XuidGameSpoofer()
        {
            ToolFormSupport.ConfigureForm(this, false);

            int h = Font.Height;
            int pad = h / 2;

            _labelGamertag = ToolFormSupport.NewLabel(string.Empty);
            _labelXuid = ToolFormSupport.NewLabel(string.Empty);
            _textBoxGamertag = new TextBox { Dock = DockStyle.Fill, MaxLength = 15, Margin = new Padding(0, 0, 0, pad / 2) };
            _textBoxXuid = new TextBox { Dock = DockStyle.Fill, MaxLength = 16, Margin = new Padding(0, 0, 0, pad / 2) };
            _buttonGetXuid = ToolFormSupport.NewButton(this, 8);
            _buttonSetXuid = ToolFormSupport.NewButton(this, 8);
            _buttonSetXuid.Margin = Padding.Empty;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddRow(root, _labelGamertag, _textBoxGamertag);
            ToolFormSupport.AddRow(root, _labelXuid, _textBoxXuid);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonGetXuid, _buttonSetXuid), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            _buttonGetXuid.Click += ButtonGetXuid_Click;
            _buttonSetXuid.Click += ButtonSetXuid_Click;

            ApplyLanguage();

            ClientSize = new Size(h * 36, h * 12);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Console boundary used to call functions and read and write memory.
        /// </summary>
        public IXboxMemoryAccess Memory { get; set; } = UnavailableXboxConsoleAccess.Instance;

        private string Text_(string key, string fallback)
        {
            return ToolFormSupport.Lookup(Language, key, fallback);
        }

        private void ApplyLanguage()
        {
            // The old form set its title from the games launcher text; this uses its own name.
            Text = Text_("XUID_GAME_SPOOFER", "XUID Game Spoofer");
            _labelGamertag.Text = "Gamertag";
            _labelXuid.Text = "XUID";
            _buttonGetXuid.Text = Text_("LABEL_GET_XUID", "Get XUID");
            _buttonSetXuid.Text = Text_("LABEL_SET_XUID", "Set XUID");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        private void ButtonGetXuid_Click(object sender, EventArgs e)
        {
            string gamertag = _textBoxGamertag.Text;

            // The old form accepted 5 to 15 characters here; kept as it was.
            if (gamertag.Length <= 4 || gamertag.Length > 15)
            {
                return;
            }

            try
            {
                if (!Memory.IsConnected)
                {
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("YOU_MUST_BE_CONNECTED_TO_USE_FEATURE", "You must be connected to a console to use this feature."));
                    return;
                }

                string xuid = GetXuid(gamertag);

                if (string.IsNullOrEmpty(xuid))
                {
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("XUID_GET_ERROR", "Unable to get the XUID of this gamertag."));
                    return;
                }

                _textBoxXuid.Text = xuid;
                SetStatus("Got the XUID.");
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to get the XUID.", ex);
            }
        }

        private void ButtonSetXuid_Click(object sender, EventArgs e)
        {
            try
            {
                if (SetXuid(_textBoxGamertag.Text, _textBoxXuid.Text))
                {
                    SetStatus("XUID set.");
                    ToolFormSupport.ShowInfo(this, Text_("SUCCESS", "Success"), Text_("XUID_SET_SUCCESS", "The XUID was set."));
                }
                else
                {
                    ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("XUID_SET_ERROR", "Unable to set the XUID."));
                }
            }
            catch (Exception ex)
            {
                ReportFailure("Unable to set the XUID.", ex);
            }
        }

        /// <summary>
        /// Ask the console for the XUID of a gamertag. Returns 16 hex characters, or an empty string when the call failed.
        /// </summary>
        private string GetXuid(string gamertag)
        {
            uint result = Memory.CallUInt32(XUserFindUserAddress, 0x9000006F93463L, 0, gamertag, 0x18, XamFreeMemory, 0);

            if (result != 0)
            {
                return string.Empty;
            }

            return BitConverter.ToString(Memory.ReadBytes(XamFreeMemory, 8)).Replace("-", string.Empty);
        }

        private AddressStatus FindXuidAddress(out uint address, out uint titleId)
        {
            address = 0;
            titleId = 0;

            if (!Memory.IsConnected)
            {
                return AddressStatus.NotConnected;
            }

            titleId = Memory.GetCurrentTitleId();

            switch (titleId)
            {
                case (uint)Title.Cod4:
                    address = (uint)XuidAddr.Cod4;
                    break;
                case (uint)Title.Mw2:
                    address = (uint)XuidAddr.Mw2;
                    break;
                case (uint)Title.Bo1:
                    address = (uint)XuidAddr.Bo1;
                    break;
                case (uint)Title.Mw3:
                    address = (uint)XuidAddr.Mw3;
                    break;
                case (uint)Title.Bo2:
                    address = (uint)XuidAddr.Bo2;
                    break;
                case (uint)Title.Ghosts:
                    address = (uint)XuidAddr.Ghosts;
                    break;
                case (uint)Title.Aw:
                    address = (uint)XuidAddr.Aw;
                    break;
                default:
                    return AddressStatus.GameNotSupported;
            }

            return AddressStatus.Found;
        }

        /// <summary>
        /// Write the gamertag and XUID into the running game. Shows the reason and returns false when it cannot.
        /// Nothing is written unless the XUID is 16 hex characters (the old code could write the gamertag and then stop).
        /// </summary>
        private bool SetXuid(string gamertag, string xuid)
        {
            uint address;
            uint titleId;
            AddressStatus status = FindXuidAddress(out address, out titleId);

            if (status == AddressStatus.NotConnected)
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("YOU_MUST_BE_CONNECTED_TO_USE_FEATURE", "You must be connected to a console to use this feature."));
                return false;
            }

            if (status == AddressStatus.GameNotSupported)
            {
                ToolFormSupport.ShowError(this, Text_("ERROR", "Error"), Text_("GAME_NOT_SUPPORTED", "The game that is running is not supported."));
                return false;
            }

            byte[] xuidBytes = xuid.Length == 16 ? ToolFormSupport.TryHexToBytes(xuid) : null;

            if (xuidBytes == null)
            {
                return false;
            }

            bool bo2 = titleId == (uint)Title.Bo2;

            if (gamertag.Length > 0)
            {
                if (bo2)
                {
                    Memory.WriteBytes(Bo2ReversedGamertagAddress, Encoding.UTF8.GetBytes(gamertag + "\0").Reverse().ToArray());
                }

                Memory.WriteBytes(address, Encoding.UTF8.GetBytes(gamertag + "\0"));
            }

            Memory.WriteBytes(address + (uint)(bo2 ? 0x20 : 0x24), xuidBytes);
            Memory.WriteBytes(address + (uint)(bo2 ? 0x28 : 0x2C), Encoding.ASCII.GetBytes(xuid));

            if (bo2)
            {
                Memory.WriteBytes(Bo2XuidCheckAddress, new byte[] { 0x48, 0x00, 0x00, 0x00 }); // XUID check
            }

            return true;
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
