using PMICDumpParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Service for managing register edits and validation
    /// </summary>
    public static class RegisterEditService
    {
        /// <summary>
        /// Validates if a register can be edited based on its type
        /// </summary>
        public static bool CanEditRegister(RegisterDef definition)
        {
            if (definition == null)
                return false;

            // Check if it's a reserved register first
            if (IsReservedRegister(definition))
                return false;

            var type = definition.Type.ToUpper();

            // Only RW (Read-Write) and RWPE (Read-Write Protected) registers can be edited
            // W1O (Write 1 to Clear) can also be written to clear bits
            // W (Write Only) can be written but not read back
            return type == "RW" || type == "RWPE" || type == "W" || type == "W1O";
        }

        /// <summary>
        /// Checks if a register definition indicates a reserved register
        /// </summary>
        private static bool IsReservedRegister(RegisterDef definition)
        {
            if (definition == null) return true;

            // Check type
            bool typeReserved = definition.Type.ToUpper() == "RV" ||
                               definition.Type.ToUpper() == "ROE";

            // Check name
            bool nameReserved = definition.Name.StartsWith("RESERVED_", StringComparison.OrdinalIgnoreCase) ||
                               definition.Name.StartsWith("RESERVED", StringComparison.OrdinalIgnoreCase);

            // Check category
            bool categoryReserved = definition.Category == "Reserved" ||
                                   string.IsNullOrWhiteSpace(definition.Category);

            return typeReserved || nameReserved || categoryReserved;
        }

        /// <summary>
        /// Encodes a physical value into register field value based on field type
        /// </summary>
        public static int EncodeFieldValue(BitField field, string physicalValue, string registerName = "")
        {
            if (field == null || string.IsNullOrEmpty(physicalValue))
                return 0;

            // Try to parse as number first
            if (double.TryParse(physicalValue, out double numericValue))
            {
                return field.Type switch
                {
                    FieldType.Volt => EncodeVoltageField(field, numericValue, registerName),
                    FieldType.Curr => EncodeCurrentField(field, numericValue, registerName),
                    FieldType.Pwr => EncodePowerField(field, numericValue),
                    FieldType.Temp => EncodeTemperatureField(field, numericValue, registerName),
                    FieldType.Time => EncodeTimeField(field, numericValue, registerName),
                    FieldType.Freq => EncodeFrequencyField(field, numericValue),
                    FieldType.Flag => EncodeFlagField(field, physicalValue),
                    FieldType.Raw => (int)numericValue,
                    FieldType.Bin => Convert.ToInt32(physicalValue, 2),
                    FieldType.Dec => (int)numericValue,
                    _ => (int)numericValue
                };
            }

            // Check if it's an enum value
            if (field.EnumValues != null)
            {
                foreach (var enumItem in field.EnumValues)
                {
                    if (enumItem.Value.Equals(physicalValue, StringComparison.OrdinalIgnoreCase) ||
                        enumItem.Key.Equals(physicalValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return int.Parse(enumItem.Key);
                    }
                }
            }

            // Try to parse hex
            if (physicalValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt32(physicalValue.Substring(2), 16);
            }

            return 0;
        }

        private static int EncodeVoltageField(BitField field, double voltage, string registerName)
        {
            if (registerName.Contains("SWA_Voltage") || registerName.Contains("SWB_Voltage"))
            {
                // Voltage setting: 800mV + (value * 5mV)
                // So: value = (voltage_mV - 800) / 5
                double voltageMv = voltage * 1000;
                int value = (int)Math.Round((voltageMv - 800) / 5);
                return Math.Max(0, Math.Min(127, value)); // 7-bit value
            }
            else if (registerName.Contains("SWC_Voltage"))
            {
                // Voltage setting: 1500mV + (value * 5mV)
                double voltageMv = voltage * 1000;
                int value = (int)Math.Round((voltageMv - 1500) / 5);
                return Math.Max(0, Math.Min(127, value));
            }
            else if (field.Name.Contains("PGL"))
            {
                // PGL setting: 0 = -5%, 1 = -7.5%
                return voltage <= 5.0 ? 0 : 1;
            }

            return (int)voltage;
        }

        private static int EncodeCurrentField(BitField field, double current, string registerName)
        {
            if (field.Name.Contains("HIGH_CURRENT") || field.Name.Contains("CONSUMPTION"))
            {
                // Current measurement: value * 0.125A
                // So: value = current / 0.125
                return (int)Math.Round(current / 0.125);
            }
            return (int)current;
        }

        private static int EncodePowerField(BitField field, double power)
        {
            // Power measurement: value * 0.125W
            return (int)Math.Round(power / 0.125);
        }

        private static int EncodeTemperatureField(BitField field, double temperature, string registerName)
        {
            // Temperature thresholds are discrete values
            if (field.Name.Contains("HTW") || field.Name.Contains("TEMP_MEAS"))
            {
                return temperature switch
                {
                    < 80 => 0,
                    >= 80 and < 90 => 0,
                    >= 85 and < 95 => 1,
                    >= 95 and < 105 => 2,
                    >= 105 and < 115 => 3,
                    >= 115 and < 125 => 4,
                    >= 125 and < 135 => 5,
                    >= 135 and < 140 => 6,
                    >= 140 => 7,
                    _ => 0
                };
            }
            return (int)temperature;
        }

        private static int EncodeTimeField(BitField field, double timeMs, string registerName)
        {
            if (field.Name.Contains("SOFT_START"))
            {
                // Soft start time: 1ms + (value * 1ms)
                return (int)Math.Round(timeMs - 1);
            }
            else if (field.Name.Contains("SOFT_STOP_TIME") || field.Name.Contains("SST"))
            {
                // Discrete time values
                return timeMs switch
                {
                    0.5 => 0,
                    1 => 1,
                    2 => 2,
                    4 => 3,
                    8 => 3, // For SWC only
                    _ => (int)timeMs
                };
            }
            return (int)timeMs;
        }

        private static int EncodeFrequencyField(BitField field, double frequencyKhz)
        {
            return frequencyKhz switch
            {
                750 => 0,
                1000 => 1,
                1250 => 2,
                1500 => 3,
                _ => (int)frequencyKhz
            };
        }

        private static int EncodeFlagField(BitField field, string value)
        {
            string lowerValue = value.ToLower();
            bool isSet = lowerValue == "1" || lowerValue == "true" || lowerValue == "set" || lowerValue == "active";

            if (field.ActiveHigh == false)
                isSet = !isSet;

            return isSet ? 1 : 0;
        }

        /// <summary>
        /// Gets the physical unit for a field type
        /// </summary>
        public static string GetFieldUnit(FieldType type)
        {
            return type switch
            {
                FieldType.Volt => "V",
                FieldType.Curr => "A",
                FieldType.Pwr => "W",
                FieldType.Temp => "°C",
                FieldType.Time => "ms",
                FieldType.Freq => "kHz",
                FieldType.Flag => "",
                FieldType.Raw => "",
                FieldType.Bin => "binary",
                FieldType.Dec => "",
                _ => ""
            };
        }

        /// <summary>
        /// Validates a new value for a register
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateValue(RegisterDef definition, byte newValue, byte? originalValue = null)
        {
            if (definition == null)
                return (false, "Register definition is null");

            // Check if register is read-only
            var type = definition.Type.ToUpper();
            if (type == "RO" || type == "ROE" || type == "RV")
                return (false, $"Register is {type} (Read-Only/Reserved) and cannot be modified");

            // Check for protected registers in DIMM vendor region (0x40-0x6F)
            byte address = definition.AddressByte;
            if (address >= 0x40 && address <= 0x6F && definition.Protected != true)
            {
                return (false, $"Register is in protected DIMM vendor region (0x40-0x6F)");
            }

            // Special validation for W1O registers (Write 1 to Clear)
            if (type == "W1O" && originalValue.HasValue)
            {
                // For W1O, we can only write 1s to clear bits (writing 1 clears the corresponding bit)
                // The actual hardware behavior might vary, but for editing we should track intent
                if (newValue != 0 && (newValue & originalValue.Value) != originalValue.Value)
                {
                    return (true, "Note: Writing 1 to W1O register will clear corresponding bits");
                }
            }

            // Check bit field constraints if fields are defined
            if (definition.Fields != null && definition.Fields.Any())
            {
                foreach (var field in definition.Fields)
                {
                    if (field.Type == FieldType.Reserved)
                        continue;

                    var (startBit, endBit) = ParseBitRange(field.Bits);
                    int fieldValue = ExtractFieldValue(newValue, startBit, endBit);

                    // Check enum constraints
                    if (field.EnumValues != null && !field.EnumValues.ContainsKey(fieldValue.ToString()))
                    {
                        var validValues = string.Join(", ", field.EnumValues.Keys);
                        return (false, $"Field '{field.Name}' value {fieldValue} is not valid. Valid values: {validValues}");
                    }

                    // Check bit field boundaries
                    int maxValue = (1 << (Math.Abs(startBit - endBit) + 1)) - 1;
                    if (fieldValue > maxValue)
                    {
                        return (false, $"Field '{field.Name}' value {fieldValue} exceeds maximum {maxValue}");
                    }
                }
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Applies a new value to a register and updates the dump
        /// </summary>
        public static void ApplyEdit(PmicDump dump, byte address, byte newValue)
        {
            if (dump == null || !dump.Registers.ContainsKey(address))
                return;

            var register = dump.Registers[address];
            register.RawValue = newValue;

            // Update decoded value
            register.DecodedValue = RegisterDecoder.DecodeRegister(register);

            // Update bit states
            for (int bit = 0; bit < 8; bit++)
            {
                register.BitStates[bit] = ((newValue >> bit) & 1) == 1;
            }

            // Update dump's raw data
            if (dump.RawData.Length > address)
            {
                dump.RawData[address] = newValue;
            }
        }

        /// <summary>
        /// Resets a register to its default value
        /// </summary>
        public static void ResetToDefault(PmicDump dump, byte address)
        {
            if (dump == null || !dump.Registers.ContainsKey(address))
                return;

            var register = dump.Registers[address];
            ApplyEdit(dump, address, register.DefaultValue);
        }

        /// <summary>
        /// Resets all changed registers to their defaults
        /// </summary>
        public static void ResetAllChanges(PmicDump dump)
        {
            if (dump == null)
                return;

            foreach (var register in dump.Registers.Values.Where(r => r.IsChanged).ToList())
            {
                ApplyEdit(dump, register.Address, register.DefaultValue);
            }
        }

        /// <summary>
        /// Saves edits to a new dump file
        /// </summary>
        public static void SaveToFile(PmicDump dump, string filePath)
        {
            if (dump == null || string.IsNullOrEmpty(filePath))
                return;

            System.IO.File.WriteAllBytes(filePath, dump.RawData);
        }

        #region Helper Methods

        private static (int start, int end) ParseBitRange(string bitRange)
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

        private static int ExtractFieldValue(byte registerValue, int startBit, int endBit)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit);
            }

            int bitCount = startBit - endBit + 1;
            int mask = ((1 << bitCount) - 1) << endBit;
            return (registerValue & mask) >> endBit;
        }

        #endregion
    }
}