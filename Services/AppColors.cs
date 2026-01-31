using System;
using System.Drawing;

namespace PMICDumpParser
{
    /// <summary>
    /// Centralized color definitions for consistent UI styling across the application
    /// </summary>
    public static class AppColors
    {
        // Professional color scheme - used throughout the application

        // Register status colors
        public static readonly Color Unchanged = Color.FromArgb(230, 255, 230);  // Light green
        public static readonly Color Changed = Color.FromArgb(220, 240, 255);    // Light blue
        public static readonly Color Critical = Color.FromArgb(255, 230, 230);   // Light red
        public static readonly Color Protected = Color.FromArgb(255, 250, 205);  // Light yellow

        // UI element colors
        public static readonly Color DefaultGrid = Color.FromArgb(245, 245, 245); // Light gray
        public static readonly Color GridBorder = Color.FromArgb(230, 230, 230);  // Medium gray
        public static readonly Color Selected = Color.FromArgb(70, 130, 180);     // Steel blue

        // Editor tab colors
        public static readonly Color EditorInfoPanel = Color.FromArgb(240, 248, 255);  // Very light blue
        public static readonly Color EditorFieldPanel = Color.FromArgb(255, 255, 255); // White
        public static readonly Color EditorValuePanel = Color.FromArgb(248, 249, 250); // Light gray

        // List view colors
        public static readonly Color Editable = Color.FromArgb(230, 240, 255);    // Very light blue
        public static readonly Color ReadOnly = Color.FromArgb(245, 245, 245);    // Light gray
        public static readonly Color PendingEdit = Color.FromArgb(255, 255, 200); // Light yellow

        // Text colors
        public static readonly Color HeaderText = Color.FromArgb(70, 70, 70);     // Dark gray
        public static readonly Color LabelText = Color.FromArgb(80, 80, 80);      // Medium gray
        public static readonly Color ValueText = Color.FromArgb(0, 0, 0);         // Black
        public static readonly Color DisabledText = Color.FromArgb(150, 150, 150);// Light gray

        // Status colors
        public static readonly Color Success = Color.FromArgb(40, 167, 69);       // Green
        public static readonly Color Warning = Color.FromArgb(255, 193, 7);       // Yellow
        public static readonly Color Error = Color.FromArgb(220, 53, 69);         // Red
        public static readonly Color Info = Color.FromArgb(23, 162, 184);         // Cyan

        /// <summary>
        /// Gets the appropriate color for a register based on its status
        /// </summary>
        /// <param name="isChanged">Whether the register value differs from default</param>
        /// <param name="isProtected">Whether the register is protected</param>
        /// <param name="isCritical">Whether the change is critical (>23%)</param>
        /// <param name="isReserved">Whether the register is reserved</param>
        /// <returns>The appropriate color for the register status</returns>
        public static Color GetRegisterColor(bool isChanged, bool isProtected, bool isCritical, bool isReserved)
        {
            if (isReserved)
                return DefaultGrid;
            else if (isCritical)
                return Critical;
            else if (isChanged)
                return Changed;
            else if (isProtected)
                return Protected;
            else
                return Unchanged;
        }

        /// <summary>
        /// Gets the appropriate text color for a register based on its status
        /// </summary>
        /// <param name="isChanged">Whether the register value differs from default</param>
        /// <param name="isEditable">Whether the register can be edited</param>
        /// <param name="isReserved">Whether the register is reserved</param>
        /// <returns>The appropriate text color for the register</returns>
        public static Color GetRegisterTextColor(bool isChanged, bool isEditable, bool isReserved)
        {
            if (isReserved)
                return DisabledText;
            else if (!isEditable)
                return DisabledText;
            else if (isChanged)
                return ValueText;
            else
                return LabelText;
        }

        /// <summary>
        /// Gets a darker version of a color for hover effects
        /// </summary>
        /// <param name="color">The base color</param>
        /// <param name="darkenAmount">Amount to darken (0-255)</param>
        /// <returns>Darkened color</returns>
        public static Color GetHoverColor(Color color, int darkenAmount = 20)
        {
            return Color.FromArgb(
                Math.Max(color.R - darkenAmount, 0),
                Math.Max(color.G - darkenAmount, 0),
                Math.Max(color.B - darkenAmount, 0)
            );
        }
    }
}