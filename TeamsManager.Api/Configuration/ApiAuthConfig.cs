using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text.Json;

namespace TeamsManager.Api.Configuration
{
    public class ApiAuthConfig
    {
        // Konfiguracja Azure AD dla API
        public class ApiAzureAdConfig
        {
            public string Instance { get; set; } = "https://login.microsoftonline.com/";
            public string? TenantId { get; set; }
            public string? ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public string? Audience { get; set; }
        }

        // Główna klasa konfiguracyjna dla API
        public class ApiOAuthConfig
        {
            public ApiAzureAdConfig AzureAd { get; set; } = new ApiAzureAdConfig();
        }

        /// <summary>
        /// Wczytuje konfigurację OAuth dla API z systemu konfiguracji V2.0 (zastępuje stary appsettings.json)
        /// </summary>
        /// <param name="configuration">Dostawca konfiguracji ASP.NET Core (używany jako fallback).</param>
        /// <param name="skipValidation">Jeśli true, pomija walidację kompletności konfiguracji.</param>
        /// <returns>Skonfigurowany obiekt ApiOAuthConfig.</returns>
        public static ApiOAuthConfig LoadApiOAuthConfig(IConfiguration? configuration, bool skipValidation = false)
        {
            System.Diagnostics.Debug.WriteLine("OAuth Config (API): Wczytywanie konfiguracji z systemu V2.0.");

            var apiOAuthConfig = new ApiOAuthConfig();

            try
            {
                // NOWE: Próba wczytania z systemu konfiguracji V2.0
                var azureAdConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TeamsManager", "config", "azure-ad.json");

                if (File.Exists(azureAdConfigPath))
                {
                    System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Znaleziono plik konfiguracji V2.0: {azureAdConfigPath}");
                    
                    var jsonContent = File.ReadAllText(azureAdConfigPath);
                    
                    // Sprawdź czy plik jest zaszyfrowany (zawiera encryptedData, salt, iv)
                    if (jsonContent.Contains("\"encryptedData\"") && jsonContent.Contains("\"salt\""))
                    {
                        System.Diagnostics.Debug.WriteLine("OAuth Config (API): Wykryto zaszyfrowaną konfigurację - używam fallback do appsettings.json");
                        // Zaszyfrowane dane wymagają systemu V2.0 - użyj fallback
                        LoadFromAppSettings(configuration, apiOAuthConfig);
                    }
                    else
                    {
                        // Niezaszyfrowane dane - możemy bezpośrednio odczytać
                        var v2Config = JsonSerializer.Deserialize<V2AzureAdConfiguration>(jsonContent, new JsonSerializerOptions 
                        { 
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                        });
                        
                        if (v2Config != null)
                        {
                            apiOAuthConfig.AzureAd.TenantId = v2Config.TenantId;
                            apiOAuthConfig.AzureAd.ClientId = v2Config.Api?.ClientId;
                            apiOAuthConfig.AzureAd.ClientSecret = v2Config.Api?.ClientSecret;
                            apiOAuthConfig.AzureAd.Audience = v2Config.Api?.Audience;
                            
                            System.Diagnostics.Debug.WriteLine("OAuth Config (API): Pomyślnie załadowano z systemu V2.0");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("OAuth Config (API): Błąd deserializacji V2.0 - używam fallback");
                            LoadFromAppSettings(configuration, apiOAuthConfig);
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OAuth Config (API): Brak pliku V2.0 - używam fallback do appsettings.json");
                    LoadFromAppSettings(configuration, apiOAuthConfig);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OAuth Config (API): Błąd podczas ładowania V2.0: {ex.Message} - używam fallback");
                LoadFromAppSettings(configuration, apiOAuthConfig);
            }

            // Logowanie wczytanych wartości dla celów diagnostycznych
            System.Diagnostics.Debug.WriteLine(
                $"OAuth Config (API) Loaded: Instance='{apiOAuthConfig.AzureAd.Instance}', " +
                $"TenantId='{apiOAuthConfig.AzureAd.TenantId}', " +
                $"ClientId (API's own for OBO)='{apiOAuthConfig.AzureAd.ClientId}', " +
                $"Audience (for incoming tokens)='{apiOAuthConfig.AzureAd.Audience}', " +
                $"ClientSecret jest {(string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) ? "NIEUSTAWIONY" : "potencjalnie")} ustawiony.");

            // Walidacja wczytanej konfiguracji
            if (!skipValidation &&
                (string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.TenantId) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientId) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.Audience)))
            {
                var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI API] Kluczowe wartości AzureAd (TenantId, ClientId, ClientSecret, Audience) " +
                                   "nie zostały w pełni skonfigurowane dla API w systemie V2.0 ani w appsettings.json. " +
                                   "Uwierzytelnianie JWT i/lub przepływ On-Behalf-Of mogą nie działać poprawnie. " +
                                   "Skonfiguruj aplikację przez UI TeamsManager.";
                Console.Error.WriteLine(errorMessage);
                System.Diagnostics.Debug.WriteLine(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            return apiOAuthConfig;
        }

        private static void LoadFromAppSettings(IConfiguration? configuration, ApiOAuthConfig apiOAuthConfig)
        {
            // Fallback do starego sposobu ładowania z appsettings.json
            if (configuration != null)
            {
                configuration.GetSection("AzureAd").Bind(apiOAuthConfig.AzureAd);
            }
        }

        // Klasy pomocnicze do deserializacji konfiguracji V2.0
        private class V2AzureAdConfiguration
        {
            public string TenantId { get; set; } = string.Empty;
            public V2ApiClientSettings? Api { get; set; }
        }

        private class V2ApiClientSettings
        {
            public string ClientId { get; set; } = string.Empty;
            public string ClientSecret { get; set; } = string.Empty;
            public string Audience { get; set; } = string.Empty;
        }
    }
}