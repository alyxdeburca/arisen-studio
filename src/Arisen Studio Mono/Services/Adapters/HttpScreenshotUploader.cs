using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using ArisenStudio.Forms.Tools;
using Newtonsoft.Json.Linq;

namespace ArisenStudio.Services.Adapters
{
    /// <summary>
    /// Where and how to upload a screenshot. The host fills this from the user's own settings.
    /// Nothing here is a default: the old Imgur client id and secret are NOT copied into this port, so an upload
    /// service has to be chosen and its credentials supplied by the user.
    /// </summary>
    public sealed class ScreenshotUploadOptions
    {
        /// <summary>The https address that receives the upload (a POST with multipart form data). Null or empty: uploads are off.</summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// The full value of the Authorization header (for Imgur that is "Client-ID " plus the user's own client id).
        /// Null or empty sends no Authorization header.
        /// </summary>
        public string AuthorizationHeader { get; set; }

        /// <summary>Name of the form field that holds the image file. Default "image".</summary>
        public string ImageFieldName { get; set; }

        /// <summary>Name of the form field that holds the title. Default "title". Set to empty to send no title.</summary>
        public string TitleFieldName { get; set; }

        /// <summary>Dotted path to the image link in the JSON answer. Default "data.link" (the Imgur answer shape).</summary>
        public string LinkPath { get; set; }

        /// <summary>How long to wait for the answer. Default 60 seconds.</summary>
        public TimeSpan Timeout { get; set; }
    }

    /// <summary>
    /// <see cref="IScreenshotUploader"/> over a plain <see cref="HttpClient"/>: no WPF, no Imgur.API package, no built-in
    /// credentials. Real code, but it sends nothing until <see cref="UploadAsync"/> is called (the user ticked Upload),
    /// and it refuses to run without an https endpoint from <see cref="ScreenshotUploadOptions"/>.
    /// </summary>
    public sealed class HttpScreenshotUploader : IScreenshotUploader
    {
        private readonly ScreenshotUploadOptions _options;
        private readonly HttpClient _client;

        /// <param name="options">Endpoint and credentials. May be null: every upload then fails with the "not configured" message.</param>
        /// <param name="client">An existing client to reuse. Null makes one (not disposed by this class).</param>
        public HttpScreenshotUploader(ScreenshotUploadOptions options, HttpClient client)
        {
            _options = options ?? new ScreenshotUploadOptions();
            _client = client ?? new HttpClient();
        }

        /// <summary>True when an https endpoint is set.</summary>
        public bool IsConfigured
        {
            get
            {
                Uri uri;
                return Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;
            }
        }

        public async Task<string> UploadAsync(string imageFilePath, string title)
        {
            if (!IsConfigured)
            {
                throw new NotSupportedException(
                    "Screenshot upload is not configured. Set an https upload endpoint (and, if the service needs one, your own access key) in the screenshot upload options.");
            }

            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
            {
                throw new FileNotFoundException("The screenshot file does not exist.", imageFilePath);
            }

            string imageField = string.IsNullOrEmpty(_options.ImageFieldName) ? "image" : _options.ImageFieldName;
            string titleField = _options.TitleFieldName == null ? "title" : _options.TitleFieldName;
            string linkPath = string.IsNullOrEmpty(_options.LinkPath) ? "data.link" : _options.LinkPath;
            TimeSpan timeout = _options.Timeout > TimeSpan.Zero ? _options.Timeout : TimeSpan.FromSeconds(60);

            byte[] bytes = File.ReadAllBytes(imageFilePath);

            using (MultipartFormDataContent form = new MultipartFormDataContent())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint))
            {
                ByteArrayContent image = new ByteArrayContent(bytes);
                image.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeOf(imageFilePath));
                form.Add(image, imageField, Path.GetFileName(imageFilePath));

                if (titleField.Length > 0 && !string.IsNullOrEmpty(title))
                {
                    form.Add(new StringContent(title), titleField);
                }

                request.Content = form;

                if (!string.IsNullOrEmpty(_options.AuthorizationHeader))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", _options.AuthorizationHeader);
                }

                using (System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource(timeout))
                using (HttpResponseMessage response = await _client.SendAsync(request, cts.Token))
                {
                    string body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException("The upload service answered " + (int)response.StatusCode + " " + response.ReasonPhrase + ".");
                    }

                    JToken link;

                    try
                    {
                        link = JObject.Parse(body).SelectToken(linkPath);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("The upload service answer could not be read: " + ex.Message, ex);
                    }

                    string url = link != null ? link.ToString() : null;

                    if (string.IsNullOrEmpty(url))
                    {
                        throw new InvalidOperationException("The upload service answer has no image link at '" + linkPath + "'.");
                    }

                    return url;
                }
            }
        }

        private static string ContentTypeOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
