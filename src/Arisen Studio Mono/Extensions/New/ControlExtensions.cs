using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Extensions
{
    /// <summary>
    /// Helpers for stock WinForms controls. Port of the old DevExpress tree's ControlExtensions.cs.
    /// The two commented-out SetTileItemStatus methods in the old file used the DevExpress TileItem type and were never
    /// compiled; they are not ported.
    /// </summary>
    internal static class ControlExtensions
    {
        /// <summary>
        /// Set control width to fit the text content.
        /// </summary>
        /// <param name="ctrl"> Control to set text </param>
        /// <param name="extra"> Extra padding to the width </param>
        public static void SetControlTextWidth(this Control ctrl, int extra = 0)
        {
            // Same measure as the old code (font family and size only, same +19 padding). The old code never disposed the
            // Font or the Graphics it created; they are disposed here.
            SizeF size;
            using (Font font = new Font(ctrl.Font.FontFamily, ctrl.Font.Size))
            using (Graphics graphics = ctrl.CreateGraphics())
            {
                size = graphics.MeasureString(ctrl.Text, font);
            }

            ctrl.Width = (int)Math.Round(size.Width, 0) + 19 + extra;
            ctrl.Refresh();
        }
    }
}
