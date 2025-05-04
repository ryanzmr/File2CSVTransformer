using File2CSVTransformer.Models;
using File2CSVTransformer.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace File2CSVTransformer.Services
{
    public class FileProcessor
    {
        private readonly AppSettings _settings;
        private readonly Logger _logger;

        public FileProcessor(AppSettings settings, Logger logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public async Task ProcessFileAsync(string filePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            string fileName = Path.GetFileName(filePath);
            string outputFilePath = Path.Combine(_settings.OutputDirectory, Path.ChangeExtension(fileName, ".csv"));

            Console.WriteLine($"Processing file: {fileName}");

            try
            {
                // Read all lines from the input file
                string[] allLines = await File.ReadAllLinesAsync(filePath);
                
                // Preprocess lines: Skip SQL query and Oracle footer
                var processedLines = PreprocessLines(allLines);
                
                // Write to CSV file
                int rowsWritten = await WriteToCSVAsync(processedLines, outputFilePath);
                
                stopwatch.Stop();
                await _logger.LogSuccessAsync(fileName, rowsWritten, stopwatch.Elapsed);
                
                Console.WriteLine($"Successfully processed {fileName} - {rowsWritten} rows written");
                Console.WriteLine($"Output saved to {outputFilePath}");
                Console.WriteLine($"Output file size: {FileHelper.GetFileSizeFormatted(outputFilePath)}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await _logger.LogErrorAsync(fileName, $"Error processing file: {ex.Message}", ex);
                Console.WriteLine($"Error processing {fileName}: {ex.Message}");
            }
        }

        private List<string[]> PreprocessLines(string[] allLines)
        {
            // Skip the first line (SQL query) and the last N lines (Oracle footer)
            var contentLines = allLines
                .Skip(_settings.LinesToSkip.Top)
                .Take(allLines.Length - _settings.LinesToSkip.Top - _settings.LinesToSkip.Bottom)
                .ToArray();

            // Try to find header line (if exists)
            int headerIndex = FindHeaderLineIndex(contentLines);
            
            // If no header line found, assume all content lines are data
            if (headerIndex < 0)
            {
                Console.WriteLine("No header line found in file. Using configured headers and treating all content as data.");
                return ParseLinesDirectly(contentLines);
            }

            // The separator line is typically right after the header line
            int separatorIndex = headerIndex + 1;
            
            if (separatorIndex >= contentLines.Length)
            {
                Console.WriteLine("No separator line found after header. Using simple parsing for all content.");
                return ParseLinesDirectly(contentLines);
            }

            // Detect column widths from header and separator lines
            int[] columnWidths = DetectColumnWidths(contentLines[headerIndex], contentLines[separatorIndex]);
            
            if (columnWidths.Length == 0)
            {
                // Fallback to simpler parsing if column width detection fails
                return ParseLinesSimple(contentLines.Skip(separatorIndex + 1).ToArray());
            }
            
            // Get all data rows (skip the header and separator lines)
            var dataLines = contentLines.Skip(separatorIndex + 1).ToArray();
            
            // Parse each data line into fields using the detected column widths
            var parsedLines = new List<string[]>();
            foreach (var line in dataLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string[] fields = DataSanitizer.ParseFixedWidthLine(line, columnWidths);
                    
                    // Only add non-empty rows (where at least one field has data)
                    if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        parsedLines.Add(fields);
                    }
                }
            }

            return parsedLines;
        }

        private List<string[]> ParseLinesDirectly(string[] contentLines)
        {
            var parsedLines = new List<string[]>();
            
            foreach (var line in contentLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Clean up line data
                    string cleanedLine = DataSanitizer.CleanText(line);
                    
                    // Simple space-based splitting with field combining
                    string[] fields = ParseDataLineSimple(cleanedLine);
                    
                    if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        parsedLines.Add(fields);
                    }
                }
            }
            
            return parsedLines;
        }

        private int[] DetectColumnWidths(string headerLine, string separatorLine)
        {
            // Try to detect column widths from the header and separator lines
            var detectedWidths = DataSanitizer.DetectColumnWidths(headerLine, separatorLine);
            
            // Validate the number of detected columns matches expected headers
            if (detectedWidths.Length != _settings.HeaderColumns.Count)
            {
                Console.WriteLine($"Warning: Detected {detectedWidths.Length} columns but expected {_settings.HeaderColumns.Count}. Using simple parsing instead.");
                return Array.Empty<int>();
            }
            
            return detectedWidths;
        }

        private List<string[]> ParseLinesSimple(string[] dataLines)
        {
            var parsedLines = new List<string[]>();
            
            foreach (var line in dataLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Clean up line data
                    string cleanedLine = DataSanitizer.CleanText(line);
                    
                    // Simple space-based splitting with field combining
                    string[] fields = ParseDataLineSimple(cleanedLine);
                    
                    if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        parsedLines.Add(fields);
                    }
                }
            }
            
            return parsedLines;
        }

        private int FindHeaderLineIndex(string[] lines)
        {
            // Look for a line that contains most of the header column names
            for (int i = 0; i < lines.Length; i++)
            {
                int matchCount = 0;
                foreach (var headerColumn in _settings.HeaderColumns)
                {
                    if (lines[i].Contains(headerColumn))
                    {
                        matchCount++;
                    }
                }

                // If the line contains at least 70% of the header columns, consider it a match
                if (matchCount >= _settings.HeaderColumns.Count * 0.7)
                {
                    return i;
                }
            }

            return -1;
        }

        private string[] ParseDataLineSimple(string line)
        {
            // Split by spaces, but need to handle special cases
            List<string> fields = new List<string>();
            
            // Split the line by spaces
            string[] rawFields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Ensure we don't have more fields than header columns
            if (rawFields.Length <= _settings.HeaderColumns.Count)
            {
                // If we have the exact number of fields or fewer, just use them directly
                fields.AddRange(rawFields);
            }
            else
            {
                // If we have more fields than headers, combine extras into the last field
                for (int i = 0; i < _settings.HeaderColumns.Count - 1; i++)
                {
                    fields.Add(i < rawFields.Length ? rawFields[i] : string.Empty);
                }
                
                // Combine remaining fields into the last column
                if (rawFields.Length > _settings.HeaderColumns.Count - 1)
                {
                    string lastField = string.Join(" ", rawFields.Skip(_settings.HeaderColumns.Count - 1));
                    fields.Add(lastField);
                }
                else
                {
                    fields.Add(string.Empty);
                }
            }
            
            // Ensure we have the right number of fields
            while (fields.Count < _settings.HeaderColumns.Count)
            {
                fields.Add(string.Empty); // Add empty fields if needed
            }
            
            return fields.ToArray();
        }

        private async Task<int> WriteToCSVAsync(List<string[]> rows, string outputFilePath)
        {
            if (rows.Count == 0)
                return 0;

            StringBuilder csvContent = new StringBuilder();
            
            // Write header
            csvContent.AppendLine(string.Join(_settings.Delimiter, _settings.HeaderColumns));
            
            // Write rows
            foreach (var row in rows)
            {
                // Ensure the row has the same number of fields as the header
                string[] normalizedRow = new string[_settings.HeaderColumns.Count];
                
                for (int i = 0; i < normalizedRow.Length; i++)
                {
                    if (i < row.Length)
                    {
                        // Sanitize field: escape quotes and delimiters if needed
                        normalizedRow[i] = DataSanitizer.EscapeForCsv(row[i], _settings.Delimiter[0]);
                    }
                    else
                    {
                        normalizedRow[i] = string.Empty;
                    }
                }
                
                csvContent.AppendLine(string.Join(_settings.Delimiter, normalizedRow));
            }
            
            await File.WriteAllTextAsync(outputFilePath, csvContent.ToString());
            return rows.Count;
        }
    }
}