using PMICDumpParser.Models;
using System;
using System.Drawing;
using System.Linq;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Centralized register analysis and validation logic
    /// </summary>
    public static class RegisterAnalyzer
    {
        // Constants for critical change detection
        private const double CRITICAL_THRESHOLD_PERCENT = 23.0;
        private const double MAX_VOLTAGE_RANGE = 440.0; // Based on voltage register range

        /// <summary>
        /// Determines if a register change is critical (>23% change for voltage/current registers)
        /// </summary>
        /// <param name="reg">The register to check</param>
        /// <returns>True if the change is critical, false otherwise</returns>
        public static bool IsCriticalChange(ParsedRegister reg)
        {
            if (reg == null) return false;

            var name = reg.Name.ToUpper();
            bool isVoltageReg = name.Contains("VOLT") || name.Contains("SWA_VOLT") ||
                                name.Contains("SWB_VOLT") || name.Contains("SWC_VOLT");
            bool isCurrentReg = name.Contains("CURR") || name.Contains("SWA_CURR") ||
                                name.Contains("SWB_CURR") || name.Contains("SWC_CURR");

            if (!isVoltageReg && !isCurrentReg)
                return false;

            if (!reg.IsChanged)
                return false;

            double nominalValue = reg.DefaultValue;
            if (nominalValue <= 0)
                return false;

            double currentValue = reg.RawValue;
            double absoluteChange = Math.Abs(currentValue - nominalValue);
            double changePercent = (absoluteChange / MAX_VOLTAGE_RANGE) * 100.0;

            return changePercent > CRITICAL_THRESHOLD_PERCENT;
        }

        /// <summary>
        /// Determines if a register is protected based on address and definition
        /// </summary>
        public static bool IsProtected(ParsedRegister reg)
        {
            if (reg == null) return false;

            // Check if register is in protected DIMM vendor region (0x40-0x6F)
            bool inProtectedRange = reg.Address >= 0x40 && reg.Address <= 0x6F;

            // Check if definition marks it as protected
            bool definitionProtected = reg.Definition.Protected == true;

            return inProtectedRange || definitionProtected;
        }

        /// <summary>
        /// Determines if a register is reserved based on name and category
        /// </summary>
        public static bool IsReserved(ParsedRegister reg)
        {
            if (reg == null) return true;

            // Check category
            bool categoryReserved = reg.Category == "Reserved" ||
                                   reg.Category == "RESERVED" ||
                                   string.IsNullOrWhiteSpace(reg.Category);

            // Check name pattern
            bool nameReserved = reg.Name.StartsWith("RESERVED_", StringComparison.OrdinalIgnoreCase) ||
                               reg.Name.StartsWith("Reserved_", StringComparison.OrdinalIgnoreCase);

            // Check register type
            bool typeReserved = reg.Definition?.Type?.ToUpper() == "RV" ||
                               reg.Definition?.Type?.ToUpper() == "ROE" ||
                               string.IsNullOrWhiteSpace(reg.Definition?.Type);

            return categoryReserved || nameReserved || typeReserved;
        }

        /// <summary>
        /// Gets the appropriate color for a register based on its status
        /// </summary>
        public static Color GetStatusColor(ParsedRegister reg)
        {
            if (reg == null) return AppColors.DefaultGrid;

            bool isCritical = IsCriticalChange(reg);
            bool isProtected = IsProtected(reg);
            bool isReserved = IsReserved(reg);

            return AppColors.GetRegisterColor(reg.IsChanged, isProtected, isCritical, isReserved);
        }

        /// <summary>
        /// Parses a bit range string (e.g., "7:0", "5") into start and end bit positions
        /// </summary>
        public static (int start, int end) ParseBitRange(string bitRange)
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

        /// <summary>
        /// Extracts a field value from a byte given start and end bit positions
        /// </summary>
        public static int ExtractFieldValue(byte registerValue, int startBit, int endBit)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit); // Ensure start is higher
            }

            int bitCount = startBit - endBit + 1;
            int mask = ((1 << bitCount) - 1) << endBit;
            return (registerValue & mask) >> endBit;
        }

        /// <summary>
        /// Sets a field value in a byte given start and end bit positions
        /// </summary>
        public static byte SetFieldValue(byte registerValue, int startBit, int endBit, int fieldValue)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit); // Ensure start is higher
            }

            int bitCount = startBit - endBit + 1;
            int mask = ((1 << bitCount) - 1) << endBit;

            // Clear the bits first
            registerValue = (byte)(registerValue & ~mask);
            // Set the new value
            registerValue = (byte)(registerValue | ((fieldValue << endBit) & mask));

            return registerValue;
        }

        /// <summary>
        /// Validates if a value is within the valid range for a bit field
        /// </summary>
        public static bool ValidateFieldValue(int value, int startBit, int endBit)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit);
            }

            int bitCount = startBit - endBit + 1;
            int maxValue = (1 << bitCount) - 1;

            return value >= 0 && value <= maxValue;
        }

        /// <summary>
        /// Calculates the maximum value for a bit field
        /// </summary>
        public static int GetMaxFieldValue(int startBit, int endBit)
        {
            if (startBit < endBit)
            {
                (startBit, endBit) = (endBit, startBit);
            }

            int bitCount = startBit - endBit + 1;
            return (1 << bitCount) - 1;
        }
    }
}