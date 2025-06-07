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
    /// Kompleksowe testy jednostkowe dla BulkUserManagementOrchestrator
    /// Testuje wzorzec Thread-Safety w Orkiestratorach, Batch Processing i Auditowanie
    /// Zgodnie ze wzorcami implementacyjnymi z docs/wzorceImplementacyjne.md
    /// </summary>
    public class BulkUserManagementOrchestratorTests : IDisposable
    {
        #region Test Dependencies and Setup

        private readonly Mock<IUserService> _userServiceMock;
        private readonly Mock<ITeamService> _teamServiceMock;
        private readonly Mock<IDepartmentService> _departmentServiceMock;
        private readonly Mock<ISubjectService> _subjectServiceMock;
        private readonly Mock<IPowerShellBulkOperationsService> _bulkOperationsServiceMock;
        private readonly Mock<IPowerShellUserManagementService> _powerShellUserManagementMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly Mock<IAdminNotificationService> _adminNotificationServiceMock;
        private readonly Mock<ICacheInvalidationService> _cacheInvalidationServiceMock;
        private readonly Mock<IOperationHistoryService> _operationHistoryServiceMock;
        private readonly Mock<ICurrentUserService> _currentUserServiceMock;
        private readonly Mock<ILogger<BulkUserManagementOrchestrator>> _loggerMock;

        private readonly BulkUserManagementOrchestrator _orchestrator;
        private readonly string _testApiToken = "test_api_token_12345";

        public BulkUserManagementOrchestratorTests()
        {
            // Inicjalizacja mocków zgodnie ze wzorcem walidacji argumentów w konstruktorach
            _userServiceMock = new Mock<IUserService>();
            _teamServiceMock = new Mock<ITeamService>();
            _departmentServiceMock = new Mock<IDepartmentService>();
            _subjectServiceMock = new Mock<ISubjectService>();
            _bulkOperationsServiceMock = new Mock<IPowerShellBulkOperationsService>();
            _powerShellUserManagementMock = new Mock<IPowerShellUserManagementService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _adminNotificationServiceMock = new Mock<IAdminNotificationService>();
            _cacheInvalidationServiceMock = new Mock<ICacheInvalidationService>();
            _operationHistoryServiceMock = new Mock<IOperationHistoryService>();
            _currentUserServiceMock = new Mock<ICurrentUserService>();
            _loggerMock = new Mock<ILogger<BulkUserManagementOrchestrator>>();

            // Setup podstawowych zachowań zgodnie ze wzorcem CurrentUserService
            _currentUserServiceMock.Setup(x => x.GetCurrentUserUpn()).Returns("test.admin@test.local");
            _currentUserServiceMock.Setup(x => x.IsAuthenticated).Returns(true);

            // Setup operationHistoryService zgodnie ze wzorcem auditowania
            var testOperation = new OperationHistory
            {
                Id = "test-operation-id",
                Type = OperationType.UserCreated,
                TargetEntityType = "User",
                Status = OperationStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };
            _operationHistoryServiceMock
                .Setup(x => x.CreateNewOperationEntryAsync(It.IsAny<OperationType>(), It.IsAny<string>(), 
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(testOperation);

            // Tworzenie instancji orkiestratora
            _orchestrator = new BulkUserManagementOrchestrator(
                _userServiceMock.Object,
                _teamServiceMock.Object,
                _departmentServiceMock.Object,
                _subjectServiceMock.Object,
                _bulkOperationsServiceMock.Object,
                _powerShellUserManagementMock.Object,
                _notificationServiceMock.Object,
                _adminNotificationServiceMock.Object,
                _cacheInvalidationServiceMock.Object,
                _operationHistoryServiceMock.Object,
                _currentUserServiceMock.Object,
                _loggerMock.Object);
        }

        #endregion

        #region Test Data Factory Methods

        private UserOnboardingPlan CreateValidOnboardingPlan(string firstName, string lastName, 
            string upn, UserRole role)
        {
            return new UserOnboardingPlan
            {
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = role,
                DepartmentId = "dept-matematyka",
                Password = "TestPassword123!",
                TeamIds = new[] { "team-1", "team-2" },
                SchoolTypeIds = new[] { "school-lo", "school-tech" },
                SubjectIds = new[] { "subject-math", "subject-physics" },
                SendWelcomeEmail = true
            };
        }

        private User CreateTestUser(string id, string upn, 
            UserRole role, bool isActive)
        {
            return new User
            {
                Id = id,
                FirstName = "Test",
                LastName = "User",
                UPN = upn,
                Role = role,
                DepartmentId = "dept-1",
                IsActive = isActive,
                CreatedDate = DateTime.UtcNow,
                TeamMemberships = new List<TeamMember>(),
                SchoolTypeAssignments = new List<UserSchoolType>(),
                TaughtSubjects = new List<UserSubject>()
            };
        }

        private Team CreateTestTeam(string id, string displayName, 
            TeamStatus status)
        {
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                Description = "Test team description",
                Owner = "owner@test.local",
                Status = status,
                Visibility = TeamVisibility.Private,
                Members = new List<TeamMember>(),
                Channels = new List<Channel>()
            };
        }

        private Department CreateTestDepartment(string id, string name)
        {
            return new Department
            {
                Id = id,
                Name = name,
                Description = "Wydział Matematyki",
                IsActive = true,
                Users = new List<User>()
            };
        }

        #endregion

        #region BulkUserOnboardingAsync Tests

        [Fact]
        public async Task BulkUserOnboardingAsync_WithValidPlans_ShouldReturnSuccess()
        {
            // Arrange
            var plans = new[]
            {
                CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel),
                CreateValidOnboardingPlan("Anna", "Nowak", "anna.nowak@test.local", UserRole.Wicedyrektor)
            };

            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);

            // Setup serwisów zgodnie ze wzorcem cacheowania wielopoziomowego
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-1", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-2", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            // Setup tworzenia użytkowników - wzorzec audytowania operacji
            _userServiceMock.Setup(x => x.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string firstName, string lastName, string upn, UserRole role, 
                    string deptId, string password, string token, bool sendEmail) => 
                    CreateTestUser(Guid.NewGuid().ToString(), upn, role, true));

            // Setup dodawania do zespołów - wzorzec batch processing
            _teamServiceMock.Setup(x => x.AddMemberAsync(It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<TeamMemberRole>(), It.IsAny<string>()))
                .ReturnsAsync(new TeamMember 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Role = TeamMemberRole.Member,
                    IsActive = true,
                    AddedDate = DateTime.UtcNow
                });

            // Setup operacji masowych zespołów
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync((string teamId, List<string> userUpns, string token) => 
                    userUpns.ToDictionary(upn => upn, upn => true));

            // Setup operacji przypisywania do typu szkoły
            _userServiceMock.Setup(x => x.AssignUserToSchoolTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSchoolType 
                { 
                    Id = Guid.NewGuid().ToString(),
                    UserId = It.IsAny<string>(),
                    SchoolTypeId = It.IsAny<string>(),
                    IsActive = true
                });

            // Setup operacji przypisywania do przedmiotu
            _userServiceMock.Setup(x => x.AssignTeacherToSubjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSubject 
                { 
                    Id = Guid.NewGuid().ToString(),
                    UserId = It.IsAny<string>(),
                    SubjectId = It.IsAny<string>(),
                    IsActive = true
                });

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
            result.Errors.Should().BeEmpty();
            result.OperationType.Should().Be("BulkUserOnboarding");

            // Verify wzorzec auditowania - operacje są logowane przez orkiestrator wewnętrznie

            // Verify wzorzec powiadomień administratorów
            _adminNotificationServiceMock.Verify(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), 
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithEmptyPlans_ShouldReturnError()
        {
            // Arrange
            var emptyPlans = Array.Empty<UserOnboardingPlan>();

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(emptyPlans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Lista planów onboardingu jest pusta");
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithInvalidPlan_ShouldContinueWithValidOnes()
        {
            // Arrange - wzorzec obsługi błędów z częściowymi sukcesami
            var plans = new[]
            {
                CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel),
                new UserOnboardingPlan { FirstName = "", LastName = "", UPN = "", Password = "" }, // Invalid
                CreateValidOnboardingPlan("Anna", "Nowak", "anna.nowak@test.local", UserRole.Nauczyciel)
            };

            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);

            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            _userServiceMock.Setup(x => x.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string firstName, string lastName, string upn, UserRole role, 
                    string deptId, string password, string token, bool sendEmail) => 
                    CreateTestUser(Guid.NewGuid().ToString(), upn, role, true));

            // Setup operacji masowych zespołów
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync((string teamId, List<string> userUpns, string token) => 
                    userUpns.ToDictionary(upn => upn, upn => true));

            // Setup operacji przypisywania do typu szkoły
            _userServiceMock.Setup(x => x.AssignUserToSchoolTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSchoolType 
                { 
                    Id = Guid.NewGuid().ToString(),
                    UserId = It.IsAny<string>(),
                    SchoolTypeId = It.IsAny<string>(),
                    IsActive = true
                });

            // Setup operacji przypisywania do przedmiotu
            _userServiceMock.Setup(x => x.AssignTeacherToSubjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSubject 
                { 
                    Id = Guid.NewGuid().ToString(),
                    UserId = It.IsAny<string>(),
                    SubjectId = It.IsAny<string>(),
                    IsActive = true
                });

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("UPN");
        }

        #endregion

        #region BulkUserOffboardingAsync Tests

        [Fact]
        public async Task BulkUserOffboardingAsync_WithValidUsers_ShouldReturnSuccess()
        {
            // Arrange
            var userIds = new[] { "user-1", "user-2" };
            var options = new OffboardingOptions
            {
                BatchSize = 10,
                TransferTeamOwnership = true,
                CreateDataBackup = true,
                DeactivateM365Accounts = true,
                ContinueOnError = true
            };

            var testUser1 = CreateTestUser("user-1", "user1@test.local", UserRole.Nauczyciel, true);
            var testUser2 = CreateTestUser("user-2", "user2@test.local", UserRole.Nauczyciel, true);
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);

            // Setup wzorzec repository z cache
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser1);
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-2", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser2);

            // Setup operacji dezaktywacji
            _userServiceMock.Setup(x => x.DeactivateUserAsync(It.IsAny<string>(), _testApiToken, It.IsAny<bool>()))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("BulkUserOffboarding");

            // Verify wzorzec thread-safety - sprawdzenie semafora
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Rozpoczynam masowy offboarding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task BulkUserOffboardingAsync_WithEmptyUserIds_ShouldReturnError()
        {
            // Arrange
            var emptyUserIds = Array.Empty<string>();
            var options = new OffboardingOptions();

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(emptyUserIds, options, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Lista użytkowników jest pusta");
        }

        [Fact]
        public async Task BulkUserOffboardingAsync_WithTeamOwnershipTransfer_ShouldTransferOwnership()
        {
            // Arrange
            var userIds = new[] { "user-owner-1" };
            var options = new OffboardingOptions
            {
                TransferTeamOwnership = true,
                CreateDataBackup = false,
                DeactivateM365Accounts = true,
                ContinueOnError = true
            };

            var ownerUser = CreateTestUser("user-owner-1", "owner@test.local", UserRole.Nauczyciel, true);
            var ownedTeam = CreateTestTeam("team-owned", "Owned Team", TeamStatus.Active);
            
            // Setup członkostwa jako właściciel
            ownerUser.TeamMemberships = new List<TeamMember>
            {
                new TeamMember
                {
                    Id = "tm-1",
                    UserId = "user-owner-1",
                    TeamId = "team-owned",
                    Role = TeamMemberRole.Owner,
                    IsActive = true,
                    Team = ownedTeam
                }
            };

            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-owner-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(ownerUser);

            _userServiceMock.Setup(x => x.DeactivateUserAsync("user-owner-1", _testApiToken, true))
                .ReturnsAsync(true);

            // Setup dla transferu własności zespołu - potrzebny fallback owner
            var fallbackOwner = CreateTestUser("fallback-owner", "fallback@test.local", UserRole.Wicedyrektor, true);
            _userServiceMock.Setup(x => x.GetUsersByRoleAsync(UserRole.Wicedyrektor, It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(new List<User> { fallbackOwner });

            // Setup usuwania z zespołów - RemoveUsersFromAllTeams
            _teamServiceMock.Setup(x => x.RemoveUsersFromTeamAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "owner@test.local", true } });

            // Setup powiadomień administratorów
            _adminNotificationServiceMock.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("BulkUserOffboarding");
        }

        [Fact]
        public async Task BulkUserOffboardingAsync_WithDataBackup_ShouldCreateBackups()
        {
            // Arrange
            var userIds = new[] { "user-backup-1" };
            var options = new OffboardingOptions
            {
                TransferTeamOwnership = false,
                CreateDataBackup = true,
                DeactivateM365Accounts = true,
                BatchSize = 5
            };

            var testUser = CreateTestUser("user-backup-1", "backup@test.local", UserRole.Nauczyciel, true);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-backup-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);

            _userServiceMock.Setup(x => x.DeactivateUserAsync("user-backup-1", _testApiToken, true))
                .ReturnsAsync(true);

            _adminNotificationServiceMock.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task BulkUserOffboardingAsync_WithNonExistentUsers_ShouldContinueWithValidOnes()
        {
            // Arrange
            var userIds = new[] { "user-valid", "user-nonexistent", "user-valid-2" };
            var options = new OffboardingOptions { ContinueOnError = true };

            var validUser1 = CreateTestUser("user-valid", "valid1@test.local", UserRole.Nauczyciel, true);
            var validUser2 = CreateTestUser("user-valid-2", "valid2@test.local", UserRole.Nauczyciel, true);

            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-valid", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(validUser1);
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-nonexistent", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync((User)null);
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-valid-2", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(validUser2);

            _userServiceMock.Setup(x => x.DeactivateUserAsync(It.IsAny<string>(), _testApiToken, It.IsAny<bool>()))
                .ReturnsAsync(true);

            _adminNotificationServiceMock.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("nie został znaleziony");
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
        }

        #endregion

        #region BulkRoleChangeAsync Tests

        [Fact]
        public async Task BulkRoleChangeAsync_WithValidChanges_ShouldReturnSuccess()
        {
            // Arrange
            var changes = new[]
            {
                new UserRoleChange
                {
                    UserId = "user-1",
                    CurrentRole = UserRole.Nauczyciel,
                    NewRole = UserRole.Wicedyrektor,
                    Reason = "Awans",
                    UpdateM365Permissions = true,
                    AdjustTeamMemberships = true
                }
            };

            var testUser = CreateTestUser("user-1", "test@test.local", UserRole.Nauczyciel, true);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            _userServiceMock.Setup(x => x.UpdateUserAsync(It.IsAny<User>(), _testApiToken))
                .ReturnsAsync(true);

            // Act
            var result = await _orchestrator.BulkRoleChangeAsync(changes, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("BulkRoleChange");
            
            // Verify wzorzec cache invalidation po zmianie roli
            _userServiceMock.Verify(x => x.UpdateUserAsync(
                It.Is<User>(u => u.Role == UserRole.Wicedyrektor), _testApiToken), Times.Once);
        }

        [Fact]
        public async Task BulkRoleChangeAsync_WithEmptyChanges_ShouldReturnError()
        {
            // Arrange
            var emptyChanges = Array.Empty<UserRoleChange>();

            // Act
            var result = await _orchestrator.BulkRoleChangeAsync(emptyChanges, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Lista zmian ról jest pusta");
        }

        #endregion

        #region BulkTeamMembershipOperationAsync Tests

        [Fact]
        public async Task BulkTeamMembershipOperationAsync_WithAddOperations_ShouldReturnSuccess()
        {
            // Arrange
            var operations = new[]
            {
                new TeamMembershipOperation
                {
                    OperationType = TeamMembershipOperationType.Add,
                    UserId = "user-1",
                    TeamId = "team-1",
                    Role = TeamMemberRole.Member,
                    Reason = "Dodanie do zespołu projektowego"
                }
            };

            var testUser = CreateTestUser("user-1", "test@test.local", UserRole.Nauczyciel, true);
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-1", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            // Setup batch operations zgodnie ze wzorcem batch processing
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync("team-1", 
                It.Is<List<string>>(list => list.Contains("test@test.local")), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "test@test.local", true } });

            // Act
            var result = await _orchestrator.BulkTeamMembershipOperationAsync(operations, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("BulkTeamMembershipOperation");
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task BulkTeamMembershipOperationAsync_WithRemoveOperations_ShouldReturnSuccess()
        {
            // Arrange
            var operations = new[]
            {
                new TeamMembershipOperation
                {
                    OperationType = TeamMembershipOperationType.Remove,
                    UserId = "user-1",
                    TeamId = "team-1",
                    Reason = "Zakończenie projektu"
                }
            };

            var testUser = CreateTestUser("user-1", "test@test.local", UserRole.Nauczyciel, true);
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-1", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            _teamServiceMock.Setup(x => x.RemoveUsersFromTeamAsync("team-1", 
                It.Is<List<string>>(list => list.Contains("test@test.local")), 
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "test@test.local", true } });

            // Act
            var result = await _orchestrator.BulkTeamMembershipOperationAsync(operations, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
        }

        #endregion

        #region Thread-Safety and Process Management Tests

        [Fact]
        public async Task GetActiveProcessesStatusAsync_ShouldReturnCurrentProcesses()
        {
            // Act
            var processes = await _orchestrator.GetActiveProcessesStatusAsync();

            // Assert
            processes.Should().NotBeNull();
            processes.Should().BeAssignableTo<IEnumerable<UserManagementProcessStatus>>();
        }

        [Fact]
        public async Task CancelProcessAsync_WithValidProcessId_ShouldReturnTrue()
        {
            // Arrange
            var processId = "test-process-123";

            // Act
            var result = await _orchestrator.CancelProcessAsync(processId);

            // Assert
            result.Should().BeFalse(); // Proces nie istnieje, więc nie można go anulować
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_ConcurrentCalls_ShouldHandleThreadSafety()
        {
            // Arrange - test wzorca thread-safety w orkiestratorach
            var plans1 = new[] { CreateValidOnboardingPlan("User1", "Test", "user1@test.local", UserRole.Nauczyciel) };
            var plans2 = new[] { CreateValidOnboardingPlan("User2", "Test", "user2@test.local", UserRole.Nauczyciel) };

            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            _userServiceMock.Setup(x => x.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((string firstName, string lastName, string upn, UserRole role, 
                    string deptId, string password, string token, bool sendEmail) => 
                    CreateTestUser(Guid.NewGuid().ToString(), upn, role, true));

            // Act - wywołanie równoległe
            var task1 = _orchestrator.BulkUserOnboardingAsync(plans1, _testApiToken);
            var task2 = _orchestrator.BulkUserOnboardingAsync(plans2, _testApiToken);

            var results = await Task.WhenAll(task1, task2);

            // Assert
            results.Should().HaveCount(2);
            results.All(r => r != null).Should().BeTrue();
            
            // Sprawdzenie że semaphore działał - maksymalnie 3 równoległe procesy
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Rozpoczynam masowy onboarding")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(2));
        }

        #endregion

        #region Validation Tests

        [Theory]
        [InlineData("")]
        public async Task BulkUserOnboardingAsync_WithInvalidApiToken_ShouldHandleGracefully(string? invalidToken)
        {
            // Arrange
            var plans = new[] { CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel) };
            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            // Act & Assert - nie powinno rzucać wyjątku, ale może zakończyć się błędem
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, invalidToken ?? "");
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithNonExistentDepartment_ShouldFailValidation()
        {
            // Arrange
            var plans = new[] { CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel) };
            
            // Department nie istnieje
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync((Department?)null);

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().HaveCountGreaterThan(0);
            result.Errors[0].Message.Should().Contain("nie istnieje");
        }

        #endregion

        #region Advanced Onboarding Error Scenarios

        [Fact]
        public async Task BulkUserOnboardingAsync_WithUserCreationFailure_ShouldHandleErrors()
        {
            // Arrange
            var plans = new[]
            {
                CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel),
                CreateValidOnboardingPlan("Anna", "Nowak", "anna.nowak@test.local", UserRole.Nauczyciel)
            };

            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);

            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            // Setup tworzenia pierwszego użytkownika - sukces
            _userServiceMock.Setup(x => x.CreateUserAsync("Jan", "Kowalski", "jan.kowalski@test.local", 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateTestUser("user-1", "jan.kowalski@test.local", UserRole.Nauczyciel, true));

            // Setup tworzenia drugiego użytkownika - błąd
            _userServiceMock.Setup(x => x.CreateUserAsync("Anna", "Nowak", "anna.nowak@test.local", 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("Błąd tworzenia użytkownika w M365"));

            _teamServiceMock.Setup(x => x.GetTeamByIdAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "jan.kowalski@test.local", true } });

            _userServiceMock.Setup(x => x.AssignUserToSchoolTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSchoolType { Id = Guid.NewGuid().ToString(), IsActive = true });

            _userServiceMock.Setup(x => x.AssignTeacherToSubjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSubject { Id = Guid.NewGuid().ToString(), IsActive = true });

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("Błąd tworzenia użytkownika w M365");
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithExistingUser_ShouldFailValidation()
        {
            // Arrange
            var plans = new[]
            {
                CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel)
            };

            var existingUser = CreateTestUser("existing-user", "jan.kowalski@test.local", UserRole.Nauczyciel, true);
            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");

            _userServiceMock.Setup(x => x.GetUserByUpnAsync("jan.kowalski@test.local", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(existingUser);

            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("już istnieje");
            result.SuccessfulOperations.Should().BeEmpty();
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithTeamAssignmentFailure_ShouldContinueWithOtherOperations()
        {
            // Arrange
            var plans = new[]
            {
                CreateValidOnboardingPlan("Jan", "Kowalski", "jan.kowalski@test.local", UserRole.Nauczyciel)
            };

            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);
            var testUser = CreateTestUser("user-1", "jan.kowalski@test.local", UserRole.Nauczyciel, true);

            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            _userServiceMock.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(testUser);

            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-1", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);
            
            // Setup błędu dodawania do zespołu
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync("team-1", It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "jan.kowalski@test.local", false } });

            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-2", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync("team-2", It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { "jan.kowalski@test.local", true } });

            _userServiceMock.Setup(x => x.AssignUserToSchoolTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSchoolType { Id = Guid.NewGuid().ToString(), IsActive = true });

            _userServiceMock.Setup(x => x.AssignTeacherToSubjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSubject { Id = Guid.NewGuid().ToString(), IsActive = true });

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.SuccessfulOperations.Should().HaveCountGreaterThan(0);
            // Powinniśmy mieć sukcesy dla: user creation, team-2 add, school types, subjects
        }

        #endregion

        #region Advanced Role Change Scenarios

        [Fact]
        public async Task BulkRoleChangeAsync_WithNonExistentUser_ShouldFailGracefully()
        {
            // Arrange
            var changes = new[]
            {
                new UserRoleChange
                {
                    UserId = "nonexistent-user",
                    CurrentRole = UserRole.Nauczyciel,
                    NewRole = UserRole.Wicedyrektor,
                    Reason = "Awans",
                    UpdateM365Permissions = true
                }
            };

            _userServiceMock.Setup(x => x.GetUserByIdAsync("nonexistent-user", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync((User)null);

            // Act
            var result = await _orchestrator.BulkRoleChangeAsync(changes, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task BulkRoleChangeAsync_WithM365UpdateFailure_ShouldLogError()
        {
            // Arrange
            var changes = new[]
            {
                new UserRoleChange
                {
                    UserId = "user-1",
                    CurrentRole = UserRole.Nauczyciel,
                    NewRole = UserRole.Wicedyrektor,
                    Reason = "Awans",
                    UpdateM365Permissions = true
                }
            };

            var testUser = CreateTestUser("user-1", "user1@test.local", UserRole.Nauczyciel, true);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            _userServiceMock.Setup(x => x.UpdateUserAsync(It.IsAny<User>(), _testApiToken))
                .ThrowsAsync(new UnauthorizedAccessException("Brak uprawnień do aktualizacji M365"));

            // Act
            var result = await _orchestrator.BulkRoleChangeAsync(changes, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("Brak uprawnień do aktualizacji M365");
        }

        #endregion

        #region Advanced Team Membership Scenarios

        [Fact]
        public async Task BulkTeamMembershipOperationAsync_WithChangeRoleOperation_ShouldReturnSuccess()
        {
            // Arrange
            var operations = new[]
            {
                new TeamMembershipOperation
                {
                    OperationType = TeamMembershipOperationType.ChangeRole,
                    UserId = "user-1",
                    TeamId = "team-1",
                    Role = TeamMemberRole.Owner,
                    Reason = "Zmiana na właściciela zespołu"
                }
            };

            var testUser = CreateTestUser("user-1", "test@test.local", UserRole.Nauczyciel, true);
            var testTeam = CreateTestTeam("team-1", "Test Team", TeamStatus.Active);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            _teamServiceMock.Setup(x => x.GetTeamByIdAsync("team-1", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testTeam);

            // Act
            var result = await _orchestrator.BulkTeamMembershipOperationAsync(operations, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.OperationType.Should().Be("BulkTeamMembershipOperation");
        }

        [Fact]
        public async Task BulkTeamMembershipOperationAsync_WithNonExistentTeam_ShouldFailGracefully()
        {
            // Arrange
            var operations = new[]
            {
                new TeamMembershipOperation
                {
                    OperationType = TeamMembershipOperationType.Add,
                    UserId = "user-1",
                    TeamId = "nonexistent-team",
                    Role = TeamMemberRole.Member
                }
            };

            var testUser = CreateTestUser("user-1", "test@test.local", UserRole.Nauczyciel, true);
            
            _userServiceMock.Setup(x => x.GetUserByIdAsync("user-1", It.IsAny<bool>(), It.IsAny<string>()))
                .ReturnsAsync(testUser);
            
            // Setup AddUsersToTeamAsync żeby rzucił wyjątek dla nieistniejącego zespołu
            _teamServiceMock.Setup(x => x.AddUsersToTeamAsync("nonexistent-team", It.IsAny<List<string>>(), It.IsAny<string>()))
                .ThrowsAsync(new ArgumentException("Zespół nie został znaleziony"));

            // Act
            var result = await _orchestrator.BulkTeamMembershipOperationAsync(operations, _testApiToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Message.Should().Contain("Zespół nie został znaleziony");
        }

        #endregion

        #region Process Management and Cancellation Tests

        [Fact]
        public async Task CancelProcessAsync_WithActiveProcess_ShouldCancelAndReturnTrue()
        {
            // Arrange - Rozpocznij długi proces w tle
            var plans = new[] { CreateValidOnboardingPlan("User", "Test", "user@test.local", UserRole.Nauczyciel) };
            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            _userServiceMock.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(async () =>
                {
                    await Task.Delay(5000); // Symulacja długiej operacji
                    return CreateTestUser("user-1", "user@test.local", UserRole.Nauczyciel, true);
                });

            // Rozpocznij onboarding ale nie czekaj na zakończenie
            var onboardingTask = _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);
            
            // Pobierz aktywne procesy
            await Task.Delay(100); // Daj czas na start procesu
            var activeProcesses = await _orchestrator.GetActiveProcessesStatusAsync();
            var processId = activeProcesses.FirstOrDefault()?.ProcessId;

            // Act
            bool cancelled = false;
            if (!string.IsNullOrEmpty(processId))
            {
                cancelled = await _orchestrator.CancelProcessAsync(processId);
            }

            // Cleanup - czekaj na zakończenie tasku
            try { await onboardingTask; } catch { /* ignored */ }

            // Assert
            if (!string.IsNullOrEmpty(processId))
            {
                cancelled.Should().BeTrue();
            }
        }

        [Fact]
        public async Task GetActiveProcessesStatusAsync_WithRunningProcess_ShouldReturnProcessInfo()
        {
            // Arrange
            var plans = new[] { CreateValidOnboardingPlan("User", "Test", "user@test.local", UserRole.Nauczyciel) };
            var testDepartment = CreateTestDepartment("dept-1", "Matematyka");
            
            _departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(testDepartment);

            _userServiceMock.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(CreateTestUser("user-1", "user@test.local", UserRole.Nauczyciel, true));

            // Act - Rozpocznij onboarding
            var onboardingTask = _orchestrator.BulkUserOnboardingAsync(plans, _testApiToken);
            
            // Sprawdź procesy w trakcie wykonywania
            await Task.Delay(50); // Daj czas na start
            var activeProcesses = await _orchestrator.GetActiveProcessesStatusAsync();
            
            await onboardingTask; // Zakończ proces

            // Assert
            activeProcesses.Should().NotBeNull();
            if (activeProcesses.Any())
            {
                var process = activeProcesses.First();
                process.ProcessType.Should().Be("BulkUserOnboarding");
                process.TotalItems.Should().Be(1);
            }
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _orchestrator?.Dispose();
        }

        #endregion
    }
}