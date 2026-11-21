using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// Stands in for a page another slice owns. It is replaced with PageHost.RegisterPage using the same key.
    /// </summary>
    internal sealed class PlaceholderPage : UserControl, IShellPage
    {
        private readonly string _title;

        public PlaceholderPage(string title)
        {
            if (title == null)
            {
                title = string.Empty;
            }
            _title = title;
            Label label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = title + " (placeholder page, not ported yet)"
            };
            Controls.Add(label);
        }

        public string Title
        {
            get { return _title; }
        }

        public void OnPageShown()
        {
            // A placeholder has nothing to load.
        }
    }
}
