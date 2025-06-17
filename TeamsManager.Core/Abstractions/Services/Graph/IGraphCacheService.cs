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
    /// Cache service z Graph API specyfiką (ETag support, rate limiting)
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
        void SetRateLimitInfo(string endpoint, GraphRateLimitStatus rateLimitInfo);

        /// <summary>
        /// Pobiera informacje o rate limiting dla endpointu
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <returns>Informacje o rate limiting lub null</returns>
        GraphRateLimitStatus? GetRateLimitInfo(string endpoint);

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

        #region Application-Specific Cache Invalidation

        /// <summary>
        /// Unieważnia cache dla konkretnego ustawienia aplikacji
        /// Używane w ApplicationSettingService
        /// </summary>
        /// <param name="settingKey">Klucz ustawienia</param>
        void InvalidateSettingByKey(string settingKey);

        /// <summary>
        /// Unieważnia cache wszystkich list departamentów
        /// Używane w DepartmentService
        /// </summary>
        void InvalidateAllDepartmentLists();

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych ustawień
        /// Używane w ApplicationSettingService
        /// </summary>
        void InvalidateAllActiveSettingsList();

        /// <summary>
        /// Unieważnia cache ustawień według kategorii
        /// Używane w ApplicationSettingService
        /// </summary>
        /// <param name="category">Kategoria ustawień</param>
        void InvalidateSettingsByCategory(string category);

        /// <summary>
        /// Unieważnia cache konkretnego departamentu
        /// Używane w DepartmentService
        /// </summary>
        /// <param name="departmentId">ID departamentu</param>
        void InvalidateDepartment(string departmentId);

        /// <summary>
        /// Unieważnia cache użytkowników według roli
        /// Używane w UserService
        /// </summary>
        /// <param name="role">Rola użytkownika</param>
        void InvalidateUsersByRole(string role);

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych użytkowników
        /// Używane w UserService
        /// </summary>
        void InvalidateAllActiveUsersList();

        /// <summary>
        /// Unieważnia cache listy użytkowników
        /// Używane w UserService
        /// </summary>
        void InvalidateUserListCache();

        /// <summary>
        /// Unieważnia cache użytkownika i powiązanych danych
        /// Używane w UserService
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        void InvalidateUserAndRelatedData(string userId);

        /// <summary>
        /// Unieważnia cache subdepartamentów
        /// Używane w DepartmentService
        /// </summary>
        /// <param name="parentDepartmentId">ID departamentu nadrzędnego</param>
        void InvalidateSubDepartments(string parentDepartmentId);

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych lat szkolnych
        /// Używane w SchoolYearService
        /// </summary>
        void InvalidateAllActiveSchoolYearsList();

        /// <summary>
        /// Unieważnia cache bieżącego roku szkolnego
        /// Używane w SchoolYearService
        /// </summary>
        void InvalidateCurrentSchoolYear();

        /// <summary>
        /// Unieważnia cache roku szkolnego według ID
        /// Używane w SchoolYearService
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego</param>
        void InvalidateSchoolYearById(string schoolYearId);

        /// <summary>
        /// Unieważnia cache nauczycieli dla przedmiotu
        /// Używane w SubjectService
        /// </summary>
        /// <param name="subjectId">ID przedmiotu</param>
        void InvalidateTeachersForSubject(string subjectId);

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych przedmiotów
        /// Używane w SubjectService
        /// </summary>
        void InvalidateAllActiveSubjectsList();

        /// <summary>
        /// Unieważnia cache przedmiotu według ID
        /// Używane w SubjectService
        /// </summary>
        /// <param name="subjectId">ID przedmiotu</param>
        void InvalidateSubjectById(string subjectId);

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych typów szkół
        /// Używane w SchoolTypeService
        /// </summary>
        void InvalidateAllActiveSchoolTypesList();

        /// <summary>
        /// Unieważnia cache typu szkoły według ID
        /// Używane w SchoolTypeService
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        void InvalidateSchoolTypeById(string schoolTypeId);

        /// <summary>
        /// Unieważnia cache szablonu zespołu według ID
        /// Używane w TeamTemplateService
        /// </summary>
        /// <param name="templateId">ID szablonu</param>
        void InvalidateTeamTemplateById(string templateId);

        /// <summary>
        /// Unieważnia cache wszystkich aktywnych szablonów zespołów
        /// Używane w TeamTemplateService
        /// </summary>
        void InvalidateAllActiveTeamTemplatesList();

        /// <summary>
        /// Unieważnia cache szablonów zespołów według typu szkoły
        /// Używane w TeamTemplateService
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        void InvalidateTeamTemplatesBySchoolType(string schoolTypeId);

        #endregion
    }
} 