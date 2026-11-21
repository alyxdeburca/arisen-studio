using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ArisenStudio.Models.GameData.PS3;
using Humanizer;

namespace ArisenStudio.Extensions
{
    internal static class HttpExtensions
    {
        /// <summary>
        /// Initialize new http web request
        /// </summary>
        /// <param name="requestUriString"> File URL </param>
        /// <param name="httpMethod"> Method for the request </param>
        /// <param name="allowAutoRedirect"> Whether request should follow redirection responses </param>
        /// <param name="contentType"> Sets content-type http header </param>
        /// <returns> Returns a new HTTP Web Request to Get Response from file </returns>
        public static HttpWebRequest GetRequest(string requestUriString, string httpMethod = "GET", bool allowAutoRedirect = true, string contentType = "text/plain")
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUriString);
            request.UserAgent = @"Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
            request.ContentType = contentType;
            request.Timeout = Convert.ToInt32(new TimeSpan(0, 5, 0).TotalMilliseconds);
            request.AllowAutoRedirect = allowAutoRedirect;
            request.Method = httpMethod;
            return request;
        }

        /// <summary>
        /// </summary>
        /// <param name="url"> URL to get stream </param>
        /// <returns></returns>
        public static Stream GetStream(string url)
        {
            return ((HttpWebResponse)GetRequest(url).GetResponse()).GetResponseStream();
        }

        /// <summary>
        /// </summary>
        /// <param name="titleId">  </param>
        /// <param name="logger"> Optional error logger. Null means errors are not logged. </param>
        /// <returns> </returns>
        public static TitlePatch GetGameUpdatesFromTitleId(string titleId, Action<Exception, string> logger = null)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                using (WebClient webClient = new WebClient())
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(TitlePatch));
                    string titlePath = webClient.DownloadString("https://a0.ww.np.dl.playstation.net/tpl/np/" + titleId + "/" + titleId + "-ver.xml");
                    using (TextReader textReader = new StringReader(titlePath))
                    {
                        return (TitlePatch)serializer.Deserialize(textReader);
                    }
                }
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger(ex, $"Unable to fetch game update for title ID: {titleId} or it wasn't recognized. Error Message: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Downloads <paramref name="url"/> to <paramref name="filePath"/>.
        /// UI-free: the caller supplies the delegates and marshals them to the UI thread if needed.
        /// </summary>
        /// <param name="url"> Source URL </param>
        /// <param name="filePath"> Destination file path </param>
        /// <param name="progress"> Receives the percentage (0-100) as the download progresses </param>
        /// <param name="reportText"> Receives the progress text, e.g. "1.2 MB / 5.0 MB". May be null. </param>
        /// <param name="setVisible"> Receives true when progress starts and false when the download ends. May be null. </param>
        /// <param name="cancellationToken"> Cancels the download </param>
        public static async Task<bool> DownloadFileAsync(string url, string filePath, IProgress<int> progress = null, Action<string> reportText = null, Action<bool> setVisible = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            using (WebClient webClient = new WebClient())
            {
                var tcs = new TaskCompletionSource<bool>();

                // Register the cancellation token to cancel the download
                cancellationToken.Register(() => webClient.CancelAsync());

                // Only signal visibility once some download progress has been made
                bool hasProgressStarted = false;

                webClient.DownloadProgressChanged += (sender, e) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    long bytesDownloaded = e.BytesReceived;
                    long totalBytes = e.TotalBytesToReceive;
                    string progressMessage = $"{ByteSizeExtensions.Bytes(bytesDownloaded).Humanize()} / {ByteSizeExtensions.Bytes(totalBytes).Humanize()}";

                    // Report progress
                    if (progress != null)
                    {
                        progress.Report(e.ProgressPercentage);
                    }

                    if (setVisible != null && !hasProgressStarted && e.ProgressPercentage > 0)
                    {
                        hasProgressStarted = true;
                        setVisible(true);
                    }

                    if (reportText != null)
                    {
                        reportText(progressMessage);
                    }
                };

                webClient.DownloadFileCompleted += (sender, e) =>
                {
                    if (e.Error == null && !e.Cancelled)
                    {
                        // Download succeeded
                        tcs.SetResult(true);
                    }
                    else if (e.Cancelled)
                    {
                        // Download was cancelled
                        tcs.SetCanceled();
                    }
                    else
                    {
                        // Download failed due to an error
                        tcs.SetException(e.Error);
                    }

                    if (setVisible != null)
                    {
                        setVisible(false);
                    }
                };

                try
                {
                    webClient.DownloadFileAsync(new Uri(url), filePath);
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    // Handle cancellation case
                    return false;
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    throw new Exception("There was an issue trying to downloading the file. Error: " + ex.Message, ex);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="url"> </param>
        /// <returns> </returns>
        public static Bitmap GetImageFromUrl(string url)
        {
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            return new Bitmap(responseStream);
        }

        public static bool CheckInternetConnection()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(x => x.OperationalStatus == OperationalStatus.Up);
        }
    }
}
