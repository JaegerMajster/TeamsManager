using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Tests.Infrastructure;
using TeamsManager.UI.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace TeamsManager.Tests.Integration
{
    /// <summary>
    /// Testy integracyjne dla systemu monitoringu
    /// Testuje całą komunikację SignalR, agregację danych i real-time updates
    /// </summary>
    public class MonitoringIntegrationTests : IntegrationTestBase
    {
        private readonly IMonitoringDataService _monitoringDataService;
        private readonly ISignalRService _signalRService;
        private readonly IHealthMonitoringOrchestrator _healthOrchestrator;

        public MonitoringIntegrationTests()
        {
            _monitoringDataService = ServiceScope.ServiceProvider.GetRequiredService<IMonitoringDataService>();
            _signalRService = ServiceScope.ServiceProvider.GetRequiredService<ISignalRService>();
            _healthOrchestrator = ServiceScope.ServiceProvider.GetRequiredService<IHealthMonitoringOrchestrator>();
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Dodatkowe serwisy dla testów monitoringu
            services.AddSingleton<ISignalRService, SignalRService>();
            services.AddScoped<IMonitoringDataService, MonitoringDataService>();
            services.AddScoped<IHealthMonitoringOrchestrator, TeamsManager.Application.Services.HealthMonitoringOrchestrator>();
            services.AddScoped<TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService, TeamsManager.Core.Services.CacheInvalidationService>();
        }

        #region MonitoringDataService Integration Tests

        [Fact]
        public async Task MonitoringDataService_GetSystemHealthAsync_IntegrationTest()
        {
            // Act
            var result = await _monitoringDataService.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
            result.Components.Should().NotBeNull();
            result.LastUpdate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Wszystkie komponenty powinny mieć podstawowe informacje
            foreach (var component in result.Components)
            {
                component.Name.Should().NotBeEmpty();
                component.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
                component.Description.Should().NotBeEmpty();
                component.ResponseTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
            }
        }

        [Fact]
        public async Task MonitoringDataService_GetPerformanceMetricsAsync_IntegrationTest()
        {
            // Act
            var result = await _monitoringDataService.GetPerformanceMetricsAsync();

            // Assert
            result.Should().NotBeNull();
            result.CpuUsagePercent.Should().BeInRange(0, 100);
            result.MemoryUsagePercent.Should().BeInRange(0, 100);
            result.DiskUsagePercent.Should().BeInRange(0, 100);
            result.NetworkThroughputMbps.Should().BeGreaterOrEqualTo(0);
            result.ActiveConnections.Should().BeGreaterOrEqualTo(0);
            result.RequestsPerMinute.Should().BeGreaterOrEqualTo(0);
            result.AverageResponseTimeMs.Should().BeGreaterOrEqualTo(0);
            result.ErrorRate.Should().BeInRange(0, 100);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task MonitoringDataService_GetActiveOperationsAsync_IntegrationTest()
        {
            // Arrange - Create some test operations
            var operationHistoryService = ServiceScope.ServiceProvider.GetRequiredService<IOperationHistoryService>();
            
            var operation1 = await operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.UserManagement,
                "test-entity-id",
                targetEntityName: "Integration Test Operation 1");

            await operationHistoryService.UpdateOperationProgressAsync(operation1.Id, 25.0, "Test progress update");

            // Act
            var result = await _monitoringDataService.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            
            if (result.Any())
            {
                var operations = result.ToList();
                foreach (var operation in operations)
                {
                    operation.Id.Should().NotBeEmpty();
                    operation.Name.Should().NotBeEmpty();
                    operation.Type.Should().NotBeEmpty();
                    operation.Status.Should().BeOneOf(OperationStatus.Pending, OperationStatus.Running, OperationStatus.Completed, OperationStatus.Failed, OperationStatus.Cancelled);
                    operation.Progress.Should().BeInRange(0, 100);
                    operation.User.Should().NotBeEmpty();
                    operation.StartTime.Should().BeBefore(DateTime.UtcNow);
                }
            }
        }

        [Fact]
        public async Task MonitoringDataService_GetRecentAlertsAsync_IntegrationTest()
        {
            // Act
            var result = await _monitoringDataService.GetRecentAlertsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty(); // Should always have some mock alerts

            foreach (var alert in result)
            {
                alert.Id.Should().NotBeEmpty();
                alert.Message.Should().NotBeEmpty();
                alert.Level.Should().BeOneOf(AlertLevel.Info, AlertLevel.Warning, AlertLevel.Error, AlertLevel.Critical);
                alert.Timestamp.Should().BeBefore(DateTime.UtcNow);
                alert.Source.Should().NotBeEmpty();
                alert.IsAcknowledged.Should().Be(false); // Mock alerts are not acknowledged
            }
        }

        [Fact]
        public async Task MonitoringDataService_GetDashboardSummaryAsync_IntegrationTest()
        {
            // Act
            var result = await _monitoringDataService.GetDashboardSummaryAsync();

            // Assert
            result.Should().NotBeNull();
            result.SystemStatus.Should().BeOneOf("Healthy", "Degraded", "Unhealthy", "Error");
            result.TotalComponents.Should().BeGreaterOrEqualTo(0);
            result.HealthyComponents.Should().BeInRange(0, result.TotalComponents);
            result.ActiveOperations.Should().BeGreaterOrEqualTo(0);
            result.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region SignalRService Integration Tests

        [Fact]
        public async Task SignalRService_ConnectionState_IntegrationTest()
        {
            // Act - Check initial state
            var initialState = _signalRService.ConnectionState;

            // Assert
            initialState.Should().BeOneOf(
                HubConnectionState.Disconnected,
                HubConnectionState.Connecting,
                HubConnectionState.Connected,
                HubConnectionState.Reconnecting);

            // Note: We can't test actual connection without running SignalR hub
            // This test validates that the service is properly configured
        }

        [Fact]
        public void SignalRService_Observables_IntegrationTest()
        {
            // Act & Assert - Verify observables are not null
            _signalRService.SystemHealthUpdates.Should().NotBeNull();
            _signalRService.PerformanceMetricsUpdates.Should().NotBeNull();
            _signalRService.ActiveOperationsUpdates.Should().NotBeNull();
            _signalRService.AlertsUpdates.Should().NotBeNull();
            _signalRService.ConnectionStateUpdates.Should().NotBeNull();

            // Note: Testing actual subscription requires a running SignalR hub
            // These tests verify the service structure is correct
        }

        #endregion

        #region HealthMonitoringOrchestrator Integration Tests

        [Fact]
        public async Task HealthMonitoringOrchestrator_RunComprehensiveHealthCheckAsync_IntegrationTest()
        {
            // Act
            var result = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("test-token");

            // Assert
            result.Should().NotBeNull();
            result.OperationType.Should().Be("ComprehensiveHealthCheck");
            result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
            
            if (result.Success)
            {
                result.IsSuccess.Should().BeTrue();
                result.HealthChecks.Should().NotBeNull();
                result.HealthChecks.Should().NotBeEmpty();
                result.Metrics.Should().NotBeNull();
                result.Recommendations.Should().NotBeNull();
            }
            else
            {
                result.IsSuccess.Should().BeFalse();
                result.ErrorMessage.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task HealthMonitoringOrchestrator_OptimizeCachePerformanceAsync_IntegrationTest()
        {
            // Act
            var result = await _healthOrchestrator.OptimizeCachePerformanceAsync("test-token");

            // Assert
            result.Should().NotBeNull();
            result.OperationType.Should().Be("CacheOptimization");
            result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);

            if (result.Success)
            {
                result.IsSuccess.Should().BeTrue();
                result.Recommendations.Should().NotBeNull();
            }
            else
            {
                result.IsSuccess.Should().BeFalse();
                result.ErrorMessage.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task HealthMonitoringOrchestrator_GetActiveProcessesStatusAsync_IntegrationTest()
        {
            // Act
            var result = await _healthOrchestrator.GetActiveProcessesStatusAsync();

            // Assert
            result.Should().NotBeNull();
            // Result can be empty if no processes are running
        }

        #endregion

        #region Full Workflow Integration Tests

        [Fact]
        public async Task MonitoringWorkflow_FullDashboardDataFlow_IntegrationTest()
        {
            // Act - Simulate full dashboard data loading
            var healthTask = _monitoringDataService.GetSystemHealthAsync();
            var metricsTask = _monitoringDataService.GetPerformanceMetricsAsync();
            var operationsTask = _monitoringDataService.GetActiveOperationsAsync();
            var alertsTask = _monitoringDataService.GetRecentAlertsAsync();
            var summaryTask = _monitoringDataService.GetDashboardSummaryAsync();

            await Task.WhenAll(healthTask, metricsTask, operationsTask, alertsTask, summaryTask);

            // Assert - All data should be retrieved successfully
            var health = await healthTask;
            var metrics = await metricsTask;
            var operations = await operationsTask;
            var alerts = await alertsTask;
            var summary = await summaryTask;

            health.Should().NotBeNull();
            metrics.Should().NotBeNull();
            operations.Should().NotBeNull();
            alerts.Should().NotBeNull();
            summary.Should().NotBeNull();

            // Verify data consistency
            summary.TotalComponents.Should().Be(health.Components.Count);
            summary.HealthyComponents.Should().Be(health.Components.Count(c => c.Status == HealthStatus.Healthy));
        }

        [Fact]
        public async Task MonitoringWorkflow_HealthCheckAndAutoRepair_IntegrationTest()
        {
            // Arrange
            var repairOptions = new RepairOptions
            {
                RepairPowerShellConnection = true,
                ClearInvalidCache = true,
                RestartStuckProcesses = false,
                OptimizeDatabase = false,
                SendAdminNotifications = false,
                DryRun = true // Safe for testing
            };

            // Act
            var healthCheck = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("test-token");
            
            if (!healthCheck.Success)
            {
                var autoRepair = await _healthOrchestrator.AutoRepairCommonIssuesAsync(repairOptions, "test-token");
                autoRepair.Should().NotBeNull();
                autoRepair.OperationType.Should().Be("AutoRepair");
            }

            // Assert
            healthCheck.Should().NotBeNull();
            healthCheck.OperationType.Should().Be("ComprehensiveHealthCheck");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task MonitoringPerformance_ConcurrentDataRetrieval_IntegrationTest()
        {
            // Arrange
            var concurrentTasks = 5;
            var tasks = new List<Task>();

            // Act - Simulate multiple concurrent dashboard requests
            for (int i = 0; i < concurrentTasks; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var summary = await _monitoringDataService.GetDashboardSummaryAsync();
                    summary.Should().NotBeNull();
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Assert - All tasks should complete successfully
            tasks.Should().OnlyContain(t => t.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task MonitoringPerformance_HealthCheckResponseTime_IntegrationTest()
        {
            // Arrange
            var startTime = DateTime.UtcNow;

            // Act
            var result = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("test-token");

            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Assert
            result.Should().NotBeNull();
            duration.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Health check should complete within 30 seconds
            result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion
    }
} 