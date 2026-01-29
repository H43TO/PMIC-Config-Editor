using System.IO;
using System.Threading.Tasks;

namespace PMICDumpParser.Services
{
    /// <summary>
    /// Helper service for file operations
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Asynchronously reads all text from a file
        /// </summary>
        public static async Task<string> ReadTextAsync(string path) =>
            await Task.Run(() => File.ReadAllText(path));

        /// <summary>
        /// Asynchronously reads all bytes from a file
        /// </summary>
        public static async Task<byte[]> ReadBytesAsync(string path) =>
            await Task.Run(() => File.ReadAllBytes(path));

        /// <summary>
        /// Asynchronously writes text to a file
        /// </summary>
        public static async Task WriteTextAsync(string path, string text) =>
            await Task.Run(() => File.WriteAllText(path, text));
    }
}