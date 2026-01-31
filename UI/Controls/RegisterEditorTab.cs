using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls
{
    /// <summary>
    /// Tab for editing PMIC registers with intuitive controls - Using centralized AppColors
    /// </summary>
    public class RegisterEditorTab : UserControl
    {
        private PmicDump _currentDump;
        private ParsedRegister _selectedRegister;
        private Dictionary<byte, byte> _pendingEdits = new Dictionary<byte, byte>();

        // UI Controls
        private Panel _registerListPanel;
        private Panel _editorPanel;
        private ListView _registerListView;
        private TableLayoutPanel _threeColumnLayout;

        // Column 1: Register Info
        private Panel _registerInfoColumn;
        private Label _registerAddressLabel;
        private Label _registerNameLabel;
        private Label _registerTypeLabel;
        private Label _registerCategoryLabel;
        private Label _registerDescriptionLabel;

        // Column 2: Field Editors
        private Panel _fieldEditorColumn;
        private FlowLayoutPanel _fieldsPanel;
        private Panel _fieldScrollPanel;

        // Column 3: Value Editors
        private Panel _valueEditorColumn;
        private TextBox _hexTextBox;
        private TextBox _decimalTextBox;
        private TextBox _binaryTextBox;
        private Panel _bitEditorPanel;

        // Status and buttons
        private Panel _statusPanel;
        private Label _editStatusLabel;
        private Button _applyButton;
        private Button _resetButton;
        private Button _saveAllButton;
        private Button _revertAllButton;

        // Control flags
        private bool _updatingValueEditors = false;
        private bool _suppressTextEvents = false;
        private bool _isEnterKeyPressed = false;

        // Fonts
        private readonly Font _headerFont = new Font("Segoe UI", 10, FontStyle.Bold);
        private readonly Font _labelFont = new Font("Segoe UI", 9);
        private readonly Font _valueFont = new Font("Segoe UI", 9, FontStyle.Regular);
        private readonly Font _editorFont = new Font("Segoe UI", 10);

        public event EventHandler EditsSaved;

        public RegisterEditorTab()
        {
            InitializeComponent();
        }

        public void LoadDump(PmicDump dump)
        {
            _currentDump = dump;
            _pendingEdits.Clear();
            LoadRegisterList();
            ClearEditor();
            UpdateEditStatus();
        }

        public bool HasPendingEdits => _pendingEdits.Count > 0;

        public void SaveEdits()
        {
            if (_currentDump == null || _pendingEdits.Count == 0)
                return;

            foreach (var edit in _pendingEdits)
            {
                RegisterEditService.ApplyEdit(_currentDump, edit.Key, edit.Value);
            }

            _pendingEdits.Clear();
            LoadRegisterList();
            ClearEditor();
            UpdateEditStatus();

            // Notify MainForm that edits were saved
            EditsSaved?.Invoke(this, EventArgs.Empty);
        }

        public void RevertAllEdits()
        {
            _pendingEdits.Clear();
            LoadRegisterList();
            if (_selectedRegister != null)
            {
                LoadRegisterEditor(_selectedRegister);
            }
            UpdateEditStatus();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Padding = new Padding(5);

            // Main container with two main panels stacked vertically
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // Row 1: Register List (Compact, Fixed Height - 200px)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));

            // Row 2: Editor Section (Fills remaining space)
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Create and add the two main panels
            _registerListPanel = CreateRegisterListPanel();
            _editorPanel = CreateEditorPanel();

            mainContainer.Controls.Add(_registerListPanel, 0, 0);
            mainContainer.Controls.Add(_editorPanel, 0, 1);

            this.Controls.Add(mainContainer);
        }

        private Panel CreateRegisterListPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0, 0, 0, 5) // Bottom padding for separation
            };

            var groupBox = new GroupBox
            {
                Text = "Registers",
                Dock = DockStyle.Fill,
                Font = _headerFont,
                ForeColor = AppColors.HeaderText,
                Padding = new Padding(8),
                Margin = new Padding(0),
                BackColor = Color.White
            };

            _registerListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Clickable,
                Font = new Font("Segoe UI", 9),
                BackColor = Color.White,
                MultiSelect = false
            };

            // Optimized column widths
            _registerListView.Columns.Add("Address", 70, HorizontalAlignment.Center);
            _registerListView.Columns.Add("Name", 120);
            _registerListView.Columns.Add("Value", 60, HorizontalAlignment.Center);
            _registerListView.Columns.Add("Default", 60, HorizontalAlignment.Center);
            _registerListView.Columns.Add("Type", 50, HorizontalAlignment.Center);
            _registerListView.Columns.Add("Status", 80, HorizontalAlignment.Center);

            _registerListView.SelectedIndexChanged += OnRegisterSelected;
            _registerListView.DoubleClick += OnRegisterDoubleClick;

            // Fix: Handle '0' key to prevent scrolling
            _registerListView.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)
                {
                    e.Handled = true; // Prevent default scrolling behavior
                    if (_hexTextBox != null && _hexTextBox.Visible)
                    {
                        _hexTextBox.Focus();
                        _hexTextBox.SelectionStart = _hexTextBox.TextLength;
                    }
                }
            };

            groupBox.Controls.Add(_registerListView);
            panel.Controls.Add(groupBox);

            return panel;
        }

        private Panel CreateEditorPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var editorGroup = new GroupBox
            {
                Text = "Register Editor",
                Dock = DockStyle.Fill,
                Font = _headerFont,
                ForeColor = AppColors.HeaderText,
                Padding = new Padding(10),
                BackColor = Color.White
            };

            // Three-column layout for editor section
            _threeColumnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.White
            };

            // Column proportions: 20% | 50% | 30%
            _threeColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            _threeColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            _threeColumnLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

            // Create the three columns
            _registerInfoColumn = CreateRegisterInfoColumn();
            _fieldEditorColumn = CreateFieldEditorColumn();
            _valueEditorColumn = CreateValueEditorColumn();

            _threeColumnLayout.Controls.Add(_registerInfoColumn, 0, 0);
            _threeColumnLayout.Controls.Add(_fieldEditorColumn, 1, 0);
            _threeColumnLayout.Controls.Add(_valueEditorColumn, 2, 0);

            // Create status panel below the three columns
            _statusPanel = CreateStatusPanel();

            // Container for editor and status
            var editorContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            editorContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 90)); // Editor columns
            editorContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Status bar

            editorContainer.Controls.Add(_threeColumnLayout, 0, 0);
            editorContainer.Controls.Add(_statusPanel, 0, 1);

            editorGroup.Controls.Add(editorContainer);
            panel.Controls.Add(editorGroup);

            return panel;
        }

        private Panel CreateRegisterInfoColumn()
        {
            var column = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.EditorInfoPanel,
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var infoGroup = new GroupBox
            {
                Text = "Register Information",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.HeaderText,
                Padding = new Padding(10),
                BackColor = AppColors.EditorInfoPanel
            };

            var infoTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(5),
                BackColor = AppColors.EditorInfoPanel
            };

            infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            infoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Address
            var addressLabel = new Label
            {
                Text = "Address:",
                Font = _labelFont,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Padding = new Padding(0, 5, 10, 5)
            };

            _registerAddressLabel = new Label
            {
                Text = "",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(0, 0, 139), // Dark blue for address
                Padding = new Padding(5, 5, 0, 5)
            };

            // Name
            var nameLabel = new Label
            {
                Text = "Name:",
                Font = _labelFont,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Padding = new Padding(0, 5, 10, 5)
            };

            _registerNameLabel = new Label
            {
                Text = "",
                Font = _valueFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.ValueText,
                Padding = new Padding(5, 5, 0, 5)
            };

            // Type
            var typeLabel = new Label
            {
                Text = "Type:",
                Font = _labelFont,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Padding = new Padding(0, 5, 10, 5)
            };

            _registerTypeLabel = new Label
            {
                Text = "",
                Font = _valueFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.ValueText,
                Padding = new Padding(5, 5, 0, 5)
            };

            // Category
            var categoryLabel = new Label
            {
                Text = "Category:",
                Font = _labelFont,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Padding = new Padding(0, 5, 10, 5)
            };

            _registerCategoryLabel = new Label
            {
                Text = "",
                Font = _valueFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.ValueText,
                Padding = new Padding(5, 5, 0, 5)
            };

            // Description
            var descLabel = new Label
            {
                Text = "Description:",
                Font = _labelFont,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Padding = new Padding(0, 5, 10, 5)
            };

            _registerDescriptionLabel = new Label
            {
                Text = "",
                Font = _valueFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 5, 0, 5),
                AutoEllipsis = true,
                Height = 40
            };

            // Add controls to table
            infoTable.Controls.Add(addressLabel, 0, 0);
            infoTable.Controls.Add(_registerAddressLabel, 1, 0);
            infoTable.Controls.Add(nameLabel, 0, 1);
            infoTable.Controls.Add(_registerNameLabel, 1, 1);
            infoTable.Controls.Add(typeLabel, 0, 2);
            infoTable.Controls.Add(_registerTypeLabel, 1, 2);
            infoTable.Controls.Add(categoryLabel, 0, 3);
            infoTable.Controls.Add(_registerCategoryLabel, 1, 3);
            infoTable.Controls.Add(descLabel, 0, 4);
            infoTable.Controls.Add(_registerDescriptionLabel, 1, 4);

            infoGroup.Controls.Add(infoTable);
            column.Controls.Add(infoGroup);

            return column;
        }

        private Panel CreateFieldEditorColumn()
        {
            var column = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.EditorFieldPanel,
                Padding = new Padding(10, 10, 10, 10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var fieldGroup = new GroupBox
            {
                Text = "Bit Field Editing",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.HeaderText,
                Padding = new Padding(10),
                BackColor = AppColors.EditorFieldPanel
            };

            _fieldScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = AppColors.EditorFieldPanel
            };

            _fieldsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = AppColors.EditorFieldPanel,
                Padding = new Padding(5)
            };

            _fieldScrollPanel.Controls.Add(_fieldsPanel);
            fieldGroup.Controls.Add(_fieldScrollPanel);
            column.Controls.Add(fieldGroup);

            return column;
        }

        private Panel CreateValueEditorColumn()
        {
            var column = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppColors.EditorValuePanel,
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var valueGroup = new GroupBox
            {
                Text = "Direct Value Editing",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.HeaderText,
                Padding = new Padding(10),
                BackColor = AppColors.EditorValuePanel
            };

            var valueLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(5),
                BackColor = AppColors.EditorValuePanel
            };

            valueLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            valueLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            // Hex editor
            valueLayout.Controls.Add(new Label
            {
                Text = "Hex:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = _editorFont,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Margin = new Padding(0, 5, 10, 5)
            }, 0, row);

            _hexTextBox = new TextBox
            {
                Font = _editorFont,
                MaxLength = 2,
                CharacterCasing = CharacterCasing.Upper,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                BackColor = Color.White
            };
            _hexTextBox.Leave += OnHexTextBoxLeave;
            _hexTextBox.KeyPress += OnHexTextBoxKeyPress;
            valueLayout.Controls.Add(_hexTextBox, 1, row++);

            // Decimal editor
            valueLayout.Controls.Add(new Label
            {
                Text = "Decimal:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = _editorFont,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Margin = new Padding(0, 5, 10, 5)
            }, 0, row);

            _decimalTextBox = new TextBox
            {
                Font = _editorFont,
                MaxLength = 3,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                BackColor = Color.White
            };
            _decimalTextBox.Leave += OnDecimalTextBoxLeave;
            _decimalTextBox.KeyPress += OnDecimalTextBoxKeyPress;
            valueLayout.Controls.Add(_decimalTextBox, 1, row++);

            // Binary editor
            valueLayout.Controls.Add(new Label
            {
                Text = "Binary:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = _editorFont,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Margin = new Padding(0, 5, 10, 5)
            }, 0, row);

            _binaryTextBox = new TextBox
            {
                Font = _editorFont,
                MaxLength = 8,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5),
                BackColor = Color.White
            };
            _binaryTextBox.Leave += OnBinaryTextBoxLeave;
            _binaryTextBox.KeyPress += OnBinaryTextBoxKeyPress;
            valueLayout.Controls.Add(_binaryTextBox, 1, row++);

            // Bit editor label
            valueLayout.Controls.Add(new Label
            {
                Text = "Bit Editor:",
                TextAlign = ContentAlignment.MiddleRight,
                Font = _editorFont,
                Dock = DockStyle.Fill,
                ForeColor = AppColors.LabelText,
                Margin = new Padding(0, 15, 10, 5)
            }, 0, row);

            // Bit editor panel
            _bitEditorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(0, 10, 0, 5),
                Height = 80,
                BorderStyle = BorderStyle.FixedSingle
            };
            valueLayout.Controls.Add(_bitEditorPanel, 1, row++);

            // Apply button row
            var buttonCell = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40,
                Padding = new Padding(0, 10, 0, 0)
            };

            valueLayout.Controls.Add(new Panel(), 0, row); // Spacer
            valueLayout.Controls.Add(buttonCell, 1, row);

            // Apply button
            _applyButton = new Button
            {
                Text = "Apply Edit",
                Size = new Size(100, 32),
                BackColor = AppColors.Success,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _labelFont,
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            _applyButton.FlatAppearance.BorderSize = 0;
            _applyButton.Click += OnApplyClicked;
            buttonCell.Controls.Add(_applyButton);
            _applyButton.Location = new Point(buttonCell.Width - _applyButton.Width, 0);

            valueGroup.Controls.Add(valueLayout);
            column.Controls.Add(valueGroup);

            return column;
        }

        private Panel CreateStatusPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 5, 10, 5),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Status label
            _editStatusLabel = new Label
            {
                Text = "No pending edits",
                Font = _labelFont,
                ForeColor = AppColors.LabelText,
                Location = new Point(10, 15),
                AutoSize = true
            };

            // Button panel
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                Width = 450,
                Height = 40
            };

            _saveAllButton = new Button
            {
                Text = "Save All Edits",
                Size = new Size(100, 32),
                BackColor = AppColors.Info,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _labelFont,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _saveAllButton.FlatAppearance.BorderSize = 0;
            _saveAllButton.Click += OnSaveAllClicked;

            _revertAllButton = new Button
            {
                Text = "Revert All",
                Size = new Size(90, 32),
                BackColor = AppColors.Warning,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = _labelFont,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _revertAllButton.FlatAppearance.BorderSize = 0;
            _revertAllButton.Click += OnRevertAllClicked;

            _resetButton = new Button
            {
                Text = "Reset to Default",
                Size = new Size(120, 32),
                BackColor = AppColors.Error,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _labelFont,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _resetButton.FlatAppearance.BorderSize = 0;
            _resetButton.Click += OnResetClicked;

            buttonPanel.Controls.AddRange(new Control[] {
                _saveAllButton, _revertAllButton, _resetButton
            });

            panel.Controls.Add(_editStatusLabel);
            panel.Controls.Add(buttonPanel);

            return panel;
        }

        private void LoadRegisterList()
        {
            if (_currentDump == null)
                return;

            _registerListView.BeginUpdate();
            _registerListView.Items.Clear();

            foreach (var register in _currentDump.Registers.Values.OrderBy(r => r.Address))
            {
                bool isEditable = RegisterEditService.CanEditRegister(register.Definition);
                bool hasPendingEdit = _pendingEdits.ContainsKey(register.Address);
                byte displayValue = hasPendingEdit ? _pendingEdits[register.Address] : register.RawValue;

                var item = new ListViewItem(register.AddrHex);
                item.SubItems.Add(register.Name);
                item.SubItems.Add($"0x{displayValue:X2}");
                item.SubItems.Add(register.DefaultHex);
                item.SubItems.Add(register.Definition.Type);

                // Status column
                string status = "";
                if (hasPendingEdit)
                    status = "PENDING";
                else if (register.IsChanged)
                    status = "CHANGED";
                else if (!isEditable)
                    status = "READ-ONLY";
                else
                    status = "DEFAULT";

                item.SubItems.Add(status);
                item.Tag = register;

                // FIXED: Use the same color coding as detailed tab
                // Check if register is reserved first
                bool isReserved = RegisterAnalyzer.IsReserved(register);

                if (hasPendingEdit)
                {
                    // Pending edits get special color
                    item.BackColor = AppColors.PendingEdit;
                    item.ForeColor = AppColors.LabelText;
                }
                else if (isReserved)
                {
                    // Reserved registers
                    item.BackColor = AppColors.DefaultGrid;
                    item.ForeColor = AppColors.DisabledText;
                }
                else
                {
                    // Use centralized color coding for status (same as detailed tab)
                    bool isProtected = RegisterAnalyzer.IsProtected(register);
                    bool isCritical = RegisterAnalyzer.IsCriticalChange(register);

                    item.BackColor = AppColors.GetRegisterColor(
                        register.IsChanged,
                        isProtected,
                        isCritical,
                        isReserved
                    );

                    // Set text color based on status
                    if (register.IsChanged)
                        item.ForeColor = AppColors.ValueText;
                    else if (!isEditable)
                        item.ForeColor = AppColors.DisabledText;
                    else
                        item.ForeColor = AppColors.LabelText;
                }

                _registerListView.Items.Add(item);
            }

            // Auto-size columns to content
            foreach (ColumnHeader column in _registerListView.Columns)
            {
                column.Width = -2; // Auto-size to content
            }

            _registerListView.EndUpdate();
        }

        private void LoadRegisterEditor(ParsedRegister register)
        {
            _selectedRegister = register;

            // Clear previous editors
            _fieldsPanel.Controls.Clear();
            ClearValueEditors();

            if (register == null)
            {
                ClearRegisterInfo();
                _applyButton.Enabled = false;
                _resetButton.Enabled = false;
                return;
            }

            // Update register info
            UpdateRegisterInfo(register);

            // Get current value (pending or actual)
            byte currentValue = _pendingEdits.TryGetValue(register.Address, out byte pendingValue)
                ? pendingValue
                : register.RawValue;

            // Check if editable
            bool isEditable = RegisterEditService.CanEditRegister(register.Definition);
            _applyButton.Enabled = isEditable;
            _resetButton.Enabled = isEditable && (_pendingEdits.ContainsKey(register.Address) || register.IsChanged);

            // Create field editors if fields are defined
            if (register.Definition.Fields != null && register.Definition.Fields.Any(f => f.Type != FieldType.Reserved))
            {
                foreach (var field in register.Definition.Fields)
                {
                    if (field.Type == FieldType.Reserved)
                        continue;

                    var fieldEditor = new FieldEditorControl(field, currentValue, register.DefaultValue,
                        register.Name, (newValue) => OnFieldValueChanged(register.Address, newValue));
                    _fieldsPanel.Controls.Add(fieldEditor);
                }
            }
            else
            {
                // No field definitions - show a message
                var noFieldsLabel = new Label
                {
                    Text = "No detailed field definitions available for this register.",
                    Font = new Font("Segoe UI", 10, FontStyle.Italic),
                    ForeColor = AppColors.DisabledText,
                    AutoSize = true,
                    Margin = new Padding(10, 20, 10, 10)
                };
                _fieldsPanel.Controls.Add(noFieldsLabel);
            }

            // Update direct value editors
            UpdateValueEditors(currentValue);

            // Create bit editor
            CreateBitEditor(currentValue);
        }

        private void UpdateRegisterInfo(ParsedRegister register)
        {
            _registerAddressLabel.Text = register.AddrHex;
            _registerNameLabel.Text = register.FullName;
            _registerTypeLabel.Text = $"{register.Definition.Type} {(register.Definition.Protected == true ? "(Protected)" : "")}";
            _registerCategoryLabel.Text = register.Category;
            _registerDescriptionLabel.Text = register.Description;
        }

        private void ClearRegisterInfo()
        {
            _registerAddressLabel.Text = "";
            _registerNameLabel.Text = "";
            _registerTypeLabel.Text = "";
            _registerCategoryLabel.Text = "";
            _registerDescriptionLabel.Text = "";
        }

        private void UpdateValueEditors(byte value)
        {
            if (_updatingValueEditors) return;

            _updatingValueEditors = true;
            try
            {
                _suppressTextEvents = true;

                _hexTextBox.Text = value.ToString("X2");
                _decimalTextBox.Text = value.ToString();
                _binaryTextBox.Text = Convert.ToString(value, 2).PadLeft(8, '0');

                // Also update bit checkboxes
                UpdateBitCheckboxes(value);
            }
            finally
            {
                _suppressTextEvents = false;
                _updatingValueEditors = false;
            }
        }

        private void ClearValueEditors()
        {
            _updatingValueEditors = true;
            try
            {
                _suppressTextEvents = true;

                _hexTextBox.Text = "";
                _decimalTextBox.Text = "";
                _binaryTextBox.Text = "";

                _bitEditorPanel.Controls.Clear();
            }
            finally
            {
                _suppressTextEvents = false;
                _updatingValueEditors = false;
            }
        }

        private void CreateBitEditor(byte value)
        {
            _bitEditorPanel.Controls.Clear();

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Height = 80
            };

            // Create 8 checkboxes for each bit (7-0)
            for (int i = 7; i >= 0; i--)
            {
                var bitPanel = new Panel
                {
                    Size = new Size(45, 70),
                    Location = new Point((7 - i) * 45, 0),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(1)
                };

                // Highlight changed bits
                bool isChanged = ((value >> i) & 1) != ((_selectedRegister.DefaultValue >> i) & 1);
                if (isChanged)
                {
                    bitPanel.BackColor = AppColors.Changed;
                }

                var bitLabel = new Label
                {
                    Text = $"Bit {i}",
                    Location = new Point(5, 5),
                    Size = new Size(35, 15),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = AppColors.LabelText
                };

                var checkbox = new CheckBox
                {
                    Checked = ((value >> i) & 1) == 1,
                    Location = new Point(12, 25),
                    Size = new Size(20, 20),
                    Tag = i,
                    Cursor = Cursors.Hand
                };

                checkbox.CheckedChanged += (s, e) =>
                {
                    if (_selectedRegister != null && !_suppressTextEvents && !_isEnterKeyPressed)
                    {
                        byte newValue = 0;
                        // Reconstruct byte from all checkboxes
                        foreach (Control control in panel.Controls)
                        {
                            if (control is Panel p && p.Controls[1] is CheckBox cb && cb.Tag is int bit)
                            {
                                if (cb.Checked)
                                    newValue |= (byte)(1 << bit);
                            }
                        }

                        OnFieldValueChanged(_selectedRegister.Address, newValue);
                    }
                };

                bitPanel.Controls.Add(bitLabel);
                bitPanel.Controls.Add(checkbox);
                panel.Controls.Add(bitPanel);
            }

            _bitEditorPanel.Controls.Add(panel);
        }

        private void UpdateBitCheckboxes(byte value)
        {
            if (_bitEditorPanel.Controls.Count > 0 && _bitEditorPanel.Controls[0] is Panel mainPanel)
            {
                foreach (Control control in mainPanel.Controls)
                {
                    if (control is Panel bitPanel && bitPanel.Controls[1] is CheckBox checkbox && checkbox.Tag is int bit)
                    {
                        checkbox.Checked = ((value >> bit) & 1) == 1;

                        // Update background color for changed bits
                        bool isChanged = ((value >> bit) & 1) != ((_selectedRegister.DefaultValue >> bit) & 1);
                        bitPanel.BackColor = isChanged ? AppColors.Changed : Color.White;
                    }
                }
            }
        }

        private void OnFieldValueChanged(byte address, byte newValue)
        {
            if (_currentDump == null || !_currentDump.Registers.ContainsKey(address))
                return;

            var register = _currentDump.Registers[address];
            var originalValue = register.RawValue;

            // Get current value (pending or actual)
            byte currentDisplayValue = _pendingEdits.TryGetValue(address, out byte pendingValue)
                ? pendingValue
                : originalValue;

            // Don't process if value hasn't changed
            if (newValue == currentDisplayValue)
                return;

            // Validate the edit
            var validation = RegisterEditService.ValidateValue(register.Definition, newValue, originalValue);

            if (validation.isValid)
            {
                if (newValue != originalValue)
                {
                    _pendingEdits[address] = newValue;
                }
                else
                {
                    _pendingEdits.Remove(address);
                }

                // Update UI
                UpdateValueEditors(newValue);
                LoadRegisterList();
                UpdateEditStatus();

                // If this is the currently selected register, reload its editor
                if (_selectedRegister != null && _selectedRegister.Address == address)
                {
                    LoadRegisterEditor(_selectedRegister);
                }
            }
            else if (!string.IsNullOrEmpty(validation.errorMessage))
            {
                MessageBox.Show(validation.errorMessage, "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnHexTextBoxLeave(object sender, EventArgs e)
        {
            if (_suppressTextEvents || _selectedRegister == null || string.IsNullOrEmpty(_hexTextBox.Text))
                return;

            try
            {
                byte value = Convert.ToByte(_hexTextBox.Text, 16);
                OnFieldValueChanged(_selectedRegister.Address, value);
            }
            catch
            {
                // Invalid hex - reset to current value
                byte currentValue = _pendingEdits.TryGetValue(_selectedRegister.Address, out byte pendingValue)
                    ? pendingValue
                    : _selectedRegister.RawValue;
                UpdateValueEditors(currentValue);
            }
        }

        private void OnHexTextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                _isEnterKeyPressed = true;
                OnHexTextBoxLeave(sender, e);
                _isEnterKeyPressed = false;
            }
        }

        private void OnDecimalTextBoxLeave(object sender, EventArgs e)
        {
            if (_suppressTextEvents || _selectedRegister == null || string.IsNullOrEmpty(_decimalTextBox.Text))
                return;

            try
            {
                byte value = byte.Parse(_decimalTextBox.Text);
                OnFieldValueChanged(_selectedRegister.Address, value);
            }
            catch
            {
                // Invalid decimal - reset to current value
                byte currentValue = _pendingEdits.TryGetValue(_selectedRegister.Address, out byte pendingValue)
                    ? pendingValue
                    : _selectedRegister.RawValue;
                UpdateValueEditors(currentValue);
            }
        }

        private void OnDecimalTextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                _isEnterKeyPressed = true;
                OnDecimalTextBoxLeave(sender, e);
                _isEnterKeyPressed = false;
            }
        }

        private void OnBinaryTextBoxLeave(object sender, EventArgs e)
        {
            if (_suppressTextEvents || _selectedRegister == null || string.IsNullOrEmpty(_binaryTextBox.Text))
                return;

            try
            {
                // Remove any non-binary characters
                string binary = _binaryTextBox.Text.Replace(" ", "").Trim();
                if (binary.Length > 8)
                    binary = binary.Substring(0, 8);

                byte value = Convert.ToByte(binary, 2);
                OnFieldValueChanged(_selectedRegister.Address, value);
            }
            catch
            {
                // Invalid binary - reset to current value
                byte currentValue = _pendingEdits.TryGetValue(_selectedRegister.Address, out byte pendingValue)
                    ? pendingValue
                    : _selectedRegister.RawValue;
                UpdateValueEditors(currentValue);
            }
        }

        private void OnBinaryTextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                _isEnterKeyPressed = true;
                OnBinaryTextBoxLeave(sender, e);
                _isEnterKeyPressed = false;
            }
        }

        private void OnRegisterSelected(object sender, EventArgs e)
        {
            if (_registerListView.SelectedItems.Count > 0 &&
                _registerListView.SelectedItems[0].Tag is ParsedRegister register)
            {
                LoadRegisterEditor(register);
            }
        }

        private void OnRegisterDoubleClick(object sender, EventArgs e)
        {
            // Same as selection
            OnRegisterSelected(sender, e);
        }

        private void OnApplyClicked(object sender, EventArgs e)
        {
            SaveEdits();
        }

        private void OnResetClicked(object sender, EventArgs e)
        {
            if (_selectedRegister != null)
            {
                if (MessageBox.Show($"Reset register {_selectedRegister.AddrHex} to default value {_selectedRegister.DefaultHex}?",
                    "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RegisterEditService.ResetToDefault(_currentDump, _selectedRegister.Address);
                    _pendingEdits.Remove(_selectedRegister.Address);
                    LoadRegisterList();
                    LoadRegisterEditor(_selectedRegister);
                    UpdateEditStatus();
                }
            }
        }

        private void OnSaveAllClicked(object sender, EventArgs e)
        {
            SaveEdits();
        }

        private void OnRevertAllClicked(object sender, EventArgs e)
        {
            if (_pendingEdits.Count > 0)
            {
                if (MessageBox.Show($"Revert all {_pendingEdits.Count} pending edits?",
                    "Confirm Revert", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RevertAllEdits();
                }
            }
        }

        private void UpdateEditStatus()
        {
            int pendingCount = _pendingEdits.Count;
            _editStatusLabel.Text = pendingCount == 0
                ? "No pending edits"
                : $"{pendingCount} register(s) have pending edits";

            _editStatusLabel.ForeColor = pendingCount == 0
                ? AppColors.LabelText
                : AppColors.Error;

            _saveAllButton.Enabled = pendingCount > 0;
            _revertAllButton.Enabled = pendingCount > 0;
        }

        private void ClearEditor()
        {
            _selectedRegister = null;
            _fieldsPanel.Controls.Clear();
            ClearRegisterInfo();
            ClearValueEditors();
            _applyButton.Enabled = false;
            _resetButton.Enabled = false;
        }
    }
}