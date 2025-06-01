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
    public class TeamTemplateRepositoryTests : RepositoryTestBase
    {
        private readonly TeamTemplateRepository _repository;

        public TeamTemplateRepositoryTests()
        {
            _repository = new TeamTemplateRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddTemplateToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var template = new TeamTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Szablon Klasowy",
                Template = "{SchoolType} {Class} - {Subject}",
                Description = "Szablon dla zespołów klasowych",
                IsDefault = false,
                IsUniversal = true,
                Category = "Klasy",
                Language = "pl-PL",
                MaxLength = 100,
                RemovePolishChars = false,
                Separator = " - ",
                SortOrder = 1,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(template);
            await SaveChangesAsync();

            // Weryfikacja
            var savedTemplate = await Context.TeamTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
            savedTemplate.Should().NotBeNull();
            savedTemplate!.Name.Should().Be("Szablon Klasowy");
            savedTemplate.Template.Should().Be("{SchoolType} {Class} - {Subject}");
            savedTemplate.IsUniversal.Should().BeTrue();
            savedTemplate.Category.Should().Be("Klasy");
            savedTemplate.CreatedBy.Should().Be("test_user_integration_base_default");
            savedTemplate.CreatedDate.Should().NotBe(default(DateTime));
            savedTemplate.ModifiedBy.Should().BeNull();
            savedTemplate.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetDefaultTemplateForSchoolTypeAsync_ShouldReturnCorrectTemplate()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                IsActive = true
            };
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SaveChangesAsync(); // Zapis z audytem dla SchoolType

            var templates = new List<TeamTemplate>
            {
                CreateTemplate("Default LO Template", schoolType.Id, true, false), // domyślny
                CreateTemplate("Alternative LO Template", schoolType.Id, false, false),
                CreateTemplate("Default Other Template", Guid.NewGuid().ToString(), true, false), // inny typ szkoły
                CreateTemplate("Universal Template", null, false, true)
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync(); // Zapis z audytem dla szablonów

            // Działanie
            var result = await _repository.GetDefaultTemplateForSchoolTypeAsync(schoolType.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.Name.Should().Be("Default LO Template");
            result.IsDefault.Should().BeTrue();
            result.SchoolTypeId.Should().Be(schoolType.Id);
            result.SchoolType.Should().NotBeNull();
            result.SchoolType!.ShortName.Should().Be("LO");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetUniversalTemplatesAsync_ShouldReturnOnlyUniversalTemplates()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var schoolTypeId = Guid.NewGuid().ToString();
            // Najpierw dodajmy SchoolType, jeśli jest potrzebny przez CreateTemplate
            var schoolTypeForSpecific = new SchoolType { Id = schoolTypeId, ShortName = "SPEC", FullName = "Specyficzny Typ", IsActive = true };
            await Context.SchoolTypes.AddAsync(schoolTypeForSpecific);
            await Context.SaveChangesAsync();

            var templates = new List<TeamTemplate>
            {
                CreateTemplate("Universal 1", null, false, true),
                CreateTemplate("Universal 2", null, false, true),
                CreateTemplate("School Specific 1", schoolTypeId, false, false),
                CreateTemplate("School Specific 2", schoolTypeId, true, false),
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync(); // Zapis z audytem

            var inactiveTemplate = CreateTemplate("Inactive Universal", null, false, true);
            await Context.TeamTemplates.AddAsync(inactiveTemplate);
            await Context.SaveChangesAsync();

            inactiveTemplate.IsActive = false;
            _repository.Update(inactiveTemplate); // Audyt dla tej zmiany zadziała, gdy wywołamy SaveChangesAsync
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetUniversalTemplatesAsync();

            // Weryfikacja
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.IsUniversal && t.IsActive);
            result.Select(t => t.Name).Should().Contain(new[] { "Universal 1", "Universal 2" });
            result.ToList().ForEach(t => t.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Fact]
        public async Task GetTemplatesBySchoolTypeAsync_ShouldReturnMatchingTemplates()
        {
            // Przygotowanie
            await CleanDatabaseAsync();

            var schoolType1 = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                IsActive = true
            };

            var schoolType2 = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "TECH",
                FullName = "Technikum",
                Description = "Test",
                IsActive = true
            };

            await Context.SchoolTypes.AddRangeAsync(schoolType1, schoolType2);
            await Context.SaveChangesAsync(); // Zapis z audytem

            var templates = new List<TeamTemplate>
            {
                CreateTemplate("LO Template 1", schoolType1.Id, false, false),
                CreateTemplate("LO Template 2", schoolType1.Id, true, false),
                CreateTemplate("TECH Template 1", schoolType2.Id, false, false),
                CreateTemplate("Universal Template", null, false, true),
                CreateTemplate("Inactive LO Template", schoolType1.Id, false, false, true)
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync(); // Zapis z audytem

            var inactiveTemplate = await Context.TeamTemplates.FirstAsync(t => t.Name == "Inactive LO Template");
            inactiveTemplate!.IsActive = false;
            _repository.Update(inactiveTemplate);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetTemplatesBySchoolTypeAsync(schoolType1.Id);

            // Weryfikacja
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.SchoolTypeId == schoolType1.Id && t.IsActive);
            result.Select(t => t.Name).Should().Contain(new[] { "LO Template 1", "LO Template 2" });
            result.All(t => t.SchoolType != null).Should().BeTrue();
            result.ToList().ForEach(t => t.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Theory]
        [InlineData("Klasowy", 2)]
        [InlineData("PROJEKT", 1)]
        [InlineData("test", 3)]
        [InlineData("nieistniejacy", 0)]
        [InlineData("", 5)]
        [InlineData(null, 5)]
        public async Task SearchTemplatesAsync_ShouldReturnMatchingTemplates(string searchTerm, int expectedCount)
        {
            // Przygotowanie
            await CleanDatabaseAsync();

            var templates = new List<TeamTemplate>
            {
                CreateTemplateWithDetails("Szablon Klasowy", "Szablon dla klas", "Klasy"),
                CreateTemplateWithDetails("Szablon Klasowy Rozszerzony", "Rozszerzony szablon", "Klasy"),
                CreateTemplateWithDetails("Szablon Projektowy", "Do projektów", "Projekty"),
                CreateTemplateWithDetails("Inny Szablon", "Test opisu", "Inne"),
                CreateTemplateWithDetails("Szablon Testowy", "Opis zawiera test", "Testy"),
                CreateTemplateWithDetails("Nieaktywny Klasowy", "Nieaktywny", "Klasy", false)
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Działanie
            var result = await _repository.SearchTemplatesAsync(searchTerm);

            // Weryfikacja
            result.Should().HaveCount(expectedCount);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                result.Should().OnlyContain(t => t.IsActive &&
                    (t.Name.ToLower().Contains(lowerSearchTerm) ||
                     (t.Description != null && t.Description.ToLower().Contains(lowerSearchTerm)) ||
                     (t.Category != null && t.Category.ToLower().Contains(lowerSearchTerm))));
            }
            else
            {
                result.Should().OnlyContain(t => t.IsActive);
            }
        }

        [Fact]
        public async Task Update_ShouldModifyTemplateData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var template = new TeamTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Original Name",
                Template = "{Original}",
                Description = "Original Description",
                IsDefault = false,
                IsUniversal = true,
                Category = "Original",
                SortOrder = 1,
                IsActive = true
            };
            await Context.TeamTemplates.AddAsync(template);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = template.CreatedBy;
            var initialCreatedDate = template.CreatedDate;
            var currentUser = "template_updater_user";
            SetTestUser(currentUser);

            // Działanie
            var templateToUpdate = await _repository.GetByIdAsync(template.Id);
            templateToUpdate!.Name = "Updated Name";
            templateToUpdate.Template = "{Updated} - {New}";
            templateToUpdate.Description = "Updated Description";
            templateToUpdate.Category = "Updated";
            templateToUpdate.SortOrder = 10;
            templateToUpdate.UsageCount = 5;
            templateToUpdate.LastUsedDate = DateTime.UtcNow;
            // templateToUpdate.MarkAsModified(currentUser); // Niepotrzebne

            _repository.Update(templateToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy na `currentUser`

            // Weryfikacja
            var updatedTemplate = await Context.TeamTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
            updatedTemplate.Should().NotBeNull();
            updatedTemplate!.Name.Should().Be("Updated Name");
            updatedTemplate.Template.Should().Be("{Updated} - {New}");
            updatedTemplate.Description.Should().Be("Updated Description");
            updatedTemplate.Category.Should().Be("Updated");
            updatedTemplate.SortOrder.Should().Be(10);
            updatedTemplate.UsageCount.Should().Be(5);
            updatedTemplate.LastUsedDate.Should().NotBeNull();
            updatedTemplate.CreatedBy.Should().Be(initialCreatedBy);
            updatedTemplate.CreatedDate.Should().Be(initialCreatedDate);
            updatedTemplate.ModifiedBy.Should().Be(currentUser);
            updatedTemplate.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkTemplateAsInactive()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var template = CreateTemplate("To Delete", null, false, true);
            await Context.TeamTemplates.AddAsync(template);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = template.CreatedBy;
            var initialCreatedDate = template.CreatedDate;
            var currentUser = "template_deleter_user";
            SetTestUser(currentUser);

            // Działanie
            var templateToUpdate = await _repository.GetByIdAsync(template.Id);
            templateToUpdate!.MarkAsDeleted(currentUser); // Ta wartość `deletedBy` zostanie nadpisana
            _repository.Update(templateToUpdate);
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy

            // Weryfikacja
            var deletedTemplate = await Context.TeamTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == template.Id);
            deletedTemplate.Should().NotBeNull();
            deletedTemplate!.IsActive.Should().BeFalse();
            deletedTemplate.CreatedBy.Should().Be(initialCreatedBy);
            deletedTemplate.CreatedDate.Should().Be(initialCreatedDate);
            deletedTemplate.ModifiedBy.Should().Be(currentUser);
            deletedTemplate.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        #region Helper Methods

        // Zmodyfikowane metody pomocnicze - usunięto parametr createdBy
        private TeamTemplate CreateTemplate(string name, string? schoolTypeId, bool isDefault, bool isUniversal, bool isActive = true)
        {
            return new TeamTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Template = "{SchoolType} {Class}",
                Description = $"Description for {name}",
                IsDefault = isDefault,
                IsUniversal = isUniversal,
                SchoolTypeId = schoolTypeId,
                Category = "Test",
                Language = "pl-PL",
                SortOrder = 1,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        private TeamTemplate CreateTemplateWithDetails(string name, string description, string category, bool isActive = true)
        {
            return new TeamTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Template = "{SchoolType} {Class}",
                Description = description,
                IsDefault = false,
                IsUniversal = true, // Zakładam, że jeśli nie ma SchoolTypeId, to jest uniwersalny
                Category = category,
                Language = "pl-PL",
                SortOrder = 1,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        #endregion
    }
}