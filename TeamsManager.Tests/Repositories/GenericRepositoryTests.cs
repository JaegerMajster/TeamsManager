using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    /// <summary>
    /// Testy jednostkowe dla GenericRepository
    /// Testuje podstawowe operacje CRUD dla repozytorium generycznego
    /// </summary>
    public class GenericRepositoryTests : RepositoryTestBase
    {
        private GenericRepository<Department> _repository;

        public GenericRepositoryTests()
        {
            _repository = new GenericRepository<Department>(Context);
        }

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_WithEmptyDatabase_ShouldReturnEmptyCollection()
        {
            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllAsync_WithMultipleEntities_ShouldReturnAllEntities()
        {
            // Arrange
            var departments = CreateTestDepartments(3);
            await Context.Departments.AddRangeAsync(departments);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(3);
            result.Select(d => d.Name).Should().Contain(departments.Select(d => d.Name));
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_WithValidId_ShouldReturnEntity()
        {
            // Arrange
            var department = CreateTestDepartment("Test Department");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(department.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(department.Id);
            result.Name.Should().Be(department.Name);
        }

        [Fact]
        public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByIdAsync("non-existent-id");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_WithNullId_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(null!);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region AddAsync Tests

        [Fact]
        public async Task AddAsync_WithValidEntity_ShouldAddToContext()
        {
            // Arrange
            var department = CreateTestDepartment("New Department");

            // Act
            await _repository.AddAsync(department);
            await Context.SaveChangesAsync();

            // Assert
            var savedDepartment = await Context.Departments.FindAsync(department.Id);
            savedDepartment.Should().NotBeNull();
            savedDepartment!.Name.Should().Be("New Department");
        }

        [Fact]
        public async Task AddAsync_WithNullEntity_ShouldThrowException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _repository.AddAsync(null!));
        }

        #endregion

        #region AddRangeAsync Tests

        [Fact]
        public async Task AddRangeAsync_WithMultipleEntities_ShouldAddAllToContext()
        {
            // Arrange
            var departments = CreateTestDepartments(3);

            // Act
            await _repository.AddRangeAsync(departments);
            await Context.SaveChangesAsync();

            // Assert
            var savedDepartments = await Context.Departments.ToListAsync();
            savedDepartments.Should().HaveCount(3);
            savedDepartments.Select(d => d.Name).Should().Contain(departments.Select(d => d.Name));
        }

        [Fact]
        public async Task AddRangeAsync_WithEmptyCollection_ShouldNotThrow()
        {
            // Arrange
            var emptyList = new List<Department>();

            // Act & Assert
            await _repository.AddRangeAsync(emptyList);
            // Should not throw
        }

        #endregion

        #region Update Tests

        [Fact]
        public async Task Update_WithExistingEntity_ShouldUpdateEntity()
        {
            // Arrange
            var department = CreateTestDepartment("Original Name");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Detach to simulate getting entity from another context
            Context.Entry(department).State = EntityState.Detached;

            // Modify the entity
            department.Name = "Updated Name";
            department.Description = "Updated Description";

            // Act
            _repository.Update(department);
            await Context.SaveChangesAsync();

            // Assert
            var updatedDepartment = await Context.Departments.FindAsync(department.Id);
            updatedDepartment.Should().NotBeNull();
            updatedDepartment!.Name.Should().Be("Updated Name");
            updatedDepartment.Description.Should().Be("Updated Description");
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task Delete_WithExistingEntity_ShouldRemoveEntity()
        {
            // Arrange
            var department = CreateTestDepartment("To Delete");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            _repository.Delete(department);
            await Context.SaveChangesAsync();

            // Assert
            var deletedDepartment = await Context.Departments.FindAsync(department.Id);
            deletedDepartment.Should().BeNull();
        }

        [Fact]
        public async Task Delete_WithDetachedEntity_ShouldRemoveEntity()
        {
            // Arrange
            var department = CreateTestDepartment("To Delete");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Detach entity
            Context.Entry(department).State = EntityState.Detached;

            // Act
            _repository.Delete(department);
            await Context.SaveChangesAsync();

            // Assert
            var deletedDepartment = await Context.Departments.FindAsync(department.Id);
            deletedDepartment.Should().BeNull();
        }

        #endregion

        #region DeleteByIdAsync Tests

        [Fact]
        public async Task DeleteByIdAsync_WithExistingId_ShouldRemoveEntity()
        {
            // Arrange
            var department = CreateTestDepartment("To Delete");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            await _repository.DeleteByIdAsync(department.Id);
            await Context.SaveChangesAsync();

            // Assert
            var deletedDepartment = await Context.Departments.FindAsync(department.Id);
            deletedDepartment.Should().BeNull();
        }

        [Fact]
        public async Task DeleteByIdAsync_WithNonExistentId_ShouldNotThrow()
        {
            // Act & Assert
            await _repository.DeleteByIdAsync("non-existent-id");
            // Should not throw
        }

        #endregion

        #region DeleteRange Tests

        [Fact]
        public async Task DeleteRange_WithMultipleEntities_ShouldRemoveAllEntities()
        {
            // Arrange
            var departments = CreateTestDepartments(3);
            await Context.Departments.AddRangeAsync(departments);
            await Context.SaveChangesAsync();

            // Act
            _repository.DeleteRange(departments);
            await Context.SaveChangesAsync();

            // Assert
            var remainingDepartments = await Context.Departments.ToListAsync();
            remainingDepartments.Should().BeEmpty();
        }

        #endregion

        #region FindAsync Tests

        [Fact]
        public async Task FindAsync_WithMatchingPredicate_ShouldReturnMatchingEntities()
        {
            // Arrange
            var departments = new[]
            {
                CreateTestDepartment("IT Department"),
                CreateTestDepartment("HR Department"),
                CreateTestDepartment("IT Support")
            };
            await Context.Departments.AddRangeAsync(departments);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.FindAsync(d => d.Name.Contains("IT"));

            // Assert
            result.Should().HaveCount(2);
            result.Select(d => d.Name).Should().Contain(new[] { "IT Department", "IT Support" });
        }

        [Fact]
        public async Task FindAsync_WithNoMatches_ShouldReturnEmptyCollection()
        {
            // Arrange
            var department = CreateTestDepartment("Test Department");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.FindAsync(d => d.Name.Contains("NonExistent"));

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WithMatchingPredicate_ShouldReturnTrue()
        {
            // Arrange
            var department = CreateTestDepartment("Test Department");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.ExistsAsync(d => d.Name == "Test Department");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WithNoMatches_ShouldReturnFalse()
        {
            // Arrange
            var department = CreateTestDepartment("Test Department");
            await Context.Departments.AddAsync(department);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.ExistsAsync(d => d.Name == "NonExistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ExistsAsync_WithEmptyDatabase_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.ExistsAsync(d => d.Name == "Any Name");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region SaveChangesAsync Tests

        [Fact]
        public async Task SaveChangesAsync_WithChanges_ShouldReturnNumberOfChanges()
        {
            // Arrange
            var departments = CreateTestDepartments(2);
            await _repository.AddRangeAsync(departments);

            // Act
            var result = await _repository.SaveChangesAsync();

            // Assert
            result.Should().Be(2);
        }

        [Fact]
        public async Task SaveChangesAsync_WithoutChanges_ShouldReturnZero()
        {
            // Act
            var result = await _repository.SaveChangesAsync();

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region Helper Methods

        private Department CreateTestDepartment(string name)
        {
            return new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = $"Description for {name}",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        private List<Department> CreateTestDepartments(int count)
        {
            var departments = new List<Department>();
            for (int i = 1; i <= count; i++)
            {
                departments.Add(CreateTestDepartment($"Department {i}"));
            }
            return departments;
        }

        #endregion
    }
} 