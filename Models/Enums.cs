namespace PMICDumpParser.Models
{
    /// <summary>
    /// Register access types from PMIC datasheet
    /// </summary>
    public enum RegType
    {
        RO,     // Read Only
        ROE,    // Read Only Error
        RW,     // Read Write
        RWPE,   // Read Write Protected
        W,      // Write Only
        W1O,    // Write 1 to Clear
        RV      // Reserved
    }

    /// <summary>
    /// Field types for decoding register values
    /// </summary>
    public enum FieldType
    {
        Reserved,
        Flag,
        Enum,
        Raw,
        Bin,
        Dec,
        Volt,
        Curr,
        Pwr,
        Time,
        Freq,
        Temp
    }

    /// <summary>
    /// Categories for organizing registers
    /// </summary>
    public enum RegCategory
    {
        Reserved,
        Status,
        Error,
        Voltage,
        Current,
        Power,
        Threshold,
        Switching,
        Sequence,
        Config,
        Measurement,
        Mask,
        Clear,
        Password,
        LDO,
        Temperature,
        ADC,
        Interface,
        Vendor,
        ID,
        Timing
    }
}