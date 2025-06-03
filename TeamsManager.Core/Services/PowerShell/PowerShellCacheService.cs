using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Implementacja serwisu zarządzającego cache'owaniem danych PowerShell/Graph
    /// </summary>
    public class PowerShellCacheService : IPowerShellCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly IPowerShellConnectionService _connectionService;
        private readonly ILogger<PowerShellCacheService> _logger;

        // Definicje kluczy cache
        private const string GraphContextCacheKey = "PowerShell_GraphContext";
        private const string UserIdCacheKeyPrefix = "PowerShell_UserId_";
        private const string UserUpnCacheKeyPrefix = "PowerShell_UserUpn_";
        private const string TeamDetailsCacheKeyPrefix = "PowerShell_Team_";
        private const string AllTeamsCacheKey = "PowerShell_Teams_All";
        private const string TeamChannelsCacheKeyPrefix = "PowerShell_TeamChannels_";
        
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5);

        // Token do zarządzania unieważnianiem wpisów cache
        private static CancellationTokenSource _powerShellCacheTokenSource = new CancellationTokenSource();

        public PowerShellCacheService(
            IMemoryCache cache,
            IPowerShellConnectionService connectionService,
            ILogger<PowerShellCacheService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("Próba pobrania ID użytkownika z pustym UPN.");
                return null;
            }

            string cacheKey = UserIdCacheKeyPrefix + userUpn;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out string? cachedId))
            {
                _logger.LogDebug("ID użytkownika {UserUpn} znalezione w cache.", userUpn);
                return cachedId;
            }

            _logger.LogDebug("ID użytkownika {UserUpn} nie znalezione w cache lub wymuszono odświeżenie.", userUpn);

            try
            {
                var script = $@"
                    $user = Get-MgUser -UserId '{userUpn.Replace("'", "''")}' -ErrorAction Stop
                    if ($user) {{ $user.Id }} else {{ $null }}
                ";

                var results = await _connectionService.ExecuteScriptAsync(script);
                var userId = results?.FirstOrDefault()?.BaseObject?.ToString();

                if (!string.IsNullOrEmpty(userId))
                {
                    _cache.Set(cacheKey, userId, GetDefaultCacheEntryOptions());

                    // Cache też po UPN
                    string upnCacheKey = UserUpnCacheKeyPrefix + userUpn;
                    _cache.Set(upnCacheKey, userId, GetDefaultCacheEntryOptions());

                    _logger.LogDebug("ID użytkownika {UserUpn} zapisane w cache.", userUpn);
                }
                else
                {
                    // Cache negatywny wynik na krótko
                    _cache.Set(cacheKey, (string?)null, TimeSpan.FromMinutes(1));
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ID użytkownika {UserUpn}", userUpn);
                return null;
            }
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            return _cache.TryGetValue(key, out value);
        }

        public void Set<T>(string key, T value, TimeSpan? duration = null)
        {
            var options = duration.HasValue 
                ? new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(duration.Value)
                    .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token))
                : GetDefaultCacheEntryOptions();

            _cache.Set(key, value, options);
            _logger.LogDebug("Zapisano w cache klucz: {Key} na czas: {Duration}", 
                key, duration ?? _defaultCacheDuration);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Usunięto z cache klucz: {Key}", key);
        }

        public void InvalidateUserCache(string? userId = null, string? userUpn = null)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                Remove(UserIdCacheKeyPrefix + userId);
            }

            if (!string.IsNullOrWhiteSpace(userUpn))
            {
                Remove(UserIdCacheKeyPrefix + userUpn);
                Remove(UserUpnCacheKeyPrefix + userUpn);
            }

            _logger.LogDebug("Unieważniono cache użytkownika. userId: {UserId}, userUpn: {UserUpn}", 
                userId, userUpn);
        }

        public void InvalidateTeamCache(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return;

            Remove(TeamDetailsCacheKeyPrefix + teamId);
            Remove(TeamChannelsCacheKeyPrefix + teamId);
            
            _logger.LogDebug("Unieważniono cache zespołu: {TeamId}", teamId);
        }

        public void InvalidateAllCache()
        {
            // Zresetuj CancellationTokenSource
            var oldTokenSource = Interlocked.Exchange(ref _powerShellCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            
            _logger.LogInformation("Token cache dla PowerShell został zresetowany. Wszystkie wpisy cache zostały unieważnione.");

            // Usuń też kluczowe wpisy bezpośrednio
            Remove(GraphContextCacheKey);
            Remove(AllTeamsCacheKey);
        }

        public MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }

        public MemoryCacheEntryOptions GetShortCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_shortCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_powerShellCacheTokenSource.Token));
        }
    }
}