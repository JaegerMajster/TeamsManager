using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions; // Dla ICurrentUserService
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Data.Repositories;
using TeamsManager.Tests.Infrastructure.Services;
using OperationHistoryRepository = TeamsManager.Data.Repositories.OperationHistoryRepository;
using UserRepository = TeamsManager.Data.Repositories.UserRepository;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Services.PowerShell;
using Xunit;

namespace TeamsManager.Tests.Integration
{
    [Collection("Sequential")]
    public class OrganizationalUnitIntegrationTests : IntegrationTestBase
    {
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly IDepartmentService _departmentService;
        private readonly TestCurrentUserService _testCurrentUserService;

        public OrganizationalUnitIntegrationTests()
        {
            _organizationalUnitService = ServiceProvider.GetRequiredService<IOrganizationalUnitService>();
            _departmentService = ServiceProvider.GetRequiredService<IDepartmentService>();
            _testCurrentUserService = (TestCurrentUserService)ServiceProvider.GetRequiredService<ICurrentUserService>();
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);
            
            // Rejestracja repozytoriów
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            
            // Rejestracja pozostałych serwisów
            services.AddScoped<IOperationHistoryService, OperationHistoryService>();
            services.AddScoped<INotificationService, StubNotificationService>();
            services.AddScoped<IPowerShellCacheService, PowerShellCacheService>();
            services.AddScoped<IMemoryCache, MemoryCache>();
            services.AddLogging();
            
            // Rejestracja serwisów specyficznych dla testów OrganizationalUnit
            services.AddScoped<IOrganizationalUnitService, OrganizationalUnitService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
        }

        private async Task<OrganizationalUnit> CreateTestOrganizationalUnitAsync(string name, string? parentId = null, int sortOrder = 0)
        {
            var unit = new OrganizationalUnit
            {
                Name = name,
                Description = $"Test description for {name}",
                ParentUnitId = parentId,
                SortOrder = sortOrder
            };

            return await _organizationalUnitService.CreateOrganizationalUnitAsync(unit);
        }

        private async Task<Department> CreateTestDepartmentAsync(string name, string organizationalUnitId)
        {
            var department = await _departmentService.CreateDepartmentAsync(
                name, 
                $"Test department {name}", 
                null, 
                null);
            
            // Ustawienie OrganizationalUnitId po utworzeniu
            if (department != null)
            {
                department.OrganizationalUnitId = organizationalUnitId;
                await _departmentService.UpdateDepartmentAsync(department);
            }

            return department!;
        }

        #region Hierarchical Structure Tests

        [Fact]
        public async Task CreateOrganizationalUnitHierarchy_ShouldCreateCompleteStructure()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("integration.test@example.com");

            // Act - Tworzenie hierarchii: Szkoła -> Liceum -> Klasa I
            var school = await CreateTestOrganizationalUnitAsync("Zespół Szkół Technicznych", sortOrder: 1);
            var highSchool = await CreateTestOrganizationalUnitAsync("Liceum Ogólnokształcące", school.Id, sortOrder: 1);
            var class1 = await CreateTestOrganizationalUnitAsync("Klasa I A", highSchool.Id, sortOrder: 1);
            var class2 = await CreateTestOrganizationalUnitAsync("Klasa I B", highSchool.Id, sortOrder: 2);

            // Assert - Sprawdzenie struktury
            school.Should().NotBeNull();
            school.IsRootUnit.Should().BeTrue();
            school.Level.Should().Be(0);

            highSchool.Should().NotBeNull();
            highSchool.ParentUnitId.Should().Be(school.Id);
            highSchool.IsRootUnit.Should().BeFalse();

            class1.Should().NotBeNull();
            class1.ParentUnitId.Should().Be(highSchool.Id);
            class1.Level.Should().Be(2);

            // Sprawdzenie pełnej ścieżki
            class1.FullPath.Should().Contain("Zespół Szkół Technicznych");
            class1.FullPath.Should().Contain("Liceum Ogólnokształcące");
            class1.FullPath.Should().Contain("Klasa I A");

            // Sprawdzenie podjednostek
            var subUnits = await _organizationalUnitService.GetSubUnitsAsync(highSchool.Id);
            subUnits.Should().HaveCount(2);
            subUnits.Should().Contain(u => u.Name == "Klasa I A");
            subUnits.Should().Contain(u => u.Name == "Klasa I B");
        }

        [Fact]
        public async Task GetOrganizationalUnitsHierarchy_ShouldReturnCompleteTree()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("hierarchy.test@example.com");
            
            var root1 = await CreateTestOrganizationalUnitAsync("Liceum", sortOrder: 1);
            var root2 = await CreateTestOrganizationalUnitAsync("Technikum", sortOrder: 2);
            var sub1 = await CreateTestOrganizationalUnitAsync("Klasa I", root1.Id, sortOrder: 1);
            var sub2 = await CreateTestOrganizationalUnitAsync("Klasa II", root1.Id, sortOrder: 2);

            // Act
            var hierarchy = await _organizationalUnitService.GetOrganizationalUnitsHierarchyAsync();

            // Assert
            hierarchy.Should().NotBeEmpty();
            var hierarchyList = hierarchy.ToList();
            
            // Sprawdzenie czy jednostki główne są obecne
            hierarchyList.Should().Contain(u => u.Name == "Liceum");
            hierarchyList.Should().Contain(u => u.Name == "Technikum");

            // Sprawdzenie sortowania jednostek głównych
            var rootUnits = hierarchyList.Where(u => u.IsRootUnit).OrderBy(u => u.SortOrder).ToList();
            rootUnits[0].Name.Should().Be("Liceum");
            rootUnits[1].Name.Should().Be("Technikum");

            // Sprawdzenie podjednostek w Liceum
            var liceumUnit = hierarchyList.First(u => u.Name == "Liceum");
            liceumUnit.SubUnits.Should().NotBeNull();
            liceumUnit.SubUnits.Should().HaveCount(2);
            liceumUnit.SubUnits.Should().Contain(u => u.Name == "Klasa I");
            liceumUnit.SubUnits.Should().Contain(u => u.Name == "Klasa II");

            // Sprawdzenie sortowania podjednostek
            var subUnits = liceumUnit.SubUnits.OrderBy(u => u.SortOrder).ToList();
            subUnits[0].Name.Should().Be("Klasa I");
            subUnits[1].Name.Should().Be("Klasa II");
        }

        #endregion

        #region Department Integration Tests

        [Fact]
        public async Task OrganizationalUnitWithDepartments_ShouldManageRelationshipCorrectly()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("dept.integration@example.com");
            
            var unit = await CreateTestOrganizationalUnitAsync("Wydział Informatyki");
            
            // Act - Dodanie działów do jednostki
            var dept1 = await CreateTestDepartmentAsync("Katedra Programowania", unit.Id);
            var dept2 = await CreateTestDepartmentAsync("Katedra Sieci", unit.Id);

            // Assert - Sprawdzenie relacji
            var departmentsInUnit = await _organizationalUnitService.GetDepartmentsByOrganizationalUnitAsync(unit.Id);
            departmentsInUnit.Should().HaveCount(2);
            departmentsInUnit.Should().Contain(d => d.Name == "Katedra Programowania");
            departmentsInUnit.Should().Contain(d => d.Name == "Katedra Sieci");

            // Sprawdzenie czy jednostka z działami nie może być usunięta
            var canDelete = await _organizationalUnitService.CanDeleteOrganizationalUnitAsync(unit.Id);
            canDelete.Should().BeFalse();

            // Próba usunięcia powinna zwrócić false (nie rzuca wyjątku)
            var deleteResult = await _organizationalUnitService.DeleteOrganizationalUnitAsync(unit.Id);
            deleteResult.Should().BeFalse();
        }

        [Fact]
        public async Task MoveDepartmentsBetweenOrganizationalUnits_ShouldUpdateRelationships()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("move.dept@example.com");
            
            var sourceUnit = await CreateTestOrganizationalUnitAsync("Jednostka Źródłowa");
            var targetUnit = await CreateTestOrganizationalUnitAsync("Jednostka Docelowa");
            
            var dept1 = await CreateTestDepartmentAsync("Dział 1", sourceUnit.Id);
            var dept2 = await CreateTestDepartmentAsync("Dział 2", sourceUnit.Id);

            // Act - Przeniesienie działów
            var movedCount = await _organizationalUnitService.MoveDepartmentsToOrganizationalUnitAsync(
                new[] { dept1.Id, dept2.Id }, targetUnit.Id);

            // Assert
            movedCount.Should().Be(2);

            // Sprawdzenie czy działy zostały przeniesione
            var sourceDepartments = await _organizationalUnitService.GetDepartmentsByOrganizationalUnitAsync(sourceUnit.Id);
            var targetDepartments = await _organizationalUnitService.GetDepartmentsByOrganizationalUnitAsync(targetUnit.Id);

            sourceDepartments.Should().BeEmpty();
            targetDepartments.Should().HaveCount(2);
            targetDepartments.Should().Contain(d => d.Name == "Dział 1");
            targetDepartments.Should().Contain(d => d.Name == "Dział 2");
        }

        #endregion

        #region Validation and Business Rules Tests

        [Fact]
        public async Task CreateOrganizationalUnit_WithDuplicateName_ShouldEnforceUniqueness()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("validation.test@example.com");
            
            var parent = await CreateTestOrganizationalUnitAsync("Rodzic");
            await CreateTestOrganizationalUnitAsync("Duplikat", parent.Id);

            // Act & Assert - Próba utworzenia drugiej jednostki o tej samej nazwie w tym samym rodzicu
            var duplicateUnit = new OrganizationalUnit
            {
                Name = "Duplikat",
                ParentUnitId = parent.Id
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _organizationalUnitService.CreateOrganizationalUnitAsync(duplicateUnit));
        }

        [Fact]
        public async Task CreateOrganizationalUnit_WithSameNameInDifferentParents_ShouldBeAllowed()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("different.parents@example.com");
            
            var parent1 = await CreateTestOrganizationalUnitAsync("Rodzic 1");
            var parent2 = await CreateTestOrganizationalUnitAsync("Rodzic 2");

            // Act - Utworzenie jednostek o tej samej nazwie w różnych rodzicach
            var unit1 = await CreateTestOrganizationalUnitAsync("Klasa I", parent1.Id);
            var unit2 = await CreateTestOrganizationalUnitAsync("Klasa I", parent2.Id);

            // Assert
            unit1.Should().NotBeNull();
            unit2.Should().NotBeNull();
            unit1.Id.Should().NotBe(unit2.Id);
            unit1.ParentUnitId.Should().Be(parent1.Id);
            unit2.ParentUnitId.Should().Be(parent2.Id);
        }

        [Fact]
        public async Task MoveOrganizationalUnit_CircularReference_ShouldBeBlocked()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("circular.test@example.com");
            
            var grandParent = await CreateTestOrganizationalUnitAsync("Dziadek");
            var parent = await CreateTestOrganizationalUnitAsync("Rodzic", grandParent.Id);
            var child = await CreateTestOrganizationalUnitAsync("Dziecko", parent.Id);

            // Act & Assert - Próba przeniesienia dziadka pod dziecko (cykl)
            var canMove = await _organizationalUnitService.CanMoveUnitAsync(grandParent.Id, child.Id);
            canMove.Should().BeFalse();

            // Próba przeniesienia rodzica pod dziecko (cykl)
            var canMoveParent = await _organizationalUnitService.CanMoveUnitAsync(parent.Id, child.Id);
            canMoveParent.Should().BeFalse();

            // Prawidłowe przeniesienie - dziecko pod dziadka (pominięcie rodzica)
            var canMoveChild = await _organizationalUnitService.CanMoveUnitAsync(child.Id, grandParent.Id);
            canMoveChild.Should().BeTrue();
        }

        #endregion

        #region Cache Integration Tests

        [Fact]
        public async Task OrganizationalUnitCaching_ShouldWorkCorrectlyWithRealOperations()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("cache.test@example.com");
            
            var unit = await CreateTestOrganizationalUnitAsync("Jednostka Cache");

            // Act - Pierwsze pobranie (z bazy danych)
            var firstFetch = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id);
            
            // Drugie pobranie (z cache)
            var secondFetch = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id);

            // Assert
            firstFetch.Should().NotBeNull();
            secondFetch.Should().NotBeNull();
            firstFetch.Should().BeEquivalentTo(secondFetch);

            // Test force refresh
            var forceRefresh = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id, forceRefresh: true);
            forceRefresh.Should().NotBeNull();
            forceRefresh.Should().BeEquivalentTo(firstFetch);
        }

        [Fact]
        public async Task OrganizationalUnitCRUD_ShouldInvalidateCacheCorrectly()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("crud.cache@example.com");
            
            var unit = await CreateTestOrganizationalUnitAsync("CRUD Test");

            // Pobranie do cache
            await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id);
            await _organizationalUnitService.GetAllOrganizationalUnitsAsync();

            // Act - Aktualizacja powinna unieważnić cache
            unit.Name = "CRUD Test - Updated";
            var updatedUnit = await _organizationalUnitService.UpdateOrganizationalUnitAsync(unit);

            // Assert - Sprawdzenie czy dane zostały zaktualizowane
            var fetchedAfterUpdate = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unit.Id);
            fetchedAfterUpdate.Should().NotBeNull();
            fetchedAfterUpdate!.Name.Should().Be("CRUD Test - Updated");

            // Sprawdzenie czy lista została również unieważniona
            var allUnits = await _organizationalUnitService.GetAllOrganizationalUnitsAsync();
            allUnits.Should().Contain(u => u.Name == "CRUD Test - Updated");
        }

        #endregion

        #region Performance and Stress Tests

        [Fact]
        public async Task CreateLargeOrganizationalUnitHierarchy_ShouldPerformWell()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("performance.test@example.com");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act - Utworzenie większej hierarchii (szkoła -> 5 wydziałów -> po 10 klas każdy)
            var school = await CreateTestOrganizationalUnitAsync("Duża Szkoła");
            
            var faculties = new List<OrganizationalUnit>();
            for (int i = 1; i <= 5; i++)
            {
                var faculty = await CreateTestOrganizationalUnitAsync($"Wydział {i}", school.Id, i);
                faculties.Add(faculty);
            }

            var allClasses = new List<OrganizationalUnit>();
            foreach (var faculty in faculties)
            {
                for (int j = 1; j <= 10; j++)
                {
                    var classUnit = await CreateTestOrganizationalUnitAsync($"Klasa {j}", faculty.Id, j);
                    allClasses.Add(classUnit);
                }
            }

            stopwatch.Stop();

            // Assert - Sprawdzenie wydajności i poprawności
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Mniej niż 10 sekund
            
            faculties.Should().HaveCount(5);
            allClasses.Should().HaveCount(50);

            // Sprawdzenie hierarchii
            var hierarchy = await _organizationalUnitService.GetOrganizationalUnitsHierarchyAsync();
            hierarchy.Should().Contain(u => u.Name == "Duża Szkoła");
            
            // Sprawdzenie czy szkoła główna ma 5 wydziałów
            var mainSchool = hierarchy.First(u => u.Name == "Duża Szkoła");
            mainSchool.SubUnits.Should().HaveCount(5);
            
            // Sprawdzenie czy każdy wydział ma 10 klas (łącznie 50 klas)
            var totalClasses = mainSchool.SubUnits.SelectMany(w => w.SubUnits).Count();
            totalClasses.Should().Be(50);
        }

        [Fact]
        public async Task GetHierarchyPath_ShouldReturnCorrectPath()
        {
            // Arrange
            _testCurrentUserService.SetCurrentUserUpn("path.test@example.com");
            
            var level1 = await CreateTestOrganizationalUnitAsync("Poziom 1");
            var level2 = await CreateTestOrganizationalUnitAsync("Poziom 2", level1.Id);
            var level3 = await CreateTestOrganizationalUnitAsync("Poziom 3", level2.Id);
            var level4 = await CreateTestOrganizationalUnitAsync("Poziom 4", level3.Id);

            // Act
            var path = await _organizationalUnitService.GetHierarchyPathAsync(level4.Id);

            // Assert
            var pathList = path.ToList();
            pathList.Should().HaveCount(4);
            pathList[0].Name.Should().Be("Poziom 1");
            pathList[1].Name.Should().Be("Poziom 2");
            pathList[2].Name.Should().Be("Poziom 3");
            pathList[3].Name.Should().Be("Poziom 4");

            // Sprawdzenie poziomów
            pathList[0].Level.Should().Be(0);
            pathList[1].Level.Should().Be(1);
            pathList[2].Level.Should().Be(2);
            pathList[3].Level.Should().Be(3);
        }

        #endregion

        #region Cleanup

        // Cleanup jest obsługiwany automatycznie przez IntegrationTestBase.Dispose()

        #endregion
    }
}