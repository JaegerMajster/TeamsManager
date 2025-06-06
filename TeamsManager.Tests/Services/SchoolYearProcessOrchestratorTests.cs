using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;

namespace TeamsManager.Tests.Services
{
    /// <summary>
    /// Testy jednostkowe dla orkiestratora procesów szkolnych
    /// </summary>
    public class SchoolYearProcessOrchestratorTests
    {
        private readonly Mock<ISchoolYearService> _mockSchoolYearService;
        private readonly Mock<ITeamTemplateService> _mockTeamTemplateService;
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<IPowerShellBulkOperationsService> _mockBulkOperationsService;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<IDepartmentService> _mockDepartmentService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<SchoolYearProcessOrchestrator>> _mockLogger;
        private readonly SchoolYearProcessOrchestrator _orchestrator;

        public SchoolYearProcessOrchestratorTests()
        {
            _mockSchoolYearService = new Mock<ISchoolYearService>();
            _mockTeamTemplateService = new Mock<ITeamTemplateService>();
            _mockTeamService = new Mock<ITeamService>();
            _mockBulkOperationsService = new Mock<IPowerShellBulkOperationsService>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockDepartmentService = new Mock<IDepartmentService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<SchoolYearProcessOrchestrator>>();

            _orchestrator = new SchoolYearProcessOrchestrator(
                _mockSchoolYearService.Object,
                _mockTeamTemplateService.Object,
                _mockTeamService.Object,
                _mockBulkOperationsService.Object,
                _mockOperationHistoryService.Object,
                _mockDepartmentService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithValidInput_ShouldReturnSuccessResult()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "template-1", "template-2" };
            var accessToken = "test-token";

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            var templates = new List<TeamTemplate>
            {
                new TeamTemplate
                {
                    Id = "template-1",
                    Name = "Szablon Klasy",
                    NamePattern = "{Class} - {Year}",
                    Description = "Szablon dla klas",
                    TeamType = "class",
                    Privacy = "Private"
                },
                new TeamTemplate
                {
                    Id = "template-2",
                    Name = "Szablon Przedmiotu",
                    NamePattern = "{Subject} - {Class} - {Year}",
                    Description = "Szablon dla przedmiotów",
                    TeamType = "subject",
                    Privacy = "Private"
                }
            };

            var departments = new List<Department>
            {
                new Department { Id = "dept-1", Name = "Klasa 1A" },
                new Department { Id = "dept-2", Name = "Klasa 1B" }
            };

            var successfulBulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "CreateTeam", EntityId = "team-1", Message = "Sukces" },
                    new BulkOperationSuccess { Operation = "CreateTeam", EntityId = "team-2", Message = "Sukces" }
                },
                Errors = new List<BulkOperationError>()
            };

            // Setup mocks
            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(templates[0]);
            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-2"))
                .ReturnsAsync(templates[1]);

            _mockDepartmentService.Setup(s => s.GetAllAsync())
                .ReturnsAsync(departments);

            _mockTeamService.Setup(s => s.CreateAsync(It.IsAny<Team>()))
                .ReturnsAsync(new Team { Id = Guid.NewGuid().ToString() });

            _mockBulkOperationsService.Setup(s => s.CreateTeamsAsync(It.IsAny<string[]>(), accessToken))
                .ReturnsAsync(successfulBulkResult);

            _mockOperationHistoryService.Setup(s => s.CreateAsync(It.IsAny<OperationHistory>()))
                .ReturnsAsync(new OperationHistory());

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessfulOperations.Count);
            Assert.Empty(result.Errors);

            // Verify service calls
            _mockSchoolYearService.Verify(s => s.GetByIdAsync(schoolYearId), Times.Once);
            _mockTeamTemplateService.Verify(s => s.GetByIdAsync(It.IsAny<string>()), Times.Exactly(2));
            _mockDepartmentService.Verify(s => s.GetAllAsync(), Times.Once);
            _mockOperationHistoryService.Verify(s => s.CreateAsync(It.IsAny<OperationHistory>()), Times.Once);
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithInvalidSchoolYear_ShouldThrowArgumentException()
        {
            // Arrange
            var schoolYearId = "invalid-school-year";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync((SchoolYear?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken));
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithNoValidTemplates_ShouldThrowArgumentException()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "invalid-template-1", "invalid-template-2" };
            var accessToken = "test-token";

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((TeamTemplate?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken));
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithDryRunOption_ShouldSimulateOperations()
        {
            // Arrange
            var schoolYearId = "school-year-1";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";
            var options = new SchoolYearProcessOptions { DryRun = true };

            var schoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "2024/2025",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(10)
            };

            var template = new TeamTemplate
            {
                Id = "template-1",
                Name = "Szablon Testowy",
                NamePattern = "{Class} - {Year}",
                TeamType = "class"
            };

            var departments = new List<Department>
            {
                new Department { Id = "dept-1", Name = "Klasa 1A" }
            };

            _mockSchoolYearService.Setup(s => s.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);

            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(template);

            _mockDepartmentService.Setup(s => s.GetAllAsync())
                .ReturnsAsync(departments);

            _mockOperationHistoryService.Setup(s => s.CreateAsync(It.IsAny<OperationHistory>()))
                .ReturnsAsync(new OperationHistory());

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(schoolYearId, templateIds, accessToken, options);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.True(result.SuccessfulOperations.Any(op => op.Message.Contains("DRY RUN")));

            // Verify that actual team creation was NOT called (only simulation)
            _mockTeamService.Verify(s => s.CreateAsync(It.IsAny<Team>()), Times.Never);
            _mockBulkOperationsService.Verify(s => s.CreateTeamsAsync(It.IsAny<string[]>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_WithValidInput_ShouldReturnSuccessResult()
        {
            // Arrange
            var schoolYearId = "old-school-year";
            var accessToken = "test-token";

            var teamsToArchive = new List<Team>
            {
                new Team { Id = "team-1", Name = "Zespół 1" },
                new Team { Id = "team-2", Name = "Zespół 2" }
            };

            var successfulBulkResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "ArchiveTeam", EntityId = "team-1" },
                    new BulkOperationSuccess { Operation = "ArchiveTeam", EntityId = "team-2" }
                },
                Errors = new List<BulkOperationError>()
            };

            _mockTeamService.Setup(s => s.GetTeamsBySchoolYearAsync(schoolYearId))
                .ReturnsAsync(teamsToArchive);

            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), accessToken, It.IsAny<int>()))
                .ReturnsAsync(successfulBulkResult);

            _mockOperationHistoryService.Setup(s => s.CreateAsync(It.IsAny<OperationHistory>()))
                .ReturnsAsync(new OperationHistory());

            // Act
            var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(schoolYearId, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessfulOperations.Count);
            Assert.Empty(result.Errors);

            _mockTeamService.Verify(s => s.GetTeamsBySchoolYearAsync(schoolYearId), Times.Once);
            _mockBulkOperationsService.Verify(s => s.ArchiveTeamsAsync(
                It.Is<string[]>(ids => ids.Length == 2 && ids.Contains("team-1") && ids.Contains("team-2")),
                accessToken,
                It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task TransitionToNewSchoolYearAsync_WithValidInput_ShouldArchiveAndCreateTeams()
        {
            // Arrange
            var oldSchoolYearId = "old-year";
            var newSchoolYearId = "new-year";
            var templateIds = new[] { "template-1" };
            var accessToken = "test-token";

            var archiveResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "Archive", EntityId = "team-old" }
                }
            };

            var createResult = new BulkOperationResult
            {
                IsSuccess = true,
                SuccessfulOperations = new List<BulkOperationSuccess>
                {
                    new BulkOperationSuccess { Operation = "Create", EntityId = "team-new" }
                }
            };

            // Setup mocks for archive process
            _mockTeamService.Setup(s => s.GetTeamsBySchoolYearAsync(oldSchoolYearId))
                .ReturnsAsync(new List<Team> { new Team { Id = "team-old" } });

            _mockBulkOperationsService.Setup(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), accessToken, It.IsAny<int>()))
                .ReturnsAsync(archiveResult);

            // Setup mocks for create process
            var newSchoolYear = new SchoolYear { Id = newSchoolYearId, Name = "2025/2026" };
            _mockSchoolYearService.Setup(s => s.GetByIdAsync(newSchoolYearId))
                .ReturnsAsync(newSchoolYear);

            var template = new TeamTemplate { Id = "template-1", Name = "Template", TeamType = "class" };
            _mockTeamTemplateService.Setup(s => s.GetByIdAsync("template-1"))
                .ReturnsAsync(template);

            _mockDepartmentService.Setup(s => s.GetAllAsync())
                .ReturnsAsync(new List<Department> { new Department { Id = "dept-1", Name = "Class 1A" } });

            _mockTeamService.Setup(s => s.CreateAsync(It.IsAny<Team>()))
                .ReturnsAsync(new Team { Id = "team-new" });

            _mockBulkOperationsService.Setup(s => s.CreateTeamsAsync(It.IsAny<string[]>(), accessToken))
                .ReturnsAsync(createResult);

            _mockOperationHistoryService.Setup(s => s.CreateAsync(It.IsAny<OperationHistory>()))
                .ReturnsAsync(new OperationHistory());

            // Act
            var result = await _orchestrator.TransitionToNewSchoolYearAsync(
                oldSchoolYearId, newSchoolYearId, templateIds, accessToken);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.SuccessfulOperations.Count); // 1 archive + 1 create
            Assert.Empty(result.Errors);

            // Verify both archive and create operations were called
            _mockTeamService.Verify(s => s.GetTeamsBySchoolYearAsync(oldSchoolYearId), Times.Once);
            _mockBulkOperationsService.Verify(s => s.ArchiveTeamsAsync(It.IsAny<string[]>(), accessToken, It.IsAny<int>()), Times.Once);
            _mockBulkOperationsService.Verify(s => s.CreateTeamsAsync(It.IsAny<string[]>(), accessToken), Times.Once);
        }

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ShouldReturnActiveProcesses()
        {
            // Act
            var result = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            Assert.NotNull(result);
            // Fresh instance should have no active processes
            Assert.Empty(result);
        }

        [Fact]
        public async Task CancelProcessAsync_WithNonExistentProcess_ShouldReturnFalse()
        {
            // Arrange
            var processId = "non-existent-process";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert
            Assert.False(result);
        }
    }
} 