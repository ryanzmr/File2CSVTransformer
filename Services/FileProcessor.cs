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
        private const int SAMPLE_SIZE = 100; // Number of lines to sample for column detection

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
                var processedData = await PreprocessAndParseDataAsync(allLines);
                
                // Write to CSV file
                int rowsWritten = await WriteToCSVAsync(processedData, outputFilePath);
                
                stopwatch.Stop();
                await _logger.LogSuccessAsync(fileName, rowsWritten, stopwatch.Elapsed);
                
                Console.WriteLine($"Successfully processed {fileName} - {rowsWritten} rows written");
                Console.WriteLine($"Output saved to {outputFilePath}");
                Console.WriteLine($"Output file size: {FileHelper.GetFileSizeFormatted(outputFilePath)}");
                Console.WriteLine($"Blank fields treated as NULL values");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await _logger.LogErrorAsync(fileName, $"Error processing file: {ex.Message}", ex);
                Console.WriteLine($"Error processing {fileName}: {ex.Message}");
            }
        }

        private async Task<List<string[]>> PreprocessAndParseDataAsync(string[] allLines)
        {
            // Skip the first line (SQL query) and the last N lines (Oracle footer)
            var contentLines = allLines
                .Skip(_settings.LinesToSkip.Top)
                .Take(allLines.Length - _settings.LinesToSkip.Top - _settings.LinesToSkip.Bottom)
                .ToArray();

            // Try to find header line (if exists)
            int headerIndex = FindHeaderLineIndex(contentLines);
            
            // Determine which parsing strategy to use
            if (headerIndex >= 0 && headerIndex + 1 < contentLines.Length)
            {
                Console.WriteLine("Header line found in file. Using column-based parsing.");
                return await ParseUsingHeaderGuidanceAsync(contentLines, headerIndex);
            }
            else
            {
                Console.WriteLine("No header line found. Using intelligent data structure detection.");
                return await ParseUsingDataStructureDetectionAsync(contentLines);
            }
        }

        private async Task<List<string[]>> ParseUsingHeaderGuidanceAsync(string[] contentLines, int headerIndex)
        {
            // The separator line is typically right after the header line
            int separatorIndex = headerIndex + 1;
            
            // Get all data rows (skip the header and separator lines)
            var dataLines = contentLines.Skip(separatorIndex + 1).ToArray();
            
            // Detect column widths from header and separator lines
            int[] columnWidths = DetectColumnWidths(contentLines[headerIndex], contentLines[separatorIndex]);
            
            if (columnWidths.Length != 0 && columnWidths.Length == _settings.HeaderColumns.Count)
            {
                Console.WriteLine($"Detected {columnWidths.Length} columns using header line. Using fixed-width parsing.");
                return ParseFixedWidthData(dataLines, columnWidths);
            }
            else
            {
                Console.WriteLine("Could not determine column widths from header. Using data structure detection.");
                return await ParseUsingDataStructureDetectionAsync(contentLines);
            }
        }

        private async Task<List<string[]>> ParseUsingDataStructureDetectionAsync(string[] contentLines)
        {
            // Take a representative sample of data lines to analyze for column structure
            string[] sampleLines = GetSampleLines(contentLines, SAMPLE_SIZE);
            
            // Use intelligent column position detection based on data analysis
            List<int> columnPositions = DataSanitizer.DetectColumnPositionsFromDataSample(
                sampleLines, 
                _settings.HeaderColumns.Count);
            
            if (columnPositions.Count == _settings.HeaderColumns.Count)
            {
                Console.WriteLine($"Detected {columnPositions.Count} column positions from data analysis. Using position-based parsing.");
                return ParsePositionBasedData(contentLines, columnPositions);
            }
            else
            {
                Console.WriteLine($"Column position detection yielded {columnPositions.Count} positions but {_settings.HeaderColumns.Count} expected. Using delimiter-based parsing with compound value handling.");
                return ParseDelimitedDataWithCompoundValues(contentLines);
            }
        }

        private string[] GetSampleLines(string[] lines, int sampleSize)
        {
            if (lines.Length <= sampleSize)
                return lines;
            
            // Get a distributed sample across the file
            int step = Math.Max(1, lines.Length / sampleSize);
            return lines.Where((line, index) => index % step == 0)
                        .Take(sampleSize)
                        .ToArray();
        }

        private List<string[]> ParseFixedWidthData(string[] dataLines, int[] columnWidths)
        {
            var parsedLines = new List<string[]>();
            
            foreach (var line in dataLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    List<int> positions = new List<int>();
                    int pos = 0;
                    
                    // Convert widths to starting positions
                    foreach (int width in columnWidths)
                    {
                        positions.Add(pos);
                        pos += width;
                    }
                    
                    string[] fields = DataSanitizer.ParseLineUsingColumnPositions(line, positions);
                    
                    // Only add non-empty rows
                    if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        parsedLines.Add(fields);
                    }
                }
            }
            
            return parsedLines;
        }

        private List<string[]> ParsePositionBasedData(string[] dataLines, List<int> columnPositions)
        {
            var parsedLines = new List<string[]>();
            
            foreach (var line in dataLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string[] fields = DataSanitizer.ParseLineUsingColumnPositions(line, columnPositions);
                    
                    // Only add non-empty rows
                    if (fields.Any(f => !string.IsNullOrWhiteSpace(f)))
                    {
                        parsedLines.Add(fields);
                    }
                }
            }
            
            return parsedLines;
        }

        private List<string[]> ParseDelimitedDataWithCompoundValues(string[] dataLines)
        {
            var parsedLines = new List<string[]>();
            
            foreach (var line in dataLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Clean up line data
                    string cleanedLine = DataSanitizer.CleanText(line);
                    
                    // Use the specialized parser that handles compound values
                    string[] fields = DataSanitizer.ParseDelimitedLineWithCompoundValues(
                        cleanedLine, 
                        _settings.HeaderColumns.Count);
                    
                    // Only add non-empty rows
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
            List<int> positions = new List<int>();
            List<int> widths = new List<int>();
            
            // Look for separator characters in the separator line
            bool inSeparator = false;
            int startPos = -1;
            
            for (int i = 0; i < separatorLine.Length; i++)
            {
                char c = separatorLine[i];
                bool isSeparator = c == '-' || c == '=' || c == '_';
                
                if (isSeparator && !inSeparator)
                {
                    startPos = i;
                    inSeparator = true;
                }
                else if (!isSeparator && inSeparator)
                {
                    positions.Add(startPos);
                    widths.Add(i - startPos);
                    inSeparator = false;
                }
            }
            
            // Handle case where separator ends at the end of the line
            if (inSeparator && startPos != -1)
            {
                positions.Add(startPos);
                widths.Add(separatorLine.Length - startPos);
            }
            
            // Check if the number of detected widths matches our expected column count
            if (widths.Count != _settings.HeaderColumns.Count)
            {
                return Array.Empty<int>();
            }
            
            return widths.ToArray();
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