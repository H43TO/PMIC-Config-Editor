using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PMICDumpParser.Models
{
    /// <summary>
    /// Represents a single bit field within a register
    /// </summary>
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

    /// <summary>
    /// Represents the definition of a PMIC register
    /// </summary>
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

    /// <summary>
    /// Represents the complete register map for a PMIC model
    /// </summary>
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

        /// <summary>
        /// Initializes the address lookup dictionary
        /// </summary>
        public void Initialize()
        {
            ByAddress = Registers.ToDictionary(r => r.AddressByte, r => r);
        }
    }
}