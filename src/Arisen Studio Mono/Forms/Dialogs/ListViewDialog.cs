using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Dialogs
{
    /// <summary>
    /// One row of a ListViewDialog. Callers must copy Name and Value from the old ListItem type.
    /// </summary>
    public class ListViewDialogItem
    {
        public string Value { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lists items and lets the user pick one.
    /// Stock WinForms port of the old DevExpress ListViewDialog form.
    /// Double-click or Enter on a selected item: SelectedItem is set and the dialog returns OK.
    /// Escape: returns Cancel. A single click only selects.
    /// </summary>
    internal class ListViewDialog : Form
    {
        private readonly GroupBox _groupListItems;
        private readonly ListBox _listBox;
        private readonly TableLayoutPanel _root;

        public ListViewDialog()
        {
            Font = SystemFonts.MessageBoxFont;
            Text = "Choose Item";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;

            int h = Font.Height;
            int pad = h / 2;

            // Items are added in OnLoad, from Items.
            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "Name",
                IntegralHeight = false,
                Margin = new Padding(0),
                TabIndex = 0
            };
            _listBox.MouseDoubleClick += ListBoxMouseDoubleClick;
            _listBox.KeyDown += ListBoxKeyDown;

            _groupListItems = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            _groupListItems.Controls.Add(_listBox);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Padding = new Padding(pad),
                Margin = new Padding(0)
            };
            _ = _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _ = _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.Controls.Add(_groupListItems, 0, 0);
            Controls.Add(_root);

            ApplyLanguage();
        }

        /// <summary>
        /// The items to list, in the order given. Null means empty. Read before the dialog is shown.
        /// </summary>
        public List<ListViewDialogItem> Items { get; set; } = new List<ListViewDialogItem>();

        /// <summary>
        /// The item the user picked. Set only when the dialog returns OK. Null otherwise.
        /// </summary>
        public ListViewDialogItem SelectedItem { get; set; }

        /// <summary>
        /// Localisation source for the dialog texts. Null (the default) shows the English texts.
        /// Set it before the dialog is shown.
        /// </summary>
        public ResourceManager Language { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            ApplyLanguage();
            FillListBox();
            SizeToContent();
            base.OnLoad(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        private void ApplyLanguage()
        {
            _groupListItems.Text = Lookup("CHOOSE_ITEM", "Choose Item...");
        }

        /// <summary>
        /// Adds one row per non-null entry in Items. Selects nothing.
        /// </summary>
        private void FillListBox()
        {
            _listBox.Items.Clear();

            foreach (ListViewDialogItem item in GetItemsOrEmpty())
            {
                _ = _listBox.Items.Add(item);
            }
        }

        /// <summary>
        /// Sizes the form to fit the longest Name, with a minimum of 20 font heights wide and 14 tall,
        /// clamped to 90% of the working area of the screen it is on.
        /// </summary>
        private void SizeToContent()
        {
            int h = Font.Height;
            int pad = h / 2;

            int longest = 0;
            foreach (ListViewDialogItem item in GetItemsOrEmpty())
            {
                Size textSize = TextRenderer.MeasureText(item.Name ?? string.Empty, Font);
                longest = Math.Max(longest, textSize.Width);
            }

            // Root padding, group padding and the list's vertical scroll bar.
            int chrome = (pad * 4) + SystemInformation.VerticalScrollBarWidth;
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Math.Max(h * 20, longest + chrome), (int)(work.Width * 0.9));
            int height = Math.Min(h * 14, (int)(work.Height * 0.9));
            ClientSize = new Size(width, height);
        }

        private void ListBoxMouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = _listBox.IndexFromPoint(e.Location);
            if (index >= 0 && index == _listBox.SelectedIndex)
            {
                AcceptSelection();
            }
        }

        private void ListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && _listBox.SelectedIndex >= 0)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                AcceptSelection();
            }
        }

        /// <summary>
        /// Stores the selected row in SelectedItem and closes with OK. Does nothing when no row is selected.
        /// </summary>
        private void AcceptSelection()
        {
            ListViewDialogItem item = _listBox.SelectedItem as ListViewDialogItem;
            if (item == null)
            {
                return;
            }

            SelectedItem = item;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Items with null entries removed. Null Items gives an empty list.
        /// </summary>
        private List<ListViewDialogItem> GetItemsOrEmpty()
        {
            List<ListViewDialogItem> result = new List<ListViewDialogItem>();
            if (Items == null)
            {
                return result;
            }

            foreach (ListViewDialogItem item in Items)
            {
                if (item != null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the localised text for a key, or the English fallback when there is no language source or no value.
        /// </summary>
        private string Lookup(string key, string fallback)
        {
            string value = Language?.GetString(key);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
