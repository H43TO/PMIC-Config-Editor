namespace PMICDumpParser.Models
{
    // Register Types (from Excel)
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

    // Field Types for decoding
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

    // Register Categories
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