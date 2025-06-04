using System;
using System.Collections.Generic;

namespace TeamsManager.UI.Models.Configuration
{
    /// <summary>
    /// Model konfiguracji OAuth dla aplikacji UI
    /// Przechowuje dane potrzebne do logowania użytkowników
    /// </summary>
    public class OAuthConfiguration
    {
        /// <summary>
        /// Lista zakresów (scopes) dostępu
        /// </summary>
        public List<string> Scopes { get; set; } = new List<string>();

        /// <summary>
        /// Konfiguracja Azure AD
        /// </summary>
        public AzureAdConfiguration AzureAd { get; set; } = new AzureAdConfiguration();

        /// <summary>
        /// Sprawdza czy wszystkie wymagane pola są wypełnione
        /// </summary>
        public bool IsComplete()
        {
            return Scopes.Count > 0 && AzureAd.IsComplete();
        }
    }

    /// <summary>
    /// Konfiguracja Azure AD
    /// </summary>
    public class AzureAdConfiguration
    {
        /// <summary>
        /// Identyfikator aplikacji (client ID) dla UI w Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// URL bazowy API
        /// </summary>
        public string ApiBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator dzierżawy (tenant) Azure AD
        /// Format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// URI przekierowania po zalogowaniu
        /// Domyślnie: http://localhost
        /// </summary>
        public string RedirectUri { get; set; } = "http://localhost";

        /// <summary>
        /// Adres URL instancji Azure AD
        /// Domyślnie: https://login.microsoftonline.com/
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";

        /// <summary>
        /// Zakres (scope) API do którego aplikacja potrzebuje dostępu
        /// Format: api://[CLIENT-ID-API]/access_as_user
        /// </summary>
        public string ApiScope { get; set; } = string.Empty;

        /// <summary>
        /// Sprawdza czy wszystkie wymagane pola są wypełnione
        /// </summary>
        public bool IsComplete()
        {
            return !string.IsNullOrWhiteSpace(TenantId) &&
                   !string.IsNullOrWhiteSpace(ClientId) &&
                   !string.IsNullOrWhiteSpace(ApiScope) &&
                   !string.IsNullOrWhiteSpace(Instance) &&
                   !string.IsNullOrWhiteSpace(RedirectUri) &&
                   !string.IsNullOrWhiteSpace(ApiBaseUrl);
        }
    }
}