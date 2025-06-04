using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

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
    }
}