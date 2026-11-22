using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using ArisenStudio.Services.Transfers;
using Humanizer;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// The install, uninstall and download progress dialog. Port of the DevExpress <c>TransferDialog</c>.
    /// Set <see cref="TransferType"/> and the item properties (the same names the old dialog had), set
    /// <see cref="Language"/>, <see cref="Settings"/> and <see cref="ConsoleProfile"/>, then ShowDialog. The transfer
    /// starts when the dialog loads.
    /// <para>
    /// The five download transfers are real (<see cref="TransferDownloadOperations"/>). The install and uninstall
    /// transfers need console and FTP code that is not ported, so they go through
    /// <see cref="ConsoleOperations"/>, which is <see cref="TransferConsoleOperationsStub"/> until the host sets a real one.
    /// </para>
    /// </summary>
    public class TransferDialog : Form, ITransferHost
    {
        private readonly Label _labelModName = new Label();
        private readonly ProgressBar _progressBarStatus = new ProgressBar();
        private readonly Label _labelStatus = new Label();
        private readonly Label _labelStatusSize = new Label();
        private readonly Button _buttonOpenFolder = new Button();
        private readonly Button _buttonCancel = new Button();

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private IProgress<int> _progress;
        private bool _isTransferInProgress;

        public TransferDialog()
        {
            InitializeControls();
        }

        /// <summary>
        /// Was the old main window's Settings. Defaults to <see cref="AppHost.Settings"/>.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// The connected console, or null. The host sets this; there is no main window to read it from.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        /// <summary>
        /// The language source. Null shows the English fallback text.
        /// </summary>
        public ResourceManager Language { get; set; }

        public TransferType TransferType { get; set; }

        public CategoryItem Category { get; set; }

        public ModItemData ModItem { get; set; }

        public PackageItemData PackageItem { get; set; }

        public AppItemData AppItem { get; set; }

        public GameSaveItemData GameSaveItem { get; set; }

        public string GameRegion { get; set; }

        public DownloadFiles DownloadFiles { get; set; }

        public AppItemFile AppItemFile { get; set; }

        public CustomItemData CustomMod { get; set; }

        public TrainerGameItem TrainerGame { get; set; }

        public TrainerItem TrainerItem { get; set; }

        public bool IsCustom { get; set; }

        /// <summary>
        /// Gives the title for an Xbox 360 title id, for the trainer header. The old dialog read it from
        /// <c>MainWindow.Database.TitleIdsX360.GetTitleFromTitleId</c>. When null the header shows the title id.
        /// </summary>
        public Func<string, string> TitleIdToTitle { get; set; }

        /// <summary>
        /// The install and uninstall code. Replace it before ShowDialog when the console transfer code is ported.
        /// </summary>
        public ITransferConsoleOperations ConsoleOperations { get; set; } = new TransferConsoleOperationsStub();

        /// <summary>
        /// The folder Open Folder opens.
        /// </summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// True when the transfer finished without error. Read it after the dialog closes.
        /// </summary>
        public bool IsSuccessful { get; set; }

        public IProgress<int> Progress
        {
            get { return _progress; }
        }

        public CancellationToken CancellationToken
        {
            get { return _cts.Token; }
        }

        private void InitializeControls()
        {
            SuspendLayout();

            // Same size, positions and anchors as the designer file. Stock fonts and colours.
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(554, 191);
            ControlBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Transfer Dialog";

            _labelModName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _labelModName.AutoSize = false;
            _labelModName.Font = new Font(Font, FontStyle.Bold);
            _labelModName.Location = new Point(12, 12);
            _labelModName.Name = "LabelModName";
            _labelModName.Size = new Size(530, 50);
            _labelModName.TabIndex = 0;
            _labelModName.Text = "Category\r\nMod Name";
            _labelModName.TextAlign = ContentAlignment.TopCenter;

            _progressBarStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _progressBarStatus.Location = new Point(12, 65);
            _progressBarStatus.Maximum = 100;
            _progressBarStatus.Minimum = 0;
            _progressBarStatus.Name = "ProgressBarStatus";
            _progressBarStatus.Size = new Size(530, 20);
            _progressBarStatus.TabIndex = 1;
            _progressBarStatus.TabStop = false;
            _progressBarStatus.Value = 1;

            _labelStatus.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _labelStatus.AutoEllipsis = true;
            _labelStatus.AutoSize = false;
            _labelStatus.Location = new Point(12, 91);
            _labelStatus.Name = "LabelStatus";
            _labelStatus.Size = new Size(530, 52);
            _labelStatus.TabIndex = 2;
            _labelStatus.Text = "Preparing...\r\n";
            _labelStatus.TextAlign = ContentAlignment.TopLeft;

            _labelStatusSize.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _labelStatusSize.AutoSize = false;
            _labelStatusSize.Location = new Point(12, 154);
            _labelStatusSize.Name = "LabelStatusSize";
            _labelStatusSize.Size = new Size(346, 24);
            _labelStatusSize.TabIndex = 3;
            _labelStatusSize.TextAlign = ContentAlignment.MiddleLeft;
            _labelStatusSize.Visible = false;

            _buttonOpenFolder.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _buttonOpenFolder.Location = new Point(364, 154);
            _buttonOpenFolder.Name = "ButtonOpenFolder";
            _buttonOpenFolder.Size = new Size(97, 25);
            _buttonOpenFolder.TabIndex = 4;
            _buttonOpenFolder.Text = "Open Folder";
            _buttonOpenFolder.UseVisualStyleBackColor = true;
            _buttonOpenFolder.Visible = false;
            _buttonOpenFolder.Click += ButtonOpenFolder_Click;

            _buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _buttonCancel.Location = new Point(467, 154);
            _buttonCancel.Name = "ButtonCancel";
            _buttonCancel.Size = new Size(75, 25);
            _buttonCancel.TabIndex = 5;
            _buttonCancel.Text = "Cancel";
            _buttonCancel.UseVisualStyleBackColor = true;
            _buttonCancel.Click += ButtonCancel_Click;

            Controls.Add(_labelStatusSize);
            Controls.Add(_progressBarStatus);
            Controls.Add(_buttonOpenFolder);
            Controls.Add(_labelStatus);
            Controls.Add(_buttonCancel);
            Controls.Add(_labelModName);

            ResumeLayout(false);
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                await LoadTransferFilesAsync();
            }
            catch (Exception ex)
            {
                // An exception out of an async void handler would end the application. Show it instead.
                UpdateStatus(ex.Message, ex);
            }
        }

        private async Task LoadTransferFilesAsync()
        {
            Text = GetText(TransferType.Humanize(), HumanizeTransferType(TransferType));

            _buttonOpenFolder.Text = GetText("OPEN_FOLDER", "Open Folder");
            _buttonCancel.Text = GetText("LABEL_CANCEL", "Cancel");

            _labelModName.Text = BuildHeader();

            await TransferFilesAsync();
        }

        /// <summary>
        /// The two-line header for the transfer kind, as the old LoadTransferFilesAsync built it.
        /// </summary>
        private string BuildHeader()
        {
            string current = _labelModName.Text;

            if (TransferType == TransferType.InstallMods || TransferType == TransferType.UninstallMods || TransferType == TransferType.DownloadMods)
            {
                return TransferStatusText.HeaderLabel(Category?.Title, ModItem?.Name);
            }

            if (TransferType == TransferType.InstallPackage || TransferType == TransferType.UninstallPackage || TransferType == TransferType.DownloadPackage)
            {
                return TransferStatusText.HeaderLabelWithDetail(PackageItem?.Category, PackageItem?.Name, PackageItem?.Region);
            }

            if (TransferType == TransferType.InstallApplication || TransferType == TransferType.UninstallApplication || TransferType == TransferType.DownloadApplication)
            {
                // The old dialog read PackageItem.Category here, which is null for an application and threw.
                // The category title (or the package category when only that is set) is used instead.
                string categoryText = Category != null ? Category.Title : (PackageItem != null ? PackageItem.Category : string.Empty);
                return TransferStatusText.HeaderLabelWithDetail(categoryText, AppItem?.Name, AppItem?.Version);
            }

            if (TransferType == TransferType.InstallGameSave || TransferType == TransferType.DownloadGameSave)
            {
                return TransferStatusText.HeaderLabel(Category?.Title, GameSaveItem?.Name);
            }

            if (TransferType == TransferType.InstallTrainer || TransferType == TransferType.DownloadTrainer)
            {
                string titleId = TrainerGame?.TitleId;
                string title = TitleIdToTitle != null ? TitleIdToTitle(titleId) : null;
                return TransferStatusText.HeaderLabel(string.IsNullOrEmpty(title) ? titleId : title, TrainerItem?.Name);
            }

            // Custom mods set their own header when they start (see ITransferConsoleOperations).
            return current;
        }

        /// <summary>
        /// Runs the transfer. A second call while one is running does nothing, as before.
        /// </summary>
        public async Task TransferFilesAsync()
        {
            if (_isTransferInProgress)
            {
                // Prevent re-entrance if a transfer is already in progress
                return;
            }

            _isTransferInProgress = true;

            try
            {
                // Created on the UI thread, so its reports are posted back to the UI thread.
                _progress = new System.Progress<int>(SetProgressValue);

                TransferRequest request = BuildRequest();

                await Task.Run(() => RunTransfer(request));
            }
            catch (Exception ex)
            {
                _progress?.Report(0);
                UpdateStatus(ex.Message, ex);
            }
            finally
            {
                _isTransferInProgress = false;

                // Update UI elements after completion
                OnUiThread(() =>
                {
                    _buttonCancel.Text = GetText("LABEL_CLOSE", "Close");
                    _buttonCancel.Visible = true;
                    _ = _buttonCancel.Focus();

                    _buttonOpenFolder.Visible = !string.IsNullOrEmpty(LocalPath) && Directory.Exists(LocalPath) && IsSuccessful;
                });
            }
        }

        private TransferRequest BuildRequest()
        {
            return new TransferRequest
            {
                TransferType = TransferType,
                Settings = Settings,
                ConsoleProfile = ConsoleProfile,
                Category = Category,
                ModItem = ModItem,
                PackageItem = PackageItem,
                AppItem = AppItem,
                GameSaveItem = GameSaveItem,
                GameRegion = GameRegion,
                DownloadFiles = DownloadFiles,
                AppItemFile = AppItemFile,
                CustomMod = CustomMod,
                TrainerGame = TrainerGame,
                TrainerItem = TrainerItem,
                IsCustom = IsCustom
            };
        }

        private Task RunTransfer(TransferRequest request)
        {
            ITransferConsoleOperations console = ConsoleOperations ?? new TransferConsoleOperationsStub();

            switch (request.TransferType)
            {
                case TransferType.InstallCustom:
                    return console.InstallCustom(request, this);
                case TransferType.UninstallCustom:
                    return console.UninstallCustom(request, this);
                case TransferType.InstallMods:
                    return console.InstallMods(request, this);
                case TransferType.UninstallMods:
                    return console.UninstallMods(request, this);
                case TransferType.DownloadMods:
                    return TransferDownloadOperations.DownloadMods(request, this);
                case TransferType.InstallPackage:
                    return console.InstallPackage(request, this);
                case TransferType.UninstallPackage:
                    return console.UninstallPackage(request, this);
                case TransferType.DownloadPackage:
                    return TransferDownloadOperations.DownloadPackage(request, this);
                case TransferType.InstallApplication:
                    return console.InstallApplication(request, this);
                case TransferType.UninstallApplication:
                    return console.UninstallApplication(request, this);
                case TransferType.DownloadApplication:
                    return TransferDownloadOperations.DownloadApplication(request, this);
                case TransferType.InstallGameSave:
                    return console.InstallGameSave(request, this);
                case TransferType.DownloadGameSave:
                    return TransferDownloadOperations.DownloadGameSave(request, this);
                case TransferType.InstallTrainer:
                    return console.InstallTrainer(request, this);
                case TransferType.DownloadTrainer:
                    return TransferDownloadOperations.DownloadTrainer(request, this);
                default:
                    return Task.FromResult(0);
            }
        }

        private void SetProgressValue(int value)
        {
            _progressBarStatus.Value = Math.Max(_progressBarStatus.Minimum, Math.Min(_progressBarStatus.Maximum, value));
        }

        private void ButtonOpenFolder_Click(object sender, EventArgs e)
        {
            try
            {
                _ = Process.Start(LocalPath);
            }
            catch (Exception ex)
            {
                UpdateStatus(ex.Message, ex);
            }
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            // The button reads Close once the transfer is over, so only cancel while it is still running.
            if (_isTransferInProgress)
            {
                _cts.Cancel();
                UpdateStatus(GetText("TRANSFER_CANCELED_BY_USER", "Download was canceled by user"));
            }

            Close();
        }

        /// <summary>
        /// Show a line in the status label and write it to the trace log (the old dialog used Program.Log).
        /// </summary>
        public void UpdateStatus(string status, Exception exception = null)
        {
            OnUiThread(() => _labelStatus.Text = status);

            if (exception == null)
            {
                Trace.TraceInformation(status);
            }
            else
            {
                Trace.TraceError(status + Environment.NewLine + exception);
            }
        }

        public void SetHeader(string firstLine, string secondLine)
        {
            string text = TransferStatusText.HeaderLabel(firstLine, secondLine);
            OnUiThread(() => _labelModName.Text = text);
        }

        public DialogResult Ask(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return buttons == MessageBoxButtons.YesNo ? DialogResult.No : DialogResult.Cancel;
            }

            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Func<DialogResult>(() => Ask(text, caption, buttons, icon)));
            }

            return MessageBox.Show(this, text, caption, buttons, icon);
        }

        public string GetText(string key, string fallback)
        {
            if (Language == null || string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            try
            {
                string value = Language.GetString(key);
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            catch (MissingManifestResourceException)
            {
                return fallback;
            }
        }

        public void SetSizeText(string text)
        {
            OnUiThread(() => _labelStatusSize.Text = text);
        }

        public void SetSizeVisible(bool visible)
        {
            OnUiThread(() => _labelStatusSize.Visible = visible);
        }

        /// <summary>
        /// Run an action on the UI thread. Does nothing once the dialog is closed, because a transfer can still be
        /// finishing on a worker thread when the user clicks Cancel.
        /// </summary>
        private void OnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    _ = BeginInvoke(action);
                }
                catch (InvalidOperationException)
                {
                    // The handle went away between the check and the call.
                }
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// English words for a transfer kind ("InstallMods" becomes "Install Mods"), for the title when no language is set.
        /// </summary>
        private static string HumanizeTransferType(TransferType transferType)
        {
            string name = transferType.ToString();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    builder.Append(' ');
                }

                builder.Append(name[i]);
            }

            return builder.ToString();
        }
    }
}
