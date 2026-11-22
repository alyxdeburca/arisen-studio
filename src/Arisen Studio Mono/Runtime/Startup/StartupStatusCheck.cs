using ArisenStudio.Constants;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArisenStudio.Runtime.Startup
{
    /// <summary>
    /// Asks the server status URL whether the database server is up.
    /// Ported from MainWindow.CheckDatabaseStatus (old source: Forms/Windows/MainWindow.cs:339-366).
    /// No UI and no settings access.
    /// </summary>
    public static class StartupStatusCheck
    {
        /// <summary>
        /// User-Agent sent with the status request (same string as the old app).
        /// </summary>
        public const string UserAgent = "ArisenStudio-CheckDatabaseStatus";

        /// <summary>
        /// Default request timeout. Matches the old app, which used the HttpClient default of 100 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Returns true only when the status body equals "OK" (ignoring case, no trimming).
        /// Returns false on a non-success HTTP status, a timeout, a cancelled token, or any other exception,
        /// the same as the old catch-all.
        /// </summary>
        /// <param name="timeout">Request timeout. Null uses <see cref="DefaultTimeout"/>.</param>
        /// <param name="cancellationToken">Cancels the request. A cancelled request returns false.</param>
        /// <param name="logError">Optional sink for the "not OK" and exception messages. May be null.</param>
        public static async Task<bool> IsServerUpAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken), Action<string> logError = null)
        {
            using (HttpClient client = new HttpClient { Timeout = timeout ?? DefaultTimeout })
            {
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

                    using (HttpResponseMessage response = await client.GetAsync(Urls.StatusCheck, cancellationToken).ConfigureAwait(false))
                    {
                        // GetStringAsync threw on a non-success status in the old code, so do the same here.
                        response.EnsureSuccessStatusCode();

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (string.Equals(body, "OK", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        logError?.Invoke("Database status not OK.");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    logError?.Invoke("Unable to load database status: " + ex.Message);
                    return false;
                }
            }
        }
    }
}
