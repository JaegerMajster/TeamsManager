using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający cache'owaniem danych Graph API
    /// Zastępuje PowerShell cache z Graph API specyfiką (ETag support, rate limiting)
    /// </summary>
    public interface IGraphCacheService
    {
        #region User ID Resolution (Critical P0 functionality)

        /// <summary>
        /// Pobiera ID użytkownika z cache lub Graph API
        /// Graph API Endpoint: GET /v1.0/users/{user-principal-name}
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie z Graph API</param>
        /// <returns>ID użytkownika lub null</returns>
        Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false);

        /// <summary>
        /// Zapisuje ID użytkownika w cache z ETag
        /// </summary>
        /// <param name="userUpn">User Principal Name</param>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="etag">ETag z Graph API response (opcjonalny)</param>
        void SetUserId(string userUpn, string userId, string? etag = null);

        #endregion

        #region Generic Cache Operations with ETag Support

        /// <summary>
        /// Pobiera obiekt z cache z sprawdzeniem ETag
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość z cache</param>
        /// <returns>True jeśli znaleziono w cache</returns>
        bool TryGetValue<T>(string key, out T? value);

        /// <summary>
        /// Pobiera obiekt z cache wraz z metadanymi Graph API
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość z cache</param>
        /// <param name="metadata">Metadane cache (ETag, expiry, itp.)</param>
        /// <returns>True jeśli znaleziono w cache</returns>
        bool TryGetValueWithMetadata<T>(string key, out T? value, out GraphCacheMetadata? metadata);

        /// <summary>
        /// Zapisuje obiekt w cache z metadanymi Graph API
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość do zapisania</param>
        /// <param name="duration">Czas przechowywania (domyślnie 15 minut)</param>
        /// <param name="etag">ETag z Graph API response</param>
        /// <param name="rateLimitInfo">Informacje o rate limiting</param>
        void Set<T>(string key, T value, TimeSpan? duration = null, string? etag = null, GraphCacheRateLimitInfo? rateLimitInfo = null);

        /// <summary>
        /// Usuwa wpis z cache
        /// </summary>
        /// <param name="key">Klucz do usunięcia</param>
        void Remove(string key);

        #endregion

        #region Graph API Specific Cache Invalidation

        /// <summary>
        /// Unieważnia cache dla użytkownika
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="userUpn">UPN użytkownika</param>
        void InvalidateUserCache(string? userId = null, string? userUpn = null);

        /// <summary>
        /// Unieważnia cache dla zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        void InvalidateTeamCache(string teamId);

        /// <summary>
        /// Unieważnia cały cache Graph API
        /// </summary>
        void InvalidateAllCache();

        /// <summary>
        /// Unieważnia cache kanałów dla zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        void InvalidateChannelsForTeam(string teamId);

        /// <summary>
        /// Unieważnia cache konkretnego kanału
        /// </summary>
        /// <param name="channelId">ID kanału</param>
        void InvalidateChannel(string channelId);

        /// <summary>
        /// Unieważnia cache kanału i jego zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        void InvalidateChannelAndTeam(string teamId, string channelId);

        #endregion

        #region Graph API Cache Options & Configuration

        /// <summary>
        /// Zwraca opcje cache krótkoterminowego dla często zmieniających się danych Graph API
        /// (członkowie zespołu, kanały) - 5 minut
        /// </summary>
        MemoryCacheEntryOptions GetShortTermCacheOptions();

        /// <summary>
        /// Zwraca opcje cache średnioterminowego dla umiarkowanie zmieniających się danych Graph API
        /// (użytkownicy, zespoły) - 15 minut
        /// </summary>
        MemoryCacheEntryOptions GetMediumTermCacheOptions();

        /// <summary>
        /// Zwraca opcje cache długoterminowego dla rzadko zmieniających się danych Graph API
        /// (organizacja, licencje, uprawnienia) - 1 godzina
        /// </summary>
        MemoryCacheEntryOptions GetLongTermCacheOptions();

        /// <summary>
        /// Zwraca opcje cache z tokenem unieważniania dla Graph API
        /// </summary>
        MemoryCacheEntryOptions GetDefaultCacheEntryOptions();

        #endregion

        #region Enhanced Cache Features for Graph API

        /// <summary>
        /// Pobiera obiekt z cache z automatycznym zbieraniem metryk wydajności Graph API
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość z cache</param>
        /// <returns>True jeśli znaleziono w cache</returns>
        bool TryGetValueWithMetrics<T>(string key, out T? value);

        /// <summary>
        /// Unieważnia wiele kluczy cache w jednej operacji batch
        /// </summary>
        /// <param name="cacheKeys">Lista kluczy do unieważnienia</param>
        /// <param name="operationName">Nazwa operacji dla logowania</param>
        void BatchInvalidateKeys(IEnumerable<string> cacheKeys, string operationName = "BatchInvalidation");

        /// <summary>
        /// Wstępnie ładuje dane do cache z Graph API (cache warming)
        /// </summary>
        /// <param name="cacheKey">Klucz cache</param>
        /// <param name="dataLoader">Funkcja ładująca dane z Graph API</param>
        /// <param name="duration">Czas przechowywania</param>
        /// <param name="respectRateLimit">Czy respektować rate limiting Graph API</param>
        Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader, TimeSpan? duration = null, bool respectRateLimit = true);

        /// <summary>
        /// Unieważnia cache na podstawie wzorca klucza
        /// </summary>
        /// <param name="pattern">Wzorzec do wyszukania</param>
        /// <param name="operationName">Nazwa operacji dla logowania</param>
        void InvalidateByPattern(string pattern, string operationName = "PatternInvalidation");

        /// <summary>
        /// Pobiera metryki wydajności cache dla Graph API
        /// </summary>
        /// <returns>Obiekt z metrykami cache</returns>
        GraphCacheMetrics GetCacheMetrics();

        #endregion

        #region Rate Limiting Integration

        /// <summary>
        /// Sprawdza czy można wykonać żądanie Graph API na podstawie rate limiting
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <returns>True jeśli można wykonać żądanie</returns>
        bool CanMakeGraphRequest(string endpoint);

        /// <summary>
        /// Zapisuje informacje o rate limiting z Graph API response
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="rateLimitInfo">Informacje o rate limiting</param>
        void SetRateLimitInfo(string endpoint, GraphRateLimitInfo rateLimitInfo);

        /// <summary>
        /// Pobiera informacje o rate limiting dla endpointu
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <returns>Informacje o rate limiting lub null</returns>
        GraphRateLimitInfo? GetRateLimitInfo(string endpoint);

        #endregion

        #region Cache Validation & ETag Support

        /// <summary>
        /// Waliduje czy cache jest aktualny na podstawie ETag
        /// </summary>
        /// <param name="key">Klucz cache</param>
        /// <param name="currentETag">Aktualny ETag z Graph API</param>
        /// <returns>Wynik walidacji cache</returns>
        GraphCacheValidationResult ValidateCache(string key, string? currentETag);

        /// <summary>
        /// Aktualizuje ETag dla istniejącego wpisu cache
        /// </summary>
        /// <param name="key">Klucz cache</param>
        /// <param name="newETag">Nowy ETag</param>
        void UpdateETag(string key, string newETag);

        /// <summary>
        /// Sprawdza czy wpis cache wygasł
        /// </summary>
        /// <param name="key">Klucz cache</param>
        /// <returns>True jeśli cache wygasł</returns>
        bool IsCacheExpired(string key);

        #endregion
    }
} 