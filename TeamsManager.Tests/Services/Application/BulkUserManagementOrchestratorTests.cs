using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Application.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Abstractions.Services.Cache;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Services.Application
{
    /// <summary>
    /// Testy jednostkowe dla BulkUserManagementOrchestrator
    /// Pokrycie: masowe operacje na użytkownikach, onboarding, offboarding
    /// </summary>
    public class BulkUserManagementOrchestratorTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<IDepartmentService> _mockDepartmentService;
        private readonly Mock<ISubjectService> _mockSubjectService;
        private readonly Mock<IGraphBulkOperationsService> _mockGraphBulkOperationsService;
        private readonly Mock<IGraphUserManagementService> _mockGraphUserManagementService;
        private readonly Mock<IGraphService> _mockGraphService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IAdminNotificationService> _mockAdminNotificationService;
        private readonly Mock<ICacheInvalidationService> _mockCacheInvalidationService;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<BulkUserManagementOrchestrator>> _mockLogger;
        private readonly BulkUserManagementOrchestrator _orchestrator;

        public BulkUserManagementOrchestratorTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockTeamService = new Mock<ITeamService>();
            _mockDepartmentService = new Mock<IDepartmentService>();
            _mockSubjectService = new Mock<ISubjectService>();
            _mockGraphBulkOperationsService = new Mock<IGraphBulkOperationsService>();
            _mockGraphUserManagementService = new Mock<IGraphUserManagementService>();
            _mockGraphService = new Mock<IGraphService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockAdminNotificationService = new Mock<IAdminNotificationService>();
            _mockCacheInvalidationService = new Mock<ICacheInvalidationService>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<BulkUserManagementOrchestrator>>();

            _orchestrator = new BulkUserManagementOrchestrator(
                _mockUserService.Object,
                _mockTeamService.Object,
                _mockDepartmentService.Object,
                _mockSubjectService.Object,
                _mockGraphBulkOperationsService.Object,
                _mockGraphUserManagementService.Object,
                _mockGraphService.Object,
                _mockNotificationService.Object,
                _mockAdminNotificationService.Object,
                _mockCacheInvalidationService.Object,
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);

            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("admin@test.com");
        }

        #region BulkUserOnboardingAsync Tests

        [Fact]
        public async Task BulkUserOnboardingAsync_WithValidPlans_ShouldProcessAllUsers()
        {
            // Arrange
            var plans = new[]
            {
                CreateTestOnboardingPlan("John", "Doe", "john.doe@test.com", UserRole.Nauczyciel),
                CreateTestOnboardingPlan("Jane", "Smith", "jane.smith@test.com", UserRole.Uczen)
            };
            var accessToken = "valid-token";

            // Setup validation mocks
            foreach (var plan in plans)
            {
                // Mock user doesn't exist (validation passes) - using concrete parameters
                _mockUserService.Setup(x => x.GetUserByUpnAsync(plan.UPN, false, null))
                    .ReturnsAsync((User?)null);

                // Mock department exists (validation passes) - using exact parameters from implementation
                var department = new Department { Id = plan.DepartmentId, Name = "Test Department", IsActive = true };
                _mockDepartmentService.Setup(x => x.GetDepartmentByIdAsync(plan.DepartmentId, false, false, false))
                    .ReturnsAsync(department);

                // Setup successful user creation
                var user = CreateTestUser(Guid.NewGuid().ToString(), plan.FirstName, plan.LastName, plan.UPN);
                _mockUserService.Setup(x => x.CreateUserAsync(
                    plan.FirstName, plan.LastName, plan.UPN, plan.Role, plan.DepartmentId,
                    It.IsAny<string>(), accessToken, It.IsAny<bool>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<bool>()))
                    .ReturnsAsync(user);

                // Mock team membership operations
                _mockTeamService.Setup(x => x.AddUsersToTeamAsync(
                    It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                    .ReturnsAsync(new Dictionary<string, bool> { { user.UPN, true } });

                // Mock school type assignment for teachers
                _mockUserService.Setup(x => x.AssignUserToSchoolTypeAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                    It.IsAny<decimal?>(), It.IsAny<string>()))
                    .ReturnsAsync(new UserSchoolType { Id = "ust-123", UserId = user.Id });

                // Mock subject assignment for teachers
                _mockUserService.Setup(x => x.AssignTeacherToSubjectAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
                    .ReturnsAsync(new UserSubject { Id = "us-123", UserId = user.Id });
            }

            // Mock admin notifications
            _mockAdminNotificationService.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), 
                It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            // John (Nauczyciel): CreateUser + 2×AddToTeam + AssignToSchoolType = 4 operacje
            // Jane (Uczen): CreateUser + 2×AddToTeam = 3 operacje = Razem 7 operacji
            result.SuccessfulOperations.Should().HaveCount(7);
            result.Errors.Should().BeEmpty();
            
            // Verify specific operations
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "CreateUser" && op.EntityName == "John Doe");
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "CreateUser" && op.EntityName == "Jane Smith");
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "AddToTeam");
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "AssignToSchoolType");
        }

        [Fact]
        public async Task BulkUserOnboardingAsync_WithPartialFailures_ShouldReportMixedResults()
        {
            // Arrange
            var plans = new[]
            {
                CreateTestOnboardingPlan("John", "Doe", "john.doe@test.com", UserRole.Nauczyciel),
                CreateTestOnboardingPlan("Jane", "Smith", "invalid-email", UserRole.Uczen)
            };
            var accessToken = "valid-token";

            // Mock validation for first user (success path) - user doesn't exist, validation passes
            _mockUserService.Setup(x => x.GetUserByUpnAsync("john.doe@test.com", false, null))
                .ReturnsAsync((User?)null);

            // Mock validation for second user (should fail) - return existing user to trigger validation failure
            var existingUser = CreateTestUser("existing-user", "Existing", "User", "invalid-email");
            _mockUserService.Setup(x => x.GetUserByUpnAsync("invalid-email", false, null))
                .ReturnsAsync(existingUser);

            // Mock department exists for both users
            var department = new Department { Id = "dept-123", Name = "Test Department", IsActive = true };
            _mockDepartmentService.Setup(x => x.GetDepartmentByIdAsync("dept-123", false, false, false))
                .ReturnsAsync(department);

            // First user succeeds - complete mock setup for successful operations
            var successUser = CreateTestUser("user-1", "John", "Doe", "john.doe@test.com");
            _mockUserService.Setup(x => x.CreateUserAsync(
                "John", "Doe", "john.doe@test.com", UserRole.Nauczyciel, It.IsAny<string>(),
                It.IsAny<string>(), accessToken, It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>()))
                .ReturnsAsync(successUser);

            // Mock team operations for successful user
            _mockTeamService.Setup(x => x.AddUsersToTeamAsync(
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync(new Dictionary<string, bool> { { successUser.UPN, true } });

            // Mock school type assignment for teacher
            _mockUserService.Setup(x => x.AssignUserToSchoolTypeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(),
                It.IsAny<decimal?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserSchoolType { Id = "ust-123", UserId = successUser.Id });

            // Second user fails (CreateUserAsync won't be called due to validation failure)
            _mockUserService.Setup(x => x.CreateUserAsync(
                "Jane", "Smith", "invalid-email", UserRole.Uczen, It.IsAny<string>(),
                It.IsAny<string>(), accessToken, It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>()))
                .ReturnsAsync((User?)null);

            // Mock admin notifications
            _mockAdminNotificationService.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), 
                It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, accessToken);

            // Assert
            result.Should().NotBeNull();
            // Onboarding logic: IsSuccess = true if ANY operations succeed (even with validation errors)
            result.IsSuccess.Should().BeTrue(); // John succeeds, so overall success despite Jane's validation failure
            // John (success): CreateUser + 2×AddToTeam + AssignToSchoolType = 4 operacje
            // Jane (validation fails): 0 operacji
            result.SuccessfulOperations.Should().HaveCount(4);
            result.Errors.Should().HaveCount(1); // Validation failure for Jane
            
            // Verify successful operations are for John
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "CreateUser" && op.EntityName == "John Doe");
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "AddToTeam");
            result.SuccessfulOperations.Should().Contain(op => op.Operation == "AssignToSchoolType");
            
            // Verify error is for Jane's validation failure
            result.Errors.Should().Contain(err => err.Operation == "ValidateOnboardingPlan" && err.EntityId == "invalid-email");
        }

        [Theory]
        [InlineData("")]
        public async Task BulkUserOnboardingAsync_WithInvalidToken_ShouldReturnError(string invalidToken)
        {
            // Arrange
            var plans = new[] { CreateTestOnboardingPlan("John", "Doe", "john@test.com", UserRole.Uczen) };

            // Mock admin notifications
            _mockAdminNotificationService.Setup(x => x.SendBulkUsersOperationNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), 
                It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _orchestrator.BulkUserOnboardingAsync(plans, invalidToken);

            // Assert - Implementation doesn't validate token, so it should process and fail at Graph API level
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
        }

        #endregion

        #region BulkUserOffboardingAsync Tests

        [Fact]
        public async Task BulkUserOffboardingAsync_WithValidUsers_ShouldDeactivateAllUsers()
        {
            // Arrange
            var userIds = new[] { "user-1", "user-2", "user-3" };
            var options = new OffboardingOptions
            {
                TransferTeamOwnership = true,
                CreateDataBackup = false,
                DeactivateM365Accounts = true
            };
            var accessToken = "valid-token";

            // Setup successful deactivation for all users
            foreach (var userId in userIds)
            {
                var user = CreateTestUser(userId, "Test", "User", $"user{userId}@test.com");
                _mockUserService.Setup(x => x.GetUserByIdAsync(userId, false, null))
                    .ReturnsAsync(user);
                _mockUserService.Setup(x => x.DeactivateUserAsync(userId, accessToken, options.DeactivateM365Accounts))
                    .ReturnsAsync(true);
            }

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            result.SuccessfulOperations.Should().HaveCount(3);
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task BulkUserOffboardingAsync_WithNonExistentUsers_ShouldReportFailures()
        {
            // Arrange
            var userIds = new[] { "non-existent-1", "non-existent-2" };
            var options = new OffboardingOptions
            {
                DeactivateM365Accounts = true,
                TransferTeamOwnership = false
            };
            var accessToken = "valid-token";

            // Setup failed user retrieval
            foreach (var userId in userIds)
            {
                _mockUserService.Setup(x => x.GetUserByIdAsync(userId, false, null))
                    .ReturnsAsync((User?)null);
            }

            // Act
            var result = await _orchestrator.BulkUserOffboardingAsync(userIds, options, accessToken);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse();
            result.SuccessfulOperations.Should().BeEmpty();
            result.Errors.Should().HaveCount(2);
        }

        #endregion

        #region Helper Methods

        private static UserOnboardingPlan CreateTestOnboardingPlan(
            string firstName, string lastName, string upn, UserRole role)
        {
            return new UserOnboardingPlan
            {
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = role,
                DepartmentId = "dept-123",
                Password = "TempPassword123!",
                SendWelcomeEmail = true,
                TeamIds = new[] { "team-1", "team-2" },
                SchoolTypeIds = new[] { "school-type-1" }
            };
        }

        private static User CreateTestUser(string id, string firstName, string lastName, string upn)
        {
            return new User
            {
                Id = id,
                FirstName = firstName,
                LastName = lastName,
                UPN = upn,
                Role = UserRole.Uczen,
                DepartmentId = "dept-123",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "admin@test.com",
                IsActive = true
            };
        }

        #endregion
    }
} 