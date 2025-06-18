using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Common;
using Microsoft.Identity.Client;

namespace TeamsManager.Tests.Services.Graph
{
    public class GraphConnectionServiceTests : IDisposable
    {
        private readonly Mock<IModernHttpService> _mockHttpService;
        private readonly Mock<IConfidentialClientApplication> _mockConfidentialClientApp;
        private readonly Mock<ILogger<GraphConnectionService>> _mockLogger;
        private readonly ModernCircuitBreaker _circuitBreaker;
        private readonly GraphApiConfiguration _graphConfig;
        private readonly GraphConnectionService _service;

        public GraphConnectionServiceTests()
        {
            _mockHttpService = new Mock<IModernHttpService>();
            _mockConfidentialClientApp = new Mock<IConfidentialClientApplication>();
            _mockLogger = new Mock<ILogger<GraphConnectionService>>();
            _circuitBreaker = new ModernCircuitBreaker(
                failureThreshold: 5, 
                openDuration: TimeSpan.FromMinutes(1),
                logger: null);
            
            _graphConfig = new GraphApiConfiguration
            {
                Endpoints = new GraphEndpoints(),
                Scopes = new GraphScopes()
            };

            _service = new GraphConnectionService(
                _mockHttpService.Object,
                _mockConfidentialClientApp.Object,
                _mockLogger.Object,
                _circuitBreaker,
                _graphConfig);
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_service);
        }

        [Fact]
        public async Task IsTokenValidAsync_WithoutToken_ReturnsFalse()
        {
            // Arrange
            _mockConfidentialClientApp.Setup(x => x.GetAccountsAsync())
                .ReturnsAsync(new List<IAccount>());

            // Act
            var result = await _service.IsTokenValidAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetConnectionHealthAsync_WithoutToken_ReturnsUnhealthyStatus()
        {
            // Arrange
            _mockConfidentialClientApp.Setup(x => x.GetAccountsAsync())
                .ReturnsAsync(new List<IAccount>());

            // Act
            var result = await _service.GetConnectionHealthAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(GraphHealthStatus.Critical, result.Status);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithoutValidToken_ReturnsFalse()
        {
            // Arrange
            _mockConfidentialClientApp.Setup(x => x.AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials))
                .Throws(new MsalServiceException("invalid_client", "Client authentication failed"));

            // Act
            var result = await _service.RefreshTokenAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CheckTokenValidityAsync_WithoutToken_ReturnsFalse()
        {
            // Arrange
            _mockConfidentialClientApp.Setup(x => x.GetAccountsAsync())
                .ReturnsAsync(new List<IAccount>());

            // Act
            var result = await _service.CheckTokenValidityAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EnsureValidTokenAsync_WithoutToken_ReturnsFalse()
        {
            // Arrange
            _mockConfidentialClientApp.Setup(x => x.GetAccountsAsync())
                .ReturnsAsync(new List<IAccount>());
            _mockConfidentialClientApp.Setup(x => x.AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials))
                .Throws(new MsalServiceException("invalid_client", "Client authentication failed"));

            // Act
            var result = await _service.EnsureValidTokenAsync();

            // Assert
            Assert.False(result);
        }

        public void Dispose()
        {
            _circuitBreaker?.Reset();
        }
    }
} 