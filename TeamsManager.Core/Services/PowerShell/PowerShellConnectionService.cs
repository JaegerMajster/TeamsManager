using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Implementacja serwisu zarządzającego połączeniem PowerShell i Microsoft Graph
    /// </summary>
    public class PowerShellConnectionService : IPowerShellConnectionService
    {
        private readonly ILogger<PowerShellConnectionService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly IPowerShellCacheService _cacheService;
        private readonly ITokenManager _tokenManager;
        private readonly IConfiguration _configuration;

        // Dla śledzenia ostatniego kontekstu połączenia
        private string? _lastConnectedUserUpn;
        private string? _lastApiAccessToken;

        // Współdzielony stan między instancjami Scoped
        private static Runspace? _sharedRunspace;
        private static bool _sharedIsConnected = false;
        private static readonly object _runspaceLock = new object();
        private bool _disposed = false;

        private const int MaxRetryAttempts = 3;

        public PowerShellConnectionService(
            ILogger<PowerShellConnectionService> logger,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            IPowerShellCacheService cacheService,
            ITokenManager tokenManager,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            InitializeRunspace();
        }

        public bool IsConnected => _sharedIsConnected;

        /// <summary>
        /// Sprawdza stan połączenia i automatycznie łączy się ponownie jeśli to konieczne
        /// </summary>
        private async Task<bool> ConnectIfNotConnectedAsync()
        {
            // Sprawdź czy mamy aktywne połączenie
            if (_sharedIsConnected && _sharedRunspace?.RunspaceStateInfo.State == RunspaceState.Opened)
            {
                _logger.LogDebug("Połączenie z Microsoft Graph jest aktywne");
                return true;
            }

            _logger.LogInformation("Brak aktywnego połączenia z Microsoft Graph, próba automatycznego połączenia");

            // Sprawdź czy mamy kontekst do ponownego połączenia
            if (string.IsNullOrWhiteSpace(_lastConnectedUserUpn) || string.IsNullOrWhiteSpace(_lastApiAccessToken))
            {
                _logger.LogWarning("Brak kontekstu do automatycznego ponownego połączenia (brak zapisanych danych użytkownika)");
                return false;
            }

            try
            {
                // Pobierz świeży token
                var freshToken = await _tokenManager.GetValidAccessTokenAsync(_lastConnectedUserUpn, _lastApiAccessToken);
                
                // Użyj istniejącej metody ConnectWithAccessTokenAsync
                var connected = await ConnectWithAccessTokenAsync(freshToken);
                
                if (connected)
                {
                    _logger.LogInformation("Automatyczne ponowne połączenie z Microsoft Graph zakończone sukcesem");
                }
                else
                {
                    _logger.LogError("Automatyczne ponowne połączenie z Microsoft Graph nie powiodło się");
                }
                
                return connected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas automatycznego ponownego łączenia z Microsoft Graph");
                await _notificationService.SendNotificationToUserAsync(_lastConnectedUserUpn, 
                    "Błąd automatycznego połączenia z Microsoft Graph", "error");
                return false;
            }
        }

        private void InitializeRunspace()
        {
            lock (_runspaceLock)
            {
                if (_sharedRunspace != null && _sharedRunspace.RunspaceStateInfo.State == RunspaceState.Opened)
                {
                    _logger.LogDebug("Używanie istniejącego środowiska PowerShell.");
                    return;
                }

                try
                {
                    var initialSessionState = InitialSessionState.CreateDefault2();
                    _sharedRunspace = RunspaceFactory.CreateRunspace(initialSessionState);
                    _sharedRunspace.Open();
                    _logger.LogInformation("Środowisko PowerShell zostało zainicjalizowane poprawnie.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zainicjalizować środowiska PowerShell. Próba inicjalizacji w trybie podstawowym.");
                    _sharedRunspace = null;
                    try
                    {
                        _sharedRunspace = RunspaceFactory.CreateRunspace();
                        _sharedRunspace.Open();
                        _logger.LogInformation("Środowisko PowerShell zostało zainicjalizowane w trybie podstawowym.");
                    }
                    catch (Exception basicEx)
                    {
                        _logger.LogError(basicEx, "Nie udało się zainicjalizować środowiska PowerShell nawet w trybie podstawowym.");
                        _sharedRunspace = null;
                    }
                }
            }
        }

        public async Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            var operationId = Guid.NewGuid().ToString();

            if (_sharedRunspace == null || _sharedRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: środowisko PowerShell nie jest poprawnie zainicjalizowane.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                    "Błąd inicjalizacji PowerShell: środowisko nie jest gotowe", "error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: token dostępu nie może być pusty.");
                await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                    "Błąd połączenia: brak tokenu dostępu", "error");
                return false;
            }

            // Powiadomienie o rozpoczęciu połączenia
            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 5,
                "Rozpoczynanie połączenia z Microsoft Graph API...");

            return await Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Próba połączenia z Microsoft Graph API. Scopes: [{Scopes}]",
                        scopes != null ? string.Join(", ", scopes) : "Brak");

                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 20,
                        "Importowanie modułów PowerShell...");

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _sharedRunspace;

                        // Import modułów
                        ps.AddScript(@"
                            Import-Module Microsoft.Graph.Authentication -ErrorAction SilentlyContinue
                            Import-Module Microsoft.Graph.Users -ErrorAction SilentlyContinue
                            Import-Module Microsoft.Graph.Teams -ErrorAction SilentlyContinue
                        ");
                        ps.Invoke();
                        ps.Commands.Clear();

                        await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 50,
                            "Uwierzytelnianie w Microsoft Graph...");

                        // Połączenie
                        var command = ps.AddCommand("Connect-MgGraph")
                                       .AddParameter("AccessToken", accessToken)
                                       .AddParameter("ErrorAction", "Stop");

                        if (scopes?.Length > 0)
                        {
                            command.AddParameter("Scopes", scopes);
                        }

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            var errorMessages = ps.Streams.Error.Select(e => e.ToString()).ToList();
                            foreach (var error in errorMessages)
                            {
                                _logger.LogError("Błąd PowerShell podczas łączenia: {Error}", error);
                            }

                            await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                                $"Błąd połączenia z Graph API: {string.Join("; ", errorMessages)}", "error");
                            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100,
                                "Połączenie zakończone niepowodzeniem");
                            return false;
                        }

                        await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 80,
                            "Weryfikacja połączenia...");

                        // Weryfikacja połączenia i cache kontekstu
                        ps.Commands.Clear();
                        var contextCheckResult = ps.AddCommand("Get-MgContext").Invoke();

                        if (!contextCheckResult.Any())
                        {
                            _logger.LogError("Połączenie z Microsoft Graph nie zostało ustanowione.");
                            await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                                "Nie udało się ustanowić połączenia z Microsoft Graph", "error");
                            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100,
                                "Weryfikacja połączenia zakończona niepowodzeniem");
                            return false;
                        }

                        _sharedIsConnected = true;

                        // Zapisz kontekst dla automatycznego reconnect
                        _lastConnectedUserUpn = currentUserUpn;
                        _lastApiAccessToken = accessToken;

                        // Zapisz token w TokenManager dla przyszłego użycia
                        var authResult = new AuthenticationResult(
                            accessToken,
                            false, // isExtendedLifeTimeToken
                            null,  // uniqueId
                            DateTimeOffset.UtcNow.AddHours(1), // Zakładamy 1h ważności
                            DateTimeOffset.UtcNow.AddHours(1),
                            currentUserUpn, // tenantId jako upn (tymczasowo)
                            null, // account
                            null, // idToken
                            scopes ?? new[] { "https://graph.microsoft.com/.default" }, // scopes
                            Guid.NewGuid(), // correlationId
                            "Bearer" // tokenType
                        );
                        await _tokenManager.StoreAuthenticationResultAsync(currentUserUpn, authResult);

                        // Cache kontekstu
                        var context = contextCheckResult.First();
                        _cacheService.Set("PowerShell_GraphContext", context);

                        await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100,
                            "Połączenie z Microsoft Graph ustanowione pomyślnie");
                        await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                            "✅ Pomyślnie połączono z Microsoft Graph API", "success");

                        _logger.LogInformation("Pomyślnie połączono z Microsoft Graph API.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wyjątek podczas łączenia z Microsoft Graph");
                    await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                        $"Błąd krytyczny podczas łączenia: {ex.Message}", "error");
                    await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100,
                        "Połączenie zakończone błędem krytycznym");
                    return false;
                }
            });
        }

        public void DisconnectFromGraph()
        {
            if (!_sharedIsConnected || _sharedRunspace == null) return;

            try
            {
                using var ps = PowerShell.Create();
                ps.Runspace = _sharedRunspace;
                ps.AddScript("Disconnect-MgGraph -ErrorAction SilentlyContinue");
                ps.Invoke();
                _logger.LogInformation("Rozłączono z Microsoft Graph.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rozłączania z Graph");
            }
            finally
            {
                _sharedIsConnected = false;
                _lastConnectedUserUpn = null;
                _lastApiAccessToken = null;
                _cacheService.InvalidateAllCache();
                
                // Wyczyść tokeny z TokenManager jeśli był zapisany użytkownik
                if (!string.IsNullOrWhiteSpace(_lastConnectedUserUpn))
                {
                    _tokenManager.ClearUserTokens(_lastConnectedUserUpn);
                }
            }
        }

        public bool ValidateRunspaceState()
        {
            lock (_runspaceLock)
            {
                if (_sharedRunspace == null || _sharedRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
                {
                    _logger.LogError("Środowisko PowerShell nie jest zainicjalizowane.");
                    return false;
                }

                if (!_sharedIsConnected)
                {
                    _logger.LogWarning("Brak aktywnego połączenia z Microsoft Graph.");
                    return false;
                }

                return true;
            }
        }

        public async Task<Collection<PSObject>?> ExecuteScriptAsync(
            string script,
            Dictionary<string, object>? parameters = null)
        {
            if (_sharedRunspace == null || _sharedRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Środowisko PowerShell nie jest zainicjalizowane.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogError("Skrypt nie może być pusty.");
                return null;
            }

            // Podstawowa sanityzacja
            if (script.Contains("`;") || script.Contains("$(") || script.Contains("${"))
            {
                _logger.LogWarning("Wykryto potencjalnie niebezpieczne znaki w skrypcie.");
            }

            _logger.LogDebug("Wykonywanie skryptu PowerShell ({Length} znaków)", script.Length);

            return await Task.Run(() =>
            {
                lock (_runspaceLock)
                {
                    try
                    {
                        using (var ps = PowerShell.Create())
                        {
                            ps.Runspace = _sharedRunspace;
                            ps.AddScript(script);

                            if (parameters != null)
                            {
                                ps.AddParameters(parameters);
                            }

                            var results = ps.Invoke();

                            // Logowanie strumieni
                            LogPowerShellStreams(ps);

                            if (ps.HadErrors && results.Count == 0)
                            {
                                _logger.LogError("Skrypt zakończył się błędami.");
                                return null;
                            }

                            _logger.LogDebug("Skrypt wykonany. Wyniki: {Count}", results.Count);
                            return results;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd wykonywania skryptu PowerShell");
                        return null;
                    }
                }
            });
        }

        public async Task<Collection<PSObject>?> ExecuteCommandWithRetryAsync(
            string commandName,
            Dictionary<string, object>? parameters = null,
            int maxRetries = MaxRetryAttempts)
        {
            // Automatyczne połączenie jeśli nie ma aktywnego
            if (!await ConnectIfNotConnectedAsync())
            {
                _logger.LogError("Nie można wykonać komendy '{CommandName}' - brak połączenia z Microsoft Graph", commandName);
                return null;
            }

            if (!ValidateRunspaceState()) return null;

            int attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug("Wykonywanie komendy PowerShell: {CommandName}, próba {Attempt}/{MaxRetries}",
                        commandName, attempt, maxRetries);

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _sharedRunspace;
                        var command = ps.AddCommand(commandName)
                                       .AddParameter("ErrorAction", "Stop");

                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                command.AddParameter(param.Key, param.Value);
                            }
                        }

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                            throw new InvalidOperationException($"PowerShell errors: {errors}");
                        }

                        _logger.LogDebug("Komenda '{CommandName}' wykonana. Wyniki: {Count}",
                            commandName, results.Count);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (IsTransientError(ex) && attempt < maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // Exponential backoff
                        _logger.LogWarning(ex,
                            "Próba {Attempt}/{MaxRetries} wykonania komendy '{CommandName}' nie powiodła się. Ponawianie za {Delay}s",
                            attempt, maxRetries, commandName, delay.TotalSeconds);

                        await Task.Delay(delay);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            _logger.LogError(lastException, "Nie udało się wykonać komendy '{CommandName}' po {MaxRetries} próbach.",
                commandName, maxRetries);
            return null;
        }

        private bool IsTransientError(Exception ex)
        {
            return ex.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("temporarily", StringComparison.OrdinalIgnoreCase);
        }

        private void LogPowerShellStreams(PowerShell ps)
        {
            foreach (var error in ps.Streams.Error)
            {
                _logger.LogError("PowerShell Error: {Error}", error.ToString());
            }

            foreach (var warning in ps.Streams.Warning)
            {
                _logger.LogWarning("PowerShell Warning: {Warning}", warning.ToString());
            }

            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                foreach (var info in ps.Streams.Information)
                {
                    _logger.LogDebug("PowerShell Info: {Info}", info.ToString());
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Nie dispose'ujemy współdzielonego runspace
                _logger.LogDebug("PowerShellConnectionService disposed (Scoped)");
            }

            _disposed = true;
        }

        public async Task<T?> ExecuteWithAutoConnectAsync<T>(Func<Task<T>> operation) where T : class
        {
            // Najpierw spróbuj połączyć się jeśli nie ma połączenia
            if (!await ConnectIfNotConnectedAsync())
            {
                _logger.LogError("Nie można wykonać operacji - brak połączenia z Microsoft Graph");
                return null;
            }

            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania operacji PowerShell");
                
                // Jeśli błąd związany z połączeniem, spróbuj ponownie po reconnect
                if (IsConnectionError(ex))
                {
                    _logger.LogInformation("Wykryto błąd połączenia, próba ponownego połączenia i wykonania operacji");
                    _sharedIsConnected = false; // Wymuś reconnect
                    
                    if (await ConnectIfNotConnectedAsync())
                    {
                        try
                        {
                            return await operation();
                        }
                        catch (Exception retryEx)
                        {
                            _logger.LogError(retryEx, "Błąd podczas ponownej próby wykonania operacji");
                        }
                    }
                }
                
                return null;
            }
        }

        /// <summary>
        /// Sprawdza czy błąd jest związany z połączeniem
        /// </summary>
        private bool IsConnectionError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("unauthorized") ||
                   message.Contains("token") ||
                   message.Contains("expired") ||
                   message.Contains("invalid_grant") ||
                   message.Contains("not connected") ||
                   message.Contains("connect-mggraph");
        }
    }
}