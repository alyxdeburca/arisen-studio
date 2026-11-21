using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// Shows one page at a time. Pages are registered under a key (a ShellPageKeys value). Any Control can be a page;
    /// a page that implements IShellPage is also told when it becomes the visible page.
    /// </summary>
    internal sealed class PageHost : Panel
    {
        private readonly Dictionary<string, Control> _pages = new Dictionary<string, Control>();
        private string _activeKey;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Pages that are not the active one are not children, so base.Dispose does not reach them.
                foreach (Control page in _pages.Values)
                {
                    if (page.Parent != this)
                    {
                        page.Dispose();
                    }
                }
            }
            base.Dispose(disposing);
        }

        public string ActiveKey
        {
            get { return _activeKey; }
        }

        public bool HasPage(string key)
        {
            return key != null && _pages.ContainsKey(key);
        }

        /// <summary>
        /// Registers a page under a key. Registering an existing key replaces that page and disposes the old control,
        /// so register each control under one key only. UI thread only.
        /// </summary>
        public void RegisterPage(string key, Control page)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (page == null)
            {
                throw new ArgumentNullException("page");
            }

            bool wasActive = _activeKey == key;
            Control old;
            if (_pages.TryGetValue(key, out old))
            {
                if (old == page)
                {
                    return;
                }
                Controls.Remove(old);
                old.Dispose();
            }

            // Only the active page is a child of the host. A registered page that is not shown stays out of Controls,
            // so it can never be painted or laid out by accident (on Mono a hidden child was still drawn, at its
            // default size in the top-left corner).
            page.Dock = DockStyle.Fill;
            _pages[key] = page;

            if (wasActive)
            {
                ShowPage(key);
            }
        }

        /// <summary>
        /// Shows the page registered under the key and hides the others. Returns false for a null or unknown key,
        /// and then the visible page does not change. UI thread only.
        /// </summary>
        public bool ShowPage(string key)
        {
            if (key == null)
            {
                return false;
            }

            Control page;
            if (!_pages.TryGetValue(key, out page))
            {
                return false;
            }

            SuspendLayout();
            try
            {
                // Take every other page out of the host, then put the wanted one in.
                foreach (Control other in _pages.Values)
                {
                    if (other != page && other.Parent == this)
                    {
                        Controls.Remove(other);
                    }
                }
                if (page.Parent != this)
                {
                    Controls.Add(page);
                }
                page.Dock = DockStyle.Fill;
                page.Visible = true;
            }
            finally
            {
                ResumeLayout(true);
            }
            _activeKey = key;

            // Notify only after the loop: a page may register another page from inside OnPageShown.
            IShellPage shellPage = page as IShellPage;
            if (shellPage != null)
            {
                shellPage.OnPageShown();
            }
            return true;
        }
    }
}
