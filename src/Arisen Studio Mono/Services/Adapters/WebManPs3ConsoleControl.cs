using System;
using System.Net;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Tools.PS3;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Managed PS3 console control over webMAN. Implements <see cref="IPs3ConsoleControl" /> with the helpers in
    /// <see cref="WebManExtensions" />. The constructor stores the address and does nothing else (no I/O).
    /// <para>
    /// Connect makes one read-only webMAN request (<see cref="WebManExtensions.GetGameAsync" />) and reports connected
    /// only when that request returns an answer. Nothing is sent until Connect is called, and never when
    /// <see cref="AllowNetwork" /> is false (the default, so smoke runs make no network call).
    /// </para>
    /// <para>
    /// Members with no webMAN equivalent of the old PS3MAPI call throw <see cref="NotSupportedException" />:
    /// temperatures, firmware type and version, core version, IDPS and PSID.
    /// webMAN's helpers swallow transport errors, so LED and buzzer calls cannot report a failed request.
    /// </para>
    /// </summary>
    public sealed class WebManPs3ConsoleControl : IPs3ConsoleControl
    {
        private readonly object _gate = new object();
        private readonly string _address;
        private string _connectedAddress;

        /// <param name="address">Console address (IP). Used by Connect when it is given no address.</param>
        public WebManPs3ConsoleControl(string address)
        {
            _address = address;
        }

        /// <summary>
        /// When false (the default), Connect throws without making any network call. The host sets it to true for
        /// normal runs only.
        /// </summary>
        public bool AllowNetwork { get; set; }

        public bool IsConnected
        {
            get { lock (_gate) { return _connectedAddress != null; } }
        }

        /// <summary>
        /// Probes webMAN with <see cref="WebManExtensions.GetGameAsync" />. Throws when the console does not answer.
        /// Blocks the calling thread; the form calls it from Task.Run.
        /// </summary>
        public void Connect(string address)
        {
            if (!AllowNetwork)
            {
                throw new InvalidOperationException("PS3 console control is off in this run. No connection was attempted.");
            }

            string target = string.IsNullOrWhiteSpace(address) ? _address : address;
            if (string.IsNullOrWhiteSpace(target) || !IPAddress.TryParse(target, out _))
            {
                throw new ArgumentException("The console address must be an IP address.", nameof(address));
            }

            string answer;
            try
            {
                answer = WebManExtensions.GetGameAsync(target).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new InvalidOperationException("Unable to reach webMAN at " + target + ". " + ex.Message, ex);
            }

            if (string.IsNullOrEmpty(answer))
            {
                Disconnect();
                throw new InvalidOperationException("Unable to reach webMAN at " + target + ". The console gave no answer.");
            }

            lock (_gate)
            {
                _connectedAddress = target;
            }
        }

        /// <summary>
        /// Forgets the address. Nothing is sent to the console: webMAN keeps no session.
        /// </summary>
        public void Disconnect()
        {
            lock (_gate)
            {
                _connectedAddress = null;
            }
        }

        public void GetTemperatures(out uint cellCelsius, out uint rsxCelsius)
        {
            throw NotAvailable("Temperatures are not available over webMAN. The old Console Manager read them through PS3MAPI, and no webMAN helper exists for them.");
        }

        public string GetFirmwareVersion()
        {
            throw NotAvailable("The firmware version is not available over webMAN. The old Console Manager read it through PS3MAPI, and no webMAN helper exists for it.");
        }

        public string GetFirmwareType()
        {
            throw NotAvailable("The firmware type is not available over webMAN. The old Console Manager read it through PS3MAPI, and no webMAN helper exists for it.");
        }

        public string GetCoreVersion()
        {
            throw NotAvailable("The core version is not available over webMAN. The old Console Manager read it through PS3MAPI, and no webMAN helper exists for it.");
        }

        public void SetConsoleLed(Ps3LedColor color, Ps3LedMode mode)
        {
            string ip = RequireConnected();
            WebManExtensions.LedColor webColor = MapLedColor(color);
            WebManExtensions.LedMode webMode = MapLedMode(mode);

            try
            {
                // Request: http://{ip}/led.ps3mapi?color={Red|Green}&mode={Off|On|BlinkFast|BlinkSlow}
                WebManExtensions.SetConsoleLed(ip, webColor, webMode);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to set the console LED through webMAN. " + ex.Message, ex);
            }
        }

        public void RingBuzzer(Ps3BuzzerMode mode)
        {
            string ip = RequireConnected();
            WebManExtensions.BuzzerMode webMode = MapBuzzerMode(mode);

            try
            {
                // Request: http://{ip}/buzzer.ps3mapi?mode={Single|Double|Triple}
                WebManExtensions.RingBuzzer(ip, webMode);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to ring the console buzzer through webMAN. " + ex.Message, ex);
            }
        }

        public void SetConsoleId(string idps)
        {
            throw NotAvailable("Setting the IDPS is not available over webMAN. The old Console Manager set it through PS3MAPI (SetConsoleID), and the webMAN setidps helpers use a different argument split that the old app never used.");
        }

        public void SetPsid(string psid)
        {
            throw NotAvailable("Setting the PSID is not available over webMAN. The old Console Manager set it through PS3MAPI (SetPSID), and the webMAN setidps helpers use a different argument split that the old app never used.");
        }

        private string RequireConnected()
        {
            lock (_gate)
            {
                if (_connectedAddress == null)
                {
                    throw new InvalidOperationException("Not connected to a console.");
                }

                return _connectedAddress;
            }
        }

        private static NotSupportedException NotAvailable(string message)
        {
            return new NotSupportedException(message);
        }

        private static WebManExtensions.LedColor MapLedColor(Ps3LedColor color)
        {
            switch (color)
            {
                case Ps3LedColor.Red:
                    return WebManExtensions.LedColor.Red;
                case Ps3LedColor.Green:
                    return WebManExtensions.LedColor.Green;
                default:
                    throw new ArgumentOutOfRangeException(nameof(color));
            }
        }

        private static WebManExtensions.LedMode MapLedMode(Ps3LedMode mode)
        {
            switch (mode)
            {
                case Ps3LedMode.Off:
                    return WebManExtensions.LedMode.Off;
                case Ps3LedMode.On:
                    return WebManExtensions.LedMode.On;
                case Ps3LedMode.BlinkFast:
                    return WebManExtensions.LedMode.BlinkFast;
                case Ps3LedMode.BlinkSlow:
                    return WebManExtensions.LedMode.BlinkSlow;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private static WebManExtensions.BuzzerMode MapBuzzerMode(Ps3BuzzerMode mode)
        {
            switch (mode)
            {
                case Ps3BuzzerMode.Single:
                    return WebManExtensions.BuzzerMode.Single;
                case Ps3BuzzerMode.Double:
                    return WebManExtensions.BuzzerMode.Double;
                case Ps3BuzzerMode.Triple:
                    return WebManExtensions.BuzzerMode.Triple;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}
