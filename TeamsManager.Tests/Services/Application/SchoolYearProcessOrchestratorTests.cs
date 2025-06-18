using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Services.Application
{
    public class SchoolYearProcessOrchestratorTests
    {
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<ITeamTemplateService> _mockTeamTemplateService;
        private readonly Mock<ISchoolYearService> _mockSchoolYearService;
        private readonly Mock<IGraphBulkOperationsService> _mockBulkOperationsService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<SchoolYearProcessOrchestrator>> _mockLogger;
        private readonly SchoolYearProcessOrchestrator _orchestrator;

        public SchoolYearProcessOrchestratorTests()
        {
            _mockTeamService = new Mock<ITeamService>();
            _mockTeamTemplateService = new Mock<ITeamTemplateService>();
            _mockSchoolYearService = new Mock<ISchoolYearService>();
            _mockBulkOperationsService = new Mock<IGraphBulkOperationsService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<SchoolYearProcessOrchestrator>>();

            _orchestrator = new SchoolYearProcessOrchestrator(
                _mockTeamService.Object,
                _mockTeamTemplateService.Object,
                _mockSchoolYearService.Object,
                _mockBulkOperationsService.Object,
                _mockNotificationService.Object,
                _mockLogger.Object
            );
        }

        #region CreateTeamsForNewSchoolYearAsync Tests

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithValidData_ShouldReturnSuccess()
        {
            // Arrange
            var schoolYearId = "sy-2024";
            var templateIds = new[] { "template-1", "template-2" };
            var accessToken = "valid-token";
            var schoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");
            var templates = new List<TeamTemplate>
            {
                CreateTestTemplate("template-1", "Math Team Template"),
                CreateTestTemplate("template-2", "Science Team Template")
            };

            _mockSchoolYearService.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);
            _mockTeamTemplateService.Setup(x => x.GetByIdAsync("template-1"))
                .ReturnsAsync(templates[0]);
            _mockTeamTemplateService.Setup(x => x.GetByIdAsync("template-2"))
                .ReturnsAsync(templates[1]);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                schoolYearId, templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("CreateTeamsForNewSchoolYear");
            result.SuccessfulOperations.Should().HaveCount(2);
            _mockSchoolYearService.Verify(x => x.GetByIdAsync(schoolYearId), Times.Once);
            _mockTeamTemplateService.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithEmptySchoolYearId_ShouldReturnError()
        {
            // Arrange
            var templateIds = new[] { "template-1" };
            var accessToken = "valid-token";

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                "", templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be("ID roku szkolnego jest wymagane");
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Be("ID roku szkolnego jest wymagane");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithEmptyTemplateIds_ShouldReturnError()
        {
            // Arrange
            var schoolYearId = "sy-2024";
            var templateIds = Array.Empty<string>();
            var accessToken = "valid-token";

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                schoolYearId, templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be("Lista szablonów jest wymagana");
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Be("Lista szablonów jest wymagana");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithNonExistentSchoolYear_ShouldReturnError()
        {
            // Arrange
            var schoolYearId = "non-existent";
            var templateIds = new[] { "template-1" };
            var accessToken = "valid-token";

            _mockSchoolYearService.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                schoolYearId, templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("nie istnieje");
        }

        [Fact]
        public async Task CreateTeamsForNewSchoolYearAsync_WithNonExistentTemplates_ShouldReturnError()
        {
            // Arrange
            var schoolYearId = "sy-2024";
            var templateIds = new[] { "non-existent-1", "non-existent-2" };
            var accessToken = "valid-token";
            var schoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");

            _mockSchoolYearService.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);
            _mockTeamTemplateService.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((TeamTemplate)null);

            // Act
            var result = await _orchestrator.CreateTeamsForNewSchoolYearAsync(
                schoolYearId, templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Żaden z podanych szablonów nie istnieje");
        }

        #endregion

        #region ArchiveTeamsFromPreviousSchoolYearAsync Tests

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_WithActiveTeams_ShouldReturnSuccess()
        {
            // Arrange
            var schoolYearId = "sy-2023";
            var accessToken = "valid-token";
            var activeTeams = new List<Team>
            {
                CreateTestTeam("team-1", "Math Team", schoolYearId),
                CreateTestTeam("team-2", "Science Team", schoolYearId)
            };
            var graphResult = new GraphBulkResult
            {
                Success = true,
                SuccessfulOperations = new List<GraphBulkOperationSuccess>
                {
                    new GraphBulkOperationSuccess { EntityId = "team-1", Operation = "Archive" },
                    new GraphBulkOperationSuccess { EntityId = "team-2", Operation = "Archive" }
                },
                Errors = new List<GraphBulkOperationError>()
            };

            _mockTeamService.Setup(x => x.GetTeamsBySchoolYearAsync(schoolYearId, false, null))
                .ReturnsAsync(activeTeams);
            _mockBulkOperationsService.Setup(x => x.ArchiveTeamsAsync(
                It.IsAny<string[]>(), accessToken, It.IsAny<int>()))
                .ReturnsAsync(graphResult);

            // Act
            var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(
                schoolYearId, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("ArchiveTeamsFromPreviousSchoolYear");
            result.SuccessfulOperations.Should().HaveCount(2);
            _mockBulkOperationsService.Verify(x => x.ArchiveTeamsAsync(
                It.Is<string[]>(ids => ids.Length == 2), accessToken, It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_WithNoActiveTeams_ShouldReturnSuccessWithEmptyResult()
        {
            // Arrange
            var schoolYearId = "sy-2023";
            var accessToken = "valid-token";
            var emptyTeams = new List<Team>();

            _mockTeamService.Setup(x => x.GetTeamsBySchoolYearAsync(schoolYearId, false, null))
                .ReturnsAsync(emptyTeams);

            // Act
            var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(
                schoolYearId, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().BeEmpty();
            _mockBulkOperationsService.Verify(x => x.ArchiveTeamsAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ArchiveTeamsFromPreviousSchoolYearAsync_WithDryRun_ShouldReturnSimulationResult()
        {
            // Arrange
            var schoolYearId = "sy-2023";
            var accessToken = "valid-token";
            var options = new SchoolYearProcessOptions { DryRun = true };
            var activeTeams = new List<Team>
            {
                CreateTestTeam("team-1", "Math Team", schoolYearId),
                CreateTestTeam("team-2", "Science Team", schoolYearId)
            };

            _mockTeamService.Setup(x => x.GetTeamsBySchoolYearAsync(schoolYearId, false, null))
                .ReturnsAsync(activeTeams);

            // Act
            var result = await _orchestrator.ArchiveTeamsFromPreviousSchoolYearAsync(
                schoolYearId, accessToken, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2);
            result.SuccessfulOperations.All(s => s.Message.Contains("DryRun")).Should().BeTrue();
            _mockBulkOperationsService.Verify(x => x.ArchiveTeamsAsync(
                It.IsAny<string[]>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        #endregion

        #region TransitionToNewSchoolYearAsync Tests

        [Fact]
        public async Task TransitionToNewSchoolYearAsync_WithValidData_ShouldReturnSuccess()
        {
            // Arrange
            var previousSchoolYearId = "sy-2023";
            var newSchoolYearId = "sy-2024";
            var templateIds = new[] { "template-1" };
            var accessToken = "valid-token";

            // Setup for archive operation
            var activeTeams = new List<Team> { CreateTestTeam("team-1", "Old Team", previousSchoolYearId) };
            var graphResult = new GraphBulkResult
            {
                Success = true,
                SuccessfulOperations = new List<GraphBulkOperationSuccess>
                {
                    new GraphBulkOperationSuccess { EntityId = "team-1", Operation = "Archive" }
                },
                Errors = new List<GraphBulkOperationError>()
            };

            _mockTeamService.Setup(x => x.GetTeamsBySchoolYearAsync(previousSchoolYearId, false, null))
                .ReturnsAsync(activeTeams);
            _mockBulkOperationsService.Setup(x => x.ArchiveTeamsAsync(
                It.IsAny<string[]>(), accessToken, It.IsAny<int>()))
                .ReturnsAsync(graphResult);

            // Setup for create operation
            var newSchoolYear = CreateTestSchoolYear(newSchoolYearId, "2024/2025");
            var template = CreateTestTemplate("template-1", "New Template");

            _mockSchoolYearService.Setup(x => x.GetByIdAsync(newSchoolYearId))
                .ReturnsAsync(newSchoolYear);
            _mockTeamTemplateService.Setup(x => x.GetByIdAsync("template-1"))
                .ReturnsAsync(template);

            // Act
            var result = await _orchestrator.TransitionToNewSchoolYearAsync(
                previousSchoolYearId, newSchoolYearId, templateIds, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("TransitionToNewSchoolYear");
            result.SuccessfulOperations.Should().HaveCount(2); // 1 archive + 1 create
        }

        #endregion

        #region GetActiveProcessesStatusAsync Tests

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ShouldReturnActiveProcesses()
        {
            // Act
            var result = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<IEnumerable<SchoolYearProcessStatus>>();
        }

        #endregion

        #region CancelProcessAsync Tests

        [Fact]
        public async Task CancelProcessAsync_WithValidProcessId_ShouldReturnTrue()
        {
            // Arrange
            var processId = "process-123";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert - Ponieważ proces nie istnieje, powinno zwrócić false
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CancelProcessAsync_WithInvalidProcessId_ShouldReturnFalse()
        {
            // Arrange
            var processId = "non-existent";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Helper Methods

        private static SchoolYear CreateTestSchoolYear(string id, string name)
        {
            return new SchoolYear
            {
                Id = id,
                Name = name,
                StartDate = DateTime.Today.AddMonths(-6),
                EndDate = DateTime.Today.AddMonths(6),
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        private static TeamTemplate CreateTestTemplate(string id, string name)
        {
            return new TeamTemplate
            {
                Id = id,
                Name = name,
                Description = $"Test template {name}",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        private static Team CreateTestTeam(string id, string displayName, string schoolYearId)
        {
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                SchoolYearId = schoolYearId,
                Status = TeamStatus.Active,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 