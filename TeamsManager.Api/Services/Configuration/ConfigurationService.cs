using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models.Configuration;

namespace TeamsManager.Api.Services.Configuration
{
    /// <summary>
    /// Implementacja serwisu konfiguracji dla API
    /// Używa tego samego systemu co UI - pliki w %APPDATA%\TeamsManager\config
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly AdvancedEncryptionService _encryption;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly string _configPath;

        public event EventHandler<Core.Abstractions.Services.ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ConfigurationService(
            AdvancedEncryptionService encryption,
            ILogger<ConfigurationService> logger)
        {
            _encryption = encryption ?? throw new ArgumentNullException(nameof(encryption));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager", "config");
            
            EnsureDirectoryExists();
        }

        public async Task<T?> GetConfigurationAsync<T>(string configurationName) where T : class
        {
            try
            {
                _logger.LogInformation("Pobieranie konfiguracji {ConfigurationName} typu {Type}", configurationName, typeof(T).Name);

                var filePath = GetConfigFilePath(configurationName);
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Plik konfiguracji {ConfigurationName} nie istnieje", configurationName);
                    return null;
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                
                // Sprawdź czy plik jest zaszyfrowany
                if (IsEncrypted(configurationName))
                {
                    _logger.LogInformation("Odczytywanie zaszyfrowanego pliku {ConfigurationName}", configurationName);
                    
                    var encryptedData = JsonSerializer.Deserialize<EncryptedData>(jsonContent);
                    
                    if (encryptedData != null)
                    {
                        var decryptedJson = _encryption.Decrypt(encryptedData);
                        
                        var jsonOptions = new JsonSerializerOptions 
                        { 
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };
                        
                        return JsonSerializer.Deserialize<T>(decryptedJson, jsonOptions);
                    }
                }
                else
                {
                    var jsonOptions = new JsonSerializerOptions 
                    { 
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    
                    return JsonSerializer.Deserialize<T>(jsonContent, jsonOptions);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task SaveConfigurationAsync<T>(string configurationName, T configuration) where T : class
        {
            try
            {
                _logger.LogInformation("Zapisywanie konfiguracji {ConfigurationName} typu {Type}", configurationName, typeof(T).Name);
                
                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                // Aktualizuj timestamp jeśli to BaseConfiguration
                if (configuration is BaseConfiguration baseConfig)
                {
                    baseConfig.Touch();
                }

                var filePath = GetConfigFilePath(configurationName);
                
                var jsonOptions = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string jsonContent;

                // Sprawdź czy plik powinien być zaszyfrowany
                if (IsEncrypted(configurationName))
                {
                    _logger.LogInformation("Konfiguracja {ConfigurationName} wymaga szyfrowania", configurationName);
                    var plainJson = JsonSerializer.Serialize(configuration, jsonOptions);
                    
                    var encryptedData = _encryption.Encrypt(plainJson);
                    jsonContent = JsonSerializer.Serialize(encryptedData, jsonOptions);
                }
                else
                {
                    _logger.LogInformation("Konfiguracja {ConfigurationName} nie wymaga szyfrowania", configurationName);
                    jsonContent = JsonSerializer.Serialize(configuration, jsonOptions);
                }

                await File.WriteAllTextAsync(filePath, jsonContent);
                _logger.LogInformation("Konfiguracja {ConfigurationName} zapisana pomyślnie", configurationName);

                // Wywołaj event
                var eventArgs = new Core.Abstractions.Services.ConfigurationChangedEventArgs(
                    configurationName, typeof(T));
                ConfigurationChanged?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task DeleteConfigurationAsync(string configurationName)
        {
            try
            {
                _logger.LogInformation("Usuwanie konfiguracji {ConfigurationName}", configurationName);

                var filePath = GetConfigFilePath(configurationName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Konfiguracja {ConfigurationName} usunięta pomyślnie", configurationName);

                    // Wywołaj event
                    var eventArgs = new Core.Abstractions.Services.ConfigurationChangedEventArgs(
                        configurationName, typeof(object));
                    ConfigurationChanged?.Invoke(this, eventArgs);
                }
                else
                {
                    _logger.LogWarning("Plik konfiguracji {ConfigurationName} nie istnieje", configurationName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task<bool> ConfigurationExistsAsync(string configurationName)
        {
            try
            {
                var filePath = GetConfigFilePath(configurationName);
                return File.Exists(filePath);
            }
            catch
            {
                return false;
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_configPath))
            {
                Directory.CreateDirectory(_configPath);
                _logger.LogInformation("Utworzono katalog konfiguracji: {ConfigPath}", _configPath);
            }
        }

        private string GetConfigFilePath(string configurationName)
        {
            return Path.Combine(_configPath, $"{configurationName}.json");
        }

        private bool IsEncrypted(string configurationName)
        {
            // Te konfiguracje są zawsze szyfrowane
            return configurationName.ToLowerInvariant() switch
            {
                "azure-ad" => true,
                "application" => false, // Konfiguracja aplikacji nie zawiera wrażliwych danych
                "login-settings" => false, // Ustawienia logowania też nie
                _ => false
            };
        }
    }
} 