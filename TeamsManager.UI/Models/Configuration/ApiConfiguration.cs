using System;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Model konfiguracji dla aplikacji API
    /// Przechowuje dane potrzebne do komunikacji z API
    /// </summary>
    public class ApiConfiguration
    {
        /// <summary>
        /// Identyfikator dzierżawy (tenant) Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator aplikacji (client ID) dla API w Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string ApiClientId { get; set; } = string.Empty;

        /// <summary>
        /// Zaszyfrowany klucz tajny (client secret) aplikacji API
        /// UWAGA: Przechowywany w formie zaszyfrowanej!
        /// </summary>
        public string ApiClientSecretEncrypted { get; set; } = string.Empty;

        /// <summary>
        /// URI aplikacji API (audience)
        /// Format: api://[CLIENT-ID-API]
        /// </summary>
        public string ApiAudience { get; set; } = string.Empty;

        /// <summary>
        /// Pełny zakres (scope) API
        /// Format: api://[CLIENT-ID-API]/access_as_user
        /// </summary>
        public string ApiScope { get; set; } = string.Empty;

        /// <summary>
        /// Bazowy URL do API
        /// Domyślnie: https://localhost:7037
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://localhost:7037";

        /// <summary>
        /// Data ostatniej modyfikacji konfiguracji
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Wersja formatu konfiguracji
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Sprawdza czy wszystkie wymagane pola są wypełnione
        /// </summary>
        public bool IsComplete()
        {
            return !string.IsNullOrWhiteSpace(TenantId) &&
                   !string.IsNullOrWhiteSpace(ApiClientId) &&
                   !string.IsNullOrWhiteSpace(ApiClientSecretEncrypted) &&
                   !string.IsNullOrWhiteSpace(ApiAudience) &&
                   !string.IsNullOrWhiteSpace(ApiScope) &&
                   !string.IsNullOrWhiteSpace(ApiBaseUrl);
        }
    }
}