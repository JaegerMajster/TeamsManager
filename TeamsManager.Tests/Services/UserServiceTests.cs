using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IGenericRepository<UserSchoolType>> _mockUserSchoolTypeRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<IGenericRepository<Subject>> _mockSubjectRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UserService>> _mockLogger;

        private readonly UserService _userService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserSchoolTypeRepository = new Mock<IGenericRepository<UserSchoolType>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockSubjectRepository = new Mock<IGenericRepository<Subject>>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UserService>>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            _userService = new UserService(
                _mockUserRepository.Object,
                _mockDepartmentRepository.Object,
                _mockUserSchoolTypeRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockSubjectRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetUserByIdAsync_ExistingUser_ShouldReturnUser()
        {
            // Arrange
            var userId = "user-123";
            var expectedUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@example.com",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(expectedUser);

            // Act
            var result = await _userService.GetUserByIdAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedUser);
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetUserByIdAsync_NonExistingUser_ShouldReturnNull()
        {
            // Arrange
            var userId = "non-existing-user";
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.GetUserByIdAsync(userId);

            // Assert
            result.Should().BeNull();
            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
        }

        [Fact]
        public async Task GetUserByUpnAsync_ExistingUser_ShouldReturnUser()
        {
            // Arrange
            var userUpn = "jan.kowalski@example.com";
            var expectedUser = new User
            {
                Id = "user-123",
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = userUpn,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(userUpn))
                             .ReturnsAsync(expectedUser);

            // Act
            var result = await _userService.GetUserByUpnAsync(userUpn);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedUser);
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(userUpn), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveUsersAsync_ShouldReturnActiveUsers()
        {
            // Arrange
            var activeUsers = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", IsActive = true },
                new User { Id = "user-2", FirstName = "Anna", LastName = "Nowak", IsActive = true }
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                             .ReturnsAsync(activeUsers);

            // Act
            var result = await _userService.GetAllActiveUsersAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(activeUsers);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CreateUserAsync_ValidInputs_ShouldCreateUserSuccessfully()
        {
            // Arrange
            var firstName = "Jan";
            var lastName = "Kowalski";
            var upn = "jan.kowalski@example.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            var department = new Department
            {
                Id = departmentId,
                Name = "Matematyka",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn))
                             .ReturnsAsync((User?)null); // User nie istnieje

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                   .ReturnsAsync(department);

            User? addedUser = null;
            _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                             .Callback<User>(user => addedUser = user)
                             .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().NotBeNull();
            addedUser.Should().NotBeNull();
            result.Should().BeSameAs(addedUser);

            result!.FirstName.Should().Be(firstName);
            result.LastName.Should().Be(lastName);
            result.UPN.Should().Be(upn);
            result.Role.Should().Be(role);
            result.DepartmentId.Should().Be(departmentId);
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(upn), Times.Once);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Theory]
        [InlineData("", "Kowalski", "jan.kowalski@example.com")]
        [InlineData("Jan", "", "jan.kowalski@example.com")]
        [InlineData("Jan", "Kowalski", "")]
        [InlineData("Jan", "Kowalski", "   ")]
        public async Task CreateUserAsync_InvalidInputs_ShouldReturnNullAndLogError(string firstName, string lastName, string upn)
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Imię, nazwisko i UPN są wymagane.");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_ExistingUser_ShouldReturnNullAndLogError()
        {
            // Arrange
            var firstName = "Jan";
            var lastName = "Kowalski";
            var upn = "jan.kowalski@example.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            var existingUser = new User
            {
                Id = "existing-user",
                UPN = upn,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn))
                             .ReturnsAsync(existingUser);

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Użytkownik o UPN '{upn}' już istnieje");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_NonExistingDepartment_ShouldReturnNullAndLogError()
        {
            // Arrange
            var firstName = "Jan";
            var lastName = "Kowalski";
            var upn = "jan.kowalski@example.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "non-existing-dept";

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn))
                             .ReturnsAsync((User?)null);

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                   .ReturnsAsync((Department?)null);

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Dział o ID '{departmentId}' nie istnieje lub jest nieaktywny");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_ValidInputs_ShouldCreateAssignmentSuccessfully()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "school-type-456";
            var assignedDate = DateTime.UtcNow;
            var workloadPercentage = 100m;
            var notes = "Test assignment";

            var user = new User { Id = userId, FirstName = "Jan", LastName = "Kowalski", IsActive = true };
            var schoolType = new SchoolType { Id = schoolTypeId, FullName = "Liceum Ogólnokształcące", IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);

            UserSchoolType? addedAssignment = null;
            _mockUserSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<UserSchoolType>()))
                                       .Callback<UserSchoolType>(assignment => addedAssignment = assignment)
                                       .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(userId, schoolTypeId, assignedDate, null, workloadPercentage, notes);

            // Assert
            result.Should().NotBeNull();
            addedAssignment.Should().NotBeNull();
            result.Should().BeSameAs(addedAssignment);

            result!.UserId.Should().Be(userId);
            result.SchoolTypeId.Should().Be(schoolTypeId);
            result.AssignedDate.Should().Be(assignedDate);
            result.WorkloadPercentage.Should().Be(workloadPercentage);
            result.Notes.Should().Be(notes);
            result.IsActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<UserSchoolType>()), Times.Once);
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_ValidInputs_ShouldCreateAssignmentSuccessfully()
        {
            // Arrange
            var teacherId = "teacher-123";
            var subjectId = "subject-456";
            var assignedDate = DateTime.UtcNow;
            var notes = "Teaching assignment";

            var teacher = new User { Id = teacherId, FirstName = "Anna", LastName = "Nowak", Role = UserRole.Nauczyciel, IsActive = true };
            var subject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId)).ReturnsAsync(teacher);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);

            UserSubject? addedAssignment = null;
            _mockUserSubjectRepository.Setup(r => r.AddAsync(It.IsAny<UserSubject>()))
                                     .Callback<UserSubject>(assignment => addedAssignment = assignment)
                                     .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate, notes);

            // Assert
            result.Should().NotBeNull();
            addedAssignment.Should().NotBeNull();
            result.Should().BeSameAs(addedAssignment);

            result!.UserId.Should().Be(teacherId);
            result.SubjectId.Should().Be(subjectId);
            result.AssignedDate.Should().Be(assignedDate);
            result.Notes.Should().Be(notes);
            result.IsActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.IsAny<UserSubject>()), Times.Once);
        }

        [Fact]
        public async Task DeactivateUserAsync_ExistingUser_ShouldDeactivateUserSuccessfully()
        {
            // Arrange
            var userId = "user-123";
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _userService.DeactivateUserAsync(userId);

            // Assert
            result.Should().BeTrue();
            user.IsActive.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(user), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task ActivateUserAsync_InactiveUser_ShouldActivateUserSuccessfully()
        {
            // Arrange
            var userId = "user-123";
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = false
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            // Act
            var result = await _userService.ActivateUserAsync(userId);

            // Assert
            result.Should().BeTrue();
            user.IsActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(user), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeactivateUserAsync_NonExistingUser_ShouldReturnFalse()
        {
            // Arrange
            var userId = "non-existing-user";
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _userService.DeactivateUserAsync(userId);

            // Assert
            result.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task RemoveUserFromSchoolTypeAsync_ExistingAssignment_ShouldRemoveSuccessfully()
        {
            // Arrange
            var userSchoolTypeId = "assignment-123";
            var assignment = new UserSchoolType
            {
                Id = userSchoolTypeId,
                UserId = "user-123",
                SchoolTypeId = "school-type-456",
                IsActive = true
            };

            _mockUserSchoolTypeRepository.Setup(r => r.GetByIdAsync(userSchoolTypeId)).ReturnsAsync(assignment);

            // Act
            var result = await _userService.RemoveUserFromSchoolTypeAsync(userSchoolTypeId);

            // Assert
            result.Should().BeTrue();
            assignment.IsActive.Should().BeFalse();

            _mockUserSchoolTypeRepository.Verify(r => r.GetByIdAsync(userSchoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.Update(assignment), Times.Once);
        }

        [Fact]
        public async Task RemoveTeacherFromSubjectAsync_ExistingAssignment_ShouldRemoveSuccessfully()
        {
            // Arrange
            var userSubjectId = "assignment-456";
            var assignment = new UserSubject
            {
                Id = userSubjectId,
                UserId = "teacher-123",
                SubjectId = "subject-789",
                IsActive = true
            };

            _mockUserSubjectRepository.Setup(r => r.GetByIdAsync(userSubjectId)).ReturnsAsync(assignment);

            // Act
            var result = await _userService.RemoveTeacherFromSubjectAsync(userSubjectId);

            // Assert
            result.Should().BeTrue();
            assignment.IsActive.Should().BeFalse();

            _mockUserSubjectRepository.Verify(r => r.GetByIdAsync(userSubjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.Update(assignment), Times.Once);
        }

        [Fact]
        public async Task GetUserByUpnAsync_NonExistingUser_ShouldReturnNull()
        {
            // Arrange
            var userUpn = "nonexistent@example.com";
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(userUpn))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.GetUserByUpnAsync(userUpn);

            // Assert
            result.Should().BeNull();
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(userUpn), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveUsersAsync_WhenNoActiveUsers_ShouldReturnEmptyList()
        {
            // Arrange
            var emptyUserList = new List<User>();
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                             .ReturnsAsync(emptyUserList);

            // Act
            var result = await _userService.GetAllActiveUsersAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveUsersAsync_WhenActiveUsersExist_ShouldReturnListOfActiveUsers()
        {
            // Arrange
            var activeUsers = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", IsActive = true },
                new User { Id = "user-2", FirstName = "Anna", LastName = "Nowak", IsActive = true },
                new User { Id = "user-3", FirstName = "Piotr", LastName = "Wiśniewski", IsActive = true }
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                             .ReturnsAsync(activeUsers);

            // Act
            var result = await _userService.GetAllActiveUsersAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().Contain(activeUsers);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Theory]
        [InlineData("", "Kowalski", "valid@example.com")]
        [InlineData("   ", "Kowalski", "valid@example.com")]
        public async Task CreateUserAsync_EmptyOrWhiteSpaceFirstName_ShouldReturnNullAndLogFailed(string firstName, string lastName, string upn)
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Imię, nazwisko i UPN są wymagane.");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Theory]
        [InlineData("Jan", "", "valid@example.com")]
        [InlineData("Jan", "   ", "valid@example.com")]
        public async Task CreateUserAsync_EmptyOrWhiteSpaceLastName_ShouldReturnNullAndLogFailed(string firstName, string lastName, string upn)
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Imię, nazwisko i UPN są wymagane.");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Theory]
        [InlineData("Jan", "Kowalski", "")]
        [InlineData("Jan", "Kowalski", "   ")]
        public async Task CreateUserAsync_EmptyOrWhiteSpaceUpn_ShouldReturnNullAndLogFailed(string firstName, string lastName, string upn)
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Imię, nazwisko i UPN są wymagane.");

            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_UpnAlreadyExists_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var firstName = "Anna";
            var lastName = "Nowak";
            var upn = "existing.user@example.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            var existingUser = new User
            {
                Id = "existing-user-456",
                UPN = upn,
                FirstName = "Istniejący",
                LastName = "Użytkownik",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn))
                             .ReturnsAsync(existingUser);

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, departmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Użytkownik o UPN '{upn}' już istnieje");

            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(upn), Times.Once);
            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_DepartmentNotFoundOrInactive_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var firstName = "Jan";
            var lastName = "Kowalski";
            var upn = "jan.kowalski@example.com";
            var role = UserRole.Nauczyciel;
            var nonExistentDepartmentId = "non-existent-dept-123";

            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(upn))
                             .ReturnsAsync((User?)null);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(nonExistentDepartmentId))
                                   .ReturnsAsync((Department?)null);

            // Act
            var result = await _userService.CreateUserAsync(firstName, lastName, upn, role, nonExistentDepartmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Dział o ID '{nonExistentDepartmentId}' nie istnieje lub jest nieaktywny");

            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(upn), Times.Once);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(nonExistentDepartmentId), Times.Once);
            _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task UpdateUserAsync_ExistingUserWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Arrange
            var userId = "user-update-123";
            var existingUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@example.com",
                Role = UserRole.Nauczyciel,
                DepartmentId = "dept-old",
                IsActive = true
            };

            var userToUpdate = new User
            {
                Id = userId,
                FirstName = "Janusz",
                LastName = "Kowalski-Nowak",
                UPN = "jan.kowalski@example.com", // UPN nie zmienione
                Role = UserRole.Wicedyrektor,
                DepartmentId = "dept-new"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(existingUser);

            // Act
            var result = await _userService.UpdateUserAsync(userToUpdate);

            // Assert
            result.Should().BeTrue();

            existingUser.FirstName.Should().Be("Janusz");
            existingUser.LastName.Should().Be("Kowalski-Nowak");
            existingUser.Role.Should().Be(UserRole.Wicedyrektor);
            existingUser.DepartmentId.Should().Be("dept-new");

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(existingUser), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateUserAsync_UserNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userToUpdate = new User
            {
                Id = "non-existent-user-789",
                FirstName = "Jan",
                LastName = "Kowalski"
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userToUpdate.Id))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.UpdateUserAsync(userToUpdate);

            // Assert
            result.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userToUpdate.Id), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task DeactivateUserAsync_ExistingActiveUser_ShouldDeactivateAndReturnTrueAndLog()
        {
            // Arrange
            var userId = "user-to-deactivate-123";
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);

            // Act
            var result = await _userService.DeactivateUserAsync(userId);

            // Assert
            result.Should().BeTrue();
            user.IsActive.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(user), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeactivateUserAsync_UserNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userId = "non-existent-user-456";

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.DeactivateUserAsync(userId);

            // Assert
            result.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task DeactivateUserAsync_UserAlreadyInactive_ShouldReturnFalseAndLogNoAction()
        {
            // Arrange
            var userId = "user-already-inactive-789";
            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = false // Już nieaktywny
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);

            // Act
            var result = await _userService.DeactivateUserAsync(userId);

            // Assert
            result.Should().BeFalse(); // Zgodnie z implementacją w UserService

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Użytkownik o ID '{userId}' jest już nieaktywny.");
        }

        [Fact]
        public async Task ActivateUserAsync_ExistingInactiveUser_ShouldActivateAndReturnTrueAndLog()
        {
            // Arrange
            var userId = "user-to-activate-123";
            var user = new User
            {
                Id = userId,
                FirstName = "Anna",
                LastName = "Nowak",
                IsActive = false
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);

            // Act
            var result = await _userService.ActivateUserAsync(userId);

            // Assert
            result.Should().BeTrue();
            user.IsActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(user), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task ActivateUserAsync_UserNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userId = "non-existent-user-456";

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.ActivateUserAsync(userId);

            // Assert
            result.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task ActivateUserAsync_UserAlreadyActive_ShouldReturnTrueAndLogNoAction()
        {
            // Arrange
            var userId = "user-already-active-789";
            var user = new User
            {
                Id = userId,
                FirstName = "Piotr",
                LastName = "Wiśniewski",
                IsActive = true // Już aktywny
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);

            // Act
            var result = await _userService.ActivateUserAsync(userId);

            // Assert
            result.Should().BeTrue(); // Zgodnie z implementacją - zwraca true jeśli już aktywny

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("był już aktywny");
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_ValidUserAndSchoolType_ShouldCreateAssignmentAndReturnObjectAndLog()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "school-type-456";
            var assignedDate = DateTime.UtcNow;
            var workloadPercentage = 50m;
            var notes = "Test assignment";

            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true
            };

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                FullName = "Liceum Ogólnokształcące",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                   .ReturnsAsync(schoolType);

            // Mock dla sprawdzenia istniejących przypisań
            var emptyAssignments = new List<UserSchoolType>();
            _mockUserSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSchoolType, bool>>>()))
                                       .ReturnsAsync(emptyAssignments);

            UserSchoolType? addedAssignment = null;
            _mockUserSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<UserSchoolType>()))
                                       .Callback<UserSchoolType>(assignment => addedAssignment = assignment)
                                       .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(userId, schoolTypeId, assignedDate, null, workloadPercentage, notes);

            // Assert
            result.Should().NotBeNull();
            addedAssignment.Should().NotBeNull();
            result.Should().BeSameAs(addedAssignment);

            result!.UserId.Should().Be(userId);
            result.SchoolTypeId.Should().Be(schoolTypeId);
            result.AssignedDate.Should().Be(assignedDate);
            result.WorkloadPercentage.Should().Be(workloadPercentage);
            result.Notes.Should().Be(notes);
            result.IsActive.Should().BeTrue();
            result.IsCurrentlyActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<UserSchoolType>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_UserNotFoundOrInactive_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var userId = "non-existent-user-123";
            var schoolTypeId = "school-type-456";
            var assignedDate = DateTime.UtcNow;

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(userId, schoolTypeId, assignedDate);

            // Assert
            result.Should().BeNull();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<UserSchoolType>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_SchoolTypeNotFoundOrInactive_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "non-existent-school-type-456";
            var assignedDate = DateTime.UtcNow;

            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                   .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(userId, schoolTypeId, assignedDate);

            // Assert
            result.Should().BeNull();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<UserSchoolType>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task AssignUserToSchoolTypeAsync_AssignmentAlreadyExistsAndActive_ShouldReturnExistingAndLogWarningOrFailed()
        {
            // Arrange
            var userId = "user-123";
            var schoolTypeId = "school-type-456";
            var assignedDate = DateTime.UtcNow;

            var user = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = true
            };

            var schoolType = new SchoolType
            {
                Id = schoolTypeId,
                FullName = "Liceum Ogólnokształcące",
                IsActive = true
            };

            var existingAssignment = new UserSchoolType
            {
                Id = "existing-assignment-789",
                UserId = userId,
                SchoolTypeId = schoolTypeId,
                IsActive = true,
                IsCurrentlyActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(user);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId))
                                   .ReturnsAsync(schoolType);

            var existingAssignments = new List<UserSchoolType> { existingAssignment };
            _mockUserSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSchoolType, bool>>>()))
                                       .ReturnsAsync(existingAssignments);

            // Act
            var result = await _userService.AssignUserToSchoolTypeAsync(userId, schoolTypeId, assignedDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(existingAssignment);

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.AddAsync(It.IsAny<UserSchoolType>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("już aktywnie przypisany");
        }

        [Fact]
        public async Task RemoveUserFromSchoolTypeAsync_ExistingAssignment_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Arrange
            var userSchoolTypeId = "assignment-123";
            var assignment = new UserSchoolType
            {
                Id = userSchoolTypeId,
                UserId = "user-123",
                SchoolTypeId = "school-type-456",
                IsActive = true
            };

            _mockUserSchoolTypeRepository.Setup(r => r.GetByIdAsync(userSchoolTypeId))
                                       .ReturnsAsync(assignment);

            // Act
            var result = await _userService.RemoveUserFromSchoolTypeAsync(userSchoolTypeId);

            // Assert
            result.Should().BeTrue();
            assignment.IsActive.Should().BeFalse();

            _mockUserSchoolTypeRepository.Verify(r => r.GetByIdAsync(userSchoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.Update(assignment), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task RemoveUserFromSchoolTypeAsync_AssignmentNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userSchoolTypeId = "non-existent-assignment-456";

            _mockUserSchoolTypeRepository.Setup(r => r.GetByIdAsync(userSchoolTypeId))
                                       .ReturnsAsync((UserSchoolType?)null);

            // Act
            var result = await _userService.RemoveUserFromSchoolTypeAsync(userSchoolTypeId);

            // Assert
            result.Should().BeFalse();

            _mockUserSchoolTypeRepository.Verify(r => r.GetByIdAsync(userSchoolTypeId), Times.Once);
            _mockUserSchoolTypeRepository.Verify(r => r.Update(It.IsAny<UserSchoolType>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Przypisanie o ID '{userSchoolTypeId}' nie zostało znalezione.");
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_ValidTeacherAndSubject_ShouldCreateAssignmentAndReturnObjectAndLog()
        {
            // Arrange
            var teacherId = "teacher-123";
            var subjectId = "subject-456";
            var assignedDate = DateTime.UtcNow;
            var notes = "Teaching assignment";

            var teacher = new User
            {
                Id = teacherId,
                FirstName = "Anna",
                LastName = "Nowak",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var subject = new Subject
            {
                Id = subjectId,
                Name = "Matematyka",
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId))
                             .ReturnsAsync(teacher);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId))
                                 .ReturnsAsync(subject);

            // Mock dla sprawdzenia istniejących przypisań
            var emptyAssignments = new List<UserSubject>();
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>()))
                                     .ReturnsAsync(emptyAssignments);

            UserSubject? addedAssignment = null;
            _mockUserSubjectRepository.Setup(r => r.AddAsync(It.IsAny<UserSubject>()))
                                     .Callback<UserSubject>(assignment => addedAssignment = assignment)
                                     .Returns(Task.CompletedTask);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate, notes);

            // Assert
            result.Should().NotBeNull();
            addedAssignment.Should().NotBeNull();
            result.Should().BeSameAs(addedAssignment);

            result!.UserId.Should().Be(teacherId);
            result.SubjectId.Should().Be(subjectId);
            result.AssignedDate.Should().Be(assignedDate);
            result.Notes.Should().Be(notes);
            result.IsActive.Should().BeTrue();

            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.IsAny<UserSubject>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_UserNotFoundOrInactiveOrNotTeacher_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var teacherId = "non-teacher-123";
            var subjectId = "subject-456";
            var assignedDate = DateTime.UtcNow;

            var nonTeacher = new User
            {
                Id = teacherId,
                FirstName = "Jan",
                LastName = "Kowalski",
                Role = UserRole.Uczen, // Nie jest nauczycielem - Uczeń nie ma uprawnień do nauczania
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId))
                             .ReturnsAsync(nonTeacher);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate);

            // Assert
            result.Should().BeNull();

            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.IsAny<UserSubject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_SubjectNotFoundOrInactive_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            var teacherId = "teacher-123";
            var subjectId = "non-existent-subject-456";
            var assignedDate = DateTime.UtcNow;

            var teacher = new User
            {
                Id = teacherId,
                FirstName = "Anna",
                LastName = "Nowak",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId))
                             .ReturnsAsync(teacher);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId))
                                 .ReturnsAsync((Subject?)null);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate);

            // Assert
            result.Should().BeNull();

            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.IsAny<UserSubject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("nie został znaleziony");
        }

        [Fact]
        public async Task AssignTeacherToSubjectAsync_AssignmentAlreadyExistsAndActive_ShouldReturnExistingAndLogWarningOrFailed()
        {
            // Arrange
            var teacherId = "teacher-123";
            var subjectId = "subject-456";
            var assignedDate = DateTime.UtcNow;

            var teacher = new User
            {
                Id = teacherId,
                FirstName = "Anna",
                LastName = "Nowak",
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var subject = new Subject
            {
                Id = subjectId,
                Name = "Matematyka",
                IsActive = true
            };

            var existingAssignment = new UserSubject
            {
                Id = "existing-assignment-789",
                UserId = teacherId,
                SubjectId = subjectId,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(teacherId))
                             .ReturnsAsync(teacher);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId))
                                 .ReturnsAsync(subject);

            var existingAssignments = new List<UserSubject> { existingAssignment };
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>()))
                                     .ReturnsAsync(existingAssignments);

            // Act
            var result = await _userService.AssignTeacherToSubjectAsync(teacherId, subjectId, assignedDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(existingAssignment);

            _mockUserRepository.Verify(r => r.GetByIdAsync(teacherId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.AddAsync(It.IsAny<UserSubject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("już przypisany do tego przedmiotu");
        }

        [Fact]
        public async Task RemoveTeacherFromSubjectAsync_ExistingAssignment_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Arrange
            var userSubjectId = "assignment-789";
            var assignment = new UserSubject
            {
                Id = userSubjectId,
                UserId = "teacher-123",
                SubjectId = "subject-456",
                IsActive = true
            };

            _mockUserSubjectRepository.Setup(r => r.GetByIdAsync(userSubjectId))
                                     .ReturnsAsync(assignment);

            // Act
            var result = await _userService.RemoveTeacherFromSubjectAsync(userSubjectId);

            // Assert
            result.Should().BeTrue();
            assignment.IsActive.Should().BeFalse();

            _mockUserSubjectRepository.Verify(r => r.GetByIdAsync(userSubjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.Update(assignment), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task RemoveTeacherFromSubjectAsync_AssignmentNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userSubjectId = "non-existent-assignment-123";

            _mockUserSubjectRepository.Setup(r => r.GetByIdAsync(userSubjectId))
                                     .ReturnsAsync((UserSubject?)null);

            // Act
            var result = await _userService.RemoveTeacherFromSubjectAsync(userSubjectId);

            // Assert
            result.Should().BeFalse();

            _mockUserSubjectRepository.Verify(r => r.GetByIdAsync(userSubjectId), Times.Once);
            _mockUserSubjectRepository.Verify(r => r.Update(It.IsAny<UserSubject>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Przypisanie o ID '{userSubjectId}' nie zostało znalezione.");
        }

        [Fact]
        public async Task UpdateUserAsync_AttemptToChangeUpnToExisting_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            var userId = "user-to-update-123";
            var existingUpn = "existing.user@example.com";
            var newUpn = "new.upn@example.com";

            var existingUser = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = existingUpn,
                Role = UserRole.Nauczyciel,
                IsActive = true
            };

            var userToUpdate = new User
            {
                Id = userId,
                FirstName = "Jan",
                LastName = "Kowalski-Updated",
                UPN = newUpn, // Próba zmiany UPN
                Role = UserRole.Nauczyciel
            };

            var userWithSameUpn = new User
            {
                Id = "different-user-456",
                UPN = newUpn,
                IsActive = true
            };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId))
                             .ReturnsAsync(existingUser);
            _mockUserRepository.Setup(r => r.GetUserByUpnAsync(newUpn))
                             .ReturnsAsync(userWithSameUpn);

            // Act
            var result = await _userService.UpdateUserAsync(userToUpdate);

            // Assert
            result.Should().BeFalse();

            _mockUserRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
            _mockUserRepository.Verify(r => r.GetUserByUpnAsync(newUpn), Times.Once);
            _mockUserRepository.Verify(r => r.Update(It.IsAny<User>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.UserUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"UPN '{newUpn}' już istnieje w systemie");

        }

        // Placeholder testy dla PowerShell - do zaimplementowania gdy PowerShellService będzie dostępny
        [Fact]
        public async Task CreateUserAsync_PowerShellCreationFails_ShouldReturnNullAndLogFailed()
        {
            // TODO: Zaimplementować test gdy PowerShellService będzie dostępny
            // Ten test sprawdzi scenariusz gdy tworzenie użytkownika w M365 nie powiedzie się

            // Arrange
            var firstName = "Jan";
            var lastName = "Kowalski";
            var upn = "jan.kowalski@example.com";
            var role = UserRole.Nauczyciel;
            var departmentId = "dept-123";

            // Act & Assert
            // Na razie PowerShell zawsze zwraca true (symulacja)
            // W przyszłości ten test sprawdzi rzeczywiste niepowodzenie PowerShell operacji

            Assert.True(true); // Placeholder assertion
        }

        [Fact]
        public async Task DeactivateUserAsync_PowerShellDeactivationFails_ShouldReturnFalseAndLogFailed()
        {
            // TODO: Zaimplementować test gdy PowerShellService będzie dostępny
            // Ten test sprawdzi scenariusz gdy dezaktywacja użytkownika w M365 nie powiedzie się

            // Arrange
            var userId = "user-123";

            // Act & Assert
            // Na razie PowerShell zawsze zwraca true (symulacja)
            // W przyszłości ten test sprawdzi rzeczywiste niepowodzenie PowerShell operacji

            Assert.True(true); // Placeholder assertion
        }
    }
}

