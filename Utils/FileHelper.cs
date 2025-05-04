using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace File2CSVTransformer.Utils
{
    public static class FileHelper
    {
        /// <summary>
        /// Creates a safe filename by removing invalid characters
        /// </summary>
        public static string CreateSafeFileName(string fileName)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            
            return Regex.Replace(fileName, invalidRegStr, "_");
        }

        /// <summary>
        /// Ensures a directory exists, creating it if it doesn't
        /// </summary>
        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Gets file size in a human-readable format
        /// </summary>
        public static string GetFileSizeFormatted(string filePath)
        {
            if (!File.Exists(filePath))
                return "0 B";

            long bytes = new FileInfo(filePath).Length;
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n2} {suffixes[counter]}";
        }
    }
}