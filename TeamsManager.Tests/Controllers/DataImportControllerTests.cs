using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla DataImportController
    /// Pokrycie: 7 endpointów importu danych, obsługa plików, walidacja, obsługa błędów
    /// </summary>
    public class DataImportControllerTests
    {
        private readonly Mock<IDataImportOrchestrator> _mockOrchestrator;
        private readonly Mock<ILogger<DataImportController>> _mockLogger;
        private readonly DataImportController _controller;

        public DataImportControllerTests()
        {
            _mockOrchestrator = new Mock<IDataImportOrchestrator>();
            _mockLogger = new Mock<ILogger<DataImportController>>();

            _controller = new DataImportController(
                _mockOrchestrator.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DataImportController(
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DataImportController(
                _mockOrchestrator.Object,
                null!));
        }

        #endregion

        #region ImportUsersFromCsv Tests

        [Fact]
        public async Task ImportUsersFromCsv_WithValidFile_ShouldReturnOk()
        {
            // Arrange
            var csvContent = "FirstName;LastName;UPN\nJan;Kowalski;jan.kowalski@test.com";
            var mockFile = CreateMockFormFile("users.csv", csvContent);

            var expectedResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ImportUser", EntityId = "jan.kowalski@test.com" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.ImportUsersFromCsvAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ImportUsersFromCsv(mockFile);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<BulkOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.SuccessfulOperations.Should().HaveCount(1);
            response.SuccessfulOperations.First().EntityId.Should().Be("jan.kowalski@test.com");

            _mockOrchestrator.Verify(x => x.ImportUsersFromCsvAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"), Times.Once);
        }

        [Fact]
        public async Task ImportUsersFromCsv_WithNullFile_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.ImportUsersFromCsv(null!);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie przesłano pliku CSV");
        }

        [Fact]
        public async Task ImportUsersFromCsv_WithEmptyFile_ShouldReturnBadRequest()
        {
            // Arrange
            var emptyFile = CreateMockFormFile("empty.csv", "");

            // Act
            var result = await _controller.ImportUsersFromCsv(emptyFile);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie przesłano pliku CSV");
        }

        [Fact]
        public async Task ImportUsersFromCsv_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var mockFile = CreateMockFormFile("users.csv", "test content");
            _mockOrchestrator.Setup(x => x.ImportUsersFromCsvAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ThrowsAsync(new Exception("Import failed"));

            // Act
            var result = await _controller.ImportUsersFromCsv(mockFile);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas importu danych");
        }

        #endregion

        #region ImportTeamsFromExcel Tests

        [Fact]
        public async Task ImportTeamsFromExcel_WithValidFile_ShouldReturnOk()
        {
            // Arrange
            var excelContent = "Name;Description;TeamType\nTest Team;Test Description;Private";
            var mockFile = CreateMockFormFile("teams.xlsx", excelContent);

            var expectedResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ImportTeam", EntityId = "test-team-id" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.ImportTeamsFromExcelAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ImportTeamsFromExcel(mockFile);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<BulkOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.SuccessfulOperations.Should().HaveCount(1);
            response.SuccessfulOperations.First().Operation.Should().Be("ImportTeam");

            _mockOrchestrator.Verify(x => x.ImportTeamsFromExcelAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"), Times.Once);
        }

        [Fact]
        public async Task ImportTeamsFromExcel_WithNullFile_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.ImportTeamsFromExcel(null!);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie przesłano pliku Excel");
        }

        [Fact]
        public async Task ImportTeamsFromExcel_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var mockFile = CreateMockFormFile("teams.xlsx", "test content");
            _mockOrchestrator.Setup(x => x.ImportTeamsFromExcelAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ThrowsAsync(new Exception("Excel import failed"));

            // Act
            var result = await _controller.ImportTeamsFromExcel(mockFile);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas importu danych");
        }

        #endregion

        #region ImportSchoolStructure Tests

        [Fact]
        public async Task ImportSchoolStructure_WithValidFile_ShouldReturnOk()
        {
            // Arrange
            var structureContent = "Name;Code;Type\nInformatyka;IT;Department";
            var mockFile = CreateMockFormFile("structure.csv", structureContent);

            var expectedResult = new BulkOperationResult
            {
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ImportDepartment", EntityId = "IT" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockOrchestrator.Setup(x => x.ImportSchoolStructureAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.ImportSchoolStructure(mockFile);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<BulkOperationResult>().Subject;

            response.Success.Should().BeTrue();
            response.SuccessfulOperations.Should().HaveCount(1);
            response.SuccessfulOperations.First().EntityId.Should().Be("IT");

            _mockOrchestrator.Verify(x => x.ImportSchoolStructureAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"), Times.Once);
        }

        [Fact]
        public async Task ImportSchoolStructure_WithNullFile_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.ImportSchoolStructure(null!);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie przesłano pliku");
        }

        [Fact]
        public async Task ImportSchoolStructure_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var mockFile = CreateMockFormFile("structure.csv", "test content");
            _mockOrchestrator.Setup(x => x.ImportSchoolStructureAsync(
                It.IsAny<Stream>(), 
                It.IsAny<ImportOptions>(), 
                "token"))
                .ThrowsAsync(new Exception("Structure import failed"));

            // Act
            var result = await _controller.ImportSchoolStructure(mockFile);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas importu danych");
        }

        #endregion

        #region ValidateImportData Tests

        [Fact]
        public async Task ValidateImportData_WithValidFile_ShouldReturnOk()
        {
            // Arrange
            var validationContent = "FirstName;LastName;UPN\nJan;Kowalski;jan.kowalski@test.com";
            var mockFile = CreateMockFormFile("validation.csv", validationContent);
            var dataType = ImportDataType.Users;

            var expectedValidation = new ImportValidationResult
            {
                IsValid = true,
                TotalRecords = 1,
                ValidRecords = 1,
                Errors = new List<ImportValidationError>(),
                Warnings = new List<ImportValidationWarning>(),
                DetectedColumns = new List<string> { "FirstName", "LastName", "UPN" },
                PreviewData = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        { "FirstName", "Jan" },
                        { "LastName", "Kowalski" },
                        { "UPN", "jan.kowalski@test.com" }
                    }
                }
            };

            _mockOrchestrator.Setup(x => x.ValidateImportDataAsync(
                It.IsAny<Stream>(), 
                dataType,
                It.IsAny<ImportOptions>()))
                .ReturnsAsync(expectedValidation);

            // Act
            var result = await _controller.ValidateImportData(mockFile, dataType);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<ImportValidationResult>().Subject;

            response.IsValid.Should().BeTrue();
            response.TotalRecords.Should().Be(1);
            response.ValidRecords.Should().Be(1);
            response.DetectedColumns.Should().Contain("FirstName", "LastName", "UPN");
            response.PreviewData.Should().HaveCount(1);

            _mockOrchestrator.Verify(x => x.ValidateImportDataAsync(
                It.IsAny<Stream>(), 
                dataType,
                It.IsAny<ImportOptions>()), Times.Once);
        }

        [Fact]
        public async Task ValidateImportData_WithInvalidFile_ShouldReturnValidationErrors()
        {
            // Arrange
            var invalidContent = "InvalidColumn;AnotherColumn\nvalue1;value2";
            var mockFile = CreateMockFormFile("invalid.csv", invalidContent);
            var dataType = ImportDataType.Users;

            var expectedValidation = new ImportValidationResult
            {
                IsValid = false,
                TotalRecords = 1,
                ValidRecords = 0,
                Errors = new List<ImportValidationError>
                {
                    new ImportValidationError
                    {
                        RowNumber = 1,
                        ColumnName = "UPN",
                        Message = "Required column 'UPN' is missing",
                        Value = ""
                    }
                },
                Warnings = new List<ImportValidationWarning>(),
                DetectedColumns = new List<string> { "InvalidColumn", "AnotherColumn" }
            };

            _mockOrchestrator.Setup(x => x.ValidateImportDataAsync(
                It.IsAny<Stream>(), 
                dataType,
                It.IsAny<ImportOptions>()))
                .ReturnsAsync(expectedValidation);

            // Act
            var result = await _controller.ValidateImportData(mockFile, dataType);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<ImportValidationResult>().Subject;

            response.IsValid.Should().BeFalse();
            response.Errors.Should().HaveCount(1);
            response.Errors.First().Message.Should().Contain("UPN");
            response.DetectedColumns.Should().Contain("InvalidColumn", "AnotherColumn");
        }

        [Fact]
        public async Task ValidateImportData_WithNullFile_ShouldReturnBadRequest()
        {
            // Act
            var result = await _controller.ValidateImportData(null!, ImportDataType.Users);

            // Assert
            var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().Be("Nie przesłano pliku do walidacji");
        }

        [Fact]
        public async Task ValidateImportData_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var mockFile = CreateMockFormFile("test.csv", "test content");
            _mockOrchestrator.Setup(x => x.ValidateImportDataAsync(
                It.IsAny<Stream>(), 
                ImportDataType.Users,
                It.IsAny<ImportOptions>()))
                .ThrowsAsync(new Exception("Validation failed"));

            // Act
            var result = await _controller.ValidateImportData(mockFile, ImportDataType.Users);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas walidacji danych");
        }

        #endregion

        #region GetActiveImportProcesses Tests

        [Fact]
        public async Task GetActiveImportProcesses_ShouldReturnOkWithProcesses()
        {
            // Arrange
            var processes = new[]
            {
                new ImportProcessStatus
                {
                    ProcessId = "process-1",
                    DataType = ImportDataType.Users,
                    FileName = "users.csv",
                    Status = "Running",
                    TotalRecords = 100,
                    ProcessedRecords = 50,
                    SuccessfulRecords = 45,
                    FailedRecords = 5,
                    StartedAt = DateTime.UtcNow.AddMinutes(-10),
                    FileFormat = ImportFileFormat.CSV,
                    StartedBy = "admin@test.com"
                },
                new ImportProcessStatus
                {
                    ProcessId = "process-2",
                    DataType = ImportDataType.Teams,
                    FileName = "teams.xlsx",
                    Status = "Completed",
                    TotalRecords = 25,
                    ProcessedRecords = 25,
                    SuccessfulRecords = 20,
                    FailedRecords = 5,
                    StartedAt = DateTime.UtcNow.AddMinutes(-30),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-5),
                    FileFormat = ImportFileFormat.Excel,
                    StartedBy = "manager@test.com"
                }
            };

            _mockOrchestrator.Setup(x => x.GetActiveImportProcessesStatusAsync())
                .ReturnsAsync(processes);

            // Act
            var result = await _controller.GetActiveImportProcesses();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProcesses = okResult.Value.Should().BeAssignableTo<IEnumerable<ImportProcessStatus>>().Subject;
            
            var processArray = returnedProcesses.ToArray();
            processArray.Should().HaveCount(2);
            
            processArray[0].ProcessId.Should().Be("process-1");
            processArray[0].DataType.Should().Be(ImportDataType.Users);
            processArray[0].Status.Should().Be("Running");
            processArray[0].ProgressPercentage.Should().Be(50.0);
            
            processArray[1].ProcessId.Should().Be("process-2");
            processArray[1].DataType.Should().Be(ImportDataType.Teams);
            processArray[1].Status.Should().Be("Completed");
            processArray[1].ProgressPercentage.Should().Be(100.0);
        }

        [Fact]
        public async Task GetActiveImportProcesses_WhenNoActiveProcesses_ShouldReturnEmptyList()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveImportProcessesStatusAsync())
                .ReturnsAsync(Array.Empty<ImportProcessStatus>());

            // Act
            var result = await _controller.GetActiveImportProcesses();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedProcesses = okResult.Value.Should().BeAssignableTo<IEnumerable<ImportProcessStatus>>().Subject;
            returnedProcesses.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveImportProcesses_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockOrchestrator.Setup(x => x.GetActiveImportProcessesStatusAsync())
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetActiveImportProcesses();

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas pobierania statusu");
        }

        #endregion

        #region CancelImportProcess Tests

        [Fact]
        public async Task CancelImportProcess_WithValidProcessId_ShouldReturnOkTrue()
        {
            // Arrange
            var processId = "process-123";
            _mockOrchestrator.Setup(x => x.CancelImportProcessAsync(processId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.CancelImportProcess(processId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<bool>().Subject;
            response.Should().BeTrue();

            _mockOrchestrator.Verify(x => x.CancelImportProcessAsync(processId), Times.Once);
        }

        [Fact]
        public async Task CancelImportProcess_WithNonExistentProcessId_ShouldReturnOkFalse()
        {
            // Arrange
            var processId = "non-existent-process";
            _mockOrchestrator.Setup(x => x.CancelImportProcessAsync(processId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.CancelImportProcess(processId);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<bool>().Subject;
            response.Should().BeFalse();
        }

        [Fact]
        public async Task CancelImportProcess_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var processId = "process-123";
            _mockOrchestrator.Setup(x => x.CancelImportProcessAsync(processId))
                .ThrowsAsync(new Exception("Cancellation failed"));

            // Act
            var result = await _controller.CancelImportProcess(processId);

            // Assert
            var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas anulowania procesu");
        }

        #endregion

        #region GenerateImportTemplate Tests

        [Fact]
        public async Task GenerateImportTemplate_WithValidParameters_ShouldReturnFileResult()
        {
            // Arrange
            var dataType = ImportDataType.Users;
            var format = ImportFileFormat.CSV;
            var templateContent = "FirstName;LastName;UPN\n;;";
            var templateStream = new MemoryStream(Encoding.UTF8.GetBytes(templateContent));

            _mockOrchestrator.Setup(x => x.GenerateImportTemplateAsync(dataType, format))
                .ReturnsAsync(templateStream);

            // Act
            var result = await _controller.GenerateImportTemplate(dataType, format);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("text/csv");
            fileResult.FileDownloadName.Should().StartWith("template_Users_");
            fileResult.FileDownloadName.Should().EndWith(".csv");

            _mockOrchestrator.Verify(x => x.GenerateImportTemplateAsync(dataType, format), Times.Once);
        }

        [Fact]
        public async Task GenerateImportTemplate_WithDefaultFormat_ShouldReturnCsvFile()
        {
            // Arrange
            var dataType = ImportDataType.Teams;
            var templateContent = "Name;Description;TeamType\n;;";
            var templateStream = new MemoryStream(Encoding.UTF8.GetBytes(templateContent));

            _mockOrchestrator.Setup(x => x.GenerateImportTemplateAsync(dataType, ImportFileFormat.CSV))
                .ReturnsAsync(templateStream);

            // Act
            var result = await _controller.GenerateImportTemplate(dataType);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("text/csv");
            fileResult.FileDownloadName.Should().StartWith("template_Teams_");

            _mockOrchestrator.Verify(x => x.GenerateImportTemplateAsync(dataType, ImportFileFormat.CSV), Times.Once);
        }

        [Fact]
        public async Task GenerateImportTemplate_WhenOrchestratorThrowsException_ShouldReturnInternalServerError()
        {
            // Arrange
            var dataType = ImportDataType.SchoolStructure;
            _mockOrchestrator.Setup(x => x.GenerateImportTemplateAsync(dataType, ImportFileFormat.CSV))
                .ThrowsAsync(new Exception("Template generation failed"));

            // Act
            var result = await _controller.GenerateImportTemplate(dataType);

            // Assert
            var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
            statusResult.StatusCode.Should().Be(500);
            statusResult.Value.Should().Be("Wystąpił błąd podczas generowania szablonu");
        }

        [Theory]
        [InlineData(ImportDataType.Users)]
        [InlineData(ImportDataType.Teams)]
        [InlineData(ImportDataType.SchoolStructure)]
        [InlineData(ImportDataType.Departments)]
        [InlineData(ImportDataType.Subjects)]
        public async Task GenerateImportTemplate_WithDifferentDataTypes_ShouldReturnCorrectFileName(ImportDataType dataType)
        {
            // Arrange
            var templateStream = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
            _mockOrchestrator.Setup(x => x.GenerateImportTemplateAsync(dataType, ImportFileFormat.CSV))
                .ReturnsAsync(templateStream);

            // Act
            var result = await _controller.GenerateImportTemplate(dataType);

            // Assert
            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.FileDownloadName.Should().StartWith($"template_{dataType}_");
        }

        #endregion

        #region Helper Methods

        private static IFormFile CreateMockFormFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(bytes.Length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            mockFile.Setup(f => f.ContentType).Returns("text/csv");
            
            return mockFile.Object;
        }

        #endregion
    }
} 