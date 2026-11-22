using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Shell;
using ArisenStudio.Shell.Wiring;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// webMAN-only console connection. Implements <see cref="IConsoleConnection" /> by calling the existing
    /// <see cref="WebManExtensions" /> helpers. The constructor opens no socket; a request is sent only when a
    /// method is called. Not compiled, not wired into ShellServices.
    /// FTP (the old file manager connection) is not part of this class: <see cref="FileService" /> is null.
    /// </summary>
    internal sealed class WebManConsoleConnection : IConsoleConnection
    {
        private readonly string _hostAddress;
        private readonly SynchronizationContext _uiContext;
        private readonly object _gate = new object();
        private ConsoleProfile _profile;
        private bool _webManInstalled;

        /// <param name="hostAddress">
        /// Fallback console address, used by <see cref="ConnectAsync" /> only when the profile has no address.
        /// </param>
        public WebManConsoleConnection(string hostAddress)
        {
            _hostAddress = hostAddress;
            // Captured here (no I/O) so that Changed and status updates can be raised on the UI thread.
            _uiContext = SynchronizationContext.Current;
        }

        public bool IsConnected
        {
            get { lock (_gate) { return _profile != null; } }
        }

        public ConsoleProfile Profile
        {
            get { lock (_gate) { return _profile; } }
        }

        public bool WebManInstalled
        {
            get { lock (_gate) { return _profile != null && _webManInstalled; } }
        }

        /// <summary>Not populated: webMAN's current-game check (GetGameAsync) is async and this property is sync.</summary>
        public string CurrentGameName
        {
            get { return null; }
        }

        /// <summary>Always null: the FTP-based console file service is not part of this class.</summary>
        public IConsoleFileService FileService
        {
            get { return null; }
        }

        public event EventHandler Changed;

        /// <summary>
        /// Checks webMAN with <see cref="WebManExtensions.IsWebManInstalled" />, the same call the old
        /// MainWindow made after connecting. Returns true when the check ran; WebManInstalled holds its result.
        /// </summary>
        public async Task<bool> ConnectAsync(ConsoleProfile profile)
        {
            try
            {
                if (profile == null)
                {
                    ReportOnUi("No console profile is set.", null);
                    return false;
                }

                string address = string.IsNullOrWhiteSpace(profile.Address) ? _hostAddress : profile.Address;
                if (string.IsNullOrWhiteSpace(address))
                {
                    ReportOnUi("No console address is set.", null);
                    return false;
                }

                bool installed = await Task.Run(() => WebManExtensions.IsWebManInstalled(address)).ConfigureAwait(false);

                lock (_gate)
                {
                    _profile = profile;
                    _webManInstalled = installed;
                }

                RaiseChangedOnUi();
                return true;
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to connect to the console through webMAN. Error: " + ex.Message);
                ReportOnUi("Could not connect to the console: " + ex.Message, ex);
                return false;
            }
        }

        /// <summary>Clears the connected profile. Sends nothing to the console.</summary>
        public void Disconnect()
        {
            try
            {
                lock (_gate)
                {
                    _profile = null;
                    _webManInstalled = false;
                }

                RaiseChangedOnUi();
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to disconnect the webMAN console connection. Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Maps the webMAN ribbon ids to the existing helpers and sends the request on a worker thread. Returns true
        /// when the request was sent. The helpers do not return a response or throw, so true does not confirm the
        /// console acted on it. Returns false for ids that are not webMAN commands and when not connected.
        /// </summary>
        public async Task<bool> RunCommandAsync(string commandId, string argument)
        {
            try
            {
                ConsoleProfile profile;
                lock (_gate)
                {
                    profile = _profile;
                }

                if (profile == null)
                {
                    ReportOnUi("webMAN Commands - No console profile is set.", null);
                    return false;
                }

                string address = profile.Address;
                Action send;

                switch (commandId)
                {
                    case RibbonCommandIds.ToolsPsPowerShutdown:
                        send = () => WebManExtensions.Power(address, WebManExtensions.PowerFlags.Shutdown);
                        break;
                    case RibbonCommandIds.ToolsPsPowerRestart:
                        send = () => WebManExtensions.Power(address, WebManExtensions.PowerFlags.Restart);
                        break;
                    case RibbonCommandIds.ToolsPsPowerSoftReboot:
                        send = () => WebManExtensions.Power(address, WebManExtensions.PowerFlags.SoftReboot);
                        break;
                    case RibbonCommandIds.ToolsPsPowerHardReboot:
                        send = () => WebManExtensions.Power(address, WebManExtensions.PowerFlags.HardReboot);
                        break;
                    case RibbonCommandIds.ToolsPsPowerQuickReboot:
                        send = () => WebManExtensions.Power(address, WebManExtensions.PowerFlags.QuickReboot);
                        break;
                    case RibbonCommandIds.ToolsPsGamesUnmount:
                        send = () => WebManExtensions.Unmount(address);
                        break;
                    case RibbonCommandIds.ToolsPsGamesLaunchGame:
                        send = () => WebManExtensions.LaunchGame(address);
                        break;
                    case RibbonCommandIds.ToolsPsGamesMountBd:
                    case RibbonCommandIds.ToolsPsGamesMountIso:
                    case RibbonCommandIds.ToolsPsGamesMountPsn:
                        // The argument is the game path chosen by the caller (ShellController.MountGameAsync).
                        if (string.IsNullOrEmpty(argument))
                        {
                            return false;
                        }
                        send = () => WebManExtensions.MountGameFromPath(address, argument);
                        break;
                    case RibbonCommandIds.ToolsPsNotifyMessage:
                        if (string.IsNullOrEmpty(argument))
                        {
                            return false;
                        }
                        send = () => WebManExtensions.NotifyPopup(address, argument);
                        break;
                    default:
                        // Not a webMAN helper here: OpenGUI, VirtualController, Xbox ids.
                        return false;
                }

                await Task.Run(send).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "Unable to run the webMAN command " + commandId + ". Error: " + ex.Message);
                ReportOnUi("webMAN Commands - Failed: " + ex.Message, ex);
                return false;
            }
        }

        /// <summary>Xbox scan is not part of webMAN. Always empty.</summary>
        public IEnumerable<ConsoleProfile> ScanXboxConsoles()
        {
            return new ConsoleProfile[0];
        }

        private void RaiseChangedOnUi()
        {
            RunOnUi(() =>
            {
                EventHandler handler = Changed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            });
        }

        private void ReportOnUi(string status, Exception exception)
        {
            RunOnUi(() => AppHost.SetStatus(status, exception));
        }

        private void RunOnUi(Action action)
        {
            SynchronizationContext context = _uiContext;
            if (context == null || SynchronizationContext.Current == context)
            {
                InvokeSafely(action);
                return;
            }

            context.Post(_ => InvokeSafely(action), null);
        }

        private static void InvokeSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppHost.Logger?.Invoke(ex, "webMAN console connection UI callback failed. Error: " + ex.Message);
            }
        }
    }
}
