using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
// using TeamsManager.Application.Services; // Moved to Core.Abstractions.Services
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.Services
{
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
        private readonly ILogger<MonitoringDataService> _logger;

        public MonitoringDataService(
            IHealthMonitoringOrchestrator healthOrchestrator,
            IOperationHistoryService operationHistoryService,
            ILogger<MonitoringDataService> logger)
        {
            _healthOrchestrator = healthOrchestrator ?? throw new ArgumentNullException(nameof(healthOrchestrator));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SystemHealthData> GetSystemHealthAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting system health data");
                
                // Note: W rzeczywistej implementacji przekazalibyśmy token
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
                _logger.LogError(ex, "[MONITORING-DATA] Error getting system health data");
                return CreateErrorHealthData(ex.Message);
            }
        }

        public async Task<SystemMetrics> GetPerformanceMetricsAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting performance metrics");

                // Symulowane metryki wydajności - w rzeczywistej implementacji zbieralibyśmy rzeczywiste metryki
                return new SystemMetrics
                {
                    CpuUsagePercent = Random.Shared.Next(10, 80),
                    MemoryUsagePercent = Random.Shared.Next(30, 90),
                    DiskUsagePercent = Random.Shared.Next(20, 70),
                    NetworkThroughputMbps = Random.Shared.Next(1, 100),
                    ActiveConnections = Random.Shared.Next(5, 50),
                    RequestsPerMinute = Random.Shared.Next(10, 200),
                    AverageResponseTimeMs = Random.Shared.Next(50, 500),
                    ErrorRate = Random.Shared.NextDouble() * 5, // 0-5% error rate
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting performance metrics");
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
                throw;
            }
        }

        public async Task<IEnumerable<SystemAlert>> GetRecentAlertsAsync()
        {
            try
            {
                _logger.LogDebug("[MONITORING-DATA] Getting recent alerts");

                // Symulowane alerty - w rzeczywistej implementacji pobieralibyśmy z systemu alertów
                var alerts = new List<SystemAlert>
                {
                    new SystemAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Level = AlertLevel.Warning,
                        Message = "Cache hit rate below optimal threshold (68%)",
                        Component = "Cache",
                        Timestamp = DateTime.UtcNow.AddMinutes(-5),
                        IsAcknowledged = false
                    },
                    new SystemAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Level = AlertLevel.Info,
                        Message = "Scheduled backup completed successfully",
                        Component = "System",
                        Timestamp = DateTime.UtcNow.AddMinutes(-15),
                        IsAcknowledged = true
                    }
                };

                return alerts.OrderByDescending(a => a.Timestamp).Take(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DATA] Error getting recent alerts");
                throw;
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
                throw;
            }
        }

        #region Helper Methods

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
            if (process.TotalComponents <= 0) return 0;
            
            // Symulujemy postęp na podstawie czasu trwania
            var elapsed = DateTime.UtcNow - process.StartedAt;
            var estimatedDuration = TimeSpan.FromMinutes(2); // Zakładamy 2 minuty na proces
            
            return Math.Min(100, (elapsed.TotalSeconds / estimatedDuration.TotalSeconds) * 100);
        }

        private static TeamsManager.UI.Models.Monitoring.HealthCheck ConvertCoreHealthStatusToUI(TeamsManager.Core.Models.HealthStatus coreStatus)
        {
            return coreStatus switch
            {
                TeamsManager.Core.Models.HealthStatus.Healthy => TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy,
                TeamsManager.Core.Models.HealthStatus.Degraded => TeamsManager.UI.Models.Monitoring.HealthCheck.Warning,
                TeamsManager.Core.Models.HealthStatus.Unhealthy => TeamsManager.UI.Models.Monitoring.HealthCheck.Critical,
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
                TeamsManager.Core.Enums.OperationStatus.PartialSuccess => OperationStatus.PartialSuccess,
                _ => OperationStatus.Failed // Default to Failed for unknown statuses
            };
        }

        private static OperationStatus ConvertStringToOperationStatus(string status)
        {
            return status?.ToLowerInvariant() switch
            {
                "pending" => OperationStatus.Pending,
                "running" or "inprogress" or "in_progress" => OperationStatus.InProgress,
                "completed" or "success" => OperationStatus.Completed,
                "failed" or "error" => OperationStatus.Failed,
                "cancelled" or "canceled" => OperationStatus.Cancelled,
                "partialsuccess" or "partial_success" => OperationStatus.PartialSuccess,
                _ => OperationStatus.Failed // Default to Failed for unknown statuses
            };
        }

        #endregion
    }

    // Data models moved to TeamsManager.UI.Models.Monitoring
} 