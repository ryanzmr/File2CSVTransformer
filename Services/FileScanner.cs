using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using File2CSVTransformer.Models;

namespace File2CSVTransformer.Services
{
    public class FileScanner
    {
        private readonly string _inputDirectory;
        private readonly Logger _logger;
        private readonly List<string> _supportedExtensions;

        public FileScanner(string inputDirectory, Logger logger, List<string> supportedExtensions)
        {
            _inputDirectory = inputDirectory;
            _logger = logger;
            _supportedExtensions = supportedExtensions ?? new List<string> { ".txt" };
        }

        public async Task<List<string>> ScanForFilesAsync()
        {
            try
            {
                if (!Directory.Exists(_inputDirectory))
                {
                    throw new DirectoryNotFoundException($"Input directory not found: {_inputDirectory}");
                }

                var allFiles = new List<string>();
                
                // Get all files with supported extensions
                foreach (var extension in _supportedExtensions)
                {
                    // Ensure extension starts with a dot
                    string searchPattern = extension.StartsWith(".") ? $"*{extension}" : $"*.{extension}";
                    
                    var files = Directory.GetFiles(_inputDirectory, searchPattern);
                    allFiles.AddRange(files);
                }
                
                // Order files by creation time
                var orderedFiles = allFiles.Distinct()
                                          .OrderBy(f => new FileInfo(f).CreationTime)
                                          .ToList();

                if (orderedFiles.Count == 0)
                {
                    Console.WriteLine($"No files with supported extensions found in {_inputDirectory}");
                    Console.WriteLine($"Supported extensions: {string.Join(", ", _supportedExtensions)}");
                }
                else
                {
                    Console.WriteLine($"Found {orderedFiles.Count} files to process");
                    
                    // Group files by extension for detailed reporting
                    var filesByExtension = orderedFiles
                        .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.Count());
                    
                    foreach (var group in filesByExtension)
                    {
                        Console.WriteLine($"  - {group.Value} {group.Key} file(s)");
                    }
                }

                return orderedFiles;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("FileScanner", $"Error scanning for files: {ex.Message}", ex);
                throw;
            }
        }
    }
}