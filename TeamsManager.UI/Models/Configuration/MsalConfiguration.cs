using System.Linq;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Konfiguracja MSAL dla aplikacji UI.
    /// Wzorowana na wzorcach konfiguracji z TeamsManager.Api.Configuration.ApiAuthConfig
    /// </summary>
    public class MsalConfiguration
    {
        /// <summary>
        /// Ustawienia Azure AD
        /// </summary>
        public AzureAdSettings AzureAd { get; set; } = new();
        
        /// <summary>
        /// Scopes wymagane przez aplikację
        /// </summary>
        public string[] Scopes { get; set; } = new[] { "User.Read" };

        /// <summary>
        /// Sprawdza czy konfiguracja jest kompletna i poprawna
        /// </summary>
        public bool IsValid()
        {
            return AzureAd != null &&
                   !string.IsNullOrWhiteSpace(AzureAd.ClientId) &&
                   !string.IsNullOrWhiteSpace(AzureAd.TenantId) &&
                   Scopes != null &&
                   Scopes.Length > 0 &&
                   Scopes.Any(s => !string.IsNullOrWhiteSpace(s));
        }

        /// <summary>
        /// Mapowanie z legacy konfiguracji MsalUiAppConfiguration
        /// </summary>
        /// <param name="legacy">Legacy konfiguracja</param>
        /// <returns>Zmapowana nowoczesna konfiguracja</returns>
        public static MsalConfiguration FromLegacyConfig(TeamsManager.UI.Services.MsalUiAppConfiguration legacy)
        {
            return new MsalConfiguration
            {
                AzureAd = new AzureAdSettings
                {
                    ClientId = legacy.AzureAd.ClientId,
                    TenantId = legacy.AzureAd.TenantId,
                    Instance = legacy.AzureAd.Instance ?? "https://login.microsoftonline.com/",
                    RedirectUri = legacy.AzureAd.RedirectUri,
                    ApiScope = legacy.AzureAd.ApiScope,
                    ApiBaseUrl = legacy.AzureAd.ApiBaseUrl
                },
                Scopes = legacy.Scopes ?? new[] { "User.Read" }
            };
        }
    }

    /// <summary>
    /// Ustawienia Azure AD dla aplikacji UI
    /// </summary>
    public class AzureAdSettings
    {
        /// <summary>
        /// Instancja Azure AD (domyślnie: https://login.microsoftonline.com/)
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";
        
        /// <summary>
        /// Identyfikator tenanta Azure AD
        /// </summary>
        public string? TenantId { get; set; }
        
        /// <summary>
        /// Identyfikator klienta aplikacji
        /// </summary>
        public string? ClientId { get; set; }
        
        /// <summary>
        /// URI przekierowania po autentykacji
        /// </summary>
        public string? RedirectUri { get; set; }
        
        /// <summary>
        /// Scope API dla komunikacji z backendem
        /// </summary>
        public string? ApiScope { get; set; }
        
        /// <summary>
        /// Bazowy URL API
        /// </summary>
        public string? ApiBaseUrl { get; set; }
    }
} 