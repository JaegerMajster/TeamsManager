using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla ReportingController
    /// Pokrycie: 6 endpointów raportowania, generowanie raportów, eksport danych, zarządzanie procesami
    /// </summary>
    public class ReportingControllerTests
    {
        private readonly Mock<IReportingOrchestrator> _mockOrchestrator;
        private readonly Mock<ITokenManager> _mockTokenManager;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<ReportingController>> _mockLogger;
        private readonly ReportingController _controller;

        private const string TestUserUpn = "test.user@contoso.com";

        public ReportingControllerTests()
        {
            _mockOrchestrator = new Mock<IReportingOrchestrator>();
            _mockTokenManager = new Mock<ITokenManager>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<ReportingController>>();

            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns(TestUserUpn);

            _controller = new ReportingController(
                _mockOrchestrator.Object,
                _mockTokenManager.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Act & Assert
            _controller.Should().NotBeNull();
        }

        #endregion

        #region GenerateSchoolYearReport Tests

        [Fact]
        public async Task GenerateSchoolYearReport_WithValidRequest_ShouldReturnFileResult()
        {
            // Arrange
            var schoolYearId = "2024-2025";
            var request = new SchoolYearReportRequest
            {
                SchoolYearId = schoolYearId,
                Options = new ReportOptions
                {
                    Format = ReportFormat.PDF,
                    IncludeDetailedData = true
                }
            };

            var reportContent = "Test PDF content";
            var reportStream = new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
            var expectedResult = ReportOperationResult.CreateSuccess("report-123", "raport_2024-2025.pdf", reportStream);

            _mockOrchestrator.Setup(x => x.GenerateSchoolYearReportAsync(schoolYearId, It.IsAny<ReportOptions>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GenerateSchoolYearReport(request);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be("raport_2024-2025.pdf");

            _mockOrchestrator.Verify(x => x.GenerateSchoolYearReportAsync(schoolYearId, It.IsAny<ReportOptions>()), Times.Once);
        }

        [Fact]
        public async Task GenerateSchoolYearReport_WithExcelFormat_ShouldReturnExcelFile()
        {
            // Arrange
            var request = new SchoolYearReportRequest
            {
                SchoolYearId = "2024-2025",
                Options = new ReportOptions { Format = ReportFormat.Excel }
            };

            var reportStream = new MemoryStream(Encoding.UTF8.GetBytes("Excel content"));
            var expectedResult = ReportOperationResult.CreateSuccess("report-123", "raport_2024-2025.xlsx", reportStream);

            _mockOrchestrator.Setup(x => x.GenerateSchoolYearReportAsync(It.IsAny<string>(), It.IsAny<ReportOptions>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GenerateSchoolYearReport(request);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        [Fact]
        public async Task GenerateSchoolYearReport_WhenReportFails_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new SchoolYearReportRequest { SchoolYearId = "invalid-id" };
            var failedResult = ReportOperationResult.CreateError("Nie znaleziono roku szkolnego");

            _mockOrchestrator.Setup(x => x.GenerateSchoolYearReportAsync(It.IsAny<string>(), It.IsAny<ReportOptions>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = await _controller.GenerateSchoolYearReport(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
            
            var responseValue = badRequestResult.Value.Should().BeAssignableTo<object>().Subject;
            var message = responseValue.GetType().GetProperty("Message")?.GetValue(responseValue)?.ToString();
            message.Should().Be("Nie znaleziono roku szkolnego");
        }

        [Fact]
        public async Task GenerateSchoolYearReport_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new SchoolYearReportRequest { SchoolYearId = "2024-2025" };
            _mockOrchestrator.Setup(x => x.GenerateSchoolYearReportAsync(It.IsAny<string>(), It.IsAny<ReportOptions>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GenerateSchoolYearReport(request);

            // Assert
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region GenerateUserActivityReport Tests

        [Fact]
        public async Task GenerateUserActivityReport_WithValidRequest_ShouldReturnExcelFile()
        {
            // Arrange
            var request = new UserActivityReportRequest
            {
                FromDate = DateTime.Now.AddDays(-30),
                ToDate = DateTime.Now
            };

            var reportContent = "Excel activity report content";
            var reportStream = new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
            var expectedResult = ReportOperationResult.CreateSuccess("activity-123", "raport_aktywnosc.xlsx", reportStream);

            _mockOrchestrator.Setup(x => x.GenerateUserActivityReportAsync(request.FromDate, request.ToDate))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GenerateUserActivityReport(request);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            fileResult.FileDownloadName.Should().Be("raport_aktywnosc.xlsx");

            _mockOrchestrator.Verify(x => x.GenerateUserActivityReportAsync(request.FromDate, request.ToDate), Times.Once);
        }

        [Fact]
        public async Task GenerateUserActivityReport_WhenReportFails_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UserActivityReportRequest
            {
                FromDate = DateTime.Now,
                ToDate = DateTime.Now.AddDays(-1) // Invalid range
            };

            var failedResult = ReportOperationResult.CreateError("Nieprawidłowy zakres dat");

            _mockOrchestrator.Setup(x => x.GenerateUserActivityReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(failedResult);

            // Act
            var result = await _controller.GenerateUserActivityReport(request);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GenerateComplianceReport Tests

        [Fact]
        public async Task GenerateComplianceReport_WithValidRequest_ShouldReturnPdfFile()
        {
            // Arrange
            var request = new ComplianceReportRequest
            {
                Type = ComplianceReportType.DataProtection
            };

            var reportContent = "PDF compliance report content";
            var reportStream = new MemoryStream(Encoding.UTF8.GetBytes(reportContent));
            var expectedResult = ReportOperationResult.CreateSuccess("compliance-123", "raport_compliance.pdf", reportStream);

            _mockOrchestrator.Setup(x => x.GenerateComplianceReportAsync(ComplianceReportType.DataProtection))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.GenerateComplianceReport(request);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("application/pdf");
            fileResult.FileDownloadName.Should().Be("raport_compliance.pdf");

            _mockOrchestrator.Verify(x => x.GenerateComplianceReportAsync(ComplianceReportType.DataProtection), Times.Once);
        }

        #endregion

        #region ExportSystemData Tests

        [Fact]
        public async Task ExportSystemData_WithValidRequest_ShouldReturnFileResult()
        {
            // Arrange
            var request = new SystemDataExportRequest
            {
                Options = new ExportOptions
                {
                    DataType = ExportDataType.Users,
                    Format = ExportFileFormat.Excel,
                    ExcludePersonalData = false
                }
            };

            var exportContent = "Excel export content";
            var exportStream = new MemoryStream(Encoding.UTF8.GetBytes(exportContent));
            var expectedResult = ReportOperationResult.CreateSuccess("export-123", "eksport_users.xlsx", exportStream);

            _mockOrchestrator.Setup(x => x.ExportSystemDataAsync(It.IsAny<ExportOptions>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ExportSystemData(request);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            fileResult.FileDownloadName.Should().Be("eksport_users.xlsx");

            _mockOrchestrator.Verify(x => x.ExportSystemDataAsync(It.IsAny<ExportOptions>()), Times.Once);
        }

        #endregion

        #region GetActiveProcessesStatus Tests

        [Fact]
        public async Task GetActiveProcessesStatus_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var processes = new[]
            {
                new ReportingProcessStatus
                {
                    ProcessId = "process-1",
                    ProcessType = "SchoolYearReport",
                    ReportType = "Raport roku szkolnego",
                    Status = "Running",
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    ProgressPercentage = 50.0,
                    CurrentOperation = "Generowanie danych",
                    StartedBy = TestUserUpn
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveProcessesStatusAsync())
                .ReturnsAsync(processes);

            // Act
            var result = await _controller.GetActiveProcessesStatus();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProcesses = okResult.Value.Should().BeAssignableTo<IEnumerable<ReportingProcessStatus>>().Subject;
            
            var processArray = returnedProcesses.ToArray();
            processArray.Should().HaveCount(1);
            processArray[0].ProcessId.Should().Be("process-1");

            _mockOrchestrator.Verify(x => x.GetActiveProcessesStatusAsync(), Times.Once);
        }

        #endregion
    }
}
