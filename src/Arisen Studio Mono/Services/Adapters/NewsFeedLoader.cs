using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ArisenStudio.Forms.Dashboard;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Managed replacement for the old <c>MainWindow.LoadNewsFeed</c>, which used <c>FeedReader.ReadAsync(Urls.RssFeedData)</c>
    /// (CodeHollow.FeedReader, not referenced by the Mono project). This class downloads the feed with <see cref="HttpClient"/>
    /// and parses RSS 2.0 (channel/item) or Atom (entry) with System.Xml.Linq.
    /// <para>
    /// Constructing this class makes no network call; only <see cref="LoadAsync"/> does. <see cref="LoadAsync"/> never throws:
    /// on any failure it returns an empty list and sets <see cref="StatusMessage"/> and <see cref="LastError"/>.
    /// </para>
    /// Normal mode only: <c>FullShellIntegration</c> creates this loader only when it is not in smoke mode.
    /// </summary>
    public sealed class NewsFeedLoader
    {
        /// <summary>The old <c>Constants/Urls.cs</c> RssFeedData address (old source line 67).</summary>
        public const string FeedUrl = "https://db.arisen.studio/app/news.xml";

        private const string FailedMessage = "The news feed could not be loaded. Please try again later.";
        private const string EmptyMessage = "There is no news right now.";

        private readonly HttpClient _httpClient;

        public NewsFeedLoader()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ArisenStudio");
        }

        /// <summary>Developer-neutral status for the last load: null after a successful load with items.</summary>
        public string StatusMessage { get; private set; }

        /// <summary>The exception behind <see cref="StatusMessage"/>, or null. For the log only.</summary>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Downloads and parses the feed. Entries come back in the order the old code added them to the panel
        /// (the feed reversed, MainWindow.cs:1854). Never throws.
        /// </summary>
        public async Task<IList<NewsEntry>> LoadAsync()
        {
            StatusMessage = null;
            LastError = null;

            try
            {
                using (HttpResponseMessage response = await _httpClient.GetAsync(FeedUrl).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        List<NewsEntry> entries = Parse(stream);

                        if (entries.Count == 0)
                        {
                            StatusMessage = EmptyMessage;
                        }

                        return entries;
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex;
                StatusMessage = FailedMessage;
                return new List<NewsEntry>();
            }
        }

        /// <summary>
        /// Parses RSS 2.0 or Atom. DTDs are refused. The result is reversed, as the old code reversed the feed.
        /// Throws on malformed XML; <see cref="LoadAsync"/> catches it.
        /// </summary>
        internal static List<NewsEntry> Parse(Stream stream)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            XDocument document;
            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                document = XDocument.Load(reader);
            }

            List<NewsEntry> entries = new List<NewsEntry>();

            if (document.Root == null)
            {
                return entries;
            }

            // RSS items are "item", Atom entries are "entry". Matching on the local name ignores namespaces.
            foreach (XElement item in document.Descendants().Where(e => e.Name.LocalName == "item" || e.Name.LocalName == "entry"))
            {
                entries.Add(new NewsEntry
                {
                    Title = ChildText(item, "title"),
                    Description = ChildText(item, "description") ?? ChildText(item, "summary") ?? ChildText(item, "content"),
                    Published = ParseDate(ChildText(item, "pubDate") ?? ChildText(item, "published") ?? ChildText(item, "updated"))
                });
            }

            entries.Reverse();
            return entries;
        }

        private static string ChildText(XElement parent, string localName)
        {
            XElement child = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
            return child == null ? null : child.Value.Trim();
        }

        // RSS uses RFC 822 dates, Atom uses RFC 3339. Both are read as UTC and shown in local time.
        // A missing or unreadable date gives DateTime.MinValue (the old code would have thrown here).
        private static DateTime ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return DateTime.MinValue;
            }

            DateTime utc;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
            {
                return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
            }

            return DateTime.MinValue;
        }
    }
}
