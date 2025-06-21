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
    /// Testy jednostkowe dla TeamLifecycleController
    /// Pokrycie: 6 endpointów cyklu życia zespołów
    /// </summary>
    public class TeamLifecycleControllerTests
    {
        private readonly Mock<ITeamLifecycleOrchestrator> _mockOrchestrator;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<ILogger<TeamLifecycleController>> _mockLogger;
        private readonly TeamLifecycleController _controller;

        public TeamLifecycleControllerTests()
        {
            _mockOrchestrator = new Mock<ITeamLifecycleOrchestrator>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockLogger = new Mock<ILogger<TeamLifecycleController>>();

            _controller = new TeamLifecycleController(
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
            var controller = new TeamLifecycleController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        #endregion

        #region BulkArchiveTeamsWithCleanup Tests

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithValidRequest_ShouldReturnOkWithResponse()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1", "team-2" },
                Options = new ArchiveOptions { Reason = "End of school year" }
            };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { EntityId = "team-1", Message = "Archived successfully" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.BulkArchiveTeamsWithCleanupAsync(
                request.TeamIds, request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeTrue();
            response.Result.Should().Be(bulkResult);
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithFailedResult_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new ArchiveOptions()
            };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = false,
                ErrorMessage = "Some teams could not be archived",
                Errors = new List<BulkOperationError>
                {
                    new BulkOperationError { EntityId = "team-1", Message = "Archive failed" }
                }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.BulkArchiveTeamsWithCleanupAsync(
                request.TeamIds, request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeFalse();
            response.Message.Should().Be("Some teams could not be archived");
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new ArchiveOptions()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Nie można uzyskać tokenu dostępu do Microsoft Graph API");
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new ArchiveOptions()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.BulkArchiveTeamsWithCleanupAsync(
                It.IsAny<string[]>(), It.IsAny<ArchiveOptions>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            var response = statusCodeResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeFalse();
        }

        #endregion

        #region BulkRestoreTeamsWithValidation Tests

        [Fact]
        public async Task BulkRestoreTeamsWithValidation_WithValidRequest_ShouldReturnOkWithResponse()
        {
            // Arrange
            var request = new BulkRestoreRequest
            {
                TeamIds = new[] { "team-1", "team-2" },
                Options = new RestoreOptions { ValidateOwnerAvailability = true }
            };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { EntityId = "team-1", Message = "Restored successfully" }
                }
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.BulkRestoreTeamsWithValidationAsync(
                request.TeamIds, request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.BulkRestoreTeamsWithValidation(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeTrue();
        }

        [Fact]
        public async Task BulkRestoreTeamsWithValidation_WithFailedResult_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new BulkRestoreRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new RestoreOptions()
            };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = false,
                ErrorMessage = "Restore failed"
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.BulkRestoreTeamsWithValidationAsync(
                request.TeamIds, request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.BulkRestoreTeamsWithValidation(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeFalse();
        }

        #endregion

        #region MigrateTeamsBetweenSchoolYears Tests

        [Fact]
        public async Task MigrateTeamsBetweenSchoolYears_WithValidRequest_ShouldReturnOkWithResponse()
        {
            // Arrange
            var plan = new TeamMigrationPlan
            {
                FromSchoolYearId = "2023-2024",
                ToSchoolYearId = "2024-2025",
                TeamIds = new[] { "team-1", "team-2" },
                ArchiveSourceTeams = true
            };
            var request = new TeamMigrationRequest { Plan = plan };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.MigrateTeamsBetweenSchoolYearsAsync(plan, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.MigrateTeamsBetweenSchoolYears(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeTrue();
        }

        [Fact]
        public async Task MigrateTeamsBetweenSchoolYears_WithFailedResult_ShouldReturnBadRequest()
        {
            // Arrange
            var plan = new TeamMigrationPlan { TeamIds = new[] { "team-1" } };
            var request = new TeamMigrationRequest { Plan = plan };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = false,
                ErrorMessage = "Migration failed"
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.MigrateTeamsBetweenSchoolYearsAsync(plan, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.MigrateTeamsBetweenSchoolYears(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeFalse();
        }

        #endregion

        #region ConsolidateInactiveTeams Tests

        [Fact]
        public async Task ConsolidateInactiveTeams_WithValidRequest_ShouldReturnOkWithResponse()
        {
            // Arrange
            var request = new ConsolidationRequest
            {
                Options = new ConsolidationOptions
                {
                    MinInactiveDays = 90,
                    MaxMembersCount = 5
                }
            };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>()
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.ConsolidateInactiveTeamsAsync(request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.ConsolidateInactiveTeams(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeTrue();
        }

        [Fact]
        public async Task ConsolidateInactiveTeams_WithFailedResult_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ConsolidationRequest { Options = new ConsolidationOptions() };

            var bulkResult = new BulkOperationResult
            {
                IsSuccess = false,
                ErrorMessage = "Consolidation failed"
            };

            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync("test@test.com", "valid-token"))
                .ReturnsAsync("graph-token");

            _mockOrchestrator.Setup(x => x.ConsolidateInactiveTeamsAsync(request.Options, "graph-token"))
                .ReturnsAsync(bulkResult);

            // Act
            var result = await _controller.ConsolidateInactiveTeams(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var response = badRequestResult.Value.Should().BeOfType<BulkOperationResponse>().Subject;
            response.Success.Should().BeFalse();
        }

        #endregion

        #region GetActiveProcessesStatus Tests

        [Fact]
        public async Task GetActiveProcessesStatus_WithActiveProcesses_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var processes = new List<TeamLifecycleProcessStatus>
            {
                new TeamLifecycleProcessStatus
                {
                    ProcessId = "process-1",
                    ProcessType = "BulkArchive",
                    Status = "Running",
                    TotalItems = 10,
                    ProcessedItems = 5
                },
                new TeamLifecycleProcessStatus
                {
                    ProcessId = "process-2",
                    ProcessType = "BulkRestore",
                    Status = "Completed",
                    TotalItems = 20,
                    ProcessedItems = 20
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(processes);

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ProcessStatusResponse>().Subject;
            response.Success.Should().BeTrue();
            response.Processes.Should().HaveCount(2);
            response.Processes.First().ProcessId.Should().Be("process-1");
        }

        [Fact]
        public async Task GetActiveProcessesStatus_WithNoProcesses_ShouldReturnEmptyList()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(new List<TeamLifecycleProcessStatus>());

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ProcessStatusResponse>().Subject;
            response.Success.Should().BeTrue();
            response.Processes.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveProcessesStatus_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
            var response = statusCodeResult.Value.Should().BeOfType<ProcessStatusResponse>().Subject;
            response.Success.Should().BeFalse();
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithoutAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new ArchiveOptions()
            };

            SetupHttpContextWithoutAuth();

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Brak tokenu dostępu w nagłówku Authorization");
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanup_WithoutUpnClaim_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new BulkArchiveRequest
            {
                TeamIds = new[] { "team-1" },
                Options = new ArchiveOptions()
            };

            SetupHttpContextWithoutUpn();

            // Act
            var result = await _controller.BulkArchiveTeamsWithCleanup(request);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
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