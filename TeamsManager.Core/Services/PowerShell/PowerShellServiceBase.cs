using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Klasa bazowa dla wszystkich serwisów PowerShell zawierająca wspólne metody diagnostyczne
    /// </summary>
    public abstract class PowerShellServiceBase
    {
        protected readonly IPowerShellConnectionService _connectionService;
        protected readonly IPowerShellCacheService _cacheService;
        protected readonly ILogger _logger;

        protected PowerShellServiceBase(
            IPowerShellConnectionService connectionService,
            IPowerShellCacheService cacheService,
            ILogger logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Wykonuje operację PowerShell z pełną diagnostyką i obsługą błędów
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationName">Nazwa operacji do logowania</param>
        /// <param name="requiredPermissions">Wymagane uprawnienia</param>
        /// <param name="validateBefore">Czy wykonać walidację przed operacją</param>
        /// <returns>Wynik operacji</returns>
        protected async Task<T?> ExecuteWithDiagnosticsAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            string[]? requiredPermissions = null,
            bool validateBefore = true) where T : class
        {
            return await _connectionService.ExecuteWithDiagnosticsAsync(
                operation, 
                operationName, 
                requiredPermissions, 
                validateBefore);
        }

        /// <summary>
        /// Sprawdza czy system ma odpowiednie uprawnienia
        /// </summary>
        /// <param name="requiredPermissions">Lista wymaganych uprawnień</param>
        /// <returns>True jeśli ma uprawnienia</returns>
        protected async Task<bool> ValidatePermissionsAsync(params string[] requiredPermissions)
        {
            try
            {
                var permissionInfo = await _connectionService.ValidatePermissionsAsync(requiredPermissions);
                
                if (!permissionInfo.IsValid)
                {
                    _logger.LogWarning("Brak wymaganych uprawnień: {RequiredPermissions}. Dostępne: {AvailableScopes}", 
                        string.Join(", ", requiredPermissions),
                        string.Join(", ", permissionInfo.AvailableScopes ?? new List<string>()));
                }

                return permissionInfo.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień: {RequiredPermissions}", 
                    string.Join(", ", requiredPermissions));
                return false;
            }
        }

        /// <summary>
        /// Wykonuje diagnostykę połączenia z opcjonalnymi testami komend
        /// </summary>
        /// <param name="testCommands">Komendy do przetestowania</param>
        /// <returns>Informacje diagnostyczne</returns>
        protected async Task<Models.PowerShellDiagnosticInfo> DiagnoseConnectionAsync(params string[] testCommands)
        {
            return await _connectionService.DiagnoseConnectionAsync(true, testCommands);
        }

        /// <summary>
        /// Loguje szczegóły operacji z kontekstem
        /// </summary>
        /// <param name="operationName">Nazwa operacji</param>
        /// <param name="parameters">Parametry operacji (bez wrażliwych danych)</param>
        /// <param name="logLevel">Poziom logowania</param>
        protected void LogOperationDetails(string operationName, object? parameters = null, LogLevel logLevel = LogLevel.Debug)
        {
            if (_logger.IsEnabled(logLevel))
            {
                if (parameters != null)
                {
                    _logger.Log(logLevel, "Operacja PowerShell: {OperationName} z parametrami: {Parameters}", 
                        operationName, System.Text.Json.JsonSerializer.Serialize(parameters));
                }
                else
                {
                    _logger.Log(logLevel, "Operacja PowerShell: {OperationName}", operationName);
                }
            }
        }

        /// <summary>
        /// Unieważnia cache w sposób granularny
        /// </summary>
        /// <param name="cacheKeys">Klucze cache do unieważnienia</param>
        protected void InvalidateCache(params string[] cacheKeys)
        {
            foreach (var key in cacheKeys)
            {
                _cacheService.Remove(key);
                _logger.LogDebug("Unieważniono cache: {CacheKey}", key);
            }
        }

        /// <summary>
        /// Sprawdza czy operacja może być wykonana (podstawowa walidacja)
        /// </summary>
        /// <param name="operationName">Nazwa operacji</param>
        /// <returns>True jeśli operacja może być wykonana</returns>
        protected bool CanExecuteOperation(string operationName)
        {
            if (!_connectionService.ValidateRunspaceState())
            {
                _logger.LogError("Nie można wykonać operacji {OperationName} - PowerShell runspace nie jest gotowy", operationName);
                return false;
            }

            if (!_connectionService.IsConnected)
            {
                _logger.LogError("Nie można wykonać operacji {OperationName} - brak połączenia z Microsoft Graph", operationName);
                return false;
            }

            return true;
        }
    }
} 