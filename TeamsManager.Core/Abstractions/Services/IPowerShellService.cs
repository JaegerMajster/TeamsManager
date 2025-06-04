using System;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Główny serwis fasadowy dla operacji PowerShell/Microsoft Graph
    /// </summary>
    public interface IPowerShellService : IDisposable
    {
        /// <summary>
        /// Sprawdza czy jest aktywne połączenie z Microsoft Graph
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Łączy się z Microsoft Graph używając tokenu dostępu
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <param name="scopes">Opcjonalne zakresy uprawnień</param>
        /// <returns>True jeśli połączenie udane, false w przeciwnym wypadku</returns>
        Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null);

        /// <summary>
        /// Wykonuje operację z automatycznym połączeniem i obsługą tokenu OBO
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO)</param>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationDescription">Opis operacji do logowania</param>
        /// <returns>Wynik operacji lub domyślna wartość w przypadku błędu</returns>
        Task<T?> ExecuteWithAutoConnectAsync<T>(string apiAccessToken, Func<Task<T>> operation, string? operationDescription = null);

        /// <summary>
        /// Serwis zarządzający zespołami i kanałami
        /// </summary>
        IPowerShellTeamManagementService Teams { get; }

        /// <summary>
        /// Serwis zarządzający użytkownikami, członkostwem i licencjami
        /// </summary>
        IPowerShellUserManagementService Users { get; }

        /// <summary>
        /// Serwis zarządzający operacjami masowymi
        /// </summary>
        IPowerShellBulkOperationsService BulkOperations { get; }
    }
}