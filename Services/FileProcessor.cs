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
        private readonly ConsoleLogger _consoleLogger;
        private const int SAMPLE_SIZE = 100; // Number of lines to sample for column detection

        public FileProcessor(AppSettings settings, Logger logger, ConsoleLogger consoleLogger)
        {
            _settings = settings;
            _logger = logger;
            _consoleLogger = consoleLogger;
        }

        public async Task<ProcessingResult> ProcessFileAsync(string filePath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            string fileName = Path.GetFileName(filePath);
            string outputFilePath = Path.Combine(_settings.OutputDirectory, Path.ChangeExtension(fileName, ".csv"));
            
            var result = new ProcessingResult
            {
                FileName = fileName,
                OutputFilePath = outputFilePath
            };

            // Removed the LogProcessing call as it's now handled by LogWithProgressInfo in Program.cs

            try
            {
                // Read all lines from the input file
                Console.ForegroundColor = ConsoleColor.DarkGray;
                _consoleLogger.LogInfo($"Reading file: {fileName}");
                string[] allLines = await File.ReadAllLinesAsync(filePath);
                result.TotalLinesRead = allLines.Length;
                Console.ResetColor();
                
                // Preprocess lines: Skip SQL query and Oracle footer
                Console.ForegroundColor = ConsoleColor.DarkGray;
                _consoleLogger.LogInfo($"Preprocessing and parsing data...");
                result.SkippedLinesTop = _settings.LinesToSkip.Top;
                result.SkippedLinesBottom = _settings.LinesToSkip.Bottom;
                Console.ResetColor();
                
                var processedData = await PreprocessAndParseDataAsync(allLines, result);
                
                // Write to CSV file
                Console.ForegroundColor = ConsoleColor.DarkGray;
                _consoleLogger.LogInfo($"Writing data to CSV file...");
                Console.ResetColor();
                int rowsWritten = await WriteToCSVAsync(processedData, outputFilePath);
                result.RowsProcessed = rowsWritten;
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                
                await _logger.LogSuccessAsync(fileName, rowsWritten, stopwatch.Elapsed);
                
                Console.ForegroundColor = ConsoleColor.Green;
                _consoleLogger.LogSuccess($"✓ Successfully processed {fileName}");
                Console.ResetColor();
                
                Console.ForegroundColor = ConsoleColor.DarkGray;
                _consoleLogger.Log($"    • File details:");
                _consoleLogger.Log($"      ◦ Lines read: {result.TotalLinesRead:N0}");
                _consoleLogger.Log($"      ◦ Skipped lines (top): {result.SkippedLinesTop}");
                _consoleLogger.Log($"      ◦ Skipped lines (bottom): {result.SkippedLinesBottom}");
                _consoleLogger.Log($"      ◦ CSV rows written: {result.RowsProcessed:N0}");
                _consoleLogger.Log($"      ◦ Processing speed: {result.RowsProcessed / Math.Max(1, result.ProcessingTime.TotalSeconds):N0} rows/second");
                _consoleLogger.Log($"    • Output:");
                _consoleLogger.Log($"      ◦ Path: {outputFilePath}");
                _consoleLogger.Log($"      ◦ Size: {FileHelper.GetFileSizeFormatted(outputFilePath)}");
                _consoleLogger.Log($"      ◦ Note: Blank fields treated as NULL values");
                Console.ResetColor();
                
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                result.ErrorMessage = ex.Message;
                
                await _logger.LogErrorAsync(fileName, $"Error processing file: {ex.Message}", ex);
                
                Console.ForegroundColor = ConsoleColor.Red;
                _consoleLogger.LogError($"✗ Error processing {fileName}: {ex.Message}");
                Console.ResetColor();
                
                result.IsSuccess = false;
            }
            
            return result;
        }

        private async Task<List<string[]>> PreprocessAndParseDataAsync(string[] allLines, ProcessingResult result)
        {
            // Check if a footer marker is configured and adjust processing accordingly
            int markerLineIndex = -1;
            if (!string.IsNullOrEmpty(_settings.FooterMarker))
            {
                markerLineIndex = Array.FindLastIndex(allLines, line => line.Contains(_settings.FooterMarker));
                
                if (markerLineIndex >= 0)
                {
                    _consoleLogger.LogInfo($"Found footer marker '{_settings.FooterMarker}' at line {markerLineIndex + 1}");
                    
                    // Always use the configured footer skip setting to ensure we skip the expected number of lines
                    // This ensures we skip the footer marker line plus any additional summary lines
                    result.SkippedLinesBottom = _settings.LinesToSkip.Bottom;
                    
                    _consoleLogger.LogInfo($"Dynamically adjusted footer skip to {result.SkippedLinesBottom} lines");
                }
            }
            
            // Make sure we capture all lines between the top and bottom skip boundaries
            var contentLines = allLines
                .Skip(_settings.LinesToSkip.Top)
                .Take(allLines.Length - _settings.LinesToSkip.Top - result.SkippedLinesBottom)
                .ToArray();

            result.ContentLinesCount = contentLines.Length;
            
            // Try to find header line (if exists)
            int headerIndex = FindHeaderLineIndex(contentLines);
            
            // Determine which parsing strategy to use
            if (headerIndex >= 0 && headerIndex + 1 < contentLines.Length)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"Header line found in file. Using column-based parsing.");
                Console.ResetColor();
                result.ParsingStrategy = "Column-based parsing";
                return await ParseUsingHeaderGuidanceAsync(contentLines, headerIndex);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"No header line found. Using intelligent data structure detection.");
                Console.ResetColor();
                result.ParsingStrategy = "Intelligent data structure detection";
                return await ParseUsingDataStructureDetectionAsync(contentLines);
            }
        }

        // ... rest of the file remains unchanged ...
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
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"Detected {columnWidths.Length} columns using header line. Using fixed-width parsing.");
                Console.ResetColor();
                return ParseFixedWidthData(dataLines, columnWidths);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"Could not determine column widths from header. Using data structure detection.");
                Console.ResetColor();
                return await ParseUsingDataStructureDetectionAsync(contentLines);
            }
        }

        private async Task<List<string[]>> ParseUsingDataStructureDetectionAsync(string[] contentLines)
        {
            // Check if a footer marker is configured and adjust processing if needed
            if (!string.IsNullOrEmpty(_settings.FooterMarker))
            {
                // Find the marker line in the content
                int markerLineIndex = Array.FindIndex(contentLines, line => line.Contains(_settings.FooterMarker));
                
                if (markerLineIndex >= 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    _consoleLogger.LogInfo($"Found footer marker '{_settings.FooterMarker}' at line {markerLineIndex + _settings.LinesToSkip.Top}");
                    Console.ResetColor();
                    
                    // Only keep content up to the marker line
                    contentLines = contentLines.Take(markerLineIndex).ToArray();
                    
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    _consoleLogger.LogInfo($"Adjusted content to exclude footer based on marker: {contentLines.Length} lines");
                    Console.ResetColor();
                }
            }
            
            // Take a representative sample of data lines to analyze for column structure
            string[] sampleLines = GetSampleLines(contentLines, SAMPLE_SIZE);
            
            // Use intelligent column position detection based on data analysis
            List<int> columnPositions = DataSanitizer.DetectColumnPositionsFromDataSample(
                sampleLines, 
                _settings.HeaderColumns.Count);
            
            if (columnPositions.Count == _settings.HeaderColumns.Count)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"Detected {columnPositions.Count} column positions from data analysis. Using position-based parsing.");
                Console.ResetColor();
                return ParsePositionBasedData(contentLines, columnPositions);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                _consoleLogger.LogInfo($"Column position detection yielded {columnPositions.Count} positions but {_settings.HeaderColumns.Count} expected.");
                _consoleLogger.LogInfo($"Using delimiter-based parsing with compound value handling.");
                Console.ResetColor();
                
                // Explicitly await something to avoid CS1998
                await Task.Yield();
                
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
                    if (i < row.Length && !string.IsNullOrWhiteSpace(row[i]))
                    {
                        // Handle date formatting for date column (index 1)
                        if (i == 1 && DateTime.TryParse(row[i].Trim(), out DateTime date))
                        {
                            normalizedRow[i] = date.ToString("dd-MMM-yyyy"); // Format as 09-Apr-2025
                        }
                        else
                        {
                            // Sanitize field: escape quotes and delimiters if needed
                            normalizedRow[i] = DataSanitizer.EscapeForCsv(row[i], _settings.Delimiter[0]);
                        }
                    }
                    else
                    {
                        // Use "NULL" for empty values instead of empty strings
                        normalizedRow[i] = "NULL";
                    }
                }
                
                csvContent.AppendLine(string.Join(_settings.Delimiter, normalizedRow));
            }
            
            await File.WriteAllTextAsync(outputFilePath, csvContent.ToString());
            return rows.Count;
        }
    }

    public class ProcessingResult
    {
        public string FileName { get; set; } = string.Empty;
        public string OutputFilePath { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public int TotalLinesRead { get; set; }
        public int SkippedLinesTop { get; set; }
        public int SkippedLinesBottom { get; set; }
        public int ContentLinesCount { get; set; }
        public int RowsProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string ParsingStrategy { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}