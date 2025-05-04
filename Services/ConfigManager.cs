using File2CSVTransformer.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace File2CSVTransformer.Services
{
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private AppSettings? _appSettings;

        public ConfigManager(string configFilePath = "Config/appsettings.json")
        {
            _configFilePath = configFilePath;
        }

        public async Task<AppSettings> LoadConfigAsync()
        {
            if (_appSettings != null)
                return _appSettings;

            try
            {
                string jsonString = await File.ReadAllTextAsync(_configFilePath);
                _appSettings = JsonSerializer.Deserialize<AppSettings>(jsonString, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (_appSettings == null)
                    throw new InvalidOperationException("Failed to deserialize configuration settings.");

                ValidateSettings(_appSettings);
                return _appSettings;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading configuration: {ex.Message}", ex);
            }
        }

        private void ValidateSettings(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.InputDirectory))
                throw new InvalidOperationException("InputDirectory is required in the configuration.");

            if (string.IsNullOrWhiteSpace(settings.OutputDirectory))
                throw new InvalidOperationException("OutputDirectory is required in the configuration.");

            if (settings.HeaderColumns == null || settings.HeaderColumns.Count == 0)
                throw new InvalidOperationException("HeaderColumns are required in the configuration.");

            if (settings.Logs == null || string.IsNullOrWhiteSpace(settings.Logs.BaseDirectory))
                throw new InvalidOperationException("Logs.BaseDirectory is required in the configuration.");
            
            // FooterMarker is optional, so no validation needed

            // Create directories if they don't exist
            Directory.CreateDirectory(settings.InputDirectory);
            Directory.CreateDirectory(settings.OutputDirectory);
            
            // Create log directories
            string baseLogDir = settings.Logs.BaseDirectory;
            Directory.CreateDirectory(baseLogDir);
            
            string errorLogDir = Path.Combine(baseLogDir, settings.Logs.ErrorLogDirectory);
            string successLogDir = Path.Combine(baseLogDir, settings.Logs.SuccessLogDirectory);
            string consoleLogDir = Path.Combine(baseLogDir, settings.Logs.ConsoleLogDirectory);
            
            Directory.CreateDirectory(errorLogDir);
            Directory.CreateDirectory(successLogDir);
            Directory.CreateDirectory(consoleLogDir);
        }
    }
}