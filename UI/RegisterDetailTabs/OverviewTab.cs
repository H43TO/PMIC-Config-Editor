using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls.RegisterDetailTabs
{
    /// <summary>
    /// Overview tab showing basic register information
    /// </summary>
    public class OverviewTab : UserControl
    {
        private readonly ParsedRegister _register;
        private readonly ToolTip _toolTip;

        public OverviewTab(ParsedRegister register, ToolTip toolTip)
        {
            _register = register;
            _toolTip = toolTip;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

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
            Controls.Add(scrollPanel);
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