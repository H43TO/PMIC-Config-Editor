using PMICDumpParser.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Service for parsing PMIC dump files into structured data
    /// </summary>
    public static class DumpParserService
    {
        /// <summary>
        /// Parses a PMIC dump file asynchronously
        /// </summary>
        /// <param name="filePath">Path to the dump file</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>Parsed PMIC dump object</returns>
        public static async Task<PmicDump> ParseAsync(string filePath, IProgress<string>? progress = null)
        {
            progress?.Report("Reading dump file...");

            var dump = new PmicDump
            {
                FilePath = filePath,
                LoadTime = DateTime.Now,
                RawData = await FileHelper.ReadBytesAsync(filePath)
            };

            ValidateDumpSize(dump);

            // Parse registers in parallel for better performance
            await Task.Run(() =>
            {
                var minLength = Math.Min(dump.RawData.Length, 256);
                var options = new System.Threading.Tasks.ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                System.Threading.Tasks.Parallel.For(0, minLength, options, i =>
                {
                    ParseSingleRegister(dump, (byte)i);
                });
            });

            return dump;
        }

        /// <summary>
        /// Generates a comprehensive text report from a parsed dump
        /// </summary>
        /// <param name="dump">Parsed PMIC dump</param>
        /// <returns>Formatted report string</returns>
        public static string GenerateReport(PmicDump dump)
        {
            var sb = new System.Text.StringBuilder(4096);
            AppendReportHeader(sb, dump);
            AppendCategorySummary(sb, dump);
            AppendChangedRegisters(sb, dump);

            return sb.ToString();
        }

        #region Private Methods

        private static void ValidateDumpSize(PmicDump dump)
        {
            if (dump.RawData.Length != 256)
            {
#if DEBUG
                System.Windows.Forms.MessageBox.Show($"Expected 256 bytes, got {dump.RawData.Length} bytes",
                              "Dump Size Warning", System.Windows.Forms.MessageBoxButtons.OK,
                              System.Windows.Forms.MessageBoxIcon.Warning);
#endif
                // Resize if smaller than expected
                if (dump.RawData.Length < 256)
                {
                    var newArray = new byte[256];
                    Array.Copy(dump.RawData, newArray, dump.RawData.Length);
                    dump.RawData = newArray;
                }
            }
        }

        private static void ParseSingleRegister(PmicDump dump, byte address)
        {
            var def = RegisterLoaderService.GetDefinition(address);
            var parsed = new ParsedRegister
            {
                Address = address,
                RawValue = dump.RawData[address],
                DefaultValue = def.DefaultByte,
                Name = def.Name,
                FullName = def.FullName,
                Category = def.Category,
                Description = def.Description,
                Definition = def
            };

            // Extract individual bit states
            for (int bit = 0; bit < 8; bit++)
            {
                parsed.BitStates[bit] = ((dump.RawData[address] >> bit) & 1) == 1;
            }

            // Decode the register value
            parsed.DecodedValue = RegisterDecoder.DecodeRegister(parsed);

            lock (dump.Registers)
            {
                dump.Registers[address] = parsed;
            }
        }

        private static void AppendReportHeader(System.Text.StringBuilder sb, PmicDump dump)
        {
            sb.AppendLine("PMIC Dump Analysis Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"File: {System.IO.Path.GetFileName(dump.FilePath)}");
            sb.AppendLine($"Model: {RegisterLoaderService.GetMap().PmicModel}");
            sb.AppendLine($"Total Registers: {dump.Registers.Count}");
            sb.AppendLine($"Changed: {dump.Changed.Count}");
            sb.AppendLine($"Protected: {dump.Protected.Count}");
            sb.AppendLine();
        }

        private static void AppendCategorySummary(System.Text.StringBuilder sb, PmicDump dump)
        {
            foreach (var category in dump.ByCategory.OrderBy(g => g.Key))
            {
                int changed = category.Value.Count(r => r.IsChanged);
                sb.AppendLine($"{category.Key}: {category.Value.Count} ({changed} changed)");
            }
        }

        private static void AppendChangedRegisters(System.Text.StringBuilder sb, PmicDump dump)
        {
            sb.AppendLine("\n=== CHANGED REGISTERS ===");
            foreach (var reg in dump.Changed.OrderBy(r => r.Address))
            {
                sb.AppendLine($"{reg.AddrHex}: {reg.Name}");
                sb.AppendLine($"  Current: {reg.ValHex} ({reg.DecodedValue})");
                sb.AppendLine($"  Default: {reg.DefaultHex}");
                sb.AppendLine();
            }
        }

        #endregion
    }
}