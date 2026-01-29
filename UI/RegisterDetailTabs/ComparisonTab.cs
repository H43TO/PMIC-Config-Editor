using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls.RegisterDetailTabs
{
    /// <summary>
    /// Comparison tab showing current vs default values
    /// </summary>
    public class ComparisonTab : UserControl
    {
        private readonly ParsedRegister _register;
        private readonly ToolTip _toolTip;

        public ComparisonTab(ParsedRegister register, ToolTip toolTip)
        {
            _register = register;
            _toolTip = toolTip;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8, 8, 8, 8);
            BackColor = Color.White;

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

            Controls.Add(mainPanel);
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

        private BitField GetFieldForBit(int bit)
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
    }
}