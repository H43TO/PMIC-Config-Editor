using PMICDumpParser.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls
{
    /// <summary>
    /// Enhanced ListView for displaying PMIC registers with filtering and sorting
    /// </summary>
    public class RegisterListView : ListView
    {
        private List<ParsedRegister> _allRegisters = new();
        private readonly ListViewColumnSorter _columnSorter;

        // Filter states
        public bool ShowChangedOnly { get; set; }
        public bool ShowProtectedOnly { get; set; }
        public string SearchText { get; set; } = string.Empty;

        // Color scheme
        private readonly Color _changedColor = Color.FromArgb(220, 240, 255);
        private readonly Color _protectedColor = Color.FromArgb(255, 250, 205);
        private readonly Color _criticalColor = Color.FromArgb(255, 230, 230);
        private readonly Color _unchangedColor = Color.FromArgb(230, 255, 230);

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

        private void SetupEvents()
        {
            SelectedIndexChanged += (s, e) => OnRegisterSelected();
            DoubleClick += (s, e) => OnRegisterDoubleClick();
            ColumnClick += OnColumnClick;
            Resize += (s, e) => AutoResizeColumns();
        }

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

        private void ApplyItemStyle(ListViewItem item, ParsedRegister reg)
        {
            bool isCritical = IsCriticalChange(reg);

            if (reg.Definition.Protected == true)
                item.BackColor = isCritical ? _criticalColor : _protectedColor;
            else if (reg.IsChanged)
                item.BackColor = isCritical ? _criticalColor : _changedColor;
            else if (reg.Category != "Reserved")
                item.BackColor = _unchangedColor;
        }

        private bool IsCriticalChange(ParsedRegister reg)
        {
            // Simplified critical change detection - can be enhanced
            var name = reg.Name.ToUpper();
            bool isVoltageReg = name.Contains("VOLT") || name.Contains("SWA_VOLT") ||
                                name.Contains("SWB_VOLT") || name.Contains("SWC_VOLT");
            bool isCurrentReg = name.Contains("CURR") || name.Contains("SWA_CURR") ||
                                name.Contains("SWB_CURR") || name.Contains("SWC_CURR");

            if (!isVoltageReg && !isCurrentReg) return false;
            if (!reg.IsChanged) return false;

            double nominalValue = reg.DefaultValue;
            if (nominalValue <= 0) return false;

            const double MAX_VOLTAGE_RANGE = 440.0;
            const double CRITICAL_THRESHOLD_PERCENT = 23.0;

            double currentValue = reg.RawValue;
            double absoluteChange = Math.Abs(currentValue - nominalValue);
            double changePercent = (absoluteChange / MAX_VOLTAGE_RANGE) * 100.0;

            return changePercent > CRITICAL_THRESHOLD_PERCENT;
        }

        private void OnRegisterSelected()
        {
            var selected = GetSelectedRegister();
            if (selected != null)
            {
                RegisterSelected?.Invoke(this, new RegisterSelectedEventArgs(selected));
            }
        }

        private void OnRegisterDoubleClick()
        {
            if (GetSelectedRegister() != null)
            {
                RegisterDoubleClicked?.Invoke(this, EventArgs.Empty);
            }
        }

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

        private void CopyToClipboard(int columnIndex)
        {
            if (SelectedItems.Count > 0)
            {
                Clipboard.SetText(SelectedItems[0].SubItems[columnIndex].Text);
            }
        }

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

            // Special handling for numeric/hex columns
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