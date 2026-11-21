using System;
using System.Collections.Generic;

namespace ArisenStudio.Services
{
    /// <summary>
    /// Pure helpers for PS3 console paths (forward-slash paths as seen over FTP). No I/O and no UI.
    /// Split out of the old FtpExtensions so the path rules can be checked without a console.
    /// </summary>
    public static class ConsolePaths
    {
        /// <summary>
        /// Folder names of the USB ports on a PS3, in probe order.
        /// </summary>
        public static readonly string[] UsbPorts =
        {
            "dev_usb000",
            "dev_usb001",
            "dev_usb002",
            "dev_usb003"
        };

        public const string HomeFolder = "/dev_hdd0/home/";

        public const string GamesBd = "GAMES/";
        public const string GamesBdAlt = "GAMEZ/";
        public const string GamesIso = "PS3ISO/";
        public const string GamesPsn = "GAMEI/";
        public const string GamesPs2Iso = "PS2ISO/";
        public const string GamesPsxIso = "PSXISO/";
        public const string GamesPsxFolders = "PSXGAMES/";

        /// <summary>
        /// Path of the file that holds the name of a console user.
        /// </summary>
        public static string LocalUserNameFile(string userId)
        {
            return HomeFolder + userId + "/localusername";
        }

        /// <summary>
        /// Folder part of a console file path, with forward slashes and a trailing slash.
        /// "/dev_hdd0/tmp/a.txt" gives "/dev_hdd0/tmp/". A path with no folder gives "/".
        /// Backslashes are treated as separators, as the old code did with Replace.
        /// </summary>
        public static string GetDirectory(string consoleFile)
        {
            if (consoleFile == null)
            {
                throw new ArgumentNullException("consoleFile");
            }

            string normalized = consoleFile.Replace('\\', '/');
            int lastSlash = normalized.LastIndexOf('/');

            if (lastSlash < 0)
            {
                return "/";
            }

            return normalized.Substring(0, lastSlash + 1);
        }

        /// <summary>
        /// Root of a game library folder on a device: the internal drive when
        /// <paramref name="usbPort" /> is null or empty, otherwise that USB port.
        /// <paramref name="folder" /> is one of the Games* constants.
        /// </summary>
        public static string Library(string usbPort, string folder)
        {
            if (string.IsNullOrEmpty(usbPort))
            {
                return "/dev_hdd0/" + folder;
            }

            return "/" + usbPort + "/" + folder;
        }

        /// <summary>
        /// The device roots to scan for games: the internal drive first (null entry),
        /// then each USB port if <paramref name="includeExternal" /> is true.
        /// </summary>
        public static List<string> DeviceRoots(bool includeExternal)
        {
            List<string> roots = new List<string>();
            roots.Add(null);

            if (includeExternal)
            {
                roots.AddRange(UsbPorts);
            }

            return roots;
        }

        /// <summary>
        /// First USB port name found in <paramref name="rootFolderNames" /> (the names under "/"),
        /// or null if none. Matches by Contains, as the old code did.
        /// </summary>
        public static string FindUsbPort(IEnumerable<string> rootFolderNames)
        {
            if (rootFolderNames == null)
            {
                return null;
            }

            List<string> names = new List<string>(rootFolderNames);

            foreach (string usbPort in UsbPorts)
            {
                foreach (string name in names)
                {
                    if (name != null && name.Contains(usbPort))
                    {
                        return usbPort;
                    }
                }
            }

            return null;
        }
    }
}
