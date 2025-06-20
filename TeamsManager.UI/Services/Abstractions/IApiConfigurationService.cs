using TeamsManager.UI.Models.Configuration;
using System.Threading.Tasks;

namespace TeamsManager.UI.Services.Abstractions
{
    /// <summary>
    /// Serwis do zarządzania konfiguracją API z automatycznym wykrywaniem środowiska
    /// </summary>
    public interface IApiConfigurationService
    {
        /// <summary>
        /// Pobiera aktualną konfigurację API
        /// </summary>
        Task<ApiConfiguration> GetApiConfigurationAsync();

        /// <summary>
        /// Zapisuje konfigurację API
        /// </summary>
        Task SaveApiConfigurationAsync(ApiConfiguration config);

        /// <summary>
        /// Automatycznie wykrywa i konfiguruje API na podstawie środowiska
        /// </summary>
        Task<ApiConfiguration> AutoDetectApiConfigurationAsync();

        /// <summary>
        /// Testuje połączenie z API
        /// </summary>
        Task<bool> TestApiConnectionAsync(string baseUrl);

        /// <summary>
        /// Sprawdza czy API jest dostępne
        /// </summary>
        Task<bool> IsApiAvailableAsync();
    }
} 