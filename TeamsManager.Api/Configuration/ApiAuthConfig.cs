using Microsoft.Extensions.Configuration; // Dla IConfiguration
using System; // Dla ArgumentNullException
// Usunięto using System.IO i System.Text.Json, bo nie będą już potrzebne do czytania pliku AppData

namespace TeamsManager.Api.Configuration
{
    public class ApiAuthConfig
    {
        // Klasa przechowująca konfigurację Azure AD dla API
        public class ApiAzureAdConfig
        {
            public string Instance { get; set; } = "https://login.microsoftonline.com/";
            public string? TenantId { get; set; }
            public string? ClientId { get; set; }     // Client ID aplikacji API (używany przez API do OBO)
            public string? ClientSecret { get; set; } // Client Secret aplikacji API (używany przez API do OBO)
            public string? Audience { get; set; }     // Audience (np. App ID URI aplikacji API), na który wystawiane są tokeny przez UI
        }

        // Główna klasa konfiguracyjna dla API, może być rozszerzona w przyszłości
        public class ApiOAuthConfig
        {
            public ApiAzureAdConfig AzureAd { get; set; } = new ApiAzureAdConfig();
        }

        // Usunięto klasy UiMsalConfigForApiDeserialization i UiAzureAdConfigForApiDeserialization,
        // ponieważ API nie będzie już czytać pliku konfiguracyjnego UI.

        /// <summary>
        /// Wczytuje konfigurację OAuth specyficzną dla API bezpośrednio z IConfiguration
        /// (czyli z appsettings.json, User Secrets, zmiennych środowiskowych itp.).
        /// </summary>
        /// <param name="configuration">Dostawca konfiguracji ASP.NET Core.</param>
        /// <param name="skipValidation">Jeśli true, pomija walidację kompletności konfiguracji (przydatne dla testów).</param>
        /// <returns>Skonfigurowany obiekt ApiOAuthConfig.</returns>
        public static ApiOAuthConfig LoadApiOAuthConfig(IConfiguration? configuration, bool skipValidation = false)
        {
            System.Diagnostics.Debug.WriteLine("OAuth Config (API): Wczytywanie konfiguracji z IConfiguration (appsettings.json / User Secrets).");

            var apiOAuthConfig = new ApiOAuthConfig();

            // Jeśli configuration jest null, zwracamy domyślną konfigurację
            if (configuration != null)
            {
                // Bindowanie całej sekcji "AzureAd" do obiektu apiOAuthConfig.AzureAd
                configuration.GetSection("AzureAd").Bind(apiOAuthConfig.AzureAd);
            }

            // Logowanie wczytanych wartości dla celów diagnostycznych (ClientSecret nie jest logowany)
            System.Diagnostics.Debug.WriteLine(
                $"OAuth Config (API) Loaded: Instance='{apiOAuthConfig.AzureAd.Instance}', " +
                $"TenantId='{apiOAuthConfig.AzureAd.TenantId}', " +
                $"ClientId (API's own for OBO)='{apiOAuthConfig.AzureAd.ClientId}', " +
                $"Audience (for incoming tokens)='{apiOAuthConfig.AzureAd.Audience}', " +
                $"ClientSecret is {(string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) ? "NOT" : "potentially")} set (checked from User Secrets/env vars).");

            // Podstawowa walidacja wczytanej konfiguracji (pomijana jeśli skipValidation = true)
            if (!skipValidation &&
                (string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.TenantId) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientId) || // ClientID API potrzebny do OBO
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) || // ClientSecret API potrzebny do OBO
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.Audience))) // Audience, którego API oczekuje w tokenach od UI
            {
                var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI API] Kluczowe wartości AzureAd (TenantId, ClientId, ClientSecret, Audience) " +
                                   "nie zostały w pełni skonfigurowane dla API w appsettings.json lub User Secrets. " +
                                   "Uwierzytelnianie JWT i/lub przepływ On-Behalf-Of mogą nie działać poprawnie.";
                Console.Error.WriteLine(errorMessage);
                System.Diagnostics.Debug.WriteLine(errorMessage);
                // Rzucenie wyjątku zatrzyma start aplikacji, jeśli te wartości są absolutnie krytyczne.
                throw new InvalidOperationException(errorMessage);
            }

            return apiOAuthConfig;
        }
    }
}