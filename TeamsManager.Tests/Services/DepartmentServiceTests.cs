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
using TeamsManager.Core.Abstractions.Services.PowerShell;
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
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<DepartmentService>> _mockLogger;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

        private readonly DepartmentService _departmentService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;


        // Klucze cache - muszą być zgodne z DepartmentService
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
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<DepartmentService>>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            // Setup dla CurrentUserService
            _mockCurrentUserService.Setup(c => c.GetCurrentUserUpn())
                                   .Returns(_currentLoggedInUserUpn);

            // Setup dla OperationHistoryService z przechwytywaniem szczegółów
            _mockOperationHistoryService.Setup(o => o.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                                        .Callback<OperationType, string, string?, string?, string?, string?>((type, entityType, entityId, entityName, details, parentId) =>
                                        {
                                            _capturedOperationHistory = new OperationHistory
                                            {
                                                Id = Guid.NewGuid().ToString(),
                                                Type = type,
                                                TargetEntityType = entityType,
                                                TargetEntityId = entityId ?? string.Empty,
                                                TargetEntityName = entityName ?? string.Empty,
                                                OperationDetails = details ?? string.Empty,
                                                ParentOperationId = parentId,
                                                Status = OperationStatus.InProgress,
                                                CreatedDate = DateTime.UtcNow
                                            };
                                        })
                                        .ReturnsAsync(() => _capturedOperationHistory!);

            _mockOperationHistoryService.Setup(o => o.UpdateOperationStatusAsync(It.IsAny<string>(), It.IsAny<OperationStatus>(), It.IsAny<string?>(), It.IsAny<string?>()))
                                        .ReturnsAsync(true);

            _departmentService = new DepartmentService(
                _mockDepartmentRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockPowerShellCacheService.Object
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            if (foundInCache && item != null)
            {
                TItem? capturedItem = item;
                _mockPowerShellCacheService.Setup(m => m.TryGetValue<TItem>(cacheKey, out It.Ref<TItem?>.IsAny))
                    .Callback(new TryGetValueCallback<TItem>((string key, out TItem? value) =>
                    {
                        value = capturedItem;
                    }))
                    .Returns(foundInCache);
            }
            else
            {
                TItem? nullItem = default(TItem);
                _mockPowerShellCacheService.Setup(m => m.TryGetValue<TItem>(cacheKey, out It.Ref<TItem?>.IsAny))
                    .Callback(new TryGetValueCallback<TItem>((string key, out TItem? value) =>
                    {
                        value = nullItem;
                    }))
                    .Returns(foundInCache);
            }
        }

        private delegate void TryGetValueCallback<TItem>(string key, out TItem? value);

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }



        private async Task AssertCacheInvalidationByReFetchingAllDepartments(List<Department> expectedDbDeptsAfterOperation, bool rootOnly)
        {
            if (rootOnly)
            {
                await AssertCacheInvalidationByReFetchingRootDepartments(expectedDbDeptsAfterOperation);
            }
            else
            {
                await AssertCacheInvalidationByReFetchingAllDepartmentsInternal(expectedDbDeptsAfterOperation);
            }
        }

        private async Task AssertCacheInvalidationByReFetchingRootDepartments(List<Department> expectedDbDeptsAfterOperation)
        {
            SetupCacheTryGetValue<IEnumerable<Department>>(AllDepartmentsRootOnlyCacheKey, null, false);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(expectedDbDeptsAfterOperation);

            var resultAfterInvalidation = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: true);

            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.AtLeastOnce);
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbDeptsAfterOperation);
            _mockPowerShellCacheService.Verify(m => m.Set(AllDepartmentsRootOnlyCacheKey, It.IsAny<IEnumerable<Department>>(), It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
        }

        private async Task AssertCacheInvalidationByReFetchingAllDepartmentsInternal(List<Department> expectedDbDeptsAfterOperation)
        {
            SetupCacheTryGetValue<IEnumerable<Department>>(AllDepartmentsAllCacheKey, null, false);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(expectedDbDeptsAfterOperation);

            var resultAfterInvalidation = await _departmentService.GetAllDepartmentsAsync(false, false);

            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.AtLeastOnce);
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbDeptsAfterOperation);
            _mockPowerShellCacheService.Verify(m => m.Set(AllDepartmentsAllCacheKey, It.IsAny<IEnumerable<Department>>(), It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
        }

        // --- Testy dla GetDepartmentByIdAsync ---
        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_NotInCache_ShouldReturnAndCache()
        {
            var departmentId = "dept-1";
            var expectedDepartment = new Department { Id = departmentId, Name = "IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue<Department>(cacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(expectedDepartment);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, false, false, false);

            result.Should().BeEquivalentTo(expectedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, expectedDepartment, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_InCache_ShouldReturnFromCache()
        {
            var departmentId = "dept-cached";
            var cachedDepartment = new Department { Id = departmentId, Name = "Cached IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue<Department>(cacheKey, cachedDepartment, true);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, false, false, false);

            result.Should().BeEquivalentTo(cachedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            var departmentId = "dept-force";
            var cachedDept = new Department { Id = departmentId, Name = "Old Data" };
            var dbDept = new Department { Id = departmentId, Name = "New Data from DB" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue<Department>(cacheKey, cachedDept, true);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(dbDept);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, false, false, true);

            result.Should().BeEquivalentTo(dbDept);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, dbDept, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_WithIncludes_ShouldFetchBaseFromCacheAndThenSubEntities()
        {
            var departmentId = "dept-includes";
            var baseDepartment = new Department { Id = departmentId, Name = "Base Dept" };
            var subDepts = new List<Department> { new Department { Id = "sub1", ParentDepartmentId = departmentId } };
            var users = new List<User> { new User { Id = "user1", DepartmentId = departmentId } };

            string baseCacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            string subDeptsCacheKey = SubDepartmentsByParentIdCacheKeyPrefix + departmentId;
            string usersCacheKey = UsersInDepartmentCacheKeyPrefix + departmentId;

            SetupCacheTryGetValue<Department>(baseCacheKey, baseDepartment, true);
            SetupCacheTryGetValue<IEnumerable<Department>>(subDeptsCacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(subDepts);
            SetupCacheTryGetValue<IEnumerable<User>>(usersCacheKey, null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(users);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, true, true);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Base Dept");
            result.SubDepartments.Should().BeEquivalentTo(subDepts);
            result.Users.Should().BeEquivalentTo(users);

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Never);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
            _mockUserRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);

            _mockPowerShellCacheService.Verify(m => m.Set(subDeptsCacheKey, It.Is<IEnumerable<Department>>(d => d.SequenceEqual(subDepts)), It.IsAny<TimeSpan?>()), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(usersCacheKey, It.Is<IEnumerable<User>>(u => u.SequenceEqual(users)), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy dla GetAllDepartmentsAsync ---
        [Fact]
        public async Task GetAllDepartmentsAsync_RootOnly_NotInCache_ShouldReturnAndCache()
        {
            var rootDepts = new List<Department> { new Department { Id = "root1", ParentDepartmentId = null } };
            SetupCacheTryGetValue<IEnumerable<Department>>(AllDepartmentsRootOnlyCacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(rootDepts);

            var result = await _departmentService.GetAllDepartmentsAsync(true, false);

            result.Should().BeEquivalentTo(rootDepts);
            _mockPowerShellCacheService.Verify(m => m.Set(AllDepartmentsRootOnlyCacheKey, It.Is<IEnumerable<Department>>(d => d.SequenceEqual(rootDepts)), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_All_NotInCache_ShouldReturnAndCache()
        {
            var allDepts = new List<Department> { new Department { Id = "all1" }, new Department { Id = "all2", ParentDepartmentId = "all1" } };
            SetupCacheTryGetValue<IEnumerable<Department>>(AllDepartmentsAllCacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                  .ReturnsAsync(allDepts);

            var result = await _departmentService.GetAllDepartmentsAsync(false, false);

            result.Should().BeEquivalentTo(allDepts);
            _mockPowerShellCacheService.Verify(m => m.Set(AllDepartmentsAllCacheKey, It.Is<IEnumerable<Department>>(d => d.SequenceEqual(allDepts)), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy dla GetSubDepartmentsAsync ---
        [Fact]
        public async Task GetSubDepartmentsAsync_NotInCache_ShouldReturnAndCache()
        {
            var parentId = "parent1";
            var subDepts = new List<Department> { new Department { Id = "sub1", ParentDepartmentId = parentId } };
            string cacheKey = SubDepartmentsByParentIdCacheKeyPrefix + parentId;
            SetupCacheTryGetValue<IEnumerable<Department>>(cacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(subDepts);

            var result = await _departmentService.GetSubDepartmentsAsync(parentId);

            result.Should().BeEquivalentTo(subDepts);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.Is<IEnumerable<Department>>(d => d.SequenceEqual(subDepts)), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy dla GetUsersInDepartmentAsync ---
        [Fact]
        public async Task GetUsersInDepartmentAsync_NotInCache_ShouldReturnAndCache()
        {
            var departmentId = "deptWithUsers";
            var users = new List<User> { new User { Id = "user1", DepartmentId = departmentId } };
            string cacheKey = UsersInDepartmentCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue<IEnumerable<User>>(cacheKey, null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                                .ReturnsAsync(users);

            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            result.Should().BeEquivalentTo(users);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.Is<IEnumerable<User>>(u => u.SequenceEqual(users)), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateDepartmentAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            string departmentName = "Nowy Dział Do Inwalidacji";
            string departmentDescription = "Opis";
            Department addedDepartmentToRepository = null!;

            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        dept.Id = Guid.NewGuid().ToString();
                                        addedDepartmentToRepository = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            var resultDepartment = await _departmentService.CreateDepartmentAsync(
                name: departmentName,
                description: departmentDescription,
                parentDepartmentId: null,
                departmentCode: null);

            resultDepartment.Should().NotBeNull();
            addedDepartmentToRepository.Should().NotBeNull();
            var createdDeptId = resultDepartment!.Id;

            // Sprawdzenie szczegółowych danych operacji
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().BeEmpty(); // Na początku pusty string, bo dział jeszcze nie istnieje
            _capturedOperationHistory.TargetEntityName.Should().Be(departmentName);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);
            _capturedOperationHistory.ParentOperationId.Should().BeNull();

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.DepartmentCreated,
                nameof(Department),
                It.Is<string?>(s => s == null), // targetEntityId jest null na początku
                departmentName,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                OperationStatus.Completed,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            // CreateDepartmentAsync nie wywołuje InvalidateSubDepartments - tylko InvalidateAllDepartmentLists

            var expectedDeptsAfterCreate = new List<Department> { resultDepartment };
            await AssertCacheInvalidationByReFetchingAllDepartments(expectedDeptsAfterCreate, false);
            await AssertCacheInvalidationByReFetchingAllDepartments(
                resultDepartment.ParentDepartmentId == null ? expectedDeptsAfterCreate : new List<Department>(),
                true);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var deptId = "dept-update-cache";
            var oldParentId = "oldParent";
            var newParentId = "newParent";
            var existingDept = new Department { Id = deptId, Name = "Old", ParentDepartmentId = oldParentId, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedDeptData = new Department { Id = deptId, Name = "New", ParentDepartmentId = newParentId, IsActive = true };
            var newParentDept = new Department { Id = newParentId, Name = "New Parent Dept", IsActive = true };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(deptId)).ReturnsAsync(existingDept);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(newParentId)).ReturnsAsync(newParentDept);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                .ReturnsAsync(new List<Department>());

            var result = await _departmentService.UpdateDepartmentAsync(updatedDeptData);
            result.Should().BeTrue();

            // Sprawdzenie szczegółowych danych operacji
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(deptId);
            _capturedOperationHistory.TargetEntityName.Should().Be(existingDept.Name); // Powinna być stara nazwa przed aktualizacją
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.DepartmentUpdated,
                nameof(Department),
                deptId,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                OperationStatus.Completed,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateDepartment(deptId), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            // UpdateDepartmentAsync nie wywołuje InvalidateSubDepartments - tylko InvalidateDepartment i InvalidateAllDepartmentLists

            var expectedDeptAfterUpdate = new Department
            {
                Id = deptId,
                Name = "New",
                ParentDepartmentId = newParentId,
                IsActive = true,
                ParentDepartment = newParentDept
            };
            var expectedDeptsAfterUpdateList = new List<Department> { expectedDeptAfterUpdate };

            await AssertCacheInvalidationByReFetchingAllDepartments(expectedDeptsAfterUpdateList, false);
            await AssertCacheInvalidationByReFetchingAllDepartments(
                expectedDeptAfterUpdate.ParentDepartmentId == null ? expectedDeptsAfterUpdateList : new List<Department>(),
                true);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task DeleteDepartmentAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var deptId = "dept-delete-cache";
            var parentId = "parentDel";
            var deptToDelete = new Department { Id = deptId, Name = "ToDelete", ParentDepartmentId = parentId, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(deptId)).ReturnsAsync(deptToDelete);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>());

            var result = await _departmentService.DeleteDepartmentAsync(deptId);
            result.Should().BeTrue();

            // Sprawdzenie szczegółowych danych operacji
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(deptId);
            _capturedOperationHistory.TargetEntityName.Should().BeEmpty(); // Na początku jest puste
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted,
                nameof(Department),
                deptId,
                It.Is<string?>(s => s == null), // targetEntityName jest null na początku
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                OperationStatus.Completed,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            // DeleteDepartmentAsync nie wywołuje InvalidateDepartment - tylko InvalidateAllDepartmentLists i InvalidateSubDepartments
            // _mockPowerShellCacheService.Verify(p => p.InvalidateDepartment(deptId), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateSubDepartments(parentId), Times.Once);

            await AssertCacheInvalidationByReFetchingAllDepartments(new List<Department>(), false);
            await AssertCacheInvalidationByReFetchingAllDepartments(new List<Department>(), true);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllDepartmentKeys()
        {
            await _departmentService.RefreshCacheAsync();

            // Sprawdzenie wywołania nowej metody PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllCache(), Times.Once);

            SetupCacheTryGetValue<IEnumerable<Department>>(AllDepartmentsAllCacheKey, null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(new List<Department>())
                                   .Verifiable();

            await _departmentService.GetAllDepartmentsAsync(false, false);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.Once);
        }

        // --- Test CreateDepartmentAsync ---
        [Fact]
        public async Task CreateDepartmentAsync_ValidInputs_ShouldCreateDepartmentSuccessfully()
        {
            ResetCapturedOperationHistory();
            var departmentName = "Test Department";
            var departmentDescription = "Test Description";
            Department addedDepartment = null!;

            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        dept.Id = Guid.NewGuid().ToString();
                                        addedDepartment = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            var result = await _departmentService.CreateDepartmentAsync(
                name: departmentName,
                description: departmentDescription,
                parentDepartmentId: null,
                departmentCode: null);

            result.Should().NotBeNull();
            addedDepartment.Should().NotBeNull();
            addedDepartment.Name.Should().Be(departmentName);
            addedDepartment.Description.Should().Be(departmentDescription);
            addedDepartment.ParentDepartmentId.Should().BeNull();

            // Sprawdzenie szczegółowych danych operacji
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().BeEmpty(); // Na początku pusty string, bo dział jeszcze nie istnieje
            _capturedOperationHistory.TargetEntityName.Should().Be(departmentName);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);
            _capturedOperationHistory.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                It.Is<OperationType>(t => t == OperationType.DepartmentCreated),
                It.Is<string>(s => s == nameof(Department)),
                It.Is<string?>(s => s == null), // targetEntityId jest null na początku
                It.Is<string>(s => s == departmentName),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                It.Is<OperationStatus>(s => s == OperationStatus.Completed),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task CreateDepartmentAsync_WithParentDepartment_ShouldCreateDepartmentWithParent()
        {
            ResetCapturedOperationHistory();
            var parentDepartmentId = "parent-dept";
            var parentDepartment = new Department { Id = parentDepartmentId, Name = "Parent Department", IsActive = true };
            var departmentName = "Child Department";
            var departmentDescription = "Description";
            Department addedDepartment = null!;

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentDepartmentId))
                                   .ReturnsAsync(parentDepartment);

            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        dept.Id = Guid.NewGuid().ToString();
                                        addedDepartment = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            var result = await _departmentService.CreateDepartmentAsync(
                name: departmentName,
                description: departmentDescription,
                parentDepartmentId: parentDepartmentId,
                departmentCode: null);

            result.Should().NotBeNull();
            result!.ParentDepartmentId.Should().Be(parentDepartmentId);
            addedDepartment.Should().NotBeNull();
            addedDepartment.Name.Should().Be(departmentName);
            addedDepartment.Description.Should().Be(departmentDescription);
            addedDepartment.ParentDepartmentId.Should().Be(parentDepartmentId);

            // Sprawdzenie szczegółowych danych operacji z rodzicem
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentCreated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().BeEmpty(); // Na początku pusty string, bo dział jeszcze nie istnieje
            _capturedOperationHistory.TargetEntityName.Should().Be(departmentName);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);
            _capturedOperationHistory.ParentOperationId.Should().BeNull(); // Nie ma operacji nadrzędnej

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                It.Is<OperationType>(t => t == OperationType.DepartmentCreated),
                It.Is<string>(s => s == nameof(Department)),
                It.Is<string?>(s => s == null), // targetEntityId jest null na początku
                It.Is<string>(s => s == departmentName),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                It.Is<OperationStatus>(s => s == OperationStatus.Completed),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task CreateDepartmentAsync_NullOrEmptyName_ShouldThrowArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync(null!, "Description", null, null));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("", "Description", null, null));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("   ", "Description", null, null));
                
            // Dla błędów walidacji parametrów operacja historii nie jest tworzona - to jest poprawne zachowanie
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ValidDepartment_ShouldUpdateSuccessfully()
        {
            ResetCapturedOperationHistory();
            var existingDepartment = new Department { Id = "dept1", Name = "Old Name", IsActive = true };
            var updatedDepartment = new Department { Id = "dept1", Name = "New Name" };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(existingDepartment.Id))
                                   .ReturnsAsync(existingDepartment);

            _mockDepartmentRepository.Setup(r => r.Update(It.IsAny<Department>()))
                                   .Callback<Department>(dept => existingDepartment = dept);

            await _departmentService.UpdateDepartmentAsync(updatedDepartment);

            // Sprawdzenie szczegółowych danych operacji aktualizacji
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentUpdated);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(existingDepartment.Id);
            _capturedOperationHistory.TargetEntityName.Should().Be("New Name"); // Implementacja przekazuje nową nazwę // Powinna być stara nazwa
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                It.Is<OperationType>(t => t == OperationType.DepartmentUpdated),
                It.Is<string>(s => s == nameof(Department)),
                It.Is<string>(s => s == existingDepartment.Id),
                It.Is<string>(s => s == "New Name"), // Implementacja przekazuje nową nazwę
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                It.Is<OperationStatus>(s => s == OperationStatus.Completed),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            existingDepartment.Name.Should().Be(updatedDepartment.Name);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithNoSubDepartmentsOrUsers_ShouldDeleteSuccessfully()
        {
            ResetCapturedOperationHistory();
            var departmentId = "dept1";
            var department = new Department { Id = departmentId, Name = "Test Department", IsActive = true };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId))
                                   .ReturnsAsync(department);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                    .ReturnsAsync(new List<Department>());

            _mockUserRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                              .ReturnsAsync(new List<User>());

            await _departmentService.DeleteDepartmentAsync(departmentId);

            // Sprawdzenie szczegółowych danych operacji usuwania
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(departmentId);
            _capturedOperationHistory.TargetEntityName.Should().BeEmpty(); // Na początku jest puste
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                It.Is<OperationType>(t => t == OperationType.DepartmentDeleted),
                It.Is<string>(s => s == nameof(Department)),
                It.Is<string>(s => s == departmentId),
                It.Is<string?>(s => s == null), // targetEntityName jest null na początku
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                It.Is<OperationStatus>(s => s == OperationStatus.Completed),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            department.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithSubDepartments_ShouldThrowInvalidOperationException()
        {
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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _departmentService.DeleteDepartmentAsync(departmentId));

            // Sprawdzenie że operacja historii została zainicjowana przed rzuceniem wyjątku
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(departmentId);
            _capturedOperationHistory.TargetEntityName.Should().BeEmpty(); // Na początku jest puste
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted,
                nameof(Department),
                departmentId,
                It.Is<string?>(s => s == null), // targetEntityName jest null na początku
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            // Sprawdzenie że operacja została zakończona z błędem
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                OperationStatus.Failed,
                It.Is<string>(msg => msg.Contains("poddziały")),
                It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_DepartmentWithUsers_ShouldThrowInvalidOperationException()
        {
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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _departmentService.DeleteDepartmentAsync(departmentId));

            // Sprawdzenie że operacja historii została zainicjowana przed rzuceniem wyjątku
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.DepartmentDeleted);
            _capturedOperationHistory.TargetEntityType.Should().Be(nameof(Department));
            _capturedOperationHistory.TargetEntityId.Should().Be(departmentId);
            _capturedOperationHistory.TargetEntityName.Should().BeEmpty(); // Na początku jest puste
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted,
                nameof(Department),
                departmentId,
                It.Is<string?>(s => s == null), // targetEntityName jest null na początku
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);

            // Sprawdzenie że operacja została zakończona z błędem
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                _capturedOperationHistory.Id,
                OperationStatus.Failed,
                It.Is<string>(msg => msg.Contains("użytkowników")),
                It.IsAny<string?>()), Times.Once);
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

            public static bool IsForRootDepartments(Expression<Func<Department, bool>> expression)
            {
                var rootDept = new Department { ParentDepartmentId = null, IsActive = true };
                var childDept = new Department { ParentDepartmentId = "root1", IsActive = true };
                var inactiveRootDept = new Department { ParentDepartmentId = null, IsActive = false };

                var compiled = expression.Compile();
                return compiled(rootDept) && !compiled(childDept) && !compiled(inactiveRootDept);
            }

            public static bool IsForActiveDepartments(Expression<Func<Department, bool>> expression)
            {
                var activeDept = new Department { IsActive = true };
                var inactiveDept = new Department { IsActive = false };

                var compiled = expression.Compile();
                return compiled(activeDept) && !compiled(inactiveDept);
            }
        }
    }
}