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
                WriteIndented = true  // Ładne formatowanie JSON
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
                System.Diagnostics.Debug.WriteLine($"Odczytano JSON z pliku: {filePath}");
                System.Diagnostics.Debug.WriteLine($"Zawartość: {json}");
                
                var config = JsonSerializer.Deserialize<OAuthConfiguration>(json, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Deserializacja - sukces: {config != null}");
                
                if (config != null)
                {
                    System.Diagnostics.Debug.WriteLine($"AzureAd sekcja: {config.AzureAd != null}");
                    if (config.AzureAd != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ClientId: '{config.AzureAd.ClientId}'");
                        System.Diagnostics.Debug.WriteLine($"TenantId: '{config.AzureAd.TenantId}'");
                    }
                    System.Diagnostics.Debug.WriteLine($"Scopes count: {config.Scopes?.Count ?? 0}");
                }
                
                System.Diagnostics.Debug.WriteLine($"Wczytano konfigurację OAuth z: {filePath}");
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania konfiguracji OAuth: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Szczegóły błędu: {ex}");
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

        #region Login Settings

        /// <summary>
        /// Wczytuje ustawienia logowania
        /// </summary>
        public async Task<LoginSettings?> LoadLoginSettingsAsync()
        {
            var filePath = Path.Combine(_appDataPath, "login_settings.json");
            
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Plik ustawień logowania nie istnieje: {filePath}");
                return new LoginSettings(); // Zwróć domyślne ustawienia
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var settings = JsonSerializer.Deserialize<LoginSettings>(json, _jsonOptions);
                
                // Odszyfruj refresh token jeśli istnieje
                if (!string.IsNullOrEmpty(settings?.EncryptedRefreshToken))
                {
                    // Token pozostaje zaszyfrowany w modelu, odszyfrowanie na żądanie
                    System.Diagnostics.Debug.WriteLine("Wczytano ustawienia logowania z zaszyfrowanym tokenem");
                }
                
                return settings ?? new LoginSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wczytywania ustawień logowania: {ex.Message}");
                return new LoginSettings();
            }
        }

        /// <summary>
        /// Zapisuje ustawienia logowania
        /// </summary>
        public async Task SaveLoginSettingsAsync(LoginSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            var filePath = Path.Combine(_appDataPath, "login_settings.json");
            
            // Nie zapisuj refresh token jeśli użytkownik nie chce być zapamiętany
            if (!settings.RememberMe)
            {
                settings.EncryptedRefreshToken = null;
                settings.LastUserEmail = null;
            }
            
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            System.Diagnostics.Debug.WriteLine($"Zapisano ustawienia logowania do: {filePath}");
        }

        /// <summary>
        /// Czyści zapisane dane logowania
        /// </summary>
        public async Task ClearLoginSettingsAsync()
        {
            var filePath = Path.Combine(_appDataPath, "login_settings.json");
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine("Usunięto zapisane ustawienia logowania");
            }
            
            await Task.CompletedTask;
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
            
            // Dla przepływu OBO potrzebujemy OBUS plików
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
        /// Usuwa wszystkie pliki konfiguracyjne (wersja synchroniczna)
        /// </summary>
        public void DeleteConfiguration()
        {
            try
            {
                var oauthPath = Path.Combine(_appDataPath, "oauth_config.json");
                var apiPath = Path.Combine(_appDataPath, "api_config.json");

                if (File.Exists(oauthPath))
                    File.Delete(oauthPath);

                if (File.Exists(apiPath))
                    File.Delete(apiPath);

                System.Diagnostics.Debug.WriteLine("Usunięto pliki konfiguracyjne");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Nie można usunąć plików konfiguracyjnych", ex);
            }
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