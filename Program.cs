using System;
using System.Windows.Forms;

namespace PMICDumpParser
{
    /// <summary>
    /// Main entry point for the PMIC Dump Parser application
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Enable visual styles for modern UI
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Set application-wide exception handling
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => HandleException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                HandleException(e.ExceptionObject as Exception);

            try
            {
                // Create and run the main form
                using (var mainForm = new MainForm())
                {
                    Application.Run(mainForm);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        /// <summary>
        /// Handles uncaught exceptions by displaying an error message
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        private static void HandleException(Exception ex)
        {
            if (ex == null) return;

            string errorMessage = $"An unexpected error occurred:\n\n{ex.Message}\n\n" +
                                 $"Stack Trace:\n{ex.StackTrace}";

            MessageBox.Show(errorMessage, "Application Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Log to file if needed
            try
            {
                System.IO.File.AppendAllText("error.log",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch
            {
                // Ignore file logging errors
            }
        }
    }
}