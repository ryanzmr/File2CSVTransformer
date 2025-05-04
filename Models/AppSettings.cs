using System.Collections.Generic;

namespace File2CSVTransformer.Models
{
    public class AppSettings
    {
        public string InputDirectory { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
        public string Delimiter { get; set; } = ",";
        public List<string> HeaderColumns { get; set; } = new List<string>();
        public LinesToSkipSettings LinesToSkip { get; set; } = new LinesToSkipSettings();
        public LogSettings Logs { get; set; } = new LogSettings();
        public string FooterMarker { get; set; } = string.Empty;
        public int AutoExitTimeoutSeconds { get; set; } = 10; // Default to 10 seconds if not specified
        public List<string> SupportedFileExtensions { get; set; } = new List<string> { ".txt" }; // Default to .txt only
    }

    public class LogSettings
    {
        public string BaseDirectory { get; set; } = string.Empty;
        public string ErrorLogDirectory { get; set; } = "Errors";
        public string SuccessLogDirectory { get; set; } = "Success";
        public string ConsoleLogDirectory { get; set; } = "ConsoleLog";
        public bool EnableDetailedLogging { get; set; } = false;
        public int MaxLogRetentionDays { get; set; } = 30;
    }

    public class LinesToSkipSettings
    {
        public int Top { get; set; } = 1;
        public int Bottom { get; set; } = 5;
    }
}