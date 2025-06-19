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
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Application.Services
{
    /// <summary>
    /// Orkiestrator monitorowania zdrowia systemu
    /// Odpowiedzialny za kompleksowe operacje diagnostyczne, naprawy automatyczne i optymalizację
    /// Następuje wzorce z SchoolYearProcessOrchestrator, TeamLifecycleOrchestrator i BulkUserManagementOrchestrator
    /// </summary>
    public class HealthMonitoringOrchestrator : IHealthMonitoringOrchestrator
    {
        private readonly IGraphConnectionService _graphConnectionService;
        private readonly IGraphCacheService _graphCacheService;
        private readonly ICacheInvalidationService _cacheInvalidationService;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<HealthMonitoringOrchestrator> _logger;
        private readonly SemaphoreSlim _processSemaphore;

        // Thread-safe słowniki dla zarządzania aktywnymi procesami (wzorzec z orkiestratorów)
        private readonly ConcurrentDictionary<string, HealthMonitoringProcessStatus> _activeProcesses;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

        public HealthMonitoringOrchestrator(
            IGraphConnectionService graphConnectionService,
            IGraphCacheService graphCacheService,
            ICacheInvalidationService cacheInvalidationService,
            IOperationHistoryService operationHistoryService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            ILogger<HealthMonitoringOrchestrator> logger)
        {
            _graphConnectionService = graphConnectionService ?? throw new ArgumentNullException(nameof(graphConnectionService));
            _graphCacheService = graphCacheService ?? throw new ArgumentNullException(nameof(graphCacheService));
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

            _logger.LogInformation("HealthOrchestrator: Rozpoczynanie kompleksowego sprawdzenia zdrowia systemu {ProcessId}", processId);

            // Zarejestruj proces
            var processStatus = new HealthMonitoringProcessStatus
            {
                ProcessId = processId,
                OperationType = "ComprehensiveHealthCheck",
                Status = "Running",
                CurrentOperation = "Inicjalizacja sprawdzenia zdrowia",
                StartedAt = DateTime.UtcNow,
                TotalComponents = 3 // Graph API, Cache, Performance
            };

            _activeProcesses[processId] = processStatus;
            _cancellationTokens[processId] = cts;

            try
            {
                await _processSemaphore.WaitAsync(cts.Token);

                // Utwórz główny wpis operacji
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemBackup, // Używamy istniejącego typu jako najbliższego
                    "System",
                    targetEntityName: "Comprehensive Health Check"
                );

                var result = HealthOperationResult.CreateSuccess("ComprehensiveHealthCheck");
                var healthChecks = new List<HealthCheckDetail>();

                            // 1. Sprawdź Graph API Connection
            await UpdateProcessStatusAsync(processId, "Sprawdzanie połączenia Graph API", 1);
            var graphCheck = await CheckGraphConnectionHealthAsync(cts.Token);
            healthChecks.Add(graphCheck);

                // 2. Sprawdź Cache Performance
                await UpdateProcessStatusAsync(processId, "Sprawdzanie wydajności cache", 2);
                var cacheCheck = await CheckCachePerformanceAsync(cts.Token);
                healthChecks.Add(cacheCheck);

                // 3. Sprawdź System Performance
                await UpdateProcessStatusAsync(processId, "Sprawdzanie wydajności systemu", 3);
                var performanceCheck = await CheckSystemPerformanceAsync(cts.Token);
                healthChecks.Add(performanceCheck);

                result.HealthChecks = healthChecks;
                result.Metrics = await CollectSystemMetricsAsync();

                // Generuj rekomendacje
                result.Recommendations = GenerateRecommendations(healthChecks, result.Metrics);

                // Podsumowanie
                var healthyCount = healthChecks.Count(h => h.Status == HealthStatus.Healthy);
                var degradedCount = healthChecks.Count(h => h.Status == HealthStatus.Degraded);
                var unhealthyCount = healthChecks.Count(h => h.Status == HealthStatus.Unhealthy);

                if (unhealthyCount > 0)
                {
                    result.Success = false;
                    result.IsSuccess = false;
                    result.ErrorMessage = $"Wykryto {unhealthyCount} krytycznych problemów w systemie";
                }
                else if (degradedCount > 0)
                {
                    result.Success = true;
                    result.IsSuccess = true;
                    result.ErrorMessage = $"System działa z {degradedCount} ograniczeniami";
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                // Zaktualizuj operację w historii
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id, 
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"Sprawdzono {healthChecks.Count} komponentów. Zdrowe: {healthyCount}, Ograniczone: {degradedCount}, Problematyczne: {unhealthyCount}"
                );

                // Wyślij powiadomienie
                await SendCompletionNotificationAsync("Comprehensive Health Check", result, processId);

                _logger.LogInformation("HealthOrchestrator: Zakończono kompleksowe sprawdzenie zdrowia {ProcessId}. Czas: {ElapsedMs}ms, Status: {Status}", 
                    processId, stopwatch.ElapsedMilliseconds, result.Success ? "SUCCESS" : "ISSUES_FOUND");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("HealthOrchestrator: Sprawdzenie zdrowia zostało anulowane {ProcessId}", processId);
                return HealthOperationResult.CreateError("Operacja została anulowana", "ComprehensiveHealthCheck");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HealthOrchestrator: Błąd podczas sprawdzania zdrowia systemu {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Krytyczny błąd podczas sprawdzania zdrowia: {ex.Message}", 
                    "ComprehensiveHealthCheck", 
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

            _logger.LogInformation("HealthOrchestrator: Rozpoczynanie automatycznej naprawy problemów {ProcessId}", processId);

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemRestore,
                    "System",
                    targetEntityName: "Auto Repair Common Issues"
                );

                var result = HealthOperationResult.CreateSuccess("AutoRepair");

                // Naprawa cache jeśli potrzeba
                if (options.ClearInvalidCache)
                {
                    await ClearInvalidCacheAsync(result, options.DryRun, cts.Token);
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"Wykonano {result.SuccessfulOperations.Count} napraw, {result.Errors.Count} błędów"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HealthOrchestrator: Błąd podczas automatycznej naprawy {ProcessId}", processId);
                
                return HealthOperationResult.CreateError(
                    $"Krytyczny błąd podczas naprawy: {ex.Message}", 
                    "AutoRepair", 
                    stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<HealthOperationResult> SynchronizeWithMicrosoftGraphAsync(string apiAccessToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemBackup,
                    "Graph",
                    targetEntityName: "Microsoft Graph Synchronization"
                );

                var result = HealthOperationResult.CreateSuccess("GraphSynchronization");

                // Sprawdź połączenie z Graph
                var connectionHealth = await _graphConnectionService.GetConnectionHealthAsync();
                if (!connectionHealth.IsConnected || !connectionHealth.IsTokenValid)
                {
                    result.Errors.Add(new HealthOperationError
                    {
                        Operation = "GraphConnection",
                        Component = "API Graph",
                        Message = "Brak prawidłowego połączenia z Microsoft Graph",
                        Severity = HealthErrorSeverity.Critical
                    });
                }
                else
                {
                    result.SuccessfulOperations.Add(new HealthOperationSuccess
                    {
                        Operation = "GraphConnection",
                        Component = "API Graph",
                        Message = "Połączenie z Microsoft Graph jest aktywne i prawidłowe"
                    });
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                result.Success = result.Errors.Count == 0;
                result.IsSuccess = result.Success;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"Synchronizacja z Graph. Sukces: {result.SuccessfulOperations.Count}, Błędy: {result.Errors.Count}"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HealthOrchestrator: Błąd podczas synchronizacji z Microsoft Graph");
                
                return HealthOperationResult.CreateError(
                    $"Błąd synchronizacji z Graph: {ex.Message}", 
                    "GraphSynchronization", 
                    stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<HealthOperationResult> OptimizeCachePerformanceAsync(string apiAccessToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.SystemBackup,
                    "Cache",
                    targetEntityName: "Optymalizacja Wydajności Cache"
                );

                var result = HealthOperationResult.CreateSuccess("CacheOptimization");

                // Pobierz aktualne metryki cache
                var currentMetrics = _graphCacheService.GetCacheMetrics();
                
                result.Metrics = new HealthMetrics
                {
                    CacheMetrics = currentMetrics
                };

                // Automatyczne optymalizacje jeśli potrzeba
                if (currentMetrics.HitRate < 70.0)
                {
                    // Nie ma metody ResetMetrics, więc robimy inwalidację cache
                    _graphCacheService.InvalidateAllCache();
                    
                    result.SuccessfulOperations.Add(new HealthOperationSuccess
                    {
                        Operation = "CacheInvalidation",
                        Component = "Cache",
                        Message = "Wyczyśzczono cache z powodu niskiej wydajności"
                    });
                }

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                result.Success = result.Errors.Count == 0;
                result.IsSuccess = result.Success;

                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    result.Success ? OperationStatus.Completed : OperationStatus.PartialSuccess,
                    $"Optymalizacja cache. Hit Rate: {currentMetrics.HitRate:F1}%, Operacje: {result.SuccessfulOperations.Count}"
                );

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HealthOrchestrator: Błąd podczas optymalizacji cache");
                
                return HealthOperationResult.CreateError(
                    $"Błąd optymalizacji cache: {ex.Message}", 
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
            if (string.IsNullOrEmpty(processId))
            {
                _logger.LogWarning("HealthOrchestrator: Próba anulowania procesu z pustym ID");
                return await Task.FromResult(false);
            }

            _logger.LogInformation("HealthOrchestrator: Próba anulowania procesu {ProcessId}", processId);

            if (_cancellationTokens.TryGetValue(processId, out var cts))
            {
                cts.Cancel();
                
                if (_activeProcesses.TryGetValue(processId, out var status))
                {
                    status.Status = "Cancelled";
                    status.CompletedAt = DateTime.UtcNow;
                }

                _logger.LogInformation("HealthOrchestrator: Proces {ProcessId} został anulowany", processId);
                return await Task.FromResult(true);
            }

            _logger.LogWarning("HealthOrchestrator: Nie znaleziono procesu {ProcessId} do anulowania", processId);
            return await Task.FromResult(false);
        }

        #region Private Helper Methods

        private async Task<HealthCheckDetail> CheckGraphConnectionHealthAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                stopwatch.Stop();

                var status = healthInfo.IsConnected && healthInfo.IsTokenValid 
                    ? HealthStatus.Healthy 
                    : HealthStatus.Degraded;

                return new HealthCheckDetail
                {
                    ComponentName = "Graph API Connection",
                    Status = status,
                    Description = status == HealthStatus.Healthy 
                        ? "Połączenie Graph API jest aktywne i sprawne" 
                        : $"Problemy z połączeniem Graph API. Connected: {healthInfo.IsConnected}, TokenValid: {healthInfo.IsTokenValid}",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["Connected"] = healthInfo.IsConnected,
                        ["TokenValid"] = healthInfo.IsTokenValid,
                        ["Status"] = healthInfo.Status.ToString(),
                        ["ResponseTimeMs"] = healthInfo.ResponseTimeMs
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Graph API Connection",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania połączenia Graph API: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckCachePerformanceAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var metrics = _graphCacheService.GetCacheMetrics();
                stopwatch.Stop();

                var status = metrics.IsPerformant ? HealthStatus.Healthy : 
                           (metrics.HitRate >= 50.0 ? HealthStatus.Degraded : HealthStatus.Unhealthy);

                return new HealthCheckDetail
                {
                    ComponentName = "Wydajność Cache",
                    Status = status,
                    Description = $"Cache Performance: {metrics.GetPerformanceStatus()}. Hit Rate: {metrics.HitRate:F1}%",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["HitRate"] = metrics.HitRate,
                        ["TotalOperations"] = metrics.TotalOperations,
                        ["AverageTimeMs"] = metrics.AverageOperationTimeMs,
                        ["PerformanceStatus"] = metrics.GetPerformanceStatus()
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Wydajność Cache",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania wydajności cache: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthCheckDetail> CheckSystemPerformanceAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var memoryUsage = GC.GetTotalMemory(false);
                
                stopwatch.Stop();

                var memoryMB = memoryUsage / (1024 * 1024);
                var status = memoryMB < 100 ? HealthStatus.Healthy : 
                           (memoryMB < 500 ? HealthStatus.Degraded : HealthStatus.Unhealthy);

                return new HealthCheckDetail
                {
                    ComponentName = "Wydajność Systemu",
                    Status = status,
                    Description = $"Użycie pamięci: {memoryMB}MB",
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    Data = new Dictionary<string, object>
                    {
                        ["MemoryUsageMB"] = memoryMB,
                        ["ProcessorCount"] = Environment.ProcessorCount
                    }
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckDetail
                {
                    ComponentName = "Wydajność Systemu",
                    Status = HealthStatus.Unhealthy,
                    Description = $"Błąd sprawdzania wydajności systemu: {ex.Message}",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private async Task<HealthMetrics> CollectSystemMetricsAsync()
        {
            var metrics = new HealthMetrics
            {
                CacheMetrics = _graphCacheService.GetCacheMetrics(),
                MemoryUsageBytes = GC.GetTotalMemory(false),
                ActiveConnections = 1,
                AverageApiResponseTimeMs = 50.0,
                ErrorsLastHour = 0
            };

            var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                            metrics.GraphConnectionStatus = healthInfo.IsConnected ? "Connected" : "Disconnected";

            return metrics;
        }

        private List<string> GenerateRecommendations(List<HealthCheckDetail> healthChecks, HealthMetrics? metrics)
        {
            var recommendations = new List<string>();

            // Rekomendacje bazujące na wynikach health checks
            var unhealthyComponents = healthChecks.Where(h => h.Status == HealthStatus.Unhealthy);
            var degradedComponents = healthChecks.Where(h => h.Status == HealthStatus.Degraded);

            foreach (var component in unhealthyComponents)
            {
                recommendations.Add($"🔴 KRYTYCZNE: {component.ComponentName} wymaga natychmiastowej uwagi - {component.Description}");
            }

            foreach (var component in degradedComponents)
            {
                recommendations.Add($"🟡 UWAGA: {component.ComponentName} działa z ograniczeniami - {component.Description}");
            }

            // Rekomendacje bazujące na metrykach
            if (metrics?.CacheMetrics != null)
            {
                if (metrics.CacheMetrics.HitRate < 80.0)
                {
                    recommendations.Add($"💡 Rozważ optymalizację strategii cache - aktualny Hit Rate: {metrics.CacheMetrics.HitRate:F1}%");
                }
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("✅ System działa optymalnie - brak rekomendacji");
            }

            return recommendations;
        }

        private async Task UpdateProcessStatusAsync(string processId, string currentOperation, int componentIndex)
        {
            if (_activeProcesses.TryGetValue(processId, out var status))
            {
                status.CurrentOperation = currentOperation;
                status.ComponentsChecked = componentIndex;
                status.ProgressPercentage = (double)componentIndex / status.TotalComponents * 100.0;
                
                _logger.LogDebug("HealthOrchestrator: {ProcessId} - {CurrentOperation} ({Progress:F1}%)", 
                    processId, currentOperation, status.ProgressPercentage);
            }

            await Task.CompletedTask;
        }

        private async Task ClearInvalidCacheAsync(HealthOperationResult result, bool dryRun, CancellationToken cancellationToken)
        {
            try
            {
                if (!dryRun)
                {
                    // ICacheInvalidationService nie ma metody InvalidateAllAsync, używamy batch invalidation
                    var allCacheKeys = new List<string> { "Teams", "Users", "Channels", "Departments", "Subjects" };
                    await _cacheInvalidationService.InvalidateBatchAsync(new Dictionary<string, List<string>>
                    {
                        ["HealthOrchestrator Auto Repair"] = allCacheKeys
                    });
                }
                
                result.SuccessfulOperations.Add(new HealthOperationSuccess
                {
                    Operation = "CacheClear",
                    Component = "Cache",
                    Message = dryRun ? "[DRY RUN] Cache zostałby wyczyszczony" : "Wyczyszczono cache"
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(new HealthOperationError
                {
                    Operation = "CacheClear",
                    Component = "Cache",
                    Message = $"Błąd czyszczenia cache: {ex.Message}",
                    Exception = ex,
                    Severity = HealthErrorSeverity.Warning
                });
            }
        }

        private async Task SendCompletionNotificationAsync(string operationType, HealthOperationResult result, string processId)
        {
            try
            {
                var message = $"HealthOrchestrator: {operationType} zakończone. " +
                             $"Status: {(result.Success ? "SUCCESS" : "ISSUES")}, " +
                             $"Czas: {result.ExecutionTimeMs}ms, " +
                             $"Sukces: {result.SuccessfulOperations.Count}, " +
                             $"Błędy: {result.Errors.Count}";

                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    message,
                    "HealthMonitoring"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HealthOrchestrator: Nie udało się wysłać powiadomienia o zakończeniu {ProcessId}", processId);
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
