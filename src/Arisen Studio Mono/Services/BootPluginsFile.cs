using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArisenStudio.Services
{
    /// <summary>
    /// Pure helpers for the PS3 boot_plugins.txt file: one plugin path per line.
    /// Split out of the old BootPluginsEditor form. Local files only: nothing here talks to the console.
    /// </summary>
    public static class BootPluginsFile
    {
        public const string FileName = "boot_plugins.txt";

        public const string FolderName = "Boot Plugins";

        /// <summary>
        /// Split file text into lines the way File.ReadLines does: "\r\n", "\n" and "\r" all end a line,
        /// and a final line break does not add an empty entry. Empty lines in the middle are kept.
        /// </summary>
        public static List<string> ParseLines(string text)
        {
            List<string> lines = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            using (StringReader reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// Join lines for writing: each line is followed by <paramref name="newLine" />
        /// (the old code used WriteLine, so the platform newline, which is the default here).
        /// Null entries are written as empty lines.
        /// </summary>
        public static string Serialize(IEnumerable<string> lines, string newLine)
        {
            if (lines == null)
            {
                throw new ArgumentNullException("lines");
            }

            string separator = newLine ?? Environment.NewLine;
            StringBuilder builder = new StringBuilder();

            foreach (string line in lines)
            {
                builder.Append(line ?? string.Empty);
                builder.Append(separator);
            }

            return builder.ToString();
        }

        public static string Serialize(IEnumerable<string> lines)
        {
            return Serialize(lines, null);
        }

        /// <summary>
        /// Lines with leading and trailing white space removed and blank lines dropped.
        /// Use this before saving if the grid may hold empty rows. Does not change the order.
        /// </summary>
        public static List<string> Clean(IEnumerable<string> lines)
        {
            List<string> result = new List<string>();

            if (lines == null)
            {
                return result;
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result.Add(line.Trim());
            }

            return result;
        }

        /// <summary>
        /// Whether the plugin path is already listed (ignoring case and surrounding white space).
        /// Comment lines (starting with "#" or ";") are not plugin entries and never match.
        /// </summary>
        public static bool ContainsPlugin(IEnumerable<string> lines, string pluginPath)
        {
            if (lines == null || string.IsNullOrWhiteSpace(pluginPath))
            {
                return false;
            }

            string wanted = pluginPath.Trim();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || IsComment(line))
                {
                    continue;
                }

                if (string.Equals(line.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsComment(string line)
        {
            if (line == null)
            {
                return false;
            }

            string trimmed = line.TrimStart();
            return trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal);
        }

        /// <summary>
        /// Folder for the working copy: &lt;appData&gt;/Boot Plugins.
        /// The old code appended "Boot Plugins\" with a backslash, which is a single odd folder name on Linux/Mono.
        /// </summary>
        public static string WorkingFolder(string appDataFolder)
        {
            if (string.IsNullOrEmpty(appDataFolder))
            {
                throw new ArgumentException("appDataFolder is empty", "appDataFolder");
            }

            return Path.Combine(appDataFolder, FolderName);
        }

        public static string WorkingFilePath(string appDataFolder)
        {
            return Path.Combine(WorkingFolder(appDataFolder), FileName);
        }

        /// <summary>
        /// Folder for the pristine copy taken when the editor first loads: &lt;backupFolder&gt;/Boot Plugins.
        /// </summary>
        public static string BackupFolder(string backupFilesFolder)
        {
            if (string.IsNullOrEmpty(backupFilesFolder))
            {
                throw new ArgumentException("backupFilesFolder is empty", "backupFilesFolder");
            }

            return Path.Combine(backupFilesFolder, FolderName);
        }

        public static string BackupFilePath(string backupFilesFolder)
        {
            return Path.Combine(BackupFolder(backupFilesFolder), FileName);
        }

        /// <summary>
        /// Read a local boot_plugins.txt. A missing file gives an empty list.
        /// </summary>
        public static List<string> ReadFile(string localPath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                throw new ArgumentException("localPath is empty", "localPath");
            }

            if (!File.Exists(localPath))
            {
                return new List<string>();
            }

            return ParseLines(File.ReadAllText(localPath));
        }

        /// <summary>
        /// Write a local boot_plugins.txt, creating its folder if needed. Plain ASCII/UTF-8 without a byte order mark,
        /// since the console reads the file as raw text.
        /// </summary>
        public static void WriteFile(string localPath, IEnumerable<string> lines)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                throw new ArgumentException("localPath is empty", "localPath");
            }

            string folder = Path.GetDirectoryName(localPath);

            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(localPath, Serialize(lines), new UTF8Encoding(false));
        }
    }
}
