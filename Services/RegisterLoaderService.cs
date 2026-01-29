using Newtonsoft.Json;
using PMICDumpParser.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Service for loading and managing PMIC register definitions
    /// </summary>
    public static class RegisterLoaderService
    {
        private static RegisterMap? _map;
        private static readonly object _lock = new();

        /// <summary>
        /// Default register values for RTQ5132 PMIC
        /// </summary>
        private static readonly Dictionary<byte, (byte Default, string Type)> _defaults = new()
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

        /// <summary>
        /// Asynchronously loads register definitions from JSON file
        /// </summary>
        public static async Task<RegisterMap> LoadAsync()
        {
            if (_map != null) return _map;

            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "RegisterMap.json");

            if (!File.Exists(jsonPath))
            {
                _map = CreateDefaultMap();
                _map.Initialize();
                return _map;
            }

            try
            {
                var json = await FileHelper.ReadTextAsync(jsonPath);
                _map = JsonConvert.DeserializeObject<RegisterMap>(json) ?? CreateDefaultMap();
                _map.Initialize();
                return _map;
            }
            catch
            {
                _map = CreateDefaultMap();
                _map.Initialize();
                return _map;
            }
        }

        /// <summary>
        /// Gets the register map (thread-safe)
        /// </summary>
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

#if DEBUG
                            Console.WriteLine($"Loaded {_map.Registers.Count} registers from JSON");
                            Console.WriteLine($"Model: {_map.PmicModel}");
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

                    _map.Initialize();
                }
                return _map;
            }
        }

        /// <summary>
        /// Gets a specific register definition by address
        /// </summary>
        /// <param name="address">Register address (0x00-0xFF)</param>
        /// <returns>Register definition</returns>
        public static RegisterDef GetDefinition(byte address)
        {
            var map = GetMap();
            return map.ByAddress.TryGetValue(address, out var def) ? def : CreateDefaultRegister(address);
        }

        /// <summary>
        /// Resets the register map cache (for hot reloading)
        /// </summary>
        public static void ResetCache()
        {
            lock (_lock)
            {
                _map = null;
            }
        }

        #region Private Helper Methods

        private static RegisterMap CreateDefaultMap()
        {
            var map = new RegisterMap
            {
                Version = "1.0",
                PmicModel = "RTQ5132",
                Registers = new List<RegisterDef>(256)
            };

            // Create definitions for all 256 possible addresses
            for (byte addr = 0; addr < 256; addr++)
            {
                map.Registers.Add(CreateDefaultRegister(addr));
            }

            return map;
        }

        private static RegisterDef CreateDefaultRegister(byte address)
        {
            var def = new RegisterDef
            {
                Address = $"0x{address:X2}",
                Name = $"RESERVED_{address:X2}",
                FullName = $"Reserved 0x{address:X2}",
                Category = "Reserved",
                Type = "RV",
                Description = $"Reserved register 0x{address:X2}"
            };

            // Apply known defaults if available
            if (_defaults.TryGetValue(address, out var info))
            {
                def.Default = $"0x{info.Default:X2}";
                def.Type = info.Type;
            }
            else
            {
                def.Default = "0x00";
            }

            // Mark DIMM vendor region as protected (0x40-0x6F)
            if (address >= 0x40 && address <= 0x6F)
            {
                def.Protected = true;
            }

            return def;
        }

        #endregion
    }
}