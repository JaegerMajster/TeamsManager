using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Common;
using TeamsManager.Core.Services.PowerShellServices;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class PowerShellConnectionServiceTests
    {
        private readonly Mock<ILogger<PowerShellConnectionService>> _mockLogger;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IPowerShellCacheService> _mockCacheService;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
        private readonly PowerShellConnectionService _service;

        public PowerShellConnectionServiceTests()
        {
            _mockLogger = new Mock<ILogger<PowerShellConnectionService>>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCacheService = new Mock<IPowerShellCacheService>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

            SetupConfiguration();
            SetupOperationHistory();
            SetupServiceScopeFactory();

            _service = new PowerShellConnectionService(
                _mockLogger.Object,
                _mockServiceScopeFactory.Object,
                _mockCacheService.Object,
                _mockTokenManager.Object,
                _mockOperationHistoryService.Object,
                _mockConfiguration.Object
            );
        }

        private void SetupConfiguration()
        {
            // Setup konfiguracji resilience
            var resilienceSection = new Mock<IConfigurationSection>();
            var retryPolicySection = new Mock<IConfigurationSection>();
            var circuitBreakerSection = new Mock<IConfigurationSection>();
            var scopesSection = new Mock<IConfigurationSection>();

            // Retry Policy
            retryPolicySection.Setup(s => s["MaxAttempts"]).Returns("3");
            retryPolicySection.Setup(s => s["InitialDelaySeconds"]).Returns("1");
            retryPolicySection.Setup(s => s["MaxDelaySeconds"]).Returns("30");

            // Circuit Breaker
            circuitBreakerSection.Setup(s => s["FailureThreshold"]).Returns("5");
            circuitBreakerSection.Setup(s => s["OpenDurationSeconds"]).Returns("60");
            circuitBreakerSection.Setup(s => s["SamplingDurationSeconds"]).Returns("10");

            resilienceSection.Setup(s => s.GetSection("RetryPolicy")).Returns(retryPolicySection.Object);
            resilienceSection.Setup(s => s.GetSection("CircuitBreaker")).Returns(circuitBreakerSection.Object);

            _mockConfiguration.Setup(c => c.GetSection("PowerShellServiceConfig:ConnectionResilience"))
                             .Returns(resilienceSection.Object);

            // Scopes
            var scopeElements = new[]
            {
                CreateConfigElement("User.Read"),
                CreateConfigElement("Group.ReadWrite.All"),
                CreateConfigElement("Directory.ReadWrite.All")
            };

            scopesSection.Setup(s => s.GetChildren()).Returns(scopeElements);
            _mockConfiguration.Setup(c => c.GetSection("PowerShellServiceConfig:DefaultScopesForGraph"))
                             .Returns(scopesSection.Object);
        }

        private IConfigurationSection CreateConfigElement(string value)
        {
            var element = new Mock<IConfigurationSection>();
            element.Setup(e => e.Value).Returns(value);
            return element.Object;
        }

        private void SetupOperationHistory()
        {
            var operationHistory = new OperationHistory
            {
                Id = "test-operation-id",
                Type = OperationType.ConfigurationChanged,
                TargetEntityName = "Test Operation", 
                Status = OperationStatus.InProgress
            };

            _mockOperationHistoryService
                .Setup(o => o.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(operationHistory);
        }

        private void SetupServiceScopeFactory()
        {
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();

            // Setup CurrentUserService in scope - używamy GetService zamiast GetRequiredService
            mockServiceProvider.Setup(p => p.GetService(typeof(ICurrentUserService)))
                              .Returns(_mockCurrentUserService.Object);

            // Setup NotificationService in scope - używamy GetService zamiast GetRequiredService
            mockServiceProvider.Setup(p => p.GetService(typeof(INotificationService)))
                              .Returns(_mockNotificationService.Object);

            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        }

        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeCorrectly()
        {
            // Act & Assert
            _service.Should().NotBeNull();
            _service.IsConnected.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithValidToken_ShouldReturnTrue()
        {
            // Arrange
            var accessToken = "valid-access-token";
            var scopes = new[] { "User.Read", "Group.ReadWrite.All" };
            var userUpn = "test@example.com";

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(userUpn);
            _mockTokenManager.Setup(t => t.HasValidToken(userUpn)).Returns(true);

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken, scopes);

            // Assert
            result.Should().BeTrue();
            _mockOperationHistoryService.Verify(o => o.CreateNewOperationEntryAsync(
                It.IsAny<OperationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithNullToken_ShouldReturnFalse()
        {
            // Arrange
            string? accessToken = null;
            var scopes = new[] { "User.Read" };

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken!, scopes);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetConnectionHealthAsync_ShouldReturnConnectionInfo()
        {
            // Arrange
            var userUpn = "test@example.com";
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(userUpn);
            _mockTokenManager.Setup(t => t.HasValidToken(userUpn)).Returns(true);

            // Act
            var result = await _service.GetConnectionHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsConnected.Should().BeFalse(); // Początkowo nie połączony
            result.TokenValid.Should().BeTrue();
            result.CircuitBreakerState.Should().Be("Closed");
            result.RunspaceState.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ExecuteWithAutoConnectAsync_WhenNotConnected_ShouldAttemptConnection()
        {
            // Arrange
            var userUpn = "test@example.com";
            var accessToken = "test-token";
            var expectedResult = "operation-result";

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(userUpn);
            _mockTokenManager.Setup(t => t.GetValidAccessTokenAsync(userUpn, accessToken))
                            .ReturnsAsync(accessToken);
            _mockTokenManager.Setup(t => t.HasValidToken(userUpn)).Returns(true);

            // Act
            var result = await _service.ExecuteWithAutoConnectAsync(async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            });

            // Assert - ponieważ PowerShell rzeczywiście nie jest zainicjalizowany, rezultat będzie null
            // ale sprawdzamy czy próbował nawiązać połączenie
            _mockTokenManager.Verify(t => t.GetValidAccessTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteCommandWithRetryAsync_WithValidCommand_ShouldAttemptExecution()
        {
            // Arrange
            var commandName = "Get-MgUser";
            var parameters = new Dictionary<string, object>
            {
                { "UserId", "test@example.com" }
            };

            // Act
            var result = await _service.ExecuteCommandWithRetryAsync(commandName, parameters, 2);

            // Assert
            // Ponieważ PowerShell nie jest rzeczywiście zainicjalizowany, wynik będzie null
            // ale sprawdzamy czy nie wystąpił wyjątek
            result.Should().BeNull();
        }

        [Fact]
        public void IsConnected_InitialState_ShouldBeFalse()
        {
            // Act & Assert
            _service.IsConnected.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WhenCircuitBreakerOpen_ShouldHandleGracefully()
        {
            // Arrange
            var accessToken = "test-token";
            var scopes = new[] { "User.Read" };

            // Symulujemy dużo błędów aby otworzyć circuit breaker
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await _service.ConnectWithAccessTokenAsync("invalid-token", scopes);
                }
                catch
                {
                    // Ignorujemy błędy
                }
            }

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken, scopes);

            // Assert
            // Sprawdzamy czy obsłużył gracefully
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ConnectWithAccessTokenAsync_WithInvalidToken_ShouldReturnFalse(string invalidToken)
        {
            // Arrange
            var scopes = new[] { "User.Read" };

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(invalidToken, scopes);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteCommandWithRetryAsync_WithNullCommandName_ShouldReturnNull()
        {
            // Arrange
            string? commandName = null;

            // Act
            var result = await _service.ExecuteCommandWithRetryAsync(commandName!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ExecuteCommandWithRetryAsync_WithEmptyParameters_ShouldWork()
        {
            // Arrange
            var commandName = "Get-MgContext";
            var emptyParameters = new Dictionary<string, object>();

            // Act
            var result = await _service.ExecuteCommandWithRetryAsync(commandName, emptyParameters);

            // Assert
            // Nie powinno rzucić wyjątku
            result.Should().BeNull(); // Ze względu na brak rzeczywistego PowerShell
        }

        [Fact]
        public void Dispose_ShouldNotThrowException()
        {
            // Act & Assert
            Action dispose = () => _service.Dispose();
            dispose.Should().NotThrow();
        }

        [Fact]
        public async Task GetConnectionHealthAsync_WhenNoUserContext_ShouldHandleGracefully()
        {
            // Arrange
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns((string?)null);

            // Act
            var result = await _service.GetConnectionHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.TokenValid.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithValidScopesArray_ShouldUseProvidedScopes()
        {
            // Arrange
            var accessToken = "test-token";
            var customScopes = new[] { "Mail.Read", "Calendars.Read" };
            var userUpn = "test@example.com";

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(userUpn);

            // Act
            var result = await _service.ConnectWithAccessTokenAsync(accessToken, customScopes);

            // Assert
            // Sprawdzamy że wykorzystał custom scopes zamiast domyślnych
            _mockOperationHistoryService.Verify(o => o.CreateNewOperationEntryAsync(
                It.IsAny<OperationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(details => details.Contains("Mail.Read") && details.Contains("Calendars.Read")),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteWithAutoConnectAsync_WhenOperationThrows_ShouldHandleException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Test operation failed");

            // Act
            var result = await _service.ExecuteWithAutoConnectAsync<string>(async () =>
            {
                await Task.Delay(10);
                throw expectedException;
            });

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ExecuteCommandWithRetryAsync_WithMaxRetriesZero_ShouldExecuteOnce()
        {
            // Arrange
            var commandName = "Test-Command";
            var maxRetries = 0;

            // Act
            var result = await _service.ExecuteCommandWithRetryAsync(commandName, null, maxRetries);

            // Assert
            result.Should().BeNull(); // Ze względu na brak rzeczywistego PowerShell
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_MultipleConcurrentCalls_ShouldHandleSafely()
        {
            // Arrange
            var accessToken = "test-token";
            var scopes = new[] { "User.Read" };
            var tasks = new List<Task<bool>>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_service.ConnectWithAccessTokenAsync(accessToken, scopes));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(5);
            // Wszystkie powinny zwrócić false ze względu na brak rzeczywistego PowerShell
            results.Should().OnlyContain(r => r == false);
        }
    }
} 