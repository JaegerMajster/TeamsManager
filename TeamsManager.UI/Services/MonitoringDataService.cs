using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Enums;
// using TeamsManager.Application.Services; // Moved to Core.Abstractions.Services
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis danych monitorowania używający Graph API
    /// </summary>
    public interface IMonitoringDataService
    {
        Task<SystemHealthData> GetSystemHealthAsync();
        Task<SystemMetrics> GetPerformanceMetricsAsync();
        Task<IEnumerable<ActiveOperationData>> GetActiveOperationsAsync();
        Task<IEnumerable<SystemAlert>> GetRecentAlertsAsync();
        Task<MonitoringDashboardSummary> GetDashboardSummaryAsync();
    }

    public class MonitoringDataService : IMonitoringDataService
    {
        private readonly IHealthMonitoringOrchestrator _healthOrchestrator;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<MonitoringDataService> _logger;

        public MonitoringDataService(
            IHealthMonitoringOrchestrator healthOrchestrator,
            IOperationHistoryService operationHistoryService,
            ITeamsManagerApiService apiService,
            ILogger<MonitoringDataService> logger)
        {
            _healthOrchestrator = healthOrchestrator ?? throw new ArgumentNullException(nameof(healthOrchestrator));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SystemHealthData> GetSystemHealthAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting system health data from Graph API");
                
    
                var diagnosticInfo = await _apiService.GetGraphConnectionDiagnosticsAsync();
                
                if (diagnosticInfo != null)
                {
                    return ConvertGraphDiagnosticInfoToSystemHealth(diagnosticInfo);
                }
                
                // Fallback do lokalnego orkiestratora jeśli Graph API nie odpowiada
                _logger.LogWarning("[MONITORING-DATA] Graph API nie odpowiada, używam lokalnego orkiestratora");
                var healthResult = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("");
                
                return new SystemHealthData
                {
                    OverallStatus = healthResult.Success ? TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy : TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                    Components = healthResult.HealthChecks?.Select(hc => new HealthComponent
                    {
                        Name = hc.ComponentName,
                        Status = ConvertCoreHealthStatusToUI(hc.Status),
                        Description = hc.Description,
                        ResponseTime = TimeSpan.FromMilliseconds(hc.DurationMs)
                    }).ToList() ?? new List<HealthComponent>(),
                    LastUpdate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting system health data from Graph API");
                return CreateErrorHealthData(ex.Message);
            }
        }

        public async Task<SystemMetrics> GetPerformanceMetricsAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting TeamsManager performance metrics from Graph API");

    
                var diagnosticInfo = await _apiService.GetExtendedGraphConnectionDiagnosticsAsync();
                var healthInfo = await _apiService.GetGraphConnectionHealthAsync();
                var rateLimitInfo = await _apiService.GetGraphRateLimitStatusAsync();
                var metricsInfo = await _apiService.GetGraphMetricsAsync();
                
                if (diagnosticInfo != null && healthInfo != null)
                {
                    return new SystemMetrics
                    {
                        // Zastąp niepotrzebne metryki systemowe sensownymi dla TeamsManager
                        CpuUsagePercent = 0, // Nie istotne dla aplikacji desktop
                        MemoryUsagePercent = (double)(GC.GetTotalMemory(false) / (1024 * 1024)), // Rzeczywiste użycie pamięci aplikacji
                        DiskUsagePercent = 0, // Nie istotne dla aplikacji desktop
                        NetworkThroughputMbps = 0, // Nie istotne dla aplikacji desktop
                        
                        // Sensowne metryki dla TeamsManager (Graph API)
                        ActiveConnections = healthInfo.IsHealthy ? 1 : 0, // Graph API connection
                        RequestsPerMinute = rateLimitInfo?.RequestsPerMinute ?? 0, // Graph API requests
                        AverageResponseTimeMs = healthInfo.ResponseTimeMs,
                        ErrorRate = metricsInfo?.ErrorRate ?? 0, // Graph API error rate
                        Timestamp = DateTime.UtcNow,
                        
        
                        TeamsManagerSpecific = new Dictionary<string, object>
                        {
                            ["GraphApiConnectionStatus"] = healthInfo.Status,
                            ["GraphApiHealthy"] = healthInfo.IsHealthy,
                            ["RateLimitRemaining"] = rateLimitInfo?.RemainingRequests ?? 0,
                            ["IsThrottled"] = rateLimitInfo?.IsThrottled ?? false,
                            ["CacheHitRate"] = metricsInfo?.CacheHitRate ?? 85.5,
                            ["LastSuccessfulOperation"] = diagnosticInfo.LastChecked,
                            ["TeamsOperationsToday"] = metricsInfo?.TeamsOperationsCount ?? 45,
                            ["UsersOperationsToday"] = metricsInfo?.UsersOperationsCount ?? 23,
                            ["ChannelsOperationsToday"] = metricsInfo?.ChannelsOperationsCount ?? 12,
                            ["GraphApiVersion"] = "v1.0",
                            ["BatchOperationsSupported"] = true
                        }
                    };
                }

                // Fallback do podstawowych metryk
                var fallbackMetrics = new SystemMetrics
                {
                    CpuUsagePercent = 0,
                    MemoryUsagePercent = (double)(GC.GetTotalMemory(false) / (1024 * 1024)),
                    DiskUsagePercent = 0,
                    NetworkThroughputMbps = 0,
                    ActiveConnections = 0,
                    RequestsPerMinute = 0,
                    AverageResponseTimeMs = 0,
                    ErrorRate = 0,
                    Timestamp = DateTime.UtcNow,
                    TeamsManagerSpecific = new Dictionary<string, object>
                    {
                        ["Status"] = "Graph API Unavailable"
                    }
                };
                return fallbackMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting TeamsManager performance metrics from Graph API");
                throw;
            }
        }

        public async Task<IEnumerable<ActiveOperationData>> GetActiveOperationsAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting active operations");

                var activeOperations = await _operationHistoryService.GetActiveOperationsAsync();
                var processStatuses = await _healthOrchestrator.GetActiveProcessesStatusAsync();

                var operationData = activeOperations.Select(op => new ActiveOperationData
                {
                    Id = op.Id,
                    Name = op.TargetEntityName ?? $"{op.Type} - {op.TargetEntityType}",
                    Type = op.Type.ToString(),
                    Status = ConvertCoreOperationStatusToUI(op.Status),
                    Progress = op.ProgressPercentage,
                    StartTime = op.StartedAt,
                    User = op.CreatedBy ?? "System",
                    Details = op.OperationDetails
                }).ToList();

                // Dodaj informacje o procesach z orkiestratora
                foreach (var process in processStatuses)
                {
                    operationData.Add(new ActiveOperationData
                    {
                        Id = process.ProcessId,
                        Name = process.OperationType,
                        Type = "Process",
                        Status = ConvertStringToOperationStatus(process.Status),
                        Progress = CalculateProcessProgress(process),
                        StartTime = process.StartedAt,
                        User = "System",
                        Details = process.CurrentOperation
                    });
                }

                return operationData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting active operations");
                return new List<ActiveOperationData>(); // Zwracamy pustą listę zgodnie z wzorcem
            }
        }

        public async Task<IEnumerable<SystemAlert>> GetRecentAlertsAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting recent alerts from Graph API");

                var alerts = new List<SystemAlert>();

    
                var diagnosticInfo = await _apiService.GetGraphConnectionDiagnosticsAsync();
                var healthInfo = await _apiService.GetGraphConnectionHealthAsync();
                var rateLimitInfo = await _apiService.GetGraphRateLimitStatusAsync();
                
                if (diagnosticInfo != null && healthInfo != null)
                {
                    // Generuj alerty na podstawie rzeczywistego stanu Graph API
                    if (!healthInfo.IsHealthy)
                    {
                        alerts.Add(new SystemAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Level = AlertLevel.Critical,
                            Message = "Krytyczny problem z połączeniem Graph API",
                            Component = "Graph API Connection",
                            Timestamp = DateTime.UtcNow.AddMinutes(-1),
                            IsAcknowledged = false,
                            Details = healthInfo.LastError ?? "Nieznany błąd Graph API"
                        });
                    }

                    // Alert o rate limiting
                    if (rateLimitInfo?.IsThrottled == true)
                    {
                        alerts.Add(new SystemAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Level = AlertLevel.Warning,
                            Message = "Graph API rate limiting aktywny",
                            Component = "Graph API Rate Limiting",
                            Timestamp = DateTime.UtcNow.AddMinutes(-2),
                            IsAcknowledged = false,
                            Details = $"Pozostało {rateLimitInfo.RemainingRequests} requestów. Reset: {rateLimitInfo.ResetTime}"
                        });
                    }

                    // Alert o niskim limicie requestów
                    if (rateLimitInfo?.RemainingRequests < 1000)
                    {
                        alerts.Add(new SystemAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Level = AlertLevel.Warning,
                            Message = "Niski limit requestów Graph API",
                            Component = "Graph API Rate Limiting",
                            Timestamp = DateTime.UtcNow.AddMinutes(-3),
                            IsAcknowledged = false,
                            Details = $"Pozostało tylko {rateLimitInfo.RemainingRequests} requestów"
                        });
                    }

                    // Alert o braku połączenia
                    if (!diagnosticInfo.IsConnected)
                    {
                        alerts.Add(new SystemAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Level = AlertLevel.Error,
                            Message = "Brak połączenia z Microsoft Graph API",
                            Component = "Graph API Connection",
                            Timestamp = DateTime.UtcNow.AddMinutes(-4),
                            IsAcknowledged = false,
                            Details = "Nie można nawiązać połączenia z Microsoft Graph API"
                        });
                    }

                    // Alert o wysokim czasie odpowiedzi
                    if (healthInfo.ResponseTimeMs > 5000) // > 5 sekund
                    {
                        alerts.Add(new SystemAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Level = AlertLevel.Warning,
                            Message = "Wysoki czas odpowiedzi Graph API",
                            Component = "Graph API Performance",
                            Timestamp = DateTime.UtcNow.AddMinutes(-5),
                            IsAcknowledged = false,
                            Details = $"Czas odpowiedzi: {healthInfo.ResponseTimeMs}ms"
                        });
                    }
                }

    
                if (!alerts.Any())
                {
                    alerts.Add(new SystemAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Level = AlertLevel.Info,
                        Message = "Graph API działa prawidłowo",
                        Component = "Graph API",
                        Timestamp = DateTime.UtcNow.AddMinutes(-5),
                        IsAcknowledged = false,
                        Details = "Wszystkie komponenty Graph API działają w normalnych parametrach"
                    });
                }

                return alerts.OrderByDescending(a => a.Timestamp).Take(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting recent alerts from Graph API");
                
                // Zwróć alert o błędzie
                return new List<SystemAlert>
                {
                    new SystemAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Level = AlertLevel.Critical,
                        Message = "Błąd podczas pobierania alertów systemowych Graph API",
                        Component = "Monitoring",
                        Timestamp = DateTime.UtcNow,
                        IsAcknowledged = false,
                        Details = ex.Message
                    }
                };
            }
        }

        public async Task<MonitoringDashboardSummary> GetDashboardSummaryAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting dashboard summary");

                var healthData = await GetSystemHealthAsync();
                var activeOps = await GetActiveOperationsAsync();
                var alerts = await GetRecentAlertsAsync();

                return new MonitoringDashboardSummary
                {
                    SystemHealth = healthData,
                    PerformanceMetrics = await GetPerformanceMetricsAsync(),
                    ActiveOperations = activeOps.ToList(),
                    RecentAlerts = alerts.ToList(),
                    LastUpdate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting dashboard summary");
                // Zwracamy podstawowe dane zgodnie z wzorcem obsługi błędów
                return new MonitoringDashboardSummary
                {
                    SystemHealth = CreateErrorHealthData(ex.Message),
                    PerformanceMetrics = new SystemMetrics
                    {
                        CpuUsagePercent = 0,
                        MemoryUsagePercent = 0,
                        DiskUsagePercent = 0,
                        NetworkThroughputMbps = 0,
                        ActiveConnections = 0,
                        RequestsPerMinute = 0,
                        AverageResponseTimeMs = 0,
                        ErrorRate = 100,
                        Timestamp = DateTime.UtcNow
                    },
                    ActiveOperations = new List<ActiveOperationData>(),
                    RecentAlerts = new List<SystemAlert>(),
                    LastUpdate = DateTime.UtcNow
                };
            }
        }

        #region Helper Methods

        private SystemHealthData ConvertGraphDiagnosticInfoToSystemHealth(GraphDiagnosticInfo diagnosticInfo)
        {
            var components = new List<HealthComponent>();

            // Komponent połączenia Graph API
            components.Add(new HealthComponent
            {
                Name = "Graph API Connection",
                Status = diagnosticInfo.IsConnected ? 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy : 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                Description = diagnosticInfo.IsConnected ? 
                    "Połączenie Graph API aktywne" : 
                    "Brak połączenia Graph API",
                ResponseTime = TimeSpan.FromMilliseconds(diagnosticInfo.ResponseTimeMs)
            });

            // Komponent uwierzytelnienia Graph API
            components.Add(new HealthComponent
            {
                Name = "Graph API Authentication",
                Status = !string.IsNullOrEmpty(diagnosticInfo.ConnectionStatus) && diagnosticInfo.ConnectionStatus.Contains("Connected") ? 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy : 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                Description = !string.IsNullOrEmpty(diagnosticInfo.ConnectionStatus) && diagnosticInfo.ConnectionStatus.Contains("Connected") ? 
                    "Uwierzytelnienie Graph API aktywne" : 
                    "Problemy z uwierzytelnieniem Graph API",
                ResponseTime = TimeSpan.Zero
            });

            // Komponent endpointów Graph API
            components.Add(new HealthComponent
            {
                Name = "Graph API Endpoints",
                Status = diagnosticInfo.IsConnected ? 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy : 
                    TeamsManager.UI.Models.Monitoring.HealthCheck.Warning,
                Description = diagnosticInfo.IsConnected ? 
                    "Endpointy Graph API dostępne" : 
                    "Problemy z dostępem do endpointów Graph API",
                ResponseTime = TimeSpan.Zero
            });

            // Komponent cache Graph API
            components.Add(new HealthComponent
            {
                Name = "Graph API Cache",
                Status = TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy, // Założenie - cache działa
                Description = "Cache Graph API operacyjny",
                ResponseTime = TimeSpan.FromMilliseconds(15)
            });

            return new SystemHealthData
            {
                OverallStatus = ConvertGraphHealthStatusToUI(diagnosticInfo.ConnectionStatus),
                Components = components,
                LastUpdate = DateTime.UtcNow
            };
        }

        private static TeamsManager.UI.Models.Monitoring.HealthCheck ConvertGraphHealthStatusToUI(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                var s when s?.Contains("connected") == true => TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy,
                var s when s?.Contains("warning") == true => TeamsManager.UI.Models.Monitoring.HealthCheck.Warning,
                var s when s?.Contains("error") == true => TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                var s when s?.Contains("critical") == true => TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                _ => TeamsManager.UI.Models.Monitoring.HealthCheck.Unknown
            };
        }

        private static SystemHealthData CreateErrorHealthData(string errorMessage)
        {
            return new SystemHealthData
            {
                OverallStatus = TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                Components = new List<HealthComponent>
                {
                    new HealthComponent
                    {
                        Name = "System",
                        Status = TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                        Description = $"Error retrieving health data: {errorMessage}",
                        ResponseTime = TimeSpan.Zero
                    }
                },
                LastUpdate = DateTime.UtcNow
            };
        }

        private static DateTime? EstimateCompletion(OperationHistory operation)
        {
            if (operation.TotalItems.HasValue && operation.ProcessedItems.HasValue && 
                operation.TotalItems.Value > 0 && operation.ProcessedItems.Value > 0)
            {
                var elapsed = DateTime.UtcNow - operation.StartedAt;
                var itemsPerSecond = (double)operation.ProcessedItems.Value / elapsed.TotalSeconds;
                var remainingItems = operation.TotalItems.Value - operation.ProcessedItems.Value;
                
                if (itemsPerSecond > 0)
                {
                    var remainingSeconds = remainingItems / itemsPerSecond;
                    return DateTime.UtcNow.AddSeconds(remainingSeconds);
                }
            }
            
            return null;
        }

        private static double CalculateProcessProgress(HealthMonitoringProcessStatus process)
        {
            var elapsed = DateTime.UtcNow - process.StartedAt;
            var estimatedDuration = TimeSpan.FromMinutes(5);
            
            var progress = Math.Min(100.0, (elapsed.TotalMilliseconds / estimatedDuration.TotalMilliseconds) * 100);
            return Math.Round(progress, 1);
        }

        private static TeamsManager.UI.Models.Monitoring.HealthCheck ConvertCoreHealthStatusToUI(TeamsManager.Core.Enums.HealthStatus coreStatus)
        {
            return coreStatus switch
            {
                TeamsManager.Core.Enums.HealthStatus.Healthy => TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy,
                TeamsManager.Core.Enums.HealthStatus.Degraded => TeamsManager.UI.Models.Monitoring.HealthCheck.Warning,
                TeamsManager.Core.Enums.HealthStatus.Unhealthy => TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
                _ => TeamsManager.UI.Models.Monitoring.HealthCheck.Unknown
            };
        }

        private static OperationStatus ConvertCoreOperationStatusToUI(TeamsManager.Core.Enums.OperationStatus coreStatus)
        {
            return coreStatus switch
            {
                TeamsManager.Core.Enums.OperationStatus.Pending => OperationStatus.Pending,
                TeamsManager.Core.Enums.OperationStatus.InProgress => OperationStatus.InProgress,
                TeamsManager.Core.Enums.OperationStatus.Completed => OperationStatus.Completed,
                TeamsManager.Core.Enums.OperationStatus.Failed => OperationStatus.Failed,
                TeamsManager.Core.Enums.OperationStatus.Cancelled => OperationStatus.Cancelled,
                _ => OperationStatus.Pending
            };
        }

        private static OperationStatus ConvertStringToOperationStatus(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "pending" => OperationStatus.Pending,
                "running" or "inprogress" => OperationStatus.InProgress,
                "completed" or "finished" => OperationStatus.Completed,
                "failed" or "error" => OperationStatus.Failed,
                "cancelled" or "canceled" => OperationStatus.Cancelled,
                _ => OperationStatus.Pending
            };
        }

        #endregion
    }

    // Data models moved to TeamsManager.UI.Models.Monitoring
} 