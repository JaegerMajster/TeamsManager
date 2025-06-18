using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla HealthMonitoringController
    /// Pokrycie: 6 endpointów monitorowania zdrowia, autoryzacja OBO flow, obsługa błędów
    /// </summary>
    public class HealthMonitoringControllerTests
    {
        private readonly Mock<IHealthMonitoringOrchestrator> _mockOrchestrator;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<HealthMonitoringController>> _mockLogger;
        private readonly HealthMonitoringController _controller;

        private const string TestUserUpn = "testuser@test.com";
        private const string ValidApiToken = "api-token-123";
        private const string ValidOboToken = "obo-token-456";
        private const string TestProcessId = "process-789";

        public HealthMonitoringControllerTests()
        {
            _mockOrchestrator = new Mock<IHealthMonitoringOrchestrator>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<HealthMonitoringController>>();

            _controller = new HealthMonitoringController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);

            SetupValidHttpContext();
        }

        private void SetupValidHttpContext()
        {
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Setup default successful behaviors
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns(TestUserUpn);
            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync(TestUserUpn, ValidApiToken))
                .ReturnsAsync(ValidOboToken);
        }

        private void SetupMockHttpContextExtension()
        {
            // Mock HttpContext.GetBearerTokenAsync() behavior
            var httpContext = new DefaultHttpContext();
            httpContext.Items["_TeamsManager_BearerToken_Cache"] = ValidApiToken;
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthMonitoringController(
                null!,
                _mockTokenManager.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullTokenManager_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthMonitoringController(
                _mockOrchestrator.Object,
                null!,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthMonitoringController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new HealthMonitoringController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region RunComprehensiveHealthCheck Tests

        [Fact]
        public async Task RunComprehensiveHealthCheck_WithValidRequest_ShouldReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var healthResult = HealthOperationResult.CreateSuccess("ComprehensiveHealthCheck");
            healthResult.HealthChecks.Add(new HealthCheckDetail
            {
                ComponentName = "Graph API",
                Status = HealthStatus.Healthy,
                Description = "Connection active"
            });
            healthResult.Recommendations.Add("Consider cache cleanup for system optimization");

            _mockOrchestrator.Setup(x => x.RunComprehensiveHealthCheckAsync(ValidOboToken))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _controller.RunComprehensiveHealthCheck();

            // Assert
            result.Should().BeOfType<ActionResult<HealthOperationResult>>();
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.OperationType.Should().Be("ComprehensiveHealthCheck");
            response.HealthChecks.Should().HaveCount(1);
            response.Recommendations.Should().HaveCount(1);

            _mockOrchestrator.Verify(x => x.RunComprehensiveHealthCheckAsync(ValidOboToken), Times.Once);
        }

        [Fact]
        public async Task RunComprehensiveHealthCheck_WithoutUserUpn_ShouldReturnBadRequest()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns((string?)null);

            // Act
            var result = await _controller.RunComprehensiveHealthCheck();

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        [Fact]
        public async Task RunComprehensiveHealthCheck_WithTokenFailure_ShouldReturnBadRequest()
        {
            // Arrange
            SetupMockHttpContextExtension();
            _mockTokenManager.Setup(x => x.GetValidAccessTokenAsync(TestUserUpn, ValidApiToken))
                .ReturnsAsync((string?)null);

            // Act
            var result = await _controller.RunComprehensiveHealthCheck();

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie udało się uzyskać tokenu dostępu");
        }

        [Fact]
        public async Task RunComprehensiveHealthCheck_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            SetupMockHttpContextExtension();
            _mockOrchestrator.Setup(x => x.RunComprehensiveHealthCheckAsync(ValidOboToken))
                .ThrowsAsync(new Exception("Health check failed"));

            // Act
            var result = await _controller.RunComprehensiveHealthCheck();

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Health check failed");
        }

        [Fact]
        public async Task RunComprehensiveHealthCheck_WithProblems_ShouldStillReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var healthResult = HealthOperationResult.CreateError("Some issues found", "ComprehensiveHealthCheck");
            healthResult.Errors.Add(new HealthOperationError
            {
                ComponentName = "Cache",
                Message = "Cache performance degraded",
                Severity = HealthErrorSeverity.Warning
            });

            _mockOrchestrator.Setup(x => x.RunComprehensiveHealthCheckAsync(ValidOboToken))
                .ReturnsAsync(healthResult);

            // Act
            var result = await _controller.RunComprehensiveHealthCheck();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeFalse();
            response.Errors.Should().HaveCount(1);
            response.Errors.First().ComponentName.Should().Be("Cache");
        }

        #endregion

        #region AutoRepairCommonIssues Tests

        [Fact]
        public async Task AutoRepairCommonIssues_WithValidRequest_ShouldReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var repairRequest = new AutoRepairRequest
            {
                RepairGraphConnection = true,
                ClearInvalidCache = true,
                DryRun = false,
                TimeoutMinutes = 15,
                MaxConcurrency = 3
            };

            var repairResult = HealthOperationResult.CreateSuccess("AutoRepair");
            repairResult.ExecutionTimeMs = 5000;
            repairResult.SuccessfulOperations.Add(new HealthOperationSuccess
            {
                ComponentName = "Graph API",
                Operation = "Connection repair",
                Message = "Reconnected successfully"
            });

            _mockOrchestrator.Setup(x => x.AutoRepairCommonIssuesAsync(It.IsAny<RepairOptions>(), ValidOboToken))
                .ReturnsAsync(repairResult);

            // Act
            var result = await _controller.AutoRepairCommonIssues(repairRequest);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.OperationType.Should().Be("AutoRepair");
            response.ExecutionTimeMs.Should().Be(5000);
            response.SuccessfulOperations.Should().HaveCount(1);

            _mockOrchestrator.Verify(x => x.AutoRepairCommonIssuesAsync(
                It.Is<RepairOptions>(o => 
                    o.RepairGraphConnection == true &&
                    o.ClearInvalidCache == true &&
                    o.DryRun == false &&
                    o.TimeoutMinutes == 15 &&
                    o.MaxConcurrency == 3), 
                ValidOboToken), Times.Once);
        }

        [Fact]
        public async Task AutoRepairCommonIssues_WithNullRequest_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.AutoRepairCommonIssues(null!);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Parametry żądania są wymagane");
        }

        [Fact]
        public async Task AutoRepairCommonIssues_WithoutUserUpn_ShouldReturnBadRequest()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns((string?)null);
            var repairRequest = new AutoRepairRequest();

            // Act
            var result = await _controller.AutoRepairCommonIssues(repairRequest);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        [Fact]
        public async Task AutoRepairCommonIssues_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var repairRequest = new AutoRepairRequest();
            _mockOrchestrator.Setup(x => x.AutoRepairCommonIssuesAsync(It.IsAny<RepairOptions>(), ValidOboToken))
                .ThrowsAsync(new Exception("Auto repair failed"));

            // Act
            var result = await _controller.AutoRepairCommonIssues(repairRequest);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Auto repair failed");
        }

        [Fact]
        public async Task AutoRepairCommonIssues_WithDryRun_ShouldPassCorrectOptions()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var repairRequest = new AutoRepairRequest
            {
                DryRun = true,
                OptimizeDatabase = true,
                SendAdminNotifications = false
            };

            var repairResult = HealthOperationResult.CreateSuccess("AutoRepair");
            _mockOrchestrator.Setup(x => x.AutoRepairCommonIssuesAsync(It.IsAny<RepairOptions>(), ValidOboToken))
                .ReturnsAsync(repairResult);

            // Act
            var result = await _controller.AutoRepairCommonIssues(repairRequest);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;

            _mockOrchestrator.Verify(x => x.AutoRepairCommonIssuesAsync(
                It.Is<RepairOptions>(o => 
                    o.DryRun == true &&
                    o.OptimizeDatabase == true &&
                    o.SendAdminNotifications == false), 
                ValidOboToken), Times.Once);
        }

        #endregion

        #region SynchronizeWithMicrosoftGraph Tests

        [Fact]
        public async Task SynchronizeWithMicrosoftGraph_WithValidRequest_ShouldReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var syncResult = HealthOperationResult.CreateSuccess("GraphSynchronization");
            syncResult.ExecutionTimeMs = 3000;
            syncResult.SuccessfulOperations.Add(new HealthOperationSuccess
            {
                ComponentName = "Microsoft Graph",
                Operation = "Synchronization",
                Message = "Data synchronized successfully"
            });

            _mockOrchestrator.Setup(x => x.SynchronizeWithMicrosoftGraphAsync(ValidOboToken))
                .ReturnsAsync(syncResult);

            // Act
            var result = await _controller.SynchronizeWithMicrosoftGraph();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.ExecutionTimeMs.Should().Be(3000);
            response.SuccessfulOperations.Should().HaveCount(1);

            _mockOrchestrator.Verify(x => x.SynchronizeWithMicrosoftGraphAsync(ValidOboToken), Times.Once);
        }

        [Fact]
        public async Task SynchronizeWithMicrosoftGraph_WithoutUserUpn_ShouldReturnBadRequest()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns((string?)null);

            // Act
            var result = await _controller.SynchronizeWithMicrosoftGraph();

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        [Fact]
        public async Task SynchronizeWithMicrosoftGraph_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            SetupMockHttpContextExtension();
            _mockOrchestrator.Setup(x => x.SynchronizeWithMicrosoftGraphAsync(ValidOboToken))
                .ThrowsAsync(new Exception("Graph sync failed"));

            // Act
            var result = await _controller.SynchronizeWithMicrosoftGraph();

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Graph sync failed");
        }

        #endregion

        #region OptimizeCachePerformance Tests

        [Fact]
        public async Task OptimizeCachePerformance_WithValidRequest_ShouldReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var optimizationResult = HealthOperationResult.CreateSuccess("CacheOptimization");
            var cacheMetrics = new Core.Models.Graph.GraphCacheMetrics
            {
                TotalRequests = 1000,
                CacheHits = 855,
                CacheMisses = 145
            };
            optimizationResult.Metrics = new HealthMetrics
            {
                CacheMetrics = cacheMetrics
            };

            _mockOrchestrator.Setup(x => x.OptimizeCachePerformanceAsync(ValidOboToken))
                .ReturnsAsync(optimizationResult);

            // Act
            var result = await _controller.OptimizeCachePerformance();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.Metrics.Should().NotBeNull();
            response.Metrics!.CacheMetrics.Should().NotBeNull();
            response.Metrics.CacheMetrics!.HitRate.Should().Be(85.5);
            response.Metrics.CacheMetrics.IsPerformant.Should().BeTrue();

            _mockOrchestrator.Verify(x => x.OptimizeCachePerformanceAsync(ValidOboToken), Times.Once);
        }

        [Fact]
        public async Task OptimizeCachePerformance_WithoutUserUpn_ShouldReturnBadRequest()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns((string?)null);

            // Act
            var result = await _controller.OptimizeCachePerformance();

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie można określić tożsamości użytkownika");
        }

        [Fact]
        public async Task OptimizeCachePerformance_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            SetupMockHttpContextExtension();
            _mockOrchestrator.Setup(x => x.OptimizeCachePerformanceAsync(ValidOboToken))
                .ThrowsAsync(new Exception("Cache optimization failed"));

            // Act
            var result = await _controller.OptimizeCachePerformance();

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Cache optimization failed");
        }

        [Fact]
        public async Task OptimizeCachePerformance_WithNullCacheMetrics_ShouldStillReturnOk()
        {
            // Arrange
            SetupMockHttpContextExtension();
            var optimizationResult = HealthOperationResult.CreateSuccess("CacheOptimization");
            optimizationResult.Metrics = new HealthMetrics
            {
                CacheMetrics = null
            };

            _mockOrchestrator.Setup(x => x.OptimizeCachePerformanceAsync(ValidOboToken))
                .ReturnsAsync(optimizationResult);

            // Act
            var result = await _controller.OptimizeCachePerformance();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<HealthOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.Metrics!.CacheMetrics.Should().BeNull();
        }

        #endregion

        #region GetActiveProcessesStatus Tests

        [Fact]
        public async Task GetActiveProcessesStatus_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var processes = new[]
            {
                new HealthMonitoringProcessStatus
                {
                    ProcessId = "process-1",
                    OperationType = "HealthCheck",
                    Status = "Running",
                    CurrentOperation = "Checking Graph API",
                    ProgressPercentage = 45.0,
                    ComponentsChecked = 2,
                    TotalComponents = 5,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new HealthMonitoringProcessStatus
                {
                    ProcessId = "process-2",
                    OperationType = "AutoRepair",
                    Status = "Completed",
                    CurrentOperation = "Cleanup finished",
                    ProgressPercentage = 100.0,
                    ComponentsChecked = 3,
                    TotalComponents = 3,
                    IssuesRepaired = 2,
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-1)
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(processes);

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProcesses = okResult.Value.Should().BeAssignableTo<IEnumerable<HealthMonitoringProcessStatus>>().Subject;
            
            returnedProcesses.Should().HaveCount(2);
            var processArray = returnedProcesses.ToArray();
            
            processArray[0].ProcessId.Should().Be("process-1");
            processArray[0].OperationType.Should().Be("HealthCheck");
            processArray[0].ProgressPercentage.Should().Be(45.0);
            
            processArray[1].ProcessId.Should().Be("process-2");
            processArray[1].Status.Should().Be("Completed");
            processArray[1].IssuesRepaired.Should().Be(2);
        }

        [Fact]
        public async Task GetActiveProcessesStatus_WhenNoActiveProcesses_ShouldReturnEmptyList()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(Array.Empty<HealthMonitoringProcessStatus>());

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProcesses = okResult.Value.Should().BeAssignableTo<IEnumerable<HealthMonitoringProcessStatus>>().Subject;
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
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Database error");
        }

        #endregion

        #region CancelProcess Tests

        [Fact]
        public async Task CancelProcess_WithValidProcessId_ShouldReturnOk()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.CancelProcessAsync(TestProcessId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelProcess(TestProcessId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<ProcessCancelResponse>().Subject;

            response.Success.Should().BeTrue();
            response.ProcessId.Should().Be(TestProcessId);
            response.Message.Should().Be("Proces został pomyślnie anulowany");
            response.ProcessType.Should().Be("HealthMonitoring");
        }

        [Fact]
        public async Task CancelProcess_WithNonExistentProcessId_ShouldReturnNotFound()
        {
            // Arrange
            var nonExistentProcessId = "non-existent-process";
            _mockOrchestrator.Setup(x => x.CancelProcessAsync(nonExistentProcessId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CancelProcess(nonExistentProcessId);

            // Assert
            var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var response = notFoundResult.Value.Should().BeAssignableTo<ProcessCancelResponse>().Subject;

            response.Success.Should().BeFalse();
            response.ProcessId.Should().Be(nonExistentProcessId);
            response.Message.Should().Be("Proces nie istnieje lub nie może być anulowany");
            response.ProcessType.Should().Be("HealthMonitoring");
        }

        [Fact]
        public async Task CancelProcess_WithEmptyProcessId_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.CancelProcess("");

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Identyfikator procesu jest wymagany");
        }

        [Fact]
        public async Task CancelProcess_WithWhitespaceProcessId_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.CancelProcess("   ");

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Identyfikator procesu jest wymagany");
        }

        [Fact]
        public async Task CancelProcess_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.CancelProcessAsync(TestProcessId))
                .ThrowsAsync(new Exception("Process cancellation failed"));

            // Act
            var result = await _controller.CancelProcess(TestProcessId);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Błąd wewnętrzny: Process cancellation failed");
        }

        #endregion

        #region Common Authorization Flow Tests

        [Fact]
        public async Task AllEndpoints_WithoutApiToken_ShouldReturnBadRequest()
        {
            // Arrange - No mock setup for token extraction (simulates null token)
            var httpContext = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act & Assert - Test each endpoint that requires tokens
            var healthCheckResult = await _controller.RunComprehensiveHealthCheck();
            healthCheckResult.Result.Should().BeOfType<BadRequestObjectResult>();

            var autoRepairResult = await _controller.AutoRepairCommonIssues(new AutoRepairRequest());
            autoRepairResult.Result.Should().BeOfType<BadRequestObjectResult>();

            var syncResult = await _controller.SynchronizeWithMicrosoftGraph();
            syncResult.Result.Should().BeOfType<BadRequestObjectResult>();

            var cacheResult = await _controller.OptimizeCachePerformance();
            cacheResult.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Theory]
        [InlineData("comprehensive-health-check")]
        [InlineData("auto-repair")]
        [InlineData("graph-synchronization")]
        [InlineData("cache-optimization")]
        public async Task EndpointsRequiringTokens_ShouldLogUserOperations(string operationType)
        {
            // Arrange
            SetupMockHttpContextExtension();
            var healthResult = HealthOperationResult.CreateSuccess(operationType);
            
            _mockOrchestrator.Setup(x => x.RunComprehensiveHealthCheckAsync(ValidOboToken))
                .ReturnsAsync(healthResult);
            _mockOrchestrator.Setup(x => x.AutoRepairCommonIssuesAsync(It.IsAny<RepairOptions>(), ValidOboToken))
                .ReturnsAsync(healthResult);
            _mockOrchestrator.Setup(x => x.SynchronizeWithMicrosoftGraphAsync(ValidOboToken))
                .ReturnsAsync(healthResult);
            _mockOrchestrator.Setup(x => x.OptimizeCachePerformanceAsync(ValidOboToken))
                .ReturnsAsync(healthResult);

            // Act
            IActionResult actualResult = operationType switch
            {
                "comprehensive-health-check" => (await _controller.RunComprehensiveHealthCheck()).Result!,
                "auto-repair" => (await _controller.AutoRepairCommonIssues(new AutoRepairRequest())).Result!,
                "graph-synchronization" => (await _controller.SynchronizeWithMicrosoftGraph()).Result!,
                "cache-optimization" => (await _controller.OptimizeCachePerformance()).Result!,
                _ => throw new ArgumentException("Unknown operation type")
            };

            // Assert
            actualResult.Should().BeOfType<OkObjectResult>();
            _mockCurrentUserService.Verify(x => x.GetCurrentUserUpn(), Times.AtLeastOnce);
        }

        #endregion
    }
} 