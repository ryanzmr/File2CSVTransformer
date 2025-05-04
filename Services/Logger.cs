using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace File2CSVTransformer.Services
{
    public class Logger
    {
        private readonly string _successLogPath;
        private readonly string _errorLogPath;

        public Logger(string logDirectory)
        {
            _successLogPath = Path.Combine(logDirectory, "Success", $"success_{DateTime.Now:yyyyMMdd}.log");
            _errorLogPath = Path.Combine(logDirectory, "Errors", $"error_{DateTime.Now:yyyyMMdd}.log");
        }

        public async Task LogSuccessAsync(string fileName, int rowsProcessed, TimeSpan processingTime)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] File processed successfully: {fileName}");
            logMessage.AppendLine($"Rows processed: {rowsProcessed}");
            logMessage.AppendLine($"Processing time: {processingTime.TotalSeconds:F2} seconds");
            logMessage.AppendLine(new string('-', 50));

            await WriteToLogFileAsync(_successLogPath, logMessage.ToString());
        }

        public async Task LogErrorAsync(string fileName, string errorMessage, Exception? exception = null)
        {
            StringBuilder logMessage = new StringBuilder();
            logMessage.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error processing file: {fileName}");
            logMessage.AppendLine($"Error Message: {errorMessage}");
            
            if (exception != null)
            {
                logMessage.AppendLine($"Exception Type: {exception.GetType().Name}");
                logMessage.AppendLine($"Stack Trace: {exception.StackTrace}");
            }
            
            logMessage.AppendLine(new string('-', 50));

            await WriteToLogFileAsync(_errorLogPath, logMessage.ToString());
        }

        private async Task WriteToLogFileAsync(string logFilePath, string message)
        {
            try
            {
                await File.AppendAllTextAsync(logFilePath, message);
            }
            catch (Exception ex)
            {
                // If logging itself fails, write to console as a fallback
                Console.WriteLine($"Failed to write to log file {logFilePath}: {ex.Message}");
                Console.WriteLine(message);
            }
        }
    }
}