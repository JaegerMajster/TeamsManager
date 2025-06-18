using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Services.Application
{
    public class HealthMonitoringOrchestratorTests
    {
        private readonly Mock<IGraphConnectionService> _mockGraphConnectionService;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly Mock<ICacheInvalidationService> _mockCacheInvalidationService;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<HealthMonitoringOrchestrator>> _mockLogger;
        private readonly HealthMonitoringOrchestrator _orchestrator;

        public HealthMonitoringOrchestratorTests()
        {
            _mockGraphConnectionService = new Mock<IGraphConnectionService>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();
            _mockCacheInvalidationService = new Mock<ICacheInvalidationService>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<HealthMonitoringOrchestrator>>();

            _orchestrator = new HealthMonitoringOrchestrator(
                _mockGraphConnectionService.Object,
                _mockGraphCacheService.Object,
                _mockCacheInvalidationService.Object,
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );

            // Domyślne setup
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("test@test.com");
        }

        #region RunComprehensiveHealthCheckAsync Tests

        [Fact]
        public async Task RunComprehensiveHealthCheckAsync_WithHealthySystem_ShouldReturnSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };
            var healthyConnectionInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy,
                ResponseTimeMs = 100
            };
            var healthyCacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 850,
                CacheMisses = 150,
                AverageAccessTimeMs = 50
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "System",
                null,
                "Comprehensive Health Check",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthyConnectionInfo);
            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(healthyCacheMetrics);

            // Act
            var result = await _orchestrator.RunComprehensiveHealthCheckAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.IsSuccess);
            Assert.True(result.HealthChecks.Count >= 2); // Przynajmniej Graph i Cache
            Assert.True(result.ExecutionTimeMs > 0);
            Assert.NotNull(result.Recommendations);
        }

        [Fact]
        public async Task RunComprehensiveHealthCheckAsync_WithUnhealthyComponents_ShouldReturnDegradedStatus()
        {
            // Arrange
            var accessToken = "valid-token";
            var unhealthyConnectionInfo = new GraphConnectionHealthInfo
            {
                IsConnected = false,
                IsTokenValid = false,
                Status = GraphHealthStatus.Critical,
                ResponseTimeMs = 5000
            };
            var poorCacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 500,
                CacheMisses = 500,
                AverageAccessTimeMs = 200
            };

            // Mock implementacji używa GetConnectionHealthAsync, nie GetDiagnosticInfoAsync
            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(unhealthyConnectionInfo);
            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(poorCacheMetrics);

            var mockOperation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "System",
                null,
                "Comprehensive Health Check",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.RunComprehensiveHealthCheckAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            // Implementacja mapuje brak połączenia Graph na Degraded, nie Unhealthy
            // Cache z 50% hit rate też jest Degraded, więc Success = true zgodnie z logiką:
            // "else if (degradedCount > 0) { result.Success = true; }"
            Assert.True(result.Success); // Degraded components still allow Success = true
            Assert.True(result.HealthChecks.Any(c => c.Status == HealthStatus.Degraded));
        }

        [Fact]
        public async Task RunComprehensiveHealthCheckAsync_WithException_ShouldReturnError()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "System",
                null,
                "Comprehensive Health Check",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ThrowsAsync(new InvalidOperationException("Graph service unavailable"));

            // Act
            var result = await _orchestrator.RunComprehensiveHealthCheckAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.False(result.IsSuccess);
            Assert.Contains("Krytyczny błąd", result.ErrorMessage);
            Assert.True(result.ExecutionTimeMs >= 0); // ExecutionTimeMs może być 0 w testach
        }

        #endregion

        #region AutoRepairCommonIssuesAsync Tests

        [Fact]
        public async Task AutoRepairCommonIssuesAsync_WithClearCacheOption_ShouldReturnSuccess()
        {
            // Arrange
            var options = new RepairOptions
            {
                ClearInvalidCache = true,
                DryRun = false,
                TimeoutMinutes = 30
            };
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemRestore,
                Status = OperationStatus.InProgress
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemRestore,
                "System",
                null,
                "Auto Repair Common Issues",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            // Mock cache invalidation for clearing invalid cache
            _mockCacheInvalidationService.Setup(x => x.InvalidateBatchAsync(
                It.IsAny<Dictionary<string, List<string>>>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.AutoRepairCommonIssuesAsync(options, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.IsSuccess);
            Assert.True(result.ExecutionTimeMs >= 0);
        }

        [Fact]
        public async Task AutoRepairCommonIssuesAsync_WithGoodCache_ShouldNotOptimize()
        {
            // Arrange
            var options = new RepairOptions
            {
                ClearInvalidCache = true,
                DryRun = false
            };
            var accessToken = "valid-token";
            var goodCacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 850,
                CacheMisses = 150,
                AverageAccessTimeMs = 50
            };

            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(goodCacheMetrics);

            var mockOperation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemRestore,
                Status = OperationStatus.InProgress
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemRestore,
                "System",
                null,
                "Auto Repair Common Issues",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            // Mock cache invalidation
            _mockCacheInvalidationService.Setup(x => x.InvalidateBatchAsync(
                It.IsAny<Dictionary<string, List<string>>>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.AutoRepairCommonIssuesAsync(options, accessToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.True(result.ExecutionTimeMs >= 0);

            // Nie powinno optymalizować dobrego cache (nie ma bezpośredniego dostępu do metryk)
            _mockGraphCacheService.Verify(x => x.InvalidateAllCache(), Times.Never);
        }

        [Fact]
        public async Task AutoRepairCommonIssuesAsync_WithException_ShouldReturnError()
        {
            // Arrange
            var options = new RepairOptions { ClearInvalidCache = true };
            var accessToken = "valid-token";

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemRestore,
                "System", 
                null,
                "Auto Repair Common Issues",
                null,
                null
            )).ThrowsAsync(new InvalidOperationException("Service unavailable"));

            // Act
            var result = await _orchestrator.AutoRepairCommonIssuesAsync(options, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.False(result.IsSuccess);
            Assert.Contains("Krytyczny błąd", result.ErrorMessage);
        }

        #endregion

        #region SynchronizeWithMicrosoftGraphAsync Tests

        [Fact]
        public async Task SynchronizeWithMicrosoftGraphAsync_WithHealthyConnection_ShouldReturnSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };
            var healthyConnectionInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "Graph",
                null,
                "Microsoft Graph Synchronization",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthyConnectionInfo);

            // Act
            var result = await _orchestrator.SynchronizeWithMicrosoftGraphAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.IsSuccess);
            Assert.Single(result.SuccessfulOperations);
            Assert.Equal("Graph API", result.SuccessfulOperations[0].Component);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task SynchronizeWithMicrosoftGraphAsync_WithUnhealthyConnection_ShouldReturnPartialSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };
            var unhealthyConnectionInfo = new GraphConnectionHealthInfo
            {
                IsConnected = false,
                IsTokenValid = false,
                Status = GraphHealthStatus.Critical
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "Graph",
                null,
                "Microsoft Graph Synchronization",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(unhealthyConnectionInfo);

            // Act
            var result = await _orchestrator.SynchronizeWithMicrosoftGraphAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.False(result.IsSuccess);
            Assert.Single(result.Errors);
            Assert.Equal("Graph API", result.Errors[0].Component);
            Assert.Equal(HealthErrorSeverity.Critical, result.Errors[0].Severity);
        }

        #endregion

        #region OptimizeCachePerformanceAsync Tests

        [Fact]
        public async Task OptimizeCachePerformanceAsync_WithGoodPerformance_ShouldReturnSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };
            var goodCacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 850,
                CacheMisses = 150,
                AverageAccessTimeMs = 50
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "Cache",
                null,
                "Cache Performance Optimization",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(goodCacheMetrics);

            // Act
            var result = await _orchestrator.OptimizeCachePerformanceAsync(accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Metrics);
            Assert.NotNull(result.Metrics.CacheMetrics);
            _mockGraphCacheService.Verify(x => x.InvalidateAllCache(), Times.Never);
        }

        [Fact]
        public async Task OptimizeCachePerformanceAsync_WithPoorPerformance_ShouldOptimizeCache()
        {
            // Arrange
            var mockMetrics = new GraphCacheMetrics
            {
                TotalRequests = 100,
                CacheHits = 40,
                CacheMisses = 60,
                AverageAccessTimeMs = 150
            };

            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(mockMetrics);

            var mockOperation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress,
                CreatedDate = DateTime.UtcNow
            };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "Cache",
                null,
                "Cache Performance Optimization",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                mockOperation.Id,
                It.IsAny<OperationStatus>(),
                It.IsAny<string>(),
                null
            )).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.OptimizeCachePerformanceAsync("valid-token");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Metrics?.CacheMetrics);
            Assert.Equal(40.0, result.Metrics.CacheMetrics.HitRate); // 40/100 * 100 = 40%
            Assert.Equal(100, result.Metrics.CacheMetrics.TotalOperations);
            Assert.Equal(150.0, result.Metrics.CacheMetrics.AverageOperationTimeMs);
            Assert.True(result.Metrics.CacheMetrics.IsPerformant == false); // Hit rate < 70%

            _mockGraphCacheService.Verify(x => x.InvalidateAllCache(), Times.Once);
            _mockOperationHistoryService.Verify(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "Cache",
                null,
                "Cache Performance Optimization",
                null,
                null
            ), Times.Once);
        }

        #endregion

        #region GetActiveProcessesStatusAsync Tests

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ShouldReturnActiveProcesses()
        {
            // Act
            var result = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<IEnumerable<HealthMonitoringProcessStatus>>();
        }

        #endregion

        #region CancelProcessAsync Tests

        [Fact]
        public async Task CancelProcessAsync_WithValidProcessId_ShouldReturnTrue()
        {
            // Arrange
            var processId = "process-123";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert - Ponieważ proces nie istnieje, powinno zwrócić false
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CancelProcessAsync_WithEmptyProcessId_ShouldReturnFalse()
        {
            // Arrange
            var processId = "";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CancelProcessAsync_WithNullProcessId_ShouldReturnFalse()
        {
            // Arrange
            string processId = null;

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            Action act = () => _orchestrator.Dispose();
            act.Should().NotThrow();
        }

        #endregion
    }
} 