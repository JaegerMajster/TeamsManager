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
    public class SchoolYearRepositoryTests : RepositoryTestBase
    {
        private readonly SchoolYearRepository _repository;

        public SchoolYearRepositoryTests()
        {
            _repository = new SchoolYearRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSchoolYearToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYear = new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsCurrent = true,
                Description = "Rok szkolny 2024/2025",
                FirstSemesterStart = new DateTime(2024, 9, 1),
                FirstSemesterEnd = new DateTime(2025, 1, 31),
                SecondSemesterStart = new DateTime(2025, 2, 1),
                SecondSemesterEnd = new DateTime(2025, 6, 30),
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(schoolYear);
            await SaveChangesAsync();

            // Weryfikacja
            var savedSchoolYear = await Context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYear.Id);
            savedSchoolYear.Should().NotBeNull();
            savedSchoolYear!.Name.Should().Be("2024/2025");
            savedSchoolYear.StartDate.Should().Be(new DateTime(2024, 9, 1));
            savedSchoolYear.EndDate.Should().Be(new DateTime(2025, 6, 30));
            savedSchoolYear.IsCurrent.Should().BeTrue();
            savedSchoolYear.CreatedBy.Should().Be("test_user_integration_base_default");
            savedSchoolYear.CreatedDate.Should().NotBe(default(DateTime));
            savedSchoolYear.ModifiedBy.Should().BeNull();
            savedSchoolYear.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSchoolYear()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYear = new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = "2023/2024",
                StartDate = new DateTime(2023, 9, 1),
                EndDate = new DateTime(2024, 6, 30),
                IsCurrent = false,
                Description = "Poprzedni rok szkolny",
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.SchoolYears.AddAsync(schoolYear);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetByIdAsync(schoolYear.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Name.Should().Be("2023/2024");
            result.IsCurrent.Should().BeFalse();
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_ShouldReturnCurrentYear()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYears = new List<SchoolYear>
            {
                CreateSchoolYear("2022/2023", new DateTime(2022, 9, 1), new DateTime(2023, 6, 30), false, true),
                CreateSchoolYear("2023/2024", new DateTime(2023, 9, 1), new DateTime(2024, 6, 30), false, true),
                CreateSchoolYear("2024/2025", new DateTime(2024, 9, 1), new DateTime(2025, 6, 30), true, true), // bieżący
                CreateSchoolYear("2025/2026", new DateTime(2025, 9, 1), new DateTime(2026, 6, 30), false, true),
                CreateSchoolYear("2026/2027", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30), true, false), // bieżący ale nieaktywny
            };

            await Context.SchoolYears.AddRangeAsync(schoolYears);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetCurrentSchoolYearAsync();

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Name.Should().Be("2024/2025");
            result.IsCurrent.Should().BeTrue();
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_ShouldReturnNull_WhenNoCurrentYear()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYears = new List<SchoolYear>
            {
                CreateSchoolYear("2022/2023", new DateTime(2022, 9, 1), new DateTime(2023, 6, 30), false, true),
                CreateSchoolYear("2023/2024", new DateTime(2023, 9, 1), new DateTime(2024, 6, 30), false, true),
            };

            await Context.SchoolYears.AddRangeAsync(schoolYears);
            await Context.SaveChangesAsync();

            // Działanie
            var result = await _repository.GetCurrentSchoolYearAsync();

            // Weryfikacja
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSchoolYearByNameAsync_ShouldReturnCorrectYear()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var targetName = "2024/2025";
            var schoolYears = new List<SchoolYear>
            {
                CreateSchoolYear("2023/2024", new DateTime(2023, 9, 1), new DateTime(2024, 6, 30), false, true),
                CreateSchoolYear(targetName, new DateTime(2024, 9, 1), new DateTime(2025, 6, 30), true, true),
                CreateSchoolYear("2025/2026", new DateTime(2025, 9, 1), new DateTime(2026, 6, 30), false, true),
                CreateSchoolYear(targetName, new DateTime(2024, 9, 1), new DateTime(2025, 6, 30), false, false), // ta sama nazwa ale nieaktywny
            };

            await Context.SchoolYears.AddRangeAsync(schoolYears);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetSchoolYearByNameAsync(targetName);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Name.Should().Be(targetName);
            result.IsActive.Should().BeTrue();
            result.IsCurrent.Should().BeTrue();
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Theory]
        [InlineData("2024-10-15", 1, "2024/2025")]
        [InlineData("2024-09-01", 1, "2024/2025")]
        [InlineData("2025-06-30", 1, "2024/2025")]
        [InlineData("2024-08-31", 0, null)]
        [InlineData("2025-07-01", 0, null)]
        [InlineData("2024-01-15", 2, null)]
        public async Task GetSchoolYearsActiveOnDateAsync_ShouldReturnCorrectYears(string dateString, int expectedCount, string? expectedYearName)
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var testDate = DateTime.Parse(dateString);

            var schoolYears = new List<SchoolYear>
            {
                CreateSchoolYear("2023/2024", new DateTime(2023, 9, 1), new DateTime(2024, 6, 30), false, true),
                CreateSchoolYear("2024/2025", new DateTime(2024, 9, 1), new DateTime(2025, 6, 30), true, true),
                CreateSchoolYear("2025/2026", new DateTime(2025, 9, 1), new DateTime(2026, 6, 30), false, true),
                CreateSchoolYear("2023/2024 zimowy", new DateTime(2023, 10, 1), new DateTime(2024, 2, 28), false, true),
                CreateSchoolYear("2024/2025", new DateTime(2024, 9, 1), new DateTime(2025, 6, 30), false, false), // nieaktywny
            };

            await Context.SchoolYears.AddRangeAsync(schoolYears);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.GetSchoolYearsActiveOnDateAsync(testDate);

            // Weryfikacja
            result.Should().HaveCount(expectedCount);
            if (expectedCount > 0 && expectedYearName != null)
            {
                result.Should().Contain(sy => sy.Name == expectedYearName);
            }
            result.Should().OnlyContain(sy => sy.IsActive && sy.StartDate.Date <= testDate.Date && sy.EndDate.Date >= testDate.Date);
        }

        [Fact]
        public async Task Update_ShouldModifySchoolYearData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYear = new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsCurrent = false,
                Description = "Original description",
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.SchoolYears.AddAsync(schoolYear);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = schoolYear.CreatedBy;
            var initialCreatedDate = schoolYear.CreatedDate;
            var currentUser = "year_updater";
            SetTestUser(currentUser);

            // Działanie
            var schoolYearToUpdate = await _repository.GetByIdAsync(schoolYear.Id);
            schoolYearToUpdate!.IsCurrent = true;
            schoolYearToUpdate.Description = "Updated description - now current year";
            schoolYearToUpdate.FirstSemesterStart = new DateTime(2024, 9, 1);
            schoolYearToUpdate.FirstSemesterEnd = new DateTime(2025, 1, 31);
            schoolYearToUpdate.SecondSemesterStart = new DateTime(2025, 2, 1);
            schoolYearToUpdate.SecondSemesterEnd = new DateTime(2025, 6, 30);
            // schoolYearToUpdate.MarkAsModified(currentUser); // Niepotrzebne, TestDbContext to obsłuży

            _repository.Update(schoolYearToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy na `currentUser`

            // Weryfikacja
            var updatedYear = await Context.SchoolYears.FirstOrDefaultAsync(sy => sy.Id == schoolYear.Id);
            updatedYear.Should().NotBeNull();
            updatedYear!.IsCurrent.Should().BeTrue();
            updatedYear.Description.Should().Be("Updated description - now current year");
            updatedYear.FirstSemesterStart.Should().Be(new DateTime(2024, 9, 1));
            updatedYear.FirstSemesterEnd.Should().Be(new DateTime(2025, 1, 31));
            updatedYear.SecondSemesterStart.Should().Be(new DateTime(2025, 2, 1));
            updatedYear.SecondSemesterEnd.Should().Be(new DateTime(2025, 6, 30));
            updatedYear.CreatedBy.Should().Be(initialCreatedBy);
            updatedYear.CreatedDate.Should().Be(initialCreatedDate);
            updatedYear.ModifiedBy.Should().Be(currentUser);
            updatedYear.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkSchoolYearAsInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolYear = CreateSchoolYear("2023/2024", new DateTime(2023, 9, 1), new DateTime(2024, 6, 30), false, true);
            await Context.SchoolYears.AddAsync(schoolYear);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = schoolYear.CreatedBy;
            var initialCreatedDate = schoolYear.CreatedDate;
            var currentUser = "year_deleter";
            SetTestUser(currentUser);

            // Działanie
            var schoolYearToUpdate = await _repository.GetByIdAsync(schoolYear.Id);
            schoolYearToUpdate!.MarkAsDeleted(currentUser); // Ta wartość `deletedBy` zostanie nadpisana przez TestDbContext
            _repository.Update(schoolYearToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy

            // Weryfikacja
            var deletedYear = await Context.SchoolYears.AsNoTracking().FirstOrDefaultAsync(sy => sy.Id == schoolYear.Id);
            deletedYear.Should().NotBeNull();
            deletedYear!.IsActive.Should().BeFalse();
            deletedYear.CreatedBy.Should().Be(initialCreatedBy);
            deletedYear.CreatedDate.Should().Be(initialCreatedDate);
            deletedYear.ModifiedBy.Should().Be(currentUser);
            deletedYear.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        #region Helper Methods

        // Zmodyfikowana metoda pomocnicza - usunięto parametr createdBy
        private SchoolYear CreateSchoolYear(string name, DateTime startDate, DateTime endDate, bool isCurrent, bool isActive)
        {
            return new SchoolYear
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                StartDate = startDate,
                EndDate = endDate,
                IsCurrent = isCurrent,
                Description = $"Rok szkolny {name}",
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        #endregion
    }
}