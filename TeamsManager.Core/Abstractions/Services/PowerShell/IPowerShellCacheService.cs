using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.Core.Enums;

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
    }
}