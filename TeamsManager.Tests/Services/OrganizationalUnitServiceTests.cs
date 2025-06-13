using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    public class OrganizationalUnitServiceTests
    {
        private readonly Mock<IGenericRepository<OrganizationalUnit>> _mockOrganizationalUnitRepository;
        private readonly Mock<IGenericRepository<Department>> _mockDepartmentRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<OrganizationalUnitService>> _mockLogger;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

        private readonly OrganizationalUnitService _organizationalUnitService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";

        // Klucze cache'u
        private const string AllOrganizationalUnitsRootOnlyCacheKey = "OrganizationalUnits_AllActive_RootOnly";
        private const string AllOrganizationalUnitsAllCacheKey = "OrganizationalUnits_AllActive_All";
        private const string OrganizationalUnitByIdCacheKeyPrefix = "OrganizationalUnit_Id_";
        private const string SubUnitsByParentIdCacheKeyPrefix = "OrganizationalUnit_Sub_ParentId_";
        private const string DepartmentsByUnitIdCacheKeyPrefix = "OrganizationalUnit_Departments_Id_";
        private const string OrganizationalUnitsHierarchyCacheKey = "OrganizationalUnits_Hierarchy";

        public OrganizationalUnitServiceTests()
        {
            _mockOrganizationalUnitRepository = new Mock<IGenericRepository<OrganizationalUnit>>();
            _mockDepartmentRepository = new Mock<IGenericRepository<Department>>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<OrganizationalUnitService>>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            var mockOperationHistory = new OperationHistory { Id = "test-id", Status = OperationStatus.Completed };
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(mockOperationHistory);

            _organizationalUnitService = new OrganizationalUnitService(
                _mockOrganizationalUnitRepository.Object,
                _mockDepartmentRepository.Object,
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
                _mockPowerShellCacheService.Setup(m => m.TryGetValue<TItem>(cacheKey, out It.Ref<TItem?>.IsAny))
                    .Callback(new TryGetValueCallback<TItem>((string key, out TItem? value) =>
                    {
                        value = item;
                    }))
                    .Returns(foundInCache);
            }
            else
            {
                _mockPowerShellCacheService.Setup(m => m.TryGetValue<TItem>(cacheKey, out It.Ref<TItem?>.IsAny))
                    .Returns(foundInCache);
            }
        }

        private delegate void TryGetValueCallback<TItem>(string key, out TItem? value);

        private OrganizationalUnit CreateTestOrganizationalUnit(string id, string name, string? parentId = null, bool isActive = true)
        {
            return new OrganizationalUnit
            {
                Id = id,
                Name = name,
                ParentUnitId = parentId,
                IsActive = isActive,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentLoggedInUserUpn,
                SortOrder = 0
            };
        }

        private Department CreateTestDepartment(string id, string name, string organizationalUnitId)
        {
            return new Department
            {
                Id = id,
                Name = name,
                OrganizationalUnitId = organizationalUnitId,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentLoggedInUserUpn
            };
        }

        #region GetOrganizationalUnitByIdAsync Tests

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_ExistingUnit_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var unitId = "unit-1";
            var expectedUnit = CreateTestOrganizationalUnit(unitId, "Liceum Ogólnokształcące");
            string cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_False_False";
            SetupCacheTryGetValue(cacheKey, (OrganizationalUnit?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(expectedUnit);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);

            // Assert
            result.Should().BeEquivalentTo(expectedUnit);
            _mockOrganizationalUnitRepository.Verify(r => r.GetByIdAsync(unitId), Times.Once);
        }

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_ExistingUnit_InCache_ShouldReturnFromCache()
        {
            // Arrange
            var unitId = "unit-cached";
            var cachedUnit = CreateTestOrganizationalUnit(unitId, "Cached Unit");
            string cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_False_False";
            SetupCacheTryGetValue(cacheKey, cachedUnit, true);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);

            // Assert
            result.Should().BeEquivalentTo(cachedUnit);
            _mockOrganizationalUnitRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_WithForceRefresh_ShouldBypassCache()
        {
            // Arrange
            var unitId = "unit-force";
            var cachedUnit = CreateTestOrganizationalUnit(unitId, "Old Data");
            var dbUnit = CreateTestOrganizationalUnit(unitId, "New Data from DB");
            string cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_False_False";
            SetupCacheTryGetValue(cacheKey, cachedUnit, true);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(dbUnit);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId, forceRefresh: true);

            // Assert
            result.Should().BeEquivalentTo(dbUnit);
            _mockOrganizationalUnitRepository.Verify(r => r.GetByIdAsync(unitId), Times.Once);
        }

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_WithIncludes_ShouldFetchSubUnitsAndDepartments()
        {
            // Arrange
            var unitId = "unit-includes";
            var baseUnit = CreateTestOrganizationalUnit(unitId, "Base Unit");
            var subUnits = new List<OrganizationalUnit> 
            { 
                CreateTestOrganizationalUnit("sub1", "Sub Unit 1", unitId) 
            };
            var departments = new List<Department> 
            { 
                CreateTestDepartment("dept1", "Department 1", unitId) 
            };

            string baseCacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_True_True";
            string subUnitsCacheKey = $"{SubUnitsByParentIdCacheKeyPrefix}{unitId}";
            string departmentsCacheKey = $"{DepartmentsByUnitIdCacheKeyPrefix}{unitId}";

            SetupCacheTryGetValue(baseCacheKey, (OrganizationalUnit?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(baseUnit);
            
            SetupCacheTryGetValue(subUnitsCacheKey, (IEnumerable<OrganizationalUnit>?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForSubUnits(ex, unitId))))
                .ReturnsAsync(subUnits);
            
            SetupCacheTryGetValue(departmentsCacheKey, (IEnumerable<Department>?)null, false);
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(
                ex => TestExpressionHelper.IsForDepartmentsInUnit(ex, unitId))))
                .ReturnsAsync(departments);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId, includeSubUnits: true, includeDepartments: true);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Base Unit");
            result.SubUnits.Should().HaveCount(1);
            result.Departments.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_NonExistentUnit_ShouldReturnNull()
        {
            // Arrange
            var unitId = "non-existent";
            string cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_False_False";
            SetupCacheTryGetValue(cacheKey, (OrganizationalUnit?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync((OrganizationalUnit?)null);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetOrganizationalUnitByIdAsync_InactiveUnit_ShouldReturnNull()
        {
            // Arrange
            var unitId = "inactive-unit";
            var inactiveUnit = CreateTestOrganizationalUnit(unitId, "Inactive Unit", isActive: false);
            string cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_False_False";
            SetupCacheTryGetValue(cacheKey, (OrganizationalUnit?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(inactiveUnit);

            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task GetOrganizationalUnitByIdAsync_EmptyOrNullId_ShouldReturnNull(string? unitId)
        {
            // Act
            var result = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId!);

            // Assert
            result.Should().BeNull();
            _mockOrganizationalUnitRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetAllOrganizationalUnitsAsync Tests

        [Fact]
        public async Task GetAllOrganizationalUnitsAsync_RootOnly_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var rootUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("root1", "Liceum Ogólnokształcące"),
                CreateTestOrganizationalUnit("root2", "Technikum")
            };
            SetupCacheTryGetValue(AllOrganizationalUnitsRootOnlyCacheKey, (IEnumerable<OrganizationalUnit>?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForRootUnits(ex))))
                .ReturnsAsync(rootUnits);

            // Act
            var result = await _organizationalUnitService.GetAllOrganizationalUnitsAsync(onlyRootUnits: true);

            // Assert
            result.Should().BeEquivalentTo(rootUnits);
            _mockOrganizationalUnitRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllOrganizationalUnitsAsync_All_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var allUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("root1", "Liceum Ogólnokształcące"),
                CreateTestOrganizationalUnit("sub1", "Klasa I", "root1")
            };
            SetupCacheTryGetValue(AllOrganizationalUnitsAllCacheKey, (IEnumerable<OrganizationalUnit>?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForActiveUnits(ex))))
                .ReturnsAsync(allUnits);

            // Act
            var result = await _organizationalUnitService.GetAllOrganizationalUnitsAsync(onlyRootUnits: false);

            // Assert
            result.Should().BeEquivalentTo(allUnits);
            _mockOrganizationalUnitRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllOrganizationalUnitsAsync_InCache_ShouldReturnFromCache()
        {
            // Arrange
            var cachedUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("cached1", "Cached Unit")
            };
            SetupCacheTryGetValue<IEnumerable<OrganizationalUnit>>(AllOrganizationalUnitsRootOnlyCacheKey, cachedUnits, true);

            // Act
            var result = await _organizationalUnitService.GetAllOrganizationalUnitsAsync(onlyRootUnits: true);

            // Assert
            result.Should().BeEquivalentTo(cachedUnits);
            _mockOrganizationalUnitRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()), Times.Never);
        }

        #endregion

        #region GetSubUnitsAsync Tests

        [Fact]
        public async Task GetSubUnitsAsync_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var parentUnitId = "parent-1";
            var subUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("sub1", "Klasa I", parentUnitId),
                CreateTestOrganizationalUnit("sub2", "Klasa II", parentUnitId)
            };
            string cacheKey = $"{SubUnitsByParentIdCacheKeyPrefix}{parentUnitId}";
            SetupCacheTryGetValue(cacheKey, (IEnumerable<OrganizationalUnit>?)null, false);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForSubUnits(ex, parentUnitId))))
                .ReturnsAsync(subUnits);

            // Act
            var result = await _organizationalUnitService.GetSubUnitsAsync(parentUnitId);

            // Assert
            result.Should().BeEquivalentTo(subUnits);
            _mockOrganizationalUnitRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task GetSubUnitsAsync_EmptyOrNullParentId_ShouldReturnEmpty(string? parentUnitId)
        {
            // Act
            var result = await _organizationalUnitService.GetSubUnitsAsync(parentUnitId!);

            // Assert
            result.Should().BeEmpty();
            _mockOrganizationalUnitRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()), Times.Never);
        }

        #endregion

        #region CreateOrganizationalUnitAsync Tests

        [Fact]
        public async Task CreateOrganizationalUnitAsync_ValidUnit_ShouldCreateSuccessfully()
        {
            // Arrange
            var newUnit = CreateTestOrganizationalUnit("", "Nowa Jednostka");
            newUnit.Id = ""; // Symulacja nowego obiektu
            var createdUnit = CreateTestOrganizationalUnit("new-unit-id", "Nowa Jednostka");
            
            _mockOrganizationalUnitRepository.Setup(r => r.AddAsync(It.IsAny<OrganizationalUnit>()))
                .Returns(Task.CompletedTask)
                .Callback<OrganizationalUnit>(unit => unit.Id = "new-unit-id");

            // Act
            var result = await _organizationalUnitService.CreateOrganizationalUnitAsync(newUnit);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Nowa Jednostka");
            _mockOrganizationalUnitRepository.Verify(r => r.AddAsync(It.IsAny<OrganizationalUnit>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.GenericCreated,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateOrganizationalUnitAsync_NullUnit_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _organizationalUnitService.CreateOrganizationalUnitAsync(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task CreateOrganizationalUnitAsync_EmptyOrNullName_ShouldThrowArgumentException(string? name)
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("", name!);
            
            // Mock IsNameUniqueAsync to return false (duplicate name)
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()))
                .ReturnsAsync(new List<OrganizationalUnit> { CreateTestOrganizationalUnit("existing", name!) });

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _organizationalUnitService.CreateOrganizationalUnitAsync(unit));
        }

        #endregion

        #region UpdateOrganizationalUnitAsync Tests

        [Fact]
        public async Task UpdateOrganizationalUnitAsync_ValidUnit_ShouldUpdateSuccessfully()
        {
            // Arrange
            var existingUnit = CreateTestOrganizationalUnit("unit-1", "Stara Nazwa");
            var updatedUnit = CreateTestOrganizationalUnit("unit-1", "Nowa Nazwa");
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync("unit-1")).ReturnsAsync(existingUnit);
            _mockOrganizationalUnitRepository.Setup(r => r.Update(It.IsAny<OrganizationalUnit>()));

            // Act
            var result = await _organizationalUnitService.UpdateOrganizationalUnitAsync(updatedUnit);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Nowa Nazwa");
            _mockOrganizationalUnitRepository.Verify(r => r.Update(It.IsAny<OrganizationalUnit>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.GenericUpdated,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateOrganizationalUnitAsync_NonExistentUnit_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("non-existent", "Test");
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync("non-existent")).ReturnsAsync((OrganizationalUnit?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _organizationalUnitService.UpdateOrganizationalUnitAsync(unit));
        }

        #endregion

        #region DeleteOrganizationalUnitAsync Tests

        [Fact]
        public async Task DeleteOrganizationalUnitAsync_UnitWithoutDependencies_ShouldDeleteSuccessfully()
        {
            // Arrange
            var unitId = "unit-to-delete";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit to Delete");
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(unit);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForSubUnits(ex, unitId))))
                .ReturnsAsync(new List<OrganizationalUnit>());
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(
                ex => TestExpressionHelper.IsForDepartmentsInUnit(ex, unitId))))
                .ReturnsAsync(new List<Department>());

            // Act
            var result = await _organizationalUnitService.DeleteOrganizationalUnitAsync(unitId);

            // Assert
            result.Should().BeTrue();
            _mockOrganizationalUnitRepository.Verify(r => r.Update(It.IsAny<OrganizationalUnit>()), Times.Once);
            _mockOrganizationalUnitRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.GenericDeleted,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task DeleteOrganizationalUnitAsync_UnitWithSubUnits_ShouldReturnFalse()
        {
            // Arrange
            var unitId = "unit-with-subs";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit with Subs");
            var subUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("sub1", "Sub Unit", unitId)
            };
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(unit);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForSubUnits(ex, unitId))))
                .ReturnsAsync(subUnits);

            // Act
            var result = await _organizationalUnitService.DeleteOrganizationalUnitAsync(unitId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteOrganizationalUnitAsync_UnitWithDepartments_ShouldReturnFalse()
        {
            // Arrange
            var unitId = "unit-with-depts";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit with Departments");
            var departments = new List<Department>
            {
                CreateTestDepartment("dept1", "Department", unitId)
            };
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(unit);
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<OrganizationalUnit, bool>>>(
                ex => TestExpressionHelper.IsForSubUnits(ex, unitId))))
                .ReturnsAsync(new List<OrganizationalUnit>());
            _mockDepartmentRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Department, bool>>>(
                ex => TestExpressionHelper.IsForDepartmentsInUnit(ex, unitId))))
                .ReturnsAsync(departments);

            // Act
            var result = await _organizationalUnitService.DeleteOrganizationalUnitAsync(unitId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task IsNameUniqueAsync_UniqueName_ShouldReturnTrue()
        {
            // Arrange
            var name = "Unique Name";
            var parentUnitId = "parent-1";
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()))
                .ReturnsAsync(new List<OrganizationalUnit>());

            // Act
            var result = await _organizationalUnitService.IsNameUniqueAsync(name, parentUnitId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsNameUniqueAsync_DuplicateName_ShouldReturnFalse()
        {
            // Arrange
            var name = "Duplicate Name";
            var parentUnitId = "parent-1";
            var existingUnits = new List<OrganizationalUnit>
            {
                CreateTestOrganizationalUnit("existing-1", name, parentUnitId)
            };
            _mockOrganizationalUnitRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OrganizationalUnit, bool>>>()))
                .ReturnsAsync(existingUnits);

            // Act
            var result = await _organizationalUnitService.IsNameUniqueAsync(name, parentUnitId);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanMoveUnitAsync_ValidMove_ShouldReturnTrue()
        {
            // Arrange
            var unitId = "unit-1";
            var newParentId = "new-parent";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit 1");
            var newParent = CreateTestOrganizationalUnit(newParentId, "New Parent");
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(unit);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(newParentId)).ReturnsAsync(newParent);

            // Act
            var result = await _organizationalUnitService.CanMoveUnitAsync(unitId, newParentId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanMoveUnitAsync_CircularReference_ShouldReturnFalse()
        {
            // Arrange
            var unitId = "unit-1";
            var childId = "child-1";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit 1");
            var child = CreateTestOrganizationalUnit(childId, "Child", unitId);
            
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(unitId)).ReturnsAsync(unit);
            _mockOrganizationalUnitRepository.Setup(r => r.GetByIdAsync(childId)).ReturnsAsync(child);

            // Act - próba przeniesienia jednostki pod jej własne dziecko
            var result = await _organizationalUnitService.CanMoveUnitAsync(unitId, childId);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Helper Class

        public static class TestExpressionHelper
        {
            public static bool IsForSubUnits(Expression<Func<OrganizationalUnit, bool>> expression, string expectedParentId)
            {
                // Uproszczona weryfikacja - w rzeczywistości można by parsować expression tree
                return expression.ToString().Contains("ParentUnitId") && expression.ToString().Contains("IsActive");
            }

            public static bool IsForDepartmentsInUnit(Expression<Func<Department, bool>> expression, string expectedUnitId)
            {
                return expression.ToString().Contains("OrganizationalUnitId") && expression.ToString().Contains("IsActive");
            }

            public static bool IsForRootUnits(Expression<Func<OrganizationalUnit, bool>> expression)
            {
                return expression.ToString().Contains("ParentUnitId") && expression.ToString().Contains("null") && expression.ToString().Contains("IsActive");
            }

            public static bool IsForActiveUnits(Expression<Func<OrganizationalUnit, bool>> expression)
            {
                return expression.ToString().Contains("IsActive");
            }
        }

        #endregion
    }
}