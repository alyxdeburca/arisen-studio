using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace ArisenStudio.Forms.Pages
{
    /// <summary>
    /// Wrapper over the stock DataGridView for the list pages (design slice S3, the old GridControl + GridView).
    /// Read-only, one full row selected at a time, no row header. Bind a DataTable with <see cref="DataSource"/>;
    /// hide or size columns by name with <see cref="HideColumn"/> and <see cref="SetColumnWidth"/> (they are
    /// remembered across rebinds). Clicking a header sorts.
    /// Selection is read with <see cref="GetSelectedValue"/>, not by row index, because the user can sort.
    /// </summary>
    internal sealed class ListGrid : UserControl
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly HashSet<string> hiddenColumns = new HashSet<string>();
        private readonly Dictionary<string, int> columnWidths = new Dictionary<string, int>();
        private DataTable table;
        private Point dragStart = Point.Empty;
        private bool dragArmed;

        /// <summary>The selected row changed, or the rows were replaced (the selection may now be empty).</summary>
        public event EventHandler SelectionChanged;

        /// <summary>A row was double-clicked (ledger MW1-133, MW1-161, MW2-010).</summary>
        public event EventHandler RowActivated;

        /// <summary>The user pulled a row out of the grid with the left button. The owner starts DoDragDrop.</summary>
        public event EventHandler ItemDragged;

        /// <summary>Something is dragged over the grid. Only raised when <see cref="AllowDropOnGrid"/> is true.</summary>
        public event DragEventHandler GridDragOver;

        /// <summary>Something was dropped on the grid. Only raised when <see cref="AllowDropOnGrid"/> is true.</summary>
        public event DragEventHandler GridDragDrop;

        public ListGrid()
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = SystemColors.Window;

            grid.SelectionChanged += Grid_SelectionChanged;
            grid.CellDoubleClick += Grid_CellDoubleClick;
            grid.MouseDown += Grid_MouseDown;
            grid.MouseMove += Grid_MouseMove;
            grid.MouseUp += Grid_MouseUp;
            grid.DragOver += Grid_DragOver;
            grid.DragDrop += Grid_DragDrop;

            Controls.Add(grid);
        }

        /// <summary>The bound table. Setting it rebinds, re-applies hidden columns and widths, and raises SelectionChanged.</summary>
        public DataTable DataSource
        {
            get { return table; }
            set
            {
                table = value;
                grid.DataSource = value;
                ApplyColumnOptions();
                OnSelectionChanged();
            }
        }

        public bool AllowDropOnGrid
        {
            get { return grid.AllowDrop; }
            set { grid.AllowDrop = value; }
        }

        /// <summary>Hides a column by its table column name. The data stays in the table (ledger "hides ModId").</summary>
        public void HideColumn(string name)
        {
            hiddenColumns.Add(name);
            ApplyColumnOptions();
        }

        /// <summary>Fixes a column to a width in pixels; the other columns share the rest.</summary>
        public void SetColumnWidth(string name, int width)
        {
            columnWidths[name] = width;
            ApplyColumnOptions();
        }

        public int RowCount
        {
            get { return grid.Rows.Count; }
        }

        public bool HasSelection
        {
            get { return grid.SelectedRows.Count > 0; }
        }

        /// <summary>The cell text of the selected row in the named column, or null when no row is selected.</summary>
        public string GetSelectedValue(string columnName)
        {
            if (grid.SelectedRows.Count == 0 || table == null || !table.Columns.Contains(columnName))
            {
                return null;
            }
            DataRowView view = grid.SelectedRows[0].DataBoundItem as DataRowView;
            if (view == null)
            {
                return null;
            }
            object value = view.Row[columnName];
            return value == null || value == DBNull.Value ? null : Convert.ToString(value);
        }

        /// <summary>Selects the first row, or clears the selection when the grid is empty.</summary>
        public void SelectFirstRow()
        {
            if (grid.Rows.Count == 0)
            {
                return;
            }
            grid.ClearSelection();
            grid.Rows[0].Selected = true;
            grid.CurrentCell = grid.Rows[0].Cells[FirstVisibleColumnIndex()];
        }

        private int FirstVisibleColumnIndex()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.Visible)
                {
                    return column.Index;
                }
            }
            return 0;
        }

        private void ApplyColumnOptions()
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.Visible = !hiddenColumns.Contains(column.DataPropertyName) && !hiddenColumns.Contains(column.Name);
                int width;
                if (columnWidths.TryGetValue(column.DataPropertyName, out width) || columnWidths.TryGetValue(column.Name, out width))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = width;
                }
            }
        }

        private void OnSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            OnSelectionChanged();
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            EventHandler handler = RowActivated;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        // Drag start (ledger MW1-162, MW1-163, MW2-011): remember a plain left press on a row, and begin the drag
        // once the pointer leaves the system drag rectangle.
        private void Grid_MouseDown(object sender, MouseEventArgs e)
        {
            dragArmed = false;
            if (e.Button != MouseButtons.Left || Control.ModifierKeys != Keys.None)
            {
                return;
            }
            DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);
            if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0)
            {
                dragStart = e.Location;
                dragArmed = true;
            }
        }

        private void Grid_MouseUp(object sender, MouseEventArgs e)
        {
            dragArmed = false;
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragArmed || (e.Button & MouseButtons.Left) == 0)
            {
                return;
            }
            Size drag = SystemInformation.DragSize;
            Rectangle zone = new Rectangle(dragStart.X - drag.Width / 2, dragStart.Y - drag.Height / 2, drag.Width, drag.Height);
            if (zone.Contains(e.Location))
            {
                return;
            }
            dragArmed = false;
            EventHandler handler = ItemDragged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        /// <summary>Starts a drag with the given data. Called by the owner from its ItemDragged handler.</summary>
        public void BeginDrag(object data)
        {
            grid.DoDragDrop(data, DragDropEffects.Copy);
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            DragEventHandler handler = GridDragOver;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void Grid_DragDrop(object sender, DragEventArgs e)
        {
            DragEventHandler handler = GridDragDrop;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
