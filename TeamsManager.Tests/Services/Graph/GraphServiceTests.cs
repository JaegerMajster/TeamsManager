using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TeamsManager.Core.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Services.Graph
{
    public class GraphServiceTests : IDisposable
    {
        private readonly Mock<IGraphTeamManagementService> _mockTeamManagementService;
        private readonly Mock<IGraphUserManagementService> _mockUserManagementService;
        private readonly Mock<IGraphBulkOperationsService> _mockBulkOperationsService;
        private readonly Mock<IGraphConnectionService> _mockConnectionService;
        private readonly Mock<IGraphCacheService> _mockCacheService;
        private readonly Mock<ILogger<GraphService>> _mockLogger;
        private readonly GraphService _service;

        public GraphServiceTests()
        {
            _mockTeamManagementService = new Mock<IGraphTeamManagementService>();
            _mockUserManagementService = new Mock<IGraphUserManagementService>();
            _mockBulkOperationsService = new Mock<IGraphBulkOperationsService>();
            _mockConnectionService = new Mock<IGraphConnectionService>();
            _mockCacheService = new Mock<IGraphCacheService>();
            _mockLogger = new Mock<ILogger<GraphService>>();

            _service = new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                _mockLogger.Object);

            SetupMockServices();
        }

        private void SetupMockServices()
        {
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(new GraphConnectionHealthInfo
                {
                    IsConnected = true,
                    IsTokenValid = true,
                    ResponseTimeMs = 150
                });

            _mockCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(new GraphCacheMetrics
                {
                    TotalRequests = 100,
                    CacheHits = 80,
                    CacheMisses = 20
                });
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_service);
            Assert.NotNull(_service.Teams);
            Assert.NotNull(_service.Users);
            Assert.NotNull(_service.BulkOperations);
            Assert.NotNull(_service.Connection);
            Assert.NotNull(_service.Cache);
        }

        [Fact]
        public void Constructor_WithNullTeamManagementService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                null,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullUserManagementService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                _mockTeamManagementService.Object,
                null,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullBulkOperationsService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                null,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullConnectionService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                null,
                _mockCacheService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCacheService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                null,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GraphService(
                _mockTeamManagementService.Object,
                _mockUserManagementService.Object,
                _mockBulkOperationsService.Object,
                _mockConnectionService.Object,
                _mockCacheService.Object,
                null));
        }

        #endregion

        #region Properties Tests

        [Fact]
        public void IsConnected_WhenTokenIsValid_ReturnsTrue()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = _service.IsConnected;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsConnected_WhenTokenIsInvalid_ReturnsFalse()
        {
            // Arrange
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(false);

            // Act
            var result = _service.IsConnected;

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Teams_ReturnsTeamManagementService()
        {
            // Act & Assert
            Assert.Same(_mockTeamManagementService.Object, _service.Teams);
        }

        [Fact]
        public void Users_ReturnsUserManagementService()
        {
            // Act & Assert
            Assert.Same(_mockUserManagementService.Object, _service.Users);
        }

        [Fact]
        public void BulkOperations_ReturnsBulkOperationsService()
        {
            // Act & Assert
            Assert.Same(_mockBulkOperationsService.Object, _service.BulkOperations);
        }

        [Fact]
        public void Connection_ReturnsConnectionService()
        {
            // Act & Assert
            Assert.Same(_mockConnectionService.Object, _service.Connection);
        }

        [Fact]
        public void Cache_ReturnsCacheService()
        {
            // Act & Assert
            Assert.Same(_mockCacheService.Object, _service.Cache);
        }

        #endregion

        #region Connection Management Tests

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithValidToken_ReturnsTrue()
        {
            // Arrange
            var accessToken = "valid_token";
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken);

            // Assert
            Assert.True(result);
            _mockConnectionService.Verify(x => x.IsTokenValidAsync(), Times.Once);
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithInvalidToken_ReturnsFalse()
        {
            // Arrange
            var accessToken = "invalid_token";
            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(false);

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithNullToken_ReturnsFalse()
        {
            // Act
            var result = await _service.ConnectWithAccessTokenAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithEmptyToken_ReturnsFalse()
        {
            // Act
            var result = await _service.ConnectWithAccessTokenAsync("");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExecuteWithAutoConnectAsync_WithValidParameters_ReturnsSuccessResult()
        {
            // Arrange
            var apiAccessToken = "valid_token";
            var expectedValue = "test_result";
            var operation = new Func<Task<string>>(() => Task.FromResult(expectedValue));

            _mockConnectionService.Setup(x => x.IsTokenValidAsync())
                .ReturnsAsync(true);

            // Act
            var result = await _service.ExecuteWithAutoConnectAsync(apiAccessToken, operation, "test_operation");

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedValue, result.Data);
            Assert.NotNull(result.ExecutionTimeMs);
        }





        [Fact]
        public async Task ExecuteBatchOperationAsync_WithValidParameters_ReturnsSuccessResult()
        {
            // Arrange
            var apiAccessToken = "valid_token";
            var batchOperations = new[]
            {
                GraphBatchOperation.CreateGet("/v1.0/me", "GetCurrentUser"),
                GraphBatchOperation.CreateGet("/v1.0/users", "GetUsers")
            };

            var mockBatchResponse = new GraphBatchResponse
            {
                Responses = new List<GraphBatchResponseItem>
                {
                    new GraphBatchResponseItem { Id = batchOperations[0].Id, Status = 200 },
                    new GraphBatchResponseItem { Id = batchOperations[1].Id, Status = 200 }
                }
            };

            _mockConnectionService.Setup(x => x.ExecuteBatchRequestAsync(It.IsAny<IEnumerable<GraphBatchRequest>>()))
                .ReturnsAsync(mockBatchResponse);

            // Act
            var result = await _service.ExecuteBatchOperationAsync<object>(apiAccessToken, batchOperations);

            // Assert
            Assert.True(result.IsSuccess);
            _mockConnectionService.Verify(x => 
                x.ExecuteBatchRequestAsync(It.IsAny<IEnumerable<GraphBatchRequest>>()), Times.Once);
        }





        #endregion

        #region Performance & Monitoring Tests

        [Fact]
        public void GetPerformanceMetrics_ReturnsMetrics()
        {
            // Act
            var metrics = _service.GetPerformanceMetrics();

            // Assert
            Assert.NotNull(metrics);
            Assert.True(metrics.TotalRequests >= 0);
            Assert.True(metrics.SuccessfulRequests >= 0);
            Assert.True(metrics.FailedRequests >= 0);
        }

        [Fact]
        public void ResetPerformanceMetrics_ResetsMetrics()
        {
            // Act
            _service.ResetPerformanceMetrics();
            var metrics = _service.GetPerformanceMetrics();

            // Assert
            Assert.Equal(0, metrics.TotalRequests);
            Assert.Equal(0, metrics.SuccessfulRequests);
            Assert.Equal(0, metrics.FailedRequests);
        }

        [Fact]
        public void SetPerformanceMetricsEnabled_UpdatesConfiguration()
        {
            // Act
            _service.SetPerformanceMetricsEnabled(true);
            var config = _service.GetConfiguration();

            // Assert
            Assert.True(config.EnablePerformanceMetrics);

            // Act
            _service.SetPerformanceMetricsEnabled(false);
            config = _service.GetConfiguration();

            // Assert  
            Assert.False(config.EnablePerformanceMetrics);
        }

        #endregion

        #region Cache Management Tests

        [Fact]
        public async Task WarmCacheAsync_WithValidOptions_ReturnsSuccessResult()
        {
            // Arrange
            var options = new GraphCacheWarmupOptions
            {
                Endpoints = new List<string> { "/v1.0/me", "/v1.0/users" }
            };

            _mockCacheService.Setup(x => x.WarmCacheAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<object>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.WarmCacheAsync(options);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(options.Endpoints.Count, result.TotalEndpoints);
            Assert.Equal(options.Endpoints.Count, result.WarmedEndpoints);
        }

        [Fact]
        public async Task WarmCacheAsync_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.WarmCacheAsync(null));
        }

        [Fact]
        public void InvalidateAllCache_CallsCacheService()
        {
            // Act
            _service.InvalidateAllCache();

            // Assert
            _mockCacheService.Verify(x => x.InvalidateAllCache(), Times.Once);
        }

        [Fact]
        public void GetCacheStatus_ReturnsCacheMetrics()
        {
            // Arrange
            var expectedMetrics = new GraphCacheMetrics
            {
                TotalRequests = 50,
                CacheHits = 40,
                CacheMisses = 10
            };

            _mockCacheService.Setup(x => x.GetCacheMetrics())
                .Returns(expectedMetrics);

            // Act
            var result = _service.GetCacheStatus();

            // Assert
            Assert.Equal(expectedMetrics.TotalRequests, result.TotalRequests);
            Assert.Equal(expectedMetrics.CacheHits, result.CacheHits);
        }

        #endregion

        #region Diagnostics & Health Check Tests

        [Fact]
        public async Task DiagnoseConnectionAsync_WithValidToken_ReturnsHealthyDiagnostic()
        {
            // Arrange
            var apiAccessToken = "valid_token";
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                ResponseTimeMs = 120
            };

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);

            // Act
            var result = await _service.DiagnoseConnectionAsync(apiAccessToken);

            // Assert
            Assert.True(result.IsConnected);
            Assert.Equal(GraphHealthStatus.Healthy, result.Status);
            Assert.Equal(120, result.ResponseTimeMs);
            Assert.Equal("v1.0", result.GraphApiVersion);
        }



        [Fact]
        public async Task PerformHealthCheckAsync_WithValidToken_ReturnsHealthyResult()
        {
            // Arrange
            var apiAccessToken = "valid_token";
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                ResponseTimeMs = 100
            };

            _mockConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);

            // Act
            var result = await _service.PerformHealthCheckAsync(apiAccessToken);

            // Assert
            Assert.True(result.IsHealthy);
            Assert.Equal(100, result.ResponseTimeMs);
        }

        [Fact]
        public async Task GetGlobalRateLimitStatusAsync_ReturnsRateLimitStatus()
        {
            // Act
            var result = await _service.GetGlobalRateLimitStatusAsync();

            // Assert
            Assert.False(result.IsLimitReached);
            Assert.Equal(60, result.RetryAfterSeconds);
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public void UpdateConfiguration_WithValidConfiguration_UpdatesConfiguration()
        {
            // Arrange
            var newConfig = new GraphServiceConfiguration
            {
                EnablePerformanceMetrics = true,
                RespectRateLimit = false
            };

            // Act
            _service.UpdateConfiguration(newConfig);
            var result = _service.GetConfiguration();

            // Assert
            Assert.Equal(newConfig.EnablePerformanceMetrics, result.EnablePerformanceMetrics);
            Assert.Equal(newConfig.RespectRateLimit, result.RespectRateLimit);
        }

        [Fact]
        public void GetConfiguration_ReturnsCurrentConfiguration()
        {
            // Act
            var config = _service.GetConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.IsType<GraphServiceConfiguration>(config);
        }

        [Fact]
        public void IsConfigurationValid_WithValidConfiguration_ReturnsTrue()
        {
            // Act
            var isValid = _service.IsConfigurationValid();

            // Assert
            Assert.True(isValid);
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_DisposesService()
        {
            // Act
            _service.Dispose();

            // Assert - No exception should be thrown
            Assert.True(true);
        }

        [Fact]  
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Act
            _service.Dispose();
            _service.Dispose();

            // Assert - No exception should be thrown
            Assert.True(true);
        }

        #endregion

        public void Dispose()
        {
            _service?.Dispose();
        }
    }
} 