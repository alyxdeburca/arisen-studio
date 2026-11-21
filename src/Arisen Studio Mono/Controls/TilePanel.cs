using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// A scrolling column of tile groups. Each group has a title above a wrapping row of <see cref="TileCard"/>s.
    /// Group ids must be unique. Tiles are found by <see cref="TileCard.Id"/>.
    /// </summary>
    internal class TilePanel : UserControl
    {
        private sealed class GroupView
        {
            public GroupView(Label header, FlowLayoutPanel tiles)
            {
                Header = header;
                Tiles = tiles;
            }

            public Label Header { get; private set; }

            public FlowLayoutPanel Tiles { get; private set; }
        }

        private readonly FlowLayoutPanel _column = new FlowLayoutPanel();
        private readonly Dictionary<string, GroupView> _groupsById = new Dictionary<string, GroupView>(StringComparer.Ordinal);
        private readonly List<GroupView> _groups = new List<GroupView>();
        private readonly List<TileCard> _tiles = new List<TileCard>();
        private Font _headerFont;
        private bool _relayouting;

        public TilePanel()
        {
            _column.Dock = DockStyle.Fill;
            _column.FlowDirection = FlowDirection.TopDown;
            _column.WrapContents = false;
            _column.AutoScroll = true;
            _column.Resize += OnColumnResize;
            Controls.Add(_column);
            Font = SystemFonts.MessageBoxFont;
            RebuildHeaderFont();
        }

        /// <summary>
        /// Adds an empty group. An empty or null title hides the group's heading.
        /// </summary>
        public void AddGroup(string id, string title)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }
            if (_groupsById.ContainsKey(id))
            {
                throw new ArgumentException("A tile group with this id already exists: " + id, "id");
            }

            Label header = new Label();
            header.AutoSize = true;
            header.Margin = new Padding(0);
            header.Font = _headerFont;
            header.Text = title ?? string.Empty;
            header.Visible = header.Text.Length > 0;

            FlowLayoutPanel tiles = new FlowLayoutPanel();
            tiles.FlowDirection = FlowDirection.LeftToRight;
            tiles.WrapContents = true;
            tiles.AutoSize = false;
            tiles.Margin = new Padding(0);
            tiles.Padding = new Padding(0);

            GroupView group = new GroupView(header, tiles);
            _groups.Add(group);
            _groupsById.Add(id, group);
            _column.Controls.Add(header);
            _column.Controls.Add(tiles);
            Relayout();
        }

        /// <summary>
        /// Adds a tile to a group. The tile's Id is its title when the title is not empty, otherwise "groupId:index".
        /// Throws <see cref="ArgumentException"/> if the group does not exist. Null image and null handler are allowed.
        /// </summary>
        public TileCard AddTile(string groupId, string title, string subtitle, TileSize size, Image image, EventHandler onClick)
        {
            GroupView group;
            if (groupId == null || !_groupsById.TryGetValue(groupId, out group))
            {
                throw new ArgumentException("No tile group with this id: " + groupId, "groupId");
            }

            TileCard card = new TileCard();
            card.Title = title ?? string.Empty;
            card.Subtitle = subtitle ?? string.Empty;
            card.SizeKind = size;
            card.Image = image;
            card.Id = card.Title.Length > 0 ? card.Title : groupId + ":" + group.Tiles.Controls.Count;
            if (onClick != null)
            {
                card.Click += onClick;
            }
            card.SizeChanged += OnTileSizeChanged;

            group.Tiles.Controls.Add(card);
            _tiles.Add(card);
            Relayout();
            return card;
        }

        /// <summary>
        /// Returns the first tile whose Id matches exactly, or null if there is none.
        /// </summary>
        public TileCard Find(string tileId)
        {
            if (tileId == null)
            {
                return null;
            }
            foreach (TileCard card in _tiles)
            {
                if (string.Equals(card.Id, tileId, StringComparison.Ordinal))
                {
                    return card;
                }
            }
            return null;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RebuildHeaderFont();
            Relayout();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Relayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _headerFont != null)
            {
                _headerFont.Dispose();
                _headerFont = null;
            }
            base.Dispose(disposing);
        }

        private void OnColumnResize(object sender, EventArgs e)
        {
            Relayout();
        }

        private void OnTileSizeChanged(object sender, EventArgs e)
        {
            Relayout();
        }

        /// <summary>
        /// Builds a bold heading font from the current font, moves every heading onto it and disposes the old one.
        /// </summary>
        private void RebuildHeaderFont()
        {
            Font previous = _headerFont;
            _headerFont = new Font(Font, FontStyle.Bold);
            foreach (GroupView group in _groups)
            {
                group.Header.Font = _headerFont;
            }
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        /// <summary>
        /// Gives each group the column's width and a height that fits its tiles. Re-entrant calls are ignored.
        /// </summary>
        private void Relayout()
        {
            if (_relayouting)
            {
                return;
            }
            _relayouting = true;
            try
            {
                int gap = Math.Max(2, Font.Height / 4);
                Padding columnPadding = new Padding(gap);
                if (_column.Padding != columnPadding)
                {
                    _column.Padding = columnPadding;
                }

                // Always reserve the scrollbar width, so showing the bar does not change the tile widths.
                int width = Math.Max(1, _column.Width - SystemInformation.VerticalScrollBarWidth - columnPadding.Horizontal);
                Padding headerPadding = new Padding(0, 0, 0, gap / 2);
                Padding groupMargin = new Padding(0, 0, 0, gap);

                foreach (GroupView group in _groups)
                {
                    if (group.Header.Padding != headerPadding)
                    {
                        group.Header.Padding = headerPadding;
                    }
                    if (group.Tiles.Margin != groupMargin)
                    {
                        group.Tiles.Margin = groupMargin;
                    }
                    if (group.Tiles.Width != width)
                    {
                        group.Tiles.Width = width;
                    }

                    group.Tiles.PerformLayout();
                    int bottom = 0;
                    foreach (Control child in group.Tiles.Controls)
                    {
                        bottom = Math.Max(bottom, child.Bottom + child.Margin.Bottom);
                    }
                    if (group.Tiles.Controls.Count == 0)
                    {
                        // An empty group shows nothing, but must never be zero-size: Mono's DrawToBitmap makes a
                        // Bitmap(width, 0) for it and GDI+ answers InvalidParameter.
                        bottom = 1;
                    }
                    if (group.Tiles.Height != bottom)
                    {
                        group.Tiles.Height = bottom;
                    }
                }
            }
            finally
            {
                _relayouting = false;
            }
        }
    }
}
