using System;
using System.Collections.Generic;
using System.Linq;

namespace PMICDumpParser.Models
{
    /// <summary>
    /// Represents a parsed PMIC dump with register data and metadata
    /// </summary>
    public class PmicDump
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime LoadTime { get; set; }
        public byte[] RawData { get; set; } = new byte[256];
        public Dictionary<byte, ParsedRegister> Registers { get; } = new();

        /// <summary>
        /// Gets all registers that have changed from their default values
        /// </summary>
        public List<ParsedRegister> Changed => Registers.Values
            .Where(r => r.IsChanged)
            .ToList();

        /// <summary>
        /// Gets all protected registers (addresses 0x40-0x6F or marked as protected)
        /// </summary>
        public List<ParsedRegister> Protected => Registers.Values
            .Where(r => r.Definition.Protected == true || (r.Address >= 0x40 && r.Address <= 0x6F))
            .ToList();

        /// <summary>
        /// Groups registers by their category for organized display
        /// </summary>
        public Dictionary<string, List<ParsedRegister>> ByCategory =>
            Registers.Values
                .GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Address).ToList());
    }
}