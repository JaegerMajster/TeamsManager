using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis odpowiedzialny za rozwiązywanie identyfikatorów użytkowników
    /// </summary>
    public interface IPowerShellUserResolverService
    {
        /// <summary>
        /// Pobiera ID użytkownika na podstawie UPN
        /// </summary>
        Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false);
        
        /// <summary>
        /// Pobiera ID użytkownika z cache (bez pobierania z Graph)
        /// </summary>
        Task<string?> GetCachedUserIdAsync(string userUpn);
    }
} 