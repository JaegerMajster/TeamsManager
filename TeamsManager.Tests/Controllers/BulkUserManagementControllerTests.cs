using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla BulkUserManagementController
    /// Pokrycie: endpointy masowych operacji na użytkownikach, autoryzacja Bearer token, obsługa błędów
    /// </summary>
    public class BulkUserManagementControllerTests
    {
        private readonly Mock<IBulkUserManagementOrchestrator> _mockOrchestrator;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<ILogger<BulkUserManagementController>> _mockLogger;
        private readonly BulkUserManagementController _controller;

        private const string ValidBearerToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private const string ValidApiAccessToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private const string ValidGraphToken = "graph-access-token-123";
        private const string TestUserUpn = "testuser@test.com";

        public BulkUserManagementControllerTests()
        {
            _mockOrchestrator = new Mock<IBulkUserManagementOrchestrator>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockLogger = new Mock<ILogger<BulkUserManagementController>>();

            _controller = new BulkUserManagementController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockLogger.Object);

            SetupValidHttpContext();
        }

        private void SetupValidHttpContext()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = new StringValues(ValidBearerToken);

            var claims = new List<Claim>
            {
                new Claim("upn", TestUserUpn),
                new Claim("preferred_username", TestUserUpn)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            httpContext.User = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Setup default token manager behavior
            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync(TestUserUpn, ValidApiAccessToken))
                .ReturnsAsync(ValidGraphToken);
        }

        private void SetupHttpContextWithoutAuth()
        {
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        private void SetupHttpContextWithoutClaims()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = new StringValues(ValidBearerToken);
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        #region BulkUserOnboarding Tests

        [Fact]
        public async Task BulkUserOnboarding_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var plans = new[]
            {
                new UserOnboardingPlan
                {
                    FirstName = "John",
                    LastName = "Doe",
                    UPN = "john.doe@test.com",
                    Role = UserRole.Nauczyciel,
                    DepartmentId = "dept-123",
                    Password = "TempPassword123!"
                }
            };

            var request = new BulkUserOnboardingRequest { Plans = plans };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "CreateUser", EntityId = "user-1" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkUserOnboardingAsync(plans, ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value.Should().BeAssignableTo<BulkUserOnboardingResponse>().Subject;

            response.Success.Should().BeTrue();
            response.TotalPlans.Should().Be(1);
            response.SuccessfulOnboardings.Should().Be(1);
            response.FailedOnboardings.Should().Be(0);
            response.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task BulkUserOnboarding_WithoutAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupHttpContextWithoutAuth();
            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().Be("Brak tokenu dostępu w nagłówku Authorization");
        }

        [Fact]
        public async Task BulkUserOnboarding_WithoutUserClaims_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupHttpContextWithoutClaims();
            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        [Fact]
        public async Task BulkUserOnboarding_WhenTokenManagerFails_ShouldReturnUnauthorized()
        {
            // Arrange
            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync(TestUserUpn, ValidApiAccessToken))
                .ReturnsAsync((string?)null);

            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result as UnauthorizedObjectResult;
            unauthorizedResult!.Value.Should().Be("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
        }

        [Fact]
        public async Task BulkUserOnboarding_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            _mockOrchestrator.Setup(x => x.BulkUserOnboardingAsync(It.IsAny<UserOnboardingPlan[]>(), ValidGraphToken))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Wystąpił błąd podczas masowego onboardingu użytkowników");
        }

        [Fact]
        public async Task BulkUserOnboarding_WithPartialFailures_ShouldReturnOkWithErrors()
        {
            // Arrange
            var plans = new[]
            {
                new UserOnboardingPlan { FirstName = "John", LastName = "Doe", UPN = "john@test.com" },
                new UserOnboardingPlan { FirstName = "Jane", LastName = "Smith", UPN = "jane@test.com" }
            };

            var request = new BulkUserOnboardingRequest { Plans = plans };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "CreateUser", EntityId = "user-1" }
                },
                Errors = new List<BulkOperationError> 
                { 
                    new BulkOperationError { Operation = "CreateUser", EntityId = "user-2", Message = "Failed to onboard jane@test.com" }
                }
            };

            _mockOrchestrator.Setup(x => x.BulkUserOnboardingAsync(plans, ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value.Should().BeAssignableTo<BulkUserOnboardingResponse>().Subject;

            response.Success.Should().BeTrue();
            response.TotalPlans.Should().Be(2);
            response.SuccessfulOnboardings.Should().Be(1);
            response.FailedOnboardings.Should().Be(1);
            response.Errors.Should().HaveCount(1);
            response.Errors.First().Should().Be("Failed to onboard jane@test.com");
        }

        #endregion

        #region BulkUserOffboarding Tests

        [Fact]
        public async Task BulkUserOffboarding_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var userIds = new[] { "user-1", "user-2" };
            var options = new OffboardingOptions { BatchSize = 10 };
            var request = new BulkUserOffboardingRequest { UserIds = userIds, Options = options };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "OffboardUser", EntityId = "user-1" },
                    new BulkOperationSuccess { Operation = "OffboardUser", EntityId = "user-2" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkUserOffboardingAsync(userIds, options, ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkUserOffboarding(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value.Should().BeAssignableTo<BulkUserOffboardingResponse>().Subject;

            response.Success.Should().BeTrue();
            response.TotalUsers.Should().Be(2);
            response.SuccessfulOffboardings.Should().Be(2);
            response.FailedOffboardings.Should().Be(0);
        }

        [Fact]
        public async Task BulkUserOffboarding_WithNullOptions_ShouldUseDefaultOptions()
        {
            // Arrange
            var userIds = new[] { "user-1" };
            var request = new BulkUserOffboardingRequest { UserIds = userIds, Options = null };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "OffboardUser", EntityId = "user-1" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkUserOffboardingAsync(userIds, It.IsAny<OffboardingOptions>(), ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkUserOffboarding(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOrchestrator.Verify(x => x.BulkUserOffboardingAsync(
                userIds, 
                It.Is<OffboardingOptions>(o => o.BatchSize == 20), // Default value
                ValidGraphToken), Times.Once);
        }

        #endregion

        #region BulkRoleChange Tests

        [Fact]
        public async Task BulkRoleChange_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var changes = new[]
            {
                new UserRoleChange
                {
                    UserId = "user-1",
                    CurrentRole = UserRole.Nauczyciel,
                    NewRole = UserRole.Wicedyrektor,
                    Reason = "Promotion"
                }
            };

            var request = new BulkRoleChangeRequest { Changes = changes };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "ChangeRole", EntityId = "user-1" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkRoleChangeAsync(changes, ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkRoleChange(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value.Should().BeAssignableTo<BulkRoleChangeResponse>().Subject;

            response.Success.Should().BeTrue();
            response.TotalChanges.Should().Be(1);
            response.SuccessfulChanges.Should().Be(1);
            response.FailedChanges.Should().Be(0);
        }

        [Fact]
        public async Task BulkRoleChange_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new BulkRoleChangeRequest { Changes = Array.Empty<UserRoleChange>() };

            _mockOrchestrator.Setup(x => x.BulkRoleChangeAsync(It.IsAny<UserRoleChange[]>(), ValidGraphToken))
                .ThrowsAsync(new Exception("Role change failed"));

            // Act
            var result = await _controller.BulkRoleChange(request);

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Wystąpił błąd podczas masowej zmiany ról użytkowników");
        }

        #endregion

        #region BulkTeamMembershipOperation Tests

        [Fact]
        public async Task BulkTeamMembershipOperation_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var operations = new[]
            {
                new TeamMembershipOperation
                {
                    OperationType = TeamMembershipOperationType.Add,
                    UserId = "user-1",
                    TeamId = "team-1",
                    Role = TeamMemberRole.Member,
                    Reason = "New assignment"
                }
            };

            var request = new BulkTeamMembershipRequest { Operations = operations };

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess> 
                { 
                    new BulkOperationSuccess { Operation = "AddTeamMember", EntityId = "user-1" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkTeamMembershipOperationAsync(operations, ValidGraphToken))
                .ReturnsAsync(operationResult);

            // Act
            var result = await _controller.BulkTeamMembershipOperation(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var response = okResult!.Value.Should().BeAssignableTo<BulkTeamMembershipResponse>().Subject;

            response.Success.Should().BeTrue();
            response.TotalOperations.Should().Be(1);
            response.SuccessfulOperations.Should().Be(1);
            response.FailedOperations.Should().Be(0);
        }

        #endregion

        #region GetActiveProcessesStatus Tests

        [Fact]
        public async Task GetActiveProcessesStatus_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var processes = new[]
            {
                new UserManagementProcessStatus
                {
                    ProcessId = "process-1",
                    ProcessType = "BulkOnboarding",
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    Status = "Running",
                    TotalItems = 10,
                    ProcessedItems = 5
                },
                new UserManagementProcessStatus
                {
                    ProcessId = "process-2",
                    ProcessType = "BulkOffboarding",
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-1),
                    Status = "Completed",
                    TotalItems = 5,
                    ProcessedItems = 5
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(processes);

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var returnedProcesses = okResult!.Value.Should().BeAssignableTo<IEnumerable<UserManagementProcessStatus>>().Subject;
            returnedProcesses.Should().HaveCount(2);
            returnedProcesses.Should().BeEquivalentTo(processes);
        }

        [Fact]
        public async Task GetActiveProcessesStatus_WhenNoActiveProcesses_ShouldReturnEmptyList()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(Array.Empty<UserManagementProcessStatus>());

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var returnedProcesses = okResult!.Value.Should().BeAssignableTo<IEnumerable<UserManagementProcessStatus>>().Subject;
            returnedProcesses.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveProcessesStatus_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            result.Should().BeOfType<ObjectResult>();
            var objectResult = result as ObjectResult;
            objectResult!.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Wystąpił błąd podczas pobierania statusu procesów");
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BulkUserManagementController(
                null!,
                _mockTokenManager.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullTokenManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BulkUserManagementController(
                _mockOrchestrator.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BulkUserManagementController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                null!));
        }

        #endregion

        #region Authorization Token Extraction Tests

        [Theory]
        [InlineData("Bearer token123", "token123")]
        [InlineData("bearer token456", "token456")]
        [InlineData("BEARER token789", "token789")]
        public async Task TokenExtraction_VariousFormats_ShouldExtractCorrectly(string authHeader, string expectedToken)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = new StringValues(authHeader);

            var claims = new List<Claim> { new Claim("upn", TestUserUpn) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            httpContext.User = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync(TestUserUpn, expectedToken))
                .ReturnsAsync(ValidGraphToken);

            var operationResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess>(),
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.BulkUserOnboardingAsync(It.IsAny<UserOnboardingPlan[]>(), ValidGraphToken))
                .ReturnsAsync(operationResult);

            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockTokenManager.Verify(x => x.GetValidAccessTokenAsync(TestUserUpn, expectedToken), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Basic dXNlcjpwYXNzd29yZA==")]
        [InlineData("InvalidTokenFormat")]
        public async Task TokenExtraction_InvalidFormats_ShouldReturnUnauthorized(string authHeader)
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = new StringValues(authHeader);

            var claims = new List<Claim> { new Claim("upn", TestUserUpn) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            httpContext.User = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };

            var request = new BulkUserOnboardingRequest { Plans = Array.Empty<UserOnboardingPlan>() };

            // Act
            var result = await _controller.BulkUserOnboarding(request);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion
    }
} 