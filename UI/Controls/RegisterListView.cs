using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls
{
    /// <summary>
    /// Enhanced ListView for displaying PMIC registers with filtering, sorting, and consistent coloring
    /// </summary>
    public class RegisterListView : ListView
    {
        private List<ParsedRegister> _allRegisters = new();
        private readonly ListViewColumnSorter _columnSorter;

        // Filter states
        public bool ShowChangedOnly { get; set; }
        public bool ShowProtectedOnly { get; set; }
        public string SearchText { get; set; } = string.Empty;

        // Events for register selection
        public event EventHandler<RegisterSelectedEventArgs>? RegisterSelected;
        public event EventHandler? RegisterDoubleClicked;

        public RegisterListView()
        {
            InitializeListView();
            _columnSorter = new ListViewColumnSorter();
            ListViewItemSorter = _columnSorter;
        }

        /// <summary>
        /// Populates the list with registers and applies current filters
        /// </summary>
        public void LoadRegisters(IEnumerable<ParsedRegister> registers)
        {
            _allRegisters = new List<ParsedRegister>(registers);
            ApplyFilters();
        }

        /// <summary>
        /// Applies current filters and refreshes the display
        /// </summary>
        public void ApplyFilters()
        {
            BeginUpdate();
            Items.Clear();

            var filteredRegisters = FilterRegisters(_allRegisters);

            foreach (var reg in filteredRegisters)
            {
                var item = CreateListViewItem(reg);
                Items.Add(item);
            }

            AutoResizeColumns();
            EndUpdate();
        }

        /// <summary>
        /// Gets the currently selected register
        /// </summary>
        public ParsedRegister? GetSelectedRegister()
        {
            if (SelectedItems.Count > 0 && SelectedItems[0].Tag is ParsedRegister reg)
            {
                return reg;
            }
            return null;
        }

        #region Private Methods

        /// <summary>
        /// Initializes the ListView with columns and event handlers
        /// </summary>
        private void InitializeListView()
        {
            View = View.Details;
            FullRowSelect = true;
            GridLines = true;
            HeaderStyle = ColumnHeaderStyle.Clickable;
            Font = new Font("Segoe UI", 9);
            BackColor = Color.White;

            SetupColumns();
            SetupEvents();
            SetupContextMenu();
        }

        /// <summary>
        /// Sets up the ListView columns with optimized widths
        /// </summary>
        private void SetupColumns()
        {
            Columns.Add("Address", 80, HorizontalAlignment.Center);
            Columns.Add("Name", 120);
            Columns.Add("Full Name", 200);
            Columns.Add("Value", 80, HorizontalAlignment.Center);
            Columns.Add("Default", 80, HorizontalAlignment.Center);
            Columns.Add("Status", 100, HorizontalAlignment.Center);
            Columns.Add("Protected", 80, HorizontalAlignment.Center);
            Columns.Add("Category", 120);
            Columns.Add("Decoded Value", 300);
        }

        /// <summary>
        /// Sets up event handlers for the ListView
        /// </summary>
        private void SetupEvents()
        {
            SelectedIndexChanged += (s, e) => OnRegisterSelected();
            DoubleClick += (s, e) => OnRegisterDoubleClick();
            ColumnClick += OnColumnClick;
            Resize += (s, e) => AutoResizeColumns();
        }

        /// <summary>
        /// Creates a context menu with useful actions
        /// </summary>
        private void SetupContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            var viewDetailsItem = new ToolStripMenuItem("View Details", null, (s, e) => OnRegisterDoubleClick());
            var separator = new ToolStripSeparator();
            var copyAddressItem = new ToolStripMenuItem("Copy Address", null, (s, e) => CopyToClipboard(0));
            var copyValueItem = new ToolStripMenuItem("Copy Value", null, (s, e) => CopyToClipboard(3));
            var copyDecodedItem = new ToolStripMenuItem("Copy Decoded Value", null, (s, e) => CopyToClipboard(8));

            contextMenu.Items.AddRange(new ToolStripItem[] {
                viewDetailsItem, separator, copyAddressItem, copyValueItem, copyDecodedItem
            });

            ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Filters registers based on current filter settings
        /// </summary>
        private IEnumerable<ParsedRegister> FilterRegisters(IEnumerable<ParsedRegister> registers)
        {
            var filtered = registers;

            if (ShowChangedOnly)
                filtered = filtered.Where(r => r.IsChanged);

            if (ShowProtectedOnly)
                filtered = filtered.Where(r => r.Definition.Protected == true);

            if (!string.IsNullOrEmpty(SearchText) && !SearchText.Equals("Search...", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r =>
                    r.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.FullName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.AddrHex.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.ValHex.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.DecodedValue.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    r.Category.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
                );
            }

            return filtered.OrderBy(r => r.Address);
        }

        /// <summary>
        /// Creates a ListViewItem for a register with appropriate styling
        /// </summary>
        private ListViewItem CreateListViewItem(ParsedRegister reg)
        {
            var item = new ListViewItem(reg.AddrHex);
            item.SubItems.Add(reg.Name);
            item.SubItems.Add(reg.FullName);
            item.SubItems.Add(reg.ValHex);
            item.SubItems.Add(reg.DefaultHex);
            item.SubItems.Add(reg.IsChanged ? "CHANGED" : "DEFAULT");
            item.SubItems.Add(reg.Definition.Protected == true ? "Yes" : "No");
            item.SubItems.Add(reg.Category);
            item.SubItems.Add(reg.DecodedValue);
            item.Tag = reg;

            ApplyItemStyle(item, reg);
            return item;
        }

        /// <summary>
        /// Applies color coding to a ListViewItem based on register status
        /// </summary>
        private void ApplyItemStyle(ListViewItem item, ParsedRegister reg)
        {
            if (reg == null) return;

            // First check if register is reserved
            bool isReserved = RegisterAnalyzer.IsReserved(reg);

            // Check if register is editable
            bool isEditable = RegisterEditService.CanEditRegister(reg.Definition);

            // Get status colors (same logic as detailed tab)
            bool isProtected = RegisterAnalyzer.IsProtected(reg);
            bool isCritical = RegisterAnalyzer.IsCriticalChange(reg);

            // Apply the color based on status
            item.BackColor = AppColors.GetRegisterColor(reg.IsChanged, isProtected, isCritical, isReserved);

            // Set text color based on status
            if (isReserved)
            {
                item.ForeColor = AppColors.DisabledText;
            }
            else if (!isEditable)
            {
                item.ForeColor = AppColors.DisabledText;
            }
            else if (reg.IsChanged)
            {
                item.ForeColor = AppColors.ValueText;
            }
            else
            {
                item.ForeColor = AppColors.LabelText;
            }
        }

        /// <summary>
        /// Raises the RegisterSelected event when a register is selected
        /// </summary>
        private void OnRegisterSelected()
        {
            var selected = GetSelectedRegister();
            if (selected != null)
            {
                RegisterSelected?.Invoke(this, new RegisterSelectedEventArgs(selected));
            }
        }

        /// <summary>
        /// Raises the RegisterDoubleClicked event when a register is double-clicked
        /// </summary>
        private void OnRegisterDoubleClick()
        {
            if (GetSelectedRegister() != null)
            {
                RegisterDoubleClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handles column clicking for sorting
        /// </summary>
        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (Items.Count == 0) return;

            if (e.Column == _columnSorter.SortColumn)
            {
                _columnSorter.Order = _columnSorter.Order == SortOrder.Ascending ?
                    SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _columnSorter.SortColumn = e.Column;
                _columnSorter.Order = SortOrder.Ascending;
            }

            Sort();
        }

        /// <summary>
        /// Copies the specified column text to clipboard
        /// </summary>
        private void CopyToClipboard(int columnIndex)
        {
            if (SelectedItems.Count > 0)
            {
                Clipboard.SetText(SelectedItems[0].SubItems[columnIndex].Text);
            }
        }

        /// <summary>
        /// Auto-resizes columns to fit content and window width
        /// </summary>
        private void AutoResizeColumns()
        {
            if (Columns.Count == 0 || ClientSize.Width <= 0) return;

            int totalWidth = ClientSize.Width;
            int fixedColumnsWidth = 0;

            for (int i = 0; i < Columns.Count - 1; i++)
            {
                fixedColumnsWidth += Columns[i].Width;
            }

            int lastColumnWidth = Math.Max(100, totalWidth - fixedColumnsWidth - 5);
            Columns[Columns.Count - 1].Width = lastColumnWidth;
        }

        #endregion
    }

    /// <summary>
    /// Custom column sorter for ListView columns
    /// </summary>
    public class ListViewColumnSorter : System.Collections.IComparer
    {
        public int SortColumn { get; set; } = 0;
        public SortOrder Order { get; set; } = SortOrder.Ascending;

        public int Compare(object? x, object? y)
        {
            if (x == null || y == null) return 0;

            var itemX = (ListViewItem)x;
            var itemY = (ListViewItem)y;

            string textX = itemX.SubItems[SortColumn].Text;
            string textY = itemY.SubItems[SortColumn].Text;

            // Special handling for numeric/hex columns (Address, Value, Default)
            if (SortColumn == 0 || SortColumn == 3 || SortColumn == 4)
            {
                if (int.TryParse(textX.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out int numX) &&
                    int.TryParse(textY.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out int numY))
                {
                    return Order == SortOrder.Ascending ? numX.CompareTo(numY) : numY.CompareTo(numX);
                }
            }

            return Order == SortOrder.Ascending
                ? string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase)
                : string.Compare(textY, textX, StringComparison.OrdinalIgnoreCase);
        }
    }
}