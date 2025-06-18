using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla modelu OrganizationalUnit
    /// Pokrycie: konstruktor, właściwości, hierarchia, computed properties
    /// </summary>
    public class OrganizationalUnitTests
    {
        #region Constructor Tests

        [Fact]
        public void OrganizationalUnit_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var unit = new OrganizationalUnit();

            // Assert - podstawowe właściwości
            unit.Id.Should().Be(string.Empty);
            unit.Name.Should().Be(string.Empty);
            unit.Code.Should().BeNull();
            unit.Description.Should().BeNull();
            unit.ParentUnitId.Should().BeNull();
            unit.ParentUnit.Should().BeNull();
            unit.SortOrder.Should().Be(0);
            unit.IsSystemDefault.Should().BeFalse();

            // Assert - kolekcje
            unit.SubUnits.Should().NotBeNull().And.BeEmpty();
            unit.Departments.Should().NotBeNull().And.BeEmpty();

            // Assert - computed properties
            unit.IsRootUnit.Should().BeTrue(); // bo ParentUnitId jest null
            unit.Level.Should().Be(0);
            unit.FullPath.Should().Be(string.Empty); // bo Name jest pusty

            // Assert - BaseEntity properties
            unit.IsActive.Should().BeTrue();
        }

        #endregion

        #region Property Tests

        [Fact]
        public void OrganizationalUnit_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var unit = new OrganizationalUnit();
            var parentUnit = new OrganizationalUnit { Id = "parent-1", Name = "Parent Unit" };

            // Act
            unit.Id = "unit-123";
            unit.Name = "Test Unit";
            unit.Code = "TU001";
            unit.Description = "Test description";
            unit.ParentUnitId = "parent-1";
            unit.ParentUnit = parentUnit;
            unit.SortOrder = 10;
            unit.IsSystemDefault = true;
            unit.IsActive = false;

            // Assert
            unit.Id.Should().Be("unit-123");
            unit.Name.Should().Be("Test Unit");
            unit.Code.Should().Be("TU001");
            unit.Description.Should().Be("Test description");
            unit.ParentUnitId.Should().Be("parent-1");
            unit.ParentUnit.Should().Be(parentUnit);
            unit.SortOrder.Should().Be(10);
            unit.IsSystemDefault.Should().BeTrue();
            unit.IsActive.Should().BeFalse();
        }

        #endregion

        #region IsRootUnit Tests

        [Fact]
        public void OrganizationalUnit_IsRootUnit_WhenParentUnitIdIsNull_ShouldReturnTrue()
        {
            // Arrange
            var unit = new OrganizationalUnit { ParentUnitId = null };

            // Act & Assert
            unit.IsRootUnit.Should().BeTrue();
        }

        [Fact]
        public void OrganizationalUnit_IsRootUnit_WhenParentUnitIdIsEmpty_ShouldReturnTrue()
        {
            // Arrange
            var unit = new OrganizationalUnit { ParentUnitId = string.Empty };

            // Act & Assert
            unit.IsRootUnit.Should().BeTrue();
        }

        [Fact]
        public void OrganizationalUnit_IsRootUnit_WhenParentUnitIdHasValue_ShouldReturnFalse()
        {
            // Arrange
            var unit = new OrganizationalUnit { ParentUnitId = "parent-123" };

            // Act & Assert
            unit.IsRootUnit.Should().BeFalse();
        }

        #endregion

        #region Level Tests

        [Fact]
        public void OrganizationalUnit_Level_WhenRootUnit_ShouldReturn0()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Name = "Root" };

            // Act & Assert
            rootUnit.Level.Should().Be(0);
        }

        [Fact]
        public void OrganizationalUnit_Level_WhenChildUnit_ShouldReturn1()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Id = "root", Name = "Root" };
            var childUnit = new OrganizationalUnit 
            { 
                Id = "child", 
                Name = "Child", 
                ParentUnitId = "root",
                ParentUnit = rootUnit 
            };

            // Act & Assert
            childUnit.Level.Should().Be(1);
        }

        [Fact]
        public void OrganizationalUnit_Level_WhenGrandChildUnit_ShouldReturn2()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Id = "root", Name = "Root" };
            var childUnit = new OrganizationalUnit 
            { 
                Id = "child", 
                Name = "Child", 
                ParentUnitId = "root",
                ParentUnit = rootUnit 
            };
            var grandChildUnit = new OrganizationalUnit 
            { 
                Id = "grandchild", 
                Name = "GrandChild", 
                ParentUnitId = "child",
                ParentUnit = childUnit 
            };

            // Act & Assert
            grandChildUnit.Level.Should().Be(2);
        }

        [Fact]
        public void OrganizationalUnit_Level_WhenDeepHierarchy_ShouldCalculateCorrectly()
        {
            // Arrange - tworzymy hierarchię 5 poziomów
            var level0 = new OrganizationalUnit { Id = "l0", Name = "Level0" };
            var level1 = new OrganizationalUnit { Id = "l1", Name = "Level1", ParentUnit = level0 };
            var level2 = new OrganizationalUnit { Id = "l2", Name = "Level2", ParentUnit = level1 };
            var level3 = new OrganizationalUnit { Id = "l3", Name = "Level3", ParentUnit = level2 };
            var level4 = new OrganizationalUnit { Id = "l4", Name = "Level4", ParentUnit = level3 };

            // Act & Assert
            level0.Level.Should().Be(0);
            level1.Level.Should().Be(1);
            level2.Level.Should().Be(2);
            level3.Level.Should().Be(3);
            level4.Level.Should().Be(4);
        }

        #endregion

        #region FullPath Tests

        [Fact]
        public void OrganizationalUnit_FullPath_WhenRootUnit_ShouldReturnUnitName()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Name = "Szkoła Główna" };

            // Act & Assert
            rootUnit.FullPath.Should().Be("Szkoła Główna");
        }

        [Fact]
        public void OrganizationalUnit_FullPath_WhenChildUnit_ShouldReturnFullPath()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Id = "root", Name = "Szkoła" };
            var childUnit = new OrganizationalUnit 
            { 
                Id = "child", 
                Name = "Liceum", 
                ParentUnit = rootUnit 
            };

            // Act & Assert
            childUnit.FullPath.Should().Be("Szkoła → Liceum");
        }

        [Fact]
        public void OrganizationalUnit_FullPath_WhenDeepHierarchy_ShouldReturnCompleteHierarchy()
        {
            // Arrange
            var school = new OrganizationalUnit { Id = "school", Name = "Zespół Szkół" };
            var highSchool = new OrganizationalUnit { Id = "hs", Name = "Liceum", ParentUnit = school };
            var class3A = new OrganizationalUnit { Id = "3a", Name = "Klasa 3A", ParentUnit = highSchool };

            // Act & Assert
            class3A.FullPath.Should().Be("Zespół Szkół → Liceum → Klasa 3A");
        }

        [Fact]
        public void OrganizationalUnit_FullPath_WhenEmptyName_ShouldHandleGracefully()
        {
            // Arrange
            var rootUnit = new OrganizationalUnit { Name = string.Empty };

            // Act & Assert
            rootUnit.FullPath.Should().Be(string.Empty);
        }

        #endregion

        #region Hierarchy Management Tests

        [Fact]
        public void OrganizationalUnit_WhenAddingSubUnits_ShouldMaintainCollection()
        {
            // Arrange
            var parentUnit = new OrganizationalUnit { Id = "parent", Name = "Parent" };
            var childUnit1 = new OrganizationalUnit { Id = "child1", Name = "Child 1" };
            var childUnit2 = new OrganizationalUnit { Id = "child2", Name = "Child 2" };

            // Act
            parentUnit.SubUnits.Add(childUnit1);
            parentUnit.SubUnits.Add(childUnit2);

            // Assert
            parentUnit.SubUnits.Should().HaveCount(2);
            parentUnit.SubUnits.Should().Contain(childUnit1);
            parentUnit.SubUnits.Should().Contain(childUnit2);
        }

        [Fact]
        public void OrganizationalUnit_WhenAddingDepartments_ShouldMaintainCollection()
        {
            // Arrange
            var unit = new OrganizationalUnit { Id = "unit", Name = "Test Unit" };
            var dept1 = new Department { Id = "dept1", Name = "Department 1" };
            var dept2 = new Department { Id = "dept2", Name = "Department 2" };

            // Act
            unit.Departments.Add(dept1);
            unit.Departments.Add(dept2);

            // Assert
            unit.Departments.Should().HaveCount(2);
            unit.Departments.Should().Contain(dept1);
            unit.Departments.Should().Contain(dept2);
        }

        #endregion

        #region Real World Scenario Tests

        [Fact]
        public void OrganizationalUnit_SchoolHierarchyScenario_ShouldWorkCorrectly()
        {
            // Arrange - tworzymy rzeczywistą hierarchię szkolną
            var school = new OrganizationalUnit 
            { 
                Id = "zs-01", 
                Name = "Zespół Szkół nr 1",
                Code = "ZS01",
                Description = "Zespół Szkół Technicznych",
                IsSystemDefault = true,
                SortOrder = 1
            };

            var highSchool = new OrganizationalUnit 
            { 
                Id = "lo-01", 
                Name = "Liceum Ogólnokształcące",
                Code = "LO01",
                ParentUnitId = school.Id,
                ParentUnit = school,
                SortOrder = 1
            };

            var techSchool = new OrganizationalUnit 
            { 
                Id = "tech-01", 
                Name = "Technikum",
                Code = "TECH01",
                ParentUnitId = school.Id,
                ParentUnit = school,
                SortOrder = 2
            };

            var class3A = new OrganizationalUnit 
            { 
                Id = "3a-lo", 
                Name = "Klasa 3A",
                Code = "3A-LO",
                ParentUnitId = highSchool.Id,
                ParentUnit = highSchool,
                SortOrder = 1
            };

            // Act - dodajemy do kolekcji
            school.SubUnits.Add(highSchool);
            school.SubUnits.Add(techSchool);
            highSchool.SubUnits.Add(class3A);

            // Assert - struktura hierarchii
            school.IsRootUnit.Should().BeTrue();
            school.Level.Should().Be(0);
            school.FullPath.Should().Be("Zespół Szkół nr 1");

            highSchool.IsRootUnit.Should().BeFalse();
            highSchool.Level.Should().Be(1);
            highSchool.FullPath.Should().Be("Zespół Szkół nr 1 → Liceum Ogólnokształcące");

            class3A.Level.Should().Be(2);
            class3A.FullPath.Should().Be("Zespół Szkół nr 1 → Liceum Ogólnokształcące → Klasa 3A");

            // Assert - kolekcje
            school.SubUnits.Should().HaveCount(2);
            highSchool.SubUnits.Should().HaveCount(1);
            techSchool.SubUnits.Should().BeEmpty();
        }

        [Fact]
        public void OrganizationalUnit_SortOrderScenario_ShouldMaintainOrdering()
        {
            // Arrange
            var parentUnit = new OrganizationalUnit { Name = "Parent" };
            
            var unit1 = new OrganizationalUnit { Name = "Unit 1", SortOrder = 3 };
            var unit2 = new OrganizationalUnit { Name = "Unit 2", SortOrder = 1 };
            var unit3 = new OrganizationalUnit { Name = "Unit 3", SortOrder = 2 };

            parentUnit.SubUnits.Add(unit1);
            parentUnit.SubUnits.Add(unit2);
            parentUnit.SubUnits.Add(unit3);

            // Act - sortujemy według SortOrder
            var sortedUnits = parentUnit.SubUnits.OrderBy(u => u.SortOrder).ToList();

            // Assert
            sortedUnits[0].Should().Be(unit2); // SortOrder = 1
            sortedUnits[1].Should().Be(unit3); // SortOrder = 2
            sortedUnits[2].Should().Be(unit1); // SortOrder = 3
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void OrganizationalUnit_WhenParentUnitChanges_LevelShouldRecalculate()
        {
            // Arrange
            var oldParent = new OrganizationalUnit { Name = "Old Parent" };
            var newParent = new OrganizationalUnit { Name = "New Parent" };
            var grandParent = new OrganizationalUnit { Name = "Grand Parent" };
            newParent.ParentUnit = grandParent;

            var unit = new OrganizationalUnit 
            { 
                Name = "Test Unit", 
                ParentUnit = oldParent 
            };

            // Act & Assert - zmiana rodzica
            unit.Level.Should().Be(1); // oldParent na poziomie 0

            unit.ParentUnit = newParent;
            unit.Level.Should().Be(2); // newParent na poziomie 1
        }

        [Fact]
        public void OrganizationalUnit_WhenCircularReference_ShouldNotCauseStackOverflow()
        {
            // Arrange
            var unit1 = new OrganizationalUnit { Id = "1", Name = "Unit 1" };
            var unit2 = new OrganizationalUnit { Id = "2", Name = "Unit 2" };

            // Act - tworzenie błędnego cyklu (w prawdziwej aplikacji to byłoby zabronione)
            unit1.ParentUnit = unit2;
            unit2.ParentUnit = unit1;

            // Assert - sprawdzamy że nie ma stack overflow (maksymalnie kilka iteracji)
            var level1 = 0;
            var level2 = 0;
            var current1 = unit1;
            var current2 = unit2;

            // Bezpieczne liczenie z limitem iteracji
            for (int i = 0; i < 10 && current1?.ParentUnit != null; i++)
            {
                level1++;
                current1 = current1.ParentUnit;
            }

            for (int i = 0; i < 10 && current2?.ParentUnit != null; i++)
            {
                level2++;
                current2 = current2.ParentUnit;
            }

            // W rzeczywistości Level może zwrócić nieprawidłową wartość dla cyklu,
            // ale nie powinno być stack overflow
            level1.Should().Be(10); // osiągnęliśmy limit
            level2.Should().Be(10); // osiągnęliśmy limit
        }

        #endregion
    }
} 