using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services.Auth;

namespace TeamsManager.Core.Services.Auth
{
    public class TokenManager : ITokenManager
    {
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TokenManager> _logger;
        private readonly IConfiguration _configuration;
        
        // Scopes dla PowerShell Microsoft Graph
        private readonly string[] _graphPowerShellScopes = new[] { 
            "https://graph.microsoft.com/User.Read", 
            "https://graph.microsoft.com/Group.ReadWrite.All", 
            "https://graph.microsoft.com/Team.ReadBasic.All",
            "https://graph.microsoft.com/TeamSettings.ReadWrite.All",
            "https://graph.microsoft.com/Channel.ReadBasic.All",
            "https://graph.microsoft.com/ChannelSettings.ReadWrite.All"
        };

        public TokenManager(
            IConfidentialClientApplication confidentialClientApp,
            IMemoryCache cache,
            ILogger<TokenManager> logger,
            IConfiguration configuration)
        {
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<string> GetValidAccessTokenAsync(string userUpn, string apiAccessToken)
        {
            if (string.IsNullOrEmpty(userUpn))
                throw new ArgumentNullException(nameof(userUpn));
            
            if (string.IsNullOrEmpty(apiAccessToken))
                throw new ArgumentNullException(nameof(apiAccessToken));

            // Sprawdź cache
            var cacheKey = GetTokenCacheKey(userUpn);
            if (_cache.TryGetValue<string>(cacheKey, out var cachedToken) && !string.IsNullOrEmpty(cachedToken))
            {
                var expirationKey = GetExpirationCacheKey(userUpn);
                if (_cache.TryGetValue<DateTimeOffset>(expirationKey, out var expiration))
                {
                    // Sprawdź czy token jest ważny z 5-minutowym buforem
                    if (DateTimeOffset.UtcNow < expiration.AddMinutes(-5))
                    {
                        _logger.LogDebug("Użyto cached token dla użytkownika: {UserUpn}", userUpn);
                        return cachedToken;
                    }
                }
            }

            try
            {
                // Pobierz nowy token przez OBO flow
                var userAssertion = new UserAssertion(apiAccessToken);
                var result = await _confidentialClientApp
                    .AcquireTokenOnBehalfOf(_graphPowerShellScopes, userAssertion)
                    .ExecuteAsync();

                // Przechowaj w cache
                await StoreAuthenticationResultAsync(userUpn, result);

                _logger.LogInformation("Uzyskano nowy token Graph dla użytkownika: {UserUpn}", userUpn);
                return result.AccessToken;
            }
            catch (MsalException ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania tokenu Graph dla użytkownika: {UserUpn}", userUpn);
                throw;
            }
        }

        public async Task<bool> RefreshTokenAsync(string userUpn)
        {
            if (string.IsNullOrEmpty(userUpn))
                return false;

            try
            {
                var account = await _confidentialClientApp.GetAccountAsync($"{userUpn}");
                if (account == null)
                {
                    _logger.LogWarning("Nie znaleziono konta dla użytkownika: {UserUpn}", userUpn);
                    return false;
                }

                var result = await _confidentialClientApp
                    .AcquireTokenSilent(_graphPowerShellScopes, account)
                    .ExecuteAsync();

                await StoreAuthenticationResultAsync(userUpn, result);
                _logger.LogInformation("Odświeżono token dla użytkownika: {UserUpn}", userUpn);
                return true;
            }
            catch (MsalException ex)
            {
                _logger.LogWarning(ex, "Nie udało się odświeżyć tokenu dla użytkownika: {UserUpn}", userUpn);
                return false;
            }
        }

        public async Task StoreAuthenticationResultAsync(string userUpn, AuthenticationResult result)
        {
            if (string.IsNullOrEmpty(userUpn) || result == null)
                return;

            var cacheKey = GetTokenCacheKey(userUpn);
            var expirationKey = GetExpirationCacheKey(userUpn);
            var scopesKey = GetScopesCacheKey(userUpn);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = result.ExpiresOn,
                SlidingExpiration = TimeSpan.FromMinutes(30),
                Priority = CacheItemPriority.High
            };

            _cache.Set(cacheKey, result.AccessToken, cacheOptions);
            _cache.Set(expirationKey, result.ExpiresOn, cacheOptions);
            _cache.Set(scopesKey, result.Scopes.ToArray(), cacheOptions);

            _logger.LogDebug("Zapisano token w cache dla użytkownika: {UserUpn}, wygasa: {ExpiresOn}", 
                userUpn, result.ExpiresOn);

            await Task.CompletedTask;
        }

        public bool HasValidToken(string userUpn)
        {
            if (string.IsNullOrEmpty(userUpn))
                return false;

            var cacheKey = GetTokenCacheKey(userUpn);
            var expirationKey = GetExpirationCacheKey(userUpn);

            if (!_cache.TryGetValue<string>(cacheKey, out var token) || string.IsNullOrEmpty(token))
                return false;

            if (!_cache.TryGetValue<DateTimeOffset>(expirationKey, out var expiration))
                return false;

            // Token jest ważny jeśli wygasa za więcej niż 5 minut
            return DateTimeOffset.UtcNow < expiration.AddMinutes(-5);
        }

        public void ClearUserTokens(string userUpn)
        {
            if (string.IsNullOrEmpty(userUpn))
                return;

            var cacheKey = GetTokenCacheKey(userUpn);
            var expirationKey = GetExpirationCacheKey(userUpn);
            var scopesKey = GetScopesCacheKey(userUpn);

            _cache.Remove(cacheKey);
            _cache.Remove(expirationKey);
            _cache.Remove(scopesKey);

            _logger.LogInformation("Usunięto tokeny z cache dla użytkownika: {UserUpn}", userUpn);
        }

        public async Task<TokenInfo?> GetTokenInfoAsync(string userUpn)
        {
            if (string.IsNullOrEmpty(userUpn))
                return null;

            var cacheKey = GetTokenCacheKey(userUpn);
            var expirationKey = GetExpirationCacheKey(userUpn);
            var scopesKey = GetScopesCacheKey(userUpn);

            if (!_cache.TryGetValue<string>(cacheKey, out var token) || string.IsNullOrEmpty(token))
                return null;

            var expiration = _cache.TryGetValue<DateTimeOffset>(expirationKey, out var exp) ? exp : DateTimeOffset.MinValue;
            var scopes = _cache.TryGetValue<string[]>(scopesKey, out var sc) ? sc ?? Array.Empty<string>() : Array.Empty<string>();

            await Task.CompletedTask;

            return new TokenInfo
            {
                AccessToken = token,
                ExpiresOn = expiration,
                Scopes = scopes
            };
        }

        private string GetTokenCacheKey(string userUpn) => $"graph_token_{userUpn}";
        private string GetExpirationCacheKey(string userUpn) => $"graph_expiration_{userUpn}";
        private string GetScopesCacheKey(string userUpn) => $"graph_scopes_{userUpn}";
    }
} 