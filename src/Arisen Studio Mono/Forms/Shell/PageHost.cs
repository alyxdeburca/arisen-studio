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

            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages[key] = page;
            Controls.Add(page);

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

            foreach (Control other in _pages.Values)
            {
                other.Visible = other == page;
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
