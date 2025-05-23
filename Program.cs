﻿using File2CSVTransformer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace File2CSVTransformer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Set console encoding to UTF-8 to display emojis correctly
            Console.OutputEncoding = Encoding.UTF8;
            
            string sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            // Create a temporary console logger for startup messages
            var tempConsoleLogger = new ConsoleLogger("./TempConsoleLog", sessionId);
            
            // Apply color and style to make console output more attractive
            Console.ForegroundColor = ConsoleColor.Cyan;
            tempConsoleLogger.Log("\n");  // Add extra space at the top
            tempConsoleLogger.Log("🚀 FILE2CSV TRANSFORMER");
            tempConsoleLogger.Log("✨ Transforming Oracle Spool Outputs into Clean, Structured CSVs");
            Console.ResetColor();
            tempConsoleLogger.LogSeparator();
            tempConsoleLogger.Log("\n");  // Add extra space for better readability

            try
            {
                // Load configuration
                Console.ForegroundColor = ConsoleColor.White;
                tempConsoleLogger.LogInfo("Loading configuration...");
                var configManager = new ConfigManager();
                var appSettings = await configManager.LoadConfigAsync();
                Console.ResetColor();
                
                // Initialize logger
                Console.ForegroundColor = ConsoleColor.White;
                tempConsoleLogger.LogInfo("Initializing loggers...");
                var logger = new Logger(appSettings.Logs.BaseDirectory);
                
                // Create the real console logger with the proper path from configuration
                string consoleLogPath = Path.Combine(appSettings.Logs.BaseDirectory, appSettings.Logs.ConsoleLogDirectory);
                var consoleLogger = new ConsoleLogger(consoleLogPath, sessionId);
                
                // Copy initial messages to the real console logger
                foreach (var line in tempConsoleLogger.GetLogContent())
                {
                    consoleLogger.AddLogLine(line);
                }
                
                Console.ResetColor();
                
                // Ensure output directory exists
                if (!Directory.Exists(appSettings.OutputDirectory))
                {
                    consoleLogger.LogInfo($"Creating output directory: {appSettings.OutputDirectory}");
                    Directory.CreateDirectory(appSettings.OutputDirectory);
                }
                
                // Initialize file scanner
                Console.ForegroundColor = ConsoleColor.White;
                consoleLogger.LogInfo($"Scanning for files in: {appSettings.InputDirectory}");
                consoleLogger.LogInfo($"Supported file extensions: {string.Join(", ", appSettings.SupportedFileExtensions)}");
                var fileScanner = new FileScanner(appSettings.InputDirectory, logger, appSettings.SupportedFileExtensions);
                Console.ResetColor();
                
                // Scan for files to process
                var filesToProcess = await fileScanner.ScanForFilesAsync();
                
                if (filesToProcess.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    consoleLogger.LogWarning($"No files found to process. Please add files with supported extensions ({string.Join(", ", appSettings.SupportedFileExtensions)}) to the Input directory.");
                    Console.ResetColor();
                    await consoleLogger.SaveLogAsync();
                    return;
                }
                
                Console.ForegroundColor = ConsoleColor.Green;
                consoleLogger.Log($"\n📁 Found {filesToProcess.Count} files to process\n");
                Console.ResetColor();
                
                // Initialize file processor
                Console.ForegroundColor = ConsoleColor.Magenta;
                consoleLogger.LogInfo($"Starting to process {filesToProcess.Count} files...");
                Console.ResetColor();
                consoleLogger.Log("\n");  // Add extra space for better readability
                
                var fileProcessor = new FileProcessor(appSettings, logger, consoleLogger);
                List<ProcessingResult> results = new List<ProcessingResult>();
                
                // Process each file with file index tracking
                for (int i = 0; i < filesToProcess.Count; i++)
                {
                    string filePath = filesToProcess[i];
                    string fileName = Path.GetFileName(filePath);
                    
                    // Display progress info (current file / total files)
                    Console.ForegroundColor = ConsoleColor.Blue;
                    consoleLogger.LogWithProgressInfo(fileName, i + 1, filesToProcess.Count, "Processing started");
                    Console.ResetColor();
                    
                    var result = await fileProcessor.ProcessFileAsync(filePath);
                    results.Add(result);
                    
                    // Add execution time summary after each file
                    if (result.IsSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        consoleLogger.LogTime($"File {i + 1}/{filesToProcess.Count} completed in {result.ProcessingTime.TotalSeconds:F2} seconds");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        consoleLogger.LogTime($"File {i + 1}/{filesToProcess.Count} failed after {result.ProcessingTime.TotalSeconds:F2} seconds");
                        Console.ResetColor();
                    }
                    
                    consoleLogger.Log("\n");  // Add spacing between file processing
                }
                
                // Display summary
                consoleLogger.LogSeparator();
                Console.ForegroundColor = ConsoleColor.Cyan;
                consoleLogger.Log("\n");  // Add extra space before summary
                consoleLogger.LogComplete("Processing complete!");
                
                // Calculate total processing time
                TimeSpan totalProcessingTime = TimeSpan.Zero;
                foreach (var result in results)
                {
                    totalProcessingTime += result.ProcessingTime;
                }
                
                consoleLogger.LogTime($"Total execution time: {totalProcessingTime.TotalSeconds:F2} seconds");
                consoleLogger.Log("\n");  // Add extra space for summary section
                Console.ResetColor();
                
                int totalRows = 0;
                int successFiles = 0;
                int failedFiles = 0;
                
                foreach (var result in results)
                {
                    if (result.IsSuccess)
                    {
                        totalRows += result.RowsProcessed;
                        successFiles++;
                    }
                    else
                    {
                        failedFiles++;
                    }
                }
                
                Console.ForegroundColor = ConsoleColor.White;
                consoleLogger.LogInfo("SUMMARY:");
                consoleLogger.Log($"  📊 Total files processed: {filesToProcess.Count}");
                Console.ForegroundColor = ConsoleColor.Green;
                consoleLogger.LogSuccess($"  ✓ Successfully processed: {successFiles} files");
                Console.ResetColor();
                
                if (failedFiles > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    consoleLogger.LogError($"  ✗ Failed files: {failedFiles}");
                    Console.ResetColor();
                }
                
                Console.ForegroundColor = ConsoleColor.White;
                consoleLogger.LogInfo($"  📈 Total rows processed: {totalRows:N0}");
                consoleLogger.Log($"  ⏱️ Average processing time: {totalProcessingTime.TotalSeconds / filesToProcess.Count:F2} seconds per file");
                consoleLogger.Log("\n");  // Add extra space for locations section
                
                consoleLogger.LogInfo("OUTPUT LOCATIONS:");
                consoleLogger.Log($"  📄 CSV files: {Path.GetFullPath(appSettings.OutputDirectory)}");
                consoleLogger.Log($"  📝 Process logs: {Path.GetFullPath(appSettings.Logs.BaseDirectory)}");
                consoleLogger.Log($"  🔍 Console logs: {Path.GetFullPath(consoleLogPath)}");
                Console.ResetColor();
                
                consoleLogger.Log("\n");  // Add extra space at the end
                
                // Save console log
                await consoleLogger.SaveLogAsync();
                
                // Auto-exit with timeout instead of waiting for key press
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nApplication will automatically close in {appSettings.AutoExitTimeoutSeconds} seconds...");
                Console.ResetColor();
                await Task.Delay(appSettings.AutoExitTimeoutSeconds * 1000);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                tempConsoleLogger.LogError($"Critical error: {ex.Message}");
                tempConsoleLogger.Log(ex.StackTrace ?? string.Empty);
                Console.ResetColor();
                
                try
                {
                    // Try to save logs - use a directory we can reasonably expect to exist
                    string fallbackLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "ConsoleLog");
                    Directory.CreateDirectory(fallbackLogDir);
                    var fallbackLogger = new ConsoleLogger(fallbackLogDir, sessionId);
                    
                    // Add error information
                    fallbackLogger.LogError($"Critical error: {ex.Message}");
                    fallbackLogger.Log(ex.StackTrace ?? string.Empty);
                    
                    // Copy messages from temp logger
                    foreach (var line in tempConsoleLogger.GetLogContent())
                    {
                        fallbackLogger.AddLogLine(line);
                    }
                    
                    await fallbackLogger.SaveLogAsync();
                    
                    Console.WriteLine($"Error logs saved to: {fallbackLogDir}");
                }
                catch
                {
                    Console.WriteLine("Failed to save console logs due to critical error");
                }
                
                // Auto-exit with timeout on error (use default 10 seconds if appSettings is not available)
                Console.ForegroundColor = ConsoleColor.Yellow;
                int exitTimeout = 10; // Default timeout
                Console.WriteLine($"\nApplication will automatically close in {exitTimeout} seconds...");
                Console.ResetColor();
                await Task.Delay(exitTimeout * 1000);
            }
        }
    }
}
