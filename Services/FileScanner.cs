using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace File2CSVTransformer.Services
{
    public class FileScanner
    {
        private readonly string _inputDirectory;
        private readonly Logger _logger;

        public FileScanner(string inputDirectory, Logger logger)
        {
            _inputDirectory = inputDirectory;
            _logger = logger;
        }

        public async Task<List<string>> ScanForFilesAsync()
        {
            try
            {
                if (!Directory.Exists(_inputDirectory))
                {
                    throw new DirectoryNotFoundException($"Input directory not found: {_inputDirectory}");
                }

                // Get all text files in the input directory
                var files = Directory.GetFiles(_inputDirectory, "*.txt")
                                    .OrderBy(f => new FileInfo(f).CreationTime)
                                    .ToList();

                if (files.Count == 0)
                {
                    Console.WriteLine($"No .txt files found in {_inputDirectory}");
                }
                else
                {
                    Console.WriteLine($"Found {files.Count} .txt files to process");
                }

                return files;
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("FileScanner", $"Error scanning for files: {ex.Message}", ex);
                throw;
            }
        }
    }
}