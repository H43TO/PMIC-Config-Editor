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
using PMICDumpParser.Utilities;

namespace PMICDumpParser
{
    /// <summary>
    /// Main application form for PMIC Dump Parser
    /// Provides a comprehensive interface for analyzing and editing PMIC register dumps
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
        private TableLayoutPanel _summaryGrid;
        private Panel _summaryStatsPanel;
        private PropertyGrid _detailPropertyGrid;
        private ToolStripTextBox _searchBox;
        private ToolStripButton _btnChangedOnly;
        private ToolStripButton _btnProtectedOnly;
        private ToolStripButton _btnResetFilter;

        // Editor tab
        private RegisterEditorTab _registerEditorTab;

        // Statistics labels
        private Label _statTotal;
        private Label _statChanged;
        private Label _statProtected;
        private Label _statCritical;
        private Label _statChangedPercent;

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
                ShowProgress("Loading register definitions...", 0);
                await RegisterLoaderService.LoadAsync().ConfigureAwait(false);
                UpdateStatus("Register definitions loaded successfully");
                HideProgress();
            }
            catch (Exception ex)
            {
                HideProgress();
                MessageBox.Show($"Failed to load register definitions: {ex.Message}",
                    "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region UI Event Handlers

        /// <summary>
        /// Handles opening a PMIC dump file
        /// </summary>
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

        /// <summary>
        /// Handles search text changes
        /// </summary>
        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            if (_currentDump != null)
            {
                _detailedListView.SearchText = _searchBox.Text;
                _detailedListView.ApplyFilters();
            }
        }

        /// <summary>
        /// Handles search box focus enter
        /// </summary>
        private void OnSearchEnter(object sender, EventArgs e)
        {
            if (_searchBox.Text == "Search...")
            {
                _searchBox.Text = "";
                _searchBox.ForeColor = Color.Black;
            }
        }

        /// <summary>
        /// Handles search box focus leave
        /// </summary>
        private void OnSearchLeave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_searchBox.Text))
            {
                _searchBox.Text = "Search...";
                _searchBox.ForeColor = Color.Gray;
            }
        }

        /// <summary>
        /// Toggles changed-only filter
        /// </summary>
        private void OnToggleChangedFilter(object sender, EventArgs e)
        {
            _detailedListView.ShowChangedOnly = _btnChangedOnly.Checked;
            _detailedListView.ApplyFilters();
        }

        /// <summary>
        /// Toggles protected-only filter
        /// </summary>
        private void OnToggleProtectedFilter(object sender, EventArgs e)
        {
            _detailedListView.ShowProtectedOnly = _btnProtectedOnly.Checked;
            _detailedListView.ApplyFilters();
        }

        /// <summary>
        /// Resets all filters to default state
        /// </summary>
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

        /// <summary>
        /// Reloads register definitions from JSON file
        /// </summary>
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

        /// <summary>
        /// Exports current dump data to CSV format
        /// </summary>
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

        /// <summary>
        /// Generates and saves a detailed text report
        /// </summary>
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

        /// <summary>
        /// Saves edited dump to file (overwrite or new file)
        /// </summary>
        private void OnSaveEditedDump(object sender, EventArgs e)
        {
            if (_currentDump == null)
            {
                MessageBox.Show("No dump loaded", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Save any pending edits in the editor tab
            if (_registerEditorTab != null && _registerEditorTab.HasPendingEdits)
            {
                if (MessageBox.Show("You have unsaved edits in the editor tab. Save them now?",
                    "Save Pending Edits", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _registerEditorTab.SaveEdits();
                }
                else
                {
                    _registerEditorTab.RevertAllEdits();
                }
            }

            using (var dialog = new SaveEditsDialog(_currentDump.FilePath))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Save the dump
                        System.IO.File.WriteAllBytes(dialog.SavePath, _currentDump.RawData);

                        // If saved as new file, reload it
                        if (dialog.SaveAsNewFile)
                        {
                            _ = LoadDumpFileAsync(dialog.SavePath);
                        }
                        else
                        {
                            // Update current dump file path and refresh views
                            _currentDump.FilePath = dialog.SavePath;
                            _currentDump.LoadTime = DateTime.Now;
                            UpdateAllViews();
                            UpdateStatusBar();
                        }

                        UpdateStatus($"Dump saved to: {Path.GetFileName(dialog.SavePath)}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving dump: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Saves all pending edits from editor tab
        /// </summary>
        private void OnSaveAllEdits(object sender, EventArgs e)
        {
            if (_registerEditorTab != null && _registerEditorTab.HasPendingEdits)
            {
                _registerEditorTab.SaveEdits();
                UpdateStatus("All edits saved");
            }
            else
            {
                MessageBox.Show("No pending edits to save.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Reverts all pending edits from editor tab
        /// </summary>
        private void OnRevertAllEdits(object sender, EventArgs e)
        {
            if (_registerEditorTab != null && _registerEditorTab.HasPendingEdits)
            {
                _registerEditorTab.RevertAllEdits();
                UpdateStatus("All edits reverted");
            }
            else
            {
                MessageBox.Show("No pending edits to revert.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Resets all registers to their default values
        /// </summary>
        private void OnResetAllToDefault(object sender, EventArgs e)
        {
            if (_currentDump == null)
                return;

            if (MessageBox.Show("Reset all registers to their default values?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                RegisterEditService.ResetAllChanges(_currentDump);
                UpdateAllViews();
                UpdateStatusBar();
                UpdateStatus("All registers reset to default values");
            }
        }

        /// <summary>
        /// Shows user guide information
        /// </summary>
        private void OnUserGuide(object sender, EventArgs e)
        {
            MessageBox.Show(
                "PMIC Dump Parser User Guide\n\n" +
                "1. Open a PMIC dump file (.bin)\n" +
                "2. View registers in different tabs\n" +
                "3. Double-click any register for detailed view\n" +
                "4. Use filters and search to find specific registers\n" +
                "5. Export reports as needed\n\n" +
                "Editor Tab:\n" +
                "- Edit register values using field editors or direct value input\n" +
                "- Apply changes and save to new or existing dump files",
                "User Guide");
        }

        /// <summary>
        /// Shows about dialog with application information
        /// </summary>
        private void OnAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "PMIC Dump Parser v1.0\n\n" +
                "A tool for analyzing PMIC register dumps\n" +
                "Supports RTQ5132 and compatible PMICs\n\n" +
                "Now with Register Editor!\n\n" +
                "© 2024 PMIC Tools",
                "About");
        }

        /// <summary>
        /// Handles column click for sorting (handled by RegisterListView)
        /// </summary>
        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Sorting is handled by the custom RegisterListView control
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
                ShowProgress("Loading dump file...", 0);
                _currentDump = await DumpParserService.ParseAsync(filePath).ConfigureAwait(false);

                this.InvokeIfRequired(() =>
                {
                    UpdateAllViews();
                    UpdateStatusBar();
                    UpdateStatus($"Loaded: {Path.GetFileName(filePath)}");
                    HideProgress();
                });
            }
            catch (OperationCanceledException)
            {
                this.InvokeIfRequired(() =>
                {
                    HideProgress();
                    UpdateStatus("Load cancelled");
                });
            }
            catch (Exception ex)
            {
                this.InvokeIfRequired(() =>
                {
                    HideProgress();
                    MessageBox.Show($"Error loading dump: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateStatus("Error loading file");
                });
            }
        }

        /// <summary>
        /// Updates all UI views with current dump data
        /// </summary>
        private void UpdateAllViews()
        {
            if (_currentDump == null) return;

            UpdateDetailedListView();
            UpdateSummaryGrid();
            UpdateStatistics();
            UpdateStatusBar();

            // Update editor tab
            if (_registerEditorTab != null)
            {
                _registerEditorTab.LoadDump(_currentDump);
            }

            // Force UI refresh
            this.Refresh();
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
            var critical = _currentDump.Registers.Values.Count(RegisterAnalyzer.IsCriticalChange);
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

        #endregion

        #region UI Helper Methods

        /// <summary>
        /// Shows progress bar with message (thread-safe)
        /// </summary>
        private void ShowProgress(string message, int value = 0)
        {
            this.InvokeIfRequired(() =>
            {
                _progressBar.Visible = true;
                _progressBar.Value = value;
                UpdateStatus(message);
            });
        }

        /// <summary>
        /// Hides the progress bar (thread-safe)
        /// </summary>
        private void HideProgress()
        {
            this.InvokeIfRequired(() =>
            {
                _progressBar.Visible = false;
            });
        }

        /// <summary>
        /// Updates the status label text (thread-safe)
        /// </summary>
        private void UpdateStatus(string message)
        {
            this.BeginInvokeIfRequired(() =>
            {
                _statusLabel.Text = message;
            });
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
                cell.BackColor = RegisterAnalyzer.GetStatusColor(reg);
            }
            else
            {
                cell.BackColor = AppColors.DefaultGrid;
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

        #endregion

        #region UI Initialization

        /// <summary>
        /// Initializes the main form UI components
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "PMIC Dump Parser v1.0 with Editor";
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

            // Editor tab
            var editorTab = CreateEditorTab();
            _tabControl.TabPages.Add(editorTab);

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
            fileMenu.DropDownItems.Add("Save &Edited Dump...", null, OnSaveEditedDump);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("E&xit", null, (s, e) => Close());
            menuStrip.Items.Add(fileMenu);

            var viewMenu = new ToolStripMenuItem("&View");
            viewMenu.DropDownItems.Add("&Summary", null, (s, e) => _tabControl.SelectedIndex = 0);
            viewMenu.DropDownItems.Add("&Detailed", null, (s, e) => _tabControl.SelectedIndex = 1);
            viewMenu.DropDownItems.Add("&Editor", null, (s, e) => _tabControl.SelectedIndex = 2);
            menuStrip.Items.Add(viewMenu);

            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add("&Save All Edits", null, OnSaveAllEdits);
            editMenu.DropDownItems.Add("&Revert All Edits", null, OnRevertAllEdits);
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add("&Reset All to Default", null, OnResetAllToDefault);
            menuStrip.Items.Add(editMenu);

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
            var btnSaveEdits = new ToolStripButton("Save Edits", null, OnSaveEditedDump)
            {
                ToolTipText = "Save edited dump file",
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };
            toolStrip.Items.Add(btnOpen);
            toolStrip.Items.Add(btnSave);
            toolStrip.Items.Add(btnSaveEdits);
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
                BackColor = AppColors.GridBorder
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
                        BackColor = AppColors.DefaultGrid,
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

                    // Click event for showing register details
                    cellPanel.Click += (s, e) => ShowRegisterCellDetails(address);
                    addressLabel.Click += (s, e) => ShowRegisterCellDetails(address);

                    // Hover effects
                    cellPanel.MouseEnter += (s, e) =>
                    {
                        if (cellPanel.BackColor != AppColors.Selected)
                        {
                            cellPanel.BackColor = AppColors.GetHoverColor(cellPanel.BackColor);
                        }
                    };

                    cellPanel.MouseLeave += (s, e) =>
                    {
                        if (cellPanel.BackColor != AppColors.Selected)
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
        /// Shows register details when clicking on a grid cell
        /// </summary>
        private void ShowRegisterCellDetails(byte address)
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
                ForeColor = AppColors.LabelText
            };

            valueLabel = new Label
            {
                Text = value,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.ValueText
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(valueLabel, 1, row);
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
            var btnExportAll = UtilityExtensions.CreateStyledButton(
                "Export All to CSV", AppColors.Info, OnExportCsv);
            btnExportAll.Width = actionsPanel.ClientSize.Width - 20;
            btnExportAll.Margin = new Padding(0, 0, 0, 8);
            actionsPanel.Controls.Add(btnExportAll);

            // Button 2: Show Changed
            var btnShowChanged = UtilityExtensions.CreateStyledButton(
                "Show Changed Only", Color.FromArgb(100, 150, 200), (s, e) =>
                {
                    _btnChangedOnly.Checked = true;
                    OnToggleChangedFilter(s, e);
                    _tabControl.SelectedIndex = 1;
                });
            btnShowChanged.Width = actionsPanel.ClientSize.Width - 20;
            btnShowChanged.Margin = new Padding(0, 0, 0, 8);
            actionsPanel.Controls.Add(btnShowChanged);

            // Button 3: Show Protected
            var btnShowProtected = UtilityExtensions.CreateStyledButton(
                "Show Protected Only", AppColors.Warning, (s, e) =>
                {
                    _btnProtectedOnly.Checked = true;
                    OnToggleProtectedFilter(s, e);
                    _tabControl.SelectedIndex = 1;
                });
            btnShowProtected.ForeColor = Color.Black;
            btnShowProtected.Width = actionsPanel.ClientSize.Width - 20;
            btnShowProtected.Margin = new Padding(0, 0, 0, 8);
            actionsPanel.Controls.Add(btnShowProtected);

            // Button 4: Reset All
            var btnResetAll = UtilityExtensions.CreateStyledButton(
                "Reset All Filters", Color.FromArgb(108, 117, 125), OnResetFilter);
            btnResetAll.Width = actionsPanel.ClientSize.Width - 20;
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

            AddCompactLegendItem(legendPanel, AppColors.Unchanged, "Unchanged");
            AddCompactLegendItem(legendPanel, AppColors.Changed, "Changed");
            AddCompactLegendItem(legendPanel, AppColors.Protected, "Protected");
            AddCompactLegendItem(legendPanel, AppColors.Critical, "Critical (>20%)");
            AddCompactLegendItem(legendPanel, AppColors.DefaultGrid, "Reserved");
            AddCompactLegendItem(legendPanel, AppColors.Selected, "Selected");

            legendGroup.Controls.Add(legendPanel);
            container.Controls.Add(legendGroup);

            return container;
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
                LineColor = AppColors.GridBorder,
                CategoryForeColor = AppColors.Selected,
                Font = new Font("Segoe UI", 9)
            };
            detailPanel.Controls.Add(_detailPropertyGrid);

            splitContainer.Panel1.Controls.Add(listPanel);
            splitContainer.Panel2.Controls.Add(detailPanel);

            tab.Controls.Add(splitContainer);
            return tab;
        }

        /// <summary>
        /// Creates the editor tab for modifying register values
        /// </summary>
        private TabPage CreateEditorTab()
        {
            var tab = new TabPage("Editor");
            tab.BackColor = Color.White;

            _registerEditorTab = new RegisterEditorTab
            {
                Dock = DockStyle.Fill
            };

            // Subscribe to edits saved event to update all views
            _registerEditorTab.EditsSaved += (s, e) =>
            {
                UpdateAllViews();
                UpdateStatusBar();
                UpdateStatus("Edits saved and applied to dump");
            };

            tab.Controls.Add(_registerEditorTab);
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