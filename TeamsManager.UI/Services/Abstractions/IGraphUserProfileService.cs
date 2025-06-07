using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TeamsManager.UI.Services; // Dla UserProfile i GraphTestResult

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu do pobierania profilu użytkownika z Microsoft Graph.
    /// Wzorowany na konwencjach z TeamsManager.Core.Abstractions.Services
    /// </summary>
    public interface IGraphUserProfileService
    {
        /// <summary>
        /// Pobiera profil użytkownika z Microsoft Graph
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Profil użytkownika lub null w przypadku błędu</returns>
        Task<UserProfile?> GetUserProfileAsync(string accessToken);
        
        /// <summary>
        /// Pobiera zdjęcie profilowe użytkownika
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Zdjęcie profilowe lub null w przypadku braku lub błędu</returns>
        Task<BitmapImage?> GetUserPhotoAsync(string accessToken);
        
        /// <summary>
        /// Testuje dostęp do Microsoft Graph API
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <returns>Wynik testów dostępu do Graph API</returns>
        Task<GraphTestResult> TestGraphAccessAsync(string accessToken);
    }
} 