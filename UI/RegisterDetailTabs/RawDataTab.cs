using PMICDumpParser.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls.RegisterDetailTabs
{
    /// <summary>
    /// Raw data tab showing register data in various formats
    /// </summary>
    public class RawDataTab : UserControl
    {
        private readonly ParsedRegister _register;

        public RawDataTab(ParsedRegister register)
        {
            _register = register;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(10, 10, 10, 10);
            BackColor = Color.White;

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

            Controls.Add(textBox);
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
    }
}