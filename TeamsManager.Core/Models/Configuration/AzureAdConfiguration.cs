namespace TeamsManager.Core.Models.Configuration
{
    /// <summary>
    /// Konfiguracja Azure Active Directory dla całego systemu
    /// Wspólna dla UI i API zgodnie z zasadami DRY
    /// </summary>
    public class AzureAdConfiguration : BaseConfiguration
    {
        /// <summary>
        /// Identyfikator tenanta Azure AD
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Instancja Azure AD (domyślnie publiczna chmura)
        /// </summary>
        public string Instance { get; set; } = "https://login.microsoftonline.com/";

        /// <summary>
        /// Konfiguracja klienta UI
        /// </summary>
        public UiClientSettings Ui { get; set; } = new();

        /// <summary>
        /// Konfiguracja klienta API
        /// </summary>
        public ApiClientSettings Api { get; set; } = new();

        /// <summary>
        /// Sprawdza czy konfiguracja Azure AD jest kompletna
        /// </summary>
        /// <returns>True jeśli wszystkie wymagane pola są wypełnione</returns>
        public override bool IsValid()
        {
            return base.IsValid() &&
                   !string.IsNullOrEmpty(TenantId) &&
                   !string.IsNullOrEmpty(Instance) &&
                   Ui.IsValid() &&
                   Api.IsValid();
        }
    }

    /// <summary>
    /// Ustawienia klienta UI (publiczny klient)
    /// </summary>
    public class UiClientSettings
    {
        /// <summary>
        /// Client ID aplikacji UI
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Czy używać Windows Hello/WAM
        /// </summary>
        public bool UseWindowsHello { get; set; } = false;

        /// <summary>
        /// Sprawdza czy ustawienia UI są prawidłowe
        /// </summary>
        /// <returns>True jeśli ClientId jest ustawiony</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ClientId);
        }
    }

    /// <summary>
    /// Ustawienia klienta API (poufny klient)
    /// </summary>
    public class ApiClientSettings
    {
        /// <summary>
        /// Client ID aplikacji API
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Client Secret aplikacji API
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Audience dla tokenów (identyfikator zasobu API)
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Scope dla tokenów API (domyślnie taki sam jak Audience)
        /// </summary>
        public string ApiScope { get; set; } = string.Empty;

        /// <summary>
        /// Sprawdza czy ustawienia API są prawidłowe
        /// </summary>
        /// <returns>True jeśli wszystkie wymagane pola są wypełnione</returns>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ClientId) &&
                   !string.IsNullOrEmpty(ClientSecret) &&
                   !string.IsNullOrEmpty(Audience) &&
                   !string.IsNullOrEmpty(ApiScope);
        }
    }
} 