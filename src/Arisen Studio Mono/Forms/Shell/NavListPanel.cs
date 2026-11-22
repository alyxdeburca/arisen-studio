using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ArisenStudio.Shell
{
    /// <summary>
    /// Raised when a page leaf is selected, by a click or by SelectLeaf. Carries the leaf key (a ShellPageKeys value).
    /// </summary>
    internal sealed class NavLeafEventArgs : EventArgs
    {
        public NavLeafEventArgs(string leafKey)
        {
            LeafKey = leafKey;
        }

        public string LeafKey { get; private set; }
    }

    /// <summary>
    /// Left navigation: three groups and the page leaves, in a stock TreeView. This is the design's fallback for the old
    /// accordion (MW1-081 to MW1-094). Leaves are text only; icons are not ported.
    /// Groups can be selected but raise nothing; leaves raise LeafSelected. The tree is rebuilt from the entry list on every change.
    /// SetLeafVisible is the platform-visibility hook (MW1-021). SetLeafCaption and SetGroupCaption are the language hook (MW2-029).
    /// UI thread only.
    /// </summary>
    internal sealed class NavListPanel : Panel
    {
        private sealed class NavEntry
        {
            public bool IsGroup;
            public string Key;
            public string Caption;
            public string Group;
            public bool Visible;
        }

        private readonly List<NavEntry> _entries = new List<NavEntry>();
        private readonly Dictionary<string, TreeNode> _groupNodes = new Dictionary<string, TreeNode>();
        private readonly Dictionary<string, TreeNode> _leafNodes = new Dictionary<string, TreeNode>();
        private readonly TreeView _tree;
        private bool _suppress;

        public NavListPanel()
        {
            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            Controls.Add(_tree);
        }

        public event EventHandler<NavLeafEventArgs> LeafSelected;

        public void AddGroup(string groupKey, string caption)
        {
            if (groupKey == null)
            {
                throw new ArgumentNullException("groupKey");
            }
            if (FindEntry(groupKey, true) != null)
            {
                throw new ArgumentException("Group already added: " + groupKey, "groupKey");
            }

            _entries.Add(new NavEntry { IsGroup = true, Key = groupKey, Caption = caption == null ? string.Empty : caption, Visible = true });
            Rebuild();
        }

        /// <summary>
        /// Adds a leaf under a group. A null groupKey puts the leaf at the top level (the Dashboard leaf).
        /// </summary>
        public void AddLeaf(string groupKey, string leafKey, string caption)
        {
            if (leafKey == null)
            {
                throw new ArgumentNullException("leafKey");
            }
            if (groupKey != null && FindEntry(groupKey, true) == null)
            {
                throw new ArgumentException("Group not added yet: " + groupKey, "groupKey");
            }
            if (FindEntry(leafKey, false) != null)
            {
                throw new ArgumentException("Leaf already added: " + leafKey, "leafKey");
            }

            _entries.Add(new NavEntry { IsGroup = false, Key = leafKey, Caption = caption == null ? string.Empty : caption, Group = groupKey, Visible = true });
            Rebuild();
        }

        /// <summary>
        /// True if the leaf was added, even when it is hidden.
        /// </summary>
        public bool HasLeaf(string leafKey)
        {
            return FindEntry(leafKey, false) != null;
        }

        /// <summary>
        /// True if the leaf was added and its Visible flag is set (SetLeafVisible). False for an unknown or hidden leaf.
        /// </summary>
        public bool IsLeafVisible(string leafKey)
        {
            NavEntry entry = FindEntry(leafKey, false);
            return entry != null && entry.Visible;
        }

        /// <summary>
        /// Selects a visible leaf and raises LeafSelected once. Returns false for a null, unknown or hidden leaf.
        /// </summary>
        public bool SelectLeaf(string leafKey)
        {
            TreeNode node;
            if (leafKey == null)
            {
                return false;
            }
            if (!_leafNodes.TryGetValue(leafKey, out node))
            {
                return false;
            }

            _suppress = true;
            try
            {
                _tree.SelectedNode = node;
            }
            finally
            {
                _suppress = false;
            }
            RaiseLeafSelected(leafKey);
            return true;
        }

        /// <summary>
        /// Clears the nav selection without raising LeafSelected (used when a page with no nav leaf is shown, such as About).
        /// </summary>
        public void ClearLeafSelection()
        {
            _suppress = true;
            try
            {
                _tree.SelectedNode = null;
            }
            finally
            {
                _suppress = false;
            }
        }

        public bool SetLeafCaption(string leafKey, string caption)
        {
            NavEntry entry = FindEntry(leafKey, false);
            if (entry == null)
            {
                return false;
            }
            entry.Caption = caption == null ? string.Empty : caption;
            Rebuild();
            return true;
        }

        public bool SetGroupCaption(string groupKey, string caption)
        {
            NavEntry entry = FindEntry(groupKey, true);
            if (entry == null)
            {
                return false;
            }
            entry.Caption = caption == null ? string.Empty : caption;
            Rebuild();
            return true;
        }

        public bool SetLeafVisible(string leafKey, bool visible)
        {
            NavEntry entry = FindEntry(leafKey, false);
            if (entry == null)
            {
                return false;
            }
            entry.Visible = visible;
            Rebuild();
            return true;
        }

        private NavEntry FindEntry(string key, bool isGroup)
        {
            if (key == null)
            {
                return null;
            }
            foreach (NavEntry entry in _entries)
            {
                if (entry.IsGroup == isGroup && entry.Key == key)
                {
                    return entry;
                }
            }
            return null;
        }

        private void Rebuild()
        {
            string selectedKey = null;
            TreeNode current = _tree.SelectedNode;
            if (current != null)
            {
                selectedKey = current.Tag as string;
            }

            _suppress = true;
            try
            {
                _tree.Nodes.Clear();
                _groupNodes.Clear();
                _leafNodes.Clear();

                // One pass in add order, so a top-level leaf (Dashboard) stays above the groups.
                foreach (NavEntry entry in _entries)
                {
                    if (entry.IsGroup)
                    {
                        TreeNode groupNode = new TreeNode(entry.Caption);
                        _groupNodes[entry.Key] = groupNode;
                        _tree.Nodes.Add(groupNode);
                    }
                    else if (entry.Visible)
                    {
                        TreeNode leafNode = new TreeNode(entry.Caption);
                        leafNode.Tag = entry.Key;
                        _leafNodes[entry.Key] = leafNode;
                        if (entry.Group == null)
                        {
                            _tree.Nodes.Add(leafNode);
                        }
                        else
                        {
                            _groupNodes[entry.Group].Nodes.Add(leafNode);
                        }
                    }
                }

                _tree.ExpandAll();

                TreeNode toSelect;
                if (selectedKey != null)
                {
                    if (_leafNodes.TryGetValue(selectedKey, out toSelect))
                    {
                        _tree.SelectedNode = toSelect;
                    }
                }
            }
            finally
            {
                _suppress = false;
            }
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_suppress || e.Node == null)
            {
                return;
            }
            string key = e.Node.Tag as string;
            if (key != null)
            {
                RaiseLeafSelected(key);
            }
        }

        private void RaiseLeafSelected(string leafKey)
        {
            EventHandler<NavLeafEventArgs> handler = LeafSelected;
            if (handler != null)
            {
                handler(this, new NavLeafEventArgs(leafKey));
            }
        }
    }
}
