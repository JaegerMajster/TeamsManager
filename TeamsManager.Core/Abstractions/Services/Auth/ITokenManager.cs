using System;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace TeamsManager.Core.Abstractions.Services.Auth
{
    /// <summary>
    /// Zarządza tokenami dostępu do Microsoft Graph dla PowerShell
    /// </summary>
    public interface ITokenManager
    {
        /// <summary>
        /// Pobiera ważny token dostępu, automatycznie odświeżając jeśli wygasł
        /// </summary>
        /// <param name="userUpn">UPN użytkownika dla kontekstu</param>
        /// <param name="apiAccessToken">Token API do przepływu OBO</param>
        /// <returns>Ważny token dostępu do Graph lub null jeśli nie można uzyskać</returns>
        Task<string?> GetValidAccessTokenAsync(string userUpn, string apiAccessToken);

        /// <summary>
        /// Odświeża token używając refresh token
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli odświeżenie się powiodło</returns>
        Task<bool> RefreshTokenAsync(string userUpn);

        /// <summary>
        /// Przechowuje wynik uwierzytelnienia dla użytkownika
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="result">Wynik uwierzytelnienia z MSAL</param>
        /// <returns>Task reprezentujący operację</returns>
        Task StoreAuthenticationResultAsync(string userUpn, AuthenticationResult result);

        /// <summary>
        /// Sprawdza czy użytkownik ma ważny token
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli token jest ważny</returns>
        bool HasValidToken(string userUpn);

        /// <summary>
        /// Usuwa wszystkie tokeny użytkownika z cache
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        void ClearUserTokens(string userUpn);

        /// <summary>
        /// Pobiera dane o tokenie bez odświeżania
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Informacje o tokenie lub null jeśli brak</returns>
        Task<TokenInfo?> GetTokenInfoAsync(string userUpn);
    }

    /// <summary>
    /// Informacje o tokenie dostępu
    /// </summary>
    public class TokenInfo
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTimeOffset ExpiresOn { get; set; }
        public string[] Scopes { get; set; } = Array.Empty<string>();
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOn.AddMinutes(-5);
    }
} 