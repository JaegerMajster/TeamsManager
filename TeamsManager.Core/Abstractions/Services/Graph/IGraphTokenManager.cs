using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services.Auth;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Rozszerzony interfejs zarządzania tokenami specyficzny dla Microsoft Graph API.
    /// Dziedziczy z ITokenManager i dodaje funkcjonalności specyficzne dla Graph API.
    /// </summary>
    public interface IGraphTokenManager : ITokenManager
    {
        /// <summary>
        /// Pobiera ważny token dostępu do Microsoft Graph API.
        /// Automatycznie sprawdza ważność i odświeża token jeśli potrzeba.
        /// </summary>
        /// <returns>Ważny token dostępu lub null jeśli nie można uzyskać tokenu</returns>
        Task<string?> GetValidGraphTokenAsync();

        /// <summary>
        /// Zapewnia ważny token dostępu do Microsoft Graph API.
        /// Sprawdza ważność tokenu i odświeża go jeśli to konieczne.
        /// </summary>
        /// <returns>True jeśli token jest ważny lub został pomyślnie odświeżony</returns>
        Task<bool> EnsureValidGraphTokenAsync();

        /// <summary>
        /// Sprawdza czy aktualny token Graph API jest ważny i nie wygasł.
        /// </summary>
        /// <returns>True jeśli token jest ważny</returns>
        Task<bool> IsGraphTokenValidAsync();

        /// <summary>
        /// Odświeża token dostępu do Microsoft Graph API.
        /// </summary>
        /// <returns>True jeśli token został pomyślnie odświeżony</returns>
        Task<bool> RefreshGraphTokenAsync();

        /// <summary>
        /// Pobiera informacje o aktualnym tokenie Graph API (czas wygaśnięcia, zakresy, itp.).
        /// </summary>
        /// <returns>Informacje o tokenie lub null jeśli token nie istnieje</returns>
        Task<GraphTokenInfo?> GetGraphTokenInfoAsync();

        /// <summary>
        /// Unieważnia aktualny token Graph API w cache.
        /// Wymusza pobranie nowego tokenu przy następnym żądaniu.
        /// </summary>
        Task InvalidateGraphTokenAsync();
    }

    /// <summary>
    /// Informacje o tokenie Microsoft Graph API
    /// </summary>
    public class GraphTokenInfo
    {
        /// <summary>
        /// Czas wygaśnięcia tokenu
        /// </summary>
        public DateTime ExpiresOn { get; set; }

        /// <summary>
        /// Zakresy uprawnień tokenu
        /// </summary>
        public string[] Scopes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Czy token jest ważny (nie wygasł)
        /// </summary>
        public bool IsValid => ExpiresOn > DateTime.UtcNow.AddMinutes(5);

        /// <summary>
        /// Pozostały czas do wygaśnięcia tokenu
        /// </summary>
        public TimeSpan TimeToExpiry => ExpiresOn - DateTime.UtcNow;

        /// <summary>
        /// Identyfikator użytkownika/aplikacji
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// Tenant ID
        /// </summary>
        public string? TenantId { get; set; }
    }
} 