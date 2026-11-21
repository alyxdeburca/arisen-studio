using ArisenStudio.Models.Resources;
using ArisenStudio.Runtime;
using System;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Tools
{
    /// <summary>
    /// Boundary for the Imgur upload the old screenshot forms did (Imgur.API plus a WPF BitmapSource, both Windows-only).
    /// Returns the link of the uploaded image, or null / empty when the upload failed.
    /// </summary>
    public interface IScreenshotUploader
    {
        Task<string> UploadAsync(string imageFilePath, string title);
    }

    /// <summary>
    /// Default uploader: always fails with a clear message. The host replaces it when an upload service is chosen.
    /// </summary>
    public sealed class UnavailableScreenshotUploader : IScreenshotUploader
    {
        public Task<string> UploadAsync(string imageFilePath, string title)
        {
            return Task.FromException<string>(new NotSupportedException("Screenshot upload is not available in this build. No upload service is connected."));
        }
    }

    /// <summary>
    /// Shared window of the PS3 and Xbox "Take Screenshot" tools: file name box, capture / delete / new / open-folder buttons,
    /// the picture, and an optional upload. The platform form only says how to capture and how to remove the console copy.
    /// Stock WinForms port of the old DevExpress TakeScreenshot forms.
    /// Screenshots are saved as BMP under &lt;Settings.PathAppData&gt;/Screenshots/&lt;platform&gt;, and a name that is taken gets "-1", "-2" and so on.
    /// </summary>
    public abstract class ScreenshotFormBase : Form
    {
        private readonly string _platformFolder;
        private readonly TextBox _textBoxFileName;
        private readonly CheckBox _checkBoxUpload;
        private readonly PictureBox _picture;
        private readonly Button _buttonCapture;
        private readonly Button _buttonDelete;
        private readonly Button _buttonNew;
        private readonly Button _buttonOpenFolder;
        private readonly Label _labelFileName;
        private readonly ToolStripStatusLabel _status;

        private string _localFilePath;

        protected ScreenshotFormBase(string platformFolder)
        {
            _platformFolder = platformFolder;

            ToolFormSupport.ConfigureForm(this, true);

            int h = Font.Height;
            int pad = h / 2;

            _labelFileName = ToolFormSupport.NewLabel("File name");
            _textBoxFileName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, pad / 2) };
            _checkBoxUpload = new CheckBox { AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 0, 0, pad / 2) };
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, pad)
            };

            _buttonCapture = ToolFormSupport.NewButton(this, 8);
            _buttonDelete = ToolFormSupport.NewButton(this, 8);
            _buttonNew = ToolFormSupport.NewButton(this, 8);
            _buttonOpenFolder = ToolFormSupport.NewButton(this, 8);
            _buttonOpenFolder.Margin = Padding.Empty;
            _buttonDelete.Enabled = false;
            _buttonNew.Enabled = false;
            _buttonOpenFolder.Enabled = false;

            TableLayoutPanel root = ToolFormSupport.NewTable(new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(pad);
            root.AutoSize = false;
            ToolFormSupport.AddRow(root, _labelFileName, _textBoxFileName);
            ToolFormSupport.AddFullRow(root, _checkBoxUpload, false);
            ToolFormSupport.AddFullRow(root, _picture, true);
            ToolFormSupport.AddFullRow(root, ToolFormSupport.FlowRow(_buttonCapture, _buttonDelete, _buttonNew, _buttonOpenFolder), false);

            Controls.Add(root);
            _status = ToolFormSupport.AddStatusBar(this);

            AcceptButton = _buttonCapture;

            _buttonCapture.Click += ButtonCapture_Click;
            _buttonDelete.Click += ButtonDelete_Click;
            _buttonNew.Click += ButtonNew_Click;
            _buttonOpenFolder.Click += ButtonOpenFolder_Click;

            ApplyLanguage();

            MinimumSize = new Size(h * 32, h * 24);
            ClientSize = new Size(h * 44, h * 32);
        }

        /// <summary>
        /// Localisation source for the texts. Null (the default) shows the English texts. Set it before the form is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        /// <summary>
        /// Settings source (Settings.PathAppData decides where screenshots are saved). Defaults to the app host settings.
        /// </summary>
        public SettingsData Settings { get; set; } = AppHost.Settings;

        /// <summary>
        /// Upload service for the "upload" box. The default always fails with a message; see <see cref="IScreenshotUploader" />.
        /// </summary>
        public IScreenshotUploader Uploader { get; set; } = new UnavailableScreenshotUploader();

        /// <summary>
        /// Full path of the screenshot that is shown now, or null.
        /// </summary>
        protected string LocalFilePath
        {
            get { return _localFilePath; }
        }

        /// <summary>
        /// Capture the console screen into <paramref name="localFilePath" /> (a .bmp path whose folder already exists).
        /// Throw when it fails; the message is shown to the user.
        /// </summary>
        protected abstract Task CaptureAsync(string localFilePath, string fileNameWithoutExtension);

        /// <summary>
        /// Remove the copy that is left on the console, if there is one. The local file is already gone when this runs. Default: nothing.
        /// </summary>
        protected virtual Task DeleteConsoleCopyAsync()
        {
            return Task.CompletedTask;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyLanguage();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ClearImage();
            base.OnFormClosed(e);
        }

        private void ApplyLanguage()
        {
            Text = ToolFormSupport.Lookup(Language, "TAKE_SCREENSHOT", "Take Screenshot");
            _labelFileName.Text = ToolFormSupport.Lookup(Language, "LABEL_FILE_NAME", "File Name");
            _checkBoxUpload.Text = ToolFormSupport.Lookup(Language, "LABEL_UPLOAD_TO_IMGUR", "Upload to Imgur");
            _buttonCapture.Text = ToolFormSupport.Lookup(Language, "CAPTURE_IMAGE", "Capture Image");
            _buttonDelete.Text = ToolFormSupport.Lookup(Language, "DELETE_IMAGE", "Delete Image");
            _buttonNew.Text = ToolFormSupport.Lookup(Language, "NEW_IMAGE", "New Image");
            _buttonOpenFolder.Text = ToolFormSupport.Lookup(Language, "OPEN_FOLDER", "Open Folder");
        }

        private string ScreenshotFolder
        {
            get { return Path.Combine(Settings.PathAppData, "Screenshots", _platformFolder); }
        }

        private static string NextFreeFileName(string folder, string name)
        {
            string candidate = Path.Combine(folder, name + ".bmp");
            int counter = 0;

            while (File.Exists(candidate))
            {
                counter++;
                candidate = Path.Combine(folder, name + "-" + counter + ".bmp");
            }

            return candidate;
        }

        private async void ButtonCapture_Click(object sender, EventArgs e)
        {
            _buttonCapture.Enabled = false;

            string localPath = null;
            string name = null;

            try
            {
                name = ToolFormSupport.RemoveInvalidFileNameChars(_textBoxFileName.Text);

                if (name.Length > 0 && Path.HasExtension(name))
                {
                    name = ToolFormSupport.RemoveInvalidFileNameChars(Path.GetFileNameWithoutExtension(name));
                }

                if (name.Length == 0)
                {
                    name = "Screenshot-" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                }

                string folder = ScreenshotFolder;
                Directory.CreateDirectory(folder);
                localPath = NextFreeFileName(folder, name);

                SetStatus("Taking screenshot...");
                await CaptureAsync(localPath, Path.GetFileNameWithoutExtension(localPath));

                ShowImage(localPath);
                _localFilePath = localPath;
                _buttonDelete.Enabled = true;
                _buttonNew.Enabled = true;
                _buttonOpenFolder.Enabled = true;

                SetStatus("Screenshot file saved to path: " + folder);
                ToolFormSupport.ShowInfo(this, ToolFormSupport.Lookup(Language, "SUCCESS", "Success"), "Screenshot file saved to path:\n" + folder);
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to take screenshot.");
                SetStatus("Unable to take screenshot.");
                ToolFormSupport.ShowError(this, ToolFormSupport.Lookup(Language, "ERROR", "Error"), "Unable to take screenshot. Error: " + ex.Message);
                _buttonCapture.Enabled = true;
                return;
            }

            _buttonCapture.Enabled = true;

            if (_checkBoxUpload.Checked)
            {
                await UploadAsync(localPath, Path.GetFileNameWithoutExtension(localPath));
            }
        }

        private async Task UploadAsync(string imagePath, string title)
        {
            SetStatus("Uploading screenshot file...");

            try
            {
                string link = await Uploader.UploadAsync(imagePath, title);

                if (string.IsNullOrEmpty(link))
                {
                    SetStatus("Unable to upload screenshot file.");
                    ToolFormSupport.ShowError(this, ToolFormSupport.Lookup(Language, "ERROR", "Error"), "Unable to upload screenshot file.");
                    return;
                }

                SetStatus("Screenshot file uploaded.");

                if (!ToolFormSupport.OpenUrl(link))
                {
                    ToolFormSupport.ShowInfo(this, ToolFormSupport.Lookup(Language, "SUCCESS", "Success"), "Screenshot file uploaded:\n" + link);
                }
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to upload screenshot file.");
                SetStatus("Unable to upload screenshot file.");
                ToolFormSupport.ShowError(this, ToolFormSupport.Lookup(Language, "ERROR", "Error"), "Unable to upload screenshot file. Error: " + ex.Message);
            }
        }

        private async void ButtonDelete_Click(object sender, EventArgs e)
        {
            string path = _localFilePath;

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ClearImage();

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                await DeleteConsoleCopyAsync();
                SetStatus("Deleted screenshot file: " + path);
            }
            catch (Exception ex)
            {
                ToolFormSupport.LogError(ex, "Unable to delete screenshot.");
                SetStatus("Unable to delete screenshot.");
                ToolFormSupport.ShowError(this, ToolFormSupport.Lookup(Language, "ERROR", "Error"), "Unable to delete screenshot. Error: " + ex.Message);
            }
        }

        private void ButtonNew_Click(object sender, EventArgs e)
        {
            ClearImage();
            SetStatus("Cleared current screenshot.");
        }

        private void ButtonOpenFolder_Click(object sender, EventArgs e)
        {
            ToolFormSupport.RevealFile(_localFilePath);
        }

        /// <summary>
        /// Show a file without keeping it open, so it can be deleted while it is on screen.
        /// </summary>
        private void ShowImage(string path)
        {
            Image loaded;

            using (FileStream stream = File.OpenRead(path))
            using (Image fromStream = Image.FromStream(stream))
            {
                loaded = new Bitmap(fromStream);
            }

            Image previous = _picture.Image;
            _picture.Image = loaded;

            if (previous != null)
            {
                previous.Dispose();
            }
        }

        private void ClearImage()
        {
            Image previous = _picture.Image;
            _picture.Image = null;

            if (previous != null)
            {
                previous.Dispose();
            }

            _localFilePath = null;
            _textBoxFileName.Text = string.Empty;
            _buttonDelete.Enabled = false;
            _buttonNew.Enabled = false;
            _buttonOpenFolder.Enabled = false;
        }

        private void SetStatus(string text)
        {
            ToolFormSupport.SetStatusText(this, _status, text);
        }
    }
}
