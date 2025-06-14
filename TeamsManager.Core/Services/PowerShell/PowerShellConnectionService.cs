using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Common;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Exceptions.PowerShell;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Implementacja serwisu zarządzającego połączeniem PowerShell i Microsoft Graph
    /// </summary>
    public class PowerShellConnectionService : IPowerShellConnectionService
    {
        private readonly ILogger<PowerShellConnectionService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
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
            IServiceScopeFactory serviceScopeFactory,
            IPowerShellCacheService cacheService,
            ITokenManager tokenManager,
            IOperationHistoryService operationHistoryService,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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
        /// Wykonuje akcję w nowym scope z dostępem do scoped services
        /// </summary>
        private async Task ExecuteWithScopedServicesAsync(Func<ICurrentUserService, INotificationService, Task> action)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            
            await action(currentUserService, notificationService);
        }

        /// <summary>
        /// Wykonuje funkcję w nowym scope z dostępem do scoped services
        /// </summary>
        private async Task<T> ExecuteWithScopedServicesAsync<T>(Func<ICurrentUserService, INotificationService, Task<T>> func)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            
            return await func(currentUserService, notificationService);
        }

        /// <summary>
        /// Pobiera Current User UPN w nowym scope
        /// </summary>
        private string GetCurrentUserUpnScoped()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
            return currentUserService.GetCurrentUserUpn() ?? "system";
        }

        /// <summary>
        /// Pobiera Current User UPN bezpiecznie w nowym scope
        /// </summary>
        private async Task<string> GetCurrentUserUpnSafeAsync()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                return currentUserService.GetCurrentUserUpn() ?? "system";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania UPN bieżącego użytkownika");
                return "system";
            }
        }

        /// <summary>
        /// Tworzy metodę fabryczną dla nowych wyjątków z dodatkowym kontekstem
        /// </summary>
        private PowerShellConnectionException CreateConnectionException(
            string message, 
            Exception? innerException = null,
            string? connectionUri = null,
            int? attemptCount = null)
        {
            var contextData = new Dictionary<string, object?>
            {
                ["RunspaceState"] = _sharedRunspace?.RunspaceStateInfo.State.ToString(),
                ["IsConnected"] = _sharedIsConnected,
                ["CircuitBreakerState"] = _connectionCircuitBreaker.State.ToString(),
                ["LastSuccessfulConnection"] = _lastSuccessfulConnection
            };

            if (attemptCount.HasValue)
            {
                contextData["AttemptCount"] = attemptCount.Value;
            }

            return new PowerShellConnectionException(
                message, 
                new List<ErrorRecord>(),
                contextData,
                connectionUri ?? "https://graph.microsoft.com",
                "AccessToken",
                innerException);
        }

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
                    await ExecuteWithScopedServicesAsync(async (currentUserService, notificationService) =>
                    {
                        await notificationService.SendNotificationToUserAsync(
                            currentUserService.GetCurrentUserUpn() ?? "system",
                            $"🔄 Automatyczna próba odnowienia połączenia z Microsoft Graph (Circuit Breaker: {_connectionCircuitBreaker.State})",
                            "info"
                        );
                    });
                    
                    // Pobierz świeży token
                    var token = await _tokenManager.GetValidAccessTokenAsync(_lastConnectedUserUpn, _lastApiAccessToken);
                    
                    if (token == null)
                    {
                        throw new InvalidOperationException("Unable to obtain valid access token for reconnection");
                    }
                    
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
                
                                    _logger.LogWarning(ex, "Circuit breaker jest otwarty. Próby połączenia są tymczasowo wstrzymane.");
                await ExecuteWithScopedServicesAsync(async (currentUserService, notificationService) =>
                {
                    await notificationService.SendNotificationToUserAsync(
                        currentUserService.GetCurrentUserUpn() ?? "system",
                        "⚠️ Połączenie z Microsoft Graph jest tymczasowo niedostępne. Spróbuj ponownie za chwilę.",
                        "warning");
                });
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
                
                _logger.LogError(ex, "Nie udało się połączyć z Microsoft Graph");
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
            // Pobierz UPN używając scope
            string currentUserUpn;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
            }
            
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
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    "PowerShell environment not initialized"
                );
                
                // Użyj nowego wyjątku z Etapu 1
                throw PowerShellConnectionException.ForConnectionFailed(
                    "Środowisko PowerShell nie jest gotowe do nawiązania połączenia",
                    connectionUri: "https://graph.microsoft.com"
                );
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Nie można połączyć z Microsoft Graph: token dostępu nie może być pusty.");
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operationHistory.Id,
                    OperationStatus.Failed,
                    "Access token is null or empty"
                );
                
                // Użyj nowego wyjątku z Etapu 1
                throw PowerShellConnectionException.ForTokenError(
                    "Brak tokenu dostępu do Microsoft Graph"
                );
            }

            // Helper method dla powiadomień
            async Task SendProgressAsync(int progress, string message)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.SendOperationProgressToUserAsync(currentUserUpn, operationId, progress, message);
            }

            async Task SendNotificationAsync(string message, string type)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.SendNotificationToUserAsync(currentUserUpn, message, type);
            }

            // Powiadomienie o rozpoczęciu połączenia
            await SendProgressAsync(5, "Rozpoczynanie połączenia z Microsoft Graph API...");

            return await Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Próba połączenia z Microsoft Graph API. Scopes: [{Scopes}]",
                        scopes != null ? string.Join(", ", scopes) : "Brak");

                    await SendProgressAsync(20, "Importowanie modułów PowerShell...");

                    using (var ps = System.Management.Automation.PowerShell.Create())
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

                        await SendProgressAsync(50, "Uwierzytelnianie w Microsoft Graph...");

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
                            var errorRecords = ps.Streams.Error.ToList();
                            
                            foreach (var error in errorMessages)
                            {
                                _logger.LogError("Błąd PowerShell podczas łączenia: {Error}", error);
                            }

                            await _operationHistoryService.UpdateOperationStatusAsync(
                                operationHistory.Id,
                                OperationStatus.Failed,
                                $"PowerShell errors: {string.Join("; ", errorMessages)}"
                            );

                            await SendNotificationAsync(
                                $"Błąd połączenia z Graph API: {string.Join("; ", errorMessages)}", 
                                "error"
                            );
                            await SendProgressAsync(100, "Połączenie zakończone niepowodzeniem");
                            
                            // Użyj nowego wyjątku z Etapu 1
                            throw PowerShellCommandExecutionException.ForCmdlet(
                                "Connect-MgGraph",
                                errorRecords,
                                scopes?.Length > 0 ? new Dictionary<string, object?> { ["Scopes"] = scopes } : null
                            );
                        }

                        await SendProgressAsync(80, "Weryfikacja połączenia...");

                        // Weryfikacja połączenia i cache kontekstu
                        ps.Commands.Clear();
                        var contextCheckResult = ps.AddCommand("Get-MgContext").Invoke();

                        if (!contextCheckResult.Any())
                        {
                            _logger.LogError("Połączenie z Microsoft Graph nie zostało ustanowione.");
                            
                            await _operationHistoryService.UpdateOperationStatusAsync(
                                operationHistory.Id,
                                OperationStatus.Failed,
                                "Graph connection verification failed - no context returned"
                            );
                            
                            await SendNotificationAsync(
                                "Nie udało się ustanowić połączenia z Microsoft Graph", 
                                "error"
                            );
                            await SendProgressAsync(100, "Weryfikacja połączenia zakończona niepowodzeniem");
                            
                            throw PowerShellConnectionException.ForConnectionFailed(
                                "Weryfikacja połączenia z Microsoft Graph nie powiodła się",
                                connectionUri: "https://graph.microsoft.com"
                            );
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

                        await SendProgressAsync(100, "Połączenie z Microsoft Graph ustanowione pomyślnie");
                        await SendNotificationAsync("✅ Pomyślnie połączono z Microsoft Graph API", "success");

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
                catch (PowerShellException)
                {
                    // Przekaż dalej nasze własne wyjątki
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wyjątek podczas łączenia z Microsoft Graph");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Failed,
                        $"Connection failed: {ex.Message}",
                        ex.ToString()
                    );
                    
                    await SendNotificationAsync($"Błąd krytyczny podczas łączenia: {ex.Message}", "error");
                    await SendProgressAsync(100, "Połączenie zakończone błędem krytycznym");
                    
                    // Opakuj w nasz wyjątek
                    throw PowerShellConnectionException.ForConnectionFailed(
                        $"Nieoczekiwany błąd podczas łączenia z Microsoft Graph: {ex.Message}",
                        ex,
                        connectionUri: "https://graph.microsoft.com",
                        authenticationMethod: "AccessToken"
                    );
                }
            });
        }

        public void DisconnectFromGraph()
        {
            if (!_sharedIsConnected || _sharedRunspace == null) return;

            try
            {
                using var ps = System.Management.Automation.PowerShell.Create();
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
                throw PowerShellConnectionException.ForConnectionFailed(
                    "Środowisko PowerShell nie jest gotowe do wykonania skryptu"
                );
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogError("Skrypt nie może być pusty.");
                throw new ArgumentException("Skrypt PowerShell nie może być pusty", nameof(script));
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
                        using (var ps = System.Management.Automation.PowerShell.Create())
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
                                var errorRecords = ps.Streams.Error.ToList();
                                _logger.LogError("Skrypt zakończył się błędami.");
                                
                                throw PowerShellCommandExecutionException.ForScript(
                                    script,
                                    errorRecords,
                                    parameters as IDictionary<string, object?>
                                );
                            }

                            _logger.LogDebug("Skrypt wykonany. Wyniki: {Count}", results.Count);
                            return results;
                        }
                    }
                    catch (PowerShellException)
                    {
                        throw; // Przekaż nasze wyjątki bez zmian
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd wykonywania skryptu PowerShell");
                        throw PowerShellCommandExecutionException.ForScript(
                            script,
                            new List<ErrorRecord>(),
                            parameters as IDictionary<string, object?>
                        );
                    }
                }
            });
        }

        public async Task<Collection<PSObject>?> ExecuteCommandWithRetryAsync(
            string commandName,
            Dictionary<string, object>? parameters = null,
            int? maxRetries = null)
        {
            // Walidacja nazwy komendy
            if (string.IsNullOrWhiteSpace(commandName))
            {
                _logger.LogWarning("Próba wykonania komendy z pustą lub null nazwą");
                return null;
            }
            
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
                if (!await ConnectIfNotConnectedAsync())
                {
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operationHistory.Id,
                        OperationStatus.Failed,
                        "PowerShell is not connected and reconnection failed"
                    );
                    
                    _logger.LogWarning("Nie można wykonać komendy: PowerShell nie jest połączony i ponowne łączenie nie powiodło się.");
                    return null;
                }
            }

            int attempt = 0;
            Exception? lastException = null;
            var errorRecordsList = new List<ErrorRecord>();

            while (attempt <= maxRetries)
            {
                try
                {
                    attempt++;
                    _logger.LogDebug("Executing PowerShell command: {CommandName}, attempt {Attempt}/{MaxRetries}",
                        commandName, attempt, maxRetries);

                    using (var ps = System.Management.Automation.PowerShell.Create())
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
                            errorRecordsList.AddRange(ps.Streams.Error);
                            var errors = string.Join("; ", ps.Streams.Error.Select(e => e.ToString()));
                            throw PowerShellCommandExecutionException.ForCmdlet(
                                commandName,
                                ps.Streams.Error,
                                parameters as IDictionary<string, object?>
                            );
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
                catch (PowerShellCommandExecutionException ex) when (IsTransientError(ex) && attempt <= maxRetries)
                {
                    lastException = ex;
                    var delay = CalculateRetryDelay(attempt);
                    
                    _logger.LogWarning(ex,
                        "Attempt {Attempt}/{MaxRetries} failed for command '{CommandName}'. Retrying in {Delay}s",
                        attempt, maxRetries, commandName, delay.TotalSeconds);

                    await Task.Delay(delay);
                }
                catch (Exception ex) when (IsConnectionError(ex))
                {
                    lastException = ex;
                    _logger.LogWarning("Wykryto błąd połączenia. Próba ponownego połączenia...");
                    _sharedIsConnected = false;
                    
                    if (!await ConnectIfNotConnectedAsync())
                    {
                        _logger.LogError("Nie udało się ponownie połączyć po błędzie połączenia.");
                        
                        throw PowerShellConnectionException.ForConnectionFailed(
                            $"Utracono połączenie podczas wykonywania komendy '{commandName}'",
                            ex,
                            connectionUri: "https://graph.microsoft.com"
                        );
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            // Ostateczne niepowodzenie
            await _operationHistoryService.UpdateOperationStatusAsync(
                operationHistory.Id,
                OperationStatus.Failed,
                $"Command failed after {maxRetries} attempts: {lastException?.Message}",
                lastException?.ToString()
            );
            
            _logger.LogError(lastException, "Nie udało się wykonać komendy '{CommandName}' po {MaxRetries} próbach.",
                commandName, maxRetries);
            
            // Rzuć odpowiedni wyjątek w zależności od typu błędu
            if (lastException is PowerShellException)
            {
                throw lastException;
            }
            
            throw PowerShellCommandExecutionException.ForCmdlet(
                commandName,
                errorRecordsList,
                parameters as IDictionary<string, object?>
            );
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

        /// <summary>
        /// Rozszerzona metoda IsTransientError obsługująca hierarchię wyjątków
        /// </summary>
        private bool IsTransientError(Exception ex)
        {
            // Sprawdź czy to nasz wyjątek z informacją o retry
            if (ex is PowerShellCommandExecutionException cmdEx)
            {
                // Sprawdź ErrorRecords
                var hasTransientError = cmdEx.ErrorRecords.Any(er =>
                    er.Exception?.Message?.Contains("throttl", StringComparison.OrdinalIgnoreCase) == true ||
                    er.Exception?.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
                    er.CategoryInfo?.Reason?.Contains("Timeout") == true
                );
                
                if (hasTransientError) return true;
            }
            
            return ex.Message.Contains("throttl", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("retry", StringComparison.OrdinalIgnoreCase);
        }

        private void LogPowerShellStreams(System.Management.Automation.PowerShell ps)
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
        /// <summary>
        /// Rozszerzona metoda IsConnectionError używająca hierarchii wyjątków
        /// </summary>
        private bool IsConnectionError(Exception ex)
        {
            // Najpierw sprawdź czy to nasz wyjątek
            if (ex is PowerShellConnectionException)
                return true;
                
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("unauthorized") ||
                   message.Contains("token") ||
                   message.Contains("expired") ||
                   message.Contains("invalid_grant") ||
                   message.Contains("not connected") ||
                   message.Contains("connect-mggraph") ||
                   message.Contains("runspace") ||
                   message.Contains("disconnected");
        }

        /// <summary>
        /// Pobiera informacje o stanie połączenia i odporności systemu
        /// </summary>
        /// <returns>Szczegółowe informacje o stanie połączenia</returns>
        public async Task<ConnectionHealthInfo> GetConnectionHealthAsync()
        {
            return new ConnectionHealthInfo
            {
                IsConnected = _sharedIsConnected,
                RunspaceState = _sharedRunspace?.RunspaceStateInfo.State.ToString() ?? "Unknown",
                CircuitBreakerState = _connectionCircuitBreaker.State.ToString(),
                LastConnectionAttempt = _lastConnectionAttempt,
                LastSuccessfulConnection = _lastSuccessfulConnection,
                TokenValid = !string.IsNullOrEmpty(_lastApiAccessToken)
            };
        }

        private async void OnCircuitBreakerStateChanged(object? sender, CircuitBreakerStateChangedEventArgs e)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                
                var currentUserUpn = currentUserService.GetCurrentUserUpn() ?? "system";
                
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
                
                // Powiadomienia o znaczących zmianach stanu
                if (e.NewState == CircuitState.Open)
                {
                    await notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "🚫 Circuit Breaker otwarty - zbyt wiele nieudanych prób połączenia. Połączenia zostały tymczasowo wstrzymane.",
                        "error"
                    );
                }
                else if (e.OldState == CircuitState.Open && e.NewState == CircuitState.HalfOpen)
                {
                    await notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "🔄 Circuit Breaker w trybie testowym - próba przywrócenia połączenia.",
                        "info"
                    );
                }
                else if (e.OldState != CircuitState.Closed && e.NewState == CircuitState.Closed)
                {
                    await notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "✅ Circuit Breaker zamknięty - połączenie przywrócone.",
                        "success"
                    );
                }
            }
            catch (Exception ex)
            {
                // W event handlerze nie możemy rzucić wyjątku, tylko zalogować
                _logger.LogError(ex, "Błąd w obsłudze zmiany stanu Circuit Breaker");
            }
        }

        private async void OnCircuitBreakerFailureRecorded(object? sender, CircuitBreakerFailureEventArgs e)
        {
            try
            {
                // Powiadomienie tylko gdy zbliżamy się do limitu
                if (e.CurrentFailureCount >= e.Threshold - 1)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var currentUserService = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    
                    await notificationService.SendNotificationToUserAsync(
                        currentUserService.GetCurrentUserUpn() ?? "system",
                        $"⚠️ Uwaga: {e.CurrentFailureCount}/{e.Threshold} prób połączenia zakończonych niepowodzeniem. Jeszcze jedna nieudana próba spowoduje tymczasowe wstrzymanie połączeń.",
                        "warning"
                    );
                }
            }
            catch (Exception ex)
            {
                // W event handlerze nie możemy rzucić wyjątku, tylko zalogować
                _logger.LogError(ex, "Błąd w obsłudze zapisu błędu Circuit Breaker");
            }
        }

        /// <summary>
        /// Sprawdza czy system ma odpowiednie uprawnienia do wykonania operacji
        /// </summary>
        /// <param name="requiredPermissions">Lista wymaganych uprawnień</param>
        /// <returns>Informacje o dostępnych uprawnieniach</returns>
        public async Task<PowerShellPermissionInfo> ValidatePermissionsAsync(params string[] requiredPermissions)
        {
            var permissionInfo = new PowerShellPermissionInfo();
            
            if (!ValidateRunspaceState())
            {
                _logger.LogWarning("Nie można sprawdzić uprawnień - PowerShell runspace nie jest gotowy");
                permissionInfo.IsValid = false;
                permissionInfo.ErrorMessage = "PowerShell runspace not ready";
                return permissionInfo;
            }

            try
            {
                _logger.LogDebug("Sprawdzanie uprawnień PowerShell/Graph: {Permissions}", string.Join(", ", requiredPermissions));
                
                // Sprawdź kontekst Graph
                var contextScript = @"
                    try {
                        $context = Get-MgContext -ErrorAction Stop
                        if ($context) {
                            @{
                                IsConnected = $true
                                Account = $context.Account
                                Scopes = $context.Scopes -join ','
                                TenantId = $context.TenantId
                                AppName = $context.AppName
                            }
                        } else {
                            @{ IsConnected = $false }
                        }
                    } catch {
                        @{ 
                            IsConnected = $false
                            Error = $_.Exception.Message 
                        }
                    }
                ";

                var contextResults = await ExecuteScriptAsync(contextScript);
                var contextData = contextResults?.FirstOrDefault()?.BaseObject as Hashtable;

                if (contextData != null)
                {
                    permissionInfo.IsConnected = (bool)(contextData["IsConnected"] ?? false);
                    permissionInfo.Account = contextData["Account"]?.ToString();
                    permissionInfo.TenantId = contextData["TenantId"]?.ToString();
                    permissionInfo.AppName = contextData["AppName"]?.ToString();
                    
                    var scopesString = contextData["Scopes"]?.ToString();
                    if (!string.IsNullOrEmpty(scopesString))
                    {
                        permissionInfo.AvailableScopes = scopesString.Split(',').ToList();
                    }

                    if (contextData.ContainsKey("Error"))
                    {
                        permissionInfo.ErrorMessage = contextData["Error"]?.ToString();
                    }
                }

                // Sprawdź konkretne uprawnienia
                if (permissionInfo.IsConnected && requiredPermissions.Any())
                {
                    foreach (var permission in requiredPermissions)
                    {
                        var hasPermission = permissionInfo.AvailableScopes?.Any(s => 
                            s.Contains(permission, StringComparison.OrdinalIgnoreCase)) ?? false;
                        
                        permissionInfo.PermissionResults[permission] = hasPermission;
                        
                        if (!hasPermission)
                        {
                            _logger.LogWarning("Brak wymaganego uprawnienia: {Permission}", permission);
                        }
                    }
                    
                    permissionInfo.IsValid = permissionInfo.PermissionResults.Values.All(v => v);
                }
                else
                {
                    permissionInfo.IsValid = permissionInfo.IsConnected;
                }

                _logger.LogDebug("Sprawdzenie uprawnień zakończone. IsValid: {IsValid}, IsConnected: {IsConnected}", 
                    permissionInfo.IsValid, permissionInfo.IsConnected);

                return permissionInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień PowerShell");
                permissionInfo.IsValid = false;
                permissionInfo.ErrorMessage = ex.Message;
                return permissionInfo;
            }
        }

        /// <summary>
        /// Wykonuje kompleksową diagnostykę połączenia PowerShell/Graph
        /// </summary>
        /// <param name="includePermissionCheck">Czy sprawdzić uprawnienia</param>
        /// <param name="testCommands">Lista komend do przetestowania</param>
        /// <returns>Szczegółowe informacje diagnostyczne</returns>
        public async Task<PowerShellDiagnosticInfo> DiagnoseConnectionAsync(
            bool includePermissionCheck = true, 
            params string[] testCommands)
        {
            var diagnostic = new PowerShellDiagnosticInfo();
            var userUpn = await GetCurrentUserUpnSafeAsync();

            try
            {
                diagnostic.UserUpn = userUpn;
                diagnostic.HasApiToken = !string.IsNullOrEmpty(_lastApiAccessToken);
                diagnostic.ApiTokenLength = _lastApiAccessToken?.Length ?? 0;
                diagnostic.RunspaceState = _sharedRunspace?.RunspaceStateInfo.State.ToString() ?? "Unknown";
                diagnostic.IsConnected = _sharedIsConnected;
                diagnostic.CircuitBreakerState = _connectionCircuitBreaker.State.ToString();
                diagnostic.LastConnectionAttempt = _lastConnectionAttempt;
                diagnostic.LastSuccessfulConnection = _lastSuccessfulConnection;

                // Test podstawowego połączenia
                if (ValidateRunspaceState())
                {
                    diagnostic.RunspaceReady = true;
                    
                    // Test prostej komendy
                    try
                    {
                        var testResult = await ExecuteScriptAsync("Get-Date");
                        diagnostic.BasicCommandTest = testResult != null;
                    }
                    catch (Exception ex)
                    {
                        diagnostic.BasicCommandTest = false;
                        diagnostic.Errors.Add($"Basic command test failed: {ex.Message}");
                    }

                    // Test połączenia Graph
                    try
                    {
                        var graphTest = await ExecuteScriptAsync("Get-MgContext");
                        diagnostic.GraphConnectionTest = graphTest != null;
                    }
                    catch (Exception ex)
                    {
                        diagnostic.GraphConnectionTest = false;
                        diagnostic.Errors.Add($"Graph connection test failed: {ex.Message}");
                    }

                    // Test uprawnień jeśli wymagany
                    if (includePermissionCheck)
                    {
                        var permissionInfo = await ValidatePermissionsAsync("User.Read", "Group.Read.All");
                        diagnostic.HasRequiredPermissions = permissionInfo.IsValid;
                        diagnostic.AvailableScopes = permissionInfo.AvailableScopes ?? new List<string>();
                        
                        if (!string.IsNullOrEmpty(permissionInfo.ErrorMessage))
                        {
                            diagnostic.Errors.Add($"Permission check: {permissionInfo.ErrorMessage}");
                        }
                    }

                    // Test dodatkowych komend
                    if (testCommands.Any())
                    {
                        foreach (var command in testCommands)
                        {
                            try
                            {
                                var result = await ExecuteCommandWithRetryAsync(command);
                                diagnostic.TestResults[command] = result != null;
                            }
                            catch (Exception ex)
                            {
                                diagnostic.TestResults[command] = false;
                                diagnostic.Errors.Add($"Command '{command}' failed: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    diagnostic.RunspaceReady = false;
                    diagnostic.Errors.Add("PowerShell runspace is not ready");
                }

                // Ogólna ocena stanu
                diagnostic.OverallHealth = DetermineOverallHealth(diagnostic);

                _logger.LogDebug("Diagnostyka PowerShell zakończona. Overall health: {Health}, Errors: {ErrorCount}", 
                    diagnostic.OverallHealth, diagnostic.Errors.Count);

                return diagnostic;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas diagnostyki PowerShell");
                diagnostic.Errors.Add($"Critical diagnostic error: {ex.Message}");
                diagnostic.OverallHealth = PowerShellHealthStatus.Critical;
                return diagnostic;
            }
        }

        /// <summary>
        /// Wykonuje operację z pełną diagnostyką i logowaniem
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationName">Nazwa operacji do logowania</param>
        /// <param name="requiredPermissions">Wymagane uprawnienia</param>
        /// <param name="validateBefore">Czy wykonać walidację przed operacją</param>
        /// <returns>Wynik operacji</returns>
        public async Task<T?> ExecuteWithDiagnosticsAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            string[]? requiredPermissions = null,
            bool validateBefore = true) where T : class
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Rozpoczynanie operacji PowerShell: {OperationName}", operationName);

            try
            {
                // Pre-validation jeśli wymagana
                if (validateBefore)
                {
                    if (!ValidateRunspaceState())
                    {
                        _logger.LogError("Operacja {OperationName} przerwana - PowerShell runspace nie jest gotowy", operationName);
                        return null;
                    }

                    if (requiredPermissions?.Any() == true)
                    {
                        var permissionCheck = await ValidatePermissionsAsync(requiredPermissions);
                        if (!permissionCheck.IsValid)
                        {
                            _logger.LogError("Operacja {OperationName} przerwana - brak wymaganych uprawnień: {Permissions}", 
                                operationName, string.Join(", ", requiredPermissions));
                            return null;
                        }
                    }
                }

                // Wykonaj operację
                var result = await operation();
                var duration = DateTime.UtcNow - startTime;

                _logger.LogInformation("Operacja PowerShell {OperationName} zakończona pomyślnie w {Duration}ms", 
                    operationName, duration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Operacja PowerShell {OperationName} nie powiodła się po {Duration}ms: {Error}", 
                    operationName, duration.TotalMilliseconds, ex.Message);

                // Record failure for circuit breaker
                _connectionCircuitBreaker.RecordFailure();
                
                throw;
            }
        }

        /// <summary>
        /// Określa ogólny stan zdrowia systemu na podstawie diagnostyki
        /// </summary>
        private PowerShellHealthStatus DetermineOverallHealth(PowerShellDiagnosticInfo diagnostic)
        {
            if (!diagnostic.RunspaceReady)
                return PowerShellHealthStatus.Critical;

            if (!diagnostic.IsConnected)
                return PowerShellHealthStatus.Critical;

            if (!diagnostic.BasicCommandTest)
                return PowerShellHealthStatus.Critical;

            if (!diagnostic.GraphConnectionTest)
                return PowerShellHealthStatus.Degraded;

            if (diagnostic.HasRequiredPermissions == false)
                return PowerShellHealthStatus.Degraded;

            if (diagnostic.Errors.Any())
                return PowerShellHealthStatus.Warning;

            return PowerShellHealthStatus.Healthy;
        }
    }
}
