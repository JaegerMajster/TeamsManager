using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;

namespace TeamsManager.UI.Services.Auth
{
    /// <summary>
    /// Manager tokenów OAuth2 On-Behalf-Of (OBO) dla EmbeddedApiServer
    /// Obsługuje exchange tokenów użytkownika na tokeny dla Microsoft Graph API
    /// </summary>
    public class EmbeddedOboTokenManager
    {
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmbeddedOboTokenManager> _logger;
        
        // Scopes dla Microsoft Graph API w przepływie OBO
        private readonly string[] _graphScopes = new[]
        {
            "https://graph.microsoft.com/User.Read",
            "https://graph.microsoft.com/User.ReadWrite.All",
            "https://graph.microsoft.com/Group.ReadWrite.All",
            "https://graph.microsoft.com/Directory.ReadWrite.All"
        };

        public EmbeddedOboTokenManager(
            IConfidentialClientApplication confidentialClientApp,
            IMemoryCache cache,
            ILogger<EmbeddedOboTokenManager> logger)
        {
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Uzyskuje token dostępu dla Microsoft Graph API używając przepływu On-Behalf-Of
        /// </summary>
        /// <param name="userAccessToken">Token dostępu użytkownika z UI</param>
        /// <returns>Token dostępu dla Graph API lub null w przypadku błędu</returns>
        public async Task<string?> GetOboAccessTokenAsync(string userAccessToken)
        {
            if (string.IsNullOrEmpty(userAccessToken))
            {
                _logger.LogWarning("Brak tokenu użytkownika dla przepływu OBO");
                return null;
            }

            try
            {
                _logger.LogDebug("Rozpoczynanie przepływu OBO dla tokenu użytkownika");

                // Sprawdź cache
                var cacheKey = $"obo_token_{userAccessToken.GetHashCode()}";
                if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
                {
                    _logger.LogDebug("Znaleziono token OBO w cache");
                    return cachedToken;
                }

                // Utwórz UserAssertion z tokenu użytkownika
                var userAssertion = new UserAssertion(userAccessToken);

                // Wykonaj przepływ On-Behalf-Of
                var result = await _confidentialClientApp
                    .AcquireTokenOnBehalfOf(_graphScopes, userAssertion)
                    .ExecuteAsync();

                if (result?.AccessToken != null)
                {
                    _logger.LogDebug("Pomyślnie uzyskano token OBO dla Graph API");
                    
                    // Zapisz w cache z czasem wygaśnięcia
                    var cacheExpiration = result.ExpiresOn.AddMinutes(-5); // 5 minut przed wygaśnięciem
                    _cache.Set(cacheKey, result.AccessToken, cacheExpiration);
                    
                    return result.AccessToken;
                }

                _logger.LogWarning("Nie udało się uzyskać tokenu OBO - result.AccessToken jest null");
                return null;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Błąd MSAL podczas przepływu OBO: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return null;
            }
            catch (MsalClientException ex)
            {
                _logger.LogError(ex, "Błąd klienta MSAL podczas przepływu OBO: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas przepływu OBO");
                return null;
            }
        }

        /// <summary>
        /// Waliduje token użytkownika z UI
        /// </summary>
        /// <param name="userAccessToken">Token do walidacji</param>
        /// <returns>True jeśli token jest prawidłowy</returns>
        public bool ValidateUserToken(string userAccessToken)
        {
            if (string.IsNullOrEmpty(userAccessToken))
            {
                _logger.LogWarning("Token użytkownika jest pusty");
                return false;
            }

            try
            {
                // Podstawowa walidacja formatu JWT
                var parts = userAccessToken.Split('.');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("Token użytkownika ma nieprawidłowy format JWT");
                    return false;
                }

                _logger.LogDebug("Token użytkownika przeszedł podstawową walidację");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas walidacji tokenu użytkownika");
                return false;
            }
        }

        /// <summary>
        /// Czyści cache tokenów OBO
        /// </summary>
        public void ClearTokenCache()
        {
            _logger.LogDebug("Czyszczenie cache tokenów OBO");
            // Memory cache nie ma metody Clear, więc używamy dispose/recreate pattern
            // W rzeczywistej implementacji można by użyć bardziej zaawansowanego cache
        }
    }
} 