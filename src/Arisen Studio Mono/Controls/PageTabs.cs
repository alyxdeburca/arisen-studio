using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// A stock TabControl wrapper used by the detail dialogs. Pages are addressed by a string id.
    ///
    /// Visibility: TabControl has no per-page visibility, so every registered page is kept in an
    /// ordered master list. Showing or hiding a page rebuilds TabPages from the visible subset, in
    /// registration order. The current selection is kept when its page stays visible; otherwise the
    /// first visible page is selected.
    ///
    /// Counting: SelectedIndex and PageCount refer to visible pages only, matching the tab strip.
    /// Find and SetTitle work on hidden pages too.
    /// </summary>
    internal class PageTabs : UserControl
    {
        private sealed class PageEntry
        {
            public PageEntry(string id, TabPage page)
            {
                Id = id;
                Page = page;
                Visible = true;
            }

            public string Id { get; private set; }
            public TabPage Page { get; private set; }
            public bool Visible { get; set; }
        }

        private readonly TabControl _tabs;
        private readonly List<PageEntry> _entries = new List<PageEntry>();
        private string _reportedId;
        private bool _rebuilding;

        public PageTabs()
        {
            Font = SystemFonts.MessageBoxFont;

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                SizeMode = TabSizeMode.Normal,
            };
            _tabs.SelectedIndexChanged += OnTabsSelectedIndexChanged;
            Controls.Add(_tabs);
        }

        /// <summary>Raised when the selected page's id changes. Not raised during SetPageVisible rebuilds unless the id actually changed.</summary>
        public event EventHandler SelectedIdChanged;

        /// <summary>Number of visible pages.</summary>
        public int PageCount
        {
            get { return _tabs.TabCount; }
        }

        /// <summary>Id of the selected visible page, or null when no page is visible.</summary>
        public string SelectedId
        {
            get
            {
                PageEntry entry = EntryForPage(_tabs.SelectedTab);
                return entry == null ? null : entry.Id;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentException("A page id is required.", "value");
                PageEntry entry = FindEntry(value);
                if (entry == null) throw new ArgumentException("No page has id '" + value + "'.", "value");
                if (!entry.Visible) throw new ArgumentException("Page '" + value + "' is hidden.", "value");
                _tabs.SelectedTab = entry.Page;
                RaiseIfChanged();
            }
        }

        /// <summary>Index among visible pages. Setting an index outside 0..PageCount-1 throws.</summary>
        public int SelectedIndex
        {
            get { return _tabs.SelectedIndex; }
            set
            {
                if (value < 0 || value >= _tabs.TabCount) throw new ArgumentOutOfRangeException("value");
                _tabs.SelectedIndex = value;
                RaiseIfChanged();
            }
        }

        /// <summary>
        /// Adds a page at the end. The content control is docked to fill the page.
        /// Throws ArgumentException for an empty or duplicate id.
        /// </summary>
        public TabPage AddPage(string id, string title, Control content)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A page id is required.", "id");
            if (title == null) throw new ArgumentNullException("title");
            if (content == null) throw new ArgumentNullException("content");
            if (FindEntry(id) != null) throw new ArgumentException("Duplicate page id '" + id + "'.", "id");

            var page = new TabPage(title);
            page.Name = id;
            page.UseVisualStyleBackColor = true;
            page.Padding = PagePadding();
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);

            _entries.Add(new PageEntry(id, page));
            _tabs.TabPages.Add(page);
            RaiseIfChanged();
            return page;
        }

        /// <summary>Returns the page with this id, whether visible or hidden, or null if unknown.</summary>
        public TabPage Find(string id)
        {
            PageEntry entry = FindEntry(id);
            return entry == null ? null : entry.Page;
        }

        /// <summary>
        /// Shows or hides a page. Returns false only when the id is unknown (nothing changes);
        /// returns true when the page exists, whether or not its state changed.
        /// </summary>
        public bool SetPageVisible(string id, bool visible)
        {
            PageEntry entry = FindEntry(id);
            if (entry == null) return false;
            if (entry.Visible == visible) return true;

            entry.Visible = visible;
            Rebuild();
            return true;
        }

        /// <summary>Changes a page's tab caption. Returns false when the id is unknown.</summary>
        public bool SetTitle(string id, string title)
        {
            if (title == null) throw new ArgumentNullException("title");
            PageEntry entry = FindEntry(id);
            if (entry == null) return false;

            entry.Page.Text = title;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Hidden pages are not in the TabControl's tree, so they are disposed here.
                foreach (PageEntry entry in _entries)
                {
                    if (!entry.Visible) entry.Page.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void Rebuild()
        {
            string keepId = SelectedId;
            _rebuilding = true;
            try
            {
                _tabs.SuspendLayout();
                _tabs.TabPages.Clear();

                PageEntry keep = null;
                foreach (PageEntry entry in _entries)
                {
                    if (!entry.Visible) continue;
                    _tabs.TabPages.Add(entry.Page);
                    if (entry.Id == keepId) keep = entry;
                }

                if (keep != null) _tabs.SelectedTab = keep.Page;
                else if (_tabs.TabCount > 0) _tabs.SelectedIndex = 0;
            }
            finally
            {
                _tabs.ResumeLayout();
                _rebuilding = false;
            }
            RaiseIfChanged();
        }

        private void OnTabsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_rebuilding) RaiseIfChanged();
        }

        private void RaiseIfChanged()
        {
            string now = SelectedId;
            if (now == _reportedId) return;

            _reportedId = now;
            EventHandler handler = SelectedIdChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private PageEntry FindEntry(string id)
        {
            if (id == null) return null;
            foreach (PageEntry entry in _entries)
            {
                if (string.Equals(entry.Id, id, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        private PageEntry EntryForPage(TabPage page)
        {
            if (page == null) return null;
            foreach (PageEntry entry in _entries)
            {
                if (ReferenceEquals(entry.Page, page)) return entry;
            }
            return null;
        }

        private Padding PagePadding()
        {
            int margin = Math.Max(3, Font.Height / 3);
            return new Padding(margin);
        }
    }
}
