using System.Drawing;
using System.Drawing.Imaging;

namespace ArisenStudio.Extensions
{
    /// <summary>
    /// Port of the old ImageExtensions, GDI+ methods only (System.Drawing).
    /// The old ConvertBitmap(Bitmap) returned a WPF BitmapSource through
    /// System.Windows.Interop.Imaging (PresentationCore). Mono has no WPF, so it is not ported.
    /// Its only callers are the two TakeScreenshot tools, which pass the result to ImgurUploader (also WPF).
    /// </summary>
    internal static class ImageExtensions
    {
        public static Bitmap Resize(this Bitmap bmp, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(bmp, 0, 0, width, height);
            }

            return result;
        }

        public static Image ChangeColor(this Image bmp, Color oldColor, Color newColor)
        {
            Bitmap img = (Bitmap)bmp;
            for (int i = 0; i < img.Width; i++)
            {
                for (int j = 0; j < img.Height; j++)
                {
                    Color pixelColor = img.GetPixel(i, j);
                    if (pixelColor == oldColor)
                    {
                        img.SetPixel(i, j, newColor);
                    }
                }
            }

            return img;
        }

        public static Bitmap AdjustBrightness(Image image, float brightness)
        {
            // Make the ColorMatrix.
            float b = brightness;
            ColorMatrix cm = new ColorMatrix(
                new float[][]
                {
                    new float[] { b, 0, 0, 0, 0 },
                    new float[] { 0, b, 0, 0, 0 },
                    new float[] { 0, 0, b, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 },
                });
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetColorMatrix(cm);

            // Draw the image onto the new bitmap while applying the new ColorMatrix.
            Point[] points = new Point[]
            {
                new Point(0, 0),
                new Point(image.Width, 0),
                new Point(0, image.Height),
            };
            Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);

            // Make the result bitmap.
            Bitmap bm = new Bitmap(image.Width, image.Height);
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.DrawImage(image, points, rect, GraphicsUnit.Pixel, attributes);
            }

            // Return the result.
            return bm;
        }
    }
}
