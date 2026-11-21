using ArisenStudio.Models.Resources;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ArisenStudio.Runtime
{
    /// <summary>
    /// Non-UI holder for the state that the data-layer classes used to read from the old main window.
    /// Nothing here touches a form or a control; the UI subscribes to the events and supplies the hooks.
    /// </summary>
    public static class AppHost
    {
        /// <summary>
        /// Was the old main window's Settings. Starts as defaults; replaced by <see cref="LoadSettings" />.
        /// </summary>
        public static SettingsData Settings { get; private set; } = new SettingsData();

        /// <summary>
        /// Raised when the app wants to show a status line. Was the old window's SetStatus.
        /// </summary>
        public static event EventHandler<StatusEventArgs> StatusChanged;

        /// <summary>
        /// Raise a status message. The UI decides how to show it.
        /// </summary>
        public static void SetStatus(string status, Exception exception = null)
        {
            EventHandler<StatusEventArgs> handler = StatusChanged;
            if (handler != null)
            {
                handler(null, new StatusEventArgs(status, exception));
            }
        }

        /// <summary>
        /// Raised when an update check finds a result. Was the old window's UpdateAvailable.
        /// </summary>
        public static event Action<bool> UpdateAvailableChanged;

        /// <summary>
        /// Raise the update-available flag. The UI decides how to show it.
        /// </summary>
        public static void UpdateAvailable(bool isNewUpdate)
        {
            Action<bool> handler = UpdateAvailableChanged;
            if (handler != null)
            {
                handler(isNewUpdate);
            }
        }

        /// <summary>
        /// Error logger hook. Left unset by default, so logging is a no-op until the host assigns it.
        /// The logger decision is open (design step 2); this class does not choose a logger.
        /// </summary>
        public static Action<Exception, string> Logger { get; set; }

        /// <summary>
        /// Delegate for the FTP download that the old FtpClient.DownloadFile call did.
        /// Arguments are (localPath, installPath). Not set by default; the FTP library is not linked here.
        /// </summary>
        public static Func<string, string, Task> FtpDownloadFile { get; set; }

        /// <summary>
        /// Load settings from <paramref name="path" />. If the file does not exist, write the current
        /// defaults to it. Returns false on failure; the error goes to <see cref="SetStatus" /> and <see cref="Logger" />.
        /// The caller supplies the path, so no platform path is hard-coded here.
        /// </summary>
        public static bool LoadSettings(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    WriteSettings(path);
                }

                using (StreamReader reader = new StreamReader(path))
                {
                    SettingsData loaded = JsonConvert.DeserializeObject<SettingsData>(reader.ReadToEnd());
                    if (loaded != null)
                    {
                        Settings = loaded;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log(ex, "Unable to load application settings data.");
                SetStatus("Unable to load application settings data: " + ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// Save the current settings to <paramref name="path" />. Returns false on failure.
        /// </summary>
        public static bool SaveSettings(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                WriteSettings(path);
                return true;
            }
            catch (Exception ex)
            {
                Log(ex, "Unable to save application settings data.");
                SetStatus("Unable to save application settings data: " + ex.Message, ex);
                return false;
            }
        }

        private static void WriteSettings(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.Write(JsonConvert.SerializeObject(Settings));
            }
        }

        private static void Log(Exception exception, string message)
        {
            Action<Exception, string> logger = Logger;
            if (logger != null)
            {
                logger(exception, message);
            }
        }
    }
}
