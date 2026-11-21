using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArisenStudio.Services.Extra
{
    /// <summary>
    /// One key and value line of a launch.ini section, with the comment lines that stood above it.
    /// </summary>
    public sealed class LaunchIniEntry
    {
        public LaunchIniEntry(string key, string value)
        {
            Key = key ?? string.Empty;
            Value = value ?? string.Empty;
            CommentsAbove = new List<string>();
        }

        public string Key { get; private set; }

        public string Value { get; set; }

        /// <summary>
        /// Comment lines (kept whole, with their leading ';' or '#') that were above the entry.
        /// </summary>
        public List<string> CommentsAbove { get; private set; }

        /// <summary>
        /// Text after a " ;" on the same line, without the ';' and the spaces around it. Null when there was none.
        /// </summary>
        public string TrailingComment { get; set; }
    }

    /// <summary>
    /// One [section] of a launch.ini file. The section with an empty name holds the keys before the first header.
    /// </summary>
    public sealed class LaunchIniSection
    {
        public LaunchIniSection(string name)
        {
            Name = name ?? string.Empty;
            Entries = new List<LaunchIniEntry>();
            CommentsAbove = new List<string>();
        }

        public string Name { get; private set; }

        public List<LaunchIniEntry> Entries { get; private set; }

        /// <summary>
        /// Comment lines that were above the [section] header.
        /// </summary>
        public List<string> CommentsAbove { get; private set; }

        public LaunchIniEntry Find(string key)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return Entries[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Small in-memory reader and writer for the Dashlaunch launch.ini file. It replaces the IniParser
    /// package that the old DashlaunchEditor used (FileIniDataParser, IniData, SectionData, KeyData),
    /// so the editor needs no extra package and the parsing can be checked without a console.
    /// Section order, key order and comment lines are kept, so a load then save does not lose notes.
    /// Section and key lookups ignore case (IniParser was case sensitive); the stored spelling is kept.
    /// A key repeated in one section is kept as two entries, and Get returns the first.
    /// </summary>
    public sealed class LaunchIniFile
    {
        private readonly List<LaunchIniSection> _sections = new List<LaunchIniSection>();

        public LaunchIniFile()
        {
            NewLine = "\r\n";
            TrailingComments = new List<string>();
        }

        /// <summary>
        /// Line ending used by <see cref="ToText" />. Set by <see cref="Parse" /> from the first line ending found
        /// (the files on the console use CRLF); CRLF for a new file.
        /// </summary>
        public string NewLine { get; set; }

        /// <summary>
        /// Comment lines at the end of the file, after the last entry.
        /// </summary>
        public List<string> TrailingComments { get; private set; }

        public IList<LaunchIniSection> Sections
        {
            get { return _sections; }
        }

        /// <summary>
        /// Section names in file order (for the section drop-down). Does not include the unnamed global section when it is empty.
        /// </summary>
        public List<string> SectionNames
        {
            get
            {
                List<string> names = new List<string>();
                foreach (LaunchIniSection section in _sections)
                {
                    if (section.Name.Length == 0 && section.Entries.Count == 0)
                    {
                        continue;
                    }

                    names.Add(section.Name);
                }

                return names;
            }
        }

        public bool HasSection(string sectionName)
        {
            return FindSection(sectionName) != null;
        }

        public LaunchIniSection FindSection(string sectionName)
        {
            string wanted = sectionName ?? string.Empty;
            foreach (LaunchIniSection section in _sections)
            {
                if (string.Equals(section.Name, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }
            }

            return null;
        }

        /// <summary>
        /// Key and value pairs of a section in file order (the grid rows). Empty list when the section does not exist.
        /// </summary>
        public List<KeyValuePair<string, string>> GetKeys(string sectionName)
        {
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            LaunchIniSection section = FindSection(sectionName);
            if (section == null)
            {
                return rows;
            }

            foreach (LaunchIniEntry entry in section.Entries)
            {
                rows.Add(new KeyValuePair<string, string>(entry.Key, entry.Value));
            }

            return rows;
        }

        /// <summary>
        /// Value of a key, or null when the section or the key is missing.
        /// </summary>
        public string Get(string sectionName, string key)
        {
            LaunchIniSection section = FindSection(sectionName);
            if (section == null)
            {
                return null;
            }

            LaunchIniEntry entry = section.Find(key);
            return entry == null ? null : entry.Value;
        }

        /// <summary>
        /// True when the value reads "true" (any case) or "1". False for anything else, including a missing key.
        /// </summary>
        public bool GetBool(string sectionName, string key)
        {
            string value = Get(sectionName, key);
            if (value == null)
            {
                return false;
            }

            value = value.Trim();
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        /// <summary>
        /// Set a value. Creates the section and the key when they are missing (as IniData's indexer did).
        /// </summary>
        public void Set(string sectionName, string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("A key name is required.", "key");
            }

            LaunchIniSection section = FindSection(sectionName);
            if (section == null)
            {
                section = new LaunchIniSection(sectionName);
                _sections.Add(section);
            }

            LaunchIniEntry entry = section.Find(key);
            if (entry == null)
            {
                section.Entries.Add(new LaunchIniEntry(key, value));
            }
            else
            {
                entry.Value = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Write "true" or "false" (lower case, as the old checkboxes did for liveblock and livestrong).
        /// </summary>
        public void SetBool(string sectionName, string key, bool value)
        {
            Set(sectionName, key, value ? "true" : "false");
        }

        /// <summary>
        /// Remove a key. True when something was removed.
        /// </summary>
        public bool Remove(string sectionName, string key)
        {
            LaunchIniSection section = FindSection(sectionName);
            if (section == null)
            {
                return false;
            }

            LaunchIniEntry entry = section.Find(key);
            return entry != null && section.Entries.Remove(entry);
        }

        /// <summary>
        /// Parse the text of a launch.ini. Never throws for odd text: a line that is not a comment, a header
        /// or key=value is kept as a key with an empty value so it is not lost on save.
        /// </summary>
        public static LaunchIniFile Parse(string text)
        {
            LaunchIniFile file = new LaunchIniFile();
            if (string.IsNullOrEmpty(text))
            {
                return file;
            }

            int firstLf = text.IndexOf('\n');
            if (firstLf >= 0)
            {
                file.NewLine = firstLf > 0 && text[firstLf - 1] == '\r' ? "\r\n" : "\n";
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int count = lines.Length;
            if (count > 0 && lines[count - 1].Length == 0)
            {
                count--; // the text ended with a line break
            }

            LaunchIniSection current = null;
            List<string> pending = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();

                if (line.Length == 0)
                {
                    continue; // blank lines are not kept; the writer puts one before each header
                }

                if (line[0] == ';' || line[0] == '#')
                {
                    pending.Add(line);
                    continue;
                }

                if (line[0] == '[')
                {
                    int close = line.IndexOf(']');
                    string name = close > 0 ? line.Substring(1, close - 1).Trim() : line.Substring(1).Trim();
                    current = file.FindSection(name);
                    if (current == null)
                    {
                        current = new LaunchIniSection(name);
                        file._sections.Add(current);
                    }

                    current.CommentsAbove.AddRange(pending);
                    pending.Clear();
                    continue;
                }

                if (current == null)
                {
                    current = new LaunchIniSection(string.Empty);
                    file._sections.Add(current);
                }

                string key;
                string value;
                string trailing;
                SplitEntry(line, out key, out value, out trailing);

                LaunchIniEntry entry = new LaunchIniEntry(key, value);
                entry.TrailingComment = trailing;
                entry.CommentsAbove.AddRange(pending);
                pending.Clear();
                current.Entries.Add(entry);
            }

            file.TrailingComments.AddRange(pending);
            return file;
        }

        private static void SplitEntry(string line, out string key, out string value, out string trailing)
        {
            trailing = null;
            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                key = line;
                value = string.Empty;
                return;
            }

            key = line.Substring(0, eq).Trim();
            string rest = line.Substring(eq + 1);

            // A ';' after white space ends the value and starts a trailing comment.
            int cut = -1;
            for (int i = 1; i < rest.Length; i++)
            {
                if (rest[i] == ';' && char.IsWhiteSpace(rest[i - 1]))
                {
                    cut = i;
                    break;
                }
            }

            if (cut >= 0)
            {
                trailing = rest.Substring(cut + 1).Trim();
                rest = rest.Substring(0, cut);
            }

            value = rest.Trim();
        }

        /// <summary>
        /// Read a launch.ini from disk (UTF-8, with or without a BOM).
        /// </summary>
        public static LaunchIniFile Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A file path is required.", "path");
            }

            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>
        /// The file as text: a blank line before each header, "key = value" lines (the spacing IniParser wrote).
        /// </summary>
        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;

            foreach (LaunchIniSection section in _sections)
            {
                bool unnamed = section.Name.Length == 0;
                if (unnamed && section.Entries.Count == 0)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(NewLine);
                }

                first = false;

                foreach (string comment in section.CommentsAbove)
                {
                    sb.Append(comment).Append(NewLine);
                }

                if (!unnamed)
                {
                    sb.Append('[').Append(section.Name).Append(']').Append(NewLine);
                }

                foreach (LaunchIniEntry entry in section.Entries)
                {
                    foreach (string comment in entry.CommentsAbove)
                    {
                        sb.Append(comment).Append(NewLine);
                    }

                    sb.Append(entry.Key);
                    if (entry.Value.Length > 0 || entry.TrailingComment != null)
                    {
                        sb.Append(" = ").Append(entry.Value);
                    }
                    else
                    {
                        sb.Append(" =");
                    }

                    if (entry.TrailingComment != null)
                    {
                        sb.Append(" ; ").Append(entry.TrailingComment);
                    }

                    sb.Append(NewLine);
                }
            }

            if (TrailingComments.Count > 0)
            {
                if (!first)
                {
                    sb.Append(NewLine);
                }

                foreach (string comment in TrailingComments)
                {
                    sb.Append(comment).Append(NewLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Write the file as UTF-8 without a BOM. Creates the folder when it is missing.
        /// </summary>
        public void Save(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A file path is required.", "path");
            }

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, ToText(), new UTF8Encoding(false));
        }
    }
}
