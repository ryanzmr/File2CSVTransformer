using System;
using System.Text;
using System.Text.RegularExpressions;

namespace File2CSVTransformer.Utils
{
    public static class DataSanitizer
    {
        /// <summary>
        /// Cleans Oracle spool text data by normalizing whitespace
        /// </summary>
        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Normalize whitespace (replace multiple spaces/tabs with a single space)
            string cleaned = Regex.Replace(input, @"\s+", " ").Trim();
            
            // Remove any control characters
            cleaned = Regex.Replace(cleaned, @"[\x00-\x1F\x7F]", string.Empty);
            
            return cleaned;
        }

        /// <summary>
        /// Normalizes line endings to be consistent across all platforms
        /// </summary>
        public static string NormalizeLineEndings(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Replace all line endings with Environment.NewLine
            return Regex.Replace(input, @"\r\n?|\n", Environment.NewLine);
        }

        /// <summary>
        /// Attempts to detect and parse column values from a fixed-width format line
        /// </summary>
        public static string[] ParseFixedWidthLine(string line, int[] columnWidths)
        {
            if (string.IsNullOrEmpty(line) || columnWidths == null || columnWidths.Length == 0)
                return Array.Empty<string>();

            string[] fields = new string[columnWidths.Length];
            int startIndex = 0;

            for (int i = 0; i < columnWidths.Length; i++)
            {
                int width = columnWidths[i];
                
                // Ensure we don't go out of bounds
                if (startIndex >= line.Length)
                {
                    fields[i] = string.Empty;
                    continue;
                }

                // Get the field value based on the width, ensuring we don't go out of bounds
                int actualWidth = Math.Min(width, line.Length - startIndex);
                fields[i] = line.Substring(startIndex, actualWidth).Trim();
                
                startIndex += width;
            }

            return fields;
        }

        /// <summary>
        /// Escapes a field for CSV output according to RFC 4180
        /// </summary>
        public static string EscapeForCsv(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            bool needsQuoting = field.Contains(delimiter.ToString()) || 
                                field.Contains("\"") || 
                                field.Contains("\r") || 
                                field.Contains("\n");

            if (needsQuoting)
            {
                // Replace any double quotes with two double quotes
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }

        /// <summary>
        /// Detects column widths from a header line and separator line in a fixed-width format
        /// </summary>
        public static int[] DetectColumnWidths(string headerLine, string separatorLine)
        {
            if (string.IsNullOrEmpty(headerLine) || string.IsNullOrEmpty(separatorLine))
                return Array.Empty<int>();

            // Look for separator characters (-, =, etc.) to identify column boundaries
            var columnBoundaries = new System.Collections.Generic.List<int>();
            bool inSeparator = false;

            for (int i = 0; i < separatorLine.Length; i++)
            {
                char c = separatorLine[i];
                bool isSeparator = c == '-' || c == '=' || c == '_';

                if (isSeparator && !inSeparator)
                {
                    // Start of a separator section
                    columnBoundaries.Add(i);
                    inSeparator = true;
                }
                else if (!isSeparator && inSeparator)
                {
                    // End of a separator section
                    columnBoundaries.Add(i);
                    inSeparator = false;
                }
            }

            // If we ended in a separator, add the end boundary
            if (inSeparator)
            {
                columnBoundaries.Add(separatorLine.Length);
            }

            // Calculate widths based on boundaries
            var widths = new System.Collections.Generic.List<int>();
            for (int i = 0; i < columnBoundaries.Count - 1; i += 2)
            {
                int start = columnBoundaries[i];
                int end = columnBoundaries[i + 1];
                widths.Add(end - start);
            }

            return widths.ToArray();
        }
    }
}