using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArisenStudio.Forms.Tools.PS3;
using FluentFTP;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// <see cref="IPs3FileTransfer"/> over <see cref="FtpService"/>. Real FTP code; it connects to nothing by itself.
    /// The connection is the host's: <see cref="FtpService"/> takes its client from the same provider, and a missing
    /// connection fails with "There is no FTP connection to the console." Every method throws on failure, as the
    /// contract says. Paths are console paths with forward slashes.
    /// The listing needs the FluentFTP client itself (FtpService has no listing method that returns size and date),
    /// so the constructor also takes the client provider.
    /// </summary>
    public sealed class Ps3FileTransfer : IPs3FileTransfer
    {
        private readonly FtpService _ftp;
        private readonly Func<AsyncFtpClient> _clientProvider;

        /// <param name="ftp">The service that does uploads, downloads and deletes.</param>
        /// <param name="clientProvider">The same client provider that <paramref name="ftp"/> was built with.</param>
        public Ps3FileTransfer(FtpService ftp, Func<AsyncFtpClient> clientProvider)
        {
            if (ftp == null)
            {
                throw new ArgumentNullException("ftp");
            }

            if (clientProvider == null)
            {
                throw new ArgumentNullException("clientProvider");
            }

            _ftp = ftp;
            _clientProvider = clientProvider;
        }

        public Task<bool> FileExistsAsync(string consoleFile)
        {
            RequirePath(consoleFile, "consoleFile");
            return _ftp.FileExistsAsync(consoleFile);
        }

        public async Task DownloadFileAsync(string localFile, string consoleFile)
        {
            RequirePath(localFile, "localFile");
            RequirePath(consoleFile, "consoleFile");

            string localFolder = Path.GetDirectoryName(localFile);

            if (!string.IsNullOrEmpty(localFolder))
            {
                Directory.CreateDirectory(localFolder);
            }

            FtpStatus status = await _ftp.DownloadFileAsync(localFile, consoleFile);

            if (status == FtpStatus.Failed)
            {
                throw new IOException("The console file could not be downloaded: " + consoleFile);
            }
        }

        public async Task UploadFileAsync(string localFile, string consoleFile)
        {
            RequirePath(localFile, "localFile");
            RequirePath(consoleFile, "consoleFile");

            if (!File.Exists(localFile))
            {
                throw new FileNotFoundException("The local file does not exist: " + localFile, localFile);
            }

            FtpStatus status = await _ftp.UploadFileAsync(localFile, consoleFile);

            if (status == FtpStatus.Failed)
            {
                throw new IOException("The file could not be uploaded to the console: " + consoleFile);
            }
        }

        public Task DeleteFileAsync(string consoleFile)
        {
            RequirePath(consoleFile, "consoleFile");
            return _ftp.DeleteFileAsync(consoleFile);
        }

        public async Task<IList<Ps3FileEntry>> ListFilesAsync(string consoleFolder)
        {
            RequirePath(consoleFolder, "consoleFolder");

            AsyncFtpClient client = _clientProvider();

            if (client == null)
            {
                throw new InvalidOperationException("There is no FTP connection to the console.");
            }

            List<Ps3FileEntry> entries = new List<Ps3FileEntry>();

            foreach (FtpListItem item in await client.GetListing(consoleFolder))
            {
                if (item.Type != FtpObjectType.Directory && item.Type != FtpObjectType.File)
                {
                    continue;
                }

                if (item.Name == "." || item.Name == "..")
                {
                    continue;
                }

                entries.Add(new Ps3FileEntry
                {
                    Name = item.Name,
                    Size = item.Size < 0 ? 0 : item.Size,
                    Modified = item.Modified,
                    IsDirectory = item.Type == FtpObjectType.Directory
                });
            }

            return entries;
        }

        private static void RequirePath(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A path is required.", name);
            }
        }
    }
}
