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
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services.Core
{
    public class DepartmentServiceTests
    {
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<DepartmentService>> _mockLogger;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly DepartmentService _service;

        public DepartmentServiceTests()
        {
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<DepartmentService>>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();

            _service = new DepartmentService(
                _mockDepartmentRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockGraphCacheService.Object
            );

            // Domyślne setup
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("test@test.com");
        }

        #region GetDepartmentByIdAsync Tests

        [Fact]
        public async Task GetDepartmentByIdAsync_WithValidId_ShouldReturnDepartment()
        {
            // Arrange
            var departmentId = "dept-123";
            var expectedDepartment = CreateTestDepartment(departmentId, "IT Department");
            
            _mockGraphCacheService.Setup(x => x.TryGetValue<Department>(It.IsAny<string>(), out It.Ref<Department>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(expectedDepartment);

            // Act
            var result = await _service.GetDepartmentByIdAsync(departmentId, false, false, false);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(departmentId);
            result.Name.Should().Be("IT Department");
            _mockDepartmentRepository.Verify(x => x.GetByIdAsync(departmentId), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithIncludeSubDepartments_ShouldIncludeSubDepartments()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = CreateTestDepartment(departmentId, "IT Department");
            var subDepartments = new List<Department>
            {
                CreateTestDepartment("sub-1", "Development", departmentId),
                CreateTestDepartment("sub-2", "Support", departmentId)
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<Department>(It.IsAny<string>(), out It.Ref<Department>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(department);
            _mockDepartmentRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Department, bool>>>()))
                .ReturnsAsync(subDepartments);

            // Act
            var result = await _service.GetDepartmentByIdAsync(departmentId, true, false, false);

            // Assert
            result.Should().NotBeNull();
            result.SubDepartments.Should().HaveCount(2);
            result.SubDepartments.Should().Contain(d => d.Name == "Development");
            result.SubDepartments.Should().Contain(d => d.Name == "Support");
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithIncludeUsers_ShouldIncludeUsers()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = CreateTestDepartment(departmentId, "IT Department");
            var users = new List<User>
            {
                CreateTestUser("user-1", "john@test.com", departmentId),
                CreateTestUser("user-2", "jane@test.com", departmentId)
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<Department>(It.IsAny<string>(), out It.Ref<Department>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(department);
            _mockUserRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>()))
                .ReturnsAsync(users);

            // Act
            var result = await _service.GetDepartmentByIdAsync(departmentId, false, true, false);

            // Assert
            result.Should().NotBeNull();
            result.Users.Should().HaveCount(2);
            result.Users.Should().Contain(u => u.UPN == "john@test.com");
            result.Users.Should().Contain(u => u.UPN == "jane@test.com");
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithCachedData_ShouldReturnFromCache()
        {
            // Arrange
            var departmentId = "dept-cached";
            var cachedDepartment = CreateTestDepartment(departmentId, "Cached Department");
            
            _mockGraphCacheService.Setup(x => x.TryGetValue<Department>(It.IsAny<string>(), out cachedDepartment))
                .Returns(true);

            // Act
            var result = await _service.GetDepartmentByIdAsync(departmentId, false, false, false);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(departmentId);
            _mockDepartmentRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetAllDepartmentsAsync Tests

        [Fact]
        public async Task GetAllDepartmentsAsync_ShouldReturnAllActiveDepartments()
        {
            // Arrange
            var departments = new List<Department>
            {
                CreateTestDepartment("dept-1", "IT Department"),
                CreateTestDepartment("dept-2", "HR Department"),
                CreateTestDepartment("dept-3", "Finance Department")
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<IEnumerable<Department>>(It.IsAny<string>(), out It.Ref<IEnumerable<Department>>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Department, bool>>>()))
                .ReturnsAsync(departments);

            // Act
            var result = await _service.GetAllDepartmentsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.All(d => d.IsActive).Should().BeTrue();
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_OnlyRootDepartments_ShouldReturnOnlyRootDepartments()
        {
            // Arrange
            var rootDepartments = new List<Department>
            {
                CreateTestDepartment("dept-1", "IT Department"),
                CreateTestDepartment("dept-2", "HR Department")
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<IEnumerable<Department>>(It.IsAny<string>(), out It.Ref<IEnumerable<Department>>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Department, bool>>>()))
                .ReturnsAsync(rootDepartments);

            // Act
            var result = await _service.GetAllDepartmentsAsync(onlyRootDepartments: true);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(d => d.ParentDepartmentId == null).Should().BeTrue();
        }

        #endregion

        #region GetSubDepartmentsAsync Tests

        [Fact]
        public async Task GetSubDepartmentsAsync_WithValidParentId_ShouldReturnSubDepartments()
        {
            // Arrange
            var parentId = "parent-123";
            var subDepartments = new List<Department>
            {
                CreateTestDepartment("sub-1", "Development", parentId),
                CreateTestDepartment("sub-2", "Support", parentId)
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<IEnumerable<Department>>(It.IsAny<string>(), out It.Ref<IEnumerable<Department>>.IsAny))
                .Returns(false);
            _mockDepartmentRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Department, bool>>>()))
                .ReturnsAsync(subDepartments);

            // Act
            var result = await _service.GetSubDepartmentsAsync(parentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(d => d.ParentDepartmentId == parentId).Should().BeTrue();
        }

        #endregion

        #region GetUsersInDepartmentAsync Tests

        [Fact]
        public async Task GetUsersInDepartmentAsync_WithValidDepartmentId_ShouldReturnUsers()
        {
            // Arrange
            var departmentId = "dept-123";
            var users = new List<User>
            {
                CreateTestUser("user-1", "john@test.com", departmentId),
                CreateTestUser("user-2", "jane@test.com", departmentId)
            };

            _mockGraphCacheService.Setup(x => x.TryGetValue<IEnumerable<User>>(It.IsAny<string>(), out It.Ref<IEnumerable<User>>.IsAny))
                .Returns(false);
            _mockUserRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<User, bool>>>()))
                .ReturnsAsync(users);

            // Act
            var result = await _service.GetUsersInDepartmentAsync(departmentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(u => u.DepartmentId == departmentId).Should().BeTrue();
        }

        #endregion

        #region CreateDepartmentAsync Tests

        [Fact]
        public async Task CreateDepartmentAsync_WithValidData_ShouldReturnCreatedDepartment()
        {
            // Arrange
            var name = "New Department";
            var description = "Test department";
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentCreated, "Department", null, name, null, null))
                .ReturnsAsync(operation);

            // Act
            var result = await _service.CreateDepartmentAsync(name, description);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name);
            result.Description.Should().Be(description);
            result.IsActive.Should().BeTrue();
            _mockDepartmentRepository.Verify(x => x.AddAsync(It.IsAny<Department>()), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithEmptyName_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CreateDepartmentAsync("", "Description"));
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithValidParent_ShouldSetParentDepartment()
        {
            // Arrange
            var name = "Sub Department";
            var description = "Sub department";
            var parentId = "parent-123";
            var parentDepartment = CreateTestDepartment(parentId, "Parent Department");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentCreated, "Department", null, name, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(parentId))
                .ReturnsAsync(parentDepartment);

            // Act
            var result = await _service.CreateDepartmentAsync(name, description, parentId);

            // Assert
            result.Should().NotBeNull();
            result.ParentDepartmentId.Should().Be(parentId);
            result.ParentDepartment.Should().Be(parentDepartment);
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithInvalidParent_ShouldReturnNull()
        {
            // Arrange
            var name = "Sub Department";
            var description = "Sub department";
            var parentId = "invalid-parent";
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentCreated, "Department", null, name, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(parentId))
                .ReturnsAsync((Department)null);

            // Act
            var result = await _service.CreateDepartmentAsync(name, description, parentId);

            // Assert
            result.Should().BeNull();
            _mockDepartmentRepository.Verify(x => x.AddAsync(It.IsAny<Department>()), Times.Never);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region UpdateDepartmentAsync Tests

        [Fact]
        public async Task UpdateDepartmentAsync_WithValidData_ShouldReturnTrue()
        {
            // Arrange
            var departmentId = "dept-123";
            var existingDepartment = CreateTestDepartment(departmentId, "Old Name");
            var updatedDepartment = CreateTestDepartment(departmentId, "New Name");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentUpdated, "Department", departmentId, updatedDepartment.Name, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(existingDepartment);

            // Act
            var result = await _service.UpdateDepartmentAsync(updatedDepartment);

            // Assert
            result.Should().BeTrue();
            existingDepartment.Name.Should().Be("New Name");
            _mockDepartmentRepository.Verify(x => x.Update(existingDepartment), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task UpdateDepartmentAsync_WithNullDepartment_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateDepartmentAsync(null));
        }

        [Fact]
        public async Task UpdateDepartmentAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var department = CreateTestDepartment("non-existent", "Department");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentUpdated, "Department", department.Id, department.Name, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(department.Id))
                .ReturnsAsync((Department)null);

            // Act
            var result = await _service.UpdateDepartmentAsync(department);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task UpdateDepartmentAsync_WithEmptyName_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "dept-123";
            var existingDepartment = CreateTestDepartment(departmentId, "Valid Name");
            var updatedDepartment = CreateTestDepartment(departmentId, "");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentUpdated, "Department", departmentId, "", null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(existingDepartment);

            // Act
            var result = await _service.UpdateDepartmentAsync(updatedDepartment);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region DeleteDepartmentAsync Tests

        [Fact]
        public async Task DeleteDepartmentAsync_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = CreateTestDepartment(departmentId, "Department");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted, "Department", departmentId, null, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync(department);

            // Act
            var result = await _service.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeTrue();
            department.IsActive.Should().BeFalse();
            _mockDepartmentRepository.Verify(x => x.Update(department), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var departmentId = "non-existent";
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted, "Department", departmentId, null, null, null))
                .ReturnsAsync(operation);
            _mockDepartmentRepository.Setup(x => x.GetByIdAsync(departmentId))
                .ReturnsAsync((Department)null);

            // Act
            var result = await _service.DeleteDepartmentAsync(departmentId);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region Helper Methods

        private static Department CreateTestDepartment(string id, string name, string parentId = null)
        {
            return new Department
            {
                Id = id,
                Name = name,
                Description = $"Test department {name}",
                ParentDepartmentId = parentId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        private static User CreateTestUser(string id, string email, string departmentId)
        {
            return new User
            {
                Id = id,
                UPN = email,
                FirstName = "Test",
                LastName = "User",
                DepartmentId = departmentId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 