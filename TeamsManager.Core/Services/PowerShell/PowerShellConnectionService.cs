using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Common;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

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
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly IConfiguration _configuration;

        // Dla śledzenia ostatniego kontekstu połączenia
        private string? _lastConnectedUserUpn;
        private string? _lastApiAccessToken;

        // Nowe pola dla resilience
        private readonly CircuitBreaker _connectionCircuitBreaker;
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan _initialRetryDelay;
        private readonly TimeSpan _maxRetryDelay;
        private DateTime? _lastConnectionAttempt;
        private DateTime? _lastSuccessfulConnection;

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
            IOperationHistoryService operationHistoryService,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Konfiguracja resilience
            var resilienceConfig = configuration.GetSection("PowerShellServiceConfig:ConnectionResilience");
            
            _maxRetryAttempts = int.Parse(resilienceConfig["RetryPolicy:MaxAttempts"] ?? "3");
            _initialRetryDelay = TimeSpan.FromSeconds(int.Parse(resilienceConfig["RetryPolicy:InitialDelaySeconds"] ?? "1"));
            _maxRetryDelay = TimeSpan.FromSeconds(int.Parse(resilienceConfig["RetryPolicy:MaxDelaySeconds"] ?? "30"));
            
            var cbFailureThreshold = int.Parse(resilienceConfig["CircuitBreaker:FailureThreshold"] ?? "5");
            var cbOpenDuration = TimeSpan.FromSeconds(int.Parse(resilienceConfig["CircuitBreaker:OpenDurationSeconds"] ?? "60"));
            var cbSamplingDuration = TimeSpan.FromSeconds(int.Parse(resilienceConfig["CircuitBreaker:SamplingDurationSeconds"] ?? "10"));
            
            _connectionCircuitBreaker = new CircuitBreaker(cbFailureThreshold, cbOpenDuration, cbSamplingDuration);

            // Subskrypcja eventów Circuit Breaker
            _connectionCircuitBreaker.StateChanged += OnCircuitBreakerStateChanged;
            _connectionCircuitBreaker.FailureRecorded += OnCircuitBreakerFailureRecorded;

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

            // Audyt próby automatycznego połączenia
            var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ConfigurationChanged,
                "PowerShellReconnection",
                "MicrosoftGraph",
                "Automatic Graph Reconnection",
                details: JsonSerializer.Serialize(new
                {
                    Reason = "Connection lost or not established",
                    LastSuccessfulConnection = _lastSuccessfulConnection,
                    CircuitBreakerState = _connectionCircuitBreaker.State.ToString()
                })
            );

            try
            {
                return await _connectionCircuitBreaker.ExecuteAsync(async () =>
                {
                    _logger.LogInformation("Circuit Breaker State: {State}. Attempting to connect...", 
                        _connectionCircuitBreaker.State);
                    
                    // Powiadomienie o próbie reconnect
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        $"🔄 Automatyczna próba odnowienia połączenia z Microsoft Graph (Circuit Breaker: {_connectionCircuitBreaker.State})",
                        "info"
                    );
                    
                    // Pobierz świeży token
                    var token = await _tokenManager.GetValidAccessTokenAsync(_lastConnectedUserUpn, _lastApiAccessToken);
                    
                    // Użyj istniejącej metody ConnectWithAccessTokenAsync
                    var result = await ConnectWithAccessTokenAsync(token, 
                        _configuration.GetSection("PowerShellServiceConfig:DefaultScopesForGraph").GetChildren().Select(x => x.Value).Where(v => v != null).ToArray()!);
                    
                    if (!result)
                    {
                        throw new InvalidOperationException("Failed to establish connection to Microsoft Graph");
                    }
                    
                    // Audyt sukcesu
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Completed,
                        "Automatic reconnection successful"
                    );
                    
                    return result;
                });
            }
            catch (CircuitBreakerOpenException ex)
            {
                // Audyt Circuit Breaker Open
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    $"Circuit breaker is open - too many recent failures",
                    ex.ToString()
                );
                
                _logger.LogWarning(ex, "Circuit breaker is open. Connection attempts are temporarily suspended.");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    "⚠️ Połączenie z Microsoft Graph jest tymczasowo niedostępne. Spróbuj ponownie za chwilę.",
                    "warning");
                return false;
            }
            catch (Exception ex)
            {
                // Audyt ogólnego błędu
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    $"Reconnection failed: {ex.Message}",
                    ex.ToString()
                );
                
                _logger.LogError(ex, "Failed to connect to Microsoft Graph");
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

            _lastConnectionAttempt = DateTime.UtcNow;
            
            // Audyt rozpoczęcia połączenia
            var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ConfigurationChanged,
                "PowerShellConnection",
                "MicrosoftGraph",
                "Microsoft Graph API Connection",
                details: JsonSerializer.Serialize(new
                {
                    Scopes = scopes ?? new[] { "default" },
                    CircuitBreakerState = _connectionCircuitBreaker.State.ToString(),
                    RetryPolicy = new
                    {
                        MaxAttempts = _maxRetryAttempts,
                        InitialDelay = _initialRetryDelay.TotalSeconds,
                        MaxDelay = _maxRetryDelay.TotalSeconds
                    }
                })
            );

            if (_sharedRunspace == null || _sharedRunspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: środowisko PowerShell nie jest poprawnie zainicjalizowane.");
                
                // Audyt niepowodzenia
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    "PowerShell environment not initialized"
                );
                
                await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                    "Błąd inicjalizacji PowerShell: środowisko nie jest gotowe", "error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: token dostępu nie może być pusty.");
                
                // Audyt niepowodzenia
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    "Access token is null or empty"
                );
                
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

                            // Audyt niepowodzenia PowerShell
                            await _operationHistoryService.UpdateOperationStatusAsync(
                                operationHistory.Id,
                                OperationStatus.Failed,
                                $"PowerShell errors: {string.Join("; ", errorMessages)}"
                            );

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
                            
                            // Audyt niepowodzenia weryfikacji
                            await _operationHistoryService.UpdateOperationStatusAsync(
                                operationHistory.Id,
                                OperationStatus.Failed,
                                "Graph connection verification failed - no context returned"
                            );
                            
                            await _notificationService.SendNotificationToUserAsync(currentUserUpn,
                                "Nie udało się ustanowić połączenia z Microsoft Graph", "error");
                            await _notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, 100,
                                "Weryfikacja połączenia zakończona niepowodzeniem");
                            return false;
                        }

                        _sharedIsConnected = true;
                        _lastSuccessfulConnection = DateTime.UtcNow;

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

                        // Audyt sukcesu
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operationHistory.Id,
                            OperationStatus.Completed,
                            "Successfully connected to Microsoft Graph"
                        );

                        _logger.LogInformation("Pomyślnie połączono z Microsoft Graph API.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wyjątek podczas łączenia z Microsoft Graph");
                    
                    // Audyt niepowodzenia
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Failed,
                        $"Connection failed: {ex.Message}",
                        ex.ToString()
                    );
                    
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
            int? maxRetries = null)
        {
            maxRetries ??= _maxRetryAttempts; // Użyj konfiguracji zamiast stałej
            
            // Audyt rozpoczęcia komendy z retry
            var operationHistory = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ConfigurationChanged,
                "PowerShellCommand",
                commandName,
                $"Execute PowerShell Command: {commandName}",
                details: JsonSerializer.Serialize(new
                {
                    CommandName = commandName,
                    Parameters = parameters?.Keys.ToList() ?? new List<string>(),
                    MaxRetries = maxRetries,
                    CircuitBreakerState = _connectionCircuitBreaker.State.ToString()
                })
            );
            
            if (!ValidateRunspaceState()) 
            {
                // Spróbuj auto-reconnect przed rezygnacją
                if (!await ConnectIfNotConnectedAsync())
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Failed,
                        "PowerShell is not connected and reconnection failed"
                    );
                    
                    _logger.LogError("Cannot execute command: PowerShell is not connected and reconnection failed.");
                    return null;
                }
            }

            int attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug("Executing PowerShell command: {CommandName}, attempt {Attempt}/{MaxRetries}",
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

                        _logger.LogDebug("Command '{CommandName}' executed successfully. Results: {Count}",
                            commandName, results.Count);
                        
                        // Po sukcesie
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operationHistory.Id,
                            OperationStatus.Completed,
                            $"Command executed successfully after {attempt} attempt(s)"
                        );
                        
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Sprawdź czy to błąd związany z połączeniem
                    if (IsConnectionError(ex))
                    {
                        _logger.LogWarning("Connection error detected. Attempting to reconnect...");
                        _sharedIsConnected = false; // Force reconnection
                        
                        if (!await ConnectIfNotConnectedAsync())
                        {
                            _logger.LogError("Failed to reconnect after connection error.");
                            break;
                        }
                    }
                    else if (IsTransientError(ex) && attempt < maxRetries)
                    {
                        // Oblicz delay z uwzględnieniem konfiguracji
                        var delay = CalculateRetryDelay(attempt);
                        
                        _logger.LogWarning(ex,
                            "Attempt {Attempt}/{MaxRetries} failed for command '{CommandName}'. Retrying in {Delay}s",
                            attempt, maxRetries, commandName, delay.TotalSeconds);

                        await Task.Delay(delay);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Audyt ostatecznego niepowodzenia
            await _operationHistoryService.UpdateOperationStatusAsync(
                operationHistory.Id,
                OperationStatus.Failed,
                $"Command failed after {maxRetries} attempts: {lastException?.Message}",
                lastException?.ToString()
            );
            
            _logger.LogError(lastException, "Failed to execute command '{CommandName}' after {MaxRetries} attempts.",
                commandName, maxRetries);
            return null;
        }

        private TimeSpan CalculateRetryDelay(int attemptNumber)
        {
            // Exponential backoff z jitter
            var exponentialDelay = _initialRetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1);
            var jitter = new Random().Next(0, 1000); // 0-1s jitter
            var totalDelay = TimeSpan.FromMilliseconds(exponentialDelay + jitter);
            
            // Cap at max delay
            return totalDelay > _maxRetryDelay ? _maxRetryDelay : totalDelay;
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
                // Odsubskrybuj eventy
                if (_connectionCircuitBreaker != null)
                {
                    _connectionCircuitBreaker.StateChanged -= OnCircuitBreakerStateChanged;
                    _connectionCircuitBreaker.FailureRecorded -= OnCircuitBreakerFailureRecorded;
                }
                
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

        public async Task<ConnectionHealthInfo> GetConnectionHealthAsync()
        {
            return new ConnectionHealthInfo
            {
                IsConnected = _sharedIsConnected,
                RunspaceState = _sharedRunspace?.RunspaceStateInfo.State.ToString() ?? "Not initialized",
                CircuitBreakerState = _connectionCircuitBreaker.State.ToString(),
                LastConnectionAttempt = _lastConnectionAttempt,
                LastSuccessfulConnection = _lastSuccessfulConnection,
                TokenValid = _lastConnectedUserUpn != null && _lastApiAccessToken != null 
                    ? _tokenManager.HasValidToken(_lastConnectedUserUpn)
                    : false
            };
        }

        private async void OnCircuitBreakerStateChanged(object? sender, CircuitBreakerStateChangedEventArgs e)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            
            // Audyt zmiany stanu
            await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.ConfigurationChanged,
                "CircuitBreaker",
                "PowerShellConnection",
                $"Circuit Breaker State Change: {e.OldState} -> {e.NewState}",
                details: JsonSerializer.Serialize(new
                {
                    OldState = e.OldState.ToString(),
                    NewState = e.NewState.ToString(),
                    Timestamp = e.Timestamp,
                    CurrentFailureCount = _connectionCircuitBreaker.FailureCount
                })
            );
            
            // Powiadomienie o znaczących zmianach stanu
            if (e.NewState == CircuitState.Open)
            {
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "🚫 Circuit Breaker otwarty - zbyt wiele nieudanych prób połączenia. Połączenia zostały tymczasowo wstrzymane.",
                    "error"
                );
            }
            else if (e.OldState == CircuitState.Open && e.NewState == CircuitState.HalfOpen)
            {
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "🔄 Circuit Breaker w trybie testowym - próba przywrócenia połączenia.",
                    "info"
                );
            }
            else if (e.OldState != CircuitState.Closed && e.NewState == CircuitState.Closed)
            {
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "✅ Circuit Breaker zamknięty - połączenie przywrócone.",
                    "success"
                );
            }
        }

        private async void OnCircuitBreakerFailureRecorded(object? sender, CircuitBreakerFailureEventArgs e)
        {
            // Powiadomienie tylko gdy zbliżamy się do limitu
            if (e.CurrentFailureCount >= e.Threshold - 1)
            {
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"⚠️ Uwaga: {e.CurrentFailureCount}/{e.Threshold} prób połączenia zakończonych niepowodzeniem. Jeszcze jedna nieudana próba spowoduje tymczasowe wstrzymanie połączeń.",
                    "warning"
                );
            }
        }
    }
}
