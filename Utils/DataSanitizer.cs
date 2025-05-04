using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Attempts to detect column positions based on a sample of data lines
        /// </summary>
        public static List<int> DetectColumnPositionsFromDataSample(string[] sampleLines, int expectedColumns)
        {
            if (sampleLines == null || sampleLines.Length == 0 || expectedColumns <= 0)
                return new List<int>();

            // Find the longest line to analyze
            string longestLine = sampleLines.OrderByDescending(l => l.Length).First();
            
            // Initialize an array to count non-space characters at each position
            int[] characterCounts = new int[longestLine.Length];
            
            // Analyze all sample lines
            foreach (var line in sampleLines)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    if (!char.IsWhiteSpace(line[i]))
                    {
                        characterCounts[i]++;
                    }
                }
            }
            
            // Identify potential column boundaries
            List<int> potentialColumnPositions = new List<int>();
            bool inColumn = false;
            int lastPosition = -1;
            
            for (int i = 0; i < characterCounts.Length; i++)
            {
                if (characterCounts[i] > 0 && !inColumn)
                {
                    // Found start of a new column
                    potentialColumnPositions.Add(i);
                    inColumn = true;
                    lastPosition = i;
                }
                else if ((characterCounts[i] == 0 || i == characterCounts.Length - 1) && inColumn)
                {
                    // Found potential end of column (gap of at least 2 spaces)
                    if (i - lastPosition >= 2 || i == characterCounts.Length - 1)
                    {
                        inColumn = false;
                    }
                }
            }
            
            // If we found too many potential columns, try to merge some
            if (potentialColumnPositions.Count > expectedColumns)
            {
                // Sort column positions by character density (total characters in each column)
                var columnDensities = new List<(int Position, int Density)>();
                
                for (int i = 0; i < potentialColumnPositions.Count - 1; i++)
                {
                    int start = potentialColumnPositions[i];
                    int end = (i < potentialColumnPositions.Count - 1) 
                        ? potentialColumnPositions[i + 1] 
                        : characterCounts.Length;
                    
                    int density = 0;
                    for (int j = start; j < end && j < characterCounts.Length; j++)
                    {
                        density += characterCounts[j];
                    }
                    
                    columnDensities.Add((Position: start, Density: density));
                }
                
                // Keep the columns with highest density
                var keptColumns = columnDensities
                    .OrderByDescending(c => c.Density)
                    .Take(expectedColumns)
                    .OrderBy(c => c.Position)
                    .Select(c => c.Position)
                    .ToList();
                
                return keptColumns;
            }
            
            return potentialColumnPositions;
        }

        /// <summary>
        /// Parses a line using detected column positions to handle fixed-width format
        /// </summary>
        public static string[] ParseLineUsingColumnPositions(string line, List<int> columnPositions)
        {
            if (string.IsNullOrEmpty(line) || columnPositions == null || columnPositions.Count == 0)
                return Array.Empty<string>();
                
            string[] fields = new string[columnPositions.Count];
            
            for (int i = 0; i < columnPositions.Count; i++)
            {
                int startPos = columnPositions[i];
                int endPos = (i < columnPositions.Count - 1) 
                    ? columnPositions[i + 1] 
                    : line.Length;
                
                // Check if we're past the end of the line
                if (startPos >= line.Length)
                {
                    fields[i] = string.Empty;
                    continue;
                }
                
                // Extract the field value
                int length = Math.Min(endPos - startPos, line.Length - startPos);
                string fieldValue = line.Substring(startPos, length).Trim();
                
                // If field is entirely whitespace, treat as NULL
                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    fields[i] = string.Empty;
                }
                else
                {
                    fields[i] = fieldValue;
                }
            }
            
            return fields;
        }

        /// <summary>
        /// Attempts to parse a line into fields with awareness of compound values
        /// </summary>
        public static string[] ParseDelimitedLineWithCompoundValues(string line, int expectedFieldCount)
        {
            if (string.IsNullOrEmpty(line) || expectedFieldCount <= 0)
                return Array.Empty<string>();
            
            // Split by whitespace initially
            string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            // If we have exact match or fewer fields, just return them
            if (tokens.Length <= expectedFieldCount)
            {
                var result = tokens.ToList();
                
                // Pad with empty strings if needed
                while (result.Count < expectedFieldCount)
                {
                    result.Add(string.Empty);
                }
                
                return result.ToArray();
            }
            
            // If we have more tokens than fields, we need to handle compound values
            List<string> fields = new List<string>();
            
            // Try to determine which fields are compound by analyzing spaces between potential field values
            int tokenIndex = 0;
            
            // For all fields except the last one
            for (int fieldIndex = 0; fieldIndex < expectedFieldCount - 1; fieldIndex++)
            {
                if (tokenIndex < tokens.Length)
                {
                    fields.Add(tokens[tokenIndex]);
                    tokenIndex++;
                }
                else
                {
                    fields.Add(string.Empty);
                }
            }
            
            // Combine all remaining tokens for the last field (which often contains company names)
            if (tokenIndex < tokens.Length)
            {
                string lastField = string.Join(" ", tokens.Skip(tokenIndex));
                fields.Add(lastField);
            }
            else
            {
                fields.Add(string.Empty);
            }
            
            return fields.ToArray();
        }

        /// <summary>
        /// Escapes a field for CSV output according to RFC 4180
        /// </summary>
        public static string EscapeForCsv(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return "NULL"; // Return NULL instead of empty string

            // Trim whitespace from the field
            field = field.Trim();
            
            // If after trimming the field is empty, return NULL
            if (string.IsNullOrEmpty(field))
                return "NULL";

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
    }
}