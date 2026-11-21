using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Paints one icon scaled to fit: aspect ratio kept, centred, high-quality resampling.
    /// <see cref="IconName"/> loads an icon embedded as <c>ArisenStudio.Icons.&lt;name&gt;.png</c>
    /// (produced by <c>tools/render-svgs.sh</c>). Names are matched case-insensitively.
    /// </summary>
    /// <remarks>
    /// Images returned by <see cref="Image"/> after <see cref="IconName"/> is set are shared, cached
    /// instances: callers must not dispose them. Set <see cref="System.Windows.Forms.Control.AccessibleName"/>
    /// yourself; the control only fills it from the icon name when it is empty.
    /// </remarks>
    internal class IconBox : Control
    {
        private const string ResourcePrefix = "ArisenStudio.Icons.";
        private const string ResourceSuffix = ".png";

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, System.Drawing.Image> IconCache =
            new Dictionary<string, System.Drawing.Image>(StringComparer.Ordinal);

        private System.Drawing.Image _image;
        private string _iconName;

        public IconBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            Font = SystemFonts.MessageBoxFont;
            Size = GetPreferredSize(Size.Empty);
        }

        /// <summary>
        /// The icon to paint. Setting it clears <see cref="IconName"/>.
        /// </summary>
        public System.Drawing.Image Image
        {
            get { return _image; }
            set
            {
                _image = value;
                _iconName = null;
                Invalidate();
            }
        }

        /// <summary>
        /// The icon base name, e.g. <c>icons8-install</c> for <c>ArisenStudio.Icons.icons8-install.png</c>.
        /// Setting it loads the embedded PNG (cached). A missing or unreadable resource yields no image.
        /// </summary>
        public string IconName
        {
            get { return _iconName; }
            set
            {
                _iconName = string.IsNullOrEmpty(value) ? null : value;
                _image = LoadIcon(_iconName);
                if (string.IsNullOrEmpty(AccessibleName) && _iconName != null)
                {
                    AccessibleName = _iconName;
                }
                Invalidate();
            }
        }

        /// <summary>
        /// A square, sized from the font height, so it scales with the UI font.
        /// </summary>
        public override Size GetPreferredSize(Size proposedSize)
        {
            int side = Font.Height * 2;
            return new Size(side, side);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            System.Drawing.Image image = _image;
            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return;
            }

            Rectangle area = GetContentRectangle();
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            float scale = Math.Min((float)area.Width / image.Width, (float)area.Height / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            Rectangle target = new Rectangle(
                area.Left + ((area.Width - width) / 2),
                area.Top + ((area.Height - height) / 2),
                width,
                height);

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(image, target);
        }

        private Rectangle GetContentRectangle()
        {
            Rectangle area = ClientRectangle;
            int inset = Math.Max(1, Font.Height / 6);
            area.Inflate(-inset, -inset);
            return area;
        }

        /// <summary>
        /// Returns the cached icon for <paramref name="iconName"/>, loading it on first use.
        /// Null results are cached too, so a missing icon is looked up only once.
        /// </summary>
        private static System.Drawing.Image LoadIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            string key = iconName.ToLowerInvariant();
            lock (CacheLock)
            {
                System.Drawing.Image cached;
                if (IconCache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                cached = ReadEmbeddedPng(ResourcePrefix + key + ResourceSuffix);
                IconCache[key] = cached;
                return cached;
            }
        }

        private static System.Drawing.Image ReadEmbeddedPng(string resourceName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (System.Drawing.Image decoded = System.Drawing.Image.FromStream(stream))
                    {
                        // Copy the pixels: GDI+ may need the source stream while the image is alive.
                        return new System.Drawing.Bitmap(decoded);
                    }
                }
            }
            catch (Exception)
            {
                // An unreadable or corrupt icon must not break painting; show nothing instead.
                return null;
            }
        }
    }
}
