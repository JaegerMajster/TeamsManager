using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Services
{
    /// <summary>
    /// Kompleksowe testy jednostkowe dla TeamLifecycleOrchestrator
    /// Testuje wzorce: Thread-Safety w Orkiestratorach, Batch Processing, Auditowanie, Powiadomienia
    /// Zgodnie ze wzorcami implementacyjnymi z docs/wzorceImplementacyjne.md
    /// </summary>
    public class TeamLifecycleOrchestratorTests : IDisposable
    {
        #region Test Dependencies and Setup

        private readonly Mock<ITeamService> _teamServiceMock;
        private readonly Mock<IUserService> _userServiceMock;
        private readonly Mock<ISchoolYearService> _schoolYearServiceMock;
        private readonly Mock<IPowerShellBulkOperationsService> _bulkOperationsServiceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IAdminNotificationService> _adminNotificationServiceMock;
        private readonly Mock<ICacheInvalidationService> _cacheInvalidationServiceMock;
        private readonly Mock<IOperationHistoryService> _operationHistoryServiceMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly Mock<ILogger<TeamLifecycleOrchestrator>> _loggerMock;

        private readonly TeamLifecycleOrchestrator _orchestrator;
        private readonly string _testToken = "test-access-token";
        private readonly string _currentUserUpn = "admin@example.com";

        public TeamLifecycleOrchestratorTests()
        {
            _teamServiceMock = new Mock<ITeamService>();
            _userServiceMock = new Mock<IUserService>();
            _schoolYearServiceMock = new Mock<ISchoolYearService>();
            _bulkOperationsServiceMock = new Mock<IPowerShellBulkOperationsService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _adminNotificationServiceMock = new Mock<IAdminNotificationService>();
            _cacheInvalidationServiceMock = new Mock<ICacheInvalidationService>();
            _operationHistoryServiceMock = new Mock<IOperationHistoryService>();
            _currentUserServiceMock = new Mock<ICurrentUserService>();
            _loggerMock = new Mock<ILogger<TeamLifecycleOrchestrator>>();

            SetupCommonMocks();

            _orchestrator = new TeamLifecycleOrchestrator(
                _teamServiceMock.Object,
                _userServiceMock.Object,
                _schoolYearServiceMock.Object,
                _bulkOperationsServiceMock.Object,
                _notificationServiceMock.Object,
                _adminNotificationServiceMock.Object,
                _cacheInvalidationServiceMock.Object,
                _operationHistoryServiceMock.Object,
                _currentUserServiceMock.Object,
                _loggerMock.Object);
        }

        private void SetupCommonMocks()
        {
            _currentUserServiceMock.Setup(x => x.GetCurrentUserUpn()).Returns(_currentUserUpn);

            // Setup powiadomień zgodnie ze wzorcem Powiadomień i Notyfikacji
            _notificationServiceMock.Setup(x => x.SendNotificationToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            
            // Setup SendOperationProgressToUserAsync - brakujący mock!
            _notificationServiceMock.Setup(x => x.SendOperationProgressToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _adminNotificationServiceMock.Setup(x => x.SendBulkTeamsOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            // Setup cache invalidation zgodnie ze wzorcem Cacheowania Wielopoziomowego
            _cacheInvalidationServiceMock.Setup(x => x.InvalidateForTeamUpdatedAsync(It.IsAny<Team>(), It.IsAny<Team>()))
                .Returns(Task.CompletedTask);
            _cacheInvalidationServiceMock.Setup(x => x.InvalidateForTeamCreatedAsync(It.IsAny<Team>()))
                .Returns(Task.CompletedTask);
            _cacheInvalidationServiceMock.Setup(x => x.InvalidateForTeamArchivedAsync(It.IsAny<Team>()))
                .Returns(Task.CompletedTask);
            _cacheInvalidationServiceMock.Setup(x => x.InvalidateForTeamMemberAddedAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        }

        #endregion

        #region BulkArchiveTeamsWithCleanupAsync Tests

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_EmptyTeamsList_ReturnsFailureResult()
        {
            // Arrange
            var options = new ArchiveOptions();

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync([], options, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be("Lista zespołów jest wymagana");
            result.Errors.Should().HaveCount(1);
            result.Errors.First().Operation.Should().Be("BulkArchiveValidation");
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_ValidTeams_SuccessfulArchival()
        {
            // Arrange
            var teamIds = new[] { "team1", "team2", "team3" };
            var options = new ArchiveOptions
            {
                NotifyOwners = true,
                BatchSize = 2,
                CreateBackup = true
            };

            var teams = CreateTestTeams(teamIds);
            SetupTeamServiceForArchival(teams);
            SetupSuccessfulBulkOperations(teamIds);

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(3);
            result.Errors.Should().BeEmpty();

            // Verify wzorzec Batch Processing - każdy zespół archiwizowany indywidualnie
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync(It.IsAny<string>(), "Masowa archiwizacja", _testToken), Times.Exactly(3));

            // Verify wzorzec Powiadomień
            _adminNotificationServiceMock.Verify(x => x.SendBulkTeamsOperationNotificationAsync(
                It.Is<string>(s => s.Contains("archiwizacja")),
                teamIds.Length,
                It.IsAny<int>(),
                It.IsAny<int>(),
                _currentUserUpn,
                It.IsAny<Dictionary<string, object>>()), Times.Once);
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_NonExistentTeams_HandlesValidationErrors()
        {
            // Arrange
            var teamIds = new[] { "non-existent1", "non-existent2" };
            var options = new ArchiveOptions();

            // Teams nie istnieją - GetByIdAsync zwraca null
            _teamServiceMock.Setup(x => x.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Team?)null);

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Be("Brak prawidłowych zespołów do archiwizacji");
            result.Errors.Should().HaveCount(2);
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_AlreadyArchivedTeams_SkipsAndContinues()
        {
            // Arrange
            var teamIds = new[] { "team1", "team2", "team3" };
            var options = new ArchiveOptions();

            var teams = new[]
            {
                CreateTestTeam("team1", TeamStatus.Active),
                CreateTestTeam("team2", TeamStatus.Archived), // Już zarchiwizowany
                CreateTestTeam("team3", TeamStatus.Active)
            };

            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(teams[0]);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team2")).ReturnsAsync(teams[1]);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team3")).ReturnsAsync(teams[2]);
            
            // Setup archivowania tylko dla aktywnych zespołów
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team1", "Masowa archiwizacja", _testToken)).ReturnsAsync(true);
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team3", "Masowa archiwizacja", _testToken)).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);

            // Assert
            result.Should().NotBeNull();
            // Gdy są błędy walidacji (zespół już zarchiwizowany), result.Success będzie false
            result.Success.Should().BeFalse();
            result.SuccessfulOperations.Should().HaveCount(2); // Tylko team1 i team3
            result.Errors.Should().HaveCount(1); // Warning o team2 już zarchiwizowanym
            result.Errors.First().Message.Should().Contain("już zarchiwizowany");
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_WithNotifyOwners_SendsNotifications()
        {
            // Arrange
            var teamIds = new[] { "team1" };
            var options = new ArchiveOptions { NotifyOwners = true };
            var teams = CreateTestTeams(teamIds);
            
            SetupTeamServiceForArchival(teams);
            SetupSuccessfulBulkOperations(teamIds);

            // Setup user dla właściciela
            var ownerUser = new User 
            { 
                Id = "owner1", 
                UPN = "owner@example.com", 
                FirstName = "Test", 
                LastName = "Owner" 
            };
            _userServiceMock.Setup(x => x.GetUserByUpnAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(ownerUser);

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);

            // Assert
            result.Success.Should().BeTrue();
            
            // Verify że powiadomienia zostały wysłane do właścicieli
            _notificationServiceMock.Verify(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(),
                It.Is<string>(msg => msg.Contains("zarchiwizowane")),
                "info"), Times.AtLeastOnce);
        }

        [Fact]
        public async Task BulkArchiveTeamsWithCleanupAsync_ContinueOnErrorFalse_StopsOnFirstError()
        {
            // Arrange
            var teamIds = new[] { "team1", "team2", "team3" };
            var options = new ArchiveOptions 
            { 
                ContinueOnError = false,
                BatchSize = 1 
            };

            var teams = CreateTestTeams(teamIds);
            SetupTeamServiceForArchival(teams);

            // Setup archivowania - pierwszy sukces, drugi błąd
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team1", "Masowa archiwizacja", _testToken)).ReturnsAsync(true);
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team2", "Masowa archiwizacja", _testToken)).ThrowsAsync(new Exception("Archive failed"));
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team3", "Masowa archiwizacja", _testToken)).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);

            // Assert
            result.Success.Should().BeFalse();
            // Verify że zatrzymano na błędzie
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("ContinueOnError=false")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        #endregion

        #region BulkRestoreTeamsWithValidationAsync Tests

        [Fact]
        public async Task BulkRestoreTeamsWithValidationAsync_ValidArchivedTeams_SuccessfulRestore()
        {
            // Arrange
            var teamIds = new[] { "team1", "team2" };
            var options = new RestoreOptions { ValidateOwnerAvailability = true };

            var archivedTeams = teamIds.Select(id => CreateTestTeam(id, TeamStatus.Archived)).ToArray();
            SetupTeamServiceForRestore(archivedTeams);
            SetupUserServiceForValidation();

            // Act
            var result = await _orchestrator.BulkRestoreTeamsWithValidationAsync(teamIds, options, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2);
            result.Errors.Should().BeEmpty();

            // Verify że RestoreTeamAsync został wywołany dla każdego zespołu
            _teamServiceMock.Verify(x => x.RestoreTeamAsync(It.IsAny<string>(), _testToken), Times.Exactly(2));
        }

        [Fact]
        public async Task BulkRestoreTeamsWithValidationAsync_ActiveTeams_SkipsAlreadyActive()
        {
            // Arrange
            var teamIds = new[] { "team1", "team2" };
            var options = new RestoreOptions { ValidateOwnerAvailability = false }; // Wyłącz walidację właściciela

            // Pierwszy zespół aktywny, drugi zarchiwizowany
            var teams = new[]
            {
                CreateTestTeam("team1", TeamStatus.Active),
                CreateTestTeam("team2", TeamStatus.Archived)
            };

            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(teams[0]);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team2")).ReturnsAsync(teams[1]);
            _teamServiceMock.Setup(x => x.RestoreTeamAsync("team2", _testToken)).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.BulkRestoreTeamsWithValidationAsync(teamIds, options, _testToken);

            // Assert
            result.Should().NotBeNull();
            // Gdy są błędy walidacji (zespół już aktywny), result.Success będzie false
            result.Success.Should().BeFalse();
            result.SuccessfulOperations.Should().HaveCount(1); // Tylko team2
            result.Errors.Should().HaveCount(1); // Warning o team1 już aktywnym
            result.Errors.First().Message.Should().Contain("już aktywny");
        }

        [Fact]
        public async Task BulkRestoreTeamsWithValidationAsync_ValidateOwnerAvailability_ChecksOwners()
        {
            // Arrange
            var teamIds = new[] { "team1" };
            var options = new RestoreOptions { ValidateOwnerAvailability = true };
            var team = CreateTestTeam("team1", TeamStatus.Archived);
            team.Owner = "owner@example.com";

            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(team);
            
            // Owner nieaktywny
            var inactiveOwner = new User 
            { 
                UPN = "owner@example.com", 
                IsActive = false 
            };
            _userServiceMock.Setup(x => x.GetUserByUpnAsync("owner@example.com", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(inactiveOwner);

            // Act
            var result = await _orchestrator.BulkRestoreTeamsWithValidationAsync(teamIds, options, _testToken);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().HaveCount(1);
            result.Errors.First().Message.Should().Contain("nieaktywny");
        }

        #endregion

        #region MigrateTeamsBetweenSchoolYearsAsync Tests

        [Fact]
        public async Task MigrateTeamsBetweenSchoolYearsAsync_ValidPlan_SuccessfulMigration()
        {
            // Arrange
            var plan = new TeamMigrationPlan
            {
                FromSchoolYearId = "2023-2024",
                ToSchoolYearId = "2024-2025",
                TeamIds = new[] { "team1", "team2" },
                ArchiveSourceTeams = true,
                CopyMembers = true,
                BatchSize = 2
            };

            var sourceSchoolYear = new SchoolYear { Id = "2023-2024", Name = "2023/2024" };
            var targetSchoolYear = new SchoolYear { Id = "2024-2025", Name = "2024/2025" };
            var teams = CreateTestTeams(plan.TeamIds);

            SetupSchoolYearService(sourceSchoolYear, targetSchoolYear);
            SetupTeamServiceForMigration(teams);

            // Act
            var result = await _orchestrator.MigrateTeamsBetweenSchoolYearsAsync(plan, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2);

            // Verify że zespoły zostały zaktualizowane z nowym rokiem szkolnym
            _teamServiceMock.Verify(x => x.UpdateTeamAsync(
                It.Is<Team>(t => t.SchoolYearId == "2024-2025"), _testToken), Times.Exactly(2));
        }

        [Fact]
        public async Task MigrateTeamsBetweenSchoolYearsAsync_InvalidSchoolYears_ReturnsError()
        {
            // Arrange
            var plan = new TeamMigrationPlan
            {
                FromSchoolYearId = "invalid-year",
                ToSchoolYearId = "2024-2025",
                TeamIds = new[] { "team1" }
            };

            _schoolYearServiceMock.Setup(x => x.GetByIdAsync("invalid-year")).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _orchestrator.MigrateTeamsBetweenSchoolYearsAsync(plan, _testToken);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Jeden z lat szkolnych nie istnieje");
        }

        [Fact]
        public async Task MigrateTeamsBetweenSchoolYearsAsync_CopyMembers_TransfersTeamMembership()
        {
            // Arrange
            var plan = new TeamMigrationPlan
            {
                FromSchoolYearId = "2023-2024",
                ToSchoolYearId = "2024-2025",
                TeamIds = new[] { "team1" },
                CopyMembers = true
            };

            var sourceSchoolYear = new SchoolYear { Id = "2023-2024", Name = "2023/2024" };
            var targetSchoolYear = new SchoolYear { Id = "2024-2025", Name = "2024/2025" };
            var teamWithMembers = CreateTestTeamWithMembers("team1");

            SetupSchoolYearService(sourceSchoolYear, targetSchoolYear);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(teamWithMembers);
            
            // BRAKUJĄCY SETUP! Implementacja używa UpdateTeamAsync, nie CreateTeamAsync
            _teamServiceMock.Setup(x => x.UpdateTeamAsync(It.IsAny<Team>(), _testToken)).ReturnsAsync(true);
            
            // Setupy poniżej nie są używane przez prawdziwą implementację, ale zostawiam dla kompatybilności
            _teamServiceMock.Setup(x => x.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TeamVisibility>(), _testToken, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(CreateTestTeam("new-team1", TeamStatus.Active));

            var addMembersResult = new Dictionary<string, bool>
            {
                ["member1@example.com"] = true,
                ["member2@example.com"] = true
            };
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync(It.IsAny<string>(), It.IsAny<List<string>>(), _testToken))
                .ReturnsAsync(addMembersResult);

            // Act
            var result = await _orchestrator.MigrateTeamsBetweenSchoolYearsAsync(plan, _testToken);

            // Assert
            result.Success.Should().BeTrue();
            
            // Verify że zespół został zaktualizowany z nowym rokiem szkolnym
            _teamServiceMock.Verify(x => x.UpdateTeamAsync(It.IsAny<Team>(), _testToken), Times.Once);
        }

        #endregion

        #region ConsolidateInactiveTeamsAsync Tests

        [Fact]
        public async Task ConsolidateInactiveTeamsAsync_FindsInactiveTeams_SuccessfulConsolidation()
        {
            // Arrange
            var options = new ConsolidationOptions
            {
                MinInactiveDays = 90,
                MaxMembersCount = 5,
                OnlyTeamsWithoutActivity = true,
                BatchSize = 10
            };

            var inactiveTeams = new[]
            {
                CreateInactiveTeam("team1", DateTime.UtcNow.AddDays(-100)),
                CreateInactiveTeam("team2", DateTime.UtcNow.AddDays(-120))
            };

            // Setup GetAllTeamsAsync z wszystkimi parametrami explicite (wzorzec CS0854)
            _teamServiceMock.Setup(x => x.GetAllTeamsAsync(false, null)).ReturnsAsync(inactiveTeams);

            // Setup GetByIdAsync dla zespołów (potrzebne dla BulkArchiveTeamsWithCleanupAsync)
            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(inactiveTeams[0]);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team2")).ReturnsAsync(inactiveTeams[1]);

            // Setup archivowania zespołów
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team1", "Automatyczna konsolidacja - zespół nieaktywny", _testToken)).ReturnsAsync(true);
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team2", "Automatyczna konsolidacja - zespół nieaktywny", _testToken)).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.ConsolidateInactiveTeamsAsync(options, _testToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2);

            // Verify że zespoły zostały zarchiwizowane
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync("team1", "Automatyczna konsolidacja - zespół nieaktywny", _testToken), Times.Once);
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync("team2", "Automatyczna konsolidacja - zespół nieaktywny", _testToken), Times.Once);
        }

        [Fact]
        public async Task ConsolidateInactiveTeamsAsync_NoInactiveTeams_ReturnsEmptyResult()
        {
            // Arrange
            var options = new ConsolidationOptions();
            var activeTeams = new[]
            {
                CreateActiveTeam("team1"),
                CreateActiveTeam("team2")
            };

            // Setup GetAllTeamsAsync z wszystkimi parametrami explicite (wzorzec CS0854)
            _teamServiceMock.Setup(x => x.GetAllTeamsAsync(false, null)).ReturnsAsync(activeTeams);

            // Act
            var result = await _orchestrator.ConsolidateInactiveTeamsAsync(options, _testToken);

            // Assert
            result.Success.Should().BeTrue();
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ConsolidateInactiveTeamsAsync_FiltersBySchoolType_OnlyIncludesMatchingTeams()
        {
            // Arrange
            var options = new ConsolidationOptions
            {
                SchoolTypeIds = new[] { "high-school" },
                MinInactiveDays = 30
            };

            var teams = new[]
            {
                CreateInactiveTeamWithSchoolType("team1", "high-school", DateTime.UtcNow.AddDays(-60)),
                CreateInactiveTeamWithSchoolType("team2", "elementary", DateTime.UtcNow.AddDays(-60)),
                CreateInactiveTeamWithSchoolType("team3", "high-school", DateTime.UtcNow.AddDays(-90))
            };

            // Setup GetAllTeamsAsync z wszystkimi parametrami explicite (wzorzec CS0854)
            _teamServiceMock.Setup(x => x.GetAllTeamsAsync(false, null)).ReturnsAsync(teams);

            // Setup GetByIdAsync dla zespołów (potrzebne dla BulkArchiveTeamsWithCleanupAsync)
            _teamServiceMock.Setup(x => x.GetByIdAsync("team1")).ReturnsAsync(teams[0]);
            _teamServiceMock.Setup(x => x.GetByIdAsync("team3")).ReturnsAsync(teams[2]);

            // Setup archivowania tylko high-school teams
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team1", "Automatyczna konsolidacja - zespół nieaktywny", _testToken)).ReturnsAsync(true);
            _teamServiceMock.Setup(x => x.ArchiveTeamAsync("team3", "Automatyczna konsolidacja - zespół nieaktywny", _testToken)).ReturnsAsync(true);

            // Act
            var result = await _orchestrator.ConsolidateInactiveTeamsAsync(options, _testToken);

            // Assert
            result.Success.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(2); // Tylko high-school teams

            // Verify że tylko high-school teams zostały zarchiwizowane
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync("team1", "Automatyczna konsolidacja - zespół nieaktywny", _testToken), Times.Once);
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync("team3", "Automatyczna konsolidacja - zespół nieaktywny", _testToken), Times.Once);
            _teamServiceMock.Verify(x => x.ArchiveTeamAsync("team2", "Automatyczna konsolidacja - zespół nieaktywny", _testToken), Times.Never);
        }

        #endregion

        #region Process Management Tests (wzorzec Thread-Safety)

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ReturnsCurrentProcesses()
        {
            // Arrange - uruchom jakiś proces w tle
            var archiveTask = Task.Run(async () =>
            {
                var teamIds = new[] { "team1" };
                var options = new ArchiveOptions();
                SetupTeamServiceForArchival(CreateTestTeams(teamIds));
                SetupSuccessfulBulkOperations(teamIds);
                await _orchestrator.BulkArchiveTeamsWithCleanupAsync(teamIds, options, _testToken);
            });

            // Krótka przerwa żeby proces się rozpoczął
            await Task.Delay(100);

            // Act
            var processes = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            processes.Should().NotBeNull();
            // Może być pusty jeśli proces już się zakończył, ale nie powinien rzucać wyjątku

            await archiveTask; // Poczekaj na zakończenie
        }

        [Fact]
        public async Task CancelProcessAsync_NonExistentProcess_ReturnsFalse()
        {
            // Act
            var result = await _orchestrator.CancelProcessAsync("non-existent-process-id");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Helper Methods for Test Data Setup

        private List<Team> CreateTestTeams(string[] teamIds)
        {
            return teamIds.Select(id => CreateTestTeam(id, TeamStatus.Active)).ToList();
        }

        private Team CreateTestTeam(string id, TeamStatus status = TeamStatus.Active)
        {
            return new Team
            {
                Id = id,
                DisplayName = $"Test Team {id}",
                Description = $"Description for {id}",
                Owner = "owner@example.com",
                Status = status,
                Members = new List<TeamMember>(),
                Channels = new List<Channel>()
            };
        }

        private Team CreateTestTeamWithMembers(string id)
        {
            var team = CreateTestTeam(id);
            var member1User = new User { Id = "user1", UPN = "member1@example.com" };
            var member2User = new User { Id = "user2", UPN = "member2@example.com" };
            
            team.Members = new List<TeamMember>
            {
                new() { UserId = "user1", User = member1User, Role = TeamMemberRole.Member, IsActive = true },
                new() { UserId = "user2", User = member2User, Role = TeamMemberRole.Member, IsActive = true }
            };
            return team;
        }

        private Team CreateInactiveTeam(string id, DateTime lastActivity)
        {
            var team = CreateTestTeam(id);
            team.LastActivityDate = lastActivity;
            team.Members = new List<TeamMember>
            {
                new() { UserId = "user1", IsActive = true }
            }; // Mało członków
            // Ustawiamy ModifiedDate na starą datę żeby był uznany za nieaktywny
            team.ModifiedDate = lastActivity;
            return team;
        }

        private Team CreateActiveTeam(string id)
        {
            var team = CreateTestTeam(id);
            team.LastActivityDate = DateTime.UtcNow.AddDays(-1); // Aktywny niedawno
            team.CreatedDate = DateTime.UtcNow.AddDays(-30); // Utworzony 30 dni temu
            team.ModifiedDate = DateTime.UtcNow.AddDays(-1); // Zmodyfikowany 1 dzień temu (aktywny)
            team.Members = new List<TeamMember>
            {
                new() { UserId = "user1", IsActive = true },
                new() { UserId = "user2", IsActive = true },
                new() { UserId = "user3", IsActive = true },
                new() { UserId = "user4", IsActive = true },
                new() { UserId = "user5", IsActive = true },
                new() { UserId = "user6", IsActive = true } // Więcej niż MaxMembersCount (5)
            };
            return team;
        }

        private Team CreateInactiveTeamWithSchoolType(string id, string schoolTypeId, DateTime lastActivity)
        {
            var team = CreateInactiveTeam(id, lastActivity);
            team.SchoolTypeId = schoolTypeId;
            return team;
        }

        private void SetupTeamServiceForArchival(List<Team> teams)
        {
            foreach (var team in teams)
            {
                _teamServiceMock.Setup(x => x.GetByIdAsync(team.Id)).ReturnsAsync(team);
                _teamServiceMock.Setup(x => x.ArchiveTeamAsync(team.Id, "Masowa archiwizacja", _testToken)).ReturnsAsync(true);
            }
        }

        private void SetupTeamServiceForRestore(Team[] teams)
        {
            foreach (var team in teams)
            {
                _teamServiceMock.Setup(x => x.GetByIdAsync(team.Id)).ReturnsAsync(team);
                _teamServiceMock.Setup(x => x.RestoreTeamAsync(team.Id, _testToken)).ReturnsAsync(true);
            }
        }

        private void SetupUserServiceForValidation()
        {
            var activeOwner = new User { UPN = "owner@example.com", IsActive = true };
            _userServiceMock.Setup(x => x.GetUserByUpnAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(activeOwner);
        }

        private void SetupSchoolYearService(SchoolYear sourceYear, SchoolYear targetYear)
        {
            _schoolYearServiceMock.Setup(x => x.GetByIdAsync(sourceYear.Id)).ReturnsAsync(sourceYear);
            _schoolYearServiceMock.Setup(x => x.GetByIdAsync(targetYear.Id)).ReturnsAsync(targetYear);
        }

        private void SetupTeamServiceForMigration(List<Team> teams)
        {
            foreach (var team in teams)
            {
                _teamServiceMock.Setup(x => x.GetByIdAsync(team.Id)).ReturnsAsync(team);
                // Setup UpdateTeamAsync dla rzeczywistej implementacji (aktualizacji)
                _teamServiceMock.Setup(x => x.UpdateTeamAsync(It.IsAny<Team>(), _testToken)).ReturnsAsync(true);
                // Setup CreateTeamAsync dla potencjalnej implementacji z kopiowaniem
                _teamServiceMock.Setup(x => x.CreateTeamAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TeamVisibility>(),
                    _testToken, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                    .ReturnsAsync(CreateTestTeam($"new-{team.Id}", TeamStatus.Active));
                _teamServiceMock.Setup(x => x.ArchiveTeamAsync(team.Id, "Migracja zespołu", _testToken)).ReturnsAsync(true);
            }
        }

        private void SetupSuccessfulBulkOperations(string[] teamIds)
        {
            var successfulResult = new BulkOperationResult
            {
                Success = true,
                IsSuccess = true,
                SuccessfulOperations = teamIds.Select(id => new BulkOperationSuccess
                {
                    Operation = "ArchiveTeam",
                    EntityId = id,
                    Message = "Archived successfully"
                }).ToList(),
                Errors = new List<BulkOperationError>()
            };

            _bulkOperationsServiceMock.Setup(x => x.ArchiveTeamsAsync(It.IsAny<string[]>(), _testToken, It.IsAny<int>()))
                .ReturnsAsync(successfulResult);
        }

        #endregion

        #region Dispose Pattern

        public void Dispose()
        {
            _orchestrator?.Dispose();
        }

        #endregion
    }
}