using PMICDumpParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Service for decoding PMIC register values into human-readable formats
    /// </summary>
    public static class RegisterDecoder
    {
        /// <summary>
        /// Decodes a complete register into human-readable format
        /// </summary>
        /// <param name="register">The register to decode</param>
        /// <returns>Decoded string representation</returns>
        public static string DecodeRegister(ParsedRegister register)
        {
            // Check for special decoding rules
            if (!string.IsNullOrEmpty(register.Definition.SpecialDecode))
            {
                return DecodeSpecialRegister(register);
            }

            return DecodeStandardRegister(register);
        }

        /// <summary>
        /// Decodes an individual field within a register
        /// </summary>
        /// <param name="field">Field definition</param>
        /// <param name="fieldValue">Raw field value</param>
        /// <param name="registerName">Optional register name for context</param>
        /// <returns>Decoded field value</returns>
        public static string DecodeField(BitField field, int fieldValue, string registerName = "")
        {
            if (field.Type == FieldType.Reserved)
                return $"0x{fieldValue:X} ({fieldValue})";

            // Use enum values if defined
            if (field.EnumValues != null && field.EnumValues.TryGetValue(fieldValue.ToString(), out var enumText))
            {
                return $"{enumText} ({fieldValue})";
            }

            // Apply type-specific decoding
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
                _ => fieldValue.ToString()
            };
        }

        #region Voltage Decoding Methods

        public static string DecodeSwaSwbVoltage(byte value)
        {
            int voltageSetting = (value >> 1) & 0x7F; // Bits 7:1
            int pglSetting = value & 0x01; // Bit 0

            double voltageMv = 800 + (voltageSetting * 5);
            double voltage = voltageMv / 1000;
            double pglPercent = pglSetting == 0 ? 5.0 : 7.5;

            return $"{voltage:F3}V, PGL: -{pglPercent}%";
        }

        public static string DecodeSwcVoltage(byte value)
        {
            int voltageSetting = (value >> 1) & 0x7F;
            int pglSetting = value & 0x01;

            double voltageMv = 1500 + (voltageSetting * 5);
            double voltage = voltageMv / 1000;
            double pglPercent = pglSetting == 0 ? 5.0 : 7.5;

            return $"{voltage:F3}V, PGL: -{pglPercent}%";
        }

        #endregion

        #region Current & Power Decoding Methods

        public static string DecodeSwaCurrent(byte value)
        {
            double current = value * 0.125;
            return $"Current: {current:F3}A";
        }

        public static string DecodeSwbSwcCurrent(byte value)
        {
            int measurement = value & 0x3F;
            double current = measurement * 0.125;
            return $"Current: {current:F3}A";
        }

        #endregion

        #region Threshold Decoding Methods

        public static string DecodeSwaSwbThreshold(byte value)
        {
            int pgh = (value >> 6) & 0x03;
            int ov = (value >> 4) & 0x03;
            int uvlo = (value >> 2) & 0x03;
            int sst = value & 0x03;

            return $"PGH: {GetPghString(pgh)}, OV: {GetOvString(ov)}, UVLO: {GetUvloString(uvlo)}, SST: {GetSstString(sst, false)}";
        }

        public static string DecodeSwcThreshold(byte value)
        {
            int pgh = (value >> 6) & 0x03;
            int ov = (value >> 4) & 0x03;
            int uvlo = (value >> 2) & 0x03;
            int sst = value & 0x03;

            return $"PGH: {GetPghString(pgh)}, OV: {GetOvString(ov)}, UVLO: {GetUvloString(uvlo)}, SST: {GetSstString(sst, true)}";
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
            double ms = 1 + (time * 1);
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
            double voltage = value * 0.015;
            return $"{voltage:F2}V";
        }

        public static string DecodeAdcReadVinBulk(byte value)
        {
            double voltage = value * 0.070;
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

        #region Private Helper Methods

        private static string DecodeSpecialRegister(ParsedRegister register)
        {
            return register.Definition.SpecialDecode switch
            {
                "SwaVoltage" => DecodeSwaSwbVoltage(register.RawValue),
                "SwbVoltage" => DecodeSwaSwbVoltage(register.RawValue),
                "SwcVoltage" => DecodeSwcVoltage(register.RawValue),
                "SwaCurrent" => DecodeSwaCurrent(register.RawValue),
                "SwbCurrent" => DecodeSwbSwcCurrent(register.RawValue),
                "SwcCurrent" => DecodeSwbSwcCurrent(register.RawValue),
                "SwaThreshold" => DecodeSwaSwbThreshold(register.RawValue),
                "SwbThreshold" => DecodeSwaSwbThreshold(register.RawValue),
                "SwcThreshold" => DecodeSwcThreshold(register.RawValue),
                "FswMode1" => DecodeFswMode1(register.RawValue),
                "FswMode2" => DecodeFswMode2(register.RawValue),
                "LdoVoltage" => DecodeLdoVoltage(register.RawValue),
                "OcThreshold" => DecodeOcThreshold(register.RawValue),
                "SoftStart1" => DecodeSoftStart1(register.RawValue),
                "SoftStart2" => DecodeSoftStart2(register.RawValue),
                "OtpThreshold" => DecodeOtpThreshold(register.RawValue),
                "GlobalErrLog" => DecodeGlobalErrorLog(register.RawValue),
                "PmicStat0" => DecodePmicStatus0(register.RawValue),
                "PmicStat1" => DecodePmicStatus1(register.RawValue),
                "AdcRead" => DecodeAdcRead(register.RawValue),
                _ => DecodeStandardRegister(register)
            };
        }

        private static string DecodeStandardRegister(ParsedRegister register)
        {
            if (register.Definition.Fields == null || !register.Definition.Fields.Any())
            {
                return $"0x{register.RawValue:X2}";
            }

            var results = new List<string>();

            foreach (var field in register.Definition.Fields)
            {
                if (field.Type == FieldType.Reserved)
                    continue;

                var (startBit, endBit) = ParseBitRange(field.Bits);
                int fieldValue = ExtractFieldValue(register.RawValue, startBit, endBit);

                string decoded = DecodeField(field, fieldValue, register.Name);
                results.Add($"{field.Name}: {decoded}");
            }

            return results.Count > 0 ? string.Join("; ", results) : $"0x{register.RawValue:X2}";
        }

        private static string DecodeVoltageField(BitField field, int fieldValue, string registerName)
        {
            if (registerName.Contains("SWA_Voltage") || registerName.Contains("SWB_Voltage"))
            {
                double voltageMv = 800 + (fieldValue * 5);
                return $"{voltageMv / 1000:F3}V ({voltageMv}mV)";
            }
            else if (registerName.Contains("SWC_Voltage"))
            {
                double voltageMv = 1500 + (fieldValue * 5);
                return $"{voltageMv / 1000:F3}V ({voltageMv}mV)";
            }
            else if (field.Name.Contains("PGL"))
            {
                return fieldValue == 0 ? "-5%" : "-7.5%";
            }
            else if (field.Name.Contains("PGH"))
            {
                return GetPghString(fieldValue);
            }
            else if (field.Name.Contains("OV"))
            {
                return GetOvString(fieldValue);
            }
            else if (field.Name.Contains("UVLO"))
            {
                return GetUvloString(fieldValue);
            }
            else
            {
                return $"{fieldValue} (Voltage setting)";
            }
        }

        private static string DecodeCurrentField(BitField field, int fieldValue, string registerName)
        {
            if (field.Name.Contains("HIGH_CURRENT") || field.Name.Contains("CONSUMPTION"))
            {
                double current = fieldValue * 0.125;
                return $"{current:F3}A";
            }
            else if (field.Name.Contains("A_OC"))
            {
                return GetAOcString(fieldValue);
            }
            else if (field.Name.Contains("B_OC"))
            {
                return GetBOcString(fieldValue);
            }
            else if (field.Name.Contains("C_OC"))
            {
                return GetCOcString(fieldValue);
            }
            else
            {
                return $"{fieldValue} (Current setting)";
            }
        }

        private static string DecodeTemperatureField(BitField field, int fieldValue, string registerName)
        {
            if (field.Name.Contains("HTW") || field.Name.Contains("TEMP_MEAS"))
            {
                return GetTemperatureString(fieldValue);
            }
            else if (field.Name.Contains("OTP"))
            {
                return GetOtpString(fieldValue);
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
                return GetSoftStopTimeString(fieldValue);
            }
            else if (field.Name.Contains("SST"))
            {
                if (registerName.Contains("SWA") || registerName.Contains("SWB"))
                {
                    return GetSstString(fieldValue, false);
                }
                else if (registerName.Contains("SWC"))
                {
                    return GetSstString(fieldValue, true);
                }
            }
            else if (field.Name.Contains("SOFT_START"))
            {
                double ms = 1 + (fieldValue * 1);
                return $"{ms:F0}ms";
            }

            return $"{fieldValue}ms";
        }

        private static string DecodeFrequencyField(BitField field, int fieldValue)
        {
            return GetFrequencyString(fieldValue);
        }

        private static string DecodeFlagField(BitField field, int fieldValue)
        {
            bool isSet = fieldValue == 1;
            bool isActive = (isSet && field.ActiveHigh == true) || (!isSet && field.ActiveHigh == false);
            return isActive ? "Active" : "Inactive";
        }

        private static int GetBitWidth(string bits)
        {
            var parts = bits.Split(':');
            if (parts.Length == 1)
                return 1;
            else
                return Math.Abs(int.Parse(parts[0]) - int.Parse(parts[1])) + 1;
        }

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

        #region String Helper Methods

        private static string GetPghString(int value) => value switch
        {
            0 => "+5% from setting",
            1 => "+7.5% from setting",
            2 => "+10% from setting",
            3 => "+2.5% from setting",
            _ => $"{value}"
        };

        private static string GetOvString(int value) => value switch
        {
            0 => "+7.5% from setting",
            1 => "+10% from setting",
            2 => "+12.5% from setting",
            3 => "+5% from setting",
            _ => $"{value}"
        };

        private static string GetUvloString(int value) => value switch
        {
            0 => "-10% from setting",
            1 => "-12.5% from setting",
            2 => "-5% from setting",
            3 => "-7.5% from setting",
            _ => $"{value}"
        };

        private static string GetSstString(int value, bool isSwc) => (isSwc, value) switch
        {
            (false, 0) => "0.5ms",
            (false, 1) => "1ms",
            (false, 2) => "2ms",
            (false, 3) => "4ms",
            (true, 0) => "1ms",
            (true, 1) => "2ms",
            (true, 2) => "4ms",
            (true, 3) => "8ms",
            _ => $"{value}"
        };

        private static string GetAOcString(int value) => value switch
        {
            0 => "3.0A",
            1 => "3.5A",
            2 => "4.0A",
            3 => "4.5A",
            _ => $"{value}"
        };

        private static string GetBOcString(int value) => value switch
        {
            0 => "3.0A",
            1 => "3.5A",
            2 => "4.0A",
            3 => "4.5A",
            _ => $"{value}"
        };

        private static string GetCOcString(int value) => value switch
        {
            0 => "0.5A",
            1 => "1.0A",
            2 => "1.5A",
            3 => "2.0A",
            _ => $"{value}"
        };

        private static string GetTemperatureString(int value) => value switch
        {
            0 => "< 80°C (±5°C)",
            1 => "85°C (±5°C)",
            2 => "95°C (±5°C)",
            3 => "105°C (±5°C)",
            4 => "115°C (±5°C)",
            5 => "125°C (±5°C)",
            6 => "135°C (±5°C)",
            7 => "≥ 140°C (±5°C)",
            _ => $"{value}"
        };

        private static string GetOtpString(int value) => value switch
        {
            0 => "105°C",
            1 => "115°C",
            2 => "125°C",
            3 => "135°C",
            4 => "145°C",
            _ => $"{value}"
        };

        private static string GetSoftStopTimeString(int value) => value switch
        {
            0 => "0.5ms",
            1 => "1ms",
            2 => "2ms",
            3 => "4ms",
            _ => $"{value}"
        };

        private static string GetFrequencyString(int value) => value switch
        {
            0 => "750kHz",
            1 => "1000kHz",
            2 => "1250kHz",
            3 => "1500kHz",
            _ => $"{value}"
        };

        #endregion

        #endregion
    }
}