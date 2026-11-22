using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ArisenStudio.Forms.Tools;
using Newtonsoft.Json.Linq;

namespace ArisenStudio.Extensions
{
    /// <summary>
    /// Port of the old ImgurUploader. The old code used the Imgur.API package and a WPF BitmapSource (PngBitmapEncoder);
    /// this one uses System.Drawing (ImageFormat.Png) and a plain HttpClient against the Imgur v3 image endpoint.
    /// The Imgur client id is NOT stored here: the caller passes it in (see <see cref="ImgurScreenshotUploader"/>).
    /// Nothing is sent until one of the Upload methods is called.
    /// </summary>
    internal static class ImgurUploader
    {
        /// <summary>The Imgur v3 image upload address. Only used when an Upload method is called.</summary>
        public const string UploadEndpoint = "https://api.imgur.com/3/image";

        /// <summary>
        /// Same shape as the old Upload: saves the image as "fileName.png" (PNG via System.Drawing), uploads it, and returns the
        /// link, or "Error" when anything fails (as the old code did).
        /// </summary>
        /// <param name="image"> The picture to upload </param>
        /// <param name="fileName"> File path without the ".png" ending; the PNG is written there and not deleted, as before </param>
        /// <param name="clientId"> The user's own Imgur client id (the part after "Client-ID ") </param>
        /// <param name="httpClient"> Client to reuse. Null makes one for this call </param>
        public static async Task<string> Upload(Image image, string fileName, string clientId, HttpClient httpClient = null)
        {
            string link;

            try
            {
                string pngPath = fileName + ".png";

                using (FileStream fileStream = new FileStream(pngPath, FileMode.Create))
                {
                    image.Save(fileStream, ImageFormat.Png);
                }

                link = await UploadPng(File.ReadAllBytes(pngPath), Path.GetFileName(pngPath), clientId, httpClient);
            }
            catch
            {
                link = "Error";
            }

            return link;
        }

        /// <summary>
        /// Uploads PNG bytes and returns the image link. Throws when the client id is empty, the service answers with an error
        /// status, or the answer has no link.
        /// </summary>
        /// <param name="pngBytes"> A PNG file in memory </param>
        /// <param name="fileName"> Name sent as the file name of the form part </param>
        /// <param name="clientId"> The user's own Imgur client id </param>
        /// <param name="httpClient"> Client to reuse. Null makes one for this call </param>
        public static async Task<string> UploadPng(byte[] pngBytes, string fileName, string clientId, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new NotSupportedException("Imgur upload is not configured. Set your own Imgur client id to upload screenshots.");
            }

            bool ownsClient = httpClient == null;
            HttpClient client = httpClient ?? new HttpClient();

            try
            {
                using (MultipartFormDataContent form = new MultipartFormDataContent())
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint))
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    ByteArrayContent image = new ByteArrayContent(pngBytes);
                    image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    form.Add(image, "image", fileName);

                    request.Content = form;
                    request.Headers.TryAddWithoutValidation("Authorization", "Client-ID " + clientId.Trim());

                    using (HttpResponseMessage response = await client.SendAsync(request, cts.Token))
                    {
                        string body = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException("Imgur answered " + (int)response.StatusCode + " " + response.ReasonPhrase + ".");
                        }

                        JToken link = JObject.Parse(body).SelectToken("data.link");
                        string url = link != null ? link.ToString() : null;

                        if (string.IsNullOrEmpty(url))
                        {
                            throw new InvalidOperationException("The Imgur answer has no image link.");
                        }

                        return url;
                    }
                }
            }
            finally
            {
                if (ownsClient)
                {
                    client.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// <see cref="IScreenshotUploader"/> for Imgur, over <see cref="HttpClient"/>. The client id comes from the caller (the
    /// constructor), so the host reads it from the user's settings; there is no built-in id. The screenshot forms save BMP files, so the
    /// file is re-encoded as PNG with System.Drawing before it is sent. Throws on failure; the screenshot form shows the message.
    /// </summary>
    public sealed class ImgurScreenshotUploader : IScreenshotUploader
    {
        private readonly string _clientId;
        private readonly HttpClient _client;

        /// <param name="clientId"> The user's own Imgur client id. Null or empty: every upload fails with a "not configured" message </param>
        /// <param name="client"> Client to reuse (not disposed here). Null makes one per upload </param>
        public ImgurScreenshotUploader(string clientId, HttpClient client = null)
        {
            _clientId = clientId;
            _client = client;
        }

        public async Task<string> UploadAsync(string imageFilePath, string title)
        {
            if (string.IsNullOrWhiteSpace(_clientId))
            {
                throw new NotSupportedException("Imgur upload is not configured. Set your own Imgur client id to upload screenshots.");
            }

            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
            {
                throw new FileNotFoundException("The screenshot file does not exist.", imageFilePath);
            }

            byte[] png;

            using (Image image = Image.FromFile(imageFilePath))
            using (MemoryStream memory = new MemoryStream())
            {
                image.Save(memory, ImageFormat.Png);
                png = memory.ToArray();
            }

            string name = string.IsNullOrEmpty(title) ? "screenshot" : title;
            return await ImgurUploader.UploadPng(png, name + ".png", _clientId, _client);
        }
    }
}
