using System.Threading.Tasks;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// Placeholder for <see cref="ITransferConsoleOperations"/> until the console and FTP transfer code is ported.
    /// Every method shows one status line, resets the progress bar and leaves <see cref="ITransferHost.IsSuccessful"/>
    /// false. It installs and uninstalls nothing and touches no console.
    /// </summary>
    public class TransferConsoleOperationsStub : ITransferConsoleOperations
    {
        public Task InstallCustom(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task UninstallCustom(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task InstallMods(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task UninstallMods(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task InstallPackage(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task UninstallPackage(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task InstallApplication(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task UninstallApplication(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task InstallGameSave(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        public Task InstallTrainer(TransferRequest request, ITransferHost host)
        {
            return NotAvailable(host);
        }

        private static Task NotAvailable(ITransferHost host)
        {
            host.Progress.Report(0);
            host.IsSuccessful = false;
            host.UpdateStatus(host.GetText("TRANSFER_CONSOLE_NOT_AVAILABLE", "Installing to and removing from a console is not available in this build yet."));
            return Task.FromResult(0);
        }
    }
}
