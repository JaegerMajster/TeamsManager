using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TeamsManager.UI.Models.Configuration;

namespace TeamsManager.UI.Services.Configuration
{
    /// <summary>
    /// Główny serwis do zarządzania konfiguracją aplikacji
    /// Odpowiada za zapis i odczyt plików konfiguracyjnych
    /// </summary>
    public class ConfigurationManager
    {
        private readonly string _appDataPath;
        private readonly EncryptionService _encryptionService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigurationManager()
        {
            // Ustal ścieżkę do folderu konfiguracji
            _appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TeamsManager"
            );

            // Inicjalizuj serwis szyfrowania
            _encryptionService = new EncryptionService();

            // Konfiguracja serializacji JSON
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,  // Ładne formatowanie JSON
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Upewnij się że folder istnieje
            EnsureDirectoryExists();
        }

        /// <summary>
        /// Upewnia się że folder konfiguracji istnieje
        /// </summary>
        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
                System.Diagnostics.Debug.WriteLine($"Utworzono folder konfiguracji: {_appDataPath}");
            }
        }

        #region OAuth Configuration (UI)

        /// <summary>
        /// Wczytuje konfigurację OAuth dla aplikacji UI
        /// </summary>
        public async Task<OAuthConfiguration?> LoadOAuthConfigAsync()
        {
            var filePath = Path.Combine(_appDataPath, "oauth_config.json");

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Plik konfiguracji OAuth nie istnieje: {filePath}");
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<OAuthConfiguration>(json, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Wczytano konfigurację OAuth z: {filePath}");
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania konfiguracji OAuth: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Zapisuje konfigurację OAuth dla aplikacji UI
        /// </summary>
        public async Task SaveOAuthConfigAsync(OAuthConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            config.LastModified = DateTime.Now;

            var filePath = Path.Combine(_appDataPath, "oauth_config.json");
            var json = JsonSerializer.Serialize(config, _jsonOptions);

            await File.WriteAllTextAsync(filePath, json);
            System.Diagnostics.Debug.WriteLine($"Zapisano konfigurację OAuth do: {filePath}");
        }

        #endregion

        #region API Configuration

        /// <summary>
        /// Wczytuje konfigurację API
        /// </summary>
        public async Task<ApiConfiguration?> LoadApiConfigAsync()
        {
            var filePath = Path.Combine(_appDataPath, "api_config.json");

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Plik konfiguracji API nie istnieje: {filePath}");
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<ApiConfiguration>(json, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Wczytano konfigurację API z: {filePath}");
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania konfiguracji API: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Zapisuje konfigurację API z szyfrowaniem secret
        /// </summary>
        /// <param name="config">Konfiguracja do zapisania</param>
        /// <param name="plainSecret">Niezaszyfrowany secret (będzie zaszyfrowany przed zapisem)</param>
        public async Task SaveApiConfigAsync(ApiConfiguration config, string plainSecret)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Zaszyfruj secret przed zapisem
            if (!string.IsNullOrEmpty(plainSecret))
            {
                config.ApiClientSecretEncrypted = _encryptionService.Encrypt(plainSecret);
            }

            config.LastModified = DateTime.Now;

            var filePath = Path.Combine(_appDataPath, "api_config.json");
            var json = JsonSerializer.Serialize(config, _jsonOptions);

            await File.WriteAllTextAsync(filePath, json);
            System.Diagnostics.Debug.WriteLine($"Zapisano konfigurację API do: {filePath}");
        }

        /// <summary>
        /// Odszyfrowuje secret API
        /// </summary>
        public string DecryptApiSecret(string encryptedSecret)
        {
            if (string.IsNullOrEmpty(encryptedSecret))
                return string.Empty;

            return _encryptionService.Decrypt(encryptedSecret);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Sprawdza czy jakakolwiek konfiguracja istnieje
        /// </summary>
        public bool ConfigurationExists()
        {
            var oauthPath = Path.Combine(_appDataPath, "oauth_config.json");
            var apiPath = Path.Combine(_appDataPath, "api_config.json");

            return File.Exists(oauthPath) && File.Exists(apiPath);
        }

        /// <summary>
        /// Usuwa wszystkie pliki konfiguracyjne (przydatne do testów)
        /// </summary>
        public async Task ClearAllConfigurationAsync()
        {
            var oauthPath = Path.Combine(_appDataPath, "oauth_config.json");
            var apiPath = Path.Combine(_appDataPath, "api_config.json");

            if (File.Exists(oauthPath))
            {
                File.Delete(oauthPath);
                System.Diagnostics.Debug.WriteLine("Usunięto konfigurację OAuth");
            }

            if (File.Exists(apiPath))
            {
                File.Delete(apiPath);
                System.Diagnostics.Debug.WriteLine("Usunięto konfigurację API");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Pobiera ścieżkę do folderu konfiguracji
        /// </summary>
        public string GetConfigurationPath()
        {
            return _appDataPath;
        }

        #endregion
    }
}