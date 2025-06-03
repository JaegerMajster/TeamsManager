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