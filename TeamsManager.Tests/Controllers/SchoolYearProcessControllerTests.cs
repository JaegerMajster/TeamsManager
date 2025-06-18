using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.API.Controllers;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla SchoolYearProcessController
    /// Pokrycie: 5 endpointów procesów roku szkolnego
    /// </summary>
    public class SchoolYearProcessControllerTests
    {
        private readonly Mock<ISchoolYearProcessOrchestrator> _mockOrchestrator;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<ILogger<SchoolYearProcessController>> _mockLogger;
        private readonly SchoolYearProcessController _controller;

        public SchoolYearProcessControllerTests()
        {
            _mockOrchestrator = new Mock<ISchoolYearProcessOrchestrator>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockLogger = new Mock<ILogger<SchoolYearProcessController>>();

            _controller = new SchoolYearProcessController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockLogger.Object);

            SetupHttpContext();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new SchoolYearProcessController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        #endregion

        #region CreateTeamsForNewSchoolYear Tests

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithValidRequest_ShouldReturnOkWithResult()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1", "template-2" },
                Options = new SchoolYearProcessOptions()
            };

            var expectedResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { EntityId = "team-1", Message = "Team created successfully" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.CreateTeamsForNewSchoolYearAsync(
                request.SchoolYearId, request.TemplateIds, "graph-token", request.Options))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(expectedResult);

            _mockOrchestrator.Verify(x => x.CreateTeamsForNewSchoolYearAsync(
                request.SchoolYearId, request.TemplateIds, "graph-token", request.Options), Times.Once);
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1" }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "invalid",
                TemplateIds = new[] { "template-1" }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.CreateTeamsForNewSchoolYearAsync(
                It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<SchoolYearProcessOptions>()))
                .ThrowsAsync(new ArgumentException("Invalid school year ID"));

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid school year ID");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1" }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.CreateTeamsForNewSchoolYearAsync(
                It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<SchoolYearProcessOptions>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            statusCodeResult.Value.Should().Be("Wystąpił błąd wewnętrzny serwera");
        }

        #endregion

        #region ArchiveTeamsFromPreviousYear Tests

        [Fact]
        public async Task ArchiveTeamsFromPreviousYear_WithValidRequest_ShouldReturnOkWithResult()
        {
            // Arrange
            var request = new ArchiveTeamsRequest
            {
                SchoolYearId = "2023-2024",
                Options = new SchoolYearProcessOptions()
            };

            var expectedResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { EntityId = "team-1", Message = "Team archived successfully" }
                }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.ArchiveTeamsFromPreviousSchoolYearAsync(
                request.SchoolYearId, "graph-token", request.Options))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ArchiveTeamsFromPreviousYear(request);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousYear_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new ArchiveTeamsRequest { SchoolYearId = "2023-2024" };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.ArchiveTeamsFromPreviousYear(request);

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region TransitionToNewSchoolYear Tests

        [Fact]
        public async Task TransitionToNewSchoolYear_WithValidRequest_ShouldReturnOkWithResult()
        {
            // Arrange
            var request = new TransitionToNewSchoolYearRequest
            {
                OldSchoolYearId = "2023-2024",
                NewSchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1", "template-2" },
                Options = new SchoolYearProcessOptions()
            };

            var expectedResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.TransitionToNewSchoolYearAsync(
                request.OldSchoolYearId, request.NewSchoolYearId, request.TemplateIds, "graph-token", request.Options))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.TransitionToNewSchoolYear(request);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(expectedResult);
        }

        [Fact]
        public async Task TransitionToNewSchoolYear_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new TransitionToNewSchoolYearRequest
            {
                OldSchoolYearId = "invalid",
                NewSchoolYearId = "invalid",
                TemplateIds = new[] { "template-1" }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.TransitionToNewSchoolYearAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<SchoolYearProcessOptions>()))
                .ThrowsAsync(new ArgumentException("Invalid transition parameters"));

            // Act
            var result = await _controller.TransitionToNewSchoolYear(request);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Invalid transition parameters");
        }

        #endregion

        #region GetActiveProcesses Tests

        [Fact]
        public async Task GetActiveProcesses_WithActiveProcesses_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var expectedProcesses = new List<SchoolYearProcessStatus>
            {
                new SchoolYearProcessStatus
                {
                    ProcessId = "process-1",
                    ProcessType = "CreateTeams",
                    Status = "InProgress",
                    StartedAt = DateTime.UtcNow,
                    TotalItems = 10,
                    ProcessedItems = 5
                },
                new SchoolYearProcessStatus
                {
                    ProcessId = "process-2",
                    ProcessType = "ArchiveTeams",
                    Status = "Completed",
                    StartedAt = DateTime.UtcNow.AddHours(-1),
                    CompletedAt = DateTime.UtcNow,
                    TotalItems = 20,
                    ProcessedItems = 20
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(expectedProcesses);

            // Act
            var result = await _controller.GetActiveProcesses();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var processes = okResult.Value.Should().BeAssignableTo<IEnumerable<SchoolYearProcessStatus>>().Subject;
            processes.Should().HaveCount(2);
            processes.First().ProcessId.Should().Be("process-1");
        }

        [Fact]
        public async Task GetActiveProcesses_WithNoActiveProcesses_ShouldReturnEmptyList()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(new List<SchoolYearProcessStatus>());

            // Act
            var result = await _controller.GetActiveProcesses();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var processes = okResult.Value.Should().BeAssignableTo<IEnumerable<SchoolYearProcessStatus>>().Subject;
            processes.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveProcesses_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetActiveProcesses();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region CancelProcess Tests

        [Fact]
        public async Task CancelProcess_WithValidProcessId_ShouldReturnOkWithSuccessResponse()
        {
            // Arrange
            var processId = "process-123";

            _mockOrchestrator.Setup(x => x.CancelProcessAsync(processId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelProcess(processId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<CancelProcessResponse>().Subject;
            response.Success.Should().BeTrue();
            response.Message.Should().Be("Proces został anulowany");
        }

        [Fact]
        public async Task CancelProcess_WithNonExistentProcessId_ShouldReturnNotFoundWithFailureResponse()
        {
            // Arrange
            var processId = "nonexistent-process";

            _mockOrchestrator.Setup(x => x.CancelProcessAsync(processId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CancelProcess(processId);

            // Assert
            var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var response = notFoundResult.Value.Should().BeOfType<CancelProcessResponse>().Subject;
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Proces nie istnieje lub już się zakończył");
        }

        [Fact]
        public async Task CancelProcess_WithEmptyProcessId_ShouldReturnBadRequest()
        {
            // Arrange
            var processId = "";

            // Act
            var result = await _controller.CancelProcess(processId);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("ID procesu nie może być pusty");
        }

        [Fact]
        public async Task CancelProcess_WithException_ShouldReturnInternalServerErrorWithFailureResponse()
        {
            // Arrange
            var processId = "process-123";

            _mockOrchestrator.Setup(x => x.CancelProcessAsync(processId))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.CancelProcess(processId);

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            
            var response = statusCodeResult.Value.Should().BeOfType<CancelProcessResponse>().Subject;
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Wystąpił błąd wewnętrzny serwera");
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithoutAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1" }
            };

            SetupHttpContextWithoutAuth();

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Brak tokenu dostępu w nagłówku Authorization");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYear_WithoutUpnClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new CreateTeamsForNewSchoolYearRequest
            {
                SchoolYearId = "2024-2025",
                TemplateIds = new[] { "template-1" }
            };

            SetupHttpContextWithoutUpn();

            // Act
            var result = await _controller.CreateTeamsForNewSchoolYear(request);

            // Assert
            var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        #endregion

        #region Helper Methods

        private void SetupHttpContext()
        {
            var claims = new List<Claim>
            {
                new("upn", "test@test.com"),
                new("preferred_username", "test@test.com")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            httpContext.Request.Headers.Authorization = "Bearer valid-token";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private void SetupHttpContextWithoutAuth()
        {
            var claims = new List<Claim>
            {
                new("upn", "test@test.com")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            // Brak nagłówka Authorization

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private void SetupHttpContextWithoutUpn()
        {
            var claims = new List<Claim>
            {
                new("name", "Test User")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            httpContext.Request.Headers.Authorization = "Bearer valid-token";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #endregion
    }
} 