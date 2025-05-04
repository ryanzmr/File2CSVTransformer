using File2CSVTransformer.Services;
using System;
using System.Threading.Tasks;

namespace File2CSVTransformer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 File2CSV Transformer");
            Console.WriteLine("Transforming Oracle Spool Outputs into Clean, Structured CSVs");
            Console.WriteLine(new string('-', 70));

            try
            {
                // Load configuration
                var configManager = new ConfigManager();
                var appSettings = await configManager.LoadConfigAsync();
                
                // Initialize logger
                var logger = new Logger(appSettings.LogDirectory);
                
                // Initialize file scanner
                var fileScanner = new FileScanner(appSettings.InputDirectory, logger);
                
                // Scan for files to process
                var filesToProcess = await fileScanner.ScanForFilesAsync();
                
                if (filesToProcess.Count == 0)
                {
                    Console.WriteLine("No files found to process. Please add .txt files to the Input directory.");
                    return;
                }
                
                // Initialize file processor
                var fileProcessor = new FileProcessor(appSettings, logger);
                
                // Process each file sequentially
                Console.WriteLine($"Starting to process {filesToProcess.Count} files...");
                
                foreach (var filePath in filesToProcess)
                {
                    await fileProcessor.ProcessFileAsync(filePath);
                }
                
                Console.WriteLine(new string('-', 70));
                Console.WriteLine($"✅ Processing complete! Processed {filesToProcess.Count} files.");
                Console.WriteLine($"Output files saved to: {appSettings.OutputDirectory}");
                Console.WriteLine($"Logs saved to: {appSettings.LogDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Critical error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
