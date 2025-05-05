using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace File2CSVTransformer.Services
{
    public class ConsoleLogger
    {
        private readonly string _logFilePath;
        private readonly StringBuilder _logContent = new StringBuilder();
        private readonly bool _echoToConsole;

        public ConsoleLogger(string consoleLogDirectory, string sessionId, bool echoToConsole = true)
        {
            if (!Directory.Exists(consoleLogDirectory))
            {
                Directory.CreateDirectory(consoleLogDirectory);
            }

            _logFilePath = Path.Combine(consoleLogDirectory, $"console_{sessionId}.log");
            _echoToConsole = echoToConsole;
        }

        public void Log(string message)
        {
            // Add timestamp to log file but not to console
            string timestampedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            _logContent.AppendLine(timestampedMessage);
            
            if (_echoToConsole)
            {
                Console.WriteLine(message);
            }
        }

        public void LogEmoji(string emoji, string message)
        {
            // Format with timestamp at the beginning for console output
            string timestamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
            string consoleMessage = $"{timestamp} {emoji} {message}";
            
            if (_echoToConsole)
            {
                Console.WriteLine(consoleMessage);
            }
            
            // Add to log file
            _logContent.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {emoji} {message}");
        }

        public void LogInfo(string message) => LogEmoji("ℹ️", message);
        public void LogSuccess(string message) => LogEmoji("✅", message);
        public void LogWarning(string message) => LogEmoji("⚠️", message);
        public void LogError(string message) => LogEmoji("❌", message);
        public void LogProcessing(string message) => LogEmoji("🔄", message);
        public void LogComplete(string message) => LogEmoji("🏁", message);
        public void LogTime(string message) => LogEmoji("⏱️", message);
        
        public void LogSeparator()
        {
            string separator = new string('-', 70);
            _logContent.AppendLine(separator);
            if (_echoToConsole)
            {
                Console.WriteLine(separator);
            }
        }

        public void LogWithProgressInfo(string fileName, int fileIndex, int totalFiles, string message)
        {
            string timestamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
            string progressInfo = $"[{fileIndex}/{totalFiles}]";
            string consoleMessage = $"{timestamp} 🔄 {progressInfo} {fileName} - {message}";
            
            if (_echoToConsole)
            {
                Console.WriteLine(consoleMessage);
            }
            
            // Add to log file
            _logContent.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 🔄 {progressInfo} {fileName} - {message}");
        }

        // New method to get all log content as a list of strings
        public List<string> GetLogContent()
        {
            var lines = new List<string>();
            using (StringReader reader = new StringReader(_logContent.ToString()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        // New method to add a log line directly (for copying from temporary logger)
        public void AddLogLine(string line)
        {
            _logContent.AppendLine(line);
        }

        public async Task SaveLogAsync()
        {
            try
            {
                await File.WriteAllTextAsync(_logFilePath, _logContent.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to save console log: {ex.Message}");
            }
        }
    }
}