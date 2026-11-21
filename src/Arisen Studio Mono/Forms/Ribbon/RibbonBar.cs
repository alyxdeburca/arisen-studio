using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ArisenStudio.Controls;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// The ribbon replacement (S6): fills an ActionStrip with the commands of the old Home and Tools ribbon pages and the
    /// caption bar, using stock ToolStrip buttons and drop-down menus. Each old ribbon group becomes one drop-down on the
    /// strip, except Connection, whose two commands are plain buttons. Items carry the old control name as their Name
    /// (RibbonCommandIds). A click raises CommandInvoked; this class does nothing else with a command. UI thread only.
    /// Placeholder: no icons (the old ribbon had large icons; they are left for the design pass, D7/D8).
    /// </summary>
    internal sealed class RibbonBar
    {
        private readonly ActionStrip _strip;
        private readonly Dictionary<string, ToolStripItem> _items = new Dictionary<string, ToolStripItem>();
        private readonly ToolStripDropDownButton _menuModdingTools;
        private readonly ToolStripDropDownButton _menuWebMan;
        private readonly ToolStripDropDownButton _menuXbdm;
        private readonly ToolStripMenuItem[] _psTools;
        private readonly ToolStripMenuItem[] _xboxTools;

        public RibbonBar(ActionStrip strip)
        {
            if (strip == null)
            {
                throw new ArgumentNullException("strip");
            }
            _strip = strip;

            // Home > Connection
            AddButton(RibbonCommandIds.ConnectConsole, "Connect Console", ActionAlign.Left);
            ToolStripButton disconnect = AddButton(RibbonCommandIds.DisconnectConsole, "Disconnect Console", ActionAlign.Left);
            disconnect.Enabled = false; // the designer default (Enabled = false); SetConnected changes it
            _ = _strip.Items.Add(new ToolStripSeparator());

            // Home > Console Profiles
            ToolStripDropDownButton profiles = AddMenu(RibbonCommandIds.MenuConsoleProfiles, "Console Profiles");
            _ = AddItem(profiles, RibbonCommandIds.AddNewProfile, "Add New Profile", false);
            _ = AddItem(profiles, RibbonCommandIds.ScanXboxConsoles, "Scan Xbox Consoles", false);
            _ = AddItem(profiles, RibbonCommandIds.EditProfiles, "Edit Profiles", false);
            _ = AddItem(profiles, RibbonCommandIds.ChangeProfile, "Change Profile", false);

            // Home > Quick Settings
            ToolStripDropDownButton quick = AddMenu(RibbonCommandIds.MenuQuickSettings, "Quick Settings");
            _ = AddItem(quick, RibbonCommandIds.HowToGuides, "How-To Guides", false);
            _ = AddItem(quick, RibbonCommandIds.SetupWizard, "Setup Wizard", false);
            _ = AddItem(quick, RibbonCommandIds.DownloadsFolder, "Downloads Folder", false);
            _ = AddItem(quick, RibbonCommandIds.DiscordPresence, "Discord Presence", true);
            _ = AddItem(quick, RibbonCommandIds.InstallModsToUsb, "Install Mods to USB", true);
            _ = AddItem(quick, RibbonCommandIds.AdvancedSettings, "Advanced Settings", false);

            // Tools > Modding Tools
            _menuModdingTools = AddMenu(RibbonCommandIds.MenuModdingTools, "Modding Tools");
            ToolStripMenuItem psUpdates = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsGameUpdates, "Game Updates", false);
            ToolStripMenuItem psBackup = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsBackupFilesManager, "Backup Files Managers", false);
            ToolStripMenuItem psPackage = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsPackageManager, "Package Manager", false);
            ToolStripMenuItem psConsole = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsConsoleManager, "Console Manager", false);
            ToolStripMenuItem psRegions = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsGameRegions, "Game Regions", false);
            ToolStripMenuItem psBoot = AddItem(_menuModdingTools, RibbonCommandIds.ToolsPsBootPluginsEditor, "Boot Plugins Editor", false);
            ToolStripMenuItem xResigner = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxGameSaveResigner, "Game Save Resigner", false);
            ToolStripMenuItem xHdKey = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxHdKey, "Xbox HD Key", false);
            ToolStripMenuItem xLauncher = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxGamesLauncher, "Games Launcher", false);
            ToolStripMenuItem xModules = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxModuleLoader, "Module Loader", false);
            ToolStripMenuItem xXuid = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxXuidSpoofer, "XUID Spoofer", false);
            ToolStripMenuItem xDash = AddItem(_menuModdingTools, RibbonCommandIds.ToolsXboxDashlaunchEditor, "Dashlaunch Editor", false);
            _psTools = new[] { psUpdates, psBackup, psPackage, psConsole, psRegions, psBoot };
            _xboxTools = new[] { xResigner, xHdKey, xLauncher, xModules, xXuid, xDash };

            // Tools > webMAN Commands
            _menuWebMan = AddMenu(RibbonCommandIds.MenuWebManCommands, "webMAN Commands");
            ToolStripMenuItem psPower = AddSubMenu(_menuWebMan, RibbonCommandIds.ToolsPsPower, "Power");
            _ = AddItem(psPower, RibbonCommandIds.ToolsPsPowerShutdown, "Shutdown", false);
            _ = AddItem(psPower, RibbonCommandIds.ToolsPsPowerRestart, "Restart", false);
            _ = psPower.DropDownItems.Add(new ToolStripSeparator()); // the old menu starts a group before Soft Reboot
            _ = AddItem(psPower, RibbonCommandIds.ToolsPsPowerSoftReboot, "Soft Reboot", false);
            _ = AddItem(psPower, RibbonCommandIds.ToolsPsPowerHardReboot, "Hard Reboot", false);
            _ = AddItem(psPower, RibbonCommandIds.ToolsPsPowerQuickReboot, "Quick Reboot", false);
            ToolStripMenuItem psGames = AddSubMenu(_menuWebMan, RibbonCommandIds.ToolsPsGames, "Games");
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesOpenGui, "Launcher (GUI)", false);
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesMountBd, "Mount BD", false);
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesMountIso, "Mount ISO", false);
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesMountPsn, "Mount PSN", false);
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesUnmount, "Unmount", false);
            _ = AddItem(psGames, RibbonCommandIds.ToolsPsGamesLaunchGame, "Launch", false);
            // The old caption was misspelt "Sceenshot Tool".
            _ = AddItem(_menuWebMan, RibbonCommandIds.ToolsPsScreenshot, "Screenshot Tool", false);
            _ = AddItem(_menuWebMan, RibbonCommandIds.ToolsPsNotifyMessage, "Notify Message", false);
            _ = AddItem(_menuWebMan, RibbonCommandIds.ToolsPsVirtualController, "Virtual Controller", false);

            // Tools > XBDM Commands
            _menuXbdm = AddMenu(RibbonCommandIds.MenuXbdmCommands, "XBDM Commands");
            ToolStripMenuItem xPower = AddSubMenu(_menuXbdm, RibbonCommandIds.ToolsXboxPower, "Power");
            _ = AddItem(xPower, RibbonCommandIds.ToolsXboxPowerShutdown, "Shutdown", false);
            _ = AddItem(xPower, RibbonCommandIds.ToolsXboxPowerSoftReboot, "Soft Reboot", false);
            _ = AddItem(xPower, RibbonCommandIds.ToolsXboxPowerHardReboot, "Hard Reboot", false);
            _ = AddItem(_menuXbdm, RibbonCommandIds.ToolsXboxTakeScreenshot, "Take Screenshot", false);
            _ = AddItem(_menuXbdm, RibbonCommandIds.ToolsXboxNeighborhoodEditor, "Neighborhood Editor", false);
            _ = AddItem(_menuXbdm, RibbonCommandIds.ToolsXboxNotifyMessage, "Notify Message", false);

            // Home > Help
            ToolStripDropDownButton help = AddMenu(RibbonCommandIds.MenuHelp, "Help");
            _ = AddItem(help, RibbonCommandIds.About, "About", false);
            _ = AddItem(help, RibbonCommandIds.OpenLogFile, "Open Log File", false);
            _ = AddItem(help, RibbonCommandIds.OpenLogFolder, "Open Log Folder", false);
            _ = AddItem(help, RibbonCommandIds.RequestMods, "Request Mods", false);
            _ = AddItem(help, RibbonCommandIds.SendFeedback, "Send Feedback", false);

            // Caption bar: right side of the strip. The update button starts hidden (MW1-027).
            _ = AddButton(RibbonCommandIds.HeaderDonate, "Support With a Donation", ActionAlign.Right);
            ToolStripButton update = AddButton(RibbonCommandIds.HeaderUpdate, "New Update Available", ActionAlign.Right);
            update.Visible = false;
        }

        /// <summary>
        /// Raised when a command item is clicked. Not raised for the drop-down buttons and sub-menus themselves.
        /// </summary>
        public event EventHandler<RibbonCommandEventArgs> CommandInvoked;

        /// <summary>
        /// Returns the strip item with the id (a RibbonCommandIds value), or null.
        /// </summary>
        public ToolStripItem Find(string id)
        {
            ToolStripItem item;
            return id != null && _items.TryGetValue(id, out item) ? item : null;
        }

        /// <summary>
        /// Sets an item's caption (the language hook). Returns false for an unknown id.
        /// </summary>
        public bool SetCaption(string id, string caption)
        {
            ToolStripItem item = Find(id);
            if (item == null)
            {
                return false;
            }
            item.Text = caption == null ? string.Empty : caption;
            return true;
        }

        public bool SetEnabled(string id, bool enabled)
        {
            ToolStripItem item = Find(id);
            if (item == null)
            {
                return false;
            }
            item.Enabled = enabled;
            return true;
        }

        public bool SetVisible(string id, bool visible)
        {
            ToolStripItem item = Find(id);
            if (item == null)
            {
                return false;
            }
            item.Visible = visible;
            return true;
        }

        /// <summary>
        /// Sets a check item (Discord Presence, Install Mods to USB) without raising CommandInvoked. Returns false for
        /// an unknown id or an item that is not a check item.
        /// </summary>
        public bool SetChecked(string id, bool isChecked)
        {
            ToolStripMenuItem item = Find(id) as ToolStripMenuItem;
            if (item == null || !item.CheckOnClick)
            {
                return false;
            }
            item.Checked = isChecked;
            return true;
        }

        /// <summary>
        /// Shows or hides the "New Update Available" button (old UpdateAvailable, MW1-027).
        /// </summary>
        public void SetUpdateAvailable(bool available)
        {
            _ = SetVisible(RibbonCommandIds.HeaderUpdate, available);
        }

        /// <summary>
        /// Connect shows "Reconnect Console" while connected and "Connect Console" otherwise; Disconnect is enabled only
        /// while connected (MainWindow.cs 1240-1241 and 1333-1334).
        /// </summary>
        public void SetConnected(bool connected)
        {
            _ = SetCaption(RibbonCommandIds.ConnectConsole, connected ? "Reconnect Console" : "Connect Console");
            _ = SetEnabled(RibbonCommandIds.DisconnectConsole, connected);
        }

        /// <summary>
        /// Shows and enables the platform tools the way the old EnableConsoleActions did (MW3-148): PS3 tools and the
        /// webMAN group for PS3, Xbox tools, the XBDM group and the Xbox scan command for Xbox 360, nothing extra for PS4.
        /// Tools need a connected console, except the Xbox Game Save Resigner. The Boot Plugins Editor also needs webMAN.
        /// A tools drop-down with no visible item is hidden (the old empty group stayed visible). The old code ran only
        /// in release builds; the strip does nothing until a host calls this. Does not touch the Connect and Disconnect
        /// buttons (use SetConnected).
        /// </summary>
        public void ApplyConsoleState(Platform platform, bool connected, bool webManInstalled)
        {
            bool ps3 = platform == Platform.PS3;
            bool xbox = platform == Platform.XBOX360;

            _ = SetVisible(RibbonCommandIds.ScanXboxConsoles, xbox);

            foreach (ToolStripMenuItem item in _psTools)
            {
                item.Visible = ps3;
                item.Enabled = connected && ps3;
            }
            _items[RibbonCommandIds.ToolsPsBootPluginsEditor].Enabled = connected && webManInstalled && ps3;

            foreach (ToolStripMenuItem item in _xboxTools)
            {
                item.Visible = xbox;
                item.Enabled = connected && xbox;
            }
            // Game Save Resigner works offline.
            _items[RibbonCommandIds.ToolsXboxGameSaveResigner].Enabled = xbox;

            _menuModdingTools.Visible = ps3 || xbox;

            _menuWebMan.Visible = ps3;
            _menuWebMan.Enabled = connected && ps3;
            _menuXbdm.Visible = xbox;
            _menuXbdm.Enabled = connected && xbox;
        }

        private ToolStripButton AddButton(string id, string text, ActionAlign align)
        {
            ToolStripButton button = _strip.AddAction(id, text, null, OnItemClick, align);
            _items[id] = button;
            return button;
        }

        private ToolStripDropDownButton AddMenu(string id, string text)
        {
            ToolStripDropDownButton menu = _strip.AddMenu(id, text, ActionAlign.Left);
            _items[id] = menu;
            return menu;
        }

        private ToolStripMenuItem AddSubMenu(ToolStripDropDownItem parent, string id, string text)
        {
            ToolStripMenuItem item = new ToolStripMenuItem { Name = id, Text = text };
            _ = parent.DropDownItems.Add(item);
            _items[id] = item;
            return item;
        }

        private ToolStripMenuItem AddItem(ToolStripDropDownItem parent, string id, string text, bool isCheck)
        {
            ToolStripMenuItem item = new ToolStripMenuItem { Name = id, Text = text, CheckOnClick = isCheck };
            item.Click += OnItemClick;
            _ = parent.DropDownItems.Add(item);
            _items[id] = item;
            return item;
        }

        private void OnItemClick(object sender, EventArgs e)
        {
            ToolStripItem item = sender as ToolStripItem;
            if (item == null)
            {
                return;
            }
            ToolStripMenuItem menuItem = item as ToolStripMenuItem;
            bool isCheck = menuItem != null && menuItem.CheckOnClick;
            EventHandler<RibbonCommandEventArgs> handler = CommandInvoked;
            if (handler != null)
            {
                // CheckOnClick has already toggled Checked when Click is raised.
                handler(this, new RibbonCommandEventArgs(item.Name, item.Text, isCheck, isCheck && menuItem.Checked));
            }
        }
    }
}
