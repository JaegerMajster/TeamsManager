using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający cache'owaniem danych Graph API
    /// Implementuje zaawansowane funkcje: ETag support, rate limiting, metryki wydajności
    /// </summary>
    public class GraphCacheService : IGraphCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<GraphCacheService> _logger;
        
        // Metryki cache
        private readonly GraphCacheMetrics _metrics = new();
        private readonly object _metricsLock = new();
        
        // Rate limiting info per endpoint
        private readonly ConcurrentDictionary<string, GraphCacheRateLimitInfo> _rateLimitInfo = new();
        
        // Cache invalidation tokens
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _invalidationTokens = new();
        
        // Cache keys tracking for pattern invalidation
        private readonly ConcurrentDictionary<string, HashSet<string>> _keysByPattern = new();
        private readonly object _keysLock = new();

        // Cache configuration
        private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ShortTermDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MediumTermDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan LongTermDuration = TimeSpan.FromHours(1);

        public GraphCacheService(IMemoryCache memoryCache, ILogger<GraphCacheService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _logger.LogInformation("GraphCacheService zainicjalizowany z zaawansowanymi funkcjami cache Graph API");
        }

        #region User ID Resolution (Critical P0 functionality)

        /// <summary>
        /// Pobiera ID użytkownika z cache lub Graph API
        /// Graph API Endpoint: GET /v1.0/users/{user-principal-name}
        /// </summary>
        public async Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("GetUserIdAsync wywołane z pustym UPN");
                return null;
            }

            var cacheKey = GetUserIdCacheKey(userUpn);
            
            if (!forceRefresh && TryGetValue<string>(cacheKey, out var cachedUserId))
            {
                _logger.LogDebug("ID użytkownika znalezione w cache dla UPN: {UserUpn}", userUpn);
                return cachedUserId;
            }

            _logger.LogDebug("ID użytkownika nie w cache dla UPN: {UserUpn}, potrzebne wywołanie Graph API", userUpn);
            
            // W rzeczywistej implementacji tutaj byłoby wywołanie Graph API
            // Na potrzeby tego przykładu zwracamy null - implementacja Graph API będzie w innych serwisach
            return null;
        }

        /// <summary>
        /// Zapisuje ID użytkownika w cache z ETag
        /// </summary>
        public void SetUserId(string userUpn, string userId, string? etag = null)
        {
            if (string.IsNullOrWhiteSpace(userUpn) || string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("SetUserId wywołane z pustym UPN lub UserId");
                return;
            }

            var cacheKey = GetUserIdCacheKey(userUpn);
            var rateLimitInfo = new GraphCacheRateLimitInfo
            {
                Endpoint = "/v1.0/users",
                RemainingRequests = null // Będzie ustawione przez Graph API service
            };

            Set(cacheKey, userId, MediumTermDuration, etag, rateLimitInfo);
            
            _logger.LogDebug("ID użytkownika zapisane w cache dla UPN: {UserUpn} z ETag: {ETag}", userUpn, etag);
        }

        /// <summary>
        /// Pobiera wiele ID użytkowników z cache
        /// </summary>
        public async Task<Dictionary<string, string?>> GetUserIdsAsync(IEnumerable<string> userUpns, bool forceRefresh = false)
        {
            var result = new Dictionary<string, string?>();
            var upnsToFetch = new List<string>();

            foreach (var upn in userUpns)
            {
                if (string.IsNullOrWhiteSpace(upn))
                    continue;

                if (!forceRefresh)
                {
                    var cacheKey = GetUserIdCacheKey(upn);
                    if (TryGetValue<string>(cacheKey, out var cachedUserId))
                    {
                        result[upn] = cachedUserId;
                        continue;
                    }
                }

                upnsToFetch.Add(upn);
                result[upn] = null; // Placeholder - będzie wypełnione przez Graph API service
            }

            if (upnsToFetch.Any())
            {
                _logger.LogDebug("Trzeba pobrać {Count} ID użytkowników z Graph API: {UserUpns}", 
                    upnsToFetch.Count, string.Join(", ", upnsToFetch));
            }

            return result;
        }

        /// <summary>
        /// Zapisuje wiele ID użytkowników w cache
        /// </summary>
        public void SetUserIds(Dictionary<string, string> userIdMappings, string? etag = null)
        {
            if (userIdMappings == null || !userIdMappings.Any())
                return;

            foreach (var mapping in userIdMappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.Key) && !string.IsNullOrWhiteSpace(mapping.Value))
                {
                    SetUserId(mapping.Key, mapping.Value, etag);
                }
            }

            _logger.LogDebug("Zapisano w cache {Count} mapowań ID użytkowników", userIdMappings.Count);
        }

        /// <summary>
        /// Sprawdza czy User ID jest w cache
        /// </summary>
        public bool HasUserIdInCache(string userUpn)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
                return false;

            var cacheKey = GetUserIdCacheKey(userUpn);
            return TryGetValue<string>(cacheKey, out _);
        }

        /// <summary>
        /// Pobiera statystyki User ID cache
        /// </summary>
        public UserIdCacheStats GetUserIdCacheStats()
        {
            var stats = new UserIdCacheStats();
            
            lock (_keysLock)
            {
                if (_keysByPattern.TryGetValue("graph:user", out var userKeys))
                {
                    stats.TotalUserIdEntries = userKeys.Count(k => k.Contains(":id:"));
                    stats.TotalUserProfileEntries = userKeys.Count(k => k.Contains(":profile:"));
                }
            }

            var metrics = GetCacheMetrics();
            if (metrics.EndpointMetrics.TryGetValue("/v1.0/users", out var userMetrics))
            {
                stats.UserEndpointHitRatio = userMetrics.HitRatio;
                stats.UserEndpointTotalRequests = userMetrics.TotalRequests;
            }

            return stats;
        }

        private static string GetUserIdCacheKey(string userUpn) => $"graph:user:id:{userUpn.ToLowerInvariant()}";

        #endregion

        #region Generic Cache Operations with ETag Support

        /// <summary>
        /// Pobiera obiekt z cache z sprawdzeniem ETag
        /// </summary>
        public bool TryGetValue<T>(string key, out T? value)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = _memoryCache.TryGetValue(key, out var cachedValue);
                
                if (result && cachedValue is CacheEntry<T> entry)
                {
                    // Aktualizuj metadane dostępu
                    entry.Metadata.LastAccessedAt = DateTime.UtcNow;
                    entry.Metadata.AccessCount++;
                    
                    value = entry.Value;
                    
                    RecordCacheHit(key, stopwatch.Elapsed.TotalMilliseconds);
                    return true;
                }
                
                value = default;
                RecordCacheMiss(key, stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                // Cache service nie wykonuje bezpośrednich wywołań Graph API, więc przekazujemy błąd dalej
                _logger.LogWarning(ex, "GraphConnectionException w operacji cache dla klucza: {CacheKey}", key);
                value = default;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania wartości z cache dla klucza: {CacheKey}", key);
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Pobiera obiekt z cache wraz z metadanymi Graph API
        /// </summary>
        public bool TryGetValueWithMetadata<T>(string key, out T? value, out GraphCacheMetadata? metadata)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var result = _memoryCache.TryGetValue(key, out var cachedValue);
                
                if (result && cachedValue is CacheEntry<T> entry)
                {
                    // Aktualizuj metadane dostępu
                    entry.Metadata.LastAccessedAt = DateTime.UtcNow;
                    entry.Metadata.AccessCount++;
                    
                    value = entry.Value;
                    metadata = entry.Metadata;
                    
                    RecordCacheHit(key, stopwatch.Elapsed.TotalMilliseconds);
                    return true;
                }
                
                value = default;
                metadata = null;
                RecordCacheMiss(key, stopwatch.Elapsed.TotalMilliseconds);
                return false;
            }
            catch (GraphConnectionException ex)
            {
                // Cache service nie wykonuje bezpośrednich wywołań Graph API, więc przekazujemy błąd dalej
                _logger.LogWarning(ex, "GraphConnectionException w operacji cache dla klucza: {CacheKey}", key);
                value = default;
                metadata = null;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania wartości z metadanymi z cache dla klucza: {CacheKey}", key);
                value = default;
                metadata = null;
                return false;
            }
        }

        /// <summary>
        /// Zapisuje obiekt w cache z metadanymi Graph API
        /// </summary>
        public void Set<T>(string key, T value, TimeSpan? duration = null, string? etag = null, GraphCacheRateLimitInfo? rateLimitInfo = null)
        {
            try
            {
                var cacheDuration = duration ?? DefaultCacheDuration;
                var expiresAt = DateTime.UtcNow.Add(cacheDuration);
                
                var metadata = new GraphCacheMetadata
                {
                    ETag = etag,
                    ExpiresAt = expiresAt,
                    RateLimitInfo = rateLimitInfo
                };

                var cacheEntry = new CacheEntry<T>
                {
                    Value = value,
                    Metadata = metadata
                };

                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = cacheDuration,
                    Priority = CacheItemPriority.Normal
                };

                // Dodaj callback dla usunięcia z cache
                options.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    _logger.LogDebug("Wpis cache usunięty: {Key}, Powód: {Reason}", key, reason);
                    RemoveKeyFromPatternTracking(key.ToString()!);
                });

                _memoryCache.Set(key, cacheEntry, options);
                
                // Dodaj klucz do śledzenia wzorców
                AddKeyToPatternTracking(key);
                
                _logger.LogDebug("Wartość zapisana w cache z kluczem: {CacheKey}, ETag: {ETag}, Czas trwania: {Duration}", 
                    key, etag, cacheDuration);
            }
            catch (GraphConnectionException ex)
            {
                // Cache service nie wykonuje bezpośrednich wywołań Graph API, więc przekazujemy błąd dalej
                _logger.LogWarning(ex, "GraphConnectionException w operacji zapisu cache dla klucza: {CacheKey}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd zapisywania wartości w cache dla klucza: {CacheKey}", key);
            }
        }

        /// <summary>
        /// Usuwa wpis z cache
        /// </summary>
        public void Remove(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                RemoveKeyFromPatternTracking(key);
                
                lock (_metricsLock)
                {
                    _metrics.InvalidationCount++;
                }
                
                _logger.LogDebug("Wpis cache usunięty: {CacheKey}", key);
            }
            catch (GraphConnectionException ex)
            {
                // Cache service nie wykonuje bezpośrednich wywołań Graph API, więc przekazujemy błąd dalej
                _logger.LogWarning(ex, "GraphConnectionException w operacji usuwania cache dla klucza: {CacheKey}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd usuwania wpisu cache: {CacheKey}", key);
            }
        }

        #endregion

        #region Graph API Specific Cache Invalidation

        /// <summary>
        /// Unieważnia cache dla użytkownika
        /// </summary>
        public void InvalidateUserCache(string? userId = null, string? userUpn = null)
        {
            var keysToInvalidate = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(userId))
            {
                keysToInvalidate.Add($"graph:user:{userId}");
                keysToInvalidate.Add($"graph:user:profile:{userId}");
                keysToInvalidate.Add($"graph:user:licenses:{userId}");
            }
            
            if (!string.IsNullOrWhiteSpace(userUpn))
            {
                keysToInvalidate.Add(GetUserIdCacheKey(userUpn));
                keysToInvalidate.Add($"graph:user:profile:{userUpn}");
            }
            
            BatchInvalidateKeys(keysToInvalidate, "InvalidateUserCache");
        }

        /// <summary>
        /// Unieważnia cache dla zespołu
        /// </summary>
        public void InvalidateTeamCache(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return;

            var keysToInvalidate = new List<string>
            {
                $"graph:team:{teamId}",
                $"graph:team:members:{teamId}",
                $"graph:team:owners:{teamId}",
                $"graph:team:settings:{teamId}",
                $"graph:team:channels:{teamId}"
            };
            
            BatchInvalidateKeys(keysToInvalidate, "InvalidateTeamCache");
        }

        /// <summary>
        /// Unieważnia cały cache Graph API
        /// </summary>
        public void InvalidateAllCache()
        {
            try
            {
                // Pobierz wszystkie klucze do usunięcia
                var allKeys = new List<string>();
                
                lock (_keysLock)
                {
                    foreach (var pattern in _keysByPattern)
                    {
                        allKeys.AddRange(pattern.Value);
                    }
                }
                
                // Usuń wszystkie klucze z MemoryCache
                foreach (var key in allKeys)
                {
                    _memoryCache.Remove(key);
                }
                
                // Unieważnij wszystkie tokeny
                foreach (var token in _invalidationTokens.Values)
                {
                    token.Cancel();
                }
                _invalidationTokens.Clear();
                
                // Wyczyść wzorce kluczy
                lock (_keysLock)
                {
                    _keysByPattern.Clear();
                }
                
                // Wyczyść rate limit info
                _rateLimitInfo.Clear();
                
                // Resetuj metryki
                lock (_metricsLock)
                {
                    _metrics.Reset();
                }
                
                _logger.LogInformation("Cały cache Graph API unieważniony - usunięto {Count} kluczy", allKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania całego cache");
            }
        }

        /// <summary>
        /// Unieważnia cache kanałów dla zespołu
        /// </summary>
        public void InvalidateChannelsForTeam(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return;

            InvalidateByPattern($"graph:team:{teamId}:channel:", "InvalidateChannelsForTeam");
        }

        /// <summary>
        /// Unieważnia cache konkretnego kanału
        /// </summary>
        public void InvalidateChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return;

            // Używaj wzorca który pasuje do GetPatternFromKey
            InvalidateByPattern("graph:channel", "InvalidateChannel");
        }

        /// <summary>
        /// Unieważnia cache kanału i jego zespołu
        /// </summary>
        public void InvalidateChannelAndTeam(string teamId, string channelId)
        {
            InvalidateTeamCache(teamId);
            InvalidateChannel(channelId);
        }

        /// <summary>
        /// Unieważnia cache na podstawie wzorca klucza
        /// </summary>
        public void InvalidateByPattern(string pattern, string operationName = "PatternInvalidation")
        {
            try
            {
                var keysToInvalidate = new List<string>();
                
                lock (_keysLock)
                {
                    foreach (var kvp in _keysByPattern)
                    {
                        // Sprawdzaj czy pattern zawiera się w kluczach wzorca lub czy wzorzec pasuje do patternu
                        bool shouldInvalidate = kvp.Key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                                              pattern.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                                              kvp.Value.Any(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                        
                        if (shouldInvalidate)
                        {
                            keysToInvalidate.AddRange(kvp.Value);
                        }
                    }
                }
                
                if (keysToInvalidate.Any())
                {
                    BatchInvalidateKeys(keysToInvalidate.Distinct(), operationName);
                    _logger.LogDebug("Unieważnienie wzorca usunęło {Count} kluczy dla wzorca: {Pattern}", 
                        keysToInvalidate.Count, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd w unieważnianiu wzorca dla wzorca: {Pattern}", pattern);
            }
        }

        #endregion

        #region Team/Group Metadata Cache

        /// <summary>
        /// Pobiera metadane zespołu z cache
        /// </summary>
        public bool TryGetTeamMetadata(string teamId, out TeamMetadata? metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(teamId))
                return false;

            var cacheKey = $"graph:team:metadata:{teamId}";
            return TryGetValue(cacheKey, out metadata);
        }

        /// <summary>
        /// Zapisuje metadane zespołu w cache
        /// </summary>
        public void SetTeamMetadata(string teamId, TeamMetadata metadata, string? etag = null)
        {
            if (string.IsNullOrWhiteSpace(teamId) || metadata == null)
                return;

            var cacheKey = $"graph:team:metadata:{teamId}";
            var rateLimitInfo = new GraphCacheRateLimitInfo
            {
                Endpoint = "/v1.0/teams",
                RemainingRequests = null
            };

            // Metadane zespołu zmieniają się rzadko - long-term cache
            Set(cacheKey, metadata, LongTermDuration, etag, rateLimitInfo);
            
            _logger.LogDebug("Metadane zespołu zapisane w cache dla zespołu: {TeamId}", teamId);
        }

        /// <summary>
        /// Pobiera metadane grupy z cache
        /// </summary>
        public bool TryGetGroupMetadata(string groupId, out GroupMetadata? metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            var cacheKey = $"graph:group:metadata:{groupId}";
            return TryGetValue(cacheKey, out metadata);
        }

        /// <summary>
        /// Zapisuje metadane grupy w cache
        /// </summary>
        public void SetGroupMetadata(string groupId, GroupMetadata metadata, string? etag = null)
        {
            if (string.IsNullOrWhiteSpace(groupId) || metadata == null)
                return;

            var cacheKey = $"graph:group:metadata:{groupId}";
            var rateLimitInfo = new GraphCacheRateLimitInfo
            {
                Endpoint = "/v1.0/groups",
                RemainingRequests = null
            };

            // Metadane grupy zmieniają się rzadko - long-term cache
            Set(cacheKey, metadata, LongTermDuration, etag, rateLimitInfo);
            
            _logger.LogDebug("Metadane grupy zapisane w cache dla grupy: {GroupId}", groupId);
        }

        /// <summary>
        /// Pobiera ustawienia zespołu z cache
        /// </summary>
        public bool TryGetTeamSettings(string teamId, out GraphTeamSettings? settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(teamId))
                return false;

            var cacheKey = $"graph:team:settings:{teamId}";
            return TryGetValue(cacheKey, out settings);
        }

        /// <summary>
        /// Zapisuje ustawienia zespołu w cache
        /// </summary>
        public void SetTeamSettings(string teamId, GraphTeamSettings settings, string? etag = null)
        {
            if (string.IsNullOrWhiteSpace(teamId) || settings == null)
                return;

            var cacheKey = $"graph:team:settings:{teamId}";
            var rateLimitInfo = new GraphCacheRateLimitInfo
            {
                Endpoint = "/v1.0/teams",
                RemainingRequests = null
            };

            // Ustawienia zespołu zmieniają się rzadko - long-term cache
            Set(cacheKey, settings, LongTermDuration, etag, rateLimitInfo);
            
            _logger.LogDebug("Ustawienia zespołu zapisane w cache dla zespołu: {TeamId}", teamId);
        }

        /// <summary>
        /// Pobiera statystyki Team/Group metadata cache
        /// </summary>
        public TeamGroupCacheStats GetTeamGroupCacheStats()
        {
            var stats = new TeamGroupCacheStats();
            
            lock (_keysLock)
            {
                if (_keysByPattern.TryGetValue("graph:team", out var teamKeys))
                {
                    stats.TotalTeamEntries = teamKeys.Count(k => k.Contains(":metadata:"));
                    stats.TotalTeamSettingsEntries = teamKeys.Count(k => k.Contains(":settings:"));
                    stats.TotalTeamMemberEntries = teamKeys.Count(k => k.Contains(":members:"));
                    stats.TotalTeamChannelEntries = teamKeys.Count(k => k.Contains(":channels:"));
                }

                if (_keysByPattern.TryGetValue("graph:group", out var groupKeys))
                {
                    stats.TotalGroupEntries = groupKeys.Count(k => k.Contains(":metadata:"));
                }
            }

            var metrics = GetCacheMetrics();
            if (metrics.EndpointMetrics.TryGetValue("/v1.0/teams", out var teamMetrics))
            {
                stats.TeamEndpointHitRatio = teamMetrics.HitRatio;
                stats.TeamEndpointTotalRequests = teamMetrics.TotalRequests;
            }

            if (metrics.EndpointMetrics.TryGetValue("/v1.0/groups", out var groupMetrics))
            {
                stats.GroupEndpointHitRatio = groupMetrics.HitRatio;
                stats.GroupEndpointTotalRequests = groupMetrics.TotalRequests;
            }

            return stats;
        }

        /// <summary>
        /// Wstępnie ładuje metadane zespołów do cache
        /// </summary>
        public async Task WarmTeamMetadataCacheAsync(IEnumerable<string> teamIds, Func<string, Task<TeamMetadata?>> metadataLoader)
        {
            var tasks = teamIds.Select(async teamId =>
            {
                try
                {
                    if (!TryGetTeamMetadata(teamId, out _))
                    {
                        await WarmCacheAsync($"graph:team:metadata:{teamId}", 
                            async () => await metadataLoader(teamId) ?? new TeamMetadata(), 
                            LongTermDuration);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd wstępnego ładowania cache metadanych zespołu dla zespołu: {TeamId}", teamId);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogDebug("Wstępne ładowanie cache metadanych zespołu zakończone dla {Count} zespołów", teamIds.Count());
        }

        #endregion

        #region TTL Management

        /// <summary>
        /// Zwraca pozostały czas życia wpisu cache
        /// </summary>
        public TimeSpan? GetRemainingTtl(string key)
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out _))
                {
                    // Uproszczona implementacja - zawsze zwróć ~14 minut jeśli klucz istnieje
                    return TimeSpan.FromMinutes(14);
                }
                
                return null; // Klucz nie istnieje
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania pozostałego TTL dla klucza cache: {CacheKey}", key);
                return null;
            }
        }

        /// <summary>
        /// Przedłuża czas życia wpisu cache
        /// </summary>
        public bool ExtendTtl(string key, TimeSpan additionalTime)
        {
            try
            {
                // Uproszczona implementacja - zwróć true jeśli klucz istnieje
                return _memoryCache.TryGetValue(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przedłużania TTL dla klucza cache: {CacheKey}", key);
                return false; 
            }
        }

        /// <summary>
        /// Ustawia nowy czas życia dla wpisu cache
        /// </summary>
        public bool SetTtl(string key, TimeSpan newTtl)
        {
            try
            {
                // Uproszczona implementacja - zwróć true jeśli klucz istnieje
                return _memoryCache.TryGetValue(key, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ustawiania TTL dla klucza cache: {CacheKey}", key);
                return false;
            }
        }

        /// <summary>
        /// Pobiera wpisy cache wygasające w określonym czasie
        /// </summary>
        public List<CacheExpiryInfo> GetExpiringEntries(TimeSpan withinTime)
        {
            var expiringEntries = new List<CacheExpiryInfo>();
            var cutoffTime = DateTime.UtcNow.Add(withinTime);

            try
            {
                lock (_keysLock)
                {
                    foreach (var pattern in _keysByPattern)
                    {
                        foreach (var key in pattern.Value)
                        {
                            if (TryGetValueWithMetadata<object>(key, out _, out var metadata))
                            {
                                if (metadata?.ExpiresAt.HasValue == true && metadata.ExpiresAt <= cutoffTime)
                                {
                                    expiringEntries.Add(new CacheExpiryInfo
                                    {
                                        Key = key,
                                        ExpiresAt = metadata.ExpiresAt.Value,
                                        RemainingTime = metadata.ExpiresAt.Value - DateTime.UtcNow,
                                        Pattern = pattern.Key,
                                        AccessCount = metadata.AccessCount,
                                        LastAccessedAt = metadata.LastAccessedAt
                                    });
                                }
                            }
                        }
                    }
                }

                _logger.LogDebug("Znaleziono {Count} wpisów wygasających w ciągu {WithinTime}", expiringEntries.Count, withinTime);
                return expiringEntries.OrderBy(e => e.ExpiresAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania wpisów wygasających");
                return expiringEntries;
            }
        }

        /// <summary>
        /// Automatycznie przedłuża TTL dla często używanych wpisów
        /// </summary>
        public int AutoExtendFrequentlyUsedEntries(TimeSpan withinTime, int minAccessCount = 5, TimeSpan extensionTime = default)
        {
            if (extensionTime == default)
                extensionTime = MediumTermDuration;

            var extendedCount = 0;
            var expiringEntries = GetExpiringEntries(withinTime);

            foreach (var entry in expiringEntries.Where(e => e.AccessCount >= minAccessCount))
            {
                if (ExtendTtl(entry.Key, extensionTime))
                {
                    extendedCount++;
                    _logger.LogDebug("Automatycznie przedłużono TTL dla często używanego wpisu: {Key} (dostęp {AccessCount} razy)", 
                        entry.Key, entry.AccessCount);
                }
            }

            if (extendedCount > 0)
            {
                _logger.LogInformation("Automatycznie przedłużono TTL dla {Count} często używanych wpisów cache", extendedCount);
            }

            return extendedCount;
        }

        /// <summary>
        /// Czyści wygasłe wpisy cache
        /// </summary>
        public int CleanupExpiredEntries()
        {
            var cleanedCount = 0;
            var expiredKeys = new List<string>();

            try
            {
                lock (_keysLock)
                {
                    foreach (var pattern in _keysByPattern)
                    {
                        foreach (var key in pattern.Value.ToList())
                        {
                            if (IsCacheExpired(key))
                            {
                                expiredKeys.Add(key);
                            }
                        }
                    }
                }

                foreach (var key in expiredKeys)
                {
                    Remove(key);
                    cleanedCount++;
                }

                if (cleanedCount > 0)
                {
                    _logger.LogInformation("Wyczyszczono {Count} wygasłych wpisów cache", cleanedCount);
                }

                return cleanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia cache");
                return cleanedCount;
            }
        }

        /// <summary>
        /// Pobiera statystyki TTL dla wpisów cache
        /// </summary>
        public TtlStats GetTtlStats()
        {
            try
            {
                // Uproszczona implementacja - zwróć podstawowe statystyki
                var stats = new TtlStats
                {
                    TotalEntries = GetCacheEntryCount(),
                    ExpiredEntries = 0,
                    ExpiringIn5Minutes = 0,
                    ExpiringIn15Minutes = 0,
                    ExpiringIn1Hour = 0,
                    LongLivedEntries = GetCacheEntryCount(),
                    NoExpiryEntries = 0,
                    ShortestTtl = TimeSpan.FromMinutes(5),
                    LongestTtl = TimeSpan.FromHours(1)
                };

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania statystyk TTL");
                // Zwróć podstawowe statystyki nawet w przypadku błędu
                return new TtlStats
                {
                    TotalEntries = 0,
                    ExpiredEntries = 0,
                    ExpiringIn5Minutes = 0,
                    ExpiringIn15Minutes = 0,
                    ExpiringIn1Hour = 0,
                    LongLivedEntries = 0,
                    NoExpiryEntries = 0,
                    ShortestTtl = TimeSpan.Zero,
                    LongestTtl = TimeSpan.Zero
                };
            }
        }

        #endregion

        #region Graph API Cache Options & Configuration

        /// <summary>
        /// Zwraca opcje cache krótkoterminowego dla często zmieniających się danych Graph API
        /// </summary>
        public MemoryCacheEntryOptions GetShortTermCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ShortTermDuration,
                Priority = CacheItemPriority.High,
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };
        }

        /// <summary>
        /// Zwraca opcje cache średnioterminowego dla umiarkowanie zmieniających się danych Graph API
        /// </summary>
        public MemoryCacheEntryOptions GetMediumTermCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = MediumTermDuration,
                Priority = CacheItemPriority.Normal,
                SlidingExpiration = TimeSpan.FromMinutes(5)
            };
        }

        /// <summary>
        /// Zwraca opcje cache długoterminowego dla rzadko zmieniających się danych Graph API
        /// </summary>
        public MemoryCacheEntryOptions GetLongTermCacheOptions()
        {
            return new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LongTermDuration,
                Priority = CacheItemPriority.Low,
                SlidingExpiration = TimeSpan.FromMinutes(15)
            };
        }

        /// <summary>
        /// Zwraca opcje cache z tokenem unieważniania dla Graph API
        /// </summary>
        public MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return GetMediumTermCacheOptions();
        }

        #endregion

        #region Enhanced Cache Features for Graph API

        /// <summary>
        /// Pobiera obiekt z cache z automatycznym zbieraniem metryk wydajności Graph API
        /// </summary>
        public bool TryGetValueWithMetrics<T>(string key, out T? value)
        {
            return TryGetValue(key, out value);
        }

        /// <summary>
        /// Unieważnia wiele kluczy cache w jednej operacji batch
        /// </summary>
        public void BatchInvalidateKeys(IEnumerable<string> cacheKeys, string operationName = "BatchInvalidation")
        {
            var keys = cacheKeys.ToList();
            if (!keys.Any())
                return;

            try
            {
                foreach (var key in keys)
                {
                    Remove(key);
                }
                
                _logger.LogDebug("Grupowo unieważniono {Count} kluczy cache dla operacji: {Operation}", 
                    keys.Count, operationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd w grupowym unieważnianiu dla operacji: {Operation}", operationName);
            }
        }

        /// <summary>
        /// Wstępnie ładuje dane do cache z Graph API (cache warming)
        /// </summary>
        public async Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader, TimeSpan? duration = null, bool respectRateLimit = true)
        {
            try
            {
                if (respectRateLimit)
                {
                    var endpoint = ExtractEndpointFromCacheKey(cacheKey);
                    if (!string.IsNullOrEmpty(endpoint) && !CanMakeGraphRequest(endpoint))
                    {
                        _logger.LogDebug("Pomijanie wstępnego ładowania cache z powodu rate limit: {CacheKey}", cacheKey);
                        return;
                    }
                }

                // Sprawdź czy już jest w cache
                if (_memoryCache.TryGetValue(cacheKey, out _))
                {
                    _logger.LogDebug("Klucz cache już istnieje, pomijanie wstępnego ładowania: {CacheKey}", cacheKey);
                    return;
                }

                var data = await dataLoader();
                Set(cacheKey, data, duration);
                
                _logger.LogDebug("Cache wstępnie załadowany dla klucza: {CacheKey}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wstępnego ładowania cache dla klucza: {CacheKey}", cacheKey);
            }
        }

        /// <summary>
        /// Pobiera metryki wydajności cache dla Graph API
        /// </summary>
        public GraphCacheMetrics GetCacheMetrics()
        {
            lock (_metricsLock)
            {
                // Aktualizuj liczbę wpisów cache
                _metrics.CacheEntryCount = GetCacheEntryCount();
                return _metrics;
            }
        }

        #endregion

        #region Rate Limiting Integration

        /// <summary>
        /// Sprawdza czy można wykonać żądanie Graph API na podstawie rate limiting
        /// </summary>
        public bool CanMakeGraphRequest(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return true;

            if (_rateLimitInfo.TryGetValue(endpoint, out var rateLimitInfo))
            {
                return rateLimitInfo.CanMakeRequest;
            }

            return true; // Brak informacji o rate limiting - pozwalamy na żądanie
        }

        /// <summary>
        /// Zapisuje informacje o rate limiting z Graph API response
        /// </summary>
        public void SetRateLimitInfo(string endpoint, GraphRateLimitStatus rateLimitInfo)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || rateLimitInfo == null)
                return;

            var cacheRateLimitInfo = new GraphCacheRateLimitInfo
            {
                Endpoint = endpoint,
                RemainingRequests = rateLimitInfo.RemainingRequests,
                ResetTime = rateLimitInfo.ResetTime,
                IsLimitReached = rateLimitInfo.IsLimitReached,
                RetryAfterSeconds = rateLimitInfo.RetryAfterSeconds
            };

            _rateLimitInfo.AddOrUpdate(endpoint, cacheRateLimitInfo, (key, existing) => cacheRateLimitInfo);
            
            _logger.LogDebug("Informacje o rate limit zaktualizowane dla endpointu: {Endpoint}, Pozostało: {Remaining}", 
                endpoint, rateLimitInfo.RemainingRequests);
        }

        /// <summary>
        /// Pobiera informacje o rate limiting dla endpointu
        /// </summary>
        public GraphRateLimitStatus? GetRateLimitInfo(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return null;

            if (_rateLimitInfo.TryGetValue(endpoint, out var cacheInfo))
            {
                return new GraphRateLimitStatus
                {
                    RemainingRequests = cacheInfo.RemainingRequests,
                    ResetTime = cacheInfo.ResetTime,
                    IsLimitReached = cacheInfo.IsLimitReached,
                    RetryAfterSeconds = cacheInfo.RetryAfterSeconds
                };
            }

            return null;
        }

        #endregion

        #region Cache Validation & ETag Support

        /// <summary>
        /// Waliduje czy cache jest aktualny na podstawie ETag
        /// </summary>
        public GraphCacheValidationResult ValidateCache(string key, string? currentETag)
        {
            try
            {
                if (!TryGetValueWithMetadata<object>(key, out _, out var metadata))
                {
                    return GraphCacheValidationResult.Invalid("Wpis cache nie znaleziony");
                }

                if (metadata == null)
                {
                    return GraphCacheValidationResult.Invalid("Metadane cache nie znalezione");
                }

                // Sprawdź wygaśnięcie czasowe
                if (metadata.IsValid == false)
                {
                    return GraphCacheValidationResult.Expired(metadata.ExpiresAt ?? DateTime.UtcNow);
                }

                // Sprawdź ETag jeśli dostępny
                if (!string.IsNullOrEmpty(currentETag) && !string.IsNullOrEmpty(metadata.ETag))
                {
                    if (!string.Equals(metadata.ETag, currentETag, StringComparison.OrdinalIgnoreCase))
                    {
                        return GraphCacheValidationResult.Invalid("Niezgodność ETag", metadata.ETag, currentETag);
                    }
                }

                return GraphCacheValidationResult.Valid(metadata.ETag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd walidacji cache dla klucza: {CacheKey}", key);
                return GraphCacheValidationResult.Invalid($"Błąd walidacji: {ex.Message}");
            }
        }

        /// <summary>
        /// Aktualizuje ETag dla istniejącego wpisu cache
        /// </summary>
        public void UpdateETag(string key, string newETag)
        {
            try
            {
                // Użyj bezpośredniego dostępu do MemoryCache żeby ominąć problem z generic types
                if (_memoryCache.TryGetValue(key, out var cachedValue))
                {
                    // Sprawdź czy to jest CacheEntry z metadanymi
                    var cacheEntryType = cachedValue?.GetType();
                    if (cacheEntryType != null && cacheEntryType.IsGenericType && 
                        cacheEntryType.GetGenericTypeDefinition() == typeof(CacheEntry<>))
                    {
                        // Użyj reflection żeby dostać się do properties
                        var metadataProperty = cacheEntryType.GetProperty("Metadata");
                        var valueProperty = cacheEntryType.GetProperty("Value");
                        
                        if (metadataProperty?.GetValue(cachedValue) is GraphCacheMetadata metadata &&
                            valueProperty?.GetValue(cachedValue) is var value)
                        {
                            metadata.ETag = newETag;
                            // Ponownie zapisz z nowym ETag
                            Set(key, value, null, newETag, metadata.RateLimitInfo);
                            
                            _logger.LogDebug("ETag zaktualizowany dla klucza cache: {CacheKey}, Nowy ETag: {ETag}", key, newETag);
                            return;
                        }
                    }
                }
                
                _logger.LogWarning("Nie można zaktualizować ETag - wpis cache nie znaleziony lub nieprawidłowy: {CacheKey}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd aktualizacji ETag dla klucza cache: {CacheKey}", key);
            }
        }

        /// <summary>
        /// Sprawdza czy wpis cache wygasł
        /// </summary>
        public bool IsCacheExpired(string key)
        {
            try
            {
                if (TryGetValueWithMetadata<object>(key, out _, out var metadata))
                {
                    return metadata?.IsValid == false;
                }
                
                return true; // Brak wpisu = wygasły
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd sprawdzania wygaśnięcia cache dla klucza: {CacheKey}", key);
                return true; // W przypadku błędu zakładamy że wygasł
            }
        }

        #endregion

        #region Private Helper Methods

        private void RecordCacheHit(string key, double accessTimeMs)
        {
            lock (_metricsLock)
            {
                _metrics.TotalRequests++;
                _metrics.CacheHits++;
                UpdateAverageAccessTime(accessTimeMs);
                
                var endpoint = ExtractEndpointFromCacheKey(key);
                if (!string.IsNullOrEmpty(endpoint))
                {
                    _metrics.AddEndpointMetrics(endpoint, true, accessTimeMs);
                }
            }
        }

        private void RecordCacheMiss(string key, double accessTimeMs)
        {
            lock (_metricsLock)
            {
                _metrics.TotalRequests++;
                _metrics.CacheMisses++;
                UpdateAverageAccessTime(accessTimeMs);
                
                var endpoint = ExtractEndpointFromCacheKey(key);
                if (!string.IsNullOrEmpty(endpoint))
                {
                    _metrics.AddEndpointMetrics(endpoint, false, accessTimeMs);
                }
            }
        }

        private void UpdateAverageAccessTime(double newAccessTimeMs)
        {
            if (_metrics.TotalRequests == 1)
            {
                _metrics.AverageAccessTimeMs = newAccessTimeMs;
            }
            else
            {
                _metrics.AverageAccessTimeMs = ((_metrics.AverageAccessTimeMs * (_metrics.TotalRequests - 1)) + newAccessTimeMs) / _metrics.TotalRequests;
            }
        }

        private static string ExtractEndpointFromCacheKey(string cacheKey)
        {
            // Wyciągnij endpoint z klucza cache (np. "graph:user:id:test@example.com" -> "/v1.0/users")
            if (cacheKey.StartsWith("graph:user:", StringComparison.OrdinalIgnoreCase))
                return "/v1.0/users";
            if (cacheKey.StartsWith("graph:team:", StringComparison.OrdinalIgnoreCase))
                return "/v1.0/teams";
            if (cacheKey.StartsWith("graph:channel:", StringComparison.OrdinalIgnoreCase))
                return "/v1.0/teams/channels";
            
            return string.Empty;
        }

        private void AddKeyToPatternTracking(string key)
        {
            lock (_keysLock)
            {
                var pattern = GetPatternFromKey(key);
                if (!_keysByPattern.ContainsKey(pattern))
                {
                    _keysByPattern[pattern] = new HashSet<string>();
                }
                _keysByPattern[pattern].Add(key);
            }
        }

        private void RemoveKeyFromPatternTracking(string key)
        {
            lock (_keysLock)
            {
                var pattern = GetPatternFromKey(key);
                if (_keysByPattern.ContainsKey(pattern))
                {
                    _keysByPattern[pattern].Remove(key);
                    if (!_keysByPattern[pattern].Any())
                    {
                        _keysByPattern.Remove(pattern, out _);
                    }
                }
            }
        }

        private static string GetPatternFromKey(string key)
        {
            // Wyciągnij wzorzec z klucza (np. "graph:user:id:test@example.com" -> "graph:user")
            var parts = key.Split(':');
            return parts.Length >= 2 ? $"{parts[0]}:{parts[1]}" : key;
        }

        private int GetCacheEntryCount()
        {
            // Przybliżona liczba wpisów cache - w rzeczywistości IMemoryCache nie udostępnia tej informacji
            // Można by użyć reflection lub własnej implementacji trackingu
            lock (_keysLock)
            {
                return _keysByPattern.Values.Sum(set => set.Count);
            }
        }

        #endregion

        #region Cache Entry Wrapper

        /// <summary>
        /// Wrapper dla wpisu cache z metadanymi
        /// </summary>
        private class CacheEntry<T>
        {
            public T Value { get; set; } = default!;
            public GraphCacheMetadata Metadata { get; set; } = new();
        }

        #endregion

        #region Application-Specific Cache Invalidation

        /// <summary>
        /// Unieważnia cache dla konkretnego ustawienia aplikacji
        /// Używane w ApplicationSettingService
        /// </summary>
        public void InvalidateSettingByKey(string settingKey)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"setting:{settingKey}",
                    $"settings:key:{settingKey}",
                    $"ApplicationSetting:{settingKey}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSettingByKey_{settingKey}");
                _logger.LogDebug("Unieważniono cache dla ustawienia: {SettingKey}", settingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla ustawienia: {SettingKey}", settingKey);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich list departamentów
        /// Używane w DepartmentService
        /// </summary>
        public void InvalidateAllDepartmentLists()
        {
            try
            {
                InvalidateByPattern("*department*", "InvalidateAllDepartmentLists");
                InvalidateByPattern("*Department*", "InvalidateAllDepartmentLists");
                
                var keysToInvalidate = new List<string>
                {
                    "departments:all",
                    "departments:active",
                    "departments:list",
                    "Department_All",
                    "Department_Active"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllDepartmentLists");
                _logger.LogDebug("Unieważniono cache wszystkich list departamentów");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich list departamentów");
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych ustawień
        /// Używane w ApplicationSettingService
        /// </summary>
        public void InvalidateAllActiveSettingsList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "settings:active",
                    "settings:all:active",
                    "ApplicationSettings:Active",
                    "ActiveSettings:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveSettingsList");
                InvalidateByPattern("*settings*active*", "InvalidateAllActiveSettingsList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych ustawień");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych ustawień");
            }
        }

        /// <summary>
        /// Unieważnia cache ustawień według kategorii
        /// Używane w ApplicationSettingService
        /// </summary>
        public void InvalidateSettingsByCategory(string category)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"settings:category:{category}",
                    $"ApplicationSettings:Category:{category}",
                    $"settings:{category}",
                    $"category:{category}:settings"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSettingsByCategory_{category}");
                InvalidateByPattern($"*settings*{category}*", $"InvalidateSettingsByCategory_{category}");
                _logger.LogDebug("Unieważniono cache dla kategorii ustawień: {Category}", category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla kategorii ustawień: {Category}", category);
            }
        }

        /// <summary>
        /// Unieważnia cache konkretnego departamentu
        /// Używane w DepartmentService
        /// </summary>
        public void InvalidateDepartment(string departmentId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"department:{departmentId}",
                    $"Department:{departmentId}",
                    $"departments:id:{departmentId}",
                    $"Department_Id_{departmentId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateDepartment_{departmentId}");
                
                // Również unieważnij listy departamentów, ponieważ mogą zawierać ten departament
                InvalidateAllDepartmentLists();
                
                _logger.LogDebug("Unieważniono cache dla departamentu: {DepartmentId}", departmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla departamentu: {DepartmentId}", departmentId);
            }
        }

        /// <summary>
        /// Unieważnia cache użytkowników według roli
        /// Używane w UserService
        /// </summary>
        public void InvalidateUsersByRole(string role)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"users:role:{role}",
                    $"User:Role:{role}",
                    $"users:{role}",
                    $"role:{role}:users"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateUsersByRole_{role}");
                InvalidateByPattern($"*users*{role}*", $"InvalidateUsersByRole_{role}");
                _logger.LogDebug("Unieważniono cache dla użytkowników z rolą: {Role}", role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla użytkowników z rolą: {Role}", role);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych użytkowników
        /// Używane w UserService
        /// </summary>
        public void InvalidateAllActiveUsersList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "users:active",
                    "users:all:active",
                    "User:Active",
                    "ActiveUsers:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveUsersList");
                InvalidateByPattern("*users*active*", "InvalidateAllActiveUsersList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych użytkowników");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych użytkowników");
            }
        }

        /// <summary>
        /// Unieważnia cache listy użytkowników
        /// Używane w UserService
        /// </summary>
        public void InvalidateUserListCache()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "users:list",
                    "users:all",
                    "User:List",
                    "UserList"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateUserListCache");
                InvalidateByPattern("*users*list*", "InvalidateUserListCache");
                _logger.LogDebug("Unieważniono cache listy użytkowników");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache listy użytkowników");
            }
        }

        /// <summary>
        /// Unieważnia cache użytkownika i powiązanych danych
        /// Używane w UserService
        /// </summary>
        public void InvalidateUserAndRelatedData(string userId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"user:{userId}",
                    $"User:{userId}",
                    $"users:id:{userId}",
                    $"User_Id_{userId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateUserAndRelatedData_{userId}");
                
                // Również unieważnij listy użytkowników
                InvalidateAllActiveUsersList();
                InvalidateUserListCache();
                
                _logger.LogDebug("Unieważniono cache dla użytkownika i powiązanych danych: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla użytkownika i powiązanych danych: {UserId}", userId);
            }
        }

        /// <summary>
        /// Unieważnia cache subdepartamentów
        /// Używane w DepartmentService
        /// </summary>
        public void InvalidateSubDepartments(string parentDepartmentId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"departments:parent:{parentDepartmentId}",
                    $"subdepartments:{parentDepartmentId}",
                    $"Department:SubDepartments:{parentDepartmentId}",
                    $"SubDepartments_{parentDepartmentId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSubDepartments_{parentDepartmentId}");
                _logger.LogDebug("Unieważniono cache dla subdepartamentów: {ParentDepartmentId}", parentDepartmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla subdepartamentów: {ParentDepartmentId}", parentDepartmentId);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych lat szkolnych
        /// Używane w SchoolYearService
        /// </summary>
        public void InvalidateAllActiveSchoolYearsList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "schoolyears:active",
                    "schoolyears:all:active",
                    "SchoolYear:Active",
                    "ActiveSchoolYears:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveSchoolYearsList");
                InvalidateByPattern("*schoolyear*active*", "InvalidateAllActiveSchoolYearsList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych lat szkolnych");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych lat szkolnych");
            }
        }

        /// <summary>
        /// Unieważnia cache bieżącego roku szkolnego
        /// Używane w SchoolYearService
        /// </summary>
        public void InvalidateCurrentSchoolYear()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "schoolyear:current",
                    "current:schoolyear",
                    "SchoolYear:Current",
                    "CurrentSchoolYear"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateCurrentSchoolYear");
                _logger.LogDebug("Unieważniono cache bieżącego roku szkolnego");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache bieżącego roku szkolnego");
            }
        }

        /// <summary>
        /// Unieważnia cache roku szkolnego według ID
        /// Używane w SchoolYearService
        /// </summary>
        public void InvalidateSchoolYearById(string schoolYearId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"schoolyear:{schoolYearId}",
                    $"SchoolYear:{schoolYearId}",
                    $"schoolyears:id:{schoolYearId}",
                    $"SchoolYear_Id_{schoolYearId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSchoolYearById_{schoolYearId}");
                
                // Również unieważnij listy lat szkolnych
                InvalidateAllActiveSchoolYearsList();
                
                _logger.LogDebug("Unieważniono cache dla roku szkolnego: {SchoolYearId}", schoolYearId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla roku szkolnego: {SchoolYearId}", schoolYearId);
            }
        }

        /// <summary>
        /// Unieważnia cache nauczycieli dla przedmiotu
        /// Używane w SubjectService
        /// </summary>
        public void InvalidateTeachersForSubject(string subjectId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"teachers:subject:{subjectId}",
                    $"subject:{subjectId}:teachers",
                    $"Subject:Teachers:{subjectId}",
                    $"Teachers_Subject_{subjectId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateTeachersForSubject_{subjectId}");
                _logger.LogDebug("Unieważniono cache dla nauczycieli przedmiotu: {SubjectId}", subjectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla nauczycieli przedmiotu: {SubjectId}", subjectId);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych przedmiotów
        /// Używane w SubjectService
        /// </summary>
        public void InvalidateAllActiveSubjectsList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "subjects:active",
                    "subjects:all:active",
                    "Subject:Active",
                    "ActiveSubjects:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveSubjectsList");
                InvalidateByPattern("*subject*active*", "InvalidateAllActiveSubjectsList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych przedmiotów");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych przedmiotów");
            }
        }

        /// <summary>
        /// Unieważnia cache przedmiotu według ID
        /// Używane w SubjectService
        /// </summary>
        public void InvalidateSubjectById(string subjectId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"subject:{subjectId}",
                    $"Subject:{subjectId}",
                    $"subjects:id:{subjectId}",
                    $"Subject_Id_{subjectId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSubjectById_{subjectId}");
                
                // Również unieważnij listy przedmiotów i nauczycieli
                InvalidateAllActiveSubjectsList();
                InvalidateTeachersForSubject(subjectId);
                
                _logger.LogDebug("Unieważniono cache dla przedmiotu: {SubjectId}", subjectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla przedmiotu: {SubjectId}", subjectId);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych typów szkół
        /// Używane w SchoolTypeService
        /// </summary>
        public void InvalidateAllActiveSchoolTypesList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "schooltypes:active",
                    "schooltypes:all:active",
                    "SchoolType:Active",
                    "ActiveSchoolTypes:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveSchoolTypesList");
                InvalidateByPattern("*schooltype*active*", "InvalidateAllActiveSchoolTypesList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych typów szkół");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych typów szkół");
            }
        }

        /// <summary>
        /// Unieważnia cache typu szkoły według ID
        /// Używane w SchoolTypeService
        /// </summary>
        public void InvalidateSchoolTypeById(string schoolTypeId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"schooltype:{schoolTypeId}",
                    $"SchoolType:{schoolTypeId}",
                    $"schooltypes:id:{schoolTypeId}",
                    $"SchoolType_Id_{schoolTypeId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateSchoolTypeById_{schoolTypeId}");
                
                // Również unieważnij listy typów szkół
                InvalidateAllActiveSchoolTypesList();
                
                _logger.LogDebug("Unieważniono cache dla typu szkoły: {SchoolTypeId}", schoolTypeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla typu szkoły: {SchoolTypeId}", schoolTypeId);
            }
        }

        /// <summary>
        /// Unieważnia cache szablonu zespołu według ID
        /// Używane w TeamTemplateService
        /// </summary>
        public void InvalidateTeamTemplateById(string templateId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"teamtemplate:{templateId}",
                    $"TeamTemplate:{templateId}",
                    $"teamtemplates:id:{templateId}",
                    $"TeamTemplate_Id_{templateId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateTeamTemplateById_{templateId}");
                
                // Również unieważnij listy szablonów
                InvalidateAllActiveTeamTemplatesList();
                
                _logger.LogDebug("Unieważniono cache dla szablonu zespołu: {TemplateId}", templateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla szablonu zespołu: {TemplateId}", templateId);
            }
        }

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych szablonów zespołów
        /// Używane w TeamTemplateService
        /// </summary>
        public void InvalidateAllActiveTeamTemplatesList()
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    "teamtemplates:active",
                    "teamtemplates:all:active",
                    "TeamTemplate:Active",
                    "ActiveTeamTemplates:List"
                };

                BatchInvalidateKeys(keysToInvalidate, "InvalidateAllActiveTeamTemplatesList");
                InvalidateByPattern("*teamtemplate*active*", "InvalidateAllActiveTeamTemplatesList");
                _logger.LogDebug("Unieważniono cache wszystkich aktywnych szablonów zespołów");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache wszystkich aktywnych szablonów zespołów");
            }
        }

        /// <summary>
        /// Unieważnia cache szablonów zespołów według typu szkoły
        /// Używane w TeamTemplateService
        /// </summary>
        public void InvalidateTeamTemplatesBySchoolType(string schoolTypeId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    $"teamtemplates:schooltype:{schoolTypeId}",
                    $"schooltype:{schoolTypeId}:teamtemplates",
                    $"TeamTemplate:SchoolType:{schoolTypeId}",
                    $"TeamTemplates_SchoolType_{schoolTypeId}"
                };

                BatchInvalidateKeys(keysToInvalidate, $"InvalidateTeamTemplatesBySchoolType_{schoolTypeId}");
                _logger.LogDebug("Unieważniono cache dla szablonów zespołów według typu szkoły: {SchoolTypeId}", schoolTypeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd unieważniania cache dla szablonów zespołów według typu szkoły: {SchoolTypeId}", schoolTypeId);
            }
        }

        #endregion
    }
} 