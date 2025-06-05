using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services.Cache;

namespace TeamsManager.Tests.Services.Cache
{
    /// <summary>
    /// Testy jednostkowe dla CacheInvalidationService
    /// ETAP 7/8: Systematyczna Inwalidacja Cache
    /// </summary>
    public class CacheInvalidationServiceTests
    {
        private readonly Mock<IPowerShellCacheService> _mockCacheService;
        private readonly Mock<ILogger<CacheInvalidationService>> _mockLogger;
        private readonly ICacheInvalidationService _service;
        
        public CacheInvalidationServiceTests()
        {
            _mockCacheService = new Mock<IPowerShellCacheService>();
            _mockLogger = new Mock<ILogger<CacheInvalidationService>>();
            _service = new CacheInvalidationService(_mockCacheService.Object, _mockLogger.Object);
        }
        
        // TEAM OPERATIONS TESTS
        
        [Fact]
        public async Task InvalidateForTeamCreated_Should_InvalidateCorrectKeys()
        {
            // Arrange
            var team = new Team
            {
                Id = "team123",
                Owner = "owner@test.com",
                SchoolYearId = "sy123",
                SchoolTypeId = "st123",
                ExternalId = "ext123"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForTeamCreatedAsync(team);
            
            // Assert
            Assert.Contains("Teams_AllActive", invalidatedKeys);
            Assert.Contains("Teams_Active", invalidatedKeys);
            Assert.Contains($"Teams_ByOwner_{team.Owner}", invalidatedKeys);
            Assert.Contains($"Team_Id_{team.Id}", invalidatedKeys);
            Assert.Contains($"Teams_BySchoolYear_{team.SchoolYearId}", invalidatedKeys);
            Assert.Contains($"Teams_BySchoolType_{team.SchoolTypeId}", invalidatedKeys);
            Assert.Contains("PowerShell_Teams_All", invalidatedKeys);
            Assert.Equal(7, invalidatedKeys.Distinct().Count());
            
            _mockCacheService.Verify(x => x.BatchInvalidateKeys(
                It.IsAny<IEnumerable<string>>(), 
                It.Is<string>(op => op.StartsWith("TeamCreated_"))), Times.Once);
        }
        
        [Fact]
        public async Task InvalidateForTeamUpdated_WithOwnerChange_Should_InvalidateBothOwners()
        {
            // Arrange
            var oldTeam = new Team { Id = "team123", Owner = "oldowner@test.com", Status = TeamStatus.Active };
            var newTeam = new Team { Id = "team123", Owner = "newowner@test.com", Status = TeamStatus.Active, ExternalId = "ext123" };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForTeamUpdatedAsync(newTeam, oldTeam);
            
            // Assert
            Assert.Contains($"Teams_ByOwner_{oldTeam.Owner}", invalidatedKeys);
            Assert.Contains($"Teams_ByOwner_{newTeam.Owner}", invalidatedKeys);
            Assert.Contains($"Team_Id_{newTeam.Id}", invalidatedKeys);
            Assert.Contains("Teams_AllActive", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForTeamUpdated_WithStatusChange_Should_InvalidateStatusKeys()
        {
            // Arrange
            var oldTeam = new Team { Id = "team123", Owner = "owner@test.com", Status = TeamStatus.Active };
            var newTeam = new Team { Id = "team123", Owner = "owner@test.com", Status = TeamStatus.Archived, ExternalId = "ext123" };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForTeamUpdatedAsync(newTeam, oldTeam);
            
            // Assert
            Assert.Contains("Teams_Active", invalidatedKeys);
            Assert.Contains("Teams_Archived", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForTeamArchived_Should_InvalidateCascadeKeys()
        {
            // Arrange
            var team = new Team
            {
                Id = "team123",
                Owner = "owner@test.com",
                ExternalId = "ext123",
                Members = new List<TeamMember>
                {
                    new TeamMember { TeamId = "team123", UserId = "user1", IsActive = true },
                    new TeamMember { TeamId = "team123", UserId = "user2", IsActive = true },
                    new TeamMember { TeamId = "team123", UserId = "user3", IsActive = false } // Nieaktywne
                }
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForTeamArchivedAsync(team);
            
            // Assert - podstawowe klucze
            Assert.Contains($"Team_Id_{team.Id}", invalidatedKeys);
            Assert.Contains("Teams_AllActive", invalidatedKeys);
            Assert.Contains("Teams_Active", invalidatedKeys);
            Assert.Contains("Teams_Archived", invalidatedKeys);
            Assert.Contains($"Teams_ByOwner_{team.Owner}", invalidatedKeys);
            Assert.Contains($"PowerShell_Team_{team.ExternalId}", invalidatedKeys);
            Assert.Contains($"PowerShell_TeamChannels_{team.Id}", invalidatedKeys);
            Assert.Contains($"Channels_TeamId_{team.Id}", invalidatedKeys);
            
            // Assert - klucze kaskadowe dla członków
            Assert.Contains("User_Teams_user1", invalidatedKeys);
            Assert.Contains("User_Teams_user2", invalidatedKeys);
            Assert.DoesNotContain("User_Teams_user3", invalidatedKeys); // Nieaktywne członkostwo
        }
        
        [Fact]
        public async Task InvalidateForTeamMembersBulkOperation_Should_InvalidateAllMembers()
        {
            // Arrange
            var teamId = "team123";
            var userIds = new List<string> { "user1", "user2", "user3" };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForTeamMembersBulkOperationAsync(teamId, userIds);
            
            // Assert
            Assert.Contains($"Team_Members_{teamId}", invalidatedKeys);
            Assert.Contains($"Team_Id_{teamId}", invalidatedKeys);
            Assert.Contains("User_Teams_user1", invalidatedKeys);
            Assert.Contains("User_Teams_user2", invalidatedKeys);
            Assert.Contains("User_Teams_user3", invalidatedKeys);
            
            _mockCacheService.Verify(x => x.BatchInvalidateKeys(
                It.IsAny<IEnumerable<string>>(), 
                It.Is<string>(op => op.Contains("TeamMembersBulk") && op.Contains("3"))), Times.Once);
        }
        
        // USER OPERATIONS TESTS
        
        [Fact]
        public async Task InvalidateForUserDeactivated_Should_InvalidateCascadeKeys()
        {
            // Arrange
            var user = new User
            {
                Id = "user123",
                UPN = "user@test.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = "dept123",
                ExternalId = "ext123",
                TaughtSubjects = new List<UserSubject>
                {
                    new UserSubject { SubjectId = "subj1", IsActive = true },
                    new UserSubject { SubjectId = "subj2", IsActive = true },
                    new UserSubject { SubjectId = "subj3", IsActive = false } // Nieaktywne
                },
                TeamMemberships = new List<TeamMember>
                {
                    new TeamMember { TeamId = "team1", IsActive = true },
                    new TeamMember { TeamId = "team2", IsActive = false } // Nieaktywne
                }
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForUserDeactivatedAsync(user);
            
            // Assert - podstawowe klucze
            Assert.Contains($"User_Id_{user.Id}", invalidatedKeys);
            Assert.Contains($"User_Upn_{user.UPN}", invalidatedKeys);
            Assert.Contains("Users_AllActive", invalidatedKeys);
            Assert.Contains($"Users_Role_{user.Role}", invalidatedKeys);
            
            // Assert - klucze kaskadowe
            Assert.Contains($"Department_UsersIn_Id_{user.DepartmentId}", invalidatedKeys);
            Assert.Contains("Subject_Teachers_Id_subj1", invalidatedKeys);
            Assert.Contains("Subject_Teachers_Id_subj2", invalidatedKeys);
            Assert.DoesNotContain("Subject_Teachers_Id_subj3", invalidatedKeys); // Nieaktywny przedmiot
            Assert.Contains("Team_Members_team1", invalidatedKeys);
            Assert.DoesNotContain("Team_Members_team2", invalidatedKeys); // Nieaktywne członkostwo
        }
        
        [Fact]
        public async Task InvalidateForUserCreated_Should_InvalidateBasicKeys()
        {
            // Arrange
            var user = new User
            {
                Id = "user123",
                UPN = "user@test.com",
                Role = UserRole.Dyrektor,
                DepartmentId = "dept123"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForUserCreatedAsync(user);
            
            // Assert
            Assert.Contains("Users_AllActive", invalidatedKeys);
            Assert.Contains($"Users_Role_{user.Role}", invalidatedKeys);
            Assert.Contains($"User_Id_{user.Id}", invalidatedKeys);
            Assert.Contains($"User_Upn_{user.UPN}", invalidatedKeys);
            Assert.Contains($"PowerShell_UserId_{user.UPN}", invalidatedKeys);
            Assert.Contains("PowerShell_M365Users_AccountEnabled_True", invalidatedKeys);
            Assert.Contains($"Department_UsersIn_Id_{user.DepartmentId}", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForUserUpdated_WithRoleChange_Should_InvalidateBothRoles()
        {
            // Arrange
            var oldUser = new User { Id = "user123", Role = UserRole.Nauczyciel, DepartmentId = "dept1" };
            var newUser = new User { Id = "user123", UPN = "user@test.com", Role = UserRole.Wicedyrektor, DepartmentId = "dept2", ExternalId = "ext123" };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForUserUpdatedAsync(newUser, oldUser);
            
            // Assert
            Assert.Contains($"Users_Role_{oldUser.Role}", invalidatedKeys);
            Assert.Contains($"Users_Role_{newUser.Role}", invalidatedKeys);
            Assert.Contains($"Department_UsersIn_Id_{oldUser.DepartmentId}", invalidatedKeys);
            Assert.Contains($"Department_UsersIn_Id_{newUser.DepartmentId}", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForUserSubjectChanged_Should_InvalidateSubjectAndUser()
        {
            // Arrange
            var userId = "user123";
            var subjectId = "subj123";
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act - dodanie przedmiotu
            await _service.InvalidateForUserSubjectChangedAsync(userId, subjectId, true);
            
            // Assert
            Assert.Contains($"User_Id_{userId}", invalidatedKeys);
            Assert.Contains($"Subject_Teachers_Id_{subjectId}", invalidatedKeys);
            Assert.Contains($"Users_Role_{UserRole.Nauczyciel}", invalidatedKeys);
            
            _mockCacheService.Verify(x => x.BatchInvalidateKeys(
                It.IsAny<IEnumerable<string>>(), 
                It.Is<string>(op => op.Contains("UserSubjectAdded"))), Times.Once);
        }
        
        // CHANNEL OPERATIONS TESTS
        
        [Fact]
        public async Task InvalidateForChannelCreated_Should_InvalidateChannelKeys()
        {
            // Arrange
            var channel = new Channel
            {
                Id = "channel123", // GraphID
                TeamId = "team123",
                DisplayName = "Test Channel"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForChannelCreatedAsync(channel);
            
            // Assert
            Assert.Contains($"Channels_TeamId_{channel.TeamId}", invalidatedKeys);
            Assert.Contains($"Channel_Id_{channel.Id}", invalidatedKeys);
            Assert.Contains($"PowerShell_TeamChannels_{channel.TeamId}", invalidatedKeys);
            Assert.Contains($"Channel_GraphId_{channel.Id}", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForChannelDeleted_Should_InvalidateAllChannelKeys()
        {
            // Arrange
            var channel = new Channel
            {
                Id = "channel123",
                TeamId = "team123",
                DisplayName = "Test Channel"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForChannelDeletedAsync(channel);
            
            // Assert
            Assert.Contains($"Channel_Id_{channel.Id}", invalidatedKeys);
            Assert.Contains($"Channels_TeamId_{channel.TeamId}", invalidatedKeys);
            Assert.Contains($"PowerShell_TeamChannels_{channel.TeamId}", invalidatedKeys);
            Assert.Contains($"Channel_GraphId_{channel.Id}", invalidatedKeys);
        }
        
        // DEPARTMENT AND SUBJECT TESTS
        
        [Fact]
        public async Task InvalidateForDepartmentChanged_Should_InvalidateDepartmentKeys()
        {
            // Arrange
            var department = new Department
            {
                Id = "dept123",
                Name = "Test Department",
                ParentDepartmentId = "parent123"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForDepartmentChangedAsync(department);
            
            // Assert
            Assert.Contains($"Department_Id_{department.Id}", invalidatedKeys);
            Assert.Contains("Departments_All", invalidatedKeys);
            Assert.Contains("Departments_Active", invalidatedKeys);
            Assert.Contains($"Department_Sub_ParentId_{department.ParentDepartmentId}", invalidatedKeys);
            Assert.Contains($"Department_UsersIn_Id_{department.Id}", invalidatedKeys);
        }
        
        [Fact]
        public async Task InvalidateForSubjectChanged_Should_InvalidateSubjectKeys()
        {
            // Arrange
            var subject = new Subject
            {
                Id = "subj123",
                Name = "Test Subject"
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => invalidatedKeys.AddRange(keys));
            
            // Act
            await _service.InvalidateForSubjectChangedAsync(subject);
            
            // Assert
            Assert.Contains($"Subject_Id_{subject.Id}", invalidatedKeys);
            Assert.Contains("Subjects_All", invalidatedKeys);
            Assert.Contains("Subjects_Active", invalidatedKeys);
            Assert.Contains($"Subject_Teachers_Id_{subject.Id}", invalidatedKeys);
        }
        
        // BATCH OPERATIONS TESTS
        
        [Fact]
        public async Task InvalidateBatchAsync_Should_CombineAllKeys()
        {
            // Arrange
            var operationsMap = new Dictionary<string, List<string>>
            {
                ["Operation1"] = new List<string> { "Key1", "Key2", "Key3" },
                ["Operation2"] = new List<string> { "Key4", "Key5" },
                ["Operation3"] = new List<string> { "Key6", "Key2" } // Duplicate Key2
            };
            
            var invalidatedKeys = new List<string>();
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Callback<IEnumerable<string>, string>((keys, op) => 
                {
                    Console.WriteLine($"DEBUG: BatchInvalidateKeys called with {keys.Count()} keys: {string.Join(", ", keys)}");
                    invalidatedKeys.AddRange(keys);
                });
            
            // Act
            await _service.InvalidateBatchAsync(operationsMap);
            
            // Assert - sprawdzamy rzeczywistą liczbę kluczy
            // Oczekiwane: Key1, Key2, Key3, Key4, Key5, Key6 (6 unikalnych)
            // Implementacja usuwa duplikaty, więc powinno być 6 kluczy
            var expectedUniqueKeys = new[] { "Key1", "Key2", "Key3", "Key4", "Key5", "Key6" };
            
            // Debug: sprawdź co faktycznie otrzymujemy
            var actualKeys = string.Join(", ", invalidatedKeys);
            
            Assert.Equal(expectedUniqueKeys.Length, invalidatedKeys.Count);
            
            foreach (var key in expectedUniqueKeys)
            {
                Assert.Contains(key, invalidatedKeys);
            }
            
            _mockCacheService.Verify(x => x.BatchInvalidateKeys(
                It.IsAny<IEnumerable<string>>(), 
                It.Is<string>(op => op.Contains("BatchOperation_Operation1_Operation2_Operation3"))), Times.Once);
        }
        
        // ERROR HANDLING TESTS
        
        [Fact]
        public async Task InvalidateForTeamCreated_WhenCacheServiceThrows_Should_PropagateException()
        {
            // Arrange
            var team = new Team { Id = "team123", Owner = "owner@test.com" };
            
            _mockCacheService
                .Setup(x => x.BatchInvalidateKeys(It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Cache service error"));
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.InvalidateForTeamCreatedAsync(team));
        }
        
        // LOGGING TESTS
        
        [Fact]
        public async Task InvalidateForTeamCreated_Should_LogOperation()
        {
            // Arrange
            var team = new Team { Id = "team123", Owner = "owner@test.com" };
            
            // Act
            await _service.InvalidateForTeamCreatedAsync(team);
            
            // Assert - sprawdź czy zostały wywołane metody logowania
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting batch invalidation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
                
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Completed batch invalidation")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        // CONSTRUCTOR TESTS
        
        [Fact]
        public void Constructor_WithNullCacheService_Should_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new CacheInvalidationService(null, _mockLogger.Object));
        }
        
        [Fact]
        public void Constructor_WithNullLogger_Should_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new CacheInvalidationService(_mockCacheService.Object, null));
        }
    }
}