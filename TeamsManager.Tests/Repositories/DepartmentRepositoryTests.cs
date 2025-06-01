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
            // Przygotowanie
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(department);
            await SaveChangesAsync();

            // Weryfikacja
            var savedDept = await Context.Departments.FirstOrDefaultAsync(d => d.Id == department.Id);
            savedDept.Should().NotBeNull();
            savedDept!.Name.Should().Be("Departament Informatyki");
            savedDept.DepartmentCode.Should().Be("IT");
            savedDept.Location.Should().Be("Budynek A, piętro 2");
            savedDept.CreatedBy.Should().Be("test_user_integration_base_default");
            savedDept.CreatedDate.Should().NotBe(default(DateTime));
            savedDept.ModifiedBy.Should().BeNull();
            savedDept.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectDepartment()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = CreateDepartment("MATH", "Departament Matematyki");
            // CreatedBy w CreateDepartment jest "test_user", SaveChangesAsync to potwierdzi
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetByIdAsync(department.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DepartmentCode.Should().Be("MATH");
            result.Name.Should().Be("Departament Matematyki");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllDepartments()
        {
            // Przygotowanie
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

            // Działanie
            var result = await _repository.GetAllAsync();

            // Weryfikacja
            result.Should().HaveCount(4);
            result.Select(d => d.DepartmentCode).Should().Contain(new[] { "IT", "MATH", "PHYS", "LANG" });
            result.ToList().ForEach(d => d.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredDepartments()
        {
            // Przygotowanie
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

            // Działanie - znajdź departamenty w Budynku A
            var resultBuildingA = await _repository.FindAsync(d => d.Location == "Budynek A" && d.IsActive);

            // Weryfikacja
            resultBuildingA.Should().HaveCount(2);
            resultBuildingA.Select(d => d.DepartmentCode).Should().Contain(new[] { "IT", "MATH" });

            // Działanie - znajdź wszystkie aktywne departamenty
            var resultActive = await _repository.FindAsync(d => d.IsActive);

            // Weryfikacja
            resultActive.Should().HaveCount(4);
        }

        [Fact]
        public async Task Update_ShouldModifyDepartmentData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = CreateDepartment("MATH", "Mathematics", "Original Desc");
            await _repository.AddAsync(department); // CreatedBy zostanie ustawione przez TestDbContext podczas SaveChangesAsync
            await SaveChangesAsync();

            var initialCreatedBy = department.CreatedBy;
            var initialCreatedDate = department.CreatedDate;
            var currentUser = "department_updater"; // Definiujemy użytkownika modyfikującego
            SetTestUser(currentUser); // Ustawiamy go jako bieżącego użytkownika testowego

            // Działanie
            // Pobieramy encję ponownie, aby upewnić się, że działamy na śledzonej przez kontekst encji
            var departmentToUpdate = await _repository.GetByIdAsync(department.Id);
            departmentToUpdate!.Name = "Updated Mathematics";
            departmentToUpdate.Description = "Updated description";
            // departmentToUpdate.MarkAsModified(currentUser); // Już niepotrzebne, TestDbContext to obsłuży
            _repository.Update(departmentToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy na `currentUser`

            // Weryfikacja
            var updatedDept = await _repository.GetByIdAsync(department.Id);
            updatedDept.Should().NotBeNull();
            updatedDept!.Name.Should().Be("Updated Mathematics");
            updatedDept.Description.Should().Be("Updated description");
            updatedDept.CreatedBy.Should().Be(initialCreatedBy);
            updatedDept.CreatedDate.Should().Be(initialCreatedDate);
            updatedDept.ModifiedBy.Should().Be(currentUser); // Oczekujemy użytkownika ustawionego przez SetTestUser
            updatedDept.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            ResetTestUser(); // Przywracamy domyślnego użytkownika testowego
        }

        [Fact]
        public async Task Delete_ShouldRemoveDepartmentFromDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = CreateDepartment("TEMP", "Tymczasowy");
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Działanie
            _repository.Delete(department); // Fizyczne usunięcie
            await SaveChangesAsync();

            // Weryfikacja
            var deletedDept = await Context.Departments.FirstOrDefaultAsync(d => d.Id == department.Id);
            deletedDept.Should().BeNull();
        }

        [Fact]
        public async Task ExistsAsync_ShouldReturnCorrectResult()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var department = CreateDepartment("BIO", "Biologia");
            await Context.Departments.AddAsync(department);
            await SaveChangesAsync();

            // Działanie
            var exists = await _repository.ExistsAsync(d => d.Id == department.Id);
            var notExists = await _repository.ExistsAsync(d => d.Id == Guid.NewGuid().ToString());

            // Weryfikacja
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
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        #endregion
    }
}