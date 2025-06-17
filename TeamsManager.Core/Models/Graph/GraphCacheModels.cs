using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models.Graph
{
    /// <summary>
    /// Metadane wpisu cache Graph API
    /// </summary>
    public class GraphCacheMetadata
    {
        /// <summary>
        /// ETag z Graph API response
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Czas utworzenia wpisu cache
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czas wygaśnięcia wpisu cache
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Czas ostatniego dostępu do wpisu cache
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Liczba dostępów do wpisu cache
        /// </summary>
        public int AccessCount { get; set; } = 1;

        /// <summary>
        /// Endpoint Graph API z którego pochodzą dane
        /// </summary>
        public string? GraphEndpoint { get; set; }

        /// <summary>
        /// Informacje o rate limiting z Graph API
        /// </summary>
        public GraphCacheRateLimitInfo? RateLimitInfo { get; set; }

        /// <summary>
        /// Czy wpis cache jest aktualny
        /// </summary>
        public bool IsValid => ExpiresAt == null || ExpiresAt > DateTime.UtcNow;

        /// <summary>
        /// Wiek wpisu cache w minutach
        /// </summary>
        public double AgeInMinutes => (DateTime.UtcNow - CreatedAt).TotalMinutes;

        /// <summary>
        /// Czy wpis cache jest często używany (więcej niż 5 dostępów)
        /// </summary>
        public bool IsFrequentlyAccessed => AccessCount > 5;
    }

    /// <summary>
    /// Informacje o rate limiting dla cache Graph API
    /// </summary>
    public class GraphCacheRateLimitInfo
    {
        /// <summary>
        /// Pozostała liczba żądań w oknie czasowym
        /// </summary>
        public int? RemainingRequests { get; set; }

        /// <summary>
        /// Czas resetowania limitu żądań
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Czy osiągnięto limit żądań
        /// </summary>
        public bool IsLimitReached { get; set; }

        /// <summary>
        /// Czas oczekiwania przed kolejnym żądaniem (w sekundach)
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        /// <summary>
        /// Endpoint Graph API dla którego obowiązuje limit
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Czy można wykonać żądanie do Graph API
        /// </summary>
        public bool CanMakeRequest => !IsLimitReached || (ResetTime.HasValue && ResetTime <= DateTime.UtcNow);
    }

    /// <summary>
    /// Metryki wydajności cache Graph API
    /// </summary>
    public class GraphCacheMetrics
    {
        /// <summary>
        /// Całkowita liczba żądań do cache
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Liczba trafień cache (cache hits)
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Liczba chybień cache (cache misses)
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Współczynnik trafień cache (0-100%)
        /// </summary>
        public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;

        /// <summary>
        /// Kompatybilność z API - alias dla HitRatio.
        /// </summary>
        public double HitRate => HitRatio;

        /// <summary>
        /// Kompatybilność z API - alias dla TotalRequests.
        /// </summary>
        public long TotalOperations => TotalRequests;

        /// <summary>
        /// Kompatybilność z API - alias dla AverageAccessTimeMs.
        /// </summary>
        public double AverageOperationTimeMs => AverageAccessTimeMs;

        /// <summary>
        /// Kompatybilność z API - sprawdza czy cache jest wydajny.
        /// </summary>
        public bool IsPerformant => IsEfficient;

        /// <summary>
        /// Kompatybilność z API - zwraca status wydajności.
        /// </summary>
        public string GetPerformanceStatus()
        {
            if (HitRatio > 80) return "Excellent";
            if (HitRatio > 60) return "Good";
            if (HitRatio > 40) return "Fair";
            return "Poor";
        }

        /// <summary>
        /// Liczba wpisów w cache
        /// </summary>
        public int CacheEntryCount { get; set; }

        /// <summary>
        /// Liczba unieważnień cache
        /// </summary>
        public long InvalidationCount { get; set; }

        /// <summary>
        /// Średni czas dostępu do cache (w milisekundach)
        /// </summary>
        public double AverageAccessTimeMs { get; set; }

        /// <summary>
        /// Liczba żądań Graph API zaoszczędzonych dzięki cache
        /// </summary>
        public long SavedGraphRequests => CacheHits;

        /// <summary>
        /// Czas ostatniego resetowania metryk
        /// </summary>
        public DateTime LastResetTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Szczegółowe metryki dla poszczególnych endpointów Graph API
        /// </summary>
        public Dictionary<string, GraphEndpointCacheMetrics> EndpointMetrics { get; set; } = new();

        /// <summary>
        /// Czy cache działa efektywnie (hit ratio > 70%)
        /// </summary>
        public bool IsEfficient => HitRatio > 70;

        /// <summary>
        /// Resetuje metryki cache
        /// </summary>
        public void Reset()
        {
            TotalRequests = 0;
            CacheHits = 0;
            CacheMisses = 0;
            InvalidationCount = 0;
            AverageAccessTimeMs = 0;
            LastResetTime = DateTime.UtcNow;
            EndpointMetrics.Clear();
        }

        /// <summary>
        /// Dodaje metryki dla endpointu
        /// </summary>
        public void AddEndpointMetrics(string endpoint, bool isHit, double accessTimeMs)
        {
            if (!EndpointMetrics.ContainsKey(endpoint))
            {
                EndpointMetrics[endpoint] = new GraphEndpointCacheMetrics { Endpoint = endpoint };
            }

            var metrics = EndpointMetrics[endpoint];
            metrics.TotalRequests++;
            
            if (isHit)
                metrics.CacheHits++;
            else
                metrics.CacheMisses++;

            metrics.UpdateAverageAccessTime(accessTimeMs);
        }
    }

    /// <summary>
    /// Metryki cache dla konkretnego endpointu Graph API
    /// </summary>
    public class GraphEndpointCacheMetrics
    {
        /// <summary>
        /// Endpoint Graph API
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Liczba żądań do tego endpointu
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Liczba trafień cache dla tego endpointu
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Liczba chybień cache dla tego endpointu
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Współczynnik trafień cache dla tego endpointu
        /// </summary>
        public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;

        /// <summary>
        /// Średni czas dostępu do cache dla tego endpointu
        /// </summary>
        public double AverageAccessTimeMs { get; set; }

        /// <summary>
        /// Aktualizuje średni czas dostępu
        /// </summary>
        public void UpdateAverageAccessTime(double newAccessTimeMs)
        {
            if (TotalRequests == 1)
            {
                AverageAccessTimeMs = newAccessTimeMs;
            }
            else
            {
                AverageAccessTimeMs = ((AverageAccessTimeMs * (TotalRequests - 1)) + newAccessTimeMs) / TotalRequests;
            }
        }
    }

    /// <summary>
    /// Wynik walidacji cache Graph API
    /// </summary>
    public class GraphCacheValidationResult
    {
        /// <summary>
        /// Czy cache jest aktualny
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Powód nieaktualności cache
        /// </summary>
        public string? InvalidationReason { get; set; }

        /// <summary>
        /// ETag z cache
        /// </summary>
        public string? CachedETag { get; set; }

        /// <summary>
        /// Aktualny ETag z Graph API
        /// </summary>
        public string? CurrentETag { get; set; }

        /// <summary>
        /// Czy ETag się zmienił
        /// </summary>
        public bool ETagChanged => !string.Equals(CachedETag, CurrentETag, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Czas wygaśnięcia cache
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Czy cache wygasł czasowo
        /// </summary>
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt <= DateTime.UtcNow;

        /// <summary>
        /// Rekomendowana akcja
        /// </summary>
        public CacheValidationAction RecommendedAction { get; set; }

        /// <summary>
        /// Tworzy wynik walidacji dla aktualnego cache
        /// </summary>
        public static GraphCacheValidationResult Valid(string? etag = null)
        {
            return new GraphCacheValidationResult
            {
                IsValid = true,
                CachedETag = etag,
                CurrentETag = etag,
                RecommendedAction = CacheValidationAction.UseCache
            };
        }

        /// <summary>
        /// Tworzy wynik walidacji dla nieaktualnego cache
        /// </summary>
        public static GraphCacheValidationResult Invalid(string reason, string? cachedETag = null, string? currentETag = null)
        {
            return new GraphCacheValidationResult
            {
                IsValid = false,
                InvalidationReason = reason,
                CachedETag = cachedETag,
                CurrentETag = currentETag,
                RecommendedAction = CacheValidationAction.RefreshFromGraph
            };
        }

        /// <summary>
        /// Tworzy wynik walidacji dla wygasłego cache
        /// </summary>
        public static GraphCacheValidationResult Expired(DateTime expiresAt)
        {
            return new GraphCacheValidationResult
            {
                IsValid = false,
                InvalidationReason = "Cache expired",
                ExpiresAt = expiresAt,
                RecommendedAction = CacheValidationAction.RefreshFromGraph
            };
        }
    }

    /// <summary>
    /// Akcje rekomendowane po walidacji cache
    /// </summary>
    public enum CacheValidationAction
    {
        /// <summary>
        /// Użyj danych z cache
        /// </summary>
        UseCache,

        /// <summary>
        /// Odśwież dane z Graph API
        /// </summary>
        RefreshFromGraph,

        /// <summary>
        /// Sprawdź ETag przed użyciem
        /// </summary>
        CheckETag,

        /// <summary>
        /// Usuń wpis z cache
        /// </summary>
        RemoveFromCache
    }

    /// <summary>
    /// Statystyki User ID resolution cache
    /// TASK 2.5.3 - Model dla statystyk User ID cache
    /// </summary>
    public class UserIdCacheStats
    {
        /// <summary>
        /// Liczba wpisów User ID w cache
        /// </summary>
        public int TotalUserIdEntries { get; set; }

        /// <summary>
        /// Liczba wpisów profili użytkowników w cache
        /// </summary>
        public int TotalUserProfileEntries { get; set; }

        /// <summary>
        /// Współczynnik trafień cache dla endpointu /v1.0/users
        /// </summary>
        public double UserEndpointHitRatio { get; set; }

        /// <summary>
        /// Całkowita liczba żądań do endpointu /v1.0/users
        /// </summary>
        public long UserEndpointTotalRequests { get; set; }

        /// <summary>
        /// Czy User ID cache działa efektywnie (hit ratio > 60%)
        /// </summary>
        public bool IsEfficient => UserEndpointHitRatio > 60;

        /// <summary>
        /// Całkowita liczba wpisów związanych z użytkownikami
        /// </summary>
        public int TotalUserEntries => TotalUserIdEntries + TotalUserProfileEntries;

        /// <summary>
        /// Pobiera podsumowanie statystyk
        /// </summary>
        public string GetSummary()
        {
            return $"User ID Cache: {TotalUserIdEntries} IDs, {TotalUserProfileEntries} profiles, " +
                   $"{UserEndpointHitRatio:F1}% hit ratio, {UserEndpointTotalRequests} total requests";
        }
    }

    /// <summary>
    /// Metadane zespołu dla cache
    /// TASK 2.5.4 - Model dla metadanych zespołu
    /// </summary>
    public class TeamMetadata
    {
        /// <summary>
        /// ID zespołu
        /// </summary>
        public string? TeamId { get; set; }

        /// <summary>
        /// Nazwa zespołu
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Opis zespołu
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Widoczność zespołu
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Czy zespół jest zarchiwizowany
        /// </summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// Liczba członków zespołu
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Liczba właścicieli zespołu
        /// </summary>
        public int OwnerCount { get; set; }

        /// <summary>
        /// Liczba kanałów w zespole
        /// </summary>
        public int ChannelCount { get; set; }

        /// <summary>
        /// Data utworzenia zespołu
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// Data ostatniej modyfikacji
        /// </summary>
        public DateTime? LastModifiedDateTime { get; set; }

        /// <summary>
        /// URL zespołu
        /// </summary>
        public string? WebUrl { get; set; }

        /// <summary>
        /// Klasyfikacja zespołu
        /// </summary>
        public string? Classification { get; set; }

        /// <summary>
        /// Czy metadane są aktualne (mniej niż 1 godzina)
        /// </summary>
        public bool IsUpToDate => LastModifiedDateTime.HasValue && 
                                   (DateTime.UtcNow - LastModifiedDateTime.Value).TotalHours < 1;
    }

    /// <summary>
    /// Metadane grupy dla cache
    /// TASK 2.5.4 - Model dla metadanych grupy
    /// </summary>
    public class GroupMetadata
    {
        /// <summary>
        /// ID grupy
        /// </summary>
        public string? GroupId { get; set; }

        /// <summary>
        /// Nazwa grupy
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Opis grupy
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Typ grupy (Unified, Security, Distribution)
        /// </summary>
        public string? GroupType { get; set; }

        /// <summary>
        /// Widoczność grupy
        /// </summary>
        public string? Visibility { get; set; }

        /// <summary>
        /// Czy grupa ma zespół Teams
        /// </summary>
        public bool HasTeam { get; set; }

        /// <summary>
        /// Liczba członków grupy
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Data utworzenia grupy
        /// </summary>
        public DateTime? CreatedDateTime { get; set; }

        /// <summary>
        /// Data ostatniej modyfikacji
        /// </summary>
        public DateTime? LastModifiedDateTime { get; set; }

        /// <summary>
        /// Email grupy
        /// </summary>
        public string? Mail { get; set; }

        /// <summary>
        /// Czy grupa jest włączona dla poczty
        /// </summary>
        public bool MailEnabled { get; set; }

        /// <summary>
        /// Czy grupa jest włączona dla zabezpieczeń
        /// </summary>
        public bool SecurityEnabled { get; set; }

        /// <summary>
        /// Czy metadane są aktualne (mniej niż 1 godzina)
        /// </summary>
        public bool IsUpToDate => LastModifiedDateTime.HasValue && 
                                   (DateTime.UtcNow - LastModifiedDateTime.Value).TotalHours < 1;
    }

    /// <summary>
    /// Statystyki Team/Group metadata cache
    /// TASK 2.5.4 - Model dla statystyk Team/Group cache
    /// </summary>
    public class TeamGroupCacheStats
    {
        /// <summary>
        /// Liczba wpisów metadanych zespołów w cache
        /// </summary>
        public int TotalTeamEntries { get; set; }

        /// <summary>
        /// Liczba wpisów ustawień zespołów w cache
        /// </summary>
        public int TotalTeamSettingsEntries { get; set; }

        /// <summary>
        /// Liczba wpisów członków zespołów w cache
        /// </summary>
        public int TotalTeamMemberEntries { get; set; }

        /// <summary>
        /// Liczba wpisów kanałów zespołów w cache
        /// </summary>
        public int TotalTeamChannelEntries { get; set; }

        /// <summary>
        /// Liczba wpisów metadanych grup w cache
        /// </summary>
        public int TotalGroupEntries { get; set; }

        /// <summary>
        /// Współczynnik trafień cache dla endpointu /v1.0/teams
        /// </summary>
        public double TeamEndpointHitRatio { get; set; }

        /// <summary>
        /// Całkowita liczba żądań do endpointu /v1.0/teams
        /// </summary>
        public long TeamEndpointTotalRequests { get; set; }

        /// <summary>
        /// Współczynnik trafień cache dla endpointu /v1.0/groups
        /// </summary>
        public double GroupEndpointHitRatio { get; set; }

        /// <summary>
        /// Całkowita liczba żądań do endpointu /v1.0/groups
        /// </summary>
        public long GroupEndpointTotalRequests { get; set; }

        /// <summary>
        /// Czy Team cache działa efektywnie (hit ratio > 70%)
        /// </summary>
        public bool IsTeamCacheEfficient => TeamEndpointHitRatio > 70;

        /// <summary>
        /// Czy Group cache działa efektywnie (hit ratio > 70%)
        /// </summary>
        public bool IsGroupCacheEfficient => GroupEndpointHitRatio > 70;

        /// <summary>
        /// Całkowita liczba wpisów związanych z zespołami
        /// </summary>
        public int TotalTeamRelatedEntries => TotalTeamEntries + TotalTeamSettingsEntries + 
                                              TotalTeamMemberEntries + TotalTeamChannelEntries;

        /// <summary>
        /// Pobiera podsumowanie statystyk
        /// </summary>
        public string GetSummary()
        {
            return $"Team/Group Cache: {TotalTeamRelatedEntries} team entries, {TotalGroupEntries} group entries, " +
                   $"Teams: {TeamEndpointHitRatio:F1}% hit ratio, Groups: {GroupEndpointHitRatio:F1}% hit ratio";
        }
    }

    /// <summary>
    /// Informacje o wygasającym wpisie cache
    /// TASK 2.5.5 - Model dla TTL management
    /// </summary>
    public class CacheExpiryInfo
    {
        /// <summary>
        /// Klucz cache
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Czas wygaśnięcia
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Pozostały czas do wygaśnięcia
        /// </summary>
        public TimeSpan RemainingTime { get; set; }

        /// <summary>
        /// Wzorzec klucza cache
        /// </summary>
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// Liczba dostępów do wpisu
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Czas ostatniego dostępu
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Czy wpis jest często używany (więcej niż 5 dostępów)
        /// </summary>
        public bool IsFrequentlyUsed => AccessCount > 5;

        /// <summary>
        /// Czy wpis wygasa w ciągu najbliższych 5 minut
        /// </summary>
        public bool IsExpiringSoon => RemainingTime <= TimeSpan.FromMinutes(5);

        /// <summary>
        /// Czy wpis już wygasł
        /// </summary>
        public bool IsExpired => RemainingTime <= TimeSpan.Zero;
    }

    /// <summary>
    /// Statystyki TTL dla cache
    /// TASK 2.5.5 - Model dla statystyk TTL
    /// </summary>
    public class TtlStats
    {
        /// <summary>
        /// Całkowita liczba wpisów w cache
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Liczba wpisów już wygasłych
        /// </summary>
        public int ExpiredEntries { get; set; }

        /// <summary>
        /// Liczba wpisów wygasających w ciągu 5 minut
        /// </summary>
        public int ExpiringIn5Minutes { get; set; }

        /// <summary>
        /// Liczba wpisów wygasających w ciągu 15 minut
        /// </summary>
        public int ExpiringIn15Minutes { get; set; }

        /// <summary>
        /// Liczba wpisów wygasających w ciągu 1 godziny
        /// </summary>
        public int ExpiringIn1Hour { get; set; }

        /// <summary>
        /// Liczba wpisów długoterminowych (powyżej 1 godziny)
        /// </summary>
        public int LongLivedEntries { get; set; }

        /// <summary>
        /// Liczba wpisów bez wygaśnięcia
        /// </summary>
        public int NoExpiryEntries { get; set; }

        /// <summary>
        /// Najkrótszy TTL w cache
        /// </summary>
        public TimeSpan? ShortestTtl { get; set; }

        /// <summary>
        /// Najdłuższy TTL w cache
        /// </summary>
        public TimeSpan? LongestTtl { get; set; }

        /// <summary>
        /// Procent wpisów wygasających w ciągu 15 minut
        /// </summary>
        public double PercentageExpiringSoon => TotalEntries > 0 ? 
            (double)(ExpiredEntries + ExpiringIn5Minutes + ExpiringIn15Minutes) / TotalEntries * 100 : 0;

        /// <summary>
        /// Czy cache wymaga czyszczenia (więcej niż 10% wygasłych wpisów)
        /// </summary>
        public bool NeedsCleanup => ExpiredEntries > 0 && (double)ExpiredEntries / TotalEntries > 0.1;

        /// <summary>
        /// Średni TTL w cache
        /// </summary>
        public TimeSpan? AverageTtl
        {
            get
            {
                if (ShortestTtl.HasValue && LongestTtl.HasValue)
                {
                    return TimeSpan.FromTicks((ShortestTtl.Value.Ticks + LongestTtl.Value.Ticks) / 2);
                }
                return null;
            }
        }

        /// <summary>
        /// Pobiera podsumowanie statystyk TTL
        /// </summary>
        public string GetSummary()
        {
            return $"TTL Stats: {TotalEntries} total, {ExpiredEntries} expired, " +
                   $"{ExpiringIn5Minutes} expiring in 5min, {ExpiringIn15Minutes} in 15min, " +
                   $"{PercentageExpiringSoon:F1}% expiring soon";
        }
    }
} 