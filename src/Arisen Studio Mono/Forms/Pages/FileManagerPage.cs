using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Shell;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// The File Manager page (slice S3, old PageFileManager, ledger MW1-153 to MW1-173 and MW2-001 to MW2-026): a local
    /// folder on the left, the console's folder on the right, buttons beside each, a status line under each.
    ///
    /// The local half works on its own with System.IO. The console half works through <see cref="ConsoleService"/>
    /// (null or not connected disables it). Drag a row from one grid to the other to copy it across.
    /// Use: set <see cref="ConsoleService"/> (again whenever the connection changes, then call
    /// <see cref="ConnectionChanged"/>), set <see cref="InitialLocalPath"/>, subscribe to the events, register under
    /// ShellPageKeys.FileManager.
    /// </summary>
    internal sealed class FileManagerPage : UserControl, IShellPage
    {
        private const string DragFormat = "ArisenStudio.FileManagerDrag";
        private const string ParentName = "..";

        // What a drag carries: which grid it started in. The row itself is the selected row of that grid.
        private sealed class DragPayload
        {
            public readonly bool FromConsole;

            public DragPayload(bool fromConsole)
            {
                FromConsole = fromConsole;
            }
        }

        // One half of the page.
        private sealed class Side
        {
            public GroupBox Group;
            public ComboBox Drives;
            public TextBox PathBox;
            public Button GoButton;
            public ListGrid Grid;
            public FlowLayoutPanel ButtonColumn;
            public Label Status;
            public List<FileManagerEntry> Entries = new List<FileManagerEntry>();
            public string CurrentPath = string.Empty;
            public bool SuppressDrives;
        }

        private readonly Side local = new Side();
        private readonly Side console = new Side();

        private Button buttonUpload;
        private Button buttonLocalDelete;
        private Button buttonLocalRename;
        private Button buttonLocalNewFolder;
        private Button buttonLocalRefresh;
        private Button buttonLocalOpen;

        private Button buttonDownload;
        private Button buttonConsoleDelete;
        private Button buttonConsoleRename;
        private Button buttonConsoleNewFolder;
        private Button buttonConsoleRefresh;
        private Button buttonAddToModules;

        private IConsoleFileService consoleService;
        private string initialLocalPath = string.Empty;
        private bool useFormattedFileSizes = true;
        private bool useRelativeTimes = true;
        private bool autoLoadDirectoryListings;
        private bool localLoaded;
        private bool consoleLoaded;
        private bool busy;
        private int consoleTicket;
        private Func<string, string> localizer;

        /// <summary>Open a folder in the system file browser (ledger MW1-171). Item is the folder path.</summary>
        public event EventHandler<ItemEventArgs<string>> OpenFolderRequested;

        /// <summary>The local folder shown changed. The shell stores it when Settings.RememberLocalPath is on (ledger MW1-153).</summary>
        public event EventHandler<ItemEventArgs<string>> LocalPathChanged;

        /// <summary>The console folder shown changed. The shell stores it when Settings.RememberConsolePath is on.</summary>
        public event EventHandler<ItemEventArgs<string>> ConsolePathChanged;

        /// <summary>Xbox "Add to Modules" (ledger MW2-019). The host checks the name and sets Error to refuse.</summary>
        public event EventHandler<AddToModulesEventArgs> AddToModulesRequested;

        public event EventHandler<PageStatusEventArgs> StatusMessage;

        public FileManagerPage()
        {
            SuspendLayout();
            Font = SystemFonts.MessageBoxFont;

            BuildSide(local, "Local", "...");
            BuildSide(console, "Console", ">>");

            buttonUpload = AddButton(local, "Upload", ButtonUpload_Click);
            buttonLocalDelete = AddButton(local, "Delete", ButtonLocalDelete_Click);
            buttonLocalRename = AddButton(local, "Rename", ButtonLocalRename_Click);
            buttonLocalNewFolder = AddButton(local, "New Folder", ButtonLocalNewFolder_Click);
            buttonLocalRefresh = AddButton(local, "Refresh", ButtonLocalRefresh_Click);
            buttonLocalOpen = AddButton(local, "Open Explorer", ButtonLocalOpen_Click);

            buttonDownload = AddButton(console, "Download", ButtonDownload_Click);
            buttonConsoleDelete = AddButton(console, "Delete", ButtonConsoleDelete_Click);
            buttonConsoleRename = AddButton(console, "Rename", ButtonConsoleRename_Click);
            buttonConsoleNewFolder = AddButton(console, "New Folder", ButtonConsoleNewFolder_Click);
            buttonConsoleRefresh = AddButton(console, "Refresh", ButtonConsoleRefresh_Click);
            buttonAddToModules = AddButton(console, "Add to Modules", ButtonAddToModules_Click);

            local.GoButton.Click += ButtonBrowse_Click;
            console.GoButton.Click += ButtonConsoleGo_Click;
            local.PathBox.KeyDown += LocalPathBox_KeyDown;
            console.PathBox.KeyDown += ConsolePathBox_KeyDown;
            local.Drives.SelectedIndexChanged += LocalDrives_SelectedIndexChanged;
            console.Drives.SelectedIndexChanged += ConsoleDrives_SelectedIndexChanged;

            local.Grid.SelectionChanged += Grid_SelectionChanged;
            console.Grid.SelectionChanged += Grid_SelectionChanged;
            local.Grid.RowActivated += LocalGrid_RowActivated;
            console.Grid.RowActivated += ConsoleGrid_RowActivated;

            // Drag a row to the other half (the old code uploaded on any drop on the local list, ledger MW1-165).
            local.Grid.ItemDragged += LocalGrid_ItemDragged;
            console.Grid.ItemDragged += ConsoleGrid_ItemDragged;
            local.Grid.AllowDropOnGrid = true;
            console.Grid.AllowDropOnGrid = true;
            local.Grid.GridDragOver += LocalGrid_DragOver;
            console.Grid.GridDragOver += ConsoleGrid_DragOver;
            local.Grid.GridDragDrop += LocalGrid_DragDrop;
            console.Grid.GridDragDrop += ConsoleGrid_DragDrop;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(local.Group, 0, 0);
            layout.Controls.Add(console.Group, 1, 0);
            Controls.Add(layout);

            ApplyTexts();
            SetConsoleEnabled();
            UpdateButtons();
            ResumeLayout(true);
        }

        // ---- public surface ----

        /// <summary>The console behind the right half. Null or a disconnected service disables the half. Call <see cref="ConnectionChanged"/> after replacing it.</summary>
        public IConsoleFileService ConsoleService
        {
            get { return consoleService; }
            set { consoleService = value; }
        }

        /// <summary>First local folder to show (the remembered path, or null for the user's home folder).</summary>
        public string InitialLocalPath
        {
            get { return initialLocalPath; }
            set { initialLocalPath = value ?? string.Empty; }
        }

        public bool UseFormattedFileSizes
        {
            get { return useFormattedFileSizes; }
            set { useFormattedFileSizes = value; }
        }

        public bool UseRelativeTimes
        {
            get { return useRelativeTimes; }
            set { useRelativeTimes = value; }
        }

        /// <summary>
        /// Settings.AutoLoadDirectoryListings. The first time the page is shown after a connect the console folder is
        /// always listed; with this on it is listed again every time the page is shown. UNVERIFIED reading of the old
        /// setting: the ledger only records that the toggle writes the property (MW2-042).
        /// </summary>
        public bool AutoLoadDirectoryListings
        {
            get { return autoLoadDirectoryListings; }
            set { autoLoadDirectoryListings = value; }
        }

        public Func<string, string> Localizer
        {
            get { return localizer; }
            set { localizer = value; }
        }

        public void ApplyLanguage()
        {
            ApplyTexts();
        }

        /// <summary>Call after a console connected or disconnected (ledger MW1-153, MW1-155). Fills the drive list and clears the console half.</summary>
        public void ConnectionChanged()
        {
            consoleLoaded = false;
            consoleTicket++;
            console.Entries.Clear();
            console.CurrentPath = string.Empty;
            console.PathBox.Text = string.Empty;
            console.Grid.DataSource = BuildTable(console);

            console.SuppressDrives = true;
            console.Drives.Items.Clear();
            if (IsConsoleConnected())
            {
                IList<string> drives = consoleService.GetDrives();
                if (drives != null)
                {
                    foreach (string drive in drives)
                    {
                        console.Drives.Items.Add(drive);
                    }
                }
            }
            console.SuppressDrives = false;

            SetConsoleEnabled();
            UpdateButtons();
            if (!IsConsoleConnected())
            {
                SetStatus(console, string.Empty, false);
            }
            else if (Visible && Parent != null)
            {
                StartInitialConsoleLoad();
            }
        }

        public void OnPageShown()
        {
            if (!localLoaded)
            {
                LoadLocalDirectory(string.IsNullOrEmpty(initialLocalPath) ? DefaultLocalPath() : initialLocalPath);
                FillLocalDrives();
            }
            if (IsConsoleConnected() && (!consoleLoaded || autoLoadDirectoryListings))
            {
                StartInitialConsoleLoad();
            }
        }

        // ---- building ----

        private static string DefaultLocalPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        private void BuildSide(Side side, string title, string goText)
        {
            side.Group = new GroupBox();
            side.Group.Text = title;
            side.Group.Dock = DockStyle.Fill;
            side.Group.Padding = new Padding(6);

            side.Drives = new ComboBox();
            side.Drives.DropDownStyle = ComboBoxStyle.DropDownList;
            side.Drives.Dock = DockStyle.Fill;

            side.PathBox = new TextBox();
            side.PathBox.Dock = DockStyle.Fill;

            side.GoButton = new Button();
            side.GoButton.Text = goText;
            side.GoButton.Dock = DockStyle.Fill;

            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 30;
            top.ColumnCount = 3;
            top.RowCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            top.Controls.Add(side.Drives, 0, 0);
            top.Controls.Add(side.PathBox, 1, 0);
            top.Controls.Add(side.GoButton, 2, 0);

            side.Grid = new ListGrid();
            side.Grid.Dock = DockStyle.Fill;
            side.Grid.HideColumn("Key");
            side.Grid.SetColumnWidth("Type", 100);
            side.Grid.SetColumnWidth("Size", 90);
            side.Grid.SetColumnWidth("Modified", 130);

            side.ButtonColumn = new FlowLayoutPanel();
            side.ButtonColumn.Dock = DockStyle.Right;
            side.ButtonColumn.Width = 112;
            side.ButtonColumn.FlowDirection = FlowDirection.TopDown;
            side.ButtonColumn.WrapContents = false;

            side.Status = new Label();
            side.Status.Dock = DockStyle.Bottom;
            side.Status.AutoSize = false;
            side.Status.Height = 22;
            side.Status.TextAlign = ContentAlignment.MiddleLeft;

            side.Grid.DataSource = BuildTable(side);

            // Fill first, edge-docked controls after it (the last control added is laid out first).
            side.Group.Controls.Add(side.Grid);
            side.Group.Controls.Add(side.ButtonColumn);
            side.Group.Controls.Add(side.Status);
            side.Group.Controls.Add(top);
        }

        private static Button AddButton(Side side, string text, EventHandler onClick)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 100;
            button.Height = 28;
            button.Click += onClick;
            side.ButtonColumn.Controls.Add(button);
            return button;
        }

        private void ApplyTexts()
        {
            local.Group.Text = PageUi.Text(localizer, "LOCAL", "Local");
            console.Group.Text = PageUi.Text(localizer, "CONSOLE", "Console");
            buttonUpload.Text = PageUi.Text(localizer, "UPLOAD", "Upload");
            buttonLocalDelete.Text = PageUi.Text(localizer, "DELETE", "Delete");
            buttonLocalRename.Text = PageUi.Text(localizer, "RENAME", "Rename");
            buttonLocalNewFolder.Text = PageUi.Text(localizer, "NEW_FOLDER", "New Folder");
            buttonLocalRefresh.Text = PageUi.Text(localizer, "REFRESH", "Refresh");
            buttonLocalOpen.Text = PageUi.Text(localizer, "OPEN_EXPLORER", "Open Explorer");
            buttonDownload.Text = PageUi.Text(localizer, "DOWNLOAD", "Download");
            buttonConsoleDelete.Text = PageUi.Text(localizer, "DELETE", "Delete");
            buttonConsoleRename.Text = PageUi.Text(localizer, "RENAME", "Rename");
            buttonConsoleNewFolder.Text = PageUi.Text(localizer, "NEW_FOLDER", "New Folder");
            buttonConsoleRefresh.Text = PageUi.Text(localizer, "REFRESH", "Refresh");
            buttonAddToModules.Text = PageUi.Text(localizer, "ADD_TO_MODULES", "Add to Modules");
        }

        // ---- shared listing helpers ----

        private DataTable BuildTable(Side side)
        {
            DataTable table = new DataTable();
            table.Columns.Add("Key", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Type", typeof(string));
            table.Columns.Add("Size", typeof(string));
            table.Columns.Add("Modified", typeof(string));
            for (int i = 0; i < side.Entries.Count; i++)
            {
                FileManagerEntry entry = side.Entries[i];
                bool parent = entry.Name == ParentName;
                string display = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
                string type = parent ? string.Empty : (entry.IsDirectory ? "<DIRECTORY>" : "File");
                string size = parent || entry.IsDirectory ? string.Empty : PageUi.FormatSize(entry.Size, useFormattedFileSizes);
                string modified = parent || entry.Modified == DateTime.MinValue ? string.Empty : PageUi.FormatDate(entry.Modified, useRelativeTimes);
                table.Rows.Add(i.ToString(), display, type, size, modified);
            }
            return table;
        }

        private void ShowEntries(Side side, string path, List<FileManagerEntry> entries)
        {
            side.CurrentPath = path;
            side.PathBox.Text = path;
            side.Entries = entries;
            side.Grid.DataSource = BuildTable(side);
            if (side.Grid.RowCount > 0)
            {
                side.Grid.SelectFirstRow();
            }

            int files = 0;
            int folders = 0;
            long total = 0;
            foreach (FileManagerEntry entry in entries)
            {
                if (entry.Name == ParentName)
                {
                    continue;
                }
                if (entry.IsDirectory)
                {
                    folders++;
                }
                else
                {
                    files++;
                    total += entry.Size;
                }
            }
            SetStatus(side, files + " file(s), " + folders + " folder(s), " + PageUi.FormatSize(total, useFormattedFileSizes), false);
            UpdateButtons();
        }

        private static void SortEntries(List<FileManagerEntry> entries)
        {
            entries.Sort(delegate (FileManagerEntry a, FileManagerEntry b)
            {
                if (a.IsDirectory != b.IsDirectory)
                {
                    return a.IsDirectory ? -1 : 1;
                }
                return string.Compare(a.DisplayName ?? a.Name, b.DisplayName ?? b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        private FileManagerEntry SelectedEntry(Side side)
        {
            string key = side.Grid.GetSelectedValue("Key");
            int index;
            if (key == null || !int.TryParse(key, out index) || index < 0 || index >= side.Entries.Count)
            {
                return null;
            }
            return side.Entries[index];
        }

        // The selected row when it is a real file or folder, not the ".." row.
        private FileManagerEntry SelectedItem(Side side)
        {
            FileManagerEntry entry = SelectedEntry(side);
            return entry == null || entry.Name == ParentName ? null : entry;
        }

        private void SetStatus(Side side, string text, bool isError)
        {
            side.Status.Text = text;
            EventHandler<PageStatusEventArgs> handler = StatusMessage;
            if (handler != null && text.Length > 0)
            {
                handler(this, new PageStatusEventArgs(text, isError));
            }
        }

        private bool IsConsoleConnected()
        {
            return consoleService != null && consoleService.IsConnected;
        }

        private void SetConsoleEnabled()
        {
            console.Group.Enabled = IsConsoleConnected();
        }

        private void UpdateButtons()
        {
            FileManagerEntry localItem = SelectedItem(local);
            bool connected = IsConsoleConnected();
            bool folderOk = connected && consoleService.SupportsFolderTransfer;

            buttonUpload.Enabled = !busy && connected && localItem != null && (!localItem.IsDirectory || folderOk);
            buttonLocalDelete.Enabled = !busy && localItem != null;
            buttonLocalRename.Enabled = !busy && localItem != null;
            buttonLocalNewFolder.Enabled = !busy && local.CurrentPath.Length > 0;
            buttonLocalRefresh.Enabled = !busy;
            buttonLocalOpen.Enabled = local.CurrentPath.Length > 0 && Directory.Exists(local.CurrentPath);

            FileManagerEntry consoleItem = SelectedItem(console);
            buttonDownload.Enabled = !busy && connected && consoleItem != null && local.CurrentPath.Length > 0
                && (!consoleItem.IsDirectory || folderOk);
            buttonConsoleDelete.Enabled = !busy && consoleItem != null;
            buttonConsoleRename.Enabled = !busy && consoleItem != null;
            buttonConsoleNewFolder.Enabled = !busy && console.CurrentPath.Length > 0;
            buttonConsoleRefresh.Enabled = !busy && connected;

            buttonAddToModules.Visible = connected && consoleService.SupportsAddToModules;
            buttonAddToModules.Enabled = !busy && consoleItem != null && !consoleItem.IsDirectory
                && consoleItem.Name.EndsWith(".xex", StringComparison.OrdinalIgnoreCase);
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private static string EnsureTrailing(string path, string separator)
        {
            return path.EndsWith(separator, StringComparison.Ordinal) ? path : path + separator;
        }

        // ---- local half ----

        private void FillLocalDrives()
        {
            local.SuppressDrives = true;
            local.Drives.Items.Clear();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    local.Drives.Items.Add(drive.Name);
                }
            }
            catch (Exception ex)
            {
                SetStatus(local, "Could not list the drives: " + ex.Message, true);
            }
            local.SuppressDrives = false;
            SyncLocalDrive();
        }

        // Selects the drive that holds the shown folder, without listing it again (old code used Substring(0,2), a Windows-ism).
        private void SyncLocalDrive()
        {
            int best = -1;
            int bestLength = -1;
            for (int i = 0; i < local.Drives.Items.Count; i++)
            {
                string drive = local.Drives.Items[i] as string;
                if (drive != null && local.CurrentPath.StartsWith(drive, StringComparison.Ordinal) && drive.Length > bestLength)
                {
                    best = i;
                    bestLength = drive.Length;
                }
            }
            local.SuppressDrives = true;
            local.Drives.SelectedIndex = best;
            local.SuppressDrives = false;
        }

        /// <summary>Lists a local folder (ledger MW1-158). If it cannot be read, the nearest readable parent is shown.</summary>
        public void LoadLocalDirectory(string path)
        {
            string target;
            try
            {
                target = Path.GetFullPath(string.IsNullOrEmpty(path) ? DefaultLocalPath() : path);
            }
            catch (Exception ex)
            {
                SetStatus(local, "Invalid folder: " + ex.Message, true);
                return;
            }

            while (true)
            {
                try
                {
                    List<FileManagerEntry> entries = ReadLocal(target);
                    localLoaded = true;
                    ShowEntries(local, target, entries);
                    SyncLocalDrive();
                    EventHandler<ItemEventArgs<string>> handler = LocalPathChanged;
                    if (handler != null)
                    {
                        handler(this, new ItemEventArgs<string>(target));
                    }
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    SetStatus(local, "Access to " + target + " is denied.", true);
                    return;
                }
                catch (Exception ex)
                {
                    SetStatus(local, ex.Message, true);
                    DirectoryInfo parent = null;
                    try
                    {
                        parent = Directory.GetParent(target);
                    }
                    catch (Exception)
                    {
                        parent = null;
                    }
                    if (parent == null)
                    {
                        return;
                    }
                    target = parent.FullName;
                }
            }
        }

        private static List<FileManagerEntry> ReadLocal(string folder)
        {
            DirectoryInfo info = new DirectoryInfo(folder);
            if (!info.Exists)
            {
                throw new DirectoryNotFoundException("The folder does not exist: " + folder);
            }

            List<FileManagerEntry> entries = new List<FileManagerEntry>();
            foreach (DirectoryInfo directory in info.GetDirectories())
            {
                entries.Add(new FileManagerEntry { Name = directory.Name, IsDirectory = true, Modified = directory.LastWriteTime });
            }
            foreach (FileInfo file in info.GetFiles())
            {
                entries.Add(new FileManagerEntry { Name = file.Name, IsDirectory = false, Size = file.Length, Modified = file.LastWriteTime });
            }
            SortEntries(entries);
            if (info.Parent != null)
            {
                entries.Insert(0, new FileManagerEntry { Name = ParentName, IsDirectory = true });
            }
            return entries;
        }

        private void LocalDrives_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (local.SuppressDrives)
            {
                return;
            }
            string drive = local.Drives.SelectedItem as string;
            if (drive != null)
            {
                LoadLocalDirectory(drive);
            }
        }

        private void LocalPathBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadLocalDirectory(local.PathBox.Text.Trim());
            }
        }

        private void ButtonBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.ShowNewFolderButton = true;
                if (local.CurrentPath.Length > 0 && Directory.Exists(local.CurrentPath))
                {
                    dialog.SelectedPath = local.CurrentPath;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                {
                    LoadLocalDirectory(dialog.SelectedPath);
                }
            }
        }

        // Double-click: ".." goes up, a folder opens, a file does nothing (ledger MW1-161).
        private void LocalGrid_RowActivated(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedEntry(local);
            if (entry == null || !entry.IsDirectory)
            {
                return;
            }
            if (entry.Name == ParentName)
            {
                DirectoryInfo parent = Directory.GetParent(local.CurrentPath);
                if (parent != null)
                {
                    LoadLocalDirectory(parent.FullName);
                }
                return;
            }
            LoadLocalDirectory(Path.Combine(local.CurrentPath, entry.Name));
        }

        private void ButtonLocalRefresh_Click(object sender, EventArgs e)
        {
            LoadLocalDirectory(local.CurrentPath);
        }

        private void ButtonLocalOpen_Click(object sender, EventArgs e)
        {
            if (local.CurrentPath.Length == 0 || !Directory.Exists(local.CurrentPath))
            {
                return;
            }
            EventHandler<ItemEventArgs<string>> handler = OpenFolderRequested;
            if (handler != null)
            {
                handler(this, new ItemEventArgs<string>(local.CurrentPath));
            }
        }

        // Delete (ledger MW1-167, MW2-002).
        private void ButtonLocalDelete_Click(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedItem(local);
            if (entry == null)
            {
                return;
            }
            string kind = entry.IsDirectory ? "folder" : "file";
            DialogResult answer = MessageBox.Show(this,
                "Delete the " + kind + " \"" + entry.Name + "\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            string full = Path.Combine(local.CurrentPath, entry.Name);
            try
            {
                if (entry.IsDirectory)
                {
                    Directory.Delete(full, true);
                }
                else
                {
                    File.Delete(full);
                }
                LoadLocalDirectory(local.CurrentPath);
                SetStatus(local, "Deleted " + entry.Name, false);
            }
            catch (Exception ex)
            {
                SetStatus(local, "Could not delete " + entry.Name + ": " + ex.Message, true);
            }
        }

        // Rename (ledger MW1-168, MW2-003, MW2-004).
        private void ButtonLocalRename_Click(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedItem(local);
            if (entry == null)
            {
                return;
            }
            string input = TextPromptForm.Show(this, "Rename", "New name:", entry.Name);
            if (input == null)
            {
                return;
            }
            string newName = PageUi.CleanFileName(input);
            if (newName.Length == 0 || newName == entry.Name)
            {
                return;
            }
            string oldFull = Path.Combine(local.CurrentPath, entry.Name);
            string newFull = Path.Combine(local.CurrentPath, newName);
            if (File.Exists(newFull) || Directory.Exists(newFull))
            {
                MessageBox.Show(this, "\"" + newName + "\" already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                if (entry.IsDirectory)
                {
                    Directory.Move(oldFull, newFull);
                }
                else
                {
                    File.Move(oldFull, newFull);
                }
                LoadLocalDirectory(local.CurrentPath);
                SetStatus(local, "Renamed " + entry.Name + " to " + newName, false);
            }
            catch (Exception ex)
            {
                SetStatus(local, "Could not rename " + entry.Name + ": " + ex.Message, true);
            }
        }

        // New folder (ledger MW1-169, MW1-172).
        private void ButtonLocalNewFolder_Click(object sender, EventArgs e)
        {
            string input = TextPromptForm.Show(this, "New Folder", "Folder name:", string.Empty);
            if (input == null)
            {
                return;
            }
            string name = PageUi.CleanFileName(input);
            if (name.Length == 0)
            {
                return;
            }
            string full = Path.Combine(local.CurrentPath, name);
            if (File.Exists(full) || Directory.Exists(full))
            {
                MessageBox.Show(this, "\"" + name + "\" already exists.", "New Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Directory.CreateDirectory(full);
                LoadLocalDirectory(local.CurrentPath);
                SetStatus(local, "Created folder " + name, false);
            }
            catch (Exception ex)
            {
                SetStatus(local, "Could not create the folder: " + ex.Message, true);
            }
        }

        // ---- console half ----

        private void StartInitialConsoleLoad()
        {
            consoleLoaded = true;
            string start = consoleService.InitialPath;
            if (string.IsNullOrEmpty(start))
            {
                IList<string> drives = consoleService.GetDrives();
                start = drives != null && drives.Count > 0 ? drives[0] : string.Empty;
            }
            if (start.Length > 0)
            {
                LoadConsoleDirectory(start);
            }
        }

        /// <summary>Lists a console folder (ledger MW2-007). Errors go to the console status line.</summary>
        public async void LoadConsoleDirectory(string path)
        {
            if (!IsConsoleConnected() || string.IsNullOrEmpty(path))
            {
                return;
            }
            IConsoleFileService service = consoleService;
            string separator = service.PathSeparator;
            string folder = EnsureTrailing(path.Trim(), separator);
            int ticket = ++consoleTicket;
            SetStatus(console, "Loading " + folder + " ...", false);
            try
            {
                IList<FileManagerEntry> listed = await service.ListAsync(folder);
                if (ticket != consoleTicket)
                {
                    return;
                }
                List<FileManagerEntry> entries = new List<FileManagerEntry>();
                if (listed != null)
                {
                    entries.AddRange(listed);
                }
                SortEntries(entries);
                if (!IsConsoleRoot(folder, service))
                {
                    entries.Insert(0, new FileManagerEntry { Name = ParentName, IsDirectory = true });
                }
                ShowEntries(console, folder, entries);
                SyncConsoleDrive(folder);
                EventHandler<ItemEventArgs<string>> handler = ConsolePathChanged;
                if (handler != null)
                {
                    handler(this, new ItemEventArgs<string>(folder));
                }
            }
            catch (Exception ex)
            {
                if (ticket == consoleTicket)
                {
                    SetStatus(console, "Could not list " + folder + ": " + ex.Message, true);
                }
            }
        }

        private static bool IsConsoleRoot(string folder, IConsoleFileService service)
        {
            string separator = service.PathSeparator;
            if (folder == separator)
            {
                return true;
            }
            IList<string> drives = service.GetDrives();
            if (drives != null)
            {
                foreach (string drive in drives)
                {
                    if (string.Equals(EnsureTrailing(drive, separator), folder, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string ConsoleParent(string folder, string separator)
        {
            string trimmed = folder.TrimEnd(separator[0]);
            int index = trimmed.LastIndexOf(separator[0]);
            return index < 0 ? folder : trimmed.Substring(0, index + 1);
        }

        private static string ConsoleCombine(string folder, string name, string separator)
        {
            return EnsureTrailing(folder, separator) + name;
        }

        // Selects the drive that holds the shown folder without listing again (ledger MW2-007 "syncs drive combo without re-firing").
        private void SyncConsoleDrive(string folder)
        {
            int best = -1;
            int bestLength = -1;
            for (int i = 0; i < console.Drives.Items.Count; i++)
            {
                string drive = console.Drives.Items[i] as string;
                if (drive != null && folder.StartsWith(drive, StringComparison.OrdinalIgnoreCase) && drive.Length > bestLength)
                {
                    best = i;
                    bestLength = drive.Length;
                }
            }
            console.SuppressDrives = true;
            console.Drives.SelectedIndex = best;
            console.SuppressDrives = false;
        }

        private void ConsoleDrives_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (console.SuppressDrives)
            {
                return;
            }
            string drive = console.Drives.SelectedItem as string;
            if (drive != null)
            {
                LoadConsoleDirectory(drive);
            }
        }

        private void ButtonConsoleGo_Click(object sender, EventArgs e)
        {
            LoadConsoleDirectory(console.PathBox.Text);
        }

        private void ConsolePathBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadConsoleDirectory(console.PathBox.Text);
            }
        }

        // Double-click: ".." goes up, a folder opens (ledger MW2-010).
        private void ConsoleGrid_RowActivated(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedEntry(console);
            if (entry == null || !entry.IsDirectory || !IsConsoleConnected())
            {
                return;
            }
            string separator = consoleService.PathSeparator;
            if (entry.Name == ParentName)
            {
                LoadConsoleDirectory(ConsoleParent(console.CurrentPath, separator));
            }
            else
            {
                LoadConsoleDirectory(ConsoleCombine(console.CurrentPath, entry.Name, separator));
            }
        }

        private void ButtonConsoleRefresh_Click(object sender, EventArgs e)
        {
            LoadConsoleDirectory(console.CurrentPath);
        }

        // Runs one console operation with the buttons locked and any failure shown in the console status line.
        private async Task RunConsoleOperation(string name, Func<Task> operation)
        {
            if (busy || !IsConsoleConnected())
            {
                return;
            }
            busy = true;
            UpdateButtons();
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                SetStatus(console, name + " failed: " + ex.Message, true);
            }
            finally
            {
                busy = false;
                UpdateButtons();
            }
        }

        // Upload (ledger MW1-166, MW1-173, MW2-001).
        private async void ButtonUpload_Click(object sender, EventArgs e)
        {
            await UploadSelectedLocalAsync();
        }

        private async Task UploadSelectedLocalAsync()
        {
            FileManagerEntry entry = SelectedItem(local);
            if (entry == null || console.CurrentPath.Length == 0)
            {
                return;
            }
            string fullPath = Path.Combine(local.CurrentPath, entry.Name);
            string target = console.CurrentPath;
            await RunConsoleOperation("Upload", async delegate
            {
                SetStatus(console, "Uploading " + entry.Name + " ...", false);
                await consoleService.UploadAsync(fullPath, entry.IsDirectory, target);
                SetStatus(console, "Uploaded " + entry.Name, false);
                LoadConsoleDirectory(target);
            });
        }

        // Download (ledger MW2-014, MW2-020).
        private async void ButtonDownload_Click(object sender, EventArgs e)
        {
            await DownloadSelectedConsoleAsync();
        }

        private async Task DownloadSelectedConsoleAsync()
        {
            FileManagerEntry entry = SelectedItem(console);
            if (entry == null || local.CurrentPath.Length == 0)
            {
                return;
            }
            string separator = consoleService.PathSeparator;
            string remote = ConsoleCombine(console.CurrentPath, entry.Name, separator);
            string targetFolder = local.CurrentPath;
            await RunConsoleOperation("Download", async delegate
            {
                SetStatus(console, "Downloading " + entry.Name + " ...", false);
                await consoleService.DownloadAsync(remote, entry.IsDirectory, targetFolder);
                SetStatus(console, "Downloaded " + entry.Name, false);
                LoadLocalDirectory(targetFolder);
            });
        }

        // Delete (ledger MW2-015, MW2-021).
        private async void ButtonConsoleDelete_Click(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedItem(console);
            if (entry == null)
            {
                return;
            }
            string label = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            DialogResult answer = MessageBox.Show(this,
                "Delete \"" + label + "\" from the console?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            string remote = ConsoleCombine(console.CurrentPath, entry.Name, consoleService.PathSeparator);
            string folder = console.CurrentPath;
            await RunConsoleOperation("Delete", async delegate
            {
                await consoleService.DeleteAsync(remote, entry.IsDirectory);
                SetStatus(console, "Deleted " + label, false);
                LoadConsoleDirectory(folder);
            });
        }

        // Rename (ledger MW2-016, MW2-022, MW2-023).
        private async void ButtonConsoleRename_Click(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedItem(console);
            if (entry == null)
            {
                return;
            }
            string input = TextPromptForm.Show(this, "Rename", "New name:", entry.Name);
            if (input == null)
            {
                return;
            }
            string newName = PageUi.CleanFileName(input);
            if (newName.Length == 0 || newName == entry.Name)
            {
                return;
            }
            string separator = consoleService.PathSeparator;
            string folder = console.CurrentPath;
            string remote = ConsoleCombine(folder, entry.Name, separator);
            string newRemote = ConsoleCombine(folder, newName, separator);
            await RunConsoleOperation("Rename", async delegate
            {
                if (await consoleService.ExistsAsync(newRemote, entry.IsDirectory))
                {
                    MessageBox.Show(this, "\"" + newName + "\" already exists.", "Rename", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await consoleService.RenameAsync(remote, newName, entry.IsDirectory);
                SetStatus(console, "Renamed " + entry.Name + " to " + newName, false);
                LoadConsoleDirectory(folder);
            });
        }

        // New folder (ledger MW2-017, MW2-024).
        private async void ButtonConsoleNewFolder_Click(object sender, EventArgs e)
        {
            if (console.CurrentPath.Length == 0)
            {
                return;
            }
            string input = TextPromptForm.Show(this, "New Folder", "Folder name:", string.Empty);
            if (input == null)
            {
                return;
            }
            string name = PageUi.CleanFileName(input);
            if (name.Length == 0)
            {
                return;
            }
            string folder = console.CurrentPath;
            string remote = ConsoleCombine(folder, name, consoleService.PathSeparator);
            await RunConsoleOperation("New folder", async delegate
            {
                if (await consoleService.ExistsAsync(remote, true))
                {
                    MessageBox.Show(this, "\"" + name + "\" already exists.", "New Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                await consoleService.CreateFolderAsync(remote);
                SetStatus(console, "Created folder " + name, false);
                LoadConsoleDirectory(folder);
            });
        }

        // Add to Modules (ledger MW2-019): Xbox .xex files only.
        private void ButtonAddToModules_Click(object sender, EventArgs e)
        {
            FileManagerEntry entry = SelectedItem(console);
            if (entry == null || entry.IsDirectory || !IsConsoleConnected())
            {
                return;
            }
            string suggested = Path.GetFileNameWithoutExtension(entry.Name);
            string name = TextPromptForm.Show(this, "Add to Modules", "Module name:", suggested);
            if (name == null)
            {
                return;
            }
            AddToModulesEventArgs args = new AddToModulesEventArgs(name.Trim(), ConsoleCombine(console.CurrentPath, entry.Name, consoleService.PathSeparator));
            EventHandler<AddToModulesEventArgs> handler = AddToModulesRequested;
            if (handler != null)
            {
                handler(this, args);
            }
            if (!string.IsNullOrEmpty(args.Error))
            {
                MessageBox.Show(this, args.Error, "Add to Modules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetStatus(console, "Added " + args.Name + " to the modules", false);
        }

        // ---- drag and drop between the halves ----

        private void LocalGrid_ItemDragged(object sender, EventArgs e)
        {
            if (IsConsoleConnected() && SelectedItem(local) != null)
            {
                local.Grid.BeginDrag(new DataObject(DragFormat, new DragPayload(false)));
            }
        }

        private void ConsoleGrid_ItemDragged(object sender, EventArgs e)
        {
            if (IsConsoleConnected() && SelectedItem(console) != null)
            {
                console.Grid.BeginDrag(new DataObject(DragFormat, new DragPayload(true)));
            }
        }

        private static DragPayload PayloadOf(DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DragFormat))
            {
                return null;
            }
            return e.Data.GetData(DragFormat) as DragPayload;
        }

        // The local list accepts rows dragged from the console (download).
        private void LocalGrid_DragOver(object sender, DragEventArgs e)
        {
            DragPayload payload = PayloadOf(e);
            e.Effect = payload != null && payload.FromConsole && !busy ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void LocalGrid_DragDrop(object sender, DragEventArgs e)
        {
            DragPayload payload = PayloadOf(e);
            if (payload != null && payload.FromConsole)
            {
                await DownloadSelectedConsoleAsync();
            }
        }

        // The console list accepts rows dragged from the local list (upload).
        private void ConsoleGrid_DragOver(object sender, DragEventArgs e)
        {
            DragPayload payload = PayloadOf(e);
            e.Effect = payload != null && !payload.FromConsole && !busy && IsConsoleConnected() ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private async void ConsoleGrid_DragDrop(object sender, DragEventArgs e)
        {
            DragPayload payload = PayloadOf(e);
            if (payload != null && !payload.FromConsole)
            {
                await UploadSelectedLocalAsync();
            }
        }
    }
}
