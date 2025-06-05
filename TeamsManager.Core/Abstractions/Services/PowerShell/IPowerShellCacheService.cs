using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis zarządzający cache'owaniem danych PowerShell/Graph
    /// </summary>
    public interface IPowerShellCacheService
    {
        /// <summary>
        /// Pobiera ID użytkownika z cache lub Graph
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie z Graph</param>
        /// <returns>ID użytkownika lub null</returns>
        Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false);

        /// <summary>
        /// Zapisuje ID użytkownika w cache
        /// </summary>
        /// <param name="userUpn">User Principal Name</param>
        /// <param name="userId">ID użytkownika</param>
        void SetUserId(string userUpn, string userId);

        /// <summary>
        /// Pobiera obiekt z cache
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość z cache</param>
        /// <returns>True jeśli znaleziono w cache</returns>
        bool TryGetValue<T>(string key, out T? value);

        /// <summary>
        /// Zapisuje obiekt w cache
        /// </summary>
        /// <typeparam name="T">Typ obiektu</typeparam>
        /// <param name="key">Klucz cache</param>
        /// <param name="value">Wartość do zapisania</param>
        /// <param name="duration">Czas przechowywania (domyślnie 15 minut)</param>
        void Set<T>(string key, T value, TimeSpan? duration = null);

        /// <summary>
        /// Usuwa wpis z cache
        /// </summary>
        /// <param name="key">Klucz do usunięcia</param>
        void Remove(string key);

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
        /// Unieważnia cały cache PowerShell
        /// </summary>
        void InvalidateAllCache();

        /// <summary>
        /// Zwraca domyślne opcje cache z tokenem unieważniania
        /// </summary>
        MemoryCacheEntryOptions GetDefaultCacheEntryOptions();

        /// <summary>
        /// Zwraca krótkie opcje cache z tokenem unieważniania
        /// </summary>
        MemoryCacheEntryOptions GetShortCacheEntryOptions();

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

        /// <summary>
        /// Unieważnia cache dla konkretnego działu po ID.
        /// </summary>
        /// <param name="departmentId">ID działu.</param>
        void InvalidateDepartment(string departmentId);

        /// <summary>
        /// Unieważnia cache dla poddziałów danego działu nadrzędnego.
        /// </summary>
        /// <param name="parentId">ID działu nadrzędnego.</param>
        void InvalidateSubDepartments(string parentId);

        /// <summary>
        /// Unieważnia cache dla użytkowników w danym dziale.
        /// </summary>
        /// <param name="departmentId">ID działu.</param>
        void InvalidateUsersInDepartment(string departmentId);

        /// <summary>
        /// Unieważnia globalne listy działów (wszystkie i root-only).
        /// To powinno być wywoływane po każdej zmianie w strukturze działów,
        /// co może wpłynąć na te listy.
        /// </summary>
        void InvalidateAllDepartmentLists();

        /// <summary>
        /// Unieważnia cache listy użytkowników.
        /// </summary>
        void InvalidateUserListCache();

        /// <summary>
        /// Unieważnia cache użytkowników według roli.
        /// Usuwa listy użytkowników przechowywane według roli.
        /// </summary>
        /// <param name="role">Rola użytkowników do unieważnienia</param>
        void InvalidateUsersByRole(UserRole role);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych użytkowników.
        /// Usuwa klucz "Users_AllActive" używany przez UserService.
        /// </summary>
        void InvalidateAllActiveUsersList();

        /// <summary>
        /// Unieważnia cache użytkownika w sposób kompleksowy, obsługując również klucze używane przez UserService.
        /// Obsługuje scenariusze zmiany UPN i roli użytkownika.
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="userUpn">Aktualny UPN użytkownika</param>
        /// <param name="oldUpn">Poprzedni UPN (jeśli został zmieniony)</param>
        /// <param name="role">Aktualna rola użytkownika</param>
        /// <param name="oldRole">Poprzednia rola (jeśli została zmieniona)</param>
        void InvalidateUserAndRelatedData(string? userId, string? userUpn, string? oldUpn, UserRole? role, UserRole? oldRole);

        /// <summary>
        /// Unieważnia cache przedmiotu w sposób granularny.
        /// Usuwa cache przedmiotu według ID i kodu.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu</param>
        /// <param name="subjectCode">Kod przedmiotu (np. "MAT")</param>
        void InvalidateSubjectById(string subjectId, string? subjectCode = null);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych przedmiotów.
        /// </summary>
        void InvalidateAllActiveSubjectsList();

        /// <summary>
        /// Unieważnia cache listy nauczycieli dla danego przedmiotu.
        /// </summary>
        /// <param name="subjectId">ID przedmiotu</param>
        void InvalidateTeachersForSubject(string subjectId);

        /// <summary>
        /// Unieważnia cache roku szkolnego według ID.
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego</param>
        void InvalidateSchoolYearById(string schoolYearId);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych lat szkolnych.
        /// </summary>
        void InvalidateAllActiveSchoolYearsList();

        /// <summary>
        /// Unieważnia cache bieżącego roku szkolnego.
        /// </summary>
        void InvalidateCurrentSchoolYear();

        /// <summary>
        /// Unieważnia cache szablonu zespołu według ID.
        /// </summary>
        /// <param name="templateId">ID szablonu zespołu</param>
        void InvalidateTeamTemplateById(string templateId);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych szablonów zespołów.
        /// </summary>
        void InvalidateAllActiveTeamTemplatesList();

        /// <summary>
        /// Unieważnia cache szablonów zespołów dla danego typu szkoły.
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        void InvalidateTeamTemplatesBySchoolType(string schoolTypeId);

        // Metody granularnej inwalidacji dla TeamService
        /// <summary>
        /// Unieważnia cache zespołu według ID.
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        void InvalidateTeamById(string teamId);

        /// <summary>
        /// Unieważnia cache zespołów według właściciela.
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela zespołu</param>
        void InvalidateTeamsByOwner(string ownerUpn);

        /// <summary>
        /// Unieważnia cache zespołów według statusu.
        /// </summary>
        /// <param name="status">Status zespołu</param>
        void InvalidateTeamsByStatus(TeamStatus status);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych zespołów.
        /// </summary>
        void InvalidateAllActiveTeamsList();

        /// <summary>
        /// Unieważnia cache listy zarchiwizowanych zespołów.
        /// </summary>
        void InvalidateArchivedTeamsList();

        /// <summary>
        /// Unieważnia cache listy zespołów o specyficznym statusie Active.
        /// </summary>
        void InvalidateTeamSpecificByStatus();

        // Metody granularnej inwalidacji dla SchoolTypeService
        /// <summary>
        /// Unieważnia cache typu szkoły według ID.
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        void InvalidateSchoolTypeById(string schoolTypeId);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych typów szkół.
        /// </summary>
        void InvalidateAllActiveSchoolTypesList();

        // Metody granularnej inwalidacji dla ApplicationSettingService
        /// <summary>
        /// Unieważnia cache ustawienia według klucza.
        /// </summary>
        /// <param name="key">Klucz ustawienia</param>
        void InvalidateSettingByKey(string key);

        /// <summary>
        /// Unieważnia cache ustawień według kategorii.
        /// </summary>
        /// <param name="category">Kategoria ustawień</param>
        void InvalidateSettingsByCategory(string category);

        /// <summary>
        /// Unieważnia cache listy wszystkich aktywnych ustawień aplikacji.
        /// </summary>
        void InvalidateAllActiveSettingsList();

        // ETAP 6/8: Zaawansowane funkcje cache P2
        /// <summary>
        /// Pobiera obiekt z cache z automatycznym zbieraniem metryk wydajności
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
        /// Wstępnie ładuje dane do cache (cache warming)
        /// </summary>
        /// <param name="cacheKey">Klucz cache</param>
        /// <param name="dataLoader">Funkcja ładująca dane</param>
        /// <param name="duration">Czas przechowywania</param>
        Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader, TimeSpan? duration = null);

        /// <summary>
        /// Unieważnia cache na podstawie wzorca klucza
        /// </summary>
        /// <param name="pattern">Wzorzec do wyszukania</param>
        /// <param name="operationName">Nazwa operacji dla logowania</param>
        void InvalidateByPattern(string pattern, string operationName = "PatternInvalidation");

        /// <summary>
        /// Pobiera metryki wydajności cache
        /// </summary>
        /// <returns>Obiekt z metrykami cache</returns>
        CacheMetrics GetCacheMetrics();
    }
}