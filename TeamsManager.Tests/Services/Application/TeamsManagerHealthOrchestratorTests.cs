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
    /// <summary>
    /// Testy jednostkowe dla TeamsManagerHealthOrchestrator
    /// Alternatywna implementacja IHealthMonitoringOrchestrator skupiona na TeamsManager
    /// </summary>
    public class TeamsManagerHealthOrchestratorTests
    {
        private readonly Mock<IGraphConnectionService> _mockGraphConnectionService;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly Mock<ICacheInvalidationService> _mockCacheInvalidationService;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<TeamsManagerHealthOrchestrator>> _mockLogger;
        private readonly TeamsManagerHealthOrchestrator _orchestrator;

        public TeamsManagerHealthOrchestratorTests()
        {
            _mockGraphConnectionService = new Mock<IGraphConnectionService>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();
            _mockCacheInvalidationService = new Mock<ICacheInvalidationService>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<TeamsManagerHealthOrchestrator>>();

            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("test@test.com");

            _orchestrator = new TeamsManagerHealthOrchestrator(
                _mockGraphConnectionService.Object,
                _mockGraphCacheService.Object,
                _mockCacheInvalidationService.Object,
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_ShouldCreateInstance()
        {
            // Act & Assert
            _orchestrator.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullGraphConnectionService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new TeamsManagerHealthOrchestrator(
                null!,
                _mockGraphCacheService.Object,
                _mockCacheInvalidationService.Object,
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("graphConnectionService");
        }

        #endregion

        #region RunComprehensiveHealthCheckAsync Tests

        [Fact]
        public async Task RunComprehensiveHealthCheckAsync_WithHealthyTeamsManagerSystem_ShouldReturnSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var mockOperation = new OperationHistory 
            { 
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SystemBackup,
                Status = OperationStatus.InProgress
            };

            // Mock all required dependencies for successful health check
            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SystemBackup,
                "TeamsManagerSystem",
                null,
                "TeamsManager Health Check",
                null,
                null
            )).ReturnsAsync(mockOperation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Mock Graph connection as healthy
            var healthyGraphConnection = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy
            };
            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthyGraphConnection);

            // Mock Graph connection diagnosis for CheckGraphConnectionAsync
            var healthyDiagnostics = new GraphDiagnosticInfo
            {
                IsConnected = true,
                Status = GraphHealthStatus.Healthy,
                LastChecked = DateTime.UtcNow,
                Errors = new List<string>()
            };
            _mockGraphConnectionService.Setup(x => x.DiagnoseConnectionAsync())
                .ReturnsAsync(healthyDiagnostics);

            // Mock Graph API test for CheckMicrosoftGraphApiHealthAsync
            // Używam ExpandoObject żeby Count był dostępny przez dynamic casting
            dynamic mockGraphResult = new System.Dynamic.ExpandoObject();
            mockGraphResult.Count = 1;
            _mockGraphConnectionService.Setup(x => x.ExecuteScriptAsync("Get-MgUser -Top 1"))
                .ReturnsAsync((object)mockGraphResult);

            // Mock cache as healthy
            var healthyCacheMetrics = new GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 900,
                CacheMisses = 100,
                AverageAccessTimeMs = 50
            };
            _mockGraphCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(healthyCacheMetrics);

            // Mock notification service for completion notification
            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.RunComprehensiveHealthCheckAsync(accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.ExecutionTimeMs.Should().BeGreaterThan(0);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _orchestrator.Dispose();
            action.Should().NotThrow();
        }

        #endregion
    }
} 