using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services.Core
{
    /// <summary>
    /// Testy jednostkowe dla TeamService - najważniejszego serwisu aplikacji
    /// Pokrycie: tworzenie zespołów, pobieranie, archiwizowanie, aktualizacja, cache
    /// </summary>
    public class TeamServiceTests
    {
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGenericRepository<TeamMember>> _mockTeamMemberRepository;
        private readonly Mock<ITeamTemplateRepository> _mockTeamTemplateRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IGraphTeamManagementService> _mockGraphTeamService;
        private readonly Mock<IGraphUserManagementService> _mockGraphUserService;
        private readonly Mock<IGraphBulkOperationsService> _mockGraphBulkOps;
        private readonly Mock<IGraphService> _mockGraphService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IAdminNotificationService> _mockAdminNotificationService;
        private readonly Mock<ILogger<TeamService>> _mockLogger;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly Mock<ICacheInvalidationService> _mockCacheInvalidationService;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGraphSynchronizer<Team, Team>> _mockTeamSynchronizer;
        private readonly TeamService _teamService;

        public TeamServiceTests()
        {
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTeamMemberRepository = new Mock<IGenericRepository<TeamMember>>();
            _mockTeamTemplateRepository = new Mock<ITeamTemplateRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockGraphTeamService = new Mock<IGraphTeamManagementService>();
            _mockGraphUserService = new Mock<IGraphUserManagementService>();
            _mockGraphBulkOps = new Mock<IGraphBulkOperationsService>();
            _mockGraphService = new Mock<IGraphService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockAdminNotificationService = new Mock<IAdminNotificationService>();
            _mockLogger = new Mock<ILogger<TeamService>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();
            _mockCacheInvalidationService = new Mock<ICacheInvalidationService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockTeamSynchronizer = new Mock<IGraphSynchronizer<Team, Team>>();

            _teamService = new TeamService(
                _mockTeamRepository.Object,
                _mockUserRepository.Object,
                _mockTeamMemberRepository.Object,
                _mockTeamTemplateRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockGraphTeamService.Object,
                _mockGraphUserService.Object,
                _mockGraphBulkOps.Object,
                _mockGraphService.Object,
                _mockNotificationService.Object,
                _mockAdminNotificationService.Object,
                _mockLogger.Object,
                _mockSchoolTypeRepository.Object,
                _mockSchoolYearRepository.Object,
                _mockOperationHistoryService.Object,
                _mockGraphCacheService.Object,
                _mockCacheInvalidationService.Object,
                null, // Pomija Unit of Work dla prostoty testów
                _mockTeamSynchronizer.Object);

            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("admin@test.com");
        }

        #region CreateTeamAsync Tests

        [Fact]
        public async Task CreateTeamAsync_WithValidData_ShouldCreateTeamSuccessfully()
        {
            // Arrange
            var displayName = "Test Team";
            var description = "Test Description";
            var ownerUpn = "owner@test.com";
            var visibility = TeamVisibility.Private;
            var accessToken = "valid-token";

            var ownerUser = CreateTestUser("user-123", "John", "Owner", ownerUpn);
            var operation = new OperationHistory { Id = "op-123" };
            var graphTeam = new GraphTeam { Id = "graph-team-123", DisplayName = displayName };

            // Mock operation history
            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamCreated, nameof(Team), null, displayName, null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            // Mock user repository
            _mockUserRepository.Setup(x => x.GetActiveUserByUpnAsync(ownerUpn))
                .ReturnsAsync(ownerUser);

            // Mock repository (dla prostoty pomijamy Unit of Work)
            _mockTeamRepository.Setup(x => x.AddAsync(It.IsAny<Team>()))
                .Returns(Task.CompletedTask);

            // Mock Graph team creation
            _mockGraphService.Setup(x => x.ExecuteWithAutoConnectAsync(
                accessToken,
                It.IsAny<Func<Task<GraphTeam>>>(),
                It.IsAny<string>()))
                .ReturnsAsync(GraphOperationResult<GraphTeam>.CreateSuccess(graphTeam));

            // Mock notifications
            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _mockAdminNotificationService.Setup(x => x.SendTeamCreatedNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            // Mock cache invalidation
            _mockCacheInvalidationService.Setup(x => x.InvalidateForTeamCreatedAsync(It.IsAny<Team>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _teamService.CreateTeamAsync(
                displayName, description, ownerUpn, visibility, accessToken);

            // Assert
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be(displayName);
            result.Description.Should().Be(description);
            result.Owner.Should().Be(ownerUpn);
            result.Visibility.Should().Be(visibility);
            result.Status.Should().Be(TeamStatus.Active);
            result.ExternalId.Should().Be(graphTeam.Id);

            _mockTeamRepository.Verify(x => x.AddAsync(It.IsAny<Team>()), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task CreateTeamAsync_WithEmptyDisplayName_ShouldReturnNull()
        {
            // Arrange
            var displayName = "";
            var description = "Test Description";
            var ownerUpn = "owner@test.com";
            var visibility = TeamVisibility.Private;
            var accessToken = "valid-token";

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamCreated, nameof(Team), null, displayName, null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            // Act
            var result = await _teamService.CreateTeamAsync(
                displayName, description, ownerUpn, visibility, accessToken);

            // Assert
            result.Should().BeNull();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task CreateTeamAsync_WithInactiveOwner_ShouldReturnNull()
        {
            // Arrange
            var displayName = "Test Team";
            var description = "Test Description";
            var ownerUpn = "inactive@test.com";
            var visibility = TeamVisibility.Private;
            var accessToken = "valid-token";

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamCreated, nameof(Team), null, displayName, null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), null))
                .ReturnsAsync(true);

            _mockUserRepository.Setup(x => x.GetActiveUserByUpnAsync(ownerUpn))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _teamService.CreateTeamAsync(
                displayName, description, ownerUpn, visibility, accessToken);

            // Assert
            result.Should().BeNull();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region GetTeamByIdAsync Tests

        [Fact]
        public async Task GetTeamByIdAsync_WithValidId_ShouldReturnTeam()
        {
            // Arrange
            var teamId = "team-123";
            var expectedTeam = CreateTestTeam(teamId, "Test Team", "Test Description", "owner@test.com");
            expectedTeam.Status = TeamStatus.Active;

            // Prostsze podejście - nie mock'ujemy cache, tylko repozytoria
            _mockTeamRepository.Setup(x => x.GetByIdAsync(teamId))
                .ReturnsAsync(expectedTeam);

            // Act
            var result = await _teamService.GetTeamByIdAsync(teamId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(teamId);
            result.DisplayName.Should().Be("Test Team");
            result.Status.Should().Be(TeamStatus.Active);
        }

        [Fact]
        public async Task GetTeamByIdAsync_WithEmptyId_ShouldReturnNull()
        {
            // Arrange
            var teamId = "";

            // Act
            var result = await _teamService.GetTeamByIdAsync(teamId);

            // Assert
            result.Should().BeNull();
            _mockTeamRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetTeamByIdAsync_WithArchivedTeam_ShouldReturnNull()
        {
            // Arrange
            var teamId = "team-123";
            var archivedTeam = CreateTestTeam(teamId, "Archived Team", "Test Description", "owner@test.com");
            archivedTeam.Status = TeamStatus.Archived;

            _mockTeamRepository.Setup(x => x.GetByIdAsync(teamId))
                .ReturnsAsync(archivedTeam);

            // Act
            var result = await _teamService.GetTeamByIdAsync(teamId);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ArchiveTeamAsync Tests

        [Fact]
        public async Task ArchiveTeamAsync_WithValidTeam_ShouldArchiveSuccessfully()
        {
            // Arrange
            var teamId = "team-123";
            var reason = "End of school year";
            var accessToken = "valid-token";

            var team = CreateTestTeam(teamId, "Test Team", "Test Description", "owner@test.com");
            team.Status = TeamStatus.Active;

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamArchived, nameof(Team), teamId, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team> { team });

            _mockGraphService.Setup(x => x.ExecuteWithAutoConnectAsync(
                accessToken,
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()))
                .Returns(Task.FromResult(GraphOperationResult<bool>.CreateSuccess(true)));

            _mockCacheInvalidationService.Setup(x => x.InvalidateForTeamArchivedAsync(It.IsAny<Team>()))
                .Returns(Task.CompletedTask);

            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _teamService.ArchiveTeamAsync(teamId, reason, accessToken);

            // Assert
            result.Should().BeTrue();
            team.Status.Should().Be(TeamStatus.Archived);
            _mockTeamRepository.Verify(x => x.Update(team), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task ArchiveTeamAsync_WithNonExistentTeam_ShouldReturnFalse()
        {
            // Arrange
            var teamId = "non-existent";
            var reason = "Test reason";
            var accessToken = "valid-token";

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamArchived, nameof(Team), teamId, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team>());

            // Act
            var result = await _teamService.ArchiveTeamAsync(teamId, reason, accessToken);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region UpdateTeamAsync Tests

        [Fact]
        public async Task UpdateTeamAsync_WithValidTeam_ShouldUpdateSuccessfully()
        {
            // Arrange
            var teamId = "team-123";
            var accessToken = "valid-token";

            var existingTeam = CreateTestTeam(teamId, "Old Name", "Old Description", "old-owner@test.com");
            var updatedTeam = CreateTestTeam(teamId, "New Name", "New Description", "new-owner@test.com");

            var newOwnerUser = CreateTestUser("user-456", "New", "Owner", "new-owner@test.com");

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamUpdated, nameof(Team), teamId, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team> { existingTeam });

            _mockUserRepository.Setup(x => x.GetActiveUserByUpnAsync("new-owner@test.com"))
                .ReturnsAsync(newOwnerUser);

            _mockGraphService.Setup(x => x.ExecuteWithAutoConnectAsync(
                accessToken,
                It.IsAny<Func<Task<bool>>>(),
                It.IsAny<string>()))
                .Returns(Task.FromResult(GraphOperationResult<bool>.CreateSuccess(true)));

            _mockCacheInvalidationService.Setup(x => x.InvalidateForTeamUpdatedAsync(It.IsAny<Team>(), It.IsAny<Team?>()))
                .Returns(Task.CompletedTask);

            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _teamService.UpdateTeamAsync(updatedTeam, accessToken);

            // Assert
            result.Should().BeTrue();
            existingTeam.DisplayName.Should().Be("New Name");
            existingTeam.Description.Should().Be("New Description");
            existingTeam.Owner.Should().Be("new-owner@test.com");
            _mockTeamRepository.Verify(x => x.Update(existingTeam), Times.Once);
        }

        [Fact]
        public async Task UpdateTeamAsync_WithNonExistentTeam_ShouldReturnFalse()
        {
            // Arrange
            var teamId = "non-existent";
            var accessToken = "valid-token";
            var team = CreateTestTeam(teamId, "Test Name", "Test Description", "owner@test.com");

            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.TeamUpdated, nameof(Team), teamId, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team>());

            // Act
            var result = await _teamService.UpdateTeamAsync(team, accessToken);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region GetAllTeamsAsync Tests

        [Fact]
        public async Task GetAllTeamsAsync_WithForceRefresh_ShouldCallRepository()
        {
            // Arrange
            var teamsFromDb = new List<Team>
            {
                CreateTestTeam("team-1", "Team 1", "Description 1", "owner1@test.com"),
                CreateTestTeam("team-2", "Team 2", "Description 2", "owner2@test.com")
            };

            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(teamsFromDb);

            // Act
            var result = await _teamService.GetAllTeamsAsync(forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(teamsFromDb);
            _mockTeamRepository.Verify(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()), Times.Once);
        }

        #endregion

        #region Helper Methods

        private static User CreateTestUser(string id, string firstName, string lastName, string upn)
        {
            return new User
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Nauczyciel,
                DepartmentId = "dept-123",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        private static Team CreateTestTeam(string id, string displayName, string description, string ownerUpn)
        {
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Owner = ownerUpn,
                Status = TeamStatus.Active,
                Visibility = TeamVisibility.Private,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com",
                ExternalId = $"external-{id}",
                Members = new List<TeamMember>()
            };
        }

        #endregion
    }
} 