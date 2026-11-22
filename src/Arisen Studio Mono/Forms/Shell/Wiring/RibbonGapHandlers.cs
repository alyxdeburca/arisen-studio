using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ArisenStudio.Constants;
using ArisenStudio.Extensions;
using ArisenStudio.Forms.Dialogs;
using ArisenStudio.Forms.Pages;
using ArisenStudio.Io;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Shell.Wiring
{
    /// <summary>
    /// Handlers for ribbon command ids that ShellController does not handle today (ribbon gap pass, request 45r).
    /// Not registered in the csproj and not called from anywhere yet. Not compiled by the request that wrote it.
    ///
    /// Call site (when wired): in ShellController.HandleRibbonAsync, before the DialogLauncher.TryShowTool(...) call
    /// and before the IsConsoleCommand(id) branch. Pass the form, the dialog context and () => _connection.Disconnect().
    /// After the Setup Wizard returns, the caller must run ApplyConnectionState(false) (private to ShellController).
    ///
    /// Return value: true means the id was handled here, including the "no console profile" case (a status is set).
    /// false means the id is not one of this class's ids, or a needed DialogContext is null. The ShellForm ids here
    /// (HeaderDonate, SendFeedback, HowToGuides, SetupWizard, RequestMods, AdvancedSettings, About, OpenLogFile,
    /// OpenLogFolder) are handled by ShellForm.OnRibbonCommand today and never reach ShellController.
    /// Gap ids (Mount BD/ISO/PSN, Xbox power and notify) are not here. Deviations are listed in BUILD-REQUEST-45r.md.
    /// Request ribbonwire removed the 9 webMAN-side ids from this switch. ShellController owns them through
    /// IsConsoleCommand and IConsoleConnection.RunCommandAsync. RunWebMan, NotifyPs3 and ConnectedProfile are kept but unreferenced.
    /// </summary>
    internal static class RibbonGapHandlers
    {
        private const string VirtualControllerUrl = "http://pad.aldostools.org/pad.html";

        private const string HowToGuidesText = "Our How-To-Guides have been moved to our website at https://arisen.studio/guide\n\nWould you like to open the link in your browser?.";

        private const string NoProfileText = "webMAN Commands - No console profile is set.";

        /// <summary>
        /// Handles one ribbon command id. Returns false when the id is not handled here.
        /// </summary>
        public static bool TryHandle(string commandId, ShellForm form, DialogContext dialogs, Action disconnect)
        {
            if (form == null)
            {
                throw new ArgumentNullException("form");
            }

            switch (commandId)
            {
                case RibbonCommandIds.HeaderDonate:
                    OpenExternal(form, Urls.DonatePatreon);
                    return true;

                case RibbonCommandIds.SendFeedback:
                    OpenExternal(form, Urls.GitHubRepo + "issues/new/choose");
                    return true;

                case RibbonCommandIds.HowToGuides:
                    if (MessageBox.Show(form, HowToGuidesText, "How-To Guides", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        OpenExternal(form, Urls.ProjectWebsite);
                    }
                    return true;

                case RibbonCommandIds.SetupWizard:
                    if (dialogs == null)
                    {
                        return false;
                    }
                    DialogLauncher.ShowSetupWizard(form, dialogs, null);
                    return true;

                case RibbonCommandIds.RequestMods:
                    using (RequestModsDialog dialog = new RequestModsDialog())
                    {
                        _ = dialog.ShowDialog(form);
                    }
                    return true;

                case RibbonCommandIds.AdvancedSettings:
                    _ = form.ShowPage(ShellPageKeys.Settings);
                    return true;

                case RibbonCommandIds.About:
                    _ = form.ShowPage(ShellPageKeys.About);
                    return true;

                case RibbonCommandIds.OpenLogFile:
                    OpenLogFile(form);
                    return true;

                case RibbonCommandIds.OpenLogFolder:
                    OpenLogFolder(form);
                    return true;

                default:
                    return false;
            }
        }

        // ====================================================================================================
        // Helpers
        // ====================================================================================================

        /// <summary>The console profile the old ConsoleProfile property meant: the connected one, else the default one.</summary>
        private static ConsoleProfile ConnectedProfile(DialogContext dialogs)
        {
            return dialogs.ConnectedProfile == null ? null : dialogs.ConnectedProfile();
        }

        /// <summary>
        /// Runs one webMAN HTTP call against the console address, with the old status texts. On success runs
        /// afterSuccess (the power commands disconnect here, as the old code did).
        /// </summary>
        private static bool RunWebMan(ShellForm form, DialogContext dialogs, string busy, string done, Action<string> call, Action afterSuccess)
        {
            if (dialogs == null)
            {
                return false;
            }
            ConsoleProfile profile = ConnectedProfile(dialogs);
            if (profile == null)
            {
                form.SetStatusText(NoProfileText);
                return true;
            }

            form.SetStatusText("webMAN Commands - " + busy);
            try
            {
                call(profile.Address);
            }
            catch (Exception ex)
            {
                form.SetStatusText("webMAN Commands - Failed: " + ex.Message);
                return true;
            }

            form.SetStatusText("webMAN Commands - " + done);
            if (afterSuccess != null)
            {
                afterSuccess();
            }
            return true;
        }

        /// <summary>Old ButtonToolsPsNotifyMessage. An empty message cancels (the old code sent it anyway, MW1-069).</summary>
        private static bool NotifyPs3(ShellForm form, DialogContext dialogs)
        {
            if (dialogs == null)
            {
                return false;
            }
            ConsoleProfile profile = ConnectedProfile(dialogs);
            if (profile == null)
            {
                form.SetStatusText(NoProfileText);
                return true;
            }

            string message = TextPromptForm.Show(form, "Notify Message", "Message:", string.Empty);
            if (string.IsNullOrWhiteSpace(message))
            {
                form.SetStatusText("No message was entered. Cancelled.");
                return true;
            }

            form.SetStatusText("webMAN Commands - Notifying Message...");
            try
            {
                WebManExtensions.NotifyPopup(profile.Address, message);
            }
            catch (Exception ex)
            {
                form.SetStatusText("webMAN Commands - Failed: " + ex.Message);
                return true;
            }
            form.SetStatusText("webMAN Commands - Message Notified: " + message);
            return true;
        }

        /// <summary>Opens a target with the shell. Sets the status text and returns false on failure.</summary>
        private static bool OpenExternal(ShellForm form, string target)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                form.SetStatusText("Could not open " + target + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>Old ButtonOpenLogFile_ItemClick.</summary>
        private static void OpenLogFile(ShellForm form)
        {
            form.SetStatusText("Opening Log file...");
            string logFile = Path.Combine(UserFolders.Logs, "latest.log");

            if (!File.Exists(logFile))
            {
                form.SetStatusText("Failed to find Log file...");
                MessageBox.Show(form, "Failed to find the log file.\nIt may have been deleted, that's ok.", "No Log File.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                _ = Process.Start(new ProcessStartInfo(logFile) { UseShellExecute = true });
            }
            catch (Exception)
            {
                form.SetStatusText("Failed to open Log file...");
                MessageBox.Show(form, "Failed to open the log file.\nIt may have been deleted, that's ok.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Old ButtonOpenLogFolder_ItemClick. Creates the folder if it is missing.</summary>
        private static void OpenLogFolder(ShellForm form)
        {
            form.SetStatusText("Opening Log folder...");
            try
            {
                if (!Directory.Exists(UserFolders.Logs))
                {
                    _ = Directory.CreateDirectory(UserFolders.Logs);
                }
            }
            catch (Exception ex)
            {
                form.SetStatusText("Could not create the log folder: " + ex.Message);
                return;
            }
            _ = OpenExternal(form, UserFolders.Logs);
        }
    }
}
