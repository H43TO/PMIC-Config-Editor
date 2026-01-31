using PMICDumpParser.Models;
using PMICDumpParser.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls
{
    /// <summary>
    /// Control for editing individual bit fields within a register - Using AppColors
    /// </summary>
    public class FieldEditorControl : UserControl
    {
        private BitField _field;
        private byte _currentValue;
        private byte _originalValue;
        private string _registerName;
        private Action<byte> _onValueChanged;

        private Label _fieldNameLabel;
        private Label _bitsLabel;
        private Label _descriptionLabel;
        private Control _valueEditor;
        private Label _currentValueLabel;
        private Label _defaultValueLabel;
        private TextBox _physicalTextBox;
        private NumericUpDown _numericUpDown;
        private ComboBox _comboBox;
        private CheckBox _flagCheckbox;
        private bool _suppressEvents = false;

        public FieldEditorControl(BitField field, byte currentValue, byte originalValue, string registerName, Action<byte> onValueChanged)
        {
            _field = field;
            _currentValue = currentValue;
            _originalValue = originalValue;
            _registerName = registerName;
            _onValueChanged = onValueChanged;

            InitializeComponent();
            UpdateFieldDisplay();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(580, 120);
            this.BackColor = Color.White;
            this.BorderStyle = BorderStyle.FixedSingle;
            this.Padding = new Padding(8);

            // Field name and bits
            _fieldNameLabel = new Label
            {
                Text = $"{_field.Name}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = AppColors.HeaderText
            };

            _bitsLabel = new Label
            {
                Text = $"Bits {_field.Bits}",
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                Location = new Point(_fieldNameLabel.Right + 10, 12),
                AutoSize = true,
                ForeColor = AppColors.DisabledText
            };

            // Description
            _descriptionLabel = new Label
            {
                Text = _field.Description,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(10, 35),
                Size = new Size(560, 35),
                ForeColor = AppColors.LabelText
            };

            // Current and default values
            var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
            int currentFieldValue = RegisterAnalyzer.ExtractFieldValue(_currentValue, startBit, endBit);
            int originalFieldValue = RegisterAnalyzer.ExtractFieldValue(_originalValue, startBit, endBit);

            _currentValueLabel = new Label
            {
                Text = $"Current: {GetFieldValueDisplay(currentFieldValue)}",
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(10, 75),
                AutoSize = true,
                ForeColor = currentFieldValue != originalFieldValue ?
                    AppColors.Error : AppColors.LabelText
            };

            _defaultValueLabel = new Label
            {
                Text = $"Default: {GetFieldValueDisplay(originalFieldValue)}",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                Location = new Point(150, 75),
                AutoSize = true,
                ForeColor = AppColors.DisabledText
            };

            // Create appropriate editor based on field type
            _valueEditor = CreateValueEditor(startBit, endBit, currentFieldValue);
            _valueEditor.Location = new Point(400, 70);

            this.Controls.AddRange(new Control[] {
                _fieldNameLabel, _bitsLabel, _descriptionLabel,
                _currentValueLabel, _defaultValueLabel, _valueEditor
            });
        }

        private Control CreateValueEditor(int startBit, int endBit, int currentValue)
        {
            int bitCount = Math.Abs(startBit - endBit) + 1;
            int maxValue = RegisterAnalyzer.GetMaxFieldValue(startBit, endBit);

            // For physical value fields (Volt, Curr, Pwr, Temp, Time, Freq)
            if (_field.Type == FieldType.Volt || _field.Type == FieldType.Curr ||
                _field.Type == FieldType.Pwr || _field.Type == FieldType.Temp ||
                _field.Type == FieldType.Time || _field.Type == FieldType.Freq)
            {
                return CreatePhysicalValueEditor(currentValue, bitCount, maxValue);
            }

            // Check if this is a single bit (flag)
            if (bitCount == 1)
            {
                return CreateFlagEditor(currentValue);
            }
            // Check if enum values are defined
            else if (_field.EnumValues != null && _field.EnumValues.Any())
            {
                return CreateEnumEditor(currentValue);
            }
            // For multi-bit fields without enums
            else
            {
                return CreateNumericEditor(currentValue, maxValue, bitCount);
            }
        }

        private Control CreatePhysicalValueEditor(int currentValue, int bitCount, int maxValue)
        {
            var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
            var panel = new Panel
            {
                Size = new Size(180, 55),
                BackColor = Color.White
            };

            // Get the decoded display value
            string decodedValue = RegisterDecoder.DecodeField(_field, currentValue, _registerName);
            string unit = RegisterEditService.GetFieldUnit(_field.Type);

            var physicalLabel = new Label
            {
                Text = $"{unit}:",
                Location = new Point(0, 5),
                Size = new Size(30, 20),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.LabelText
            };

            _physicalTextBox = new TextBox
            {
                Location = new Point(35, 3),
                Size = new Size(70, 22),
                Font = new Font("Segoe UI", 9),
                Text = GetPhysicalValueFromDecoded(decodedValue),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var unitLabel = new Label
            {
                Text = unit,
                Location = new Point(110, 5),
                Size = new Size(30, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9),
                ForeColor = AppColors.DisabledText
            };

            // Raw value label
            var rawLabel = new Label
            {
                Text = $"Raw: 0x{currentValue:X} ({currentValue})",
                Location = new Point(0, 30),
                Size = new Size(170, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = AppColors.DisabledText
            };

            // Update only on focus loss or Enter key
            _physicalTextBox.Leave += (s, e) => UpdatePhysicalValue(_physicalTextBox, rawLabel, startBit, endBit, maxValue);

            _physicalTextBox.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    UpdatePhysicalValue(_physicalTextBox, rawLabel, startBit, endBit, maxValue);
                    e.Handled = true;
                }
            };

            // Clear error highlighting when user starts typing
            _physicalTextBox.TextChanged += (s, e) =>
            {
                _physicalTextBox.BackColor = Color.White;
            };

            panel.Controls.AddRange(new Control[] { physicalLabel, _physicalTextBox, unitLabel, rawLabel });
            return panel;
        }

        private void UpdatePhysicalValue(TextBox textBox, Label rawLabel, int startBit, int endBit, int maxValue)
        {
            if (_suppressEvents || string.IsNullOrEmpty(textBox.Text))
                return;

            try
            {
                var (_, _) = RegisterAnalyzer.ParseBitRange(_field.Bits);
                int newFieldValue = RegisterEditService.EncodeFieldValue(_field, textBox.Text, _registerName);

                // Validate bounds
                if (newFieldValue >= 0 && newFieldValue <= maxValue)
                {
                    byte newValue = RegisterAnalyzer.SetFieldValue(_currentValue, startBit, endBit, newFieldValue);
                    _onValueChanged?.Invoke(newValue);

                    rawLabel.Text = $"Raw: 0x{newFieldValue:X} ({newFieldValue})";

                    // Update current value display
                    UpdateCurrentValueLabel(newFieldValue);
                }
                else
                {
                    textBox.BackColor = AppColors.Critical;
                }
            }
            catch
            {
                textBox.BackColor = AppColors.Critical;
            }
        }

        private string GetPhysicalValueFromDecoded(string decodedValue)
        {
            // Extract numeric value from decoded string
            if (string.IsNullOrEmpty(decodedValue))
                return "0";

            // Find first number in the string
            string numberPart = "";
            bool inNumber = false;
            bool foundDecimal = false;

            foreach (char c in decodedValue)
            {
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    numberPart += c;
                    inNumber = true;
                    if (c == '.' || c == ',') foundDecimal = true;
                }
                else if (inNumber && (c == ' ' || c == ':' || c == '='))
                {
                    // Allow spaces/colons/equals within numbers
                    if (foundDecimal) break;
                    continue;
                }
                else if (inNumber)
                {
                    break;
                }
            }

            return string.IsNullOrEmpty(numberPart) ? "0" : numberPart;
        }

        private Control CreateFlagEditor(int currentValue)
        {
            _flagCheckbox = new CheckBox
            {
                Text = currentValue == 1 ? "Set (1)" : "Clear (0)",
                Checked = currentValue == 1,
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                Location = new Point(0, 0),
                ForeColor = AppColors.LabelText,
                Cursor = Cursors.Hand
            };

            _flagCheckbox.CheckedChanged += (s, e) =>
            {
                if (_suppressEvents) return;

                var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
                int newFieldValue = _flagCheckbox.Checked ? 1 : 0;
                byte newValue = RegisterAnalyzer.SetFieldValue(_currentValue, startBit, endBit, newFieldValue);
                _onValueChanged?.Invoke(newValue);

                UpdateCurrentValueLabel(newFieldValue);
                _flagCheckbox.Text = _flagCheckbox.Checked ? "Set (1)" : "Clear (0)";
            };

            return _flagCheckbox;
        }

        private Control CreateEnumEditor(int currentValue)
        {
            _comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Font = new Font("Segoe UI", 9),
                Location = new Point(0, 0),
                BackColor = Color.White,
                ForeColor = AppColors.ValueText,
                Cursor = Cursors.Hand
            };

            foreach (var enumItem in _field.EnumValues)
            {
                _comboBox.Items.Add(new ComboBoxItem
                {
                    Text = $"{enumItem.Value} ({enumItem.Key})",
                    Value = int.Parse(enumItem.Key)
                });
            }

            // Select current value
            var currentItem = _comboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Value == currentValue);
            if (currentItem != null)
                _comboBox.SelectedItem = currentItem;

            _comboBox.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressEvents || _comboBox.SelectedItem == null) return;

                if (_comboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
                    byte newValue = RegisterAnalyzer.SetFieldValue(_currentValue, startBit, endBit, selectedItem.Value);
                    _onValueChanged?.Invoke(newValue);

                    UpdateCurrentValueLabel(selectedItem.Value);
                }
            };

            return _comboBox;
        }

        private Control CreateNumericEditor(int currentValue, int maxValue, int bitCount)
        {
            var panel = new Panel
            {
                Size = new Size(180, 55),
                BackColor = Color.White
            };

            _numericUpDown = new NumericUpDown
            {
                Minimum = 0,
                Maximum = maxValue,
                Value = currentValue,
                Width = 70,
                Location = new Point(0, 0),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.White,
                ForeColor = AppColors.ValueText
            };

            var hexLabel = new Label
            {
                Text = $"0x{currentValue:X}",
                Location = new Point(75, 5),
                AutoSize = true,
                Font = new Font("Consolas", 9),
                ForeColor = AppColors.Info
            };

            var binLabel = new Label
            {
                Text = $"Bin: {Convert.ToString(currentValue, 2).PadLeft(bitCount, '0')}",
                Location = new Point(0, 30),
                AutoSize = true,
                Font = new Font("Consolas", 8),
                ForeColor = AppColors.DisabledText
            };

            _numericUpDown.ValueChanged += (s, e) =>
            {
                if (_suppressEvents) return;

                int newFieldValue = (int)_numericUpDown.Value;
                hexLabel.Text = $"0x{newFieldValue:X}";
                binLabel.Text = $"Bin: {Convert.ToString(newFieldValue, 2).PadLeft(bitCount, '0')}";

                var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
                byte newValue = RegisterAnalyzer.SetFieldValue(_currentValue, startBit, endBit, newFieldValue);
                _onValueChanged?.Invoke(newValue);

                UpdateCurrentValueLabel(newFieldValue);
            };

            panel.Controls.Add(_numericUpDown);
            panel.Controls.Add(hexLabel);
            panel.Controls.Add(binLabel);
            panel.Height = 55;

            return panel;
        }

        private void UpdateFieldDisplay()
        {
            var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
            int currentFieldValue = RegisterAnalyzer.ExtractFieldValue(_currentValue, startBit, endBit);
            int originalFieldValue = RegisterAnalyzer.ExtractFieldValue(_originalValue, startBit, endBit);

            UpdateCurrentValueLabel(currentFieldValue);
        }

        private void UpdateCurrentValueLabel(int fieldValue)
        {
            var (startBit, endBit) = RegisterAnalyzer.ParseBitRange(_field.Bits);
            int originalFieldValue = RegisterAnalyzer.ExtractFieldValue(_originalValue, startBit, endBit);

            _currentValueLabel.Text = $"Current: {GetFieldValueDisplay(fieldValue)}";
            _currentValueLabel.ForeColor = fieldValue != originalFieldValue ?
                AppColors.Error : AppColors.LabelText;
        }

        private string GetFieldValueDisplay(int value)
        {
            return RegisterDecoder.DecodeField(_field, value, _registerName);
        }

        private class ComboBoxItem
        {
            public string Text { get; set; }
            public int Value { get; set; }

            public override string ToString() => Text;
        }
    }
}