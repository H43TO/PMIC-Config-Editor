using System;
using System.Drawing;
using System.Windows.Forms;

namespace PMICDumpParser.Utilities
{
    /// <summary>
    /// Extension methods and utility functions for cleaner code
    /// </summary>
    public static class UtilityExtensions
    {
        /// <summary>
        /// Safely invokes an action on the UI thread if needed
        /// </summary>
        public static void InvokeIfRequired(this Control control, Action action)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Safely begins invoke on the UI thread for better responsiveness
        /// </summary>
        public static void BeginInvokeIfRequired(this Control control, Action action)
        {
            if (control.InvokeRequired)
            {
                control.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Creates a consistent header label with standard styling
        /// </summary>
        public static Label CreateHeaderLabel(string text, ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = AppColors.HeaderText,
                TextAlign = alignment,
                AutoSize = true
            };
        }

        /// <summary>
        /// Creates a consistent value label with standard styling
        /// </summary>
        public static Label CreateValueLabel(string text, ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9),
                ForeColor = AppColors.ValueText,
                TextAlign = alignment,
                AutoSize = true
            };
        }

        /// <summary>
        /// Creates a button with consistent styling
        /// </summary>
        public static Button CreateStyledButton(string text, Color backColor, EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Height = 32,
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += clickHandler;

            return button;
        }

        /// <summary>
        /// Creates a textbox with consistent styling and event handling
        /// </summary>
        public static TextBox CreateStyledTextBox(string placeholder = "", int maxLength = 0)
        {
            var textBox = new TextBox
            {
                Font = new Font("Segoe UI", 9),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(2)
            };

            if (maxLength > 0)
                textBox.MaxLength = maxLength;

            if (!string.IsNullOrEmpty(placeholder))
            {
                textBox.Text = placeholder;
                textBox.ForeColor = AppColors.DisabledText;

                textBox.Enter += (s, e) =>
                {
                    if (textBox.Text == placeholder)
                    {
                        textBox.Text = "";
                        textBox.ForeColor = AppColors.ValueText;
                    }
                };

                textBox.Leave += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = placeholder;
                        textBox.ForeColor = AppColors.DisabledText;
                    }
                };
            }

            return textBox;
        }

        /// <summary>
        /// Converts a byte to a formatted hexadecimal string
        /// </summary>
        public static string ToHexString(this byte value) => $"0x{value:X2}";

        /// <summary>
        /// Converts a byte to a formatted binary string (8 bits)
        /// </summary>
        public static string ToBinaryString(this byte value) => Convert.ToString(value, 2).PadLeft(8, '0');

        /// <summary>
        /// Safely parses a string as a byte from hex format
        /// </summary>
        public static bool TryParseHex(this string text, out byte result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleanText = text.Replace("0x", "").Replace("0X", "").Trim();

            return byte.TryParse(cleanText, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Safely parses a string as a byte from binary format
        /// </summary>
        public static bool TryParseBinary(this string text, out byte result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleanText = text.Replace(" ", "").Trim();

            if (cleanText.Length > 8)
                cleanText = cleanText.Substring(0, 8);

            try
            {
                result = Convert.ToByte(cleanText, 2);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}