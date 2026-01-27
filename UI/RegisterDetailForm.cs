using PMICDumpParser.Models;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PMICDumpParser
{
    public partial class RegisterDetailForm : Form
    {
        private readonly ParsedRegister _register;
        private readonly Func<ParsedRegister, bool> _isCriticalChange;
        private readonly ToolTip _toolTip;

        public RegisterDetailForm(ParsedRegister register, Func<ParsedRegister, bool> isCriticalChange)
        {
            _register = register;
            _isCriticalChange = isCriticalChange;
            _toolTip = new ToolTip();
            InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.Text = $"{_register.Name} - 0x{_register.Address:X2}";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 600);
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.White;
            this.Padding = new Padding(1);

            // Main layout container
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8, 8, 8, 8),
                Margin = new Padding(0, 0, 0, 0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Tab control for different views
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(6, 6),
                Font = new Font("Segoe UI", 9),
                Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(100, 28),
                SizeMode = TabSizeMode.Fixed
            };

            // Create tabs with better styling
            tabControl.TabPages.Add(CreateOverviewTab());
            tabControl.TabPages.Add(CreateBitFieldTab());
            tabControl.TabPages.Add(CreateFieldsTab());
            tabControl.TabPages.Add(CreateRawDataTab());
            tabControl.TabPages.Add(CreateComparisonTab());

            // Style tab headers
            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.BackColor = Color.White;
                tab.Padding = new Padding(6, 6, 6, 6);
            }

            mainLayout.Controls.Add(tabControl, 0, 0);

            // Close button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.White
            };

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Location = new Point(buttonPanel.Width - closeButton.Width - 10, 0);

            buttonPanel.Controls.Add(closeButton);
            mainLayout.Controls.Add(buttonPanel, 0, 1);

            this.Controls.Add(mainLayout);
        }

        private TabPage CreateOverviewTab()
        {
            var tab = new TabPage("Overview");
            tab.Padding = new Padding(10, 10, 10, 10);
            tab.Font = new Font("Segoe UI", 9.5f);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };

            var contentPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = scrollPanel.ClientSize.Width - 25,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 11,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(8, 8, 8, 8),
                Location = new Point(0, 0),
                BackColor = Color.White
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            AddOverviewRow(table, row++, "Address:", _register.AddrHex);
            AddOverviewRow(table, row++, "Name:", _register.Name);
            AddOverviewRow(table, row++, "Full Name:", _register.FullName);
            AddOverviewRow(table, row++, "Category:", _register.Category);
            AddOverviewRow(table, row++, "Type:", $"{_register.Definition.Type} {(_register.Definition.Protected == true ? "(Protected)" : "")}");
            AddOverviewRow(table, row++, "Description:", _register.Description);
            AddOverviewRow(table, row++, "Default Value:", $"{_register.DefaultHex} (Dec: {_register.DefaultValue}, Bin: {Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0')})");
            AddOverviewRow(table, row++, "Current Value:", $"{_register.ValHex} (Dec: {_register.RawValue}, Bin: {Convert.ToString(_register.RawValue, 2).PadLeft(8, '0')})");

            var statusColor = _register.IsChanged ? Color.FromArgb(0, 102, 204) : Color.FromArgb(0, 128, 0);
            AddOverviewRow(table, row++, "Status:", _register.IsChanged ? "CHANGED FROM DEFAULT" : "AT DEFAULT", statusColor);

            AddOverviewRow(table, row++, "Protected:", _register.Definition.Protected == true ? "Yes" : "No");
            AddOverviewRow(table, row++, "Decoded Value:", _register.DecodedValue);

            contentPanel.Controls.Add(table);

            // Add binary visualization below the table
            var binaryLabel = new Label
            {
                Text = "Bit Visualization:",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(0, table.Height + 25),
                AutoSize = true,
                ForeColor = Color.FromArgb(70, 70, 70)
            };
            contentPanel.Controls.Add(binaryLabel);

            var binaryPanel = CreateBinaryVisualization();
            binaryPanel.Location = new Point(0, table.Height + 55);
            contentPanel.Controls.Add(binaryPanel);

            contentPanel.Height = table.Height + binaryPanel.Height + 90;
            scrollPanel.Controls.Add(contentPanel);
            tab.Controls.Add(scrollPanel);

            return tab;
        }

        private TabPage CreateBitFieldTab()
        {
            var tab = new TabPage("Bit Field Analysis");
            tab.Padding = new Padding(8, 8, 8, 8);
            tab.BackColor = Color.White;

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 280,
                SplitterWidth = 2,
                BackColor = Color.White
            };

            // Visualization panel
            var visPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            var visGroup = new GroupBox
            {
                Text = "Bit Visualization",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            var visualization = CreateDetailedBitVisualization();
            visualization.Dock = DockStyle.Fill;
            visGroup.Controls.Add(visualization);
            visPanel.Controls.Add(visGroup);

            // List view panel
            var listPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            var listGroup = new GroupBox
            {
                Text = "Field Analysis",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

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

            listView.Columns.Add("Bit(s)", 70);
            listView.Columns.Add("Field Name", 110);
            listView.Columns.Add("Value", 70);
            listView.Columns.Add("Description", 180);
            listView.Columns.Add("Type", 70);
            listView.Columns.Add("Meaning", 140);

            if (_register.Definition.Fields != null && _register.Definition.Fields.Any())
            {
                foreach (var field in _register.Definition.Fields)
                {
                    var (startBit, endBit) = ParseBitRange(field.Bits);
                    int fieldValue = ExtractFieldValue(_register.RawValue, startBit, endBit);
                    int defaultValue = ExtractFieldValue(_register.DefaultValue, startBit, endBit);

                    string meaning = RegisterDecode.DecodeField(field, fieldValue, _register.Name);

                    var item = new ListViewItem(field.Bits);
                    item.SubItems.Add(field.Name);
                    item.SubItems.Add($"{fieldValue} (0x{fieldValue:X})");
                    item.SubItems.Add(field.Description);
                    item.SubItems.Add(field.Type.ToString());
                    item.SubItems.Add(meaning);

                    if (fieldValue != defaultValue)
                        item.BackColor = Color.FromArgb(255, 240, 240);

                    listView.Items.Add(item);
                }
            }
            else
            {
                for (int bit = 7; bit >= 0; bit--)
                {
                    bool currentValue = ((_register.RawValue >> bit) & 1) == 1;
                    bool defaultValue = ((_register.DefaultValue >> bit) & 1) == 1;

                    var item = new ListViewItem($"Bit {bit}");
                    item.SubItems.Add("Reserved");
                    item.SubItems.Add(currentValue ? "1" : "0");
                    item.SubItems.Add("Reserved bit");
                    item.SubItems.Add("Reserved");
                    item.SubItems.Add(currentValue ? "Set" : "Clear");

                    if (currentValue != defaultValue)
                        item.BackColor = Color.FromArgb(255, 240, 240);

                    listView.Items.Add(item);
                }
            }

            // Auto-size columns
            foreach (ColumnHeader column in listView.Columns)
            {
                column.Width = -2;
            }

            listGroup.Controls.Add(listView);
            listPanel.Controls.Add(listGroup);

            splitContainer.Panel1.Controls.Add(visPanel);
            splitContainer.Panel2.Controls.Add(listPanel);
            tab.Controls.Add(splitContainer);

            return tab;
        }

        private TabPage CreateFieldsTab()
        {
            var tab = new TabPage("Field Details");
            tab.Padding = new Padding(8, 8, 8, 8);
            tab.BackColor = Color.White;

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };

            var contentPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = scrollPanel.ClientSize.Width + 250,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            if (_register.Definition.Fields == null || !_register.Definition.Fields.Any())
            {
                var noDataLabel = new Label
                {
                    Text = "No field definitions available for this register.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10, FontStyle.Italic),
                    ForeColor = Color.Gray
                };
                scrollPanel.Controls.Add(noDataLabel);
                tab.Controls.Add(scrollPanel);
                return tab;
            }

            var yPos = 10;
            foreach (var field in _register.Definition.Fields)
            {
                var (startBit, endBit) = ParseBitRange(field.Bits);
                int fieldValue = ExtractFieldValue(_register.RawValue, startBit, endBit);
                int defaultValue = ExtractFieldValue(_register.DefaultValue, startBit, endBit);

                var groupBox = new GroupBox
                {
                    Text = $"{field.Name} (Bits {field.Bits})",
                    Location = new Point(10, yPos),
                    Size = new Size(contentPanel.Width, 180),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Padding = new Padding(10, 10, 10, 10),
                    ForeColor = Color.FromArgb(70, 70, 70),
                    BackColor = Color.White
                };

                var table = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Padding = new Padding(5, 5, 5, 5),
                    BackColor = Color.White
                };

                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                AddFieldRow(table, 0, "Description:", field.Description);
                AddFieldRow(table, 1, "Type:", field.Type.ToString());
                AddFieldRow(table, 2, "Current Value:", GetFieldValueDisplay(field, fieldValue));
                AddFieldRow(table, 3, "Default Value:", GetFieldValueDisplay(field, defaultValue));

                bool isCritical = _isCriticalChange(_register);

                if (fieldValue != defaultValue)
                {
                    groupBox.BackColor = Color.FromArgb(240, 230, 255);
                    groupBox.Text += " [CHANGED]";
                    groupBox.ForeColor = Color.FromArgb(129, 75, 209);

                    if (isCritical)
                    {
                        groupBox.Text += " [WARNING]";
                        groupBox.BackColor = Color.FromArgb(252, 219, 215);
                        groupBox.ForeColor = Color.FromArgb(184, 49, 33);
                        groupBox.Height = 190;
                        table.RowCount = 5;
                        AddFieldRow(table, 4, "Warning:", "Current Value exceeds safe threshold (>23% change from default)");
                        yPos += 15;
                    }
                }

                groupBox.Controls.Add(table);
                contentPanel.Controls.Add(groupBox);
                yPos += groupBox.Height + 10;
            }

            contentPanel.Height = yPos + 10;
            scrollPanel.Controls.Add(contentPanel);
            tab.Controls.Add(scrollPanel);

            return tab;
        }

        private TabPage CreateRawDataTab()
        {
            var tab = new TabPage("Raw Data");
            tab.Padding = new Padding(10, 10, 10, 10);
            tab.BackColor = Color.White;

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 10),
                Text = GetRawDataText(),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle
            };

            tab.Controls.Add(textBox);
            return tab;
        }

        private TabPage CreateComparisonTab()
        {
            var tab = new TabPage("Comparison");
            tab.Padding = new Padding(8, 8, 8, 8);
            tab.BackColor = Color.White;

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = Color.White
            };

            // Comparison table
            var comparisonTable = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 6,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(6, 6, 6, 6),
                Location = new Point(10, 10),
                BackColor = Color.White
            };

            comparisonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            comparisonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            comparisonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            comparisonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Headers
            var currentHeader = new Label
            {
                Text = "CURRENT VALUE",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            var defaultHeader = new Label
            {
                Text = "DEFAULT VALUE",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            comparisonTable.Controls.Add(currentHeader, 0, 0);
            comparisonTable.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.White }, 1, 0);
            comparisonTable.Controls.Add(defaultHeader, 2, 0);
            comparisonTable.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.White }, 3, 0);

            // Values
            int row = 1;
            AddComparisonRow(comparisonTable, row++, "Hex:", _register.ValHex, _register.DefaultHex);
            AddComparisonRow(comparisonTable, row++, "Decimal:", _register.RawValue.ToString(), _register.DefaultValue.ToString());
            AddComparisonRow(comparisonTable, row++, "Binary:",
                Convert.ToString(_register.RawValue, 2).PadLeft(8, '0'),
                Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0'));
            AddComparisonRow(comparisonTable, row++, "Status:",
                _register.IsChanged ? "CHANGED" : "DEFAULT",
                "DEFAULT");
            AddComparisonRow(comparisonTable, row++, "Decoded:", _register.DecodedValue,
                RegisterDecoder.DecodeRegister(new ParsedRegister
                {
                    RawValue = _register.DefaultValue,
                    Definition = _register.Definition
                }));

            comparisonTable.Height = row * 40 + 10;

            mainPanel.Controls.Add(comparisonTable);

            // Bit comparison visualization
            var bitComparisonLabel = new Label
            {
                Text = "Bit-by-bit Comparison:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(10, comparisonTable.Height + 15),
                AutoSize = true,
                ForeColor = Color.FromArgb(70, 70, 70)
            };
            mainPanel.Controls.Add(bitComparisonLabel);

            var bitComparisonPanel = CreateCompactBitComparisonVisualization();
            bitComparisonPanel.Location = new Point(10, comparisonTable.Height + 45);
            bitComparisonPanel.Width = mainPanel.ClientSize.Width - 35;
            mainPanel.Controls.Add(bitComparisonPanel);

            // Set main panel height
            mainPanel.Height = comparisonTable.Height + bitComparisonPanel.Height + 70;

            tab.Controls.Add(mainPanel);
            return tab;
        }

        private void AddOverviewRow(TableLayoutPanel table, int row, string label, string value, Color? color = null)
        {
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 10, 6),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            var val = new Label
            {
                Text = value,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = color ?? Color.Black,
                Dock = DockStyle.Fill,
                Padding = new Padding(5, 6, 0, 6),
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(val, 1, row);
        }

        private void AddFieldRow(TableLayoutPanel table, int row, string label, string value)
        {
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9),
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 3, 10, 3),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            var val = new Label
            {
                Text = value,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Segoe UI", 9)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(val, 1, row);
        }

        private void AddComparisonRow(TableLayoutPanel table, int row, string label, string currentValue, string defaultValue)
        {
            var lbl = new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(80, 80, 80),
                Padding = new Padding(0, 5, 0, 5)
            };

            var current = new Label
            {
                Text = currentValue,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(5, 5, 5, 5)
            };

            var def = new Label
            {
                Text = defaultValue,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(5, 5, 5, 5)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(current, 1, row);
            table.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(80, 80, 80),
                Padding = new Padding(0, 5, 0, 5)
            }, 2, row);
            table.Controls.Add(def, 3, row);

            if (currentValue != defaultValue && label != "Status:")
            {
                current.BackColor = Color.FromArgb(255, 240, 240);
                def.BackColor = Color.FromArgb(255, 240, 240);
                current.ForeColor = Color.FromArgb(200, 0, 0);
                def.ForeColor = Color.FromArgb(200, 0, 0);
            }
        }

        private Control CreateBinaryVisualization()
        {
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 90,
                Padding = new Padding(5, 5, 5, 5)
            };

            var binaryStr = Convert.ToString(_register.RawValue, 2).PadLeft(8, '0');
            var defaultStr = Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0');

            for (int i = 7; i >= 0; i--)
            {
                var bitPanel = new Panel
                {
                    Width = 55,
                    Height = 70,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(3, 3, 3, 3),
                    BackColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(220, 240, 255) :
                        Color.FromArgb(245, 245, 245),
                    Padding = new Padding(1, 1, 1, 1)
                };

                if (binaryStr[7 - i] != defaultStr[7 - i])
                {
                    bitPanel.BorderStyle = BorderStyle.Fixed3D;
                    bitPanel.BackColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(180, 220, 255) :
                        Color.FromArgb(255, 240, 240);
                }

                var bitLabel = new Label
                {
                    Text = $"Bit {i}",
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    Height = 18,
                    ForeColor = Color.FromArgb(100, 100, 100)
                };

                var valueLabel = new Label
                {
                    Text = binaryStr[7 - i].ToString(),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 14, FontStyle.Bold),
                    ForeColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(0, 80, 160) :
                        Color.FromArgb(100, 100, 100)
                };

                var field = GetFieldForBit(i);
                if (field != null)
                    _toolTip.SetToolTip(bitPanel, $"{field.Name}: {field.Description}");

                bitPanel.Controls.Add(valueLabel);
                bitPanel.Controls.Add(bitLabel);
                panel.Controls.Add(bitPanel);
            }

            return panel;
        }

        private Control CreateDetailedBitVisualization()
        {
            var mainPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            var table = new TableLayoutPanel
            {
                ColumnCount = 8,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(2, 2, 2, 2),
                BackColor = Color.White
            };

            for (int i = 0; i < 8; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

            var binaryStr = Convert.ToString(_register.RawValue, 2).PadLeft(8, '0');
            var defaultStr = Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0');

            for (int i = 7; i >= 0; i--)
            {
                // Bit number
                var bitLabel = new Label
                {
                    Text = $"{i}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(245, 245, 245),
                    ForeColor = Color.FromArgb(80, 80, 80)
                };
                table.Controls.Add(bitLabel, 7 - i, 0);

                // Bit value
                var valuePanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(220, 240, 255) :
                        Color.FromArgb(245, 245, 245),
                    Margin = new Padding(1, 1, 1, 1)
                };

                var valueLabel = new Label
                {
                    Text = binaryStr[7 - i].ToString(),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    ForeColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(0, 80, 160) :
                        Color.FromArgb(100, 100, 100)
                };

                valuePanel.Controls.Add(valueLabel);

                if (binaryStr[7 - i] != defaultStr[7 - i])
                {
                    valuePanel.BorderStyle = BorderStyle.Fixed3D;
                    valuePanel.BackColor = binaryStr[7 - i] == '1' ?
                        Color.FromArgb(180, 220, 255) :
                        Color.FromArgb(255, 240, 240);
                }

                table.Controls.Add(valuePanel, 7 - i, 1);

                // Field name
                var field = GetFieldForBit(i);
                var fieldLabel = new Label
                {
                    Text = field?.Name ?? "Reserved",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f),
                    Dock = DockStyle.Fill,
                    Height = 26,
                    AutoEllipsis = true,
                    ForeColor = field != null ?
                        Color.FromArgb(70, 70, 70) :
                        Color.FromArgb(150, 150, 150)
                };
                table.Controls.Add(fieldLabel, 7 - i, 2);

                if (field != null)
                {
                    _toolTip.SetToolTip(valuePanel, $"{field.Name}: {field.Description}");
                    _toolTip.SetToolTip(fieldLabel, $"{field.Name}: {field.Description}");
                }
            }

            mainPanel.Controls.Add(table);
            return mainPanel;
        }

        private Control CreateCompactBitComparisonVisualization()
        {
            var panel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(5, 5, 5, 5),
                BackColor = Color.White
            };

            var table = new TableLayoutPanel
            {
                ColumnCount = 5,
                RowCount = 9,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                Padding = new Padding(3, 3, 3, 3),
                BackColor = Color.White
            };

            // Compact headers
            string[] headers = { "Bit", "Current", "Default", "Field", "Change" };
            for (int i = 0; i < headers.Length; i++)
            {
                var header = new Label
                {
                    Text = headers[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(245, 245, 245),
                    Padding = new Padding(2, 2, 2, 2),
                    ForeColor = Color.FromArgb(80, 80, 80)
                };
                table.Controls.Add(header, i, 0);
            }

            var currentBinary = Convert.ToString(_register.RawValue, 2).PadLeft(8, '0');
            var defaultBinary = Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0');

            for (int bit = 7; bit >= 0; bit--)
            {
                int row = 7 - bit + 1;
                char currentBit = currentBinary[7 - bit];
                char defaultBit = defaultBinary[7 - bit];
                bool changed = currentBit != defaultBit;

                var field = GetFieldForBit(bit);

                // Bit number
                table.Controls.Add(new Label
                {
                    Text = bit.ToString(),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Padding = new Padding(2, 2, 2, 2),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(80, 80, 80)
                }, 0, row);

                // Current value
                table.Controls.Add(new Label
                {
                    Text = currentBit.ToString(),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = currentBit == '1' ?
                        Color.FromArgb(220, 240, 255) :
                        Color.FromArgb(245, 245, 245),
                    Padding = new Padding(2, 2, 2, 2),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = currentBit == '1' ?
                        Color.FromArgb(0, 80, 160) :
                        Color.FromArgb(100, 100, 100)
                }, 1, row);

                // Default value
                table.Controls.Add(new Label
                {
                    Text = defaultBit.ToString(),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = defaultBit == '1' ?
                        Color.FromArgb(220, 240, 255) :
                        Color.FromArgb(245, 245, 245),
                    Padding = new Padding(2, 2, 2, 2),
                    Font = new Font("Segoe UI", 9),
                    ForeColor = defaultBit == '1' ?
                        Color.FromArgb(0, 80, 160) :
                        Color.FromArgb(100, 100, 100)
                }, 2, row);

                // Field name
                table.Controls.Add(new Label
                {
                    Text = field?.Name ?? "Reserved",
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 2, 2, 2),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = field != null ?
                        Color.FromArgb(70, 70, 70) :
                        Color.FromArgb(150, 150, 150)
                }, 3, row);

                // Change status
                table.Controls.Add(new Label
                {
                    Text = changed ? "Modified" : "Same",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5f, changed ? FontStyle.Bold : FontStyle.Regular),
                    Padding = new Padding(2, 2, 2, 2),
                    AutoSize = true,
                    ForeColor = changed ? Color.FromArgb(200, 0, 0) : Color.FromArgb(0, 128, 0)
                }, 4, row);
            }

            panel.Controls.Add(table);
            return panel;
        }

        private string GetRawDataText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("REGISTER RAW DATA ANALYSIS");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();
            sb.AppendLine($"Address: 0x{_register.Address:X2} ({_register.Address})");
            sb.AppendLine($"Name: {_register.Name}");
            sb.AppendLine($"Full Name: {_register.FullName}");
            sb.AppendLine();
            sb.AppendLine("VALUE REPRESENTATIONS:");
            sb.AppendLine($"  Hex: 0x{_register.RawValue:X2}");
            sb.AppendLine($"  Decimal: {_register.RawValue}");
            sb.AppendLine($"  Binary: {Convert.ToString(_register.RawValue, 2).PadLeft(8, '0')}");
            sb.AppendLine();
            sb.AppendLine("DEFAULT VALUE:");
            sb.AppendLine($"  Hex: 0x{_register.DefaultValue:X2}");
            sb.AppendLine($"  Decimal: {_register.DefaultValue}");
            sb.AppendLine($"  Binary: {Convert.ToString(_register.DefaultValue, 2).PadLeft(8, '0')}");
            sb.AppendLine();
            sb.AppendLine($"Status: {(_register.IsChanged ? "CHANGED FROM DEFAULT" : "AT DEFAULT")}");
            sb.AppendLine($"Protected: {(_register.Definition.Protected == true ? "Yes" : "No")}");
            sb.AppendLine();
            sb.AppendLine("BIT STATES:");
            for (int i = 7; i >= 0; i--)
            {
                bool isSet = _register.BitStates.ContainsKey(i) && _register.BitStates[i];
                sb.AppendLine($"  Bit {i}: {(isSet ? "1" : "0")}");
            }
            sb.AppendLine();

            if (_register.Definition.Fields != null && _register.Definition.Fields.Any())
            {
                sb.AppendLine("FIELD DEFINITIONS:");
                foreach (var field in _register.Definition.Fields)
                {
                    var (start, end) = ParseBitRange(field.Bits);
                    int fieldValue = ExtractFieldValue(_register.RawValue, start, end);
                    int defaultValue = ExtractFieldValue(_register.DefaultValue, start, end);

                    sb.AppendLine($"  {field.Name} (Bits {field.Bits}):");
                    sb.AppendLine($"    Description: {field.Description}");
                    sb.AppendLine($"    Type: {field.Type}");
                    sb.AppendLine($"    Current Value: {fieldValue} (0x{fieldValue:X})");
                    sb.AppendLine($"    Default Value: {defaultValue} (0x{defaultValue:X})");
                    if (fieldValue != defaultValue)
                        sb.AppendLine($"    WARNING: Field value changed from default!");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private (int start, int end) ParseBitRange(string bitRange)
        {
            if (string.IsNullOrEmpty(bitRange))
                return (0, 0);

            var parts = bitRange.Split(':');
            if (parts.Length == 1)
            {
                int bit = int.Parse(parts[0]);
                return (bit, bit);
            }
            else
            {
                return (int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }

        private int ExtractFieldValue(byte registerValue, int startBit, int endBit)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit);
            }

            int bitCount = startBit - endBit + 1;
            int mask = ((1 << bitCount) - 1) << endBit;
            return (registerValue & mask) >> endBit;
        }

        private BitField? GetFieldForBit(int bit)
        {
            if (_register.Definition.Fields == null) return null;

            foreach (var field in _register.Definition.Fields)
            {
                var (start, end) = ParseBitRange(field.Bits);
                if (bit >= end && bit <= start)
                    return field;
            }
            return null;
        }

        private string GetFieldValueDisplay(BitField field, int value)
        {
            return RegisterDecode.DecodeField(field, value, _register.Name);
        }
    }
}