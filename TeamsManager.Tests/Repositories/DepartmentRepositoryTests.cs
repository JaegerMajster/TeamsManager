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
    [Collection("Sequential")]
        public class DepartmentRepositoryTests : RepositoryTestBase
    {
        private readonly GenericRepository<Department> _repository;

        public DepartmentRepositoryTests()
        {
            _repository = new GenericRepository<Department>(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddDepartmentToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = new Department
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Departament Informatyki",
                Description = "Departament odpowiedzialny za nauczanie informatyki",
                DepartmentCode = "IT",
                Location = "Budynek A, piętro 2",
                Phone = "123-456-789",
                Email = "it.dept@school.com",
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(department);
            await SaveChangesAsync();

            // Assert
            var savedDept = await Context.Departments.FirstOrDefaultAsync(d => d.Id == department.Id);
            savedDept.Should().NotBeNull();
            savedDept!.Name.Should().Be("Departament Informatyki");
            savedDept.DepartmentCode.Should().Be("IT");
            savedDept.Location.Should().Be("Budynek A, piętro 2");
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectDepartment()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = CreateDepartment("MATH", "Departament Matematyki");
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(department.Id);

            // Assert
            result.Should().NotBeNull();
            result!.DepartmentCode.Should().Be("MATH");
            result.Name.Should().Be("Departament Matematyki");
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllDepartments()
        {
            // Arrange
            await CleanDatabaseAsync();
            var departments = new List<Department>
            {
                CreateDepartment("IT", "Informatyka"),
                CreateDepartment("MATH", "Matematyka"),
                CreateDepartment("PHYS", "Fizyka"),
                CreateDepartment("LANG", "Języki Obce")
            };
            await Context.Departments.AddRangeAsync(departments);
            await SaveChangesAsync();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(4);
            result.Select(d => d.DepartmentCode).Should().Contain(new[] { "IT", "MATH", "PHYS", "LANG" });
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredDepartments()
        {
            // Arrange
            await CleanDatabaseAsync();
            var departments = new List<Department>
            {
                CreateDepartment("IT", "Informatyka", "Budynek A"),
                CreateDepartment("MATH", "Matematyka", "Budynek A"),
                CreateDepartment("PHYS", "Fizyka", "Budynek B"),
                CreateDepartment("CHEM", "Chemia", "Budynek B"),
                CreateDepartment("HIST", "Historia", "Budynek C", false) // nieaktywny
            };
            await Context.Departments.AddRangeAsync(departments);
            await SaveChangesAsync();

            // Act - znajdź departamenty w Budynku A
            var resultBuildingA = await _repository.FindAsync(d => d.Location == "Budynek A" && d.IsActive);

            // Assert
            resultBuildingA.Should().HaveCount(2);
            resultBuildingA.Select(d => d.DepartmentCode).Should().Contain(new[] { "IT", "MATH" });

            // Act - znajdź wszystkie aktywne departamenty
            var resultActive = await _repository.FindAsync(d => d.IsActive);

            // Assert
            resultActive.Should().HaveCount(4);
        }

        [Fact]
        public async Task Update_ShouldModifyDepartmentData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = CreateDepartment("MATH", "Mathematics", "Original Desc");
            await _repository.AddAsync(department);
            await SaveChangesAsync();

            // Act
            department.Name = "Updated Mathematics";
            department.Description = "Updated description";
            _repository.Update(department);
            await SaveChangesAsync();

            // Assert
            var updatedDept = await _repository.GetByIdAsync(department.Id);
            updatedDept.Should().NotBeNull();
            updatedDept.Name.Should().Be("Updated Mathematics");
            updatedDept.Description.Should().Be("Updated description");
            updatedDept.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedDept.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Delete_ShouldRemoveDepartmentFromDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = CreateDepartment("TEMP", "Tymczasowy");
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Act
            _repository.Delete(department);
            await SaveChangesAsync();

            // Assert
            var deletedDept = await Context.Departments.FirstOrDefaultAsync(d => d.Id == department.Id);
            deletedDept.Should().BeNull();
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnCorrectResult()
        {
            // Arrange
            await CleanDatabaseAsync();
            var department = CreateDepartment("BIO", "Biologia");
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Act
            var exists = await _repository.ExistsAsync(d => d.Id == department.Id);
            var notExists = await _repository.ExistsAsync(d => d.Id == Guid.NewGuid().ToString());

            // Assert
            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }

        #region Helper Methods

        private Department CreateDepartment(string code, string name, string location = "Budynek Główny", bool isActive = true)
        {
            return new Department
            {
                Id = Guid.NewGuid().ToString(),
                DepartmentCode = code,
                Name = name,
                Description = $"Opis departamentu {name}",
                Location = location,
                Phone = "123-456-000",
                Email = $"{code.ToLower()}@school.com",
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        #endregion
    }
} 