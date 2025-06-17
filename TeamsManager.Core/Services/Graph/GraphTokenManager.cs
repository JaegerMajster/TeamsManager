using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Services.Auth;
using TeamsManager.Core.Exceptions.Graph;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Implementacja rozszerzonego zarządzania tokenami dla Microsoft Graph API.
    /// Dziedziczy z TokenManager i dodaje funkcjonalności specyficzne dla Graph API.
    /// </summary>
    public class GraphTokenManager : TokenManager, IGraphTokenManager
    {
        private readonly ILogger<GraphTokenManager> _logger;
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly TeamsManager.Core.Models.Graph.GraphApiConfiguration _graphConfig;

        public GraphTokenManager(
            IConfidentialClientApplication confidentialClientApp,
            IMemoryCache memoryCache,
            ILogger<GraphTokenManager> logger,
            IConfiguration configuration,
            TeamsManager.Core.Models.Graph.GraphApiConfiguration? graphConfig = null)
            : base(confidentialClientApp, memoryCache, logger, configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _graphConfig = graphConfig ?? new TeamsManager.Core.Models.Graph.GraphApiConfiguration();
        }

        /// <summary>
        /// Pobiera ważny token dostępu do Microsoft Graph API.
        /// Automatycznie sprawdza ważność i odświeża token jeśli potrzeba.
        /// </summary>
        public async Task<string?> GetValidGraphTokenAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie ważnego tokenu Graph API...");

                // Sprawdź czy token jest ważny
                if (await IsGraphTokenValidAsync())
                {
                    var tokenInfo = await GetGraphTokenInfoAsync();
                    if (tokenInfo?.IsValid == true)
                    {
                        _logger.LogDebug("Zwracam aktualny ważny token Graph API");
                        return await GetClientCredentialsTokenAsync();
                    }
                }

                // Token nieważny lub nie istnieje - odśwież
                _logger.LogDebug("Token Graph API nieważny - odświeżanie...");
                if (await RefreshGraphTokenAsync())
                {
                    var refreshedToken = await GetClientCredentialsTokenAsync();
                    _logger.LogDebug("Token Graph API został odświeżony");
                    return refreshedToken;
                }

                _logger.LogWarning("Nie można uzyskać ważnego tokenu Graph API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ważnego tokenu Graph API");
                return null;
            }
        }

        /// <summary>
        /// Pobiera token dla aplikacji (client credentials flow)
        /// </summary>
        private async Task<string?> GetClientCredentialsTokenAsync()
        {
            try
            {
                var result = await _confidentialClientApp
                    .AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials)
                    .ExecuteAsync();

                return result?.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania tokenu client credentials");
                return null;
            }
        }

        /// <summary>
        /// Zapewnia ważny token dostępu do Microsoft Graph API.
        /// </summary>
        public async Task<bool> EnsureValidGraphTokenAsync()
        {
            try
            {
                var token = await GetValidGraphTokenAsync();
                return !string.IsNullOrEmpty(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapewniania ważnego tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// Sprawdza czy aktualny token Graph API jest ważny.
        /// </summary>
        public async Task<bool> IsGraphTokenValidAsync()
        {
            try
            {
                var tokenInfo = await GetGraphTokenInfoAsync();
                return tokenInfo?.IsValid == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania ważności tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// Odświeża token dostępu do Microsoft Graph API.
        /// </summary>
        public async Task<bool> RefreshGraphTokenAsync()
        {
            try
            {
                _logger.LogDebug("Odświeżanie tokenu Graph API...");

                var result = await _confidentialClientApp
                    .AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials)
                    .ExecuteAsync();

                if (result?.AccessToken != null)
                {
                    _logger.LogDebug("Token Graph API został pomyślnie odświeżony");
                    return true;
                }

                _logger.LogWarning("Nie udało się odświeżyć tokenu Graph API");
                return false;
            }
            catch (MsalServiceException ex)
            {
                _logger.LogError(ex, "Błąd serwisu MSAL podczas odświeżania tokenu Graph API: {ErrorCode}", ex.ErrorCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// Pobiera informacje o aktualnym tokenie Graph API (czas wygaśnięcia, zakresy, itp.).
        /// </summary>
        /// <returns>Informacje o tokenie lub null jeśli token nie istnieje</returns>
        public async Task<GraphTokenInfo?> GetGraphTokenInfoAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie informacji o tokenie Graph API...");

                var result = await _confidentialClientApp
                    .AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials)
                    .ExecuteAsync();

                if (result?.AccessToken != null)
                {
                    return new GraphTokenInfo
                    {
                        ExpiresOn = result.ExpiresOn.DateTime,
                        Scopes = result.Scopes?.ToArray() ?? Array.Empty<string>(),
                        Subject = result.Account?.HomeAccountId?.Identifier,
                        TenantId = result.TenantId
                    };
                }

                _logger.LogWarning("Nie można pobrać informacji o tokenie Graph API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania informacji o tokenie Graph API");
                return null;
            }
        }

        /// <summary>
        /// Unieważnia aktualny token Graph API w cache.
        /// </summary>
        public async Task InvalidateGraphTokenAsync()
        {
            try
            {
                _logger.LogDebug("Unieważnianie tokenu Graph API w cache...");

#pragma warning disable CS0618 // Type or member is obsolete
                var accounts = await _confidentialClientApp.GetAccountsAsync();
#pragma warning restore CS0618 // Type or member is obsolete
                foreach (var account in accounts)
                {
                    await _confidentialClientApp.RemoveAsync(account);
                }

                _logger.LogDebug("Token Graph API został unieważniony w cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas unieważniania tokenu Graph API");
            }
        }
    }
} 