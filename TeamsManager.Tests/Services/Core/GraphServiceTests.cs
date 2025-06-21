using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Services.Graph;
using Xunit;

namespace TeamsManager.Tests.Services.Core
{
    [Collection("Sequential")]
    public class GraphServiceTests
    {
        private readonly Mock<IGraphTeamManagementService> _mockTeamManagementService;
        private readonly Mock<IGraphUserManagementService> _mockUserManagementService;
        private readonly Mock<IGraphBulkOperationsService> _mockBulkOperationsService;
        private readonly Mock<IGraphConnectionService> _mockConnectionService;
        private readonly Mock<IGraphCacheService> _mockCacheService;
        private readonly Mock<ILogger<GraphService>> _mockLogger;
        private readonly GraphService _graphService;

        public GraphServiceTests()
        {
            _mockTeamManagementService = new Mock<IGraphTeamManagementService>();
            _mockUserManagementService = new Mock<IGraphUserManagementService>();
            _mockBulkOperationsService = new Mock<IGraphBulkOperationsService>();
            _mockConnectionService = new Mock<IGraphConnectionService>();
            _mockCacheService = new Mock<IGraphCacheService>();
            _mockLogger = new Mock<ILogger<GraphService>>();

            _graphService = new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                _mockLogger.Object);
        }

        #region Properties Tests

        [Fact]
        public void IsConnected_WhenConnectionServiceReturnsTrue_ShouldReturnTrue()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = _graphService.IsConnected;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsConnected_WhenConnectionServiceReturnsFalse_ShouldReturnFalse()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(false);

            // Act
            var result = _graphService.IsConnected;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Teams_ShouldReturnTeamManagementService()
        {
            // Act
            var result = _graphService.Teams;

            // Assert
            result.Should().Be(_mockTeamManagementService.Object);
        }

        [Fact]
        public void Users_ShouldReturnUserManagementService()
        {
            // Act
            var result = _graphService.Users;

            // Assert
            result.Should().Be(_mockUserManagementService.Object);
        }

        [Fact]
        public void BulkOperations_ShouldReturnBulkOperationsService()
        {
            // Act
            var result = _graphService.BulkOperations;

            // Assert
            result.Should().Be(_mockBulkOperationsService.Object);
        }

        [Fact]
        public void Connection_ShouldReturnConnectionService()
        {
            // Act
            var result = _graphService.Connection;

            // Assert
            result.Should().Be(_mockConnectionService.Object);
        }

        [Fact]
        public void Cache_ShouldReturnCacheService()
        {
            // Act
            var result = _graphService.Cache;

            // Assert
            result.Should().Be(_mockCacheService.Object);
        }

        #endregion

        #region ConnectWithAccessTokenAsync Tests

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithValidToken_ShouldReturnTrue()
        {
            // Arrange
            var accessToken = "valid-token";
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _graphService.ConnectWithAccessTokenAsync(accessToken);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithEmptyToken_ShouldReturnFalse()
        {
            // Arrange
            var accessToken = "";

            // Act
            var result = await _graphService.ConnectWithAccessTokenAsync(accessToken);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithNullToken_ShouldReturnFalse()
        {
            // Arrange
            string? accessToken = null;

            // Act
            var result = await _graphService.ConnectWithAccessTokenAsync(accessToken!);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WhenTokenValidationFails_ShouldReturnFalse()
        {
            // Arrange
            var accessToken = "invalid-token";
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _graphService.ConnectWithAccessTokenAsync(accessToken);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ExecuteWithAutoConnectAsync Tests

        [Fact]
        public async Task ExecuteWithAutoConnectAsync_WithValidTokenAndOperation_ShouldReturnSuccess()
        {
            // Arrange
            var accessToken = "valid-token";
            var expectedResult = "test-result";
            var operation = new Func<Task<string>>(() => Task.FromResult(expectedResult));

            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _graphService.ExecuteWithAutoConnectAsync(accessToken, operation, "test-operation");

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(expectedResult);
        }





        [Fact]
        public async Task ExecuteWithAutoConnectAsync_WhenNotConnected_ShouldTryToConnect()
        {
            // Arrange
            var accessToken = "valid-token";
            var expectedResult = "test-result";
            var operation = new Func<Task<string>>(() => Task.FromResult(expectedResult));

            _mockConnectionService.SetupSequence(x => x.IsTokenValidAsync())
                .ReturnsAsync(false) // Not connected initially
                .ReturnsAsync(true); // Connected after ConnectWithAccessTokenAsync

            // Act
            var result = await _graphService.ExecuteWithAutoConnectAsync(accessToken, operation);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
        }



        #endregion

        #region DiagnoseConnectionAsync Tests

        [Fact]
        public async Task DiagnoseConnectionAsync_WithValidToken_ShouldReturnHealthyStatus()
        {
            // Arrange
            var accessToken = "valid-token";
            var expectedHealthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                ResponseTimeMs = 100,
                LastError = null
            };

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(expectedHealthInfo);

            // Act
            var result = await _graphService.DiagnoseConnectionAsync(accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsConnected.Should().BeTrue();
            result.Status.Should().Be(GraphHealthStatus.Healthy);
            result.ResponseTimeMs.Should().Be(100);
        }



        [Fact]
        public async Task DiagnoseConnectionAsync_WhenConnectionUnhealthy_ShouldReturnCriticalStatus()
        {
            // Arrange
            var accessToken = "valid-token";
            var unhealthyInfo = new GraphConnectionHealthInfo
            {
                IsConnected = false,
                IsTokenValid = false,
                ResponseTimeMs = 5000,
                LastError = "Connection timeout"
            };

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(unhealthyInfo);

            // Act
            var result = await _graphService.DiagnoseConnectionAsync(accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsConnected.Should().BeFalse();
            result.Status.Should().Be(GraphHealthStatus.Critical);
            result.Errors.Should().Contain("Connection timeout");
        }

        #endregion

        #region PerformHealthCheckAsync Tests

        [Fact]
        public async Task PerformHealthCheckAsync_WithValidToken_ShouldReturnHealthyInfo()
        {
            // Arrange
            var accessToken = "valid-token";
            var expectedHealthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                ResponseTimeMs = 150,
                LastError = null
            };

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(expectedHealthInfo);

            // Act
            var result = await _graphService.PerformHealthCheckAsync(accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsHealthy.Should().BeTrue();
            result.ResponseTimeMs.Should().Be(150);
        }



        #endregion

        #region GetPerformanceMetrics Tests

        [Fact]
        public void GetPerformanceMetrics_ShouldReturnMetrics()
        {
            // Act
            var result = _graphService.GetPerformanceMetrics();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<GraphServiceMetrics>();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_ShouldDisposeWithoutException()
        {
            // Act & Assert
            _graphService.Invoking(x => x.Dispose()).Should().NotThrow();
        }

        #endregion
    }
} 