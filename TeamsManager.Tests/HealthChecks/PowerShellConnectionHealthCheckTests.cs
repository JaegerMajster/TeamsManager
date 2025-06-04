using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.HealthChecks;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using Xunit;

namespace TeamsManager.Tests.HealthChecks
{
    public class PowerShellConnectionHealthCheckTests
    {
        private readonly Mock<IPowerShellConnectionService> _mockConnectionService;
        private readonly Mock<ILogger<PowerShellConnectionHealthCheck>> _mockLogger;
        private readonly PowerShellConnectionHealthCheck _healthCheck;

        public PowerShellConnectionHealthCheckTests()
        {
            _mockConnectionService = new Mock<IPowerShellConnectionService>();
            _mockLogger = new Mock<ILogger<PowerShellConnectionHealthCheck>>();
            _healthCheck = new PowerShellConnectionHealthCheck(_mockConnectionService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectedAndTokenValid_ShouldReturnHealthy()
        {
            // Arrange
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = true,
                TokenValid = true,
                RunspaceState = "Opened",
                CircuitBreakerState = "Closed",
                LastSuccessfulConnection = DateTime.UtcNow.AddMinutes(-5),
                LastConnectionAttempt = DateTime.UtcNow.AddMinutes(-5)
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Be("PowerShell connection is active and functional");
            result.Data.Should().ContainKey("connected").WhoseValue.Should().Be(true);
            result.Data.Should().ContainKey("tokenValid").WhoseValue.Should().Be(true);
            result.Data.Should().ContainKey("runspaceState").WhoseValue.Should().Be("Opened");
            result.Data.Should().ContainKey("circuitBreakerState").WhoseValue.Should().Be("Closed");
            result.Data.Should().ContainKey("lastSuccessfulConnection");
            result.Data.Should().ContainKey("timestamp");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenNotConnected_ShouldReturnUnhealthy()
        {
            // Arrange
            _mockConnectionService.Setup(s => s.IsConnected).Returns(false);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Be("PowerShell connection is not active");
            result.Data.Should().ContainKey("connected").WhoseValue.Should().Be(false);
            result.Data.Should().ContainKey("timestamp");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectedButTokenInvalid_ShouldReturnDegraded()
        {
            // Arrange
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = true,
                TokenValid = false,
                RunspaceState = "Opened",
                CircuitBreakerState = "Closed",
                LastConnectionAttempt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Degraded);
            result.Description.Should().Contain("PowerShell connection test failed");
            result.Description.Should().Contain("Connected: True, TokenValid: False");
            result.Data.Should().ContainKey("connected").WhoseValue.Should().Be(true);
            result.Data.Should().ContainKey("tokenValid").WhoseValue.Should().Be(false);
            result.Data.Should().ContainKey("lastConnectionAttempt");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenCircuitBreakerOpen_ShouldReturnDegraded()
        {
            // Arrange
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = false,
                TokenValid = true,
                RunspaceState = "Closed",
                CircuitBreakerState = "Open",
                LastConnectionAttempt = DateTime.UtcNow.AddMinutes(-1)
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(false);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Degraded);
            result.Data.Should().ContainKey("circuitBreakerState").WhoseValue.Should().Be("Open");
            result.Data.Should().ContainKey("connected").WhoseValue.Should().Be(false);
            result.Data.Should().ContainKey("tokenValid").WhoseValue.Should().Be(true);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenExceptionThrown_ShouldReturnUnhealthy()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Connection service error");
            _mockConnectionService.Setup(s => s.IsConnected).Throws(expectedException);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Be("PowerShell health check failed");
            result.Exception.Should().Be(expectedException);
            result.Data.Should().ContainKey("error").WhoseValue.Should().Be("Connection service error");
            result.Data.Should().ContainKey("timestamp");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenGetConnectionHealthThrows_ShouldReturnUnhealthy()
        {
            // Arrange
            var expectedException = new TimeoutException("Health check timeout");
            
            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ThrowsAsync(expectedException);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().Be(expectedException);
            result.Data.Should().ContainKey("error").WhoseValue.Should().Be("Health check timeout");
        }

        [Fact]
        public async Task CheckHealthAsync_WithCancellationToken_ShouldRespectCancellation()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync())
                                 .Returns(Task.Delay(1000).ContinueWith(_ => new ConnectionHealthInfo(), cancellationTokenSource.Token));

            var context = new HealthCheckContext();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await _healthCheck.CheckHealthAsync(context, cancellationTokenSource.Token));
        }

        [Fact]
        public async Task CheckHealthAsync_WithAllDataPresent_ShouldIncludeCompleteInformation()
        {
            // Arrange
            var lastSuccessful = DateTime.UtcNow.AddMinutes(-10);
            var lastAttempt = DateTime.UtcNow.AddMinutes(-2);
            
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = true,
                TokenValid = true,
                RunspaceState = "Opened",
                CircuitBreakerState = "Closed",
                LastSuccessfulConnection = lastSuccessful,
                LastConnectionAttempt = lastAttempt
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Data.Should().HaveCount(6); // connected, tokenValid, runspaceState, circuitBreakerState, lastSuccessfulConnection, timestamp
            result.Data["lastSuccessfulConnection"].Should().Be(lastSuccessful);
            
            // Sprawdź że timestamp jest aktualny (w ostatniej sekundzie)
            var timestamp = (DateTime)result.Data["timestamp"];
            timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CheckHealthAsync_WithNullHealthInfo_ShouldHandleGracefully()
        {
            // Arrange
            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync((ConnectionHealthInfo?)null);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().NotBeNull();
        }

        [Fact]
        public async Task CheckHealthAsync_MultipleCallsConcurrently_ShouldBeSafe()
        {
            // Arrange
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = true,
                TokenValid = true,
                RunspaceState = "Opened",
                CircuitBreakerState = "Closed"
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(true);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();
            var tasks = new Task<HealthCheckResult>[5];

            // Act
            for (int i = 0; i < 5; i++)
            {
                tasks[i] = _healthCheck.CheckHealthAsync(context, CancellationToken.None);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(5);
            results.Should().OnlyContain(r => r.Status == HealthStatus.Healthy);
        }

        [Theory]
        [InlineData(true, true, HealthStatus.Healthy)]
        [InlineData(true, false, HealthStatus.Degraded)]
        [InlineData(false, true, HealthStatus.Degraded)]
        [InlineData(false, false, HealthStatus.Degraded)]
        public async Task CheckHealthAsync_WithDifferentConnectionStates_ShouldReturnCorrectStatus(
            bool isConnected, bool tokenValid, HealthStatus expectedStatus)
        {
            // Arrange
            var healthInfo = new ConnectionHealthInfo
            {
                IsConnected = isConnected,
                TokenValid = tokenValid,
                RunspaceState = isConnected ? "Opened" : "Closed",
                CircuitBreakerState = "Closed"
            };

            _mockConnectionService.Setup(s => s.IsConnected).Returns(isConnected);
            _mockConnectionService.Setup(s => s.GetConnectionHealthAsync()).ReturnsAsync(healthInfo);

            var context = new HealthCheckContext();

            // Act
            var result = await _healthCheck.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(expectedStatus);
        }

        [Fact]
        public void Constructor_WithNullConnectionService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PowerShellConnectionHealthCheck(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new PowerShellConnectionHealthCheck(_mockConnectionService.Object, null!));
        }
    }
} 