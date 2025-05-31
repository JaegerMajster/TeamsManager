using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
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
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly DepartmentService _departmentService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u
        private const string AllDepartmentsRootOnlyCacheKey = "Departments_AllActive_RootOnly";
        private const string AllDepartmentsAllCacheKey = "Departments_AllActive_All";
        private const string DepartmentByIdCacheKeyPrefix = "Department_Id_";
        private const string SubDepartmentsByParentIdCacheKeyPrefix = "Department_Sub_ParentId_";
        private const string UsersInDepartmentCacheKeyPrefix = "Department_UsersIn_Id_";

        public DepartmentServiceTests()
        {
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<DepartmentService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                        .Callback<OperationHistory>(op => _capturedOperationHistory = op!);

            // Konfiguracja mocka IMemoryCache.CreateEntry
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            _departmentService = new DepartmentService(
                _mockDepartmentRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        // --- Testy dla GetDepartmentByIdAsync ---
        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var departmentId = "dept-1";
            var expectedDepartment = new Department { Id = departmentId, Name = "IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, (Department?)null, false);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(expectedDepartment);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

            // Assert
            result.Should().BeEquivalentTo(expectedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_InCache_ShouldReturnFromCache()
        {
            // Arrange
            var departmentId = "dept-cached";
            var cachedDepartment = new Department { Id = departmentId, Name = "Cached IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, cachedDepartment, true);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

            // Assert
            result.Should().BeEquivalentTo(cachedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            // Arrange
            var departmentId = "dept-force";
            var cachedDept = new Department { Id = departmentId, Name = "Old Data" };
            var dbDept = new Department { Id = departmentId, Name = "New Data from DB" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, cachedDept, true);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(dbDept);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, forceRefresh: true);

            // Assert
            result.Should().BeEquivalentTo(dbDept);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithIncludes_ShouldFetchBaseFromCacheAndThenSubEntities()
        {
            // Arrange
            var departmentId = "dept-includes";
            var baseDepartment = new Department { Id = departmentId, Name = "Base Dept" };
            var subDepts = new List<Department> { new Department { Id = "sub1", ParentDepartmentId = departmentId } };
            var users = new List<User> { new User { Id = "user1", DepartmentId = departmentId } };

            string baseCacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            string subDeptsCacheKey = SubDepartmentsByParentIdCacheKeyPrefix + departmentId;
            string usersCacheKey = UsersInDepartmentCacheKeyPrefix + departmentId;

            // Krok 1: Pobranie bazowego obiektu z cache
            SetupCacheTryGetValue(baseCacheKey, baseDepartment, true);
            // Krok 2: Pobranie poddziałów (założenie, że nie ma w cache dla GetSubDepartmentsAsync)
            SetupCacheTryGetValue(subDeptsCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))))
                                   .ReturnsAsync(subDepts);
            // Krok 3: Pobranie użytkowników (założenie, że nie ma w cache dla GetUsersInDepartmentAsync)
            SetupCacheTryGetValue(usersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))))
                               .ReturnsAsync(users);

            // Act
            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, includeSubDepartments: true, includeUsers: true);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Base Dept");
            result.SubDepartments.Should().BeEquivalentTo(subDepts);
            result.Users.Should().BeEquivalentTo(users);

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Never); // Bo bazowy obiekt był w cache
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))), Times.Once);
            _mockUserRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))), Times.Once);

            _mockMemoryCache.Verify(m => m.CreateEntry(subDeptsCacheKey), Times.Once); // Sprawdzenie cache'owania poddziałów
            _mockMemoryCache.Verify(m => m.CreateEntry(usersCacheKey), Times.Once);    // Sprawdzenie cache'owania użytkowników
        }

        // --- Testy dla GetAllDepartmentsAsync ---
        [Fact]
        public async Task GetAllDepartmentsAsync_RootOnly_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var rootDepts = new List<Department> { new Department { Id = "root1", ParentDepartmentId = null } };
            SetupCacheTryGetValue(AllDepartmentsRootOnlyCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>())).ReturnsAsync(rootDepts);

            // Act
            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: true);

            // Assert
            result.Should().BeEquivalentTo(rootDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllDepartmentsRootOnlyCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_All_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var allDepts = new List<Department> { new Department { Id = "all1" }, new Department { Id = "all2", ParentDepartmentId = "all1" } };
            SetupCacheTryGetValue(AllDepartmentsAllCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>())).ReturnsAsync(allDepts);

            // Act
            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: false);

            // Assert
            result.Should().BeEquivalentTo(allDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllDepartmentsAllCacheKey), Times.Once);
        }

        // --- Testy dla GetSubDepartmentsAsync ---
        [Fact]
        public async Task GetSubDepartmentsAsync_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var parentId = "parent1";
            var subDepts = new List<Department> { new Department { Id = "sub1", ParentDepartmentId = parentId } };
            string cacheKey = SubDepartmentsByParentIdCacheKeyPrefix + parentId;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>())).ReturnsAsync(subDepts);

            // Act
            var result = await _departmentService.GetSubDepartmentsAsync(parentId);

            // Assert
            result.Should().BeEquivalentTo(subDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy dla GetUsersInDepartmentAsync ---
        [Fact]
        public async Task GetUsersInDepartmentAsync_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var departmentId = "deptWithUsers";
            var users = new List<User> { new User { Id = "user1", DepartmentId = departmentId } };
            string cacheKey = UsersInDepartmentCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>())).ReturnsAsync(users);

            // Act
            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            // Assert
            result.Should().BeEquivalentTo(users);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateDepartmentAsync_ShouldInvalidateCache()
        {
            // Arrange
            string departmentName = "Nowy Dział Do Inwalidacji";
            string departmentDescription = "Opis";
            Department? addedDepartmentToRepository = null; // Zmienna do przechwycenia działu dodanego do repozytorium

            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        addedDepartmentToRepository = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            // Act
            var resultDepartment = await _departmentService.CreateDepartmentAsync(departmentName, departmentDescription);

            // Assert
            resultDepartment.Should().NotBeNull(); // Sprawdzenie, czy dział został zwrócony
            addedDepartmentToRepository.Should().NotBeNull(); // Sprawdzenie, czy callback przechwycił dział
            resultDepartment!.Id.Should().Be(addedDepartmentToRepository!.Id); // Sprawdzenie, czy ID są spójne

            _mockMemoryCache.Verify(m => m.Remove(DepartmentByIdCacheKeyPrefix + resultDepartment.Id), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UsersInDepartmentCacheKeyPrefix + resultDepartment.Id), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SubDepartmentsByParentIdCacheKeyPrefix + resultDepartment.Id), Times.AtLeastOnce);

        }

        [Fact]
        public async Task UpdateDepartmentAsync_ShouldInvalidateCache()
        {
            // Arrange
            var deptId = "dept-update-cache";
            var existingDept = new Department { Id = deptId, Name = "Old", ParentDepartmentId = "oldParent" };
            var updatedDept = new Department { Id = deptId, Name = "New", ParentDepartmentId = "newParent" };
            var newParentDept = new Department { Id = "newParent", Name = "New Parent Dept", IsActive = true };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(deptId)).ReturnsAsync(existingDept);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync("newParent")).ReturnsAsync(newParentDept);

            // Act
            await _departmentService.UpdateDepartmentAsync(updatedDept);

            // Assert
            _mockMemoryCache.Verify(m => m.Remove(DepartmentByIdCacheKeyPrefix + deptId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SubDepartmentsByParentIdCacheKeyPrefix + "oldParent"), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SubDepartmentsByParentIdCacheKeyPrefix + "newParent"), Times.AtLeastOnce);
            // Token też powinien być zresetowany
        }

        [Fact]
        public async Task DeleteDepartmentAsync_ShouldInvalidateCache()
        {
            // Arrange
            var deptId = "dept-delete-cache";
            var deptToDelete = new Department { Id = deptId, Name = "ToDelete", ParentDepartmentId = "parentDel", IsActive = true };
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(deptId)).ReturnsAsync(deptToDelete);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>()); // No sub-departments
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>()); // No users

            // Act
            await _departmentService.DeleteDepartmentAsync(deptId);

            // Assert
            _mockMemoryCache.Verify(m => m.Remove(DepartmentByIdCacheKeyPrefix + deptId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SubDepartmentsByParentIdCacheKeyPrefix + "parentDel"), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllDepartmentKeys()
        {
            // Act
            await _departmentService.RefreshCacheAsync();

            // Assert
            // Weryfikacja, że odpowiednie klucze zostałyby usunięte (lub token anulowany)
            // Najprostsza weryfikacja to sprawdzenie, czy następne wywołanie Get pobiera z repozytorium
            SetupCacheTryGetValue(AllDepartmentsAllCacheKey, (IEnumerable<Department>?)null, false); // Symulacja braku w cache
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(new List<Department>())
                                   .Verifiable();

            await _departmentService.GetAllDepartmentsAsync();
            _mockDepartmentRepository.Verify(); // Sprawdza, czy metoda oznaczona Verifiable została wywołana
        }

        // --- Test CreateDepartmentAsync ---
        [Fact]
        public async Task CreateDepartmentAsync_ValidInputs_ShouldCreateDepartmentSuccessfully()
        {
            // Arrange
            ResetCapturedOperationHistory();
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
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithParentDepartment_ShouldCreateDepartmentWithParent()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Subdział";
            var description = "Opis poddziału";
            var parentId = "parent-dept-id";
            var departmentCode = "SD";
            var parentDepartment = new Department { Id = parentId, Name = "Parent Dept", IsActive = true };
            Department? addedDepartment = null;

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentId)).ReturnsAsync(parentDepartment);
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept => addedDepartment = dept)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _departmentService.CreateDepartmentAsync(name, description, parentId, departmentCode);

            // Assert
            result.Should().NotBeNull();
            addedDepartment.Should().NotBeNull();
            result!.ParentDepartmentId.Should().Be(parentId);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateDepartmentAsync_NullOrEmptyName_ShouldThrowArgumentException()
        {
            // Arrange
            ResetCapturedOperationHistory();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync(null!, "Description"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("", "Description"));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("   ", "Description"));
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ValidDepartment_ShouldUpdateSuccessfully()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var departmentId = "dept-to-update";
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Old Name",
                Description = "Old Description",
                IsActive = true
            };
            var updatedDepartment = new Department
            {
                Id = departmentId,
                Name = "New Name",
                Description = "New Description",
                IsActive = true
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(existingDepartment);
            _mockDepartmentRepository.Setup(r => r.Update(It.IsAny<Department>())).Verifiable();

            // Act
            await _departmentService.UpdateDepartmentAsync(updatedDepartment);

            // Assert
            _mockDepartmentRepository.Verify(r => r.Update(It.Is<Department>(d =>
                d.Id == departmentId &&
                d.Name == "New Name" &&
                d.Description == "New Description")), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithNoSubDepartmentsOrUsers_ShouldDeleteSuccessfully()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var departmentId = "dept-to-delete";
            var department = new Department { Id = departmentId, Name = "To Delete", IsActive = true };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>());
            _mockDepartmentRepository.Setup(r => r.Update(It.IsAny<Department>())).Verifiable();

            // Act
            await _departmentService.DeleteDepartmentAsync(departmentId);

            // Assert
            _mockDepartmentRepository.Verify(r => r.Update(It.Is<Department>(d =>
                d.Id == departmentId && d.IsActive == false)), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithSubDepartments_ShouldThrowInvalidOperationException()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var departmentId = "dept-with-subdepts";
            var department = new Department { Id = departmentId, Name = "Parent Dept", IsActive = true };
            var subDepartments = new List<Department>
            {
                new Department { Id = "sub1", ParentDepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepartments);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _departmentService.DeleteDepartmentAsync(departmentId));
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithUsers_ShouldThrowInvalidOperationException()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var departmentId = "dept-with-users";
            var department = new Department { Id = departmentId, Name = "Dept with Users", IsActive = true };
            var users = new List<User>
            {
                new User { Id = "user1", DepartmentId = departmentId, IsActive = true }
            };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(users);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _departmentService.DeleteDepartmentAsync(departmentId));
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
        }

        // Pomocnicza klasa do testowania predykatów przekazywanych do FindAsync
        public static class TestExpressionHelper
        {
            public static bool IsForSubDepartments(Expression<Func<Department, bool>> expression, string expectedParentId)
            {
                var department = new Department { ParentDepartmentId = expectedParentId, IsActive = true };
                var nonMatchingDepartment = new Department { ParentDepartmentId = "otherId", IsActive = true };
                var inactiveDepartment = new Department { ParentDepartmentId = expectedParentId, IsActive = false };

                var compiled = expression.Compile();
                return compiled(department) && !compiled(nonMatchingDepartment) && !compiled(inactiveDepartment);
            }

            public static bool IsForUsersInDepartment(Expression<Func<User, bool>> expression, string expectedDepartmentId)
            {
                var user = new User { DepartmentId = expectedDepartmentId, IsActive = true };
                var nonMatchingUser = new User { DepartmentId = "otherId", IsActive = true };
                var inactiveUser = new User { DepartmentId = expectedDepartmentId, IsActive = false };

                var compiled = expression.Compile();
                return compiled(user) && !compiled(nonMatchingUser) && !compiled(inactiveUser);
            }
        }
    }
}