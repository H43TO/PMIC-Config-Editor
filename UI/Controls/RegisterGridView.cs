using PMICDumpParser.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PMICDumpParser.UI.Controls
{
    /// <summary>
    /// Custom control for displaying PMIC registers in a 16x16 grid
    /// </summary>
    public class RegisterGridView : TableLayoutPanel
    {
        private PmicDump _currentDump;
        private readonly Color _changedColor = Color.FromArgb(220, 240, 255);
        private readonly Color _protectedColor = Color.FromArgb(255, 250, 205);
        private readonly Color _criticalColor = Color.FromArgb(255, 230, 230);
        private readonly Color _defaultGridColor = Color.FromArgb(245, 245, 245);
        private readonly Color _unchangedColor = Color.FromArgb(230, 255, 230);
        private readonly Color _selectedGridColor = Color.FromArgb(70, 130, 180);

        public event EventHandler<RegisterSelectedEventArgs> RegisterSelected;

        public RegisterGridView()
        {
            InitializeGrid();
        }

        /// <summary>
        /// Updates the grid with data from a new PMIC dump
        /// </summary>
        public void UpdateGrid(PmicDump dump)
        {
            _currentDump = dump;
            RefreshGridCells();
        }

        /// <summary>
        /// Highlights a specific register in the grid
        /// </summary>
        public void HighlightRegister(byte address)
        {
            foreach (Control control in Controls)
            {
                if (control is Panel cell && cell.Tag is byte cellAddress)
                {
                    bool isSelected = cellAddress == address;
                    cell.BorderStyle = isSelected ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;

                    if (isSelected)
                    {
                        cell.BackColor = _selectedGridColor;
                        cell.BringToFront();
                    }
                    else
                    {
                        UpdateCellColor(cell, cellAddress);
                    }
                }
            }
        }

        #region Private Methods

        private void InitializeGrid()
        {
            Dock = DockStyle.Fill;
            ColumnCount = 17;
            RowCount = 17;
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            Padding = new Padding(1);
            BackColor = Color.FromArgb(230, 230, 230);

            SetupGridStyles();
            CreateHeaders();
            CreateRegisterCells();
        }

        private void SetupGridStyles()
        {
            for (int i = 0; i < 17; i++)
            {
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, i == 0 ? 4 : 6));
                RowStyles.Add(new RowStyle(SizeType.Percent, i == 0 ? 4 : 6));
            }
        }

        private void CreateHeaders()
        {
            for (int col = 0; col < 16; col++)
            {
                var header = CreateHeaderLabel($"{col:X1}");
                Controls.Add(header, col + 1, 0);
            }

            for (int row = 0; row < 16; row++)
            {
                var header = CreateHeaderLabel($"{row:X1}0");
                Controls.Add(header, 0, row + 1);
            }
        }

        private Label CreateHeaderLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
        }

        private void CreateRegisterCells()
        {
            for (int row = 0; row < 16; row++)
            {
                for (int col = 0; col < 16; col++)
                {
                    byte address = (byte)((row << 4) | col);
                    var cellPanel = CreateRegisterCell(address);
                    Controls.Add(cellPanel, col + 1, row + 1);
                }
            }
        }

        private Panel CreateRegisterCell(byte address)
        {
            var cellPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _defaultGridColor,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(1),
                Tag = address,
                Cursor = Cursors.Hand
            };

            var addressLabel = new Label
            {
                Text = $"0x{address:X2}",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 7.5f),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            cellPanel.Controls.Add(addressLabel);
            SetupCellEvents(cellPanel, addressLabel, address);

            return cellPanel;
        }

        private void SetupCellEvents(Panel cellPanel, Label addressLabel, byte address)
        {
            EventHandler clickHandler = (s, e) => OnCellClicked(address);
            cellPanel.Click += clickHandler;
            addressLabel.Click += clickHandler;

            cellPanel.MouseEnter += (s, e) => ApplyHoverEffect(cellPanel, true);
            cellPanel.MouseLeave += (s, e) => ApplyHoverEffect(cellPanel, false);
            addressLabel.MouseEnter += (s, e) => ApplyHoverEffect(cellPanel, true);
            addressLabel.MouseLeave += (s, e) => ApplyHoverEffect(cellPanel, false);
        }

        private void OnCellClicked(byte address)
        {
            ParsedRegister register = null;

            if (_currentDump?.Registers.TryGetValue(address, out register) == true)
            {
                RegisterSelected?.Invoke(this, new RegisterSelectedEventArgs(register));
            }
            else
            {
                register = CreateReservedRegister(address);
                RegisterSelected?.Invoke(this, new RegisterSelectedEventArgs(register));
            }
        }

        private ParsedRegister CreateReservedRegister(byte address)
        {
            return new ParsedRegister
            {
                Address = address,
                RawValue = 0,
                DefaultValue = 0,
                Name = $"RESERVED_{address:X2}",
                FullName = $"Reserved 0x{address:X2}",
                Category = "Reserved",
                DecodedValue = "Reserved",
                Description = $"Reserved register 0x{address:X2}",
                Definition = Services.RegisterLoaderService.GetDefinition(address)
            };
        }

        private void ApplyHoverEffect(Panel cell, bool isEntering)
        {
            if (cell.BackColor == _selectedGridColor) return;

            if (isEntering)
            {
                Color original = cell.BackColor;
                cell.BackColor = Color.FromArgb(
                    Math.Max(original.R - 20, 0),
                    Math.Max(original.G - 20, 0),
                    Math.Max(original.B - 20, 0)
                );
            }
            else
            {
                if (cell.Tag is byte address)
                {
                    UpdateCellColor(cell, address);
                }
            }
        }

        private void RefreshGridCells()
        {
            foreach (Control control in Controls)
            {
                if (control is Panel cell && cell.Tag is byte address)
                {
                    UpdateCellColor(cell, address);
                }
            }
        }

        private void UpdateCellColor(Panel cell, byte address)
        {
            if (_currentDump?.Registers.TryGetValue(address, out var reg) == true)
            {
                if (reg.Category == "Reserved" && reg.Name.StartsWith("RESERVED_"))
                {
                    cell.BackColor = _defaultGridColor;
                }
                else if (reg.IsChanged)
                {
                    bool isCritical = IsCriticalChange(reg);
                    cell.BackColor = isCritical ? _criticalColor : _changedColor;
                }
                else if (reg.Definition.Protected == true)
                {
                    cell.BackColor = _protectedColor;
                }
                else
                {
                    cell.BackColor = _unchangedColor;
                }
            }
            else
            {
                cell.BackColor = _defaultGridColor;
            }
        }

        private bool IsCriticalChange(ParsedRegister reg)
        {
            var name = reg.Name.ToUpper();
            bool isVoltageReg = name.Contains("VOLT") || name.Contains("SWA_VOLT") ||
                                name.Contains("SWB_VOLT") || name.Contains("SWC_VOLT");
            bool isCurrentReg = name.Contains("CURR") || name.Contains("SWA_CURR") ||
                                name.Contains("SWB_CURR") || name.Contains("SWC_CURR");

            if (!isVoltageReg && !isCurrentReg) return false;
            if (!reg.IsChanged) return false;

            double nominalValue = reg.DefaultValue;
            if (nominalValue <= 0) return false;

            const double MAX_VOLTAGE_RANGE = 440.0;
            const double CRITICAL_THRESHOLD_PERCENT = 23.0;

            double currentValue = reg.RawValue;
            double absoluteChange = Math.Abs(currentValue - nominalValue);
            double changePercent = (absoluteChange / MAX_VOLTAGE_RANGE) * 100.0;

            return changePercent > CRITICAL_THRESHOLD_PERCENT;
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for register selection events
    /// </summary>
    public class RegisterSelectedEventArgs : EventArgs
    {
        public ParsedRegister Register { get; }

        public RegisterSelectedEventArgs(ParsedRegister register)
        {
            Register = register;
        }
    }
}