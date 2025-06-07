using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.Configuration;

namespace TeamsManager.UI.Services.Configuration
{
    /// <summary>
    /// Interfejs providera konfiguracji MSAL
    /// </summary>
    public interface IMsalConfigurationProvider
    {
        /// <summary>
        /// Pobiera konfigurację MSAL
        /// </summary>
        /// <returns>Konfiguracja MSAL</returns>
        MsalConfiguration GetConfiguration();
        
        /// <summary>
        /// Próbuje załadować konfigurację
        /// </summary>
        /// <param name="configuration">Załadowana konfiguracja</param>
        /// <returns>True jeśli udało się załadować poprawną konfigurację</returns>
        bool TryLoadConfiguration(out MsalConfiguration? configuration);
    }

    /// <summary>
    /// Provider konfiguracji MSAL wzorowany na LoadApiOAuthConfig z API
    /// </summary>
    public class MsalConfigurationProvider : IMsalConfigurationProvider
    {
        private readonly ILogger<MsalConfigurationProvider> _logger;
        private const string AppDataFolderName = "TeamsManager";
        private const string ConfigFileNameInAppData = "oauth_config.json";
        private const string DeveloperConfigFileName = "msalconfig.developer.json";

        public MsalConfigurationProvider(ILogger<MsalConfigurationProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MsalConfiguration GetConfiguration()
        {
            if (TryLoadConfiguration(out var config) && config != null)
            {
                return config;
            }

            _logger.LogError("Failed to load MSAL configuration from any source");
            return new MsalConfiguration(); // Pusta konfiguracja
        }

        public bool TryLoadConfiguration(out MsalConfiguration? configuration)
        {
            // 1. Spróbuj z AppData
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataFolderName,
                ConfigFileNameInAppData);

            if (TryLoadFromFile(appDataPath, out configuration))
            {
                _logger.LogInformation("Loaded MSAL config from AppData: {Path}", appDataPath);
                return true;
            }

            // 2. Spróbuj z pliku deweloperskiego
            if (TryLoadFromFile(DeveloperConfigFileName, out configuration))
            {
                _logger.LogInformation("Loaded MSAL config from developer file: {Path}", DeveloperConfigFileName);
                
                // Opcjonalnie skopiuj do AppData
                TryCopyToAppData(configuration!, appDataPath);
                return true;
            }

            configuration = null;
            return false;
        }

        private bool TryLoadFromFile(string path, out MsalConfiguration? configuration)
        {
            try
            {
                if (!File.Exists(path))
                {
                    configuration = null;
                    return false;
                }

                var json = File.ReadAllText(path);
                
                // Spróbuj załadować jako MsalConfiguration
                try
                {
                    configuration = JsonSerializer.Deserialize<MsalConfiguration>(json, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (configuration != null && configuration.IsValid())
                    {
                        _logger.LogDebug("Loaded as MsalConfiguration from {Path}", path);
                        return true;
                    }
                }
                catch (JsonException)
                {
                    _logger.LogDebug("Failed to deserialize as MsalConfiguration, trying legacy format");
                }
                
                // Spróbuj jako legacy format
                try
                {
                    var legacy = JsonSerializer.Deserialize<TeamsManager.UI.Services.MsalUiAppConfiguration>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (legacy != null)
                    {
                        configuration = MsalConfiguration.FromLegacyConfig(legacy);
                        if (configuration.IsValid())
                        {
                            _logger.LogDebug("Loaded and converted legacy configuration from {Path}", path);
                            return true;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize legacy format from {Path}", path);
                }

                configuration = null;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading config from {Path}", path);
                configuration = null;
                return false;
            }
        }

        private void TryCopyToAppData(MsalConfiguration config, string targetPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(config, 
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(targetPath, json);
                
                _logger.LogInformation("Copied config to AppData: {Path}", targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy config to AppData");
            }
        }
    }
} 