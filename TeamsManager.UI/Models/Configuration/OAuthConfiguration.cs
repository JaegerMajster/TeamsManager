using System;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Model konfiguracji OAuth dla aplikacji UI
    /// Przechowuje dane potrzebne do logowania użytkowników
    /// </summary>
    public class OAuthConfiguration
    {
        /// <summary>
        /// Identyfikator dzierżawy (tenant) Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator aplikacji (client ID) dla UI w Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Adres URL instancji Azure AD
        /// Domyślnie: https://login.microsoftonline.com/
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";

        /// <summary>
        /// URI przekierowania po zalogowaniu
        /// Domyślnie: http://localhost
        /// </summary>
        public string RedirectUri { get; set; } = "http://localhost";

        /// <summary>
        /// Zakres (scope) API do którego aplikacja potrzebuje dostępu
        /// Format: api://[CLIENT-ID-API]/access_as_user
        /// </summary>
        public string ApiScope { get; set; } = string.Empty;

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
                   !string.IsNullOrWhiteSpace(ClientId) &&
                   !string.IsNullOrWhiteSpace(ApiScope) &&
                   !string.IsNullOrWhiteSpace(Instance) &&
                   !string.IsNullOrWhiteSpace(RedirectUri);
        }
    }
}