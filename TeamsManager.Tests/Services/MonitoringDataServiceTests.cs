using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.UI.Models.Monitoring;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using Xunit;
using FluentAssertions;

namespace TeamsManager.Tests.Services
{
    /// <summary>
    /// Testy jednostkowe dla MonitoringDataService
    /// Testuje agregację danych z różnych źródeł dla dashboard monitoringu
    /// </summary>
    public class MonitoringDataServiceTests
    {
        private readonly Mock<IHealthMonitoringOrchestrator> _healthOrchestratorMock;
        private readonly Mock<IOperationHistoryService> _operationHistoryServiceMock;
        private readonly Mock<ITeamsManagerApiService> _apiServiceMock;
        private readonly Mock<ILogger<MonitoringDataService>> _loggerMock;
        private readonly MonitoringDataService _service;

        public MonitoringDataServiceTests()
        {
            _healthOrchestratorMock = new Mock<IHealthMonitoringOrchestrator>();
            _operationHistoryServiceMock = new Mock<IOperationHistoryService>();
            _apiServiceMock = new Mock<ITeamsManagerApiService>();
            _loggerMock = new Mock<ILogger<MonitoringDataService>>();

            _service = new MonitoringDataService(
                _healthOrchestratorMock.Object,
                _operationHistoryServiceMock.Object,
                _apiServiceMock.Object,
                _loggerMock.Object);
        }

        #region GetSystemHealthAsync Tests

        [Fact]
        public async Task GetSystemHealthAsync_HealthySystem_ReturnsHealthyStatus()
        {
            // Arrange
            var healthResult = HealthOperationResult.CreateSuccess("ComprehensiveHealthCheck");
            healthResult.HealthChecks = new List<HealthCheckDetail>
            {
                new HealthCheckDetail
                {
                    ComponentName = "Graph API",
                    Status = TeamsManager.Core.Enums.HealthStatus.Healthy,
                    Description = "Graph API connection is healthy",
                    DurationMs = 150
                },
                new HealthCheckDetail
                {
                    ComponentName = "Cache",
                    Status = TeamsManager.Core.Enums.HealthStatus.Healthy,
                    Description = "Cache performance is optimal",
                    DurationMs = 75
                }
            };

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(It.IsAny<string>()))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy);
            result.Components.Should().HaveCount(2);
            result.Components.Should().OnlyContain(c => c.Status == TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy);
            result.LastUpdate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetSystemHealthAsync_UnhealthyComponent_ReturnsUnhealthyStatus()
        {
            // Arrange
            var healthResult = new HealthOperationResult
            {
                Success = false,
                HealthChecks = new List<HealthCheckDetail>
                {
                    new HealthCheckDetail
                    {
                        ComponentName = "Graph API",
                        Status = TeamsManager.Core.Enums.HealthStatus.Unhealthy,
                        Description = "Graph API connection failed",
                        DurationMs = 5000
                    }
                }
            };

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(It.IsAny<string>()))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Critical);
            result.Components.Should().HaveCount(1);
            result.Components.First().Status.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Critical);
        }

        [Fact]
        public async Task GetSystemHealthAsync_Exception_ReturnsErrorHealth()
        {
            // Arrange
            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Health check failed"));

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Critical);
            result.Components.Should().HaveCount(1);
            result.Components.First().Name.Should().Be("System");
            result.Components.First().Description.Should().Contain("Health check failed");
        }

        #endregion

        #region GetPerformanceMetricsAsync Tests

        [Fact]
        public async Task GetPerformanceMetricsAsync_ValidMetrics_ReturnsSystemMetrics()
        {
            // Act
            var result = await _service.GetPerformanceMetricsAsync();

            // Assert
            result.Should().NotBeNull();
            result.CpuUsagePercent.Should().BeInRange(0, 100);
            result.MemoryUsagePercent.Should().BeInRange(0, 100);
            result.DiskUsagePercent.Should().BeInRange(0, 100);
            result.NetworkThroughputMbps.Should().BeGreaterThanOrEqualTo(0);
            result.ActiveConnections.Should().BeGreaterThanOrEqualTo(0);
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        #endregion

        #region GetActiveOperationsAsync Tests

        [Fact]
        public async Task GetActiveOperationsAsync_WithActiveOperations_ReturnsOperations()
        {
            // Arrange
            var activeOperations = new List<OperationHistory>
            {
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.UserCreated,
                    Status = OperationStatus.InProgress,
                    CreatedBy = "test@example.com",
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    ProcessedItems = 455,
                    TotalItems = 1000,
                    OperationDetails = "Processing user bulk operations"
                },
                new OperationHistory
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = OperationType.TeamCreated,
                    Status = OperationStatus.InProgress,
                    CreatedBy = "admin@example.com",
                    StartedAt = DateTime.UtcNow.AddMinutes(-2),
                    ProcessedItems = 782,
                    TotalItems = 1000,
                    OperationDetails = "Creating team channels"
                }
            };

            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ReturnsAsync(activeOperations);

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            
            var operations = result.ToList();
            operations[0].Type.Should().Be("UserCreated");
            operations[0].Status.Should().Be(OperationStatus.InProgress);
            operations[0].Progress.Should().Be(45.5);
            operations[0].User.Should().Be("test@example.com");
            
            operations[1].Type.Should().Be("TeamCreated");
            operations[1].Status.Should().Be(OperationStatus.InProgress);
            operations[1].Progress.Should().Be(78.2);
            operations[1].User.Should().Be("admin@example.com");
        }

        [Fact]
        public async Task GetActiveOperationsAsync_NoActiveOperations_ReturnsEmptyList()
        {
            // Arrange
            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ReturnsAsync(new List<OperationHistory>());

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveOperationsAsync_Exception_ReturnsEmptyList()
        {
            // Arrange
            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region GetRecentAlertsAsync Tests

        [Fact]
        public async Task GetRecentAlertsAsync_ValidAlerts_ReturnsSystemAlerts()
        {
            // Act
            var result = await _service.GetRecentAlertsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            
            foreach (var alert in result)
            {
                alert.Id.Should().NotBeEmpty();
                alert.Message.Should().NotBeEmpty();
                alert.Level.Should().BeOneOf(TeamsManager.UI.Models.Monitoring.AlertLevel.Info, TeamsManager.UI.Models.Monitoring.AlertLevel.Warning, TeamsManager.UI.Models.Monitoring.AlertLevel.Error, TeamsManager.UI.Models.Monitoring.AlertLevel.Critical);
                alert.Timestamp.Should().BeBefore(DateTime.UtcNow);
            }
        }

        #endregion

        #region GetDashboardSummaryAsync Tests

        [Fact]
        public async Task GetDashboardSummaryAsync_ValidData_ReturnsSummary()
        {
            // Arrange
            var healthResult = HealthOperationResult.CreateSuccess("ComprehensiveHealthCheck");
            healthResult.HealthChecks = new List<HealthCheckDetail>
            {
                new HealthCheckDetail { ComponentName = "Graph API", Status = TeamsManager.Core.Enums.HealthStatus.Healthy },
                new HealthCheckDetail { ComponentName = "Cache", Status = TeamsManager.Core.Enums.HealthStatus.Degraded }
            };

            var activeOperations = new List<OperationHistory>
            {
                new OperationHistory { Status = OperationStatus.InProgress },
                new OperationHistory { Status = OperationStatus.InProgress },
                new OperationHistory { Status = OperationStatus.Pending }
            };

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(It.IsAny<string>()))
                .ReturnsAsync(healthResult);

            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ReturnsAsync(activeOperations);

            // Act
            var result = await _service.GetDashboardSummaryAsync();

            // Assert
            result.Should().NotBeNull();
            result.SystemHealth.Should().NotBeNull();
            result.PerformanceMetrics.Should().NotBeNull();
            result.ActiveOperations.Should().NotBeNull();
            result.ActiveOperations.Count.Should().Be(3);
            result.LastUpdate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetDashboardSummaryAsync_Exception_ReturnsErrorSummary()
        {
            // Arrange
            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Service unavailable"));

            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _service.GetDashboardSummaryAsync();

            // Assert
            result.Should().NotBeNull();
            result.SystemHealth.Should().NotBeNull();
            result.PerformanceMetrics.Should().NotBeNull();
            result.ActiveOperations.Should().NotBeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            // Act & Assert
            var service = new MonitoringDataService(
                _healthOrchestratorMock.Object,
                _operationHistoryServiceMock.Object,
                _apiServiceMock.Object,
                _loggerMock.Object);

            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullHealthOrchestrator_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new MonitoringDataService(
                null!,
                _operationHistoryServiceMock.Object,
                _apiServiceMock.Object,
                _loggerMock.Object);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("healthOrchestrator");
        }

        [Fact]
        public void Constructor_WithNullOperationHistoryService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new MonitoringDataService(
                _healthOrchestratorMock.Object,
                null!,
                _apiServiceMock.Object,
                _loggerMock.Object);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("operationHistoryService");
        }

        [Fact]
        public void Constructor_WithNullApiService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new MonitoringDataService(
                _healthOrchestratorMock.Object,
                _operationHistoryServiceMock.Object,
                null!,
                _loggerMock.Object);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("apiService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var action = () => new MonitoringDataService(
                _healthOrchestratorMock.Object,
                _operationHistoryServiceMock.Object,
                _apiServiceMock.Object,
                null!);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        #endregion

        #region GetSystemHealthAsync Additional Tests

        [Fact]
        public async Task GetSystemHealthAsync_WithNullGraphDiagnosticInfo_ShouldFallbackToOrchestrator()
        {
            // Arrange
            var healthResult = new HealthOperationResult
            {
                Success = true,
                IsSuccess = true,
                HealthChecks = new List<HealthCheckDetail>
                {
                    new HealthCheckDetail
                    {
                        ComponentName = "Graph API",
                        Status = HealthStatus.Healthy,
                        Description = "Connection healthy",
                        DurationMs = 100
                    }
                }
            };

            _apiServiceMock
                .Setup(x => x.GetGraphConnectionDiagnosticsAsync())
                .ReturnsAsync((GraphDiagnosticInfo)null);

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(""))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Healthy);
            result.Components.Should().HaveCount(1);
            result.Components[0].Name.Should().Be("Graph API");
            _healthOrchestratorMock.Verify(x => x.RunComprehensiveHealthCheckAsync(""), Times.Once);
        }

        [Fact]
        public async Task GetSystemHealthAsync_WithUnhealthyOrchestrator_ShouldReturnCritical()
        {
            // Arrange
            var healthResult = new HealthOperationResult
            {
                Success = false,
                IsSuccess = false,
                HealthChecks = new List<HealthCheckDetail>
                {
                    new HealthCheckDetail
                    {
                        ComponentName = "Graph API",
                        Status = HealthStatus.Unhealthy,
                        Description = "Connection failed",
                        DurationMs = 5000
                    }
                }
            };

            _apiServiceMock
                .Setup(x => x.GetGraphConnectionDiagnosticsAsync())
                .ReturnsAsync((GraphDiagnosticInfo)null);

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(""))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.OverallStatus.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Critical);
            result.Components.Should().HaveCount(1);
            result.Components[0].Status.Should().Be(TeamsManager.UI.Models.Monitoring.HealthCheck.Critical);
        }

        #endregion

        #region GetActiveOperationsAsync Additional Tests

        [Fact]
        public async Task GetActiveOperationsAsync_WithProcessStatuses_ShouldIncludeProcessData()
        {
            // Arrange
            var activeOperations = new List<OperationHistory>
            {
                new OperationHistory
                {
                    Id = "op-1",
                    Type = OperationType.TeamCreated,
                    Status = OperationStatus.InProgress,
                    TargetEntityName = "Test Team",
                    CreatedBy = "user@test.com",
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    ProcessedItems = 50,
                    TotalItems = 100
                }
            };

            var processStatuses = new List<HealthMonitoringProcessStatus>
            {
                new HealthMonitoringProcessStatus
                {
                    ProcessId = "process-1",
                    OperationType = "HealthCheck",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow.AddMinutes(-2),
                    CurrentOperation = "Checking Graph API"
                }
            };

            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ReturnsAsync(activeOperations);

            _healthOrchestratorMock
                .Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(processStatuses);

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(op => op.Type == "Process");
            result.Should().Contain(op => op.Name == "HealthCheck");
        }

        [Fact]
        public async Task GetActiveOperationsAsync_WithException_ShouldReturnEmptyListGracefully()
        {
            // Arrange
            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ThrowsAsync(new InvalidOperationException("Database connection failed"));

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            // Implementation gracefully handles exceptions by returning empty list
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task GetSystemHealthAsync_Performance_ShouldCompleteQuickly()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var healthResult = new HealthOperationResult
            {
                Success = true,
                IsSuccess = true,
                HealthChecks = new List<HealthCheckDetail>()
            };

            _apiServiceMock
                .Setup(x => x.GetGraphConnectionDiagnosticsAsync())
                .ReturnsAsync((GraphDiagnosticInfo)null);

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(""))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();
            stopwatch.Stop();

            // Assert
            result.Should().NotBeNull();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetSystemHealthAsync_WithEmptyHealthChecks_ShouldHandleGracefully()
        {
            // Arrange
            var healthResult = new HealthOperationResult
            {
                Success = true,
                IsSuccess = true,
                HealthChecks = null // Null health checks
            };

            _apiServiceMock
                .Setup(x => x.GetGraphConnectionDiagnosticsAsync())
                .ReturnsAsync((GraphDiagnosticInfo)null);

            _healthOrchestratorMock
                .Setup(x => x.RunComprehensiveHealthCheckAsync(""))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _service.GetSystemHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.Components.Should().NotBeNull();
            result.Components.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveOperationsAsync_WithNullProcessStatuses_ShouldHandleGracefully()
        {
            // Arrange
            var activeOperations = new List<OperationHistory>();

            _operationHistoryServiceMock
                .Setup(x => x.GetActiveOperationsAsync())
                .ReturnsAsync(activeOperations);

            _healthOrchestratorMock
                .Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync((IEnumerable<HealthMonitoringProcessStatus>)null);

            // Act
            var result = await _service.GetActiveOperationsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion
    }
} 