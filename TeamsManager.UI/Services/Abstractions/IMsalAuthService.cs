using Microsoft.Identity.Client;
using System.Threading.Tasks;
using System.Windows;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Interfejs serwisu zarządzającego autentykacją MSAL.
    /// Wzorowany na konwencjach z TeamsManager.Core.Abstractions.Services
    /// </summary>
    public interface IMsalAuthService
    {
        /// <summary>
        /// Pobiera token w trybie interaktywnym (z oknem logowania)
        /// </summary>
        /// <param name="window">Okno rodzica dla dialogu logowania</param>
        /// <returns>Rezultat autentykacji lub null w przypadku błędu</returns>
        Task<AuthenticationResult?> AcquireTokenInteractiveAsync(Window window);
        
        /// <summary>
        /// Wylogowuje użytkownika
        /// </summary>
        /// <returns>Task reprezentujący operację asynchroniczną</returns>
        Task SignOutAsync();
        
        /// <summary>
        /// Pobiera token dla Microsoft Graph w trybie cichym
        /// </summary>
        /// <returns>Token dostępu lub null w przypadku błędu</returns>
        Task<string?> AcquireGraphTokenAsync();
        
        /// <summary>
        /// Pobiera token dla Microsoft Graph w trybie interaktywnym
        /// </summary>
        /// <param name="window">Okno rodzica dla dialogu logowania</param>
        /// <returns>Token dostępu lub null w przypadku błędu</returns>
        Task<string?> AcquireGraphTokenInteractiveAsync(Window window);
        
        /// <summary>
        /// Pobiera token w trybie cichym (bez UI)
        /// </summary>
        /// <returns>Rezultat autentykacji lub null jeśli wymagana interakcja użytkownika</returns>
        Task<AuthenticationResult?> AcquireTokenSilentAsync();
        
        /// <summary>
        /// Pobiera token dostępu w trybie cichym
        /// </summary>
        /// <returns>Token dostępu lub null w przypadku błędu</returns>
        Task<string?> GetAccessTokenAsync();
    }
} 