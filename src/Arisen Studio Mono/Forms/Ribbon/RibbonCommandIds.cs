namespace ArisenStudio.Shell
{
    /// <summary>
    /// Command ids of the ribbon replacement (S6). Each id is the name of the old BarButtonItem, so a ledger row
    /// (MW1-026 to MW1-070 and MW3-148) maps to its id by name. The id is the Name of the strip item.
    /// </summary>
    internal static class RibbonCommandIds
    {
        // Caption bar (MW1-026 to MW1-028)
        public const string HeaderDonate = "ButtonHeaderDonate";
        public const string HeaderUpdate = "ButtonHeaderUpdate";

        // Home > Connection (MW1-029, MW1-030)
        public const string ConnectConsole = "ButtonConnectConsole";
        public const string DisconnectConsole = "ButtonDisconnectConsole";

        // Home > Console Profiles (MW1-031 to MW1-034)
        public const string AddNewProfile = "ButtonAddNewProfile";
        public const string ScanXboxConsoles = "ButtonScanXboxConsoles";
        public const string EditProfiles = "ButtonEditProfiles";
        public const string ChangeProfile = "ButtonChangeProfile";

        // Home > Quick Settings (MW1-035 to MW1-040)
        public const string HowToGuides = "ButtonHowToGuides";
        public const string SetupWizard = "ButtonSetupWizard";
        public const string DownloadsFolder = "ButtonDownloadsFolder";
        public const string DiscordPresence = "ButtonDiscordPresence";
        public const string InstallModsToUsb = "ButtonInstallModsToUSB";
        public const string AdvancedSettings = "ButtonAdvancedSettings";

        // Home > Help (MW1-041 to MW1-045)
        public const string About = "ButtonAbout";
        public const string OpenLogFile = "ButtonOpenLogFile";
        public const string OpenLogFolder = "ButtonOpenLogFolder";
        public const string RequestMods = "ButtonRequestMods";
        public const string SendFeedback = "ButtonSendFeedback";

        // Tools > Modding Tools (MW1-046 onwards)
        public const string ToolsPsGameUpdates = "ButtonToolsPsGameUpdates";
        public const string ToolsPsBackupFilesManager = "ButtonToolsPsBackupFilesManager";
        public const string ToolsPsPackageManager = "ButtonToolsPsPackageManager";
        public const string ToolsPsConsoleManager = "ButtonToolsPsConsoleManager";
        public const string ToolsPsGameRegions = "ButtonToolsPsGameRegions";
        public const string ToolsPsBootPluginsEditor = "ButtonToolsPsBootPluginsEditor";
        public const string ToolsXboxGameSaveResigner = "ButtonToolsXboxGameSaveResigner";
        public const string ToolsXboxHdKey = "ButtonToolsXboxHDKey";
        public const string ToolsXboxGamesLauncher = "ButtonToolsXboxGamesLauncher";
        public const string ToolsXboxModuleLoader = "ButtonToolsXboxModuleLoader";
        public const string ToolsXboxXuidSpoofer = "ButtonToolsXboxXuidSpoofer";
        public const string ToolsXboxDashlaunchEditor = "ButtonToolsXboxDashlaunchEditor";

        // Tools > webMAN Commands
        public const string ToolsPsPower = "ButtonToolsPsPower";
        public const string ToolsPsPowerShutdown = "ButtonToolsPsPowerShutdown";
        public const string ToolsPsPowerRestart = "ButtonToolsPsPowerRestart";
        public const string ToolsPsPowerSoftReboot = "ButtonToolsPsPowerSoftReboot";
        public const string ToolsPsPowerHardReboot = "ButtonToolsPsPowerHardReboot";
        public const string ToolsPsPowerQuickReboot = "ButtonToolsPsPowerQuickReboot";
        public const string ToolsPsGames = "ButtonToolsPsGames";
        public const string ToolsPsGamesOpenGui = "ButtonToolsPsGamesOpenGUI";
        public const string ToolsPsGamesMountBd = "ButtonToolsPsGamesMountBD";
        public const string ToolsPsGamesMountIso = "ButtonToolsPsGamesMountISO";
        public const string ToolsPsGamesMountPsn = "ButtonToolsPsGamesMountPSN";
        public const string ToolsPsGamesUnmount = "ButtonToolsPsGamesUnmount";
        public const string ToolsPsGamesLaunchGame = "ButtonToolsPsGamesLaunchGame";
        public const string ToolsPsScreenshot = "ButtonToolsPsScreenshot";
        public const string ToolsPsNotifyMessage = "ButtonToolsPsNotifyMessage";
        public const string ToolsPsVirtualController = "ButtonToolsPsVirtualController";

        // Tools > XBDM Commands
        public const string ToolsXboxPower = "ButtonToolsXboxPower";
        public const string ToolsXboxPowerShutdown = "ButtonToolsXboxPowerShutdown";
        public const string ToolsXboxPowerSoftReboot = "ButtonToolsXboxPowerSoftReboot";
        public const string ToolsXboxPowerHardReboot = "ButtonToolsXboxPowerHardReboot";
        public const string ToolsXboxTakeScreenshot = "ButtonToolsXboxTakeScreenshot";
        public const string ToolsXboxNeighborhoodEditor = "ButtonToolsXboxNeighborhoodEditor";
        public const string ToolsXboxNotifyMessage = "ButtonToolsXboxNotifyMessage";

        // Menu buttons on the strip (the old ribbon groups). These raise no command.
        public const string MenuConsoleProfiles = "RibbonGroupConnsoleProfiles";
        public const string MenuQuickSettings = "RibbonGroupQuickSettings";
        public const string MenuModdingTools = "RibbonGroupModdingTools";
        public const string MenuWebManCommands = "RibbonGroupWebManCommands";
        public const string MenuXbdmCommands = "RibbonGroupXbdmCommands";
        public const string MenuHelp = "RibbonGroupHelp";
    }
}
