using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Controls
{
    /// <summary>
    /// Which side of the strip an action sits on.
    /// </summary>
    internal enum ActionAlign
    {
        Left,
        Right
    }

    /// <summary>
    /// A top-docked toolbar of text/icon buttons. Each button stores its id in Name so it can be found again.
    /// </summary>
    internal sealed class ActionStrip : ToolStrip
    {
        public ActionStrip()
        {
            Dock = DockStyle.Top;
            GripStyle = ToolStripGripStyle.Hidden;
            Font = SystemFonts.MessageBoxFont;
            ImageScalingSize = new Size(Font.Height, Font.Height);
        }

        /// <summary>
        /// Adds a button to the strip.
        /// </summary>
        public ToolStripButton AddAction(string id, string text, Image icon, EventHandler onClick, ActionAlign align = ActionAlign.Right)
        {
            ToolStripButton button = new ToolStripButton
            {
                Name = id,
                Text = text,
                Image = icon,
                DisplayStyle = icon != null ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Text,
                Alignment = align == ActionAlign.Left ? ToolStripItemAlignment.Left : ToolStripItemAlignment.Right
            };
            if (onClick != null)
            {
                button.Click += onClick;
            }
            _ = Items.Add(button);
            return button;
        }

        /// <summary>
        /// Returns the button added with the given id, or null if there is none.
        /// </summary>
        public ToolStripButton Find(string id)
        {
            foreach (ToolStripItem item in Items)
            {
                ToolStripButton button = item as ToolStripButton;
                if (button != null && button.Name == id)
                {
                    return button;
                }
            }
            return null;
        }

        /// <summary>
        /// Adds a text label to the strip, for example a status text.
        /// </summary>
        public ToolStripLabel AddLabel(string id, string text, ActionAlign align = ActionAlign.Left)
        {
            ToolStripLabel label = new ToolStripLabel
            {
                Name = id,
                Text = text,
                Alignment = align == ActionAlign.Left ? ToolStripItemAlignment.Left : ToolStripItemAlignment.Right
            };
            _ = Items.Add(label);
            return label;
        }

        /// <summary>
        /// Adds a drop-down menu button to the strip. Fill it with AddMenuItem.
        /// </summary>
        public ToolStripDropDownButton AddMenu(string id, string text, ActionAlign align = ActionAlign.Left)
        {
            ToolStripDropDownButton menu = new ToolStripDropDownButton
            {
                Name = id,
                Text = text,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Alignment = align == ActionAlign.Left ? ToolStripItemAlignment.Left : ToolStripItemAlignment.Right
            };
            _ = Items.Add(menu);
            return menu;
        }

        /// <summary>
        /// Adds an item to a menu created by AddMenu. Returns null if there is no menu with the given id.
        /// </summary>
        public ToolStripMenuItem AddMenuItem(string menuId, string itemId, string text, EventHandler onClick)
        {
            foreach (ToolStripItem item in Items)
            {
                ToolStripDropDownButton menu = item as ToolStripDropDownButton;
                if (menu != null && menu.Name == menuId)
                {
                    ToolStripMenuItem menuItem = new ToolStripMenuItem
                    {
                        Name = itemId,
                        Text = text
                    };
                    if (onClick != null)
                    {
                        menuItem.Click += onClick;
                    }
                    _ = menu.DropDownItems.Add(menuItem);
                    return menuItem;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the item of any kind with the given id, or null if there is none.
        /// Items on the strip are checked first, then the items inside its menus.
        /// </summary>
        public ToolStripItem FindItem(string id)
        {
            foreach (ToolStripItem item in Items)
            {
                if (item.Name == id)
                {
                    return item;
                }
            }
            foreach (ToolStripItem item in Items)
            {
                ToolStripDropDownItem menu = item as ToolStripDropDownItem;
                if (menu != null && menu.HasDropDownItems)
                {
                    foreach (ToolStripItem child in menu.DropDownItems)
                    {
                        if (child.Name == id)
                        {
                            return child;
                        }
                    }
                }
            }
            return null;
        }
    }
}
