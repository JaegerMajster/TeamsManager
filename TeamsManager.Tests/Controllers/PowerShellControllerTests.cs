using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using System.Threading.Tasks;
using FluentAssertions;
using System.Text.Json;

namespace TeamsManager.Tests.Controllers
{
    public class PowerShellControllerTests
    {
        private readonly Mock<IPowerShellService> _mockPowerShellService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<PowerShellController>> _mockLogger;
        private readonly PowerShellController _controller;

        public PowerShellControllerTests()
        {
            _mockPowerShellService = new Mock<IPowerShellService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<PowerShellController>>();
            
            _controller = new PowerShellController(
                _mockPowerShellService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);

            // Setup default HTTP context
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task TestConnection_WithoutToken_ReturnsBadRequest()
        {
            // Arrange - brak tokenu w nagłówku
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("123");

            // Act
            var result = await _controller.TestConnection();

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            
            var jsonElement = JsonSerializer.SerializeToElement(badRequestResult!.Value);
            var message = jsonElement.GetProperty("Message").GetString();
            var isConnected = jsonElement.GetProperty("IsConnected").GetBoolean();
            var connectionAttempted = jsonElement.GetProperty("ConnectionAttempted").GetBoolean();

            message.Should().Be("Brak tokenu dostępu w nagłówku Authorization");
            isConnected.Should().BeFalse();
            connectionAttempted.Should().BeFalse();
        }

        [Fact]
        public async Task TestConnection_WithValidToken_CallsPowerShellService()
        {
            // Arrange
            _controller.HttpContext.Request.Headers["Authorization"] = "Bearer test-token-123";
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("user-123");
            
            _mockPowerShellService
                .Setup(x => x.ExecuteWithAutoConnectAsync(
                    "test-token-123",
                    It.IsAny<System.Func<Task<bool>>>(),
                    "Test połączenia PowerShell"))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.TestConnection();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var jsonElement = JsonSerializer.SerializeToElement(okResult!.Value);
            var message = jsonElement.GetProperty("Message").GetString();
            var isConnected = jsonElement.GetProperty("IsConnected").GetBoolean();
            var connectionAttempted = jsonElement.GetProperty("ConnectionAttempted").GetBoolean();
            var userUpn = jsonElement.GetProperty("UserUpn").GetString();
            var userId = jsonElement.GetProperty("UserId").GetString();

            message.Should().Contain("Pomyślnie nawiązano połączenie");
            isConnected.Should().BeTrue();
            connectionAttempted.Should().BeTrue();
            userUpn.Should().Be("user@test.com");
            userId.Should().Be("user-123");

            // Verify PowerShell service was called with correct token
            _mockPowerShellService.Verify(x => x.ExecuteWithAutoConnectAsync(
                "test-token-123",
                It.IsAny<System.Func<Task<bool>>>(),
                "Test połączenia PowerShell"), Times.Once);
        }

        [Fact]
        public async Task TestConnection_WithConnectionFailure_ReturnsOkButNotConnected()
        {
            // Arrange
            _controller.HttpContext.Request.Headers["Authorization"] = "Bearer test-token-456";
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("user-456");
            
            _mockPowerShellService
                .Setup(x => x.ExecuteWithAutoConnectAsync(
                    "test-token-456",
                    It.IsAny<System.Func<Task<bool>>>(),
                    "Test połączenia PowerShell"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.TestConnection();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var jsonElement = JsonSerializer.SerializeToElement(okResult!.Value);
            var message = jsonElement.GetProperty("Message").GetString();
            var isConnected = jsonElement.GetProperty("IsConnected").GetBoolean();
            var connectionAttempted = jsonElement.GetProperty("ConnectionAttempted").GetBoolean();

            message.Should().Contain("Nie udało się nawiązać połączenia");
            isConnected.Should().BeFalse();
            connectionAttempted.Should().BeTrue();
        }

        [Fact]
        public async Task TestConnection_CaseInsensitiveBearerToken_Works()
        {
            // Arrange - test case insensitive "bearer" z małą literą
            _controller.HttpContext.Request.Headers["Authorization"] = "bearer test-token-case";
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("user-case");
            
            _mockPowerShellService
                .Setup(x => x.ExecuteWithAutoConnectAsync(
                    "test-token-case",
                    It.IsAny<System.Func<Task<bool>>>(),
                    "Test połączenia PowerShell"))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.TestConnection();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            // Verify token was extracted correctly from case-insensitive header
            _mockPowerShellService.Verify(x => x.ExecuteWithAutoConnectAsync(
                "test-token-case",
                It.IsAny<System.Func<Task<bool>>>(),
                "Test połączenia PowerShell"), Times.Once);
        }

        [Fact]
        public void GetStatus_ReturnsCurrentStatus()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockPowerShellService.Setup(x => x.IsConnected).Returns(true);

            // Act
            var result = _controller.GetStatus();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            
            var jsonElement = JsonSerializer.SerializeToElement(okResult!.Value);
            var isConnected = jsonElement.GetProperty("IsConnected").GetBoolean();
            var userUpn = jsonElement.GetProperty("UserUpn").GetString();

            isConnected.Should().BeTrue();
            userUpn.Should().Be("user@test.com");
        }

        [Fact]
        public async Task TestConnection_WithException_ReturnsInternalServerError()
        {
            // Arrange
            _controller.HttpContext.Request.Headers["Authorization"] = "Bearer test-token";
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("user-123");
            
            _mockPowerShellService
                .Setup(x => x.ExecuteWithAutoConnectAsync(
                    It.IsAny<string>(),
                    It.IsAny<System.Func<Task<bool>>>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new System.Exception("PowerShell connection failed"));

            // Act
            var result = await _controller.TestConnection();

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            
            var jsonElement = JsonSerializer.SerializeToElement(objectResult.Value);
            var message = jsonElement.GetProperty("Message").GetString();
            var isConnected = jsonElement.GetProperty("IsConnected").GetBoolean();
            var connectionAttempted = jsonElement.GetProperty("ConnectionAttempted").GetBoolean();

            message.Should().Be("Błąd podczas testowania połączenia PowerShell");
            isConnected.Should().BeFalse();
            connectionAttempted.Should().BeFalse();
        }
    }
} 