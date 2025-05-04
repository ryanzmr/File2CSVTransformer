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
        public string LogDirectory { get; set; } = string.Empty;
        public string FooterMarker { get; set; } = string.Empty;
        public int AutoExitTimeoutSeconds { get; set; } = 10; // Default to 10 seconds if not specified
    }

    public class LinesToSkipSettings
    {
        public int Top { get; set; } = 1;
        public int Bottom { get; set; } = 5;
    }
}