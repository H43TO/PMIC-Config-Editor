using System;
using System.Collections.Generic;

namespace PMICDumpParser.Models
{
    public static class RegisterDecode
    {

        #region Field Decoder Method

        public static string DecodeField(BitField field, int fieldValue, string registerName = "")
        {
            if (field.Type == FieldType.Reserved)
                return $"0x{fieldValue:X} ({fieldValue})";

            // If field has enum values, use them
            if (field.EnumValues != null && field.EnumValues.TryGetValue(fieldValue.ToString(), out var enumText))
            {
                return $"{enumText} ({fieldValue})";
            }

            // Decode based on field type
            return field.Type switch
            {
                FieldType.Volt => DecodeVoltageField(field, fieldValue, registerName),
                FieldType.Curr => DecodeCurrentField(field, fieldValue, registerName),
                FieldType.Pwr => DecodePowerField(field, fieldValue),
                FieldType.Temp => DecodeTemperatureField(field, fieldValue, registerName),
                FieldType.Time => DecodeTimeField(field, fieldValue, registerName),
                FieldType.Freq => DecodeFrequencyField(field, fieldValue),
                FieldType.Flag => DecodeFlagField(field, fieldValue),
                FieldType.Raw => $"0x{fieldValue:X} ({fieldValue})",
                FieldType.Bin => Convert.ToString(fieldValue, 2).PadLeft(GetBitWidth(field.Bits), '0'),
                FieldType.Dec => fieldValue.ToString(),
                FieldType.Enum => fieldValue.ToString(), // Should have been handled by enum above
                _ => fieldValue.ToString()
            };
        }

        private static int GetBitWidth(string bits)
        {
            var parts = bits.Split(':');
            if (parts.Length == 1)
                return 1;
            else
                return Math.Abs(int.Parse(parts[0]) - int.Parse(parts[1])) + 1;
        }

        #endregion

        #region Specific Field Decoders

        private static string DecodeVoltageField(BitField field, int fieldValue, string registerName)
        {
            // Determine which voltage range based on register name
            if (registerName.Contains("SWA_Voltage") || registerName.Contains("SWB_Voltage"))
            {
                // SWA/SWB: 800mV to 1435mV in 5mV steps
                double voltageMv = 800 + (fieldValue * 5);
                double voltage = voltageMv / 1000.0;
                return $"{voltage:F3}V ({voltageMv}mV)";
            }
            else if (registerName.Contains("SWC_Voltage"))
            {
                // SWC: 1500mV to 2135mV in 5mV steps
                double voltageMv = 1500 + (fieldValue * 5);
                double voltage = voltageMv / 1000.0;
                return $"{voltage:F3}V ({voltageMv}mV)";
            }
            else if (field.Name.Contains("PGL"))
            {
                // PGL field: -5% or -7.5%
                return fieldValue == 0 ? "-5%" : "-7.5%";
            }
            else if (field.Name.Contains("PGH"))
            {
                // PGH field
                return fieldValue switch
                {
                    0 => "+5% from setting",
                    1 => "+7.5% from setting",
                    2 => "+10% from setting",
                    3 => "+2.5% from setting",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("OV"))
            {
                // OV field
                return fieldValue switch
                {
                    0 => "+7.5% from setting",
                    1 => "+10% from setting",
                    2 => "+12.5% from setting",
                    3 => "+5% from setting",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("UVLO"))
            {
                // UVLO field
                return fieldValue switch
                {
                    0 => "-10% from setting",
                    1 => "-12.5% from setting",
                    2 => "-5% from setting",
                    3 => "-7.5% from setting",
                    _ => $"{fieldValue}"
                };
            }
            else
            {
                // Generic voltage field
                return $"{fieldValue} (Voltage setting)";
            }
        }

        private static string DecodeCurrentField(BitField field, int fieldValue, string registerName)
        {
            // Current: 0.125A per step for high current warning thresholds
            if (field.Name.Contains("HIGH_CURRENT") || field.Name.Contains("CONSUMPTION"))
            {
                double current = fieldValue * 0.125;
                return $"{current:F3}A";
            }
            else if (field.Name.Contains("A_OC"))
            {
                return fieldValue switch
                {
                    0 => "3.0A",
                    1 => "3.5A",
                    2 => "4.0A",
                    3 => "4.5A",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("B_OC"))
            {
                return fieldValue switch
                {
                    0 => "3.0A",
                    1 => "3.5A",
                    2 => "4.0A",
                    3 => "4.5A",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("C_OC"))
            {
                return fieldValue switch
                {
                    0 => "0.5A",
                    1 => "1.0A",
                    2 => "1.5A",
                    3 => "2.0A",
                    _ => $"{fieldValue}"
                };
            }
            else
            {
                // Generic current field
                return $"{fieldValue} (Current setting)";
            }
        }

        private static string DecodeTemperatureField(BitField field, int fieldValue, string registerName)
        {
            if (field.Name.Contains("HTW") || field.Name.Contains("TEMP_MEAS"))
            {
                return fieldValue switch
                {
                    0 => "< 80°C (±5°C)",
                    1 => "85°C (±5°C)",
                    2 => "95°C (±5°C)",
                    3 => "105°C (±5°C)",
                    4 => "115°C (±5°C)",
                    5 => "125°C (±5°C)",
                    6 => "135°C (±5°C)",
                    7 => "≥ 140°C (±5°C)",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("OTP"))
            {
                return fieldValue switch
                {
                    0 => "105°C",
                    1 => "115°C",
                    2 => "125°C",
                    3 => "135°C",
                    4 => "145°C",
                    _ => $"{fieldValue}"
                };
            }
            else
            {
                return $"{fieldValue}°C";
            }
        }

        private static string DecodePowerField(BitField field, int fieldValue)
        {
            double power = fieldValue * 0.125;
            return $"{power:F3}W";
        }

        private static string DecodeTimeField(BitField field, int fieldValue, string registerName)
        {
            if (field.Name.Contains("SOFT_STOP_TIME"))
            {
                return fieldValue switch
                {
                    0 => "0.5ms",
                    1 => "1ms",
                    2 => "2ms",
                    3 => "4ms",
                    _ => $"{fieldValue}"
                };
            }
            else if (field.Name.Contains("SST"))
            {
                if (registerName.Contains("SWA") || registerName.Contains("SWB"))
                {
                    return fieldValue switch
                    {
                        0 => "0.5ms",
                        1 => "1ms",
                        2 => "2ms",
                        3 => "4ms",
                        _ => $"{fieldValue}"
                    };
                }
                else if (registerName.Contains("SWC"))
                {
                    return fieldValue switch
                    {
                        0 => "1ms",
                        1 => "2ms",
                        2 => "4ms",
                        3 => "8ms",
                        _ => $"{fieldValue}"
                    };
                }
            }
            else if (field.Name.Contains("SOFT_START"))
            {
                double ms = 1 + (fieldValue * 1); // 1ms to 14ms in 1ms steps
                return $"{ms:F0}ms";
            }

            return $"{fieldValue}ms";
        }

        private static string DecodeFrequencyField(BitField field, int fieldValue)
        {
            return fieldValue switch
            {
                0 => "750kHz",
                1 => "1000kHz",
                2 => "1250kHz",
                3 => "1500kHz",
                _ => $"{fieldValue}"
            };
        }

        private static string DecodeFlagField(BitField field, int fieldValue)
        {
            bool isSet = fieldValue == 1;
            bool isActive = (isSet && field.ActiveHigh == true) || (!isSet && field.ActiveHigh == false);
            return isActive ? "Active" : "Inactive";
        }

        #endregion

        #region Register-Level Decoders (keep existing methods below)

        // SWA/SWB Voltage: 800mV to 1435mV in 5mV steps (bits 7:1)
        public static string DecodeSwaSwbVoltage(byte value)
        {
            int voltageSetting = (value >> 1) & 0x7F; // Bits 7:1
            int pglSetting = value & 0x01; // Bit 0

            // Voltage formula: 800mV + (value * 5mV)
            double voltageMv = 800 + (voltageSetting * 5);
            double voltage = voltageMv / 1000;

            // PGL: -5% or -7.5%
            double pglPercent = pglSetting == 0 ? 5.0 : 7.5;

            return $"{voltage:F3}V, PGL: -{pglPercent}%";
        }

        // SWC Voltage: 1500mV to 2135mV in 5mV steps (bits 7:1)
        public static string DecodeSwcVoltage(byte value)
        {
            int voltageSetting = (value >> 1) & 0x7F; // Bits 7:1
            int pglSetting = value & 0x01; // Bit 0

            // Voltage formula: 1500mV + (value * 5mV)
            double voltageMv = 1500 + (voltageSetting * 5);
            double voltage = voltageMv / 1000;

            // PGL: -5% or -7.5%
            double pglPercent = pglSetting == 0 ? 5.0 : 7.5;

            return $"{voltage:F3}V, PGL: -{pglPercent}%";
        }


        #endregion

        #region Current & Power Decoders

        // SWA Current: 0.125A per step (full 8 bits)
        public static string DecodeSwaCurrent(byte value)
        {
            double current = value * 0.125;
            return $"Current: {current:F3}A";
        }

        // SWB/SWC Current: 0.125A per step (bits 5:0)
        public static string DecodeSwbSwcCurrent(byte value)
        {
            int measurement = value & 0x3F; // Bits 5:0
            double current = measurement * 0.125;
            return $"Current: {current:F3}A";
        }

        // SWA Power (when bit 0x1A[1] = 1): 0.125W per step
        public static string DecodeSwaPower(byte value)
        {
            double power = value * 0.125;
            return $"Power: {power:F3}W";
        }

        #endregion

        #region Threshold Decoders

        public static string DecodeSwaSwbThreshold(byte value)
        {
            int pgh = (value >> 6) & 0x03;
            int ov = (value >> 4) & 0x03;
            int uvlo = (value >> 2) & 0x03;
            int sst = value & 0x03;

            string pghStr = pgh switch
            {
                0 => "+5%",
                1 => "+7.5%",
                2 => "+10%",
                3 => "+2.5%",
                _ => "?"
            };

            string ovStr = ov switch
            {
                0 => "+7.5%",
                1 => "+10%",
                2 => "+12.5%",
                3 => "+5%",
                _ => "?"
            };

            string uvloStr = uvlo switch
            {
                0 => "-10%",
                1 => "-12.5%",
                2 => "-5%",
                3 => "-7.5%",
                _ => "?"
            };

            string sstStr = sst switch
            {
                0 => "0.5ms",
                1 => "1ms",
                2 => "2ms",
                3 => "4ms",
                _ => "?"
            };

            return $"PGH: {pghStr}, OV: {ovStr}, UVLO: {uvloStr}, SST: {sstStr}";
        }

        public static string DecodeSwcThreshold(byte value)
        {
            int pgh = (value >> 6) & 0x03;
            int ov = (value >> 4) & 0x03;
            int uvlo = (value >> 2) & 0x03;
            int sst = value & 0x03;

            string pghStr = pgh switch
            {
                0 => "+5%",
                1 => "+7.5%",
                2 => "+10%",
                3 => "+2.5%",
                _ => "?"
            };

            string ovStr = ov switch
            {
                0 => "+7.5%",
                1 => "+10%",
                2 => "+12.5%",
                3 => "+5%",
                _ => "?"
            };

            string uvloStr = uvlo switch
            {
                0 => "-10%",
                1 => "-12.5%",
                2 => "-5%",
                3 => "-7.5%",
                _ => "?"
            };

            string sstStr = sst switch
            {
                0 => "1ms",
                1 => "2ms",
                2 => "4ms",
                3 => "8ms",
                _ => "?"
            };

            return $"PGH: {pghStr}, OV: {ovStr}, UVLO: {uvloStr}, SST: {sstStr}";
        }

        #endregion

        #region Switching Decoders

        public static string DecodeFswMode1(byte value)
        {
            int mode = (value >> 6) & 0x03;
            int fsw = (value >> 4) & 0x03;

            string modeStr = mode switch
            {
                2 => "COT; DCM",
                3 => "COT; CCM",
                _ => $"Mode {mode}"
            };

            string fswStr = fsw switch
            {
                0 => "750kHz",
                1 => "1000kHz",
                2 => "1250kHz",
                3 => "1500kHz",
                _ => $"{fsw}"
            };

            return $"SWA: {modeStr}, {fswStr}";
        }

        public static string DecodeFswMode2(byte value)
        {
            int bMode = (value >> 6) & 0x03;
            int bFsw = (value >> 4) & 0x03;
            int cMode = (value >> 2) & 0x03;
            int cFsw = value & 0x03;

            string bModeStr = bMode switch
            {
                2 => "COT; DCM",
                3 => "COT; CCM",
                _ => $"Mode {bMode}"
            };

            string cModeStr = cMode switch
            {
                2 => "COT; DCM",
                3 => "COT; CCM",
                _ => $"Mode {cMode}"
            };

            string bFswStr = bFsw switch
            {
                0 => "750kHz",
                1 => "1000kHz",
                2 => "1250kHz",
                3 => "1500kHz",
                _ => $"{bFsw}"
            };

            string cFswStr = cFsw switch
            {
                0 => "750kHz",
                1 => "1000kHz",
                2 => "1250kHz",
                3 => "1500kHz",
                _ => $"{cFsw}"
            };

            return $"SWB: {bModeStr}, {bFswStr}; SWC: {cModeStr}, {cFswStr}";
        }

        #endregion

        #region LDO Decoders

        public static string DecodeLdoVoltage(byte value)
        {
            int ldo18v = (value >> 6) & 0x03;
            int ldo10v = (value >> 1) & 0x03;

            double voltage18 = ldo18v switch
            {
                0 => 1.7,
                1 => 1.8,
                2 => 1.9,
                3 => 2.0,
                _ => 0
            };

            double voltage10 = ldo10v switch
            {
                0 => 0.9,
                1 => 1.0,
                2 => 1.1,
                3 => 1.2,
                _ => 0
            };

            return $"VLDO1.8V: {voltage18:F1}V, VLDO1.0V: {voltage10:F1}V";
        }

        #endregion

        #region OC Threshold Decoders

        public static string DecodeOcThreshold(byte value)
        {
            int aOc = (value >> 6) & 0x03;
            int bOc = (value >> 2) & 0x03;
            int cOc = value & 0x03;

            double aCurrent = aOc switch
            {
                0 => 3.0,
                1 => 3.5,
                2 => 4.0,
                3 => 4.5,
                _ => 0
            };

            double bCurrent = bOc switch
            {
                0 => 3.0,
                1 => 3.5,
                2 => 4.0,
                3 => 4.5,
                _ => 0
            };

            double cCurrent = cOc switch
            {
                0 => 0.5,
                1 => 1.0,
                2 => 1.5,
                3 => 2.0,
                _ => 0
            };

            return $"SWA: {aCurrent:F1}A, SWB: {bCurrent:F1}A, SWC: {cCurrent:F1}A";
        }

        #endregion

        #region Soft Start Decoders

        public static string DecodeSoftStart1(byte value)
        {
            int time = (value >> 5) & 0x07;
            double ms = 1 + (time * 1); // 1ms to 14ms in 1ms steps
            return $"SWA: {ms:F0}ms";
        }

        public static string DecodeSoftStart2(byte value)
        {
            int bTime = (value >> 5) & 0x07;
            int cTime = (value >> 1) & 0x07;

            double bMs = 1 + (bTime * 1);
            double cMs = 1 + (cTime * 1);

            return $"SWB: {bMs:F0}ms, SWC: {cMs:F0}ms";
        }

        #endregion

        #region Temperature Decoders

        public static string DecodeOtpThreshold(byte value)
        {
            int threshold = value & 0x07;

            return threshold switch
            {
                0 => "105°C",
                1 => "115°C",
                2 => "125°C",
                3 => "135°C",
                4 => "145°C",
                _ => $"Threshold {threshold}"
            };
        }

        public static string DecodeTemperatureMeasurement(byte value)
        {
            int temp = (value >> 5) & 0x07;

            return temp switch
            {
                0 => "<80°C",
                1 => "85°C",
                2 => "95°C",
                3 => "105°C",
                4 => "115°C",
                5 => "125°C",
                6 => "135°C",
                7 => "≥140°C",
                _ => $"Temp {temp}"
            };
        }

        #endregion

        #region ADC Decoders

        public static string DecodeAdcRead(byte value)
        {
            // ADC reading: 15mV per step for SW/VDLO, 70mV per step for VIN_BULK
            double voltage = value * 0.015; // Default for SW/VDLO
            return $"{voltage:F2}V";
        }

        public static string DecodeAdcReadVinBulk(byte value)
        {
            double voltage = value * 0.070; // 70mV per step for VIN_BULK
            return $"{voltage:F1}V";
        }

        #endregion

        #region Status Flag Decoders

        public static string DecodeGlobalErrorLog(byte value)
        {
            var parts = new List<string>();

            if ((value & 0x80) != 0) parts.Add("Error Count > 1");
            if ((value & 0x40) != 0) parts.Add("Buck OV/UV Error");
            if ((value & 0x20) != 0) parts.Add("VIN Bulk OV");
            if ((value & 0x10) != 0) parts.Add("Critical Temp Error");

            return parts.Count > 0 ? string.Join(", ", parts) : "No Errors";
        }

        public static string DecodePmicStatus0(byte value)
        {
            var parts = new List<string>();

            if ((value & 0x40) != 0) parts.Add("Critical Temp Shutdown");

            // Power Good flags (active low)
            parts.Add((value & 0x20) != 0 ? "SWA: Not Good" : "SWA: ✓");
            parts.Add((value & 0x08) != 0 ? "SWB: Not Good" : "SWB: ✓");
            parts.Add((value & 0x04) != 0 ? "SWC: Not Good" : "SWC: ✓");

            if ((value & 0x01) != 0) parts.Add("VIN Bulk OV");

            return string.Join(", ", parts);
        }

        public static string DecodePmicStatus1(byte value)
        {
            var parts = new List<string>();

            if ((value & 0x80) != 0) parts.Add("High Temp Warning");
            parts.Add((value & 0x20) != 0 ? "VLDO1.8V: Not Good" : "VLDO1.8V: ✓");

            if ((value & 0x08) != 0) parts.Add("SWA High Current");
            if ((value & 0x02) != 0) parts.Add("SWB High Current");
            if ((value & 0x01) != 0) parts.Add("SWC High Current");

            return string.Join(", ", parts);
        }

        #endregion
    }
}