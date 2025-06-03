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
    }
}