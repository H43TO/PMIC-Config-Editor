using System.Collections.Generic;

namespace PMICDumpParser.Models
{
    /// <summary>
    /// Represents a fully parsed PMIC register with decoded values
    /// </summary>
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

        /// <summary>
        /// Indicates if the register value differs from its default
        /// </summary>
        public bool IsChanged => RawValue != DefaultValue;

        /// <summary>
        /// Gets address in hexadecimal format (0xXX)
        /// </summary>
        public string AddrHex => $"0x{Address:X2}";

        /// <summary>
        /// Gets current value in hexadecimal format (0xXX)
        /// </summary>
        public string ValHex => $"0x{RawValue:X2}";

        /// <summary>
        /// Gets default value in hexadecimal format (0xXX)
        /// </summary>
        public string DefaultHex => $"0x{DefaultValue:X2}";
    }
}