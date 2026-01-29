using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls.RegisterDetailTabs
{
    /// <summary>
    /// Bit field analysis tab showing detailed field information
    /// </summary>
    public class BitFieldAnalysisTab : UserControl
    {
        private readonly ParsedRegister _register;
        private readonly ToolTip _toolTip;

        public BitFieldAnalysisTab(ParsedRegister register, ToolTip toolTip)
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

                    string meaning = RegisterDecoder.DecodeField(field, fieldValue, _register.Name);

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
            Controls.Add(splitContainer);
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
    }
}