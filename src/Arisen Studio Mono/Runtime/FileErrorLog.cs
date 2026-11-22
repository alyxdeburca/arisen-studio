using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ArisenStudio.Runtime
{
    /// <summary>
    /// Plain file error log for <see cref="AppHost.Logger" />. One line per error, timestamped, appended to one file
    /// (the caller chooses it; <see cref="AppBootstrapper" /> picks Logs/latest.log). Nothing here can throw: every
    /// IO problem is swallowed, because a logger that throws from inside a catch block hides the original error.
    /// No WinForms, no network. Thread safe (one lock around the write). New type; nothing was compiled or run.
    /// </summary>
    internal sealed class FileErrorLog
    {
        /// <summary>At start-up a log larger than this is overwritten instead of appended to, so latest.log stays small.</summary>
        internal const long MaxBytesBeforeRestart = 1024 * 1024;

        private readonly object _lock = new object();
        private readonly string _path;
        private bool _disabled;

        /// <summary>The file this log writes to.</summary>
        public string FilePath
        {
            get { return _path; }
        }

        /// <summary>
        /// Creates the log and prepares the folder. Returns an instance even when the folder cannot be created; writes
        /// are then silently dropped. Never throws.
        /// </summary>
        /// <param name="path">Full path of the log file.</param>
        public FileErrorLog(string path)
        {
            _path = path;
            try
            {
                string directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool restart = false;
                if (File.Exists(path))
                {
                    restart = new FileInfo(path).Length > MaxBytesBeforeRestart;
                }

                using (FileStream stream = new FileStream(path, restart ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(Stamp() + " [INFO] ---- session start ----");
                }
            }
            catch (Exception)
            {
                _disabled = true;
            }
        }

        /// <summary>The delegate to assign to <see cref="AppHost.Logger" />.</summary>
        public void Write(Exception exception, string message)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                StringBuilder line = new StringBuilder();
                line.Append(Stamp()).Append(" [ERROR] ").Append(message ?? string.Empty);
                if (exception != null)
                {
                    // ToString() carries the type, the message, the stack and the inner exceptions.
                    line.AppendLine().Append(exception.ToString());
                }

                lock (_lock)
                {
                    using (FileStream stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(line.ToString());
                    }
                }
            }
            catch (Exception)
            {
                // Never throw from the logger. Drop the line and make sure the folder exists for the next one.
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static string Stamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }
}
