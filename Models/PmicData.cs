using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PMICDumpParser.Models
{
    #region Compact Data Models

    public class BitField
    {
        [JsonProperty("bits")]
        public string Bits { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("desc")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("type")]
        public FieldType Type { get; set; } = FieldType.Reserved;

        [JsonProperty("active", NullValueHandling = NullValueHandling.Ignore)]
        public bool? ActiveHigh { get; set; }

        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string>? EnumValues { get; set; }
    }

    public class RegisterDef
    {
        [JsonProperty("addr")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("full")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("cat")]
        public string Category { get; set; } = "Reserved";

        [JsonProperty("default")]
        public string Default { get; set; } = "0x00";

        [JsonProperty("type")]
        public string Type { get; set; } = "RV";

        [JsonProperty("desc")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("fields")]
        public List<BitField> Fields { get; set; } = new();

        [JsonProperty("prot", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Protected { get; set; }

        [JsonProperty("special", NullValueHandling = NullValueHandling.Ignore)]
        public string? SpecialDecode { get; set; }

        [JsonIgnore]
        public byte AddressByte
        {
            get
            {
                if (string.IsNullOrEmpty(Address)) return 0;
                string hex = Address.Replace("0x", "").Replace("0X", "");
                return byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            }
        }

        [JsonIgnore]
        public byte DefaultByte
        {
            get
            {
                if (string.IsNullOrEmpty(Default))
                    return 0;

                string hexValue = Default.Trim();
                if (hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hexValue = hexValue.Substring(2);

                if (string.IsNullOrEmpty(hexValue))
                    return 0;

                if (byte.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte result))
                {
                    return result;
                }

#if DEBUG
                Console.WriteLine($"Invalid hex value '{Default}' for register {Address}");
#endif
                return 0;
            }
        }
    }

    public class RegisterMap
    {
        [JsonProperty("ver")]
        public string Version { get; set; } = "1.0";

        [JsonProperty("model")]
        public string PmicModel { get; set; } = "RTQ5132";

        [JsonProperty("regs")]
        public List<RegisterDef> Registers { get; set; } = new();

        [JsonIgnore]
        public Dictionary<byte, RegisterDef> ByAddress { get; private set; } = new();

        public void Init()
        {
            ByAddress = Registers.ToDictionary(r => r.AddressByte, r => r);
        }
    }

    public class ParsedRegister
    {
        public byte Address { get; set; }
        public byte RawValue { get; set; }
        public byte DefaultValue { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DecodedValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RegisterDef Definition { get; set; } = null!;
        public Dictionary<int, bool> BitStates { get; } = new();
        public bool IsChanged => RawValue != DefaultValue;

        public string AddrHex => $"0x{Address:X2}";
        public string ValHex => $"0x{RawValue:X2}";
        public string DefaultHex => $"0x{DefaultValue:X2}";
    }

    public class PmicDump
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime LoadTime { get; set; }
        public byte[] RawData { get; set; } = new byte[256];
        public Dictionary<byte, ParsedRegister> Registers { get; } = new();

        public List<ParsedRegister> Changed => Registers.Values
            .Where(r => r.IsChanged)
            .ToList();

        public List<ParsedRegister> Protected => Registers.Values
            .Where(r => r.Definition.Protected == true || (r.Address >= 0x40 && r.Address <= 0x6F))
            .ToList();

        public Dictionary<string, List<ParsedRegister>> ByCategory =>
            Registers.Values
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Address).ToList());
    }

    #endregion

    #region Register Decoder

    public static class RegisterDecoder
    {
        public static string DecodeRegister(ParsedRegister reg)
        {
            if (!string.IsNullOrEmpty(reg.Definition.SpecialDecode))
            {
                return DecodeSpecial(reg);
            }

            return DecodeStandard(reg);
        }

        private static string DecodeSpecial(ParsedRegister reg)
        {
            return reg.Definition.SpecialDecode switch
            {
                "SwaVoltage" => RegisterDecode.DecodeSwaSwbVoltage(reg.RawValue),
                "SwbVoltage" => RegisterDecode.DecodeSwaSwbVoltage(reg.RawValue),
                "SwcVoltage" => RegisterDecode.DecodeSwcVoltage(reg.RawValue),
                "SwaCurrent" => RegisterDecode.DecodeSwaCurrent(reg.RawValue),
                "SwbCurrent" => RegisterDecode.DecodeSwbSwcCurrent(reg.RawValue),
                "SwcCurrent" => RegisterDecode.DecodeSwbSwcCurrent(reg.RawValue),
                "SwaThreshold" => RegisterDecode.DecodeSwaSwbThreshold(reg.RawValue),
                "SwbThreshold" => RegisterDecode.DecodeSwaSwbThreshold(reg.RawValue),
                "SwcThreshold" => RegisterDecode.DecodeSwcThreshold(reg.RawValue),
                "FswMode1" => RegisterDecode.DecodeFswMode1(reg.RawValue),
                "FswMode2" => RegisterDecode.DecodeFswMode2(reg.RawValue),
                "LdoVoltage" => RegisterDecode.DecodeLdoVoltage(reg.RawValue),
                "OcThreshold" => RegisterDecode.DecodeOcThreshold(reg.RawValue),
                "SoftStart1" => RegisterDecode.DecodeSoftStart1(reg.RawValue),
                "SoftStart2" => RegisterDecode.DecodeSoftStart2(reg.RawValue),
                "OtpThreshold" => RegisterDecode.DecodeOtpThreshold(reg.RawValue),
                "GlobalErrLog" => RegisterDecode.DecodeGlobalErrorLog(reg.RawValue),
                "PmicStat0" => RegisterDecode.DecodePmicStatus0(reg.RawValue),
                "PmicStat1" => RegisterDecode.DecodePmicStatus1(reg.RawValue),
                "AdcRead" => RegisterDecode.DecodeAdcRead(reg.RawValue),
                _ => DecodeStandard(reg)
            };
        }

        private static string DecodeStandard(ParsedRegister reg)
        {
            if (reg.Definition.Fields == null || !reg.Definition.Fields.Any())
            {
                return $"0x{reg.RawValue:X2}";
            }

            var results = new List<string>();

            foreach (var field in reg.Definition.Fields)
            {
                if (field.Type == FieldType.Reserved)
                    continue;

                var (startBit, endBit) = ParseBitRange(field.Bits);
                int fieldValue = ExtractFieldValue(reg.RawValue, startBit, endBit);

                // Use the new field decoder
                string decoded = RegisterDecode.DecodeField(field, fieldValue, reg.Name);
                results.Add($"{field.Name}: {decoded}");
            }

            return results.Count > 0 ? string.Join("; ", results) : $"0x{reg.RawValue:X2}";
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
                // Swap if parameters are reversed
                (startBit, endBit) = (endBit, startBit);
            }

            int bitCount = startBit - endBit + 1;
            int mask = ((1 << bitCount) - 1) << endBit;
            return (registerValue & mask) >> endBit;
        }

        private static string DecodeFlag(BitField field, int value)
        {
            bool isSet = value == 1;
            bool isActive = (isSet && field.ActiveHigh == true) || (!isSet && field.ActiveHigh == false);
            return isActive ? field.Name : $"{field.Name} (Inactive)";
        }

        private static string DecodeEnum(BitField field, int value)
        {
            if (field.EnumValues?.TryGetValue(value.ToString(), out var text) == true)
            {
                return text;
            }
            return $"{value}";
        }
    }

    #endregion

    #region File Helpers

    public static class FileHelper
    {
        public static async Task<string> ReadTextAsync(string path) =>
            await Task.Run(() => File.ReadAllText(path));

        public static async Task<byte[]> ReadBytesAsync(string path) =>
            await Task.Run(() => File.ReadAllBytes(path));

        public static async Task WriteTextAsync(string path, string text) =>
            await Task.Run(() => File.WriteAllText(path, text));
    }

    #endregion

    #region Register Loader

    public static class RegisterDefaults
    {
        public static readonly Dictionary<byte, (byte Default, string Type)> Defaults = new()
        {
            {0x15, (0x2C, "RW")}, {0x16, (0x20, "RW")}, {0x19, (0x04, "RW")},
            {0x1B, (0x05, "RW")}, {0x1C, (0x60, "RW")}, {0x1E, (0x60, "RW")},
            {0x1F, (0x60, "RW")}, {0x20, (0xCF, "RW")}, {0x21, (0x78, "RW")},
            {0x22, (0x63, "RW")}, {0x25, (0x78, "RW")}, {0x26, (0x63, "RW")},
            {0x27, (0x78, "RW")}, {0x28, (0x63, "RW")}, {0x29, (0x80, "RW")},
            {0x2A, (0x88, "RW")}, {0x2B, (0x42, "RW")}, {0x2C, (0x20, "RW")},
            {0x2D, (0x22, "RW")}, {0x2E, (0x04, "RW")}, {0x2F, (0x06, "RW")},
            {0x34, (0x0E, "RO")}, {0x3C, (0x8A, "ROE")}, {0x3D, (0x8C, "ROE")},
            {0x40, (0x89, "RWPE")}, {0x41, (0xD9, "RWPE")}, {0x45, (0x78, "RWPE")},
            {0x46, (0x63, "RWPE")}, {0x49, (0x78, "RWPE")}, {0x4A, (0x63, "RWPE")},
            {0x4B, (0x78, "RWPE")}, {0x4C, (0x63, "RWPE")}, {0x4D, (0x80, "RWPE")},
            {0x4E, (0x88, "RWPE")}, {0x50, (0xCF, "RWPE")}, {0x51, (0x42, "RWPE")},
            {0x58, (0xD1, "RWPE")}, {0x59, (0xD9, "RWPE")}, {0x5D, (0x20, "RWPE")},
            {0x5E, (0x22, "RWPE")}
        };
    }

    public static class RegisterLoader
    {
        private static RegisterMap? _map;
        private static readonly object _lock = new();

        public static async Task<RegisterMap> LoadAsync()
        {
            if (_map != null) return _map;

            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "RegisterMap.json");

            if (!File.Exists(jsonPath))
            {
                _map = CreateDefaultMap();
                _map.Init();
                return _map;
            }

            try
            {
                var json = await FileHelper.ReadTextAsync(jsonPath);
                _map = JsonConvert.DeserializeObject<RegisterMap>(json) ?? CreateDefaultMap();
                _map.Init();
                return _map;
            }
            catch
            {
                _map = CreateDefaultMap();
                _map.Init();
                return _map;
            }
        }

        public static RegisterMap GetMap()
        {
            lock (_lock)
            {
                if (_map == null)
                {
                    var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "RegisterMap.json");

                    if (!File.Exists(jsonPath))
                    {
#if DEBUG
                        MessageBox.Show($"JSON file not found: {jsonPath}", "Warning",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
#endif
                        _map = CreateDefaultMap();
                    }
                    else
                    {
                        try
                        {
                            var json = File.ReadAllText(jsonPath);
                            _map = JsonConvert.DeserializeObject<RegisterMap>(json) ?? CreateDefaultMap();

                            // Debug output
#if DEBUG
                            Console.WriteLine($"Loaded {_map.Registers.Count} registers from JSON");
                            Console.WriteLine($"Model: {_map.PmicModel}");

                            // Show first few registers
                            foreach (var reg in _map.Registers.Take(5))
                            {
                                Console.WriteLine($"  {reg.Address}: {reg.Name}");
                            }
#endif
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            MessageBox.Show($"Error loading JSON: {ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
                            _map = CreateDefaultMap();
                        }
                    }

                    _map.Init();
                }
                return _map;
            }
        }

        private static RegisterMap CreateDefaultMap()
        {
            var map = new RegisterMap
            {
                Version = "1.0",
                PmicModel = "RTQ5132",
                Registers = new List<RegisterDef>(256)
            };

            for (byte addr = 0; addr < 256; addr++)
            {
                map.Registers.Add(CreateDefaultReg(addr));
            }

            return map;
        }

        private static RegisterDef CreateDefaultReg(byte addr)
        {
            var def = new RegisterDef
            {
                Address = $"0x{addr:X2}",
                Name = $"RESERVED_{addr:X2}",
                FullName = $"Reserved 0x{addr:X2}",
                Category = "Reserved",
                Type = "RV",
                Description = $"Reserved register 0x{addr:X2}"
            };

            if (RegisterDefaults.Defaults.TryGetValue(addr, out var info))
            {
                def.Default = $"0x{info.Default:X2}";
                def.Type = info.Type;
            }
            else
            {
                def.Default = "0x00";
            }

            // Mark DIMM vendor region as protected
            if (addr >= 0x40 && addr <= 0x6F)
            {
                def.Protected = true;
            }

            return def;
        }

        public static RegisterDef GetDef(byte addr)
        {
            var map = GetMap();
            return map.ByAddress.TryGetValue(addr, out var def) ? def : CreateDefaultReg(addr);
        }
    }

    #endregion

    #region Dump Parser

    public static class DumpParser
    {
        public static async Task<PmicDump> ParseAsync(string filePath, IProgress<string>? progress = null)
        {
            progress?.Report("Reading dump file...");

            var dump = new PmicDump
            {
                FilePath = filePath,
                LoadTime = DateTime.Now,
                RawData = await FileHelper.ReadBytesAsync(filePath)
            };

            if (dump.RawData.Length != 256)
            {
#if DEBUG
                MessageBox.Show($"Expected 256 bytes, got {dump.RawData.Length} bytes",
                              "Dump Size Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
#endif
                // Resize if needed
                if (dump.RawData.Length < 256)
                {
                    // Create a new array and copy the data
                    var newArray = new byte[256];
                    Array.Copy(dump.RawData, newArray, dump.RawData.Length);
                    dump.RawData = newArray;
                }
            }

            // Parse registers in parallel for better performance
            await Task.Run(() =>
            {
                Parallel.For(0, Math.Min(dump.RawData.Length, 256), new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, i =>
                {
                    ParseRegister(dump, (byte)i);
                });
            });

            return dump;
        }

        private static void ParseRegister(PmicDump dump, byte addr)
        {
            var def = RegisterLoader.GetDef(addr);
            var parsed = new ParsedRegister
            {
                Address = addr,
                RawValue = dump.RawData[addr],
                DefaultValue = def.DefaultByte,
                Name = def.Name,
                FullName = def.FullName,
                Category = def.Category,
                Description = def.Description,
                Definition = def
            };

            // Extract bit states
            for (int bit = 0; bit < 8; bit++)
            {
                parsed.BitStates[bit] = ((dump.RawData[addr] >> bit) & 1) == 1;
            }

            // Decode value
            parsed.DecodedValue = RegisterDecoder.DecodeRegister(parsed);

            lock (dump.Registers)
            {
                dump.Registers[addr] = parsed;
            }
        }

        public static string GenerateReport(PmicDump dump)
        {
            var sb = new System.Text.StringBuilder(4096);
            sb.AppendLine("PMIC Dump Analysis Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"File: {Path.GetFileName(dump.FilePath)}");
            sb.AppendLine($"Model: {RegisterLoader.GetMap().PmicModel}");
            sb.AppendLine($"Total Registers: {dump.Registers.Count}");
            sb.AppendLine($"Changed: {dump.Changed.Count}");
            sb.AppendLine($"Protected: {dump.Protected.Count}");
            sb.AppendLine();

            // Summary
            foreach (var cat in dump.ByCategory.OrderBy(g => g.Key))
            {
                int changed = cat.Value.Count(r => r.IsChanged);
                sb.AppendLine($"{cat.Key}: {cat.Value.Count} ({changed} changed)");
            }

            // Changed registers
            sb.AppendLine("\n=== CHANGED REGISTERS ===");
            foreach (var reg in dump.Changed.OrderBy(r => r.Address))
            {
                sb.AppendLine($"{reg.AddrHex}: {reg.Name}");
                sb.AppendLine($"  Current: {reg.ValHex} ({reg.DecodedValue})");
                sb.AppendLine($"  Default: {reg.DefaultHex}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    #endregion
}