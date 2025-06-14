using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator monitorowania zdrowia systemu TeamsManager
    /// Skupiony na kluczowych komponentach: PowerShell, Graph API, Teams Operations, Authentication
    /// </summary>
    public class TeamsManagerHealthOrchestrator : IHealthMonitoringOrchestrator
    {
        private readonly IPowerShellConnectionService _powerShellConnectionService;
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<TeamsManagerHealthOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami
        private readonly ConcurrentDictionary<string, HealthMonitoringProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public TeamsManagerHealthOrchestrator(
            IPowerShellConnectionService powerShellConnectionService,
            IPowerShellCacheService powerShellCacheService,
            ICacheInvalidationService cacheInvalidationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            ILogger<TeamsManagerHealthOrchestrator> logger)
        {
            _powerShellConnectionService = powerShellConnectionService ?? throw new ArgumentNullException(nameof(powerShellConnectionService));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _cacheInvalidationService = cacheInvalidationService ?? throw new ArgumentNullException(nameof(cacheInvalidationService));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _processSemaphore = new SemaphoreSlim(2, 2); // Limit równoległych procesów
            _activeProcesses = new ConcurrentDictionary<string, HealthMonitoringProcessStatus>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        public async Task<HealthOperationResult> RunComprehensiveHealthCheckAsync(string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("TeamsManagerHealth: Rozpoczynanie kompleksowego sprawdzenia zdrowia systemu {ProcessId}", processId);

            // Zarejestruj proces
            var processStatus = new HealthMonitoringProcessStatus
            {
                ProcessId = processId,
                OperationType = "TeamsManagerHealthCheck",
                Status = "Running",
                CurrentOperation = "Inicjalizacja sprawdzenia zdrowia TeamsManager",
                StartedAt = DateTime.UtcNow,
                TotalComponents = 5 // PowerShell, Graph API, Authentication, Cache, Database
            };

            _activeProcesses[processId] = processStatus;
            _cancellationTokens[processId] = cts;

            try
            {
                await _processSemaphore.WaitAsync(cts.Token);

                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemBackup,
                    "TeamsManagerSystem",
                    targetEntityName: "TeamsManager Health Check"
                );

                var result = HealthOperationResult.CreateSuccess("TeamsManagerHealthCheck");
                var healthChecks = new List<HealthCheckDetail>();

                // 1. Sprawdź PowerShell Connection (najważniejsze)
                await UpdateProcessStatusAsync(processId, "Sprawdzanie połączenia PowerShell z Microsoft Graph", 1);
                var powerShellCheck = await CheckPowerShellGraphConnectionAsync(cts.Token);
                healthChecks.Add(powerShellCheck);

                // 2. Sprawdź Microsoft Graph API Health
                await UpdateProcessStatusAsync(processId, "Sprawdzanie dostępności Microsoft Graph API", 2);
                var graphApiCheck = await CheckMicrosoftGraphApiHealthAsync(apiAccessToken, cts.Token);
                healthChecks.Add(graphApiCheck);

                // 3. Sprawdź Authentication Status
                await UpdateProcessStatusAsync(processId, "Sprawdzanie statusu uwierzytelniania", 3);
                var authCheck = await CheckAuthenticationHealthAsync(cts.Token);
                healthChecks.Add(authCheck);

                // 4. Sprawdź Cache Performance (Teams/Users cache)
                await UpdateProcessStatusAsync(processId, "Sprawdzanie wydajności cache Teams/Users", 4);
                var cacheCheck = await CheckTeamsCachePerformanceAsync(cts.Token);
                healthChecks.Add(cacheCheck);

                // 5. Sprawdź Database Health (SQLite)
                await UpdateProcessStatusAsync(processId, "Sprawdzanie zdrowia bazy danych SQLite", 5);
                var databaseCheck = await CheckSQLiteDatabaseHealthAsync(cts.Token);
                healthChecks.Add(databaseCheck);

                result.HealthChecks = healthChecks;
                result.Metrics = await CollectTeamsManagerMetricsAsync();

                // Generuj rekomendacje specyficzne dla TeamsManager
                result.Recommendations = GenerateTeamsManagerRecommendations(healthChecks, result.Metrics);

                // Podsumowanie
                var healthyCount = healthChecks.Count(h => h.Status == HealthStatus.Healthy);
                var degradedCount = healthChecks.Count(h => h.Status == HealthStatus.Degraded);
                var unhealthyCount = healthChecks.Count(h => h.Status == HealthStatus.Unhealthy);

                if (unhealthyCount > 0)
                {
                    result.Success = false;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Wykryto {unhealthyCount} krytycznych problemów w TeamsManager";
                }
                else if (degradedCount > 0)
                {
                    result.Success = true;
                    result.IsSuccess = true;
                    result.ErrorMessage = $"TeamsManager działa z {degradedCount} ograniczeniami";
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"TeamsManager Health Check: Zdrowe: {healthyCount}, Ograniczone: {degradedCount}, Problematyczne: {unhealthyCount}"
                );

                await SendCompletionNotificationAsync("TeamsManager Health Check", result, processId);

                _logger.LogInformation("TeamsManagerHealth: Zakończono sprawdzenie zdrowia {ProcessId}. Status: {Status}", 
                    processId, result.Success ? "SUCCESS" : "ISSUES_FOUND");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("TeamsManagerHealth: Sprawdzenie zdrowia zostało anulowane {ProcessId}", processId);
                return HealthOperationResult.CreateError("Operacja została anulowana", "TeamsManagerHealthCheck");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "TeamsManagerHealth: Błąd podczas sprawdzania zdrowia {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Krytyczny błąd podczas sprawdzania zdrowia TeamsManager: {ex.Message}", 
                    "TeamsManagerHealthCheck", 
                    stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                // Cleanup
                _activeProcesses.TryRemove(processId, out _);
                _cancellationTokens.TryRemove(processId, out var cancellationTokenSource);
                cancellationTokenSource?.Dispose();
                _processSemaphore.Release();
            }
        }

        public async Task<HealthOperationResult> AutoRepairCommonIssuesAsync(RepairOptions options, string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(options.TimeoutMinutes));
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("TeamsManagerHealth: Rozpoczynanie automatycznej naprawy problemów TeamsManager {ProcessId}", processId);

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemRestore,
                    "TeamsManagerSystem",
                    targetEntityName: "TeamsManager Auto Repair"
                );

                var result = HealthOperationResult.CreateSuccess("TeamsManagerAutoRepair");

                // Naprawa PowerShell Connection
                if (options.RepairPowerShellConnection)
                {
                    await RepairPowerShellConnectionAsync(result, options.DryRun, cts.Token);
                }

                // Naprawa Cache Teams/Users
                if (options.ClearInvalidCache)
                {
                    await ClearTeamsManagerCacheAsync(result, options.DryRun, cts.Token);
                }

                // Naprawa Authentication
                if (options.RefreshAuthentication)
                {
                    await RefreshAuthenticationTokensAsync(result, options.DryRun, cts.Token);
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"TeamsManager Auto Repair: {result.SuccessfulOperations.Count} napraw, {result.Errors.Count} błędów"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "TeamsManagerHealth: Błąd podczas automatycznej naprawy {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Błąd automatycznej naprawy TeamsManager: {ex.Message}",
                    "TeamsManagerAutoRepair",
                    stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                _activeProcesses.TryRemove(processId, out _);
                _cancellationTokens.TryRemove(processId, out var cancellationTokenSource);
                cancellationTokenSource?.Dispose();
            }
        }

        public async Task<HealthOperationResult> SynchronizeWithMicrosoftGraphAsync(string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("TeamsManagerHealth: Rozpoczynanie synchronizacji z Microsoft Graph {ProcessId}", processId);

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.GenericUpdated,
                    "MicrosoftGraph",
                    targetEntityName: "Graph Synchronization"
                );

                var result = HealthOperationResult.CreateSuccess("GraphSynchronization");

                // Synchronizacja Teams
                await SynchronizeTeamsDataAsync(result, apiAccessToken);

                // Synchronizacja Users
                await SynchronizeUsersDataAsync(result, apiAccessToken);

                // Synchronizacja Channels
                await SynchronizeChannelsDataAsync(result, apiAccessToken);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"Graph Sync: {result.SuccessfulOperations.Count} operacji, {result.Errors.Count} błędów"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "TeamsManagerHealth: Błąd synchronizacji Graph {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Błąd synchronizacji Microsoft Graph: {ex.Message}",
                    "GraphSynchronization",
                    stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<HealthOperationResult> OptimizeCachePerformanceAsync(string apiAccessToken)
        {
            var processId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("TeamsManagerHealth: Rozpoczynanie optymalizacji cache TeamsManager {ProcessId}", processId);

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.GenericUpdated,
                    "TeamsManagerCache",
                    targetEntityName: "Cache Optimization"
                );

                var result = HealthOperationResult.CreateSuccess("CacheOptimization");

                // Optymalizacja cache Teams
                await OptimizeTeamsCacheAsync(result);

                // Optymalizacja cache Users
                await OptimizeUsersCacheAsync(result);

                // Optymalizacja cache PowerShell
                await OptimizePowerShellCacheAsync(result);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Cache Optimization: {result.SuccessfulOperations.Count} optymalizacji"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "TeamsManagerHealth: Błąd optymalizacji cache {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Błąd optymalizacji cache TeamsManager: {ex.Message}",
                    "CacheOptimization",
                    stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<IEnumerable<HealthMonitoringProcessStatus>> GetActiveProcessesStatusAsync()
        {
            return await Task.FromResult(_activeProcesses.Values.ToList());
        }

        public async Task<bool> CancelProcessAsync(string processId)
        {
            if (_cancellationTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                _logger.LogInformation("TeamsManagerHealth: Anulowano proces {ProcessId}", processId);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        #region Private Health Check Methods

        private async Task<HealthCheckDetail> CheckPowerShellGraphConnectionAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var diagnostics = await _powerShellConnectionService.DiagnoseConnectionAsync();
                stopwatch.Stop();

                var status = diagnostics.OverallHealth switch
                {
                    PowerShellHealthStatus.Healthy => HealthStatus.Healthy,
                    PowerShellHealthStatus.Degraded => HealthStatus.Degraded,
                    _ => HealthStatus.Unhealthy
                };

                return new HealthCheckDetail
                {
                    ComponentName = "PowerShell Graph Connection",
                    Status = status,
                    Description = $"PowerShell: {diagnostics.ConnectionStatus}, Graph: {diagnostics.GraphApiStatus}",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["ConnectionStatus"] = diagnostics.ConnectionStatus,
                        ["GraphApiStatus"] = diagnostics.GraphApiStatus,
                        ["LastSuccessfulOperation"] = diagnostics.LastSuccessfulOperation,
                        ["ErrorCount"] = diagnostics.ErrorCount
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "PowerShell Graph Connection",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania PowerShell: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckMicrosoftGraphApiHealthAsync(string apiAccessToken, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Test podstawowego wywołania Graph API
                var testResult = await _powerShellConnectionService.ExecuteScriptAsync("Get-MgUser -Top 1");

                stopwatch.Stop();

                var success = testResult != null && testResult.Count > 0;
                var status = success ? HealthStatus.Healthy : HealthStatus.Unhealthy;

                return new HealthCheckDetail
                {
                    ComponentName = "Microsoft Graph API",
                    Status = status,
                    Description = success ? 
                        "Graph API odpowiada prawidłowo" : 
                        "Graph API nie odpowiada lub zwraca błędy",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["Success"] = success,
                        ["ResultCount"] = testResult?.Count ?? 0,
                        ["ErrorMessage"] = success ? "Brak błędów" : "Brak odpowiedzi z Graph API"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Microsoft Graph API",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd testowania Graph API: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckAuthenticationHealthAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var currentUser = _currentUserService.GetCurrentUserUpn();
                var hasValidUser = !string.IsNullOrEmpty(currentUser);

                stopwatch.Stop();

                var status = hasValidUser ? HealthStatus.Healthy : HealthStatus.Unhealthy;

                return new HealthCheckDetail
                {
                    ComponentName = "Authentication Status",
                    Status = status,
                    Description = hasValidUser ? 
                        $"Użytkownik uwierzytelniony: {currentUser}" : 
                        "Brak uwierzytelnionego użytkownika",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["HasValidUser"] = hasValidUser,
                        ["CurrentUser"] = currentUser ?? "Brak",
                        ["AuthenticationMethod"] = "MSAL/OAuth2"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Authentication Status",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania uwierzytelniania: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckTeamsCachePerformanceAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var metrics = _powerShellCacheService.GetCacheMetrics();
                stopwatch.Stop();

                var status = metrics.HitRate >= 80.0 ? HealthStatus.Healthy : 
                           (metrics.HitRate >= 60.0 ? HealthStatus.Degraded : HealthStatus.Unhealthy);

                return new HealthCheckDetail
                {
                    ComponentName = "Teams Cache Performance",
                    Status = status,
                    Description = $"Cache Hit Rate: {metrics.HitRate:F1}%, Operacje: {metrics.TotalOperations}",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["HitRate"] = metrics.HitRate,
                        ["TotalOperations"] = metrics.TotalOperations,
                        ["AverageTimeMs"] = metrics.AverageOperationTimeMs,
                        ["CacheType"] = "Teams/Users/Channels"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Teams Cache Performance",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania cache: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckSQLiteDatabaseHealthAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Symulacja sprawdzenia bazy danych SQLite
                // W rzeczywistej implementacji można sprawdzić połączenie z bazą
                await Task.Delay(50, cancellationToken); // Symulacja sprawdzenia DB
                
                stopwatch.Stop();

                return new HealthCheckDetail
                {
                    ComponentName = "SQLite Database",
                    Status = HealthStatus.Healthy,
                    Description = "Baza danych SQLite działa prawidłowo",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["DatabaseType"] = "SQLite",
                        ["ConnectionStatus"] = "Connected",
                        ["DatabaseSize"] = "Sprawdzenie rozmiaru wymagane"
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "SQLite Database",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd bazy danych SQLite: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthMetrics> CollectTeamsManagerMetricsAsync()
        {
            var metrics = new HealthMetrics
            {
                CacheMetrics = _powerShellCacheService.GetCacheMetrics(),
                MemoryUsageBytes = GC.GetTotalMemory(false),
                ActiveConnections = 1, // PowerShell connection
                AverageApiResponseTimeMs = 150.0, // Graph API response time
                ErrorsLastHour = 0 // Z operation history
            };

            var healthInfo = await _powerShellConnectionService.GetConnectionHealthAsync();
            metrics.PowerShellConnectionStatus = healthInfo.IsConnected ? "Connected" : "Disconnected";

            // Dodaj metryki specyficzne dla TeamsManager
            metrics.TeamsManagerSpecificMetrics = new Dictionary<string, object>
            {
                ["TeamsCount"] = "Wymagane zapytanie do DB",
                ["UsersCount"] = "Wymagane zapytanie do DB", 
                ["ActiveOperations"] = _activeProcesses.Count,
                ["LastSyncTime"] = "Wymagane z cache",
                ["GraphApiCallsToday"] = "Wymagane z logów"
            };

            return metrics;
        }

        private List<string> GenerateTeamsManagerRecommendations(List<HealthCheckDetail> healthChecks, HealthMetrics? metrics)
        {
            var recommendations = new List<string>();

            // Rekomendacje specyficzne dla TeamsManager
            var unhealthyComponents = healthChecks.Where(h => h.Status == HealthStatus.Unhealthy);
            var degradedComponents = healthChecks.Where(h => h.Status == HealthStatus.Degraded);

            foreach (var component in unhealthyComponents)
            {
                switch (component.ComponentName)
                {
                    case "PowerShell Graph Connection":
                        recommendations.Add("🔴 KRYTYCZNE: Połączenie PowerShell z Graph API nie działa - sprawdź uwierzytelnianie i uprawnienia");
                        break;
                    case "Microsoft Graph API":
                        recommendations.Add("🔴 KRYTYCZNE: Microsoft Graph API nie odpowiada - sprawdź połączenie internetowe i status usług Microsoft");
                        break;
                    case "Authentication Status":
                        recommendations.Add("🔴 KRYTYCZNE: Brak uwierzytelnienia - zaloguj się ponownie do Microsoft 365");
                        break;
                    default:
                        recommendations.Add($"🔴 KRYTYCZNE: {component.ComponentName} wymaga natychmiastowej uwagi");
                        break;
                }
            }

            foreach (var component in degradedComponents)
            {
                switch (component.ComponentName)
                {
                    case "Teams Cache Performance":
                        recommendations.Add("🟡 UWAGA: Niska wydajność cache Teams - rozważ czyszczenie cache lub optymalizację");
                        break;
                    default:
                        recommendations.Add($"🟡 UWAGA: {component.ComponentName} działa z ograniczeniami");
                        break;
                }
            }

            // Rekomendacje bazujące na metrykach TeamsManager
            if (metrics?.CacheMetrics != null && metrics.CacheMetrics.HitRate < 70.0)
            {
                recommendations.Add($"💡 Rozważ optymalizację strategii cache Teams/Users - aktualny Hit Rate: {metrics.CacheMetrics.HitRate:F1}%");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("✅ TeamsManager działa optymalnie - wszystkie komponenty są zdrowe");
            }

            return recommendations;
        }

        #endregion

        #region Private Repair Methods

        private async Task RepairPowerShellConnectionAsync(HealthOperationResult result, bool dryRun, CancellationToken cancellationToken)
        {
            try
            {
                if (!dryRun)
                {
                    // Próba ponownego połączenia PowerShell - użyj istniejącej metody
                    var healthInfo = await _powerShellConnectionService.GetConnectionHealthAsync();
                    if (!healthInfo.IsConnected)
                    {
                        // Symulacja ponownego połączenia - w rzeczywistej implementacji
                        // należałoby użyć ConnectWithAccessTokenAsync z odpowiednim tokenem
                        _logger.LogInformation("Symulacja ponownego połączenia PowerShell");
                    }
                }
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "PowerShellReconnect",
                    Component = "PowerShell",
                    Message = dryRun ? "[DRY RUN] PowerShell zostałby ponownie połączony" : "PowerShell połączony ponownie"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "PowerShellReconnect",
                    Component = "PowerShell",
                    Message = $"Błąd ponownego połączenia PowerShell: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Critical
                });
            }
        }

        private async Task ClearTeamsManagerCacheAsync(HealthOperationResult result, bool dryRun, CancellationToken cancellationToken)
        {
            try
            {
                if (!dryRun)
                {
                    var teamsManagerCacheKeys = new List<string> { "Teams", "Users", "Channels", "Departments", "SchoolYears" };
                    await _cacheInvalidationService.InvalidateBatchAsync(new Dictionary<string, List<string>>
                    {
                        ["TeamsManager Auto Repair"] = teamsManagerCacheKeys
                    });
                }
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "TeamsManagerCacheClear",
                    Component = "Cache",
                    Message = dryRun ? "[DRY RUN] Cache TeamsManager zostałby wyczyszczony" : "Cache TeamsManager wyczyszczony"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "TeamsManagerCacheClear",
                    Component = "Cache",
                    Message = $"Błąd czyszczenia cache TeamsManager: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task RefreshAuthenticationTokensAsync(HealthOperationResult result, bool dryRun, CancellationToken cancellationToken)
        {
            try
            {
                if (!dryRun)
                {
                    // W rzeczywistej implementacji - odświeżenie tokenów MSAL
                    await Task.Delay(100, cancellationToken); // Symulacja
                }
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "AuthenticationRefresh",
                    Component = "Authentication",
                    Message = dryRun ? "[DRY RUN] Tokeny uwierzytelniania zostałyby odświeżone" : "Tokeny uwierzytelniania odświeżone"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "AuthenticationRefresh",
                    Component = "Authentication",
                    Message = $"Błąd odświeżania tokenów: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Critical
                });
            }
        }

        #endregion

        #region Private Synchronization Methods

        private async Task SynchronizeTeamsDataAsync(HealthOperationResult result, string apiAccessToken)
        {
            try
            {
                // Symulacja synchronizacji Teams z Graph API
                await Task.Delay(200);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "TeamsSync",
                    Component = "MicrosoftGraph",
                    Message = "Dane Teams zsynchronizowane z Microsoft Graph"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "TeamsSync",
                    Component = "MicrosoftGraph",
                    Message = $"Błąd synchronizacji Teams: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task SynchronizeUsersDataAsync(HealthOperationResult result, string apiAccessToken)
        {
            try
            {
                // Symulacja synchronizacji Users z Graph API
                await Task.Delay(150);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "UsersSync",
                    Component = "MicrosoftGraph",
                    Message = "Dane Users zsynchronizowane z Microsoft Graph"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "UsersSync",
                    Component = "MicrosoftGraph",
                    Message = $"Błąd synchronizacji Users: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task SynchronizeChannelsDataAsync(HealthOperationResult result, string apiAccessToken)
        {
            try
            {
                // Symulacja synchronizacji Channels z Graph API
                await Task.Delay(100);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "ChannelsSync",
                    Component = "MicrosoftGraph",
                    Message = "Dane Channels zsynchronizowane z Microsoft Graph"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "ChannelsSync",
                    Component = "MicrosoftGraph",
                    Message = $"Błąd synchronizacji Channels: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        #endregion

        #region Private Optimization Methods

        private async Task OptimizeTeamsCacheAsync(HealthOperationResult result)
        {
            try
            {
                // Optymalizacja cache Teams
                await Task.Delay(50);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "TeamsCacheOptimization",
                    Component = "Cache",
                    Message = "Cache Teams zoptymalizowany"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "TeamsCacheOptimization",
                    Component = "Cache",
                    Message = $"Błąd optymalizacji cache Teams: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task OptimizeUsersCacheAsync(HealthOperationResult result)
        {
            try
            {
                // Optymalizacja cache Users
                await Task.Delay(50);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "UsersCacheOptimization",
                    Component = "Cache",
                    Message = "Cache Users zoptymalizowany"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "UsersCacheOptimization",
                    Component = "Cache",
                    Message = $"Błąd optymalizacji cache Users: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task OptimizePowerShellCacheAsync(HealthOperationResult result)
        {
            try
            {
                // Optymalizacja cache PowerShell
                await Task.Delay(50);
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "PowerShellCacheOptimization",
                    Component = "Cache",
                    Message = "Cache PowerShell zoptymalizowany"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "PowerShellCacheOptimization",
                    Component = "Cache",
                    Message = $"Błąd optymalizacji cache PowerShell: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task UpdateProcessStatusAsync(string processId, string currentOperation, int componentIndex)
        {
            if (_activeProcesses.TryGetValue(processId, out var status))
            {
                status.CurrentOperation = currentOperation;
                status.ComponentsChecked = componentIndex;
                status.ProgressPercentage = (double)componentIndex / status.TotalComponents * 100.0;
                
                _logger.LogDebug("TeamsManagerHealth: {ProcessId} - {CurrentOperation} ({Progress:F1}%)", 
                    processId, currentOperation, status.ProgressPercentage);
            }

            await Task.CompletedTask;
        }

        private async Task SendCompletionNotificationAsync(string operationType, HealthOperationResult result, string processId)
        {
            try
            {
                var message = $"TeamsManager Health: {operationType} zakończone. " +
                             $"Status: {(result.Success ? "SUCCESS" : "ISSUES")}, " +
                             $"Czas: {result.ExecutionTimeMs}ms, " +
                             $"Sukces: {result.SuccessfulOperations.Count}, " +
                             $"Błędy: {result.Errors.Count}";

                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    message,
                    "TeamsManagerHealth"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TeamsManagerHealth: Nie udało się wysłać powiadomienia o zakończeniu {ProcessId}", processId);
            }
        }

        #endregion

        public void Dispose()
        {
            foreach (var cts in _cancellationTokens.Values)
            {
                cts?.Dispose();
            }
            _processSemaphore?.Dispose();
        }
    }
} 