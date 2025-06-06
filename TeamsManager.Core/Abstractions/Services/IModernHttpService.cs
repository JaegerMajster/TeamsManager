using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs nowoczesnego HTTP service wykorzystującego Microsoft.Extensions.Http.Resilience
    /// Zastępuje stare wzorce resilience
    /// </summary>
    public interface IModernHttpService
    {
        /// <summary>
        /// Wykonuje żądanie GET do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <typeparam name="T">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="endpoint">Endpoint Graph API (np. "v1.0/groups")</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zdeserializowany obiekt lub null w przypadku błędu</returns>
        Task<T?> GetFromGraphAsync<T>(string endpoint, string? accessToken = null) where T : class;

        /// <summary>
        /// Wykonuje żądanie POST do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <typeparam name="TRequest">Typ danych do wysłania</typeparam>
        /// <typeparam name="TResponse">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="data">Dane do wysłania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zdeserializowany obiekt odpowiedzi lub null w przypadku błędu</returns>
        Task<TResponse?> PostToGraphAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest data, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Wykonuje żądanie GET do zewnętrznego API z resilience
        /// </summary>
        /// <typeparam name="T">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="url">Pełny URL do API</param>
        /// <returns>Zdeserializowany obiekt lub null w przypadku błędu</returns>
        Task<T?> GetFromExternalApiAsync<T>(string url) where T : class;

        /// <summary>
        /// Sprawdza dostępność Microsoft Graph API
        /// </summary>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli API jest dostępne</returns>
        Task<bool> CheckGraphApiHealthAsync(string? accessToken = null);
    }
} 