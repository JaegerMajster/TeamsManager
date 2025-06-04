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
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<DepartmentService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

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
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            // Usunięto setup dla Update, ponieważ zakładamy, że SaveOperationHistoryAsync zawsze dodaje nowy wpis.
            // Jeśli jakaś logika serwisu bezpośrednio wywołuje Update na _operationHistoryRepository,
            // ten setup może być potrzebny dla konkretnych testów.

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            // Setup dla GetDefaultCacheEntryOptions w PowerShellCacheService
            _mockPowerShellCacheService.Setup(p => p.GetDefaultCacheEntryOptions())
                                       .Returns(new MemoryCacheEntryOptions());

            _departmentService = new DepartmentService(
                _mockDepartmentRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object,
                _mockPowerShellCacheService.Object
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

        private void AssertCacheInvalidationByReFetchingAllDepartments(List<Department> expectedDbDeptsAfterOperation, bool rootOnly)
        {
            string cacheKeyToReFetch = rootOnly ? AllDepartmentsRootOnlyCacheKey : AllDepartmentsAllCacheKey;

            SetupCacheTryGetValue(cacheKeyToReFetch, (IEnumerable<Department>?)null, false);

            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(expectedDbDeptsAfterOperation)
                                   .Verifiable();

            var resultAfterInvalidation = _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: rootOnly).Result;

            _mockDepartmentRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()), Times.AtLeastOnce, $"GetAllDepartmentsAsync(rootOnly:{rootOnly}) powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbDeptsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKeyToReFetch), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }

        // --- Testy dla GetDepartmentByIdAsync ---
        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_NotInCache_ShouldReturnAndCache()
        {
            var departmentId = "dept-1";
            var expectedDepartment = new Department { Id = departmentId, Name = "IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, (Department?)null, false);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(expectedDepartment);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

            result.Should().BeEquivalentTo(expectedDepartment);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetDepartmentByIdAsync_ExistingDepartment_InCache_ShouldReturnFromCache()
        {
            var departmentId = "dept-cached";
            var cachedDepartment = new Department { Id = departmentId, Name = "Cached IT" };
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, cachedDepartment, true);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId);

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
            SetupCacheTryGetValue(cacheKey, cachedDept, true);
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(dbDept);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbDept);
            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
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

            SetupCacheTryGetValue(baseCacheKey, baseDepartment, true);
            SetupCacheTryGetValue(subDeptsCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))))
                                   .ReturnsAsync(subDepts);
            SetupCacheTryGetValue(usersCacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))))
                               .ReturnsAsync(users);

            var result = await _departmentService.GetDepartmentByIdAsync(departmentId, includeSubDepartments: true, includeUsers: true);

            result.Should().NotBeNull();
            result!.Name.Should().Be("Base Dept");
            result.SubDepartments.Should().BeEquivalentTo(subDepts);
            result.Users.Should().BeEquivalentTo(users);

            _mockDepartmentRepository.Verify(r => r.GetByIdAsync(departmentId), Times.Never);
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))), Times.Once);
            _mockUserRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))), Times.Once);

            _mockMemoryCache.Verify(m => m.CreateEntry(subDeptsCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(usersCacheKey), Times.Once);
        }

        // --- Testy dla GetAllDepartmentsAsync ---
        [Fact]
        public async Task GetAllDepartmentsAsync_RootOnly_NotInCache_ShouldReturnAndCache()
        {
            var rootDepts = new List<Department> { new Department { Id = "root1", ParentDepartmentId = null } };
            SetupCacheTryGetValue(AllDepartmentsRootOnlyCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForRootDepartments(ex))))
                                    .ReturnsAsync(rootDepts);

            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: true);

            result.Should().BeEquivalentTo(rootDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllDepartmentsRootOnlyCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllDepartmentsAsync_All_NotInCache_ShouldReturnAndCache()
        {
            var allDepts = new List<Department> { new Department { Id = "all1" }, new Department { Id = "all2", ParentDepartmentId = "all1" } };
            SetupCacheTryGetValue(AllDepartmentsAllCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForActiveDepartments(ex))))
                                  .ReturnsAsync(allDepts);

            var result = await _departmentService.GetAllDepartmentsAsync(onlyRootDepartments: false);

            result.Should().BeEquivalentTo(allDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllDepartmentsAllCacheKey), Times.Once);
        }

        // --- Testy dla GetSubDepartmentsAsync ---
        [Fact]
        public async Task GetSubDepartmentsAsync_NotInCache_ShouldReturnAndCache()
        {
            var parentId = "parent1";
            var subDepts = new List<Department> { new Department { Id = "sub1", ParentDepartmentId = parentId } };
            string cacheKey = SubDepartmentsByParentIdCacheKeyPrefix + parentId;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, parentId))))
                                    .ReturnsAsync(subDepts);

            var result = await _departmentService.GetSubDepartmentsAsync(parentId);

            result.Should().BeEquivalentTo(subDepts);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy dla GetUsersInDepartmentAsync ---
        [Fact]
        public async Task GetUsersInDepartmentAsync_NotInCache_ShouldReturnAndCache()
        {
            var departmentId = "deptWithUsers";
            var users = new List<User> { new User { Id = "user1", DepartmentId = departmentId } };
            string cacheKey = UsersInDepartmentCacheKeyPrefix + departmentId;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))))
                                .ReturnsAsync(users);

            var result = await _departmentService.GetUsersInDepartmentAsync(departmentId);

            result.Should().BeEquivalentTo(users);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
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
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var resultDepartment = await _departmentService.CreateDepartmentAsync(departmentName, departmentDescription);

            resultDepartment.Should().NotBeNull();
            addedDepartmentToRepository.Should().NotBeNull();
            var createdDeptId = resultDepartment!.Id;

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityName == departmentName && op.Type == OperationType.DepartmentCreated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            if (resultDepartment.ParentDepartmentId != null)
            {
                _mockPowerShellCacheService.Verify(p => p.InvalidateSubDepartments(resultDepartment.ParentDepartmentId), Times.Once);
            }

            var expectedDeptsAfterCreate = new List<Department> { resultDepartment };
            AssertCacheInvalidationByReFetchingAllDepartments(expectedDeptsAfterCreate, false);
            AssertCacheInvalidationByReFetchingAllDepartments(
                resultDepartment.ParentDepartmentId == null ? expectedDeptsAfterCreate : new List<Department>(),
                true);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentCreated);
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
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _departmentService.UpdateDepartmentAsync(updatedDeptData);
            result.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == deptId && op.Type == OperationType.DepartmentUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateDepartment(deptId), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateSubDepartments(oldParentId), Times.Once);
            if (!string.IsNullOrEmpty(newParentId))
            {
                _mockPowerShellCacheService.Verify(p => p.InvalidateSubDepartments(newParentId), Times.Once);
            }

            var expectedDeptAfterUpdate = new Department
            {
                Id = deptId,
                Name = "New",
                ParentDepartmentId = newParentId,
                IsActive = true,
                ParentDepartment = newParentDept
            };
            var expectedDeptsAfterUpdateList = new List<Department> { expectedDeptAfterUpdate };

            AssertCacheInvalidationByReFetchingAllDepartments(expectedDeptsAfterUpdateList, false);
            AssertCacheInvalidationByReFetchingAllDepartments(
                expectedDeptAfterUpdate.ParentDepartmentId == null ? expectedDeptsAfterUpdateList : new List<Department>(),
                true);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentUpdated);
        }

        [Fact]
        public async Task DeleteDepartmentAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var deptId = "dept-delete-cache";
            var parentId = "parentDel";
            var deptToDelete = new Department { Id = deptId, Name = "ToDelete", ParentDepartmentId = parentId, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(deptId)).ReturnsAsync(deptToDelete);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, deptId))))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, deptId))))
                              .ReturnsAsync(new List<User>());

            var result = await _departmentService.DeleteDepartmentAsync(deptId);
            result.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == deptId && op.Type == OperationType.DepartmentDeleted)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            // Sprawdzenie wywołań nowych metod PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateDepartment(deptId), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllDepartmentLists(), Times.Once);
            _mockPowerShellCacheService.Verify(p => p.InvalidateSubDepartments(parentId), Times.Once);

            AssertCacheInvalidationByReFetchingAllDepartments(new List<Department>(), false);
            AssertCacheInvalidationByReFetchingAllDepartments(new List<Department>(), true);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.DepartmentDeleted);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidationForAllDepartmentKeys()
        {
            await _departmentService.RefreshCacheAsync();

            // Sprawdzenie wywołania nowej metody PowerShellCacheService
            _mockPowerShellCacheService.Verify(p => p.InvalidateAllCache(), Times.Once);

            SetupCacheTryGetValue(AllDepartmentsAllCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Department, bool>>>()))
                                   .ReturnsAsync(new List<Department>())
                                   .Verifiable();

            await _departmentService.GetAllDepartmentsAsync();
            _mockDepartmentRepository.Verify(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForActiveDepartments(ex))), Times.Once);
        }

        // --- Test CreateDepartmentAsync ---
        [Fact]
        public async Task CreateDepartmentAsync_ValidInputs_ShouldCreateDepartmentSuccessfully()
        {
            ResetCapturedOperationHistory();
            var name = "Nowy Dział";
            var description = "Opis nowego działu";
            var departmentCode = "ND";
            Department? addedDepartment = null;
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        dept.Id = Guid.NewGuid().ToString();
                                        addedDepartment = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            var result = await _departmentService.CreateDepartmentAsync(name, description, null, departmentCode);

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
            ResetCapturedOperationHistory();
            var name = "Subdział";
            var description = "Opis poddziału";
            var parentId = "parent-dept-id";
            var departmentCode = "SD";
            var parentDepartment = new Department { Id = parentId, Name = "Parent Dept", IsActive = true };
            Department? addedDepartment = null;

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(parentId)).ReturnsAsync(parentDepartment);
            _mockDepartmentRepository.Setup(r => r.AddAsync(It.IsAny<Department>()))
                                    .Callback<Department>(dept =>
                                    {
                                        dept.Id = Guid.NewGuid().ToString();
                                        addedDepartment = dept;
                                    })
                                    .Returns(Task.CompletedTask);

            var result = await _departmentService.CreateDepartmentAsync(name, description, parentId, departmentCode);

            result.Should().NotBeNull();
            addedDepartment.Should().NotBeNull();
            result!.ParentDepartmentId.Should().Be(parentId);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task CreateDepartmentAsync_NullOrEmptyName_ShouldThrowArgumentException()
        {
            ResetCapturedOperationHistory();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync(null!, "Description"));
            // Po rzuceniu wyjątku, _capturedOperationHistory może nie być w pełni zaktualizowane,
            // jeśli SaveOperationHistoryAsync jest w bloku finally, a MarkAsFailed jest wołane przed rzuceniem.
            // Aby to przetestować, musielibyśmy przechwycić wyjątek i sprawdzić _capturedOperationHistory
            // lub upewnić się, że MarkAsFailed jest ostatnią operacją przed rzuceniem.
            // Zgodnie z implementacją CreateDepartmentAsync, MarkAsFailed jest wołane PRZED rzuceniem wyjątku,
            // a SaveOperationHistoryAsync w bloku finally powinno zapisać ten stan.
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Status == OperationStatus.Failed)), Times.AtLeastOnce());


            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("", "Description"));
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Status == OperationStatus.Failed)), Times.AtLeastOnce());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _departmentService.CreateDepartmentAsync("   ", "Description"));
            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.Status == OperationStatus.Failed)), Times.AtLeastOnce());
        }

        [Fact]
        public async Task UpdateDepartmentAsync_ValidDepartment_ShouldUpdateSuccessfully()
        {
            ResetCapturedOperationHistory();
            var departmentId = "dept-to-update";
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Old Name",
                Description = "Old Description",
                IsActive = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
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

            await _departmentService.UpdateDepartmentAsync(updatedDepartment);

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
            ResetCapturedOperationHistory();
            var departmentId = "dept-to-delete";
            var department = new Department { Id = departmentId, Name = "To Delete", IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };

            _mockDepartmentRepository.Setup(r => r.GetByIdAsync(departmentId)).ReturnsAsync(department);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))))
                              .ReturnsAsync(new List<User>());
            _mockDepartmentRepository.Setup(r => r.Update(It.IsAny<Department>())).Verifiable();

            await _departmentService.DeleteDepartmentAsync(departmentId);

            _mockDepartmentRepository.Verify(r => r.Update(It.Is<Department>(d =>
                d.Id == departmentId && d.IsActive == false)), Times.Once);
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
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
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))))
                                    .ReturnsAsync(subDepartments);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _departmentService.DeleteDepartmentAsync(departmentId));

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
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
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(ex => TestExpressionHelper.IsForSubDepartments(ex, departmentId))))
                                    .ReturnsAsync(new List<Department>());
            _mockUserRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<User, bool>>>(ex => TestExpressionHelper.IsForUsersInDepartment(ex, departmentId))))
                              .ReturnsAsync(users);

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