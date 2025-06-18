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
    /// Testy jednostkowe dla UserService
    /// Pokrycie: CRUD użytkowników, role, przypisania do szkół i przedmiotów
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IGenericRepository<UserSchoolType>> _mockUserSchoolTypeRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<ISubjectRepository> _mockSubjectRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly Mock<ISubjectService> _mockSubjectService;
        private readonly Mock<IGraphService> _mockGraphService;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IAdminNotificationService> _mockAdminNotificationService;
        private readonly Mock<IGraphSynchronizer<User, GraphUser>> _mockUserSynchronizer;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserSchoolTypeRepository = new Mock<IGenericRepository<UserSchoolType>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockSubjectRepository = new Mock<ISubjectRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _mockSubjectService = new Mock<ISubjectService>();
            _mockGraphService = new Mock<IGraphService>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockAdminNotificationService = new Mock<IAdminNotificationService>();
            _mockUserSynchronizer = new Mock<IGraphSynchronizer<User, GraphUser>>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _userService = new UserService(
                _mockUserRepository.Object,
                _mockDepartmentRepository.Object,
                _mockUserSchoolTypeRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockSubjectRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockSubjectService.Object,
                _mockGraphService.Object,
                _mockOperationHistoryService.Object,
                _mockGraphCacheService.Object,
                _mockNotificationService.Object,
                _mockAdminNotificationService.Object,
                _mockUserSynchronizer.Object,
                _mockUnitOfWork.Object);

            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("admin@test.com");
        }

        #region GetUserByUpnAsync Tests

        [Fact]
        public async Task GetUserByUpnAsync_WithValidUpn_ShouldReturnUser()
        {
            // Arrange
            var upn = "john.doe@test.com";
            var expectedUser = CreateTestUser("user-123", "John", "Doe", upn);

            // Prostsze podejście - nie mock'ujemy cache, tylko główne zależności
            _mockUserRepository.Setup(x => x.GetUserByUpnAsync(upn))
                .ReturnsAsync(expectedUser);

            _mockUserRepository.Setup(x => x.GetByIdAsync(expectedUser.Id))
                .ReturnsAsync(expectedUser);

            // Act
            var result = await _userService.GetUserByUpnAsync(upn);

            // Assert
            result.Should().NotBeNull();
            result!.UPN.Should().Be(upn);
            result.FirstName.Should().Be("John");
            result.LastName.Should().Be("Doe");
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid-email")]
        [InlineData("nonexistent@test.com")]
        public async Task GetUserByUpnAsync_WithInvalidUpn_ShouldReturnNull(string invalidUpn)
        {
            // Arrange
            _mockUserRepository.Setup(x => x.GetUserByUpnAsync(invalidUpn))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.GetUserByUpnAsync(invalidUpn);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region CreateUserAsync Tests

        [Fact]
        public async Task CreateUserAsync_WithValidData_ShouldCreateUserSuccessfully()
        {
            // Arrange
            var firstName = "Jane";
            var lastName = "Smith";
            var upn = "jane.smith@test.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";
            var password = "SecurePassword123!";
            var accessToken = "valid-token";

            var operation = new OperationHistory { Id = "op-123" };
            var graphUser = new GraphUser { Id = "graph-user-123", UserPrincipalName = upn };
            var department = new Department 
            { 
                Id = departmentId, 
                Name = "Test Department", 
                IsActive = true, 
                CreatedDate = DateTime.UtcNow, 
                CreatedBy = "test@test.com" 
            };

            // Mock operation history
            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.UserCreated, nameof(User), null, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Mock repository calls
            _mockUserRepository.Setup(x => x.GetUserByUpnAsync(upn))
                .ReturnsAsync((User?)null); // No existing user

            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(department);

            // Mock Graph diagnostic
            var mockConnection = new Mock<IGraphConnectionService>();
            mockConnection.Setup(x => x.GetDiagnosticInfoAsync())
                .ReturnsAsync(new GraphDiagnosticInfo { Status = GraphHealthStatus.Healthy });

            _mockGraphService.Setup(x => x.Connection)
                .Returns(mockConnection.Object);

            // Mock Graph API user creation
            _mockGraphService.Setup(x => x.ExecuteWithAutoConnectAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<GraphUser>>>(),
                It.IsAny<string>()))
                .Returns(Task.FromResult(GraphOperationResult<GraphUser>.CreateSuccess(graphUser)));

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask);

            // Mock notifications
            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _mockAdminNotificationService.Setup(x => x.SendUserCreatedNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.CreateUserAsync(
                firstName, lastName, upn, role, departmentId, password, accessToken);

            // Assert
            result.Should().NotBeNull();
            result!.FirstName.Should().Be(firstName);
            result.LastName.Should().Be(lastName);
            result.UPN.Should().Be(upn);
            result.Role.Should().Be(role);
            result.DepartmentId.Should().Be(departmentId);

            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Once);
        }

        [Theory]
        [InlineData("", "Smith", "jane@test.com")]
        [InlineData("Jane", "", "jane@test.com")]
        [InlineData("Jane", "Smith", "")]
        [InlineData("Jane", "Smith", "invalid-email")]
        public async Task CreateUserAsync_WithInvalidData_ShouldReturnNull(
            string firstName, string lastName, string upn)
        {
            // Arrange
            var role = UserRole.Uczen;
            var departmentId = "dept-123";
            var password = "password";
            var accessToken = "token";

            var operation = new OperationHistory { Id = "op-123" };
            
            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.UserCreated, "User", null, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            // Act
            var result = await _userService.CreateUserAsync(
                firstName, lastName, upn, role, departmentId, password, accessToken);

            // Assert
            result.Should().BeNull();
            _mockUserRepository.Verify(x => x.AddAsync(It.IsAny<User>()), Times.Never);
        }

        #endregion

        #region GetUsersByRoleAsync Tests

        [Fact]
        public async Task GetUsersByRoleAsync_WithValidRole_ShouldReturnUsersWithThatRole()
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var teachers = CreateTestUsersWithRoles().Where(u => u.Role == role);

            _mockUserRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(CreateTestUsersWithRoles().Where(u => u.Role == role));

            // Act
            var result = await _userService.GetUsersByRoleAsync(role);

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(u => u.Role == role);
        }

        #endregion

        #region AssignUserToSchoolTypeAsync Tests

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_WithValidData_ShouldCreateAssignment()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "school-type-123";
            var assignedDate = DateTime.Today;

            var user = CreateTestUser(userId, "John", "Doe", "john@test.com");
            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };

            var operation = new OperationHistory { Id = "op-123" };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId))
                .ReturnsAsync(user);

            _mockSchoolTypeRepository.Setup(x => x.GetByIdAsync(schoolTypeId))
                .ReturnsAsync(schoolType);

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.UserSchoolTypeAssigned, nameof(UserSchoolType), null, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockUserSchoolTypeRepository.Setup(x => x.AddAsync(It.IsAny<UserSchoolType>()))
                .Returns(Task.CompletedTask);

            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(
                userId, schoolTypeId, assignedDate);

            // Assert
            result.Should().NotBeNull();
            result!.UserId.Should().Be(userId);
            result.SchoolTypeId.Should().Be(schoolTypeId);
            result.AssignedDate.Should().Be(assignedDate);
            result.IsCurrentlyActive.Should().BeTrue();

            _mockUserSchoolTypeRepository.Verify(x => x.AddAsync(It.IsAny<UserSchoolType>()), Times.Once);
        }

        #endregion

        #region AssignTeacherToSubjectAsync Tests

        [Fact]
        public async Task AssignTeacherToSubjectAsync_WithValidData_ShouldCreateAssignment()
        {
            // Arrange
            var teacherId = "teacher-123";
            var subjectId = "subject-123";
            var assignedDate = DateTime.Today;
            var notes = "Main teacher";

            var teacher = CreateTestUser(teacherId, "John", "Teacher", "john.teacher@test.com");
            teacher.Role = UserRole.Nauczyciel;
            teacher.TaughtSubjects = new List<UserSubject>(); // Inicjalizacja kolekcji

            var subject = new Subject
            {
                Id = subjectId,
                Name = "Mathematics",
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };

            var operation = new OperationHistory { Id = "op-123" };

            // Setup mockowania
            _mockUserRepository.Setup(x => x.GetByIdAsync(teacherId))
                .ReturnsAsync(teacher);

            _mockSubjectRepository.Setup(x => x.GetByIdAsync(subjectId))
                .ReturnsAsync(subject);

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.UserSubjectAssigned, nameof(UserSubject), null, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            _mockUserSubjectRepository.Setup(x => x.AddAsync(It.IsAny<UserSubject>()))
                .Returns(Task.CompletedTask);

            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _mockSubjectService.Setup(x => x.InvalidateTeachersCacheForSubjectAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(
                teacherId, subjectId, assignedDate, notes);

            // Assert
            result.Should().NotBeNull();
            result!.UserId.Should().Be(teacherId);
            result.SubjectId.Should().Be(subjectId);
            result.AssignedDate.Should().Be(assignedDate);
            result.Notes.Should().Be(notes);

            _mockUserSubjectRepository.Verify(x => x.AddAsync(It.IsAny<UserSubject>()), Times.Once);
        }

        #endregion

        #region DeactivateUserAsync Tests

        /*
        [Fact]
        public async Task DeactivateUserAsync_WithValidUser_ShouldDeactivateSuccessfully()
        {
            // Arrange
            var userId = "user-123";
            var accessToken = "valid-token";
            var user = CreateTestUser(userId, "John", "Doe", "john@test.com");

            var operation = new OperationHistory { Id = "op-123" };

            // Setup mockowania - poprawione aby używać FindAsync zamiast GetByIdAsync
            _mockUserRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.UserDeactivated, nameof(User), userId, It.IsAny<string>(), null, null))
                .ReturnsAsync(operation);

            _mockOperationHistoryService.Setup(x => x.UpdateOperationStatusAsync(
                It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // _mockGraphService.Setup(x => x.ExecuteWithAutoConnectAsync(
            //     It.IsAny<string>(),
            //     It.IsAny<Func<Task<GraphOperationResult<bool>>>>(),
            //     It.IsAny<string>()))
            //     .Returns(Task.FromResult(GraphOperationResult<bool>.CreateSuccess(true)));

            _mockUserRepository.Setup(x => x.Update(It.IsAny<User>()));

            _mockNotificationService.Setup(x => x.SendNotificationToUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.DeactivateUserAsync(userId, accessToken);

            // Assert
            result.Should().BeTrue();
            user.IsActive.Should().BeFalse();
            _mockUserRepository.Verify(x => x.Update(It.IsAny<User>()), Times.Once);
        }
        */

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
                Role = UserRole.Uczen,
                DepartmentId = "dept-123",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com",
                IsActive = true,
                // Inicjalizacja kolekcji aby uniknąć NullReferenceException
                SchoolTypeAssignments = new List<UserSchoolType>(),
                TaughtSubjects = new List<UserSubject>()
            };
        }

        private static List<User> CreateTestUsersWithRoles()
        {
            var user1 = CreateTestUser("1", "John", "Teacher", "john.teacher@test.com");
            user1.Role = UserRole.Nauczyciel;
            
            var user2 = CreateTestUser("2", "Jane", "Student", "jane.student@test.com");
            user2.Role = UserRole.Uczen;
            
            var user3 = CreateTestUser("3", "Bob", "Admin", "bob.admin@test.com");
            user3.Role = UserRole.Administrator;
            
            var user4 = CreateTestUser("4", "Alice", "Teacher2", "alice.teacher@test.com");
            user4.Role = UserRole.Nauczyciel;
            
            return new List<User> { user1, user2, user3, user4 };
        }

        #endregion
    }
} 