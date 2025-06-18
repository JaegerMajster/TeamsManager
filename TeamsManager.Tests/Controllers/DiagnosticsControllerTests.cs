using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla DiagnosticsController
    /// Pokrycie: 6 najważniejszych endpointów diagnostycznych Graph API
    /// </summary>
    public class DiagnosticsControllerTests
    {
        private readonly Mock<IGraphConnectionService> _mockGraphConnectionService;
        private readonly Mock<IGraphService> _mockGraphService;
        private readonly Mock<ILogger<DiagnosticsController>> _mockLogger;
        private readonly DiagnosticsController _controller;

        public DiagnosticsControllerTests()
        {
            _mockGraphConnectionService = new Mock<IGraphConnectionService>();
            _mockGraphService = new Mock<IGraphService>();
            _mockLogger = new Mock<ILogger<DiagnosticsController>>();

            _controller = new DiagnosticsController(
                _mockGraphConnectionService.Object,
                _mockGraphService.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new DiagnosticsController(
                _mockGraphConnectionService.Object,
                _mockGraphService.Object,
                _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullGraphConnectionService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DiagnosticsController(
                null!,
                _mockGraphService.Object,
                _mockLogger.Object));
        }

        #endregion

        #region DiagnoseConnectionAsync Tests

        [Fact]
        public async Task DiagnoseConnectionAsync_WithValidConnection_ShouldReturnOkWithDiagnosticInfo()
        {
            // Arrange
            var diagnosticInfo = new GraphDiagnosticInfo
            {
                IsConnected = true,
                Status = GraphHealthStatus.Healthy,
                ResponseTimeMs = 100,
                HasRequiredPermissions = true
            };

            _mockGraphConnectionService.Setup(x => x.GetDiagnosticInfoAsync())
                .ReturnsAsync(diagnosticInfo);

            // Act
            var result = await _controller.DiagnoseConnectionAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDiagnostic = okResult.Value.Should().BeOfType<GraphDiagnosticInfo>().Subject;
            returnedDiagnostic.IsConnected.Should().BeTrue();
            returnedDiagnostic.Status.Should().Be(GraphHealthStatus.Healthy);
        }

        [Fact]
        public async Task DiagnoseConnectionAsync_WithConnectionFailure_ShouldReturnOkWithFailedDiagnostic()
        {
            // Arrange
            var diagnosticInfo = new GraphDiagnosticInfo
            {
                IsConnected = false,
                Status = GraphHealthStatus.Critical,
                Errors = new List<string> { "Connection failed" }
            };

            _mockGraphConnectionService.Setup(x => x.GetDiagnosticInfoAsync())
                .ReturnsAsync(diagnosticInfo);

            // Act
            var result = await _controller.DiagnoseConnectionAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedDiagnostic = okResult.Value.Should().BeOfType<GraphDiagnosticInfo>().Subject;
            returnedDiagnostic.IsConnected.Should().BeFalse();
            returnedDiagnostic.Status.Should().Be(GraphHealthStatus.Critical);
        }

        [Fact]
        public async Task DiagnoseConnectionAsync_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockGraphConnectionService.Setup(x => x.GetDiagnosticInfoAsync())
                .ThrowsAsync(new Exception("Connection service error"));

            // Act
            var result = await _controller.DiagnoseConnectionAsync();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region ValidatePermissionsAsync Tests

        [Fact]
        public async Task ValidatePermissionsAsync_WithValidPermissions_ShouldReturnOkWithPermissionInfo()
        {
            // Arrange
            var requiredPermissions = new[] { "User.Read", "Group.Read.All" };
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = true,
                AssignedPermissions = new List<string> { "User.Read", "Group.Read.All", "User.ReadWrite.All" }
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.ValidatePermissionsAsync(requiredPermissions);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedPermissionInfo = okResult.Value.Should().BeOfType<GraphPermissionInfo>().Subject;
            returnedPermissionInfo.HasRequiredPermissions.Should().BeTrue();
        }

        [Fact]
        public async Task ValidatePermissionsAsync_WithMissingPermissions_ShouldReturnOkWithErrorInfo()
        {
            // Arrange
            var requiredPermissions = new[] { "User.Read", "Directory.ReadWrite.All" };
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = false,
                AssignedPermissions = new List<string> { "User.Read" },
                Errors = new List<string>()
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.ValidatePermissionsAsync(requiredPermissions);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedPermissionInfo = okResult.Value.Should().BeOfType<GraphPermissionInfo>().Subject;
            returnedPermissionInfo.HasRequiredPermissions.Should().BeFalse();
            returnedPermissionInfo.Errors.Should().Contain(e => e.Contains("Directory.ReadWrite.All"));
        }

        [Fact]
        public async Task ValidatePermissionsAsync_WithEmptyPermissionsList_ShouldReturnBadRequest()
        {
            // Arrange
            var emptyPermissions = Array.Empty<string>();

            // Act
            var result = await _controller.ValidatePermissionsAsync(emptyPermissions);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        }

        #endregion

        #region GetConnectionHealthAsync Tests

        [Fact]
        public async Task GetConnectionHealthAsync_WithHealthyConnection_ShouldReturnOkWithHealthInfo()
        {
            // Arrange
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy,
                ResponseTimeMs = 150
            };

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);

            // Act
            var result = await _controller.GetConnectionHealthAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedHealthInfo = okResult.Value.Should().BeOfType<GraphConnectionHealthInfo>().Subject;
            returnedHealthInfo.IsConnected.Should().BeTrue();
            returnedHealthInfo.IsTokenValid.Should().BeTrue();
            returnedHealthInfo.Status.Should().Be(GraphHealthStatus.Healthy);
        }

        [Fact]
        public async Task GetConnectionHealthAsync_WithUnhealthyConnection_ShouldReturnOkWithErrorInfo()
        {
            // Arrange
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = false,
                IsTokenValid = false,
                Status = GraphHealthStatus.Critical,
                LastError = "Token expired"
            };

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);

            // Act
            var result = await _controller.GetConnectionHealthAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedHealthInfo = okResult.Value.Should().BeOfType<GraphConnectionHealthInfo>().Subject;
            returnedHealthInfo.IsConnected.Should().BeFalse();
            returnedHealthInfo.Status.Should().Be(GraphHealthStatus.Critical);
        }

        [Fact]
        public async Task GetConnectionHealthAsync_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ThrowsAsync(new Exception("Health check failed"));

            // Act
            var result = await _controller.GetConnectionHealthAsync();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region ValidateUserCreationPermissionsAsync Tests

        [Fact]
        public async Task ValidateUserCreationPermissionsAsync_WithValidPermissions_ShouldReturnOkWithSuccess()
        {
            // Arrange
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = true,
                AssignedPermissions = new List<string> 
                { 
                    "User.ReadWrite.All", 
                    "Directory.ReadWrite.All", 
                    "Group.ReadWrite.All" 
                }
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.ValidateUserCreationPermissionsAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedPermissionInfo = okResult.Value.Should().BeOfType<GraphPermissionInfo>().Subject;
            returnedPermissionInfo.HasRequiredPermissions.Should().BeTrue();
        }

        [Fact]
        public async Task ValidateUserCreationPermissionsAsync_WithMissingPermissions_ShouldReturnOkWithErrors()
        {
            // Arrange
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = false,
                AssignedPermissions = new List<string> { "User.Read" },
                Errors = new List<string>()
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.ValidateUserCreationPermissionsAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedPermissionInfo = okResult.Value.Should().BeOfType<GraphPermissionInfo>().Subject;
            returnedPermissionInfo.HasRequiredPermissions.Should().BeFalse();
            returnedPermissionInfo.Errors.Should().Contain(e => e.Contains("User.ReadWrite.All"));
        }

        #endregion

        #region TestOperationAsync Tests

        [Fact]
        public async Task TestOperationAsync_WithValidOperation_ShouldReturnOkWithCanExecuteTrue()
        {
            // Arrange
            var operationName = "CreateUser";
            var requiredPermissions = new[] { "User.ReadWrite.All" };
            
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = true,
                AssignedPermissions = new List<string> { "User.ReadWrite.All" }
            };

            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy
            };

            var testResult = new GraphConnectionTestResult
            {
                AllTestsPassed = true,
                IsConnected = true,
                IsAuthenticated = true,
                HasRequiredPermissions = true
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);
            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);
            _mockGraphConnectionService.Setup(x => x.TestConnectionAsync())
                .ReturnsAsync(testResult);

            // Act
            var result = await _controller.TestOperationAsync(operationName, requiredPermissions);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            // Use reflection to check dynamic object properties
            var canExecute = response.GetType().GetProperty("CanExecute")?.GetValue(response);
            canExecute.Should().Be(true);
        }

        [Fact]
        public async Task TestOperationAsync_WithEmptyOperationName_ShouldReturnBadRequest()
        {
            // Arrange
            var operationName = "";

            // Act
            var result = await _controller.TestOperationAsync(operationName);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        }

        [Fact]
        public async Task TestOperationAsync_WithInsufficientPermissions_ShouldReturnOkWithCanExecuteFalse()
        {
            // Arrange
            var operationName = "CreateUser";
            var requiredPermissions = new[] { "Directory.ReadWrite.All" };
            
            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = false,
                AssignedPermissions = new List<string> { "User.Read" }
            };

            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.TestOperationAsync(operationName, requiredPermissions);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            // Use reflection to check dynamic object properties
            var canExecute = response.GetType().GetProperty("CanExecute")?.GetValue(response);
            var reason = response.GetType().GetProperty("Reason")?.GetValue(response);
            
            canExecute.Should().Be(false);
            reason.Should().Be("Insufficient permissions");
        }

        #endregion

        #region GetFullDiagnosticReportAsync Tests

        [Fact]
        public async Task GetFullDiagnosticReportAsync_WithValidData_ShouldReturnOkWithCompleteReport()
        {
            // Arrange
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = true,
                IsTokenValid = true,
                Status = GraphHealthStatus.Healthy,
                ResponseTimeMs = 120
            };

            var diagnostic = new GraphDiagnosticInfo
            {
                IsConnected = true,
                Status = GraphHealthStatus.Healthy,
                HasRequiredPermissions = true,
                Errors = new List<string>()
            };

            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = true,
                AssignedPermissions = new List<string> { "User.Read", "Group.Read.All" }
            };

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);
            _mockGraphConnectionService.Setup(x => x.GetDiagnosticInfoAsync())
                .ReturnsAsync(diagnostic);
            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.GetFullDiagnosticReportAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var report = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            // Check that the report contains expected properties
            var overallStatus = report.GetType().GetProperty("OverallStatus")?.GetValue(report);
            overallStatus.Should().Be("Healthy");
        }

        [Fact]
        public async Task GetFullDiagnosticReportAsync_WithCriticalIssues_ShouldReturnOkWithCriticalReport()
        {
            // Arrange
            var healthInfo = new GraphConnectionHealthInfo
            {
                IsConnected = false,
                IsTokenValid = false,
                Status = GraphHealthStatus.Critical
            };

            var diagnostic = new GraphDiagnosticInfo
            {
                IsConnected = false,
                Status = GraphHealthStatus.Critical,
                HasRequiredPermissions = false,
                Errors = new List<string> { "Connection failed", "Token invalid" }
            };

            var permissionInfo = new GraphPermissionInfo
            {
                HasRequiredPermissions = false,
                Errors = new List<string> { "Insufficient permissions" }
            };

            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ReturnsAsync(healthInfo);
            _mockGraphConnectionService.Setup(x => x.GetDiagnosticInfoAsync())
                .ReturnsAsync(diagnostic);
            _mockGraphConnectionService.Setup(x => x.GetPermissionInfoAsync())
                .ReturnsAsync(permissionInfo);

            // Act
            var result = await _controller.GetFullDiagnosticReportAsync();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var report = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            // Check that the report shows critical status
            var overallStatus = report.GetType().GetProperty("OverallStatus")?.GetValue(report);
            overallStatus.Should().Be("Critical");
        }

        [Fact]
        public async Task GetFullDiagnosticReportAsync_WithException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockGraphConnectionService.Setup(x => x.GetConnectionHealthAsync())
                .ThrowsAsync(new Exception("Service unavailable"));

            // Act
            var result = await _controller.GetFullDiagnosticReportAsync();

            // Assert
            var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusCodeResult.StatusCode.Should().Be(500);
        }

        #endregion
    }
}
