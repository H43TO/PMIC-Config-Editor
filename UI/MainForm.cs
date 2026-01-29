using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PMICDumpParser.Models;
using PMICDumpParser.Services;
using PMICDumpParser.UI.Controls;

namespace PMICDumpParser
{
    /// <summary>
    /// Main application form for PMIC Dump Parser
    /// Provides a comprehensive interface for analyzing PMIC register dumps
    /// </summary>
    public partial class MainForm : Form
    {
        #region Fields

        private PmicDump _currentDump;
        private readonly Dictionary<string, ListView> _categoryListViews = new Dictionary<string, ListView>();
        private readonly Dictionary<string, List<ParsedRegister>> _categoryRegisters = new Dictionary<string, List<ParsedRegister>>();
        private CancellationTokenSource _loadCancellationTokenSource;

        // UI Controls
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _fileInfoLabel;
        private ToolStripStatusLabel _regCountLabel;
        private ToolStripProgressBar _progressBar;
        private TabControl _tabControl;
        private RegisterListView _detailedListView;
        private TreeView _categoryTreeView;
        private TableLayoutPanel _summaryGrid;
        private Panel _summaryStatsPanel;
        private PropertyGrid _detailPropertyGrid;
        private ToolStripTextBox _searchBox;
        private ToolStripButton _btnChangedOnly;
        private ToolStripButton _btnProtectedOnly;
        private ToolStripButton _btnResetFilter;

        // Statistics labels
        private Label _statTotal;
        private Label _statChanged;
        private Label _statProtected;
        private Label _statCritical;
        private Label _statChangedPercent;

        // Professional color scheme
        private readonly Color _changedColor = Color.FromArgb(220, 240, 255);  // Light blue
        private readonly Color _protectedColor = Color.FromArgb(255, 250, 205); // Light yellow
        private readonly Color _criticalColor = Color.FromArgb(255, 230, 230);  // Light red
        private readonly Color _defaultGridColor = Color.FromArgb(245, 245, 245);
        private readonly Color _unchangedColor = Color.FromArgb(230, 255, 230); // Light green for unchanged
        private readonly Color _gridBorderColor = Color.FromArgb(230, 230, 230);
        private readonly Color _selectedGridColor = Color.FromArgb(70, 130, 180); // Steel blue

        // Constants for critical change detection
        private const double CRITICAL_THRESHOLD_PERCENT = 23.0;
        private const double MAX_VOLTAGE_RANGE = 440.0; // Based on voltage register range

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the MainForm class
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            // Initialize the application asynchronously without blocking the constructor
            this.Load += async (sender, e) => await InitializeApplicationAsync();
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Asynchronously initializes the application by loading register definitions
        /// </summary>
        private async Task InitializeApplicationAsync()
        {
            try
            {
                // Make sure we're on the UI thread when showing progress
                if (InvokeRequired)
                {
                    Invoke(new Action(() => ShowProgress("Loading register definitions...", 0)));
                }
                else
                {
                    ShowProgress("Loading register definitions...", 0);
                }

                await RegisterLoaderService.LoadAsync().ConfigureAwait(false);

                // Update status on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        UpdateStatus("Register definitions loaded successfully");
                        HideProgress();
                    }));
                }
                else
                {
                    UpdateStatus("Register definitions loaded successfully");
                    HideProgress();
                }
            }
            catch (Exception ex)
            {
                // Handle error on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        HideProgress();
                        MessageBox.Show($"Failed to load register definitions: {ex.Message}", "Initialization Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                else
                {
                    HideProgress();
                    MessageBox.Show($"Failed to load register definitions: {ex.Message}", "Initialization Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region UI Event Handlers

        private async void OnOpenDump(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "PMIC Dump Files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Open PMIC Dump File",
                Multiselect = false
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    await LoadDumpFileAsync(dialog.FileName);
                }
            }
        }

        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            if (_currentDump != null)
            {
                _detailedListView.SearchText = _searchBox.Text;
                _detailedListView.ApplyFilters();
            }
        }

        private void OnSearchEnter(object sender, EventArgs e)
        {
            if (_searchBox.Text == "Search...")
            {
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.Black;
            }
        }

        private void OnSearchLeave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchBox.Text = "Search...";
                _searchBox.ForeColor = Color.Gray;
            }
        }

        private void OnToggleChangedFilter(object sender, EventArgs e)
        {
            _detailedListView.ShowChangedOnly = _btnChangedOnly.Checked;
            _detailedListView.ApplyFilters();
        }

        private void OnToggleProtectedFilter(object sender, EventArgs e)
        {
            _detailedListView.ShowProtectedOnly = _btnProtectedOnly.Checked;
            _detailedListView.ApplyFilters();
        }

        private void OnResetFilter(object sender, EventArgs e)
        {
            _btnChangedOnly.Checked = false;
            _btnProtectedOnly.Checked = false;
            _searchBox.Text = "Search...";
            _searchBox.ForeColor = Color.Gray;

            if (_currentDump != null)
            {
                _detailedListView.ShowChangedOnly = false;
                _detailedListView.ShowProtectedOnly = false;
                _detailedListView.SearchText = string.Empty;
                _detailedListView.ApplyFilters();
            }
        }

        private async void OnReloadDefinitions(object sender, EventArgs e)
        {
            try
            {
                ShowProgress("Reloading register definitions...", 0);
                RegisterLoaderService.ResetCache();
                await RegisterLoaderService.LoadAsync().ConfigureAwait(false);
                HideProgress();

                if (_currentDump != null)
                {
                    await LoadDumpFileAsync(_currentDump.FilePath);
                    MessageBox.Show("Register definitions reloaded successfully!", "Reload Complete");
                }
                else
                {
                    MessageBox.Show("Register definitions reloaded. Load a dump file to apply changes.", "Reload Complete");
                }
            }
            catch (Exception ex)
            {
                HideProgress();
                MessageBox.Show($"Error reloading definitions: {ex.Message}", "Error");
            }
        }

        private void OnExportCsv(object sender, EventArgs e)
        {
            if (_currentDump == null)
            {
                MessageBox.Show("No dump loaded", "Info");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"PMIC_Dump_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ExportToCsv(saveDialog.FileName);
                    UpdateStatus($"Exported to CSV: {Path.GetFileName(saveDialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting CSV: {ex.Message}", "Error");
                }
            }
        }

        private async void OnSaveReport(object sender, EventArgs e)
        {
            if (_currentDump == null)
            {
                MessageBox.Show("No dump loaded", "Info");
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
                FileName = $"PMIC_Dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            })
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        ShowProgress("Generating report...", 0);
                        var report = DumpParserService.GenerateReport(_currentDump);
                        await FileHelper.WriteTextAsync(dialog.FileName, report).ConfigureAwait(false);
                        HideProgress();
                        MessageBox.Show($"Report saved to {dialog.FileName}", "Success");
                    }
                    catch (Exception ex)
                    {
                        HideProgress();
                        MessageBox.Show($"Error saving report: {ex.Message}", "Error");
                    }
                }
            }
        }

        private void OnUserGuide(object sender, EventArgs e)
        {
            MessageBox.Show("PMIC Dump Parser User Guide\n\n1. Open a PMIC dump file (.bin)\n2. View registers in different tabs\n3. Double-click any register for detailed view\n4. Use filters and search to find specific registers\n5. Export reports as needed", "User Guide");
        }

        private void OnAbout(object sender, EventArgs e)
        {
            MessageBox.Show("PMIC Dump Parser v1.0\n\nA tool for analyzing PMIC register dumps\nSupports RTQ5132 and compatible PMICs\n\n© 2024 PMIC Tools", "About");
        }

        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Sorting is handled by the custom RegisterListView control
        }

        private void OnCategorySelected(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is ParsedRegister reg)
            {
                UpdateDetailPanel(reg);
                if (_categoryListViews.TryGetValue("Category", out var listView))
                {
                    foreach (ListViewItem item in listView.Items)
                    {
                        if (item.Tag == reg)
                        {
                            item.Selected = true;
                            item.EnsureVisible();
                            break;
                        }
                    }
                }
            }
            else if (e.Node.Tag is string category && _categoryRegisters.TryGetValue(category, out var registers))
            {
                if (_categoryListViews.TryGetValue("Category", out var listView))
                {
                    listView.BeginUpdate();
                    listView.Items.Clear();

                    foreach (var categoryReg in registers.OrderBy(r => r.Address))
                    {
                        var item = new ListViewItem(categoryReg.AddrHex);
                        item.SubItems.Add(categoryReg.Name);
                        item.SubItems.Add(categoryReg.ValHex);
                        item.SubItems.Add(categoryReg.DefaultHex);
                        item.SubItems.Add(categoryReg.IsChanged ? "CHANGED" : "DEFAULT");
                        item.SubItems.Add(categoryReg.DecodedValue);
                        item.Tag = categoryReg;

                        if (categoryReg.IsChanged)
                            item.BackColor = _changedColor;
                        else if (categoryReg.Definition.Protected == true)
                            item.BackColor = _protectedColor;

                        listView.Items.Add(item);
                    }

                    listView.EndUpdate();
                    AutoSizeCategoryListViewColumns(listView);
                }
            }
        }

        private void OnCategoryNodeDoubleClick(TreeNode node)
        {
            if (node.Tag is ParsedRegister reg)
            {
                ShowRegisterDetails(reg);
            }
        }

        #endregion

        #region Core Business Logic

        /// <summary>
        /// Loads and parses a PMIC dump file asynchronously
        /// </summary>
        /// <param name="filePath">Path to the dump file</param>
        private async Task LoadDumpFileAsync(string filePath)
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Show progress on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() => ShowProgress("Loading dump file...", 0)));
                }
                else
                {
                    ShowProgress("Loading dump file...", 0);
                }

                _currentDump = await DumpParserService.ParseAsync(filePath).ConfigureAwait(false);

                // Update UI on the UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        UpdateAllViews();
                        UpdateStatusBar();
                        UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
                        HideProgress();
                    }));
                }
                else
                {
                    UpdateAllViews();
                    UpdateStatusBar();
                    UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
                    HideProgress();
                }
            }
            catch (OperationCanceledException)
            {
                // Hide progress on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        HideProgress();
                        UpdateStatus("Load cancelled");
                    }));
                }
                else
                {
                    HideProgress();
                    UpdateStatus("Load cancelled");
                }
            }
            catch (Exception ex)
            {
                // Handle error on UI thread
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        HideProgress();
                        MessageBox.Show($"Error loading dump: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatus("Error loading file");
                    }));
                }
                else
                {
                    HideProgress();
                    MessageBox.Show($"Error loading dump: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatus("Error loading file");
                }
            }
        }

        /// <summary>
        /// Updates all UI views with current dump data
        /// </summary>
        private void UpdateAllViews()
        {
            if (_currentDump == null) return;
            UpdateDetailedListView();
            UpdateCategoryTree();
            UpdateSummaryGrid();
        }

        /// <summary>
        /// Updates the detailed list view with current dump data
        /// </summary>
        private void UpdateDetailedListView()
        {
            if (_currentDump == null) return;

            _detailedListView.LoadRegisters(_currentDump.Registers.Values);
        }

        /// <summary>
        /// Updates the category tree view with current dump data
        /// </summary>
        private void UpdateCategoryTree()
        {
            if (_currentDump == null) return;

            _categoryTreeView.BeginUpdate();
            _categoryTreeView.Nodes.Clear();
            _categoryRegisters.Clear();

            try
            {
                var categories = _currentDump.Registers.Values
                    .GroupBy(r => r.Category)
                    .OrderBy(g => g.Key);

                foreach (var category in categories)
                {
                    var categoryNode = new TreeNode($"{category.Key} ({category.Count()})")
                    {
                        Tag = category.Key
                    };

                    foreach (var reg in category.OrderBy(r => r.Address))
                    {
                        string status = reg.IsChanged ? " [CHANGED]" : "";
                        string protectedFlag = reg.Definition.Protected == true ? " [PROTECTED]" : "";

                        var regNode = new TreeNode($"{reg.AddrHex}: {reg.Name}{status}{protectedFlag}")
                        {
                            Tag = reg
                        };

                        if (reg.IsChanged && reg.Definition.Protected == false)
                            regNode.ForeColor = Color.FromArgb(0, 102, 204);
                        else if (reg.Definition.Protected == true)
                        {
                            regNode.ForeColor = Color.FromArgb(153, 102, 0);

                            if (reg.IsChanged == true)
                                regNode.ForeColor = IsCriticalChange(reg) ? Color.FromArgb(224, 63, 63) : Color.FromArgb(98, 182, 245);
                        }

                        categoryNode.Nodes.Add(regNode);
                    }

                    _categoryTreeView.Nodes.Add(categoryNode);
                    _categoryRegisters[category.Key] = category.ToList();
                }
            }
            finally
            {
                _categoryTreeView.EndUpdate();
            }
        }

        /// <summary>
        /// Updates the summary grid with current dump data
        /// </summary>
        private void UpdateSummaryGrid()
        {
            if (_summaryGrid == null || _currentDump == null) return;

            foreach (Control control in _summaryGrid.Controls)
            {
                if (control is Panel cell && cell.Tag is byte address)
                {
                    UpdateGridCellColor(cell, address);
                }
            }

            UpdateStatistics();
        }

        /// <summary>
        /// Updates statistics display with current dump data
        /// </summary>
        private void UpdateStatistics()
        {
            if (_currentDump == null) return;

            var total = _currentDump.Registers.Count;
            var changed = _currentDump.Changed.Count;
            var protectedCount = _currentDump.Protected.Count;
            var critical = _currentDump.Registers.Values.Count(IsCriticalChange);
            var changedPercent = total > 0 ? (changed * 100.0 / total) : 0;

            if (_statTotal != null) _statTotal.Text = total.ToString();
            if (_statChanged != null) _statChanged.Text = changed.ToString();
            if (_statProtected != null) _statProtected.Text = protectedCount.ToString();
            if (_statCritical != null) _statCritical.Text = critical.ToString();
            if (_statChangedPercent != null) _statChangedPercent.Text = $"{changedPercent:F1}%";
        }

        /// <summary>
        /// Updates the status bar with file information
        /// </summary>
        private void UpdateStatusBar()
        {
            if (_currentDump == null)
            {
                _fileInfoLabel.Text = string.Empty;
                _regCountLabel.Text = string.Empty;
                return;
            }

            _fileInfoLabel.Text = $"File: {Path.GetFileName(_currentDump.FilePath)} | Loaded: {_currentDump.LoadTime:yyyy-MM-dd HH:mm:ss}";
            _regCountLabel.Text = $"Total: {_currentDump.Registers.Count} | Changed: {_currentDump.Changed.Count} | Protected: {_currentDump.Protected.Count}";
        }

        /// <summary>
        /// Exports current dump to CSV format
        /// </summary>
        private void ExportToCsv(string filePath)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Address,Name,Full Name,Value,Default,Status,Protected,Category,Decoded Value,Description");

            foreach (var reg in _currentDump.Registers.Values.OrderBy(r => r.Address))
            {
                csv.AppendLine($"\"{reg.AddrHex}\",\"{reg.Name}\",\"{reg.FullName}\",\"{reg.ValHex}\",\"{reg.DefaultHex}\",\"{(reg.IsChanged ? "CHANGED" : "DEFAULT")}\",\"{(reg.Definition.Protected == true ? "Yes" : "No")}\",\"{reg.Category}\",\"{reg.DecodedValue}\",\"{reg.Description.Replace("\"", "\"\"")}\"");
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        /// <summary>
        /// Determines if a register change is critical (>23% change for voltage/current registers)
        /// </summary>
        /// <param name="reg">The register to check</param>
        /// <returns>True if the change is critical, false otherwise</returns>
        private bool IsCriticalChange(ParsedRegister reg)
        {
            var name = reg.Name.ToUpper();
            bool isVoltageReg = name.Contains("VOLT") || name.Contains("SWA_VOLT") ||
                                name.Contains("SWB_VOLT") || name.Contains("SWC_VOLT");
            bool isCurrentReg = name.Contains("CURR") || name.Contains("SWA_CURR") ||
                                name.Contains("SWB_CURR") || name.Contains("SWC_CURR");

            if (!isVoltageReg && !isCurrentReg)
                return false;

            if (!reg.IsChanged)
                return false;

            double nominalValue = reg.DefaultValue;
            if (nominalValue <= 0)
                return false;

            double currentValue = reg.RawValue;
            double absoluteChange = Math.Abs(currentValue - nominalValue);
            double changePercent = (absoluteChange / MAX_VOLTAGE_RANGE) * 100.0;

            return changePercent > CRITICAL_THRESHOLD_PERCENT;
        }

        #endregion

        #region UI Helper Methods

        /// <summary>
        /// Shows progress bar with message (thread-safe)
        /// </summary>
        private void ShowProgress(string message, int value = 0)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => ShowProgress(message, value)));
                return;
            }

            _progressBar.Visible = true;
            _progressBar.Value = value;
            UpdateStatus(message);
        }

        /// <summary>
        /// Hides the progress bar (thread-safe)
        /// </summary>
        private void HideProgress()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(HideProgress));
                return;
            }

            _progressBar.Visible = false;
        }

        /// <summary>
        /// Updates the status label text (thread-safe)
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => _statusLabel.Text = message));
            }
            else
            {
                _statusLabel.Text = message;
            }
        }

        /// <summary>
        /// Handles register selection from list view
        /// </summary>
        private void OnRegisterSelected(object sender, EventArgs e)
        {
            var selected = _detailedListView.GetSelectedRegister();
            if (selected != null)
            {
                UpdateDetailPanel(selected);
            }
        }

        /// <summary>
        /// Handles register double-click to show details
        /// </summary>
        private void OnRegisterDoubleClick(object sender, EventArgs e)
        {
            var selected = _detailedListView.GetSelectedRegister();
            if (selected != null)
            {
                ShowRegisterDetails(selected);
            }
        }

        /// <summary>
        /// Shows detailed view for a specific register
        /// </summary>
        /// <param name="register">The register to display</param>
        private void ShowRegisterDetails(ParsedRegister register)
        {
            var detailForm = new RegisterDetailForm(register);
            detailForm.ShowDialog();
        }

        /// <summary>
        /// Updates the detail property grid with register information
        /// </summary>
        /// <param name="register">The register to display</param>
        private void UpdateDetailPanel(ParsedRegister register)
        {
            var displayObj = new
            {
                Address = register.AddrHex,
                Name = register.Name,
                FullName = register.FullName,
                Category = register.Category,
                Type = $"{register.Definition.Type} {(register.Definition.Protected == true ? "(Protected)" : "")}",
                RawValue = $"{register.ValHex} ({register.RawValue})",
                DefaultValue = $"{register.DefaultHex} ({register.DefaultValue})",
                BinaryValue = Convert.ToString(register.RawValue, 2).PadLeft(8, '0'),
                Status = register.IsChanged ? "CHANGED FROM DEFAULT" : "AT DEFAULT",
                DecodedValue = register.DecodedValue,
                Description = register.Description,
                Fields = register.Definition.Fields?.Count ?? 0,
                IsProtected = register.Definition.Protected == true ? "Yes" : "No"
            };

            _detailPropertyGrid.SelectedObject = displayObj;
        }

        /// <summary>
        /// Updates grid cell color based on register status
        /// </summary>
        private void UpdateGridCellColor(Panel cell, byte address)
        {
            if (_currentDump?.Registers.TryGetValue(address, out var reg) == true)
            {
                if (reg.Category == "Reserved" && reg.Name.StartsWith("RESERVED_"))
                {
                    cell.BackColor = _defaultGridColor;
                }
                else if (reg.IsChanged)
                {
                    bool isCritical = IsCriticalChange(reg);
                    cell.BackColor = isCritical ? _criticalColor : _changedColor;
                }
                else if (reg.Definition.Protected == true)
                {
                    cell.BackColor = _protectedColor;
                }
                else
                {
                    cell.BackColor = _unchangedColor;
                }
            }
            else
            {
                cell.BackColor = _defaultGridColor;
            }
        }

        /// <summary>
        /// Auto-sizes list view columns for optimal display
        /// </summary>
        private void AutoSizeListViewColumns()
        {
            if (_detailedListView.Columns.Count == 0 || _detailedListView.ClientSize.Width <= 0)
                return;

            int totalWidth = _detailedListView.ClientSize.Width;
            int fixedColumnsWidth = 0;

            for (int i = 0; i < _detailedListView.Columns.Count - 1; i++)
            {
                fixedColumnsWidth += _detailedListView.Columns[i].Width;
            }

            int lastColumnWidth = Math.Max(100, totalWidth - fixedColumnsWidth - 5);
            _detailedListView.Columns[_detailedListView.Columns.Count - 1].Width = lastColumnWidth;
        }

        /// <summary>
        /// Auto-sizes category list view columns for optimal display
        /// </summary>
        private void AutoSizeCategoryListViewColumns(ListView listView)
        {
            if (listView.Columns.Count == 0 || listView.ClientSize.Width <= 0)
                return;

            int totalWidth = listView.ClientSize.Width;
            int fixedColumnsWidth = 0;

            for (int i = 0; i < listView.Columns.Count - 1; i++)
            {
                fixedColumnsWidth += listView.Columns[i].Width;
            }

            int lastColumnWidth = Math.Max(100, totalWidth - fixedColumnsWidth - 5);
            listView.Columns[listView.Columns.Count - 1].Width = lastColumnWidth;
        }

        #endregion

        #region UI Initialization

        /// <summary>
        /// Initializes the main form UI components
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "PMIC Dump Parser v1.0";
            this.Size = new Size(Screen.PrimaryScreen.WorkingArea.Width * 3 / 4,
                                 Screen.PrimaryScreen.WorkingArea.Height * 3 / 4);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9);

            // Main layout container
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Menu
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Toolbar
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Content
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Status

            // Menu
            var menuStrip = CreateMenuStrip();
            mainContainer.Controls.Add(menuStrip, 0, 0);

            // Toolbar
            var toolStrip = CreateToolbar();
            mainContainer.Controls.Add(toolStrip, 0, 1);

            // Content area with tabs
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(6, 6),
                Font = new Font("Segoe UI", 9)
            };

            // Summary tab
            var summaryTab = CreateSummaryTab();
            _tabControl.TabPages.Add(summaryTab);

            // Detailed tab
            var detailedTab = CreateDetailedTab();
            _tabControl.TabPages.Add(detailedTab);

            // Category tab
            var categoryTab = CreateCategoryTab();
            _tabControl.TabPages.Add(categoryTab);

            mainContainer.Controls.Add(_tabControl, 0, 2);

            // Status bar
            var statusStrip = CreateStatusBar();
            mainContainer.Controls.Add(statusStrip, 0, 3);

            this.Controls.Add(mainContainer);
        }

        /// <summary>
        /// Creates the main menu strip
        /// </summary>
        private MenuStrip CreateMenuStrip()
        {
            var menuStrip = new MenuStrip
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add("&Open Dump...", null, OnOpenDump);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("&Save Report...", null, OnSaveReport);
            fileMenu.DropDownItems.Add("&Export to CSV...", null, OnExportCsv);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());
            menuStrip.Items.Add(fileMenu);

            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add("&Summary", null, (s, e) => _tabControl.SelectedIndex = 0);
            viewMenu.DropDownItems.Add("&Detailed", null, (s, e) => _tabControl.SelectedIndex = 1);
            viewMenu.DropDownItems.Add("By &Category", null, (s, e) => _tabControl.SelectedIndex = 2);
            menuStrip.Items.Add(viewMenu);

            var toolsMenu = new ToolStripMenuItem("&Tools");
            toolsMenu.DropDownItems.Add("&Reload Definitions", null, OnReloadDefinitions);
            menuStrip.Items.Add(toolsMenu);

            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add("&User Guide", null, OnUserGuide);
            helpMenu.DropDownItems.Add("&About", null, OnAbout);
            menuStrip.Items.Add(helpMenu);

            return menuStrip;
        }

        /// <summary>
        /// Creates the toolbar
        /// </summary>
        private ToolStrip CreateToolbar()
        {
            var toolStrip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 248, 248),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(4, 0, 4, 0)
            };

            var btnOpen = new ToolStripButton("Open", null, OnOpenDump)
            {
                ToolTipText = "Open PMIC dump file",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            var btnSave = new ToolStripButton("Save Report", null, OnSaveReport)
            {
                ToolTipText = "Save analysis report",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            toolStrip.Items.Add(btnOpen);
            toolStrip.Items.Add(btnSave);
            toolStrip.Items.Add(new ToolStripSeparator());

            _btnChangedOnly = new ToolStripButton("Changed Only", null, OnToggleChangedFilter)
            {
                CheckOnClick = true,
                ToolTipText = "Show only changed registers",
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _btnProtectedOnly = new ToolStripButton("Protected Only", null, OnToggleProtectedFilter)
            {
                CheckOnClick = true,
                ToolTipText = "Show only protected registers",
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            _btnResetFilter = new ToolStripButton("Reset Filter", null, OnResetFilter)
            {
                ToolTipText = "Reset all filters",
                DisplayStyle = ToolStripItemDisplayStyle.Text
            };
            toolStrip.Items.Add(_btnChangedOnly);
            toolStrip.Items.Add(_btnProtectedOnly);
            toolStrip.Items.Add(_btnResetFilter);
            toolStrip.Items.Add(new ToolStripSeparator());

            _searchBox = new ToolStripTextBox
            {
                Width = 250,
                ToolTipText = "Search by address, name, value, or decoded value"
            };
            _searchBox.Text = "Search...";
            _searchBox.ForeColor = Color.Gray;
            _searchBox.TextChanged += OnSearchTextChanged;
            _searchBox.Enter += OnSearchEnter;
            _searchBox.Leave += OnSearchLeave;
            toolStrip.Items.Add(new ToolStripLabel("Search:"));
            toolStrip.Items.Add(_searchBox);

            return toolStrip;
        }

        /// <summary>
        /// Creates the summary tab with register grid and statistics
        /// </summary>
        private TabPage CreateSummaryTab()
        {
            var tab = new TabPage("Summary");
            tab.Padding = new Padding(10);
            tab.BackColor = Color.White;

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 500,
                SplitterWidth = 2,
                BackColor = Color.White
            };

            // Grid Panel
            var gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Create 16x16 grid
            _summaryGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 17,
                RowCount = 17,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(1),
                BackColor = _gridBorderColor
            };

            for (int i = 0; i < 17; i++)
            {
                _summaryGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i == 0 ? 4 : 6));
                _summaryGrid.RowStyles.Add(new RowStyle(SizeType.Percent, i == 0 ? 4 : 6));
            }

            // Add headers
            for (int col = 0; col < 16; col++)
            {
                var header = new Label
                {
                    Text = $"{col:X1}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(240, 240, 240),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(80, 80, 80)
                };
                _summaryGrid.Controls.Add(header, col + 1, 0);
            }

            for (int row = 0; row < 16; row++)
            {
                var header = new Label
                {
                    Text = $"{row:X1}0",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(240, 240, 240),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(80, 80, 80)
                };
                _summaryGrid.Controls.Add(header, 0, row + 1);
            }

            // Add cell panels
            for (int row = 0; row < 16; row++)
            {
                for (int col = 0; col < 16; col++)
                {
                    byte address = (byte)((row << 4) | col);
                    var cellPanel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = _defaultGridColor,
                        BorderStyle = BorderStyle.FixedSingle,
                        Margin = new Padding(1),
                        Tag = address,
                        Cursor = Cursors.Hand
                    };

                    var addressLabel = new Label
                    {
                        Text = $"0x{address:X2}",
                        TextAlign = ContentAlignment.MiddleCenter,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 7.5f),
                        ForeColor = Color.FromArgb(100, 100, 100)
                    };

                    cellPanel.Controls.Add(addressLabel);

                    cellPanel.Click += (s, e) =>
                    {
                        if (_currentDump?.Registers.TryGetValue(address, out var reg) == true)
                        {
                            ShowRegisterDetails(reg);
                        }
                        else
                        {
                            var reservedReg = new ParsedRegister
                            {
                                Address = address,
                                RawValue = 0,
                                DefaultValue = 0,
                                Name = $"RESERVED_{address:X2}",
                                FullName = $"Reserved 0x{address:X2}",
                                Category = "Reserved",
                                DecodedValue = "Reserved",
                                Description = $"Reserved register 0x{address:X2}",
                                Definition = RegisterLoaderService.GetDefinition(address)
                            };
                            ShowRegisterDetails(reservedReg);
                        }
                    };

                    addressLabel.Click += (s, e) =>
                    {
                        if (_currentDump?.Registers.TryGetValue(address, out var reg) == true)
                        {
                            ShowRegisterDetails(reg);
                        }
                        else
                        {
                            var reservedReg = new ParsedRegister
                            {
                                Address = address,
                                RawValue = 0,
                                DefaultValue = 0,
                                Name = $"RESERVED_{address:X2}",
                                FullName = $"Reserved 0x{address:X2}",
                                Category = "Reserved",
                                DecodedValue = "Reserved",
                                Description = $"Reserved register 0x{address:X2}",
                                Definition = RegisterLoaderService.GetDefinition(address)
                            };
                            ShowRegisterDetails(reservedReg);
                        }
                    };

                    cellPanel.MouseEnter += (s, e) =>
                    {
                        if (cellPanel.BackColor != _selectedGridColor)
                        {
                            Color originalColor = cellPanel.BackColor;
                            cellPanel.BackColor = Color.FromArgb(
                                Math.Max(originalColor.R - 20, 0),
                                Math.Max(originalColor.G - 20, 0),
                                Math.Max(originalColor.B - 20, 0)
                            );
                        }
                    };

                    cellPanel.MouseLeave += (s, e) =>
                    {
                        if (cellPanel.BackColor != _selectedGridColor)
                        {
                            UpdateGridCellColor(cellPanel, address);
                        }
                    };

                    _summaryGrid.Controls.Add(cellPanel, col + 1, row + 1);
                }
            }

            gridPanel.Controls.Add(_summaryGrid);

            // Right Panel with 3 columns side by side
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var threeColumnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                ColumnStyles =
                {
                    new ColumnStyle(SizeType.Percent, 16.67f),
                    new ColumnStyle(SizeType.Percent, 66.67f),
                    new ColumnStyle(SizeType.Percent, 16.67f)
                },
                Padding = new Padding(5),
                BackColor = Color.White
            };

            // Statistics Column
            var statsColumn = CreateStatisticsColumn();
            threeColumnLayout.Controls.Add(statsColumn, 0, 0);

            // Quick Actions Column
            var actionsColumn = CreateQuickActionsColumn();
            threeColumnLayout.Controls.Add(actionsColumn, 1, 0);

            // Color Legend Column
            var legendColumn = CreateLegendColumn();
            threeColumnLayout.Controls.Add(legendColumn, 2, 0);

            rightPanel.Controls.Add(threeColumnLayout);
            _summaryStatsPanel = rightPanel;

            splitContainer.Panel1.Controls.Add(gridPanel);
            splitContainer.Panel2.Controls.Add(rightPanel);

            tab.Controls.Add(splitContainer);

            return tab;
        }

        /// <summary>
        /// Creates the statistics column for the summary tab
        /// </summary>
        private Control CreateStatisticsColumn()
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var statsGroup = new GroupBox
            {
                Text = "Statistics",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70),
                Padding = new Padding(10)
            };

            var statsTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(5)
            };

            statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            AddStatRow(statsTable, 0, "Total Registers:", "0", out _statTotal);
            AddStatRow(statsTable, 1, "Changed:", "0", out _statChanged);
            AddStatRow(statsTable, 2, "Protected:", "0", out _statProtected);
            AddStatRow(statsTable, 3, "Critical (>20%):", "0", out _statCritical);
            AddStatRow(statsTable, 4, "Changed %:", "0.0%", out _statChangedPercent);

            statsGroup.Controls.Add(statsTable);
            container.Controls.Add(statsGroup);

            return container;
        }

        /// <summary>
        /// Creates the quick actions column for the summary tab
        /// </summary>
        private Control CreateQuickActionsColumn()
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var actionsGroup = new GroupBox
            {
                Text = "Quick Actions",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70),
                Padding = new Padding(10)
            };

            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 5, 0, 5)
            };

            // Button 1: Export All
            var btnExportAll = new Button
            {
                Text = "Export All to CSV",
                Height = 32,
                Width = actionsPanel.ClientSize.Width - 20,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnExportAll.FlatAppearance.BorderSize = 0;
            btnExportAll.Click += OnExportCsv;
            actionsPanel.Controls.Add(btnExportAll);

            // Button 2: Show Changed
            var btnShowChanged = new Button
            {
                Text = "Show Changed Only",
                Height = 32,
                Width = actionsPanel.ClientSize.Width - 20,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.FromArgb(100, 150, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnShowChanged.FlatAppearance.BorderSize = 0;
            btnShowChanged.Click += (s, e) =>
            {
                _btnChangedOnly.Checked = true;
                OnToggleChangedFilter(s, e);
                _tabControl.SelectedIndex = 1;
            };
            actionsPanel.Controls.Add(btnShowChanged);

            // Button 3: Show Protected
            var btnShowProtected = new Button
            {
                Text = "Show Protected Only",
                Height = 32,
                Width = actionsPanel.ClientSize.Width - 20,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnShowProtected.FlatAppearance.BorderSize = 0;
            btnShowProtected.Click += (s, e) =>
            {
                _btnProtectedOnly.Checked = true;
                OnToggleProtectedFilter(s, e);
                _tabControl.SelectedIndex = 1;
            };
            actionsPanel.Controls.Add(btnShowProtected);

            // Button 4: Reset All
            var btnResetAll = new Button
            {
                Text = "Reset All Filters",
                Height = 32,
                Width = actionsPanel.ClientSize.Width - 20,
                Margin = new Padding(0, 0, 0, 0),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnResetAll.FlatAppearance.BorderSize = 0;
            btnResetAll.Click += OnResetFilter;
            actionsPanel.Controls.Add(btnResetAll);

            actionsPanel.Resize += (s, e) =>
            {
                foreach (Control control in actionsPanel.Controls)
                {
                    if (control is Button button)
                    {
                        button.Width = actionsPanel.ClientSize.Width - 20;
                    }
                }
            };

            actionsGroup.Controls.Add(actionsPanel);
            container.Controls.Add(actionsGroup);

            return container;
        }

        /// <summary>
        /// Creates the color legend column for the summary tab
        /// </summary>
        private Control CreateLegendColumn()
        {
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };

            var legendGroup = new GroupBox
            {
                Text = "Color Legend",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70),
                Padding = new Padding(10)
            };

            var legendPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 5, 0, 5)
            };

            AddCompactLegendItem(legendPanel, _unchangedColor, "Unchanged");
            AddCompactLegendItem(legendPanel, _changedColor, "Changed");
            AddCompactLegendItem(legendPanel, _protectedColor, "Protected");
            AddCompactLegendItem(legendPanel, _criticalColor, "Critical (>20%)");
            AddCompactLegendItem(legendPanel, _defaultGridColor, "Reserved");
            AddCompactLegendItem(legendPanel, _selectedGridColor, "Selected");

            legendGroup.Controls.Add(legendPanel);
            container.Controls.Add(legendGroup);

            return container;
        }

        /// <summary>
        /// Adds a statistic row to the statistics table
        /// </summary>
        private void AddStatRow(TableLayoutPanel table, int row, string label, string value, out Label valueLabel)
        {
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            valueLabel = new Label
            {
                Text = value,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(valueLabel, 1, row);
        }

        /// <summary>
        /// Adds a compact legend item to the legend panel
        /// </summary>
        private void AddCompactLegendItem(FlowLayoutPanel panel, Color color, string text)
        {
            var itemPanel = new Panel
            {
                Height = 25,
                Width = panel.ClientSize.Width - 25,
                BackColor = Color.White,
                Margin = new Padding(2)
            };

            var colorBox = new Panel
            {
                Width = 20,
                Height = 20,
                BackColor = color,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(5, 2)
            };

            var textLabel = new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(30, 2),
                Size = new Size(itemPanel.Width - 35, 20),
                Font = new Font("Segoe UI", 8.5f)
            };

            itemPanel.Controls.Add(textLabel);
            itemPanel.Controls.Add(colorBox);
            panel.Controls.Add(itemPanel);
        }

        /// <summary>
        /// Creates the detailed tab with list view and property grid
        /// </summary>
        private TabPage CreateDetailedTab()
        {
            var tab = new TabPage("Detailed");
            tab.BackColor = Color.White;

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = (int)(this.ClientSize.Width * 0.6),
                SplitterWidth = 2,
                BackColor = Color.White
            };

            // List View Panel
            var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            _detailedListView = new RegisterListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Clickable,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.White
            };

            // Add columns with percentage widths
            _detailedListView.Columns.Add("Address", 80, HorizontalAlignment.Center);
            _detailedListView.Columns.Add("Name", 120);
            _detailedListView.Columns.Add("Full Name", 200);
            _detailedListView.Columns.Add("Value", 80, HorizontalAlignment.Center);
            _detailedListView.Columns.Add("Default", 80, HorizontalAlignment.Center);
            _detailedListView.Columns.Add("Status", 100, HorizontalAlignment.Center);
            _detailedListView.Columns.Add("Protected", 80, HorizontalAlignment.Center);
            _detailedListView.Columns.Add("Category", 120);
            _detailedListView.Columns.Add("Decoded Value", 300);

            _detailedListView.RegisterSelected += OnRegisterSelected;
            _detailedListView.RegisterDoubleClicked += OnRegisterDoubleClick;
            _detailedListView.ColumnClick += OnColumnClick;
            _detailedListView.Resize += (s, e) => AutoSizeListViewColumns();

            // Context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("View Details", null, (s, e) => OnRegisterDoubleClick(s, e));
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Copy Address", null, (s, e) => CopyToClipboard(0));
            contextMenu.Items.Add("Copy Value", null, (s, e) => CopyToClipboard(3));
            contextMenu.Items.Add("Copy Decoded Value", null, (s, e) => CopyToClipboard(8));
            _detailedListView.ContextMenuStrip = contextMenu;

            listPanel.Controls.Add(_detailedListView);

            // Property Grid Panel
            var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _detailPropertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = false,
                BackColor = Color.White,
                ViewBackColor = Color.White,
                LineColor = Color.FromArgb(240, 240, 240),
                CategoryForeColor = Color.FromArgb(70, 130, 180),
                Font = new Font("Segoe UI", 9)
            };
            detailPanel.Controls.Add(_detailPropertyGrid);

            splitContainer.Panel1.Controls.Add(listPanel);
            splitContainer.Panel2.Controls.Add(detailPanel);

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// Creates the category tab with tree view and list view
        /// </summary>
        private TabPage CreateCategoryTab()
        {
            var tab = new TabPage("By Category");
            tab.BackColor = Color.White;

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                SplitterWidth = 2,
                BackColor = Color.White
            };

            // Tree View Panel
            var treePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _categoryTreeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Scrollable = true,
                Indent = 20,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _categoryTreeView.AfterSelect += OnCategorySelected;
            _categoryTreeView.NodeMouseDoubleClick += (s, e) => OnCategoryNodeDoubleClick(e.Node);
            treePanel.Controls.Add(_categoryTreeView);

            // List View Panel
            var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Clickable,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.White
            };

            listView.Columns.Add("Address", 80, HorizontalAlignment.Center);
            listView.Columns.Add("Name", 120);
            listView.Columns.Add("Value", 80, HorizontalAlignment.Center);
            listView.Columns.Add("Default", 80, HorizontalAlignment.Center);
            listView.Columns.Add("Status", 100, HorizontalAlignment.Center);
            listView.Columns.Add("Decoded", 250);

            listView.SelectedIndexChanged += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0 && listView.SelectedItems[0].Tag is ParsedRegister reg)
                {
                    UpdateDetailPanel(reg);
                }
            };
            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0 && listView.SelectedItems[0].Tag is ParsedRegister reg)
                {
                    ShowRegisterDetails(reg);
                }
            };
            listView.Resize += (s, e) => AutoSizeCategoryListViewColumns(listView);

            listPanel.Controls.Add(listView);

            splitContainer.Panel1.Controls.Add(treePanel);
            splitContainer.Panel2.Controls.Add(listView);

            _categoryListViews["Category"] = listView;

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// Creates the status bar
        /// </summary>
        private StatusStrip CreateStatusBar()
        {
            var statusStrip = new StatusStrip
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            _statusLabel = new ToolStripStatusLabel("Ready")
            {
                Spring = false,
                BorderSides = ToolStripStatusLabelBorderSides.Right,
                BorderStyle = Border3DStyle.Etched,
                Font = new Font("Segoe UI", 8.5f)
            };

            _fileInfoLabel = new ToolStripStatusLabel
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f)
            };

            _regCountLabel = new ToolStripStatusLabel
            {
                Spring = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            _progressBar = new ToolStripProgressBar
            {
                Style = ProgressBarStyle.Blocks,
                Visible = false,
                Height = 18
            };

            statusStrip.Items.Add(_statusLabel);
            statusStrip.Items.Add(_fileInfoLabel);
            statusStrip.Items.Add(_regCountLabel);
            statusStrip.Items.Add(_progressBar);

            return statusStrip;
        }

        /// <summary>
        /// Copies selected cell text to clipboard
        /// </summary>
        private void CopyToClipboard(int columnIndex)
        {
            if (_detailedListView.SelectedItems.Count > 0)
            {
                Clipboard.SetText(_detailedListView.SelectedItems[0].SubItems[columnIndex].Text);
                UpdateStatus("Copied to clipboard");
            }
        }

        #endregion
    }
}