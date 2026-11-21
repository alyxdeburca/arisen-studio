namespace ArisenStudio.Shell
{
    /// <summary>
    /// Page keys for the navigation leaves and the About page. Each leaf key is also the key its page is registered under in PageHost.
    /// Names follow the old NavigationItem* controls (MW1-081 to MW1-094).
    /// </summary>
    internal static class ShellPageKeys
    {
        public const string Dashboard = "dashboard";
        public const string Downloads = "downloads";
        public const string InstalledFiles = "installed-files";
        public const string FileManager = "file-manager";
        public const string Settings = "settings";
        public const string CustomMods = "custom-mods";
        public const string GameMods = "game-mods";
        public const string Homebrew = "homebrew";
        public const string Trainers = "trainers";
        public const string Resources = "resources";
        public const string PkgStore = "pkg-store";
        public const string Games = "games";
        public const string GameSaves = "game-saves";
        public const string GameCheats = "game-cheats";
        public const string About = "about";
    }
}
