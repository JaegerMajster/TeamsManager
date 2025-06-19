using Microsoft.Extensions.Configuration;
using System;

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
        /// Wczytuje konfigurację OAuth dla API z IConfiguration
        /// (appsettings.json, User Secrets, zmienne środowiskowe).
        /// </summary>
        /// <param name="configuration">Dostawca konfiguracji ASP.NET Core.</param>
        /// <param name="skipValidation">Jeśli true, pomija walidację kompletności konfiguracji.</param>
        /// <returns>Skonfigurowany obiekt ApiOAuthConfig.</returns>
        public static ApiOAuthConfig LoadApiOAuthConfig(IConfiguration? configuration, bool skipValidation = false)
        {
            System.Diagnostics.Debug.WriteLine("OAuth Config (API): Wczytywanie konfiguracji z IConfiguration.");

            var apiOAuthConfig = new ApiOAuthConfig();

            // Jeśli configuration jest null, zwracamy domyślną konfigurację
            if (configuration != null)
            {
                // Bindowanie sekcji "AzureAd" do obiektu konfiguracji
                configuration.GetSection("AzureAd").Bind(apiOAuthConfig.AzureAd);
            }

            // Logowanie wczytanych wartości dla celów diagnostycznych
            System.Diagnostics.Debug.WriteLine(
                $"OAuth Config (API) Loaded: Instance='{apiOAuthConfig.AzureAd.Instance}', " +
                $"TenantId='{apiOAuthConfig.AzureAd.TenantId}', " +
                $"ClientId (API's own for OBO)='{apiOAuthConfig.AzureAd.ClientId}', " +
                $"Audience (for incoming tokens)='{apiOAuthConfig.AzureAd.Audience}', " +
                $"ClientSecret is {(string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) ? "NOT" : "potentially")} set.");

            // Walidacja wczytanej konfiguracji
            if (!skipValidation &&
                (string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.TenantId) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientId) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.ClientSecret) ||
                 string.IsNullOrWhiteSpace(apiOAuthConfig.AzureAd.Audience)))
            {
                var errorMessage = "[KRYTYCZNY BŁĄD KONFIGURACJI API] Kluczowe wartości AzureAd (TenantId, ClientId, ClientSecret, Audience) " +
                                   "nie zostały w pełni skonfigurowane dla API w appsettings.json lub User Secrets. " +
                                   "Uwierzytelnianie JWT i/lub przepływ On-Behalf-Of mogą nie działać poprawnie.";
                Console.Error.WriteLine(errorMessage);
                System.Diagnostics.Debug.WriteLine(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            return apiOAuthConfig;
        }
    }
}