using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Validation;
using Xunit;

namespace TeamsManager.Tests.Validation
{
    public class OrganizationalUnitValidatorTests
    {
        private readonly Mock<IOrganizationalUnitService> _mockOrganizationalUnitService;
        private readonly Mock<ILogger<OrganizationalUnitValidator>> _mockLogger;
        private readonly OrganizationalUnitValidator _validator;

        public OrganizationalUnitValidatorTests()
        {
            _mockOrganizationalUnitService = new Mock<IOrganizationalUnitService>();
            _mockLogger = new Mock<ILogger<OrganizationalUnitValidator>>();
            _validator = new OrganizationalUnitValidator(_mockOrganizationalUnitService.Object, _mockLogger.Object);
        }

        private OrganizationalUnit CreateTestOrganizationalUnit(string id, string name, string? parentId = null, bool isActive = true)
        {
            return new OrganizationalUnit
            {
                Id = id,
                Name = name,
                ParentUnitId = parentId,
                IsActive = isActive,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@example.com",
                SortOrder = 0
            };
        }

        #region Basic Validation Tests

        [Fact]
        public async Task ValidateForCreation_ValidUnit_ShouldReturnSuccess()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("", "Valid Unit Name");
            _mockOrganizationalUnitService.Setup(s => s.IsNameUniqueAsync(unit.Name, unit.ParentUnitId, null))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.ValidateForCreateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task ValidateForCreation_EmptyOrNullName_ShouldReturnError(string? name)
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("", name!);

            // Act
            var result = await _validator.ValidateForCreateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Nazwa jednostki organizacyjnej jest wymagana"));
        }

        [Fact]
        public async Task ValidateForCreation_NameTooLong_ShouldReturnError()
        {
            // Arrange
            var longName = new string('A', 101); // Przekroczenie limitu 100 znaków
            var unit = CreateTestOrganizationalUnit("", longName);

            // Act
            var result = await _validator.ValidateForCreateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Nazwa jednostki organizacyjnej nie może przekraczać 100 znaków"));
        }

        [Fact]
        public async Task ValidateForCreation_DuplicateName_ShouldReturnError()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("", "Duplicate Name");
            _mockOrganizationalUnitService.Setup(s => s.IsNameUniqueAsync(unit.Name, unit.ParentUnitId, null))
                .ReturnsAsync(false);

            // Act
            var result = await _validator.ValidateForCreateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("już istnieje na poziomie głównym"));
        }

        [Fact]
        public async Task ValidateForCreation_DescriptionTooLong_ShouldReturnError()
        {
            // Arrange
            var longDescription = new string('A', 501); // Przekroczenie limitu 500 znaków
            var unit = CreateTestOrganizationalUnit("", "Valid Name");
            unit.Description = longDescription;
            _mockOrganizationalUnitService.Setup(s => s.IsNameUniqueAsync(unit.Name, unit.ParentUnitId, null))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.ValidateForCreateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Opis jednostki organizacyjnej nie może przekraczać 500 znaków"));
        }

        #endregion

        #region Update Validation Tests

        [Fact]
        public async Task ValidateForUpdate_ValidUnit_ShouldReturnSuccess()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("unit-1", "Updated Name");
            var existingUnit = CreateTestOrganizationalUnit("unit-1", "Old Name");
            
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(unit.Id, false, false, false))
                .ReturnsAsync(existingUnit);
            _mockOrganizationalUnitService.Setup(s => s.IsNameUniqueAsync(unit.Name, unit.ParentUnitId, unit.Id))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.ValidateForUpdateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateForUpdate_NonExistentUnit_ShouldReturnError()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit("non-existent", "Some Name");
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(unit.Id, false, false, false))
                .ReturnsAsync((OrganizationalUnit?)null);

            // Act
            var result = await _validator.ValidateForUpdateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna o podanym ID nie istnieje"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ValidateForUpdate_EmptyOrNullId_ShouldReturnError(string? unitId)
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit(unitId!, "Some Name");

            // Act
            var result = await _validator.ValidateForUpdateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("ID jednostki organizacyjnej jest wymagane przy aktualizacji"));
        }

        [Fact]
        public async Task ValidateForUpdate_WhitespaceId_ShouldReturnError()
        {
            // Arrange
            var unit = CreateTestOrganizationalUnit(" ", "Some Name");

            // Act
            var result = await _validator.ValidateForUpdateAsync(unit);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna o podanym ID nie istnieje"));
        }

        #endregion

        #region Deletion Validation Tests

        [Fact]
        public async Task ValidateForDeletion_UnitWithoutDependencies_ShouldReturnSuccess()
        {
            // Arrange
            var unitId = "unit-to-delete";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit to Delete");
            
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(unitId, false, false, false))
                .ReturnsAsync(unit);
            _mockOrganizationalUnitService.Setup(s => s.CanDeleteOrganizationalUnitAsync(unitId))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.ValidateForDeleteAsync(unitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateForDeletion_UnitWithDependencies_ShouldReturnError()
        {
            // Arrange
            var unitId = "unit-with-deps";
            var unit = CreateTestOrganizationalUnit(unitId, "Unit with Dependencies");
            
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(unitId, false, false, false))
                .ReturnsAsync(unit);
            _mockOrganizationalUnitService.Setup(s => s.CanDeleteOrganizationalUnitAsync(unitId))
                .ReturnsAsync(false);
            
            // Mock'owanie podjednostek i działów
            var subUnits = new List<OrganizationalUnit> { CreateTestOrganizationalUnit("sub1", "Sub Unit 1", unitId) };
            var departments = new List<Department> { new Department { Id = "dept1", Name = "Department 1" } };
            
            _mockOrganizationalUnitService.Setup(s => s.GetSubUnitsAsync(unitId, false))
                .ReturnsAsync(subUnits);
            _mockOrganizationalUnitService.Setup(s => s.GetDepartmentsByOrganizationalUnitAsync(unitId, false))
                .ReturnsAsync(departments);

            // Act
            var result = await _validator.ValidateForDeleteAsync(unitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("zawiera") && e.Contains("podjednostek"));
        }

        [Fact]
        public async Task ValidateForDeletion_NonExistentUnit_ShouldReturnError()
        {
            // Arrange
            var unitId = "non-existent";
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(unitId, false, false, false))
                .ReturnsAsync((OrganizationalUnit?)null);

            // Act
            var result = await _validator.ValidateForDeleteAsync(unitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna o podanym ID nie istnieje"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task ValidateForDeletion_EmptyOrNullId_ShouldReturnError(string? unitId)
        {
            // Act
            var result = await _validator.ValidateForDeleteAsync(unitId!);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("ID jednostki organizacyjnej jest wymagane"));
        }

        [Fact]
        public async Task ValidateForDeletion_WhitespaceId_ShouldReturnError()
        {
            // Arrange
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(" ", false, false, false))
                .ReturnsAsync((OrganizationalUnit?)null);

            // Act
            var result = await _validator.ValidateForDeleteAsync(" ");

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna o podanym ID nie istnieje"));
        }

        #endregion

        #region Move Departments Validation Tests

        [Fact]
        public async Task ValidateMoveDepartments_ValidMove_ShouldReturnSuccess()
        {
            // Arrange
            var departmentIds = new[] { "dept1", "dept2" };
            var targetUnitId = "target-unit";
            var targetUnit = CreateTestOrganizationalUnit(targetUnitId, "Target Unit");
            
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(targetUnitId, false, false, false))
                .ReturnsAsync(targetUnit);

            // Act
            var result = await _validator.ValidateMoveDepartmentsAsync(departmentIds, targetUnitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateMoveDepartments_EmptyDepartmentList_ShouldReturnError()
        {
            // Arrange
            var departmentIds = new string[0];
            var targetUnitId = "target-unit";

            // Act
            var result = await _validator.ValidateMoveDepartmentsAsync(departmentIds, targetUnitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Lista działów do przeniesienia nie może być pusta"));
        }

        [Fact]
        public async Task ValidateMoveDepartments_NonExistentTargetUnit_ShouldReturnError()
        {
            // Arrange
            var departmentIds = new[] { "dept1" };
            var targetUnitId = "non-existent";
            
            _mockOrganizationalUnitService.Setup(s => s.GetOrganizationalUnitByIdAsync(targetUnitId, false, false, false))
                .ReturnsAsync((OrganizationalUnit?)null);

            // Act
            var result = await _validator.ValidateMoveDepartmentsAsync(departmentIds, targetUnitId);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Docelowa jednostka organizacyjna nie istnieje"));
        }

        #endregion

        #region Null Validation Tests

        [Fact]
        public async Task ValidateForCreation_NullUnit_ShouldReturnError()
        {
            // Act
            var result = await _validator.ValidateForCreateAsync(null!);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna nie może być null"));
        }

        [Fact]
        public async Task ValidateForUpdate_NullUnit_ShouldReturnError()
        {
            // Act
            var result = await _validator.ValidateForUpdateAsync(null!);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Jednostka organizacyjna nie może być null"));
        }

        // Usunięto test ValidateHierarchy_NullUnits_ShouldThrowArgumentNullException
        // ponieważ metoda ValidateHierarchyAsync nie istnieje w walidatorze

        #endregion
    }
}
