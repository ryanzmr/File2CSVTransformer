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
    }

    public class LinesToSkipSettings
    {
        public int Top { get; set; } = 1;
        public int Bottom { get; set; } = 5;
    }
}