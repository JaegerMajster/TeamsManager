using System;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis odpowiedzialny za zarządzanie połączeniem PowerShell i Microsoft Graph
    /// </summary>
    public interface IPowerShellConnectionService : IDisposable
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
        /// Rozłącza się z Microsoft Graph
        /// </summary>
        void DisconnectFromGraph();

        /// <summary>
        /// Waliduje stan środowiska PowerShell
        /// </summary>
        /// <returns>True jeśli środowisko jest gotowe do pracy</returns>
        bool ValidateRunspaceState();

        /// <summary>
        /// Wykonuje skrypt PowerShell
        /// </summary>
        /// <param name="script">Skrypt do wykonania</param>
        /// <param name="parameters">Opcjonalne parametry</param>
        /// <returns>Kolekcja wyników lub null w przypadku błędu</returns>
        Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null);

        /// <summary>
        /// Wykonuje komendę PowerShell z mechanizmem retry
        /// </summary>
        /// <param name="commandName">Nazwa komendy</param>
        /// <param name="parameters">Parametry komendy</param>
        /// <param name="maxRetries">Maksymalna liczba prób (opcjonalne)</param>
        /// <returns>Kolekcja wyników lub null w przypadku błędu</returns>
        Task<Collection<PSObject>?> ExecuteCommandWithRetryAsync(
            string commandName,
            Dictionary<string, object>? parameters = null,
            int? maxRetries = null);

        /// <summary>
        /// Wykonuje operację z automatycznym połączeniem jeśli to konieczne
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="operation">Operacja do wykonania</param>
        /// <returns>Wynik operacji lub domyślna wartość w przypadku błędu</returns>
        Task<T?> ExecuteWithAutoConnectAsync<T>(Func<Task<T>> operation) where T : class;

        /// <summary>
        /// Pobiera informacje o stanie połączenia i odporności systemu
        /// </summary>
        /// <returns>Szczegółowe informacje o stanie połączenia</returns>
        Task<ConnectionHealthInfo> GetConnectionHealthAsync();
    }

    /// <summary>
    /// Informacje o stanie połączenia PowerShell
    /// </summary>
    public class ConnectionHealthInfo
    {
        public bool IsConnected { get; set; }
        public string RunspaceState { get; set; } = "";
        public string CircuitBreakerState { get; set; } = "";
        public DateTime? LastConnectionAttempt { get; set; }
        public DateTime? LastSuccessfulConnection { get; set; }
        public bool TokenValid { get; set; }
    }
}