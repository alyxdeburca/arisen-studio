using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ArisenStudio.Database;
using ArisenStudio.Models.Database;
using ArisenStudio.Models.Resources;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// What the transfer dialog was asked to do: the transfer kind and the items it works on. A snapshot of the
    /// properties the host set on <see cref="TransferDialog"/> (they are the same names the DevExpress dialog had).
    /// </summary>
    public class TransferRequest
    {
        public TransferType TransferType { get; set; }

        public SettingsData Settings { get; set; }

        /// <summary>
        /// The connected console, or null when none is connected.
        /// </summary>
        public ConsoleProfile ConsoleProfile { get; set; }

        public CategoryItem Category { get; set; }

        public ModItemData ModItem { get; set; }

        public PackageItemData PackageItem { get; set; }

        public AppItemData AppItem { get; set; }

        public GameSaveItemData GameSaveItem { get; set; }

        /// <summary>
        /// The game region picked by the host, used by the mod uninstall.
        /// </summary>
        public string GameRegion { get; set; }

        /// <summary>
        /// The download file the user picked on the details dialog. When null, a mod with one download file uses that one.
        /// </summary>
        public DownloadFiles DownloadFiles { get; set; }

        public AppItemFile AppItemFile { get; set; }

        public CustomItemData CustomMod { get; set; }

        public TrainerGameItem TrainerGame { get; set; }

        public TrainerItem TrainerItem { get; set; }

        public bool IsCustom { get; set; }
    }

    /// <summary>
    /// What a transfer operation can do to the dialog. <see cref="TransferDialog"/> implements this. Every member is
    /// safe to call from a worker thread: the dialog marshals to the UI thread itself.
    /// </summary>
    public interface ITransferHost
    {
        /// <summary>
        /// Progress bar percentage (0-100).
        /// </summary>
        IProgress<int> Progress { get; }

        /// <summary>
        /// Set when the user clicks Cancel.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// The folder the Open Folder button opens. The button shows only when this folder exists and the transfer succeeded.
        /// </summary>
        string LocalPath { get; set; }

        /// <summary>
        /// Set to true when the transfer finished without error.
        /// </summary>
        bool IsSuccessful { get; set; }

        /// <summary>
        /// Show a line in the status label and write it to the log. Pass the exception to log it as an error.
        /// </summary>
        void UpdateStatus(string status, Exception exception = null);

        /// <summary>
        /// Set the two-line header (category on the first line, item on the second). Ampersands are doubled for the label.
        /// </summary>
        void SetHeader(string firstLine, string secondLine);

        /// <summary>
        /// Show a message box on the UI thread and return the answer.
        /// </summary>
        DialogResult Ask(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon);

        /// <summary>
        /// The text for a language key, or <paramref name="fallback"/> (English) when there is no language source or no value.
        /// </summary>
        string GetText(string key, string fallback);

        /// <summary>
        /// The text of the transfer-size label under the status (for example "1.2 MB / 5.0 MB").
        /// </summary>
        void SetSizeText(string text);

        /// <summary>
        /// Show or hide the transfer-size label.
        /// </summary>
        void SetSizeVisible(bool visible);
    }

    /// <summary>
    /// The transfers that need a console: FTP uploads and deletes, the Xbox 360 file calls, WebMAN popups, and the
    /// installed-mods bookkeeping. This code is not ported yet, so the dialog reaches it through this interface.
    /// <see cref="TransferConsoleOperationsStub"/> is the placeholder. Each method does the work of the DevExpress
    /// dialog method of the same name, reports through <paramref name="host"/>, and sets
    /// <see cref="ITransferHost.IsSuccessful"/> when it succeeds. Use <c>ArisenStudio.Services.Transfers</c> for the
    /// path and text helpers.
    /// </summary>
    public interface ITransferConsoleOperations
    {
        /// <summary>Original: InstallCustom.</summary>
        Task InstallCustom(TransferRequest request, ITransferHost host);

        /// <summary>Original: UninstallCustom.</summary>
        Task UninstallCustom(TransferRequest request, ITransferHost host);

        /// <summary>Original: InstallMods.</summary>
        Task InstallMods(TransferRequest request, ITransferHost host);

        /// <summary>Original: UninstallMods (the region comes from <see cref="TransferRequest.GameRegion"/>).</summary>
        Task UninstallMods(TransferRequest request, ITransferHost host);

        /// <summary>Original: InstallPackageFile.</summary>
        Task InstallPackage(TransferRequest request, ITransferHost host);

        /// <summary>Original: UninstallPackageFile.</summary>
        Task UninstallPackage(TransferRequest request, ITransferHost host);

        /// <summary>Original: InstallApplication.</summary>
        Task InstallApplication(TransferRequest request, ITransferHost host);

        /// <summary>Original: UninstallApplication.</summary>
        Task UninstallApplication(TransferRequest request, ITransferHost host);

        /// <summary>Original: InstallGameSave.</summary>
        Task InstallGameSave(TransferRequest request, ITransferHost host);

        /// <summary>Original: InstallTrainer.</summary>
        Task InstallTrainer(TransferRequest request, ITransferHost host);
    }
}
