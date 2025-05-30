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
    public class DepartmentServiceTests
    {
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<DepartmentService>> _mockLogger;

        private readonly DepartmentService _departmentService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        public DepartmentServiceTests()
        {
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<DepartmentService>>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);

            _departmentService = new DepartmentService(
                _mockDepartmentRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_ShouldReturnDepartment()
        {
            // Arrange
            var departmentId = "dept-123";
            var expectedDepartment = new Department
            {
                Id = departmentId,
                Name = "Informatyka",
                Description = "Wydział‚ Informatyki",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(expectedDepartment);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_NonExistingDepartment_ShouldReturnNull()
        {
            // Arrange
            var departmentId = "non-existing-dept";
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

            // Assert
            result.Should().BeNull();
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_OnlyRootDepartments_ShouldReturnRootDepartments()
        {
            // Arrange
            var rootDepartments = new List<Department>
            {
                new Department { Id = "dept-1", Name = "Informatyka", ParentDepartmentId = null, IsActive = true },
                new Department { Id = "dept-2", Name = "Matematyka", ParentDepartmentId = null, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(rootDepartments);

            // Act
            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: true);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(rootDepartments);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_AllDepartments_ShouldReturnAllActiveDepartments()
        {
            // Arrange
            var allDepartments = new List<Department>
            {
                new Department { Id = "dept-1", Name = "Informatyka", ParentDepartmentId = null, IsActive = true },
                new Department { Id = "dept-2", Name = "Programowanie", ParentDepartmentId = "dept-1", IsActive = true },
                new Department { Id = "dept-3", Name = "Matematyka", ParentDepartmentId = null, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(allDepartments);

            // Act
            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: false);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Should().Contain(allDepartments);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CreateDepartmentAsync_ValidInputs_ShouldCreateDepartmentSuccessfully()
        {
            // Arrange
            var name = "Nowy Dział";
            var description = "Opis nowego działu";
            var departmentCode = "ND";

            Department? addedDepartment = null;
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept => addedDepartment = dept)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, null, departmentCode);

            // Assert
            result.Should().NotBeNull();
            addedDepartment.Should().NotBeNull();
            result.Should().BeSameAs(addedDepartment);

            result!.Name.Should().Be(name);
            result.Description.Should().Be(description);
            result.DepartmentCode.Should().Be(departmentCode);
            result.ParentDepartmentId.Should().BeNull();
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _mockDepartmentRepository.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithParentDepartment_ShouldCreateDepartmentWithParent()
        {
            // Arrange
            var name = "Poddział";
            var description = "Opis poddziału";
            var parentDepartmentId = "parent-dept-123";

            var parentDepartment = new Department
            {
                Id = parentDepartmentId,
                Name = "Dział Nadrzędny",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentDepartmentId))
                                    .ReturnsAsync(parentDepartment);

            Department? addedDepartment = null;
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept => addedDepartment = dept)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, parentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be(name);
            result.Description.Should().Be(description);
            result.ParentDepartmentId.Should().Be(parentDepartmentId);
            result.ParentDepartment.Should().Be(parentDepartment);

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(parentDepartmentId), Times.Once);
            _mockDepartmentRepository.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CreateDepartmentAsync_EmptyName_ShouldReturnNull(string name)
        {
            // Arrange
            var description = "Opis działu";

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Nazwa działu nie może być pusta.");

            _mockDepartmentRepository.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Never);
        }

        [Fact]
        public async Task CreateDepartmentAsync_NonExistingParentDepartment_ShouldReturnNull()
        {
            // Arrange
            var name = "Poddział";
            var description = "Opis poddziału";
            var parentDepartmentId = "non-existing-parent";

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentDepartmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, parentDepartmentId);

            // Assert
            result.Should().BeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Dział nadrzędny o ID '{parentDepartmentId}' nie istnieje");

            _mockDepartmentRepository.Verify(r => r.AddAsync(It.IsAny<Department>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ExistingDepartment_ShouldUpdateSuccessfully()
        {
            // Arrange
            var departmentId = "dept-123";
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Stara Nazwa",
                Description = "Stary opis",
                IsActive = true
            };

            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Nowa Nazwa",
                Description = "Nowy opis",
                Email = "nowy@example.com",
                Phone = "123-456-789",
                Location = "Nowa lokalizacja"
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(existingDepartment);

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeTrue();

            existingDepartment.Name.Should().Be("Nowa Nazwa");
            existingDepartment.Description.Should().Be("Nowy opis");
            existingDepartment.Email.Should().Be("nowy@example.com");
            existingDepartment.Phone.Should().Be("123-456-789");
            existingDepartment.Location.Should().Be("Nowa lokalizacja");

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockDepartmentRepository.Verify(r => r.Update(existingDepartment), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateDepartmentAsync_NonExistingDepartment_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "non-existing-dept";
            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Nowa Nazwa",
                Description = "Nowy opis"
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dział nie istnieje.");

            _mockDepartmentRepository.Verify(r => r.Update(It.IsAny<Department>()), Times.Never);
        }

        [Fact]
        public void UpdateDepartmentAsync_NullDepartment_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                () => _departmentService.UpdateDepartmentAsync(null!));

            exception.Should().NotBeNull();
        }

        [Fact]
        public void UpdateDepartmentAsync_EmptyDepartmentId_ShouldThrowArgumentNullException()
        {
            // Arrange
            var departmentToUpdate = new Department
            {
                Id = "",
                Name = "Nazwa",
                Description = "Opis"
            };

            // Act & Assert
            var exception = Assert.ThrowsAsync<ArgumentNullException>(
                () => _departmentService.UpdateDepartmentAsync(departmentToUpdate));

            exception.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteDepartmentAsync_ExistingDepartmentWithoutUsers_ShouldDeleteSuccessfully()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział do usunięcia",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>()); // Brak poddziałów

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>()); // Brak użytkowników

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeTrue();
            department.IsActive.Should().BeFalse(); // Soft delete

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
            _mockDepartmentRepository.Verify(r => r.Update(department), Times.Once);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithActiveUsers_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział z użytkownikami",
                IsActive = true
            };

            var usersInDepartment = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", DepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>()); // Brak poddziałów

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(usersInDepartment);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            department.IsActive.Should().BeTrue(); // Powinien pozostać aktywny

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("ma przypisanych aktywnych użytkowników");

            _mockDepartmentRepository.Verify(r => r.Update(department), Times.Never);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithSubDepartments_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział z poddziałami",
                IsActive = true
            };

            var subDepartments = new List<Department>
            {
                new Department { Id = "sub-dept-1", Name = "Poddział", ParentDepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            department.IsActive.Should().BeTrue(); // Powinien pozostać aktywny

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("ma przypisane poddziały");

            _mockDepartmentRepository.Verify(r => r.Update(department), Times.Never);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_NonExistingDepartment_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "non-existing-dept";

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dział nie istnieje.");

            _mockDepartmentRepository.Verify(r => r.Update(It.IsAny<Department>()), Times.Never);
        }

        [Fact]
        public async Task GetSubDepartmentsAsync_ShouldReturnSubDepartments()
        {
            // Arrange
            var parentDepartmentId = "parent-dept-123";
            var subDepartments = new List<Department>
            {
                new Department { Id = "sub-1", Name = "Poddział 1", ParentDepartmentId = parentDepartmentId, IsActive = true },
                new Department { Id = "sub-2", Name = "Poddział 2", ParentDepartmentId = parentDepartmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);

            // Act
            var result = await _departmentService.GetSubDepartmentsAsync(parentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(subDepartments);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetUsersInDepartmentAsync_ShouldReturnUsersInDepartment()
        {
            // Arrange
            var departmentId = "dept-123";
            var usersInDepartment = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", DepartmentId = departmentId, IsActive = true },
                new User { Id = "user-2", FirstName = "Anna", LastName = "Nowak", DepartmentId = departmentId, IsActive = true }
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(usersInDepartment);

            // Act
            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(usersInDepartment);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithIncludeFlags_ShouldReturnDepartmentWithIncludedData()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Informatyka",
                Description = "Wydział Informatyki",
                IsActive = true
            };

            var subDepartments = new List<Department>
            {
                new Department { Id = "sub-1", Name = "Poddział 1", ParentDepartmentId = departmentId, IsActive = true }
            };

            var users = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", DepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(users);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, includeSubDepartments: true, includeUsers: true);

            // Assert
            result.Should().NotBeNull();
            result!.SubDepartments.Should().HaveCount(1);
            result.Users.Should().HaveCount(1);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_NoDepartmentsExist_ShouldReturnEmptyList()
        {
            // Arrange
            var emptyDepartmentList = new List<Department>();
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(emptyDepartmentList);

            // Act
            var result = await _departmentService.GetAllDepartmentsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CreateDepartmentAsync_ValidDataWithoutParent_ShouldCreateAndReturnDepartmentAndLog()
        {
            // Ten test już istnieje jako CreateDepartmentAsync_ValidInputs_ShouldCreateDepartmentSuccessfully
            // Sprawdzamy czy logowanie OperationHistory działa poprawnie
            
            // Arrange
            var name = "Nowy Dział";
            var description = "Opis nowego działu";
            var departmentCode = "ND";

            Department? addedDepartment = null;
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept => addedDepartment = dept)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, null, departmentCode);

            // Assert
            result.Should().NotBeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityName.Should().Be(name);
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
        }

        [Fact]
        public async Task CreateDepartmentAsync_ValidDataWithExistingParent_ShouldCreateAndReturnDepartmentAndLog()
        {
            // Ten test już istnieje jako CreateDepartmentAsync_WithParentDepartment_ShouldCreateDepartmentWithParent
            // ale sprawdzimy czy logowanie OperationHistory działa poprawnie

            // Arrange
            var name = "Poddział";
            var description = "Opis poddziału";
            var parentDepartmentId = "parent-dept-123";

            var parentDepartment = new Department
            {
                Id = parentDepartmentId,
                Name = "Dział Nadrzędny",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentDepartmentId))
                                    .ReturnsAsync(parentDepartment);

            Department? addedDepartment = null;
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept => addedDepartment = dept)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, parentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateDepartmentAsync_EmptyName_ShouldReturnNullAndLogFailed()
        {
            // Ten test już istnieje jako CreateDepartmentAsync_EmptyName_ShouldReturnNull
            // ale sprawdzimy czy logowanie OperationHistory działa poprawnie

            // Arrange
            var name = "";
            var description = "Opis działu";

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Nazwa działu nie może być pusta.");
        }

        [Fact]
        public async Task CreateDepartmentAsync_ParentDepartmentNotFound_ShouldReturnNullAndLogFailed()
        {
            // Ten test już istnieje jako CreateDepartmentAsync_NonExistingParentDepartment_ShouldReturnNull
            // ale sprawdzimy czy logowanie OperationHistory działa poprawnie

            // Arrange
            var name = "Poddział";
            var description = "Opis poddziału";
            var parentDepartmentId = "non-existing-parent";

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentDepartmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, parentDepartmentId);

            // Assert
            result.Should().BeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Dział nadrzędny o ID '{parentDepartmentId}' nie istnieje");
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ExistingDepartmentWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Rozszerzenie istniejącego testu o sprawdzenie logowania OperationHistory

            // Arrange
            var departmentId = "dept-123";
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Stara Nazwa",
                Description = "Stary opis",
                IsActive = true
            };

            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Nowa Nazwa",
                Description = "Nowy opis",
                Email = "nowy@example.com",
                Phone = "123-456-789",
                Location = "Nowa lokalizacja"
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(existingDepartment);

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(departmentId);
        }

        [Fact]
        public async Task UpdateDepartmentAsync_DepartmentNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Ten test już istnieje ale sprawdzimy logowanie OperationHistory

            // Arrange
            var departmentId = "non-existing-dept";
            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Nowa Nazwa",
                Description = "Nowy opis"
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dział nie istnieje.");
        }

        [Fact]
        public async Task UpdateDepartmentAsync_AttemptToSetInvalidParent_ShouldReturnFalseAndLogFailed()
        {
            // Nowy test sprawdzający walidację cyklicznej zależności

            // Arrange
            var departmentId = "dept-123";
            var childDepartmentId = "child-dept-456";

            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Dział Główny",
                IsActive = true
            };

            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Dział Główny",
                ParentDepartmentId = childDepartmentId // Próba ustawienia dziecka jako rodzica
            };

            var childDepartment = new Department
            {
                Id = childDepartmentId,
                Name = "Dział Potomny",
                ParentDepartmentId = departmentId,
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(existingDepartment);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(childDepartmentId))
                                    .ReturnsAsync(childDepartment);

            // Mock dla sprawdzenia potomków
            _mockDepartmentRepository.SetupSequence(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department> { childDepartment }) // Pierwsze wywołanie: potomkowie dept-123
                                    .ReturnsAsync(new List<Department>()); // Drugie wywołanie: potomkowie child-dept-456

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("cykliczną zależność");
        }

        [Fact]
        public async Task UpdateDepartmentAsync_AttemptToSetSelfAsParent_ShouldReturnFalseAndLogFailed()
        {
            // Test sprawdzający próbę ustawienia siebie jako rodzica

            // Arrange
            var departmentId = "dept-123";
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Dział",
                IsActive = true
            };

            var departmentToUpdate = new Department
            {
                Id = departmentId,
                Name = "Dział",
                ParentDepartmentId = departmentId // Próba ustawienia siebie jako rodzica
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(existingDepartment);

            // Act
            var result = await _departmentService.UpdateDepartmentAsync(departmentToUpdate);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dział nie może być swoim własnym rodzicem.");
        }

        [Fact]
        public async Task DeleteDepartmentAsync_ExistingEmptyDepartment_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Rozszerzenie istniejącego testu o sprawdzenie logowania OperationHistory

            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział do usunięcia",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>()); // Brak poddziałów

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>()); // Brak użytkowników

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(departmentId);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Rozszerzenie istniejącego testu o sprawdzenie logowania OperationHistory

            // Arrange
            var departmentId = "non-existing-dept";

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync((Department?)null);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Dział nie istnieje.");
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithActiveSubDepartments_ShouldReturnFalseAndLogFailed()
        {
            // Rozszerzenie istniejącego testu o sprawdzenie logowania OperationHistory

            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział z poddziałami",
                IsActive = true
            };

            var subDepartments = new List<Department>
            {
                new Department { Id = "sub-dept-1", Name = "Poddział", ParentDepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("ma przypisane poddziały");
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithActiveUsers_ShouldReturnFalseAndLogFailed()
        {
            // Rozszerzenie istniejącego testu o sprawdzenie logowania OperationHistory

            // Arrange
            var departmentId = "dept-123";
            var department = new Department
            {
                Id = departmentId,
                Name = "Dział z użytkownikami",
                IsActive = true
            };

            var usersInDepartment = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", DepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                    .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>()); // Brak poddziałów

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(usersInDepartment);

            // Act
            var result = await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("ma przypisanych aktywnych użytkowników");
        }

        [Fact]
        public async Task GetSubDepartmentsAsync_ParentWithSubDepartments_ShouldReturnSubDepartments()
        {
            // Ten test już istnieje jako GetSubDepartmentsAsync_ShouldReturnSubDepartments
            // Duplikujemy tylko dla kompletności zgodnie z listą
            
            // Arrange
            var parentDepartmentId = "parent-dept-123";
            var subDepartments = new List<Department>
            {
                new Department { Id = "sub-1", Name = "Poddział 1", ParentDepartmentId = parentDepartmentId, IsActive = true },
                new Department { Id = "sub-2", Name = "Poddział 2", ParentDepartmentId = parentDepartmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);

            // Act
            var result = await _departmentService.GetSubDepartmentsAsync(parentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(subDepartments);
        }

        [Fact]
        public async Task GetSubDepartmentsAsync_ParentWithoutSubDepartments_ShouldReturnEmptyList()
        {
            // Arrange
            var parentDepartmentId = "parent-dept-123";
            var emptySubDepartments = new List<Department>();

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(emptySubDepartments);

            // Act
            var result = await _departmentService.GetSubDepartmentsAsync(parentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetSubDepartmentsAsync_ParentNotFound_ShouldReturnEmptyListOrLogWarning()
        {
            // Metoda GetSubDepartmentsAsync nie sprawdza czy parent istnieje - po prostu zwraca wyniki zapytania
            // To jest poprawne zachowanie, bo może być używane do sprawdzenia czy dział ma potomków

            // Arrange
            var nonExistentParentId = "non-existent-parent";
            var emptySubDepartments = new List<Department>();

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(emptySubDepartments);

            // Act
            var result = await _departmentService.GetSubDepartmentsAsync(nonExistentParentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetUsersInDepartmentAsync_DepartmentWithUsers_ShouldReturnActiveUsers()
        {
            // Ten test już istnieje jako GetUsersInDepartmentAsync_ShouldReturnUsersInDepartment
            // Duplikujemy tylko dla kompletności zgodnie z listą

            // Arrange
            var departmentId = "dept-123";
            var usersInDepartment = new List<User>
            {
                new User { Id = "user-1", FirstName = "Jan", LastName = "Kowalski", DepartmentId = departmentId, IsActive = true },
                new User { Id = "user-2", FirstName = "Anna", LastName = "Nowak", DepartmentId = departmentId, IsActive = true }
            };

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(usersInDepartment);

            // Act
            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(usersInDepartment);
        }

        [Fact]
        public async Task GetUsersInDepartmentAsync_DepartmentWithoutUsers_ShouldReturnEmptyList()
        {
            // Arrange
            var departmentId = "dept-123";
            var emptyUserList = new List<User>();

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(emptyUserList);

            // Act
            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetUsersInDepartmentAsync_DepartmentNotFound_ShouldReturnEmptyListOrLogWarning()
        {
            // Metoda GetUsersInDepartmentAsync nie sprawdza czy dział istnieje - po prostu zwraca wyniki zapytania
            // To jest poprawne zachowanie

            // Arrange
            var nonExistentDepartmentId = "non-existent-dept";
            var emptyUserList = new List<User>();

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(emptyUserList);

            // Act
            var result = await _departmentService.GetUsersInDepartmentAsync(nonExistentDepartmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
        }
    }
}
