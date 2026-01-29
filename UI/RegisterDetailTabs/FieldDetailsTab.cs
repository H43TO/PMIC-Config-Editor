using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls.RegisterDetailTabs
{
    /// <summary>
    /// Field details tab showing individual field information
    /// </summary>
    public class FieldDetailsTab : UserControl
    {
        private readonly ParsedRegister _register;
        private readonly ToolTip _toolTip;

        public FieldDetailsTab(ParsedRegister register, ToolTip toolTip)
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
                Controls.Add(scrollPanel);
                return;
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

                bool isCritical = IsCriticalChange(_register);

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
            Controls.Add(scrollPanel);
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

        private string GetFieldValueDisplay(BitField field, int value)
        {
            return RegisterDecoder.DecodeField(field, value, _register.Name);
        }

        private bool IsCriticalChange(ParsedRegister reg)
        {
            const double CRITICAL_THRESHOLD_PERCENT = 23.0;
            const double MAX_VOLTAGE_RANGE = 440.0;

            var name = reg.Name.ToUpper();
            bool isVoltageReg = name.Contains("VOLT") || name.Contains("SWA_VOLT") ||
                                name.Contains("SWB_VOLT") || name.Contains("SWC_VOLT");
            bool isCurrentReg = name.Contains("CURR") || name.Contains("SWA_CURR") ||
                                name.Contains("SWB_CURR") || name.Contains("SWC_CURR");

            if (!isVoltageReg && !isCurrentReg) return false;
            if (!reg.IsChanged) return false;

            double nominalValue = reg.DefaultValue;
            if (nominalValue <= 0) return false;

            double currentValue = reg.RawValue;
            double absoluteChange = Math.Abs(currentValue - nominalValue);
            double changePercent = (absoluteChange / MAX_VOLTAGE_RANGE) * 100.0;

            return changePercent > CRITICAL_THRESHOLD_PERCENT;
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