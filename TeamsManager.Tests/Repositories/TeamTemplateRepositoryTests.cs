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
            // Arrange
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
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(template);
            await SaveChangesAsync();

            // Assert
            var savedTemplate = await Context.TeamTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
            savedTemplate.Should().NotBeNull();
            savedTemplate!.Name.Should().Be("Szablon Klasowy");
            savedTemplate.Template.Should().Be("{SchoolType} {Class} - {Subject}");
            savedTemplate.IsUniversal.Should().BeTrue();
            savedTemplate.Category.Should().Be("Klasy");
        }

        [Fact]
        public async Task GetDefaultTemplateForSchoolTypeAsync_ShouldReturnCorrectTemplate()
        {
            // Arrange
            await CleanDatabaseAsync();
            var schoolType = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.SchoolTypes.AddAsync(schoolType);
            await Context.SaveChangesAsync();

            var templates = new List<TeamTemplate>
            {
                CreateTemplate("Default LO Template", schoolType.Id, true, false), // domyślny
                CreateTemplate("Alternative LO Template", schoolType.Id, false, false),
                CreateTemplate("Default Other Template", Guid.NewGuid().ToString(), true, false), // inny typ szkoły
                CreateTemplate("Universal Template", null, false, true)
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetDefaultTemplateForSchoolTypeAsync(schoolType.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Default LO Template");
            result.IsDefault.Should().BeTrue();
            result.SchoolTypeId.Should().Be(schoolType.Id);
            result.SchoolType.Should().NotBeNull(); // Sprawdzenie Include
            result.SchoolType!.ShortName.Should().Be("LO");
        }

        [Fact]
        public async Task GetUniversalTemplatesAsync_ShouldReturnOnlyUniversalTemplates()
        {
            // Arrange
            await CleanDatabaseAsync();
            var schoolTypeId = Guid.NewGuid().ToString();
            
            var templates = new List<TeamTemplate>
            {
                CreateTemplate("Universal 1", null, false, true),
                CreateTemplate("Universal 2", null, false, true),
                CreateTemplate("School Specific 1", schoolTypeId, false, false),
                CreateTemplate("School Specific 2", schoolTypeId, true, false),
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync();

            // Tworzymy nieaktywny szablon uniwersalny
            var inactiveTemplate = CreateTemplate("Inactive Universal", null, false, true);
            await Context.TeamTemplates.AddAsync(inactiveTemplate);
            await Context.SaveChangesAsync();
            
            // Dezaktywujemy go
            inactiveTemplate.IsActive = false;
            _repository.Update(inactiveTemplate);
            await SaveChangesAsync();

            // Act
            var result = await _repository.GetUniversalTemplatesAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.IsUniversal && t.IsActive);
            result.Select(t => t.Name).Should().Contain(new[] { "Universal 1", "Universal 2" });
        }

        [Fact]
        public async Task GetTemplatesBySchoolTypeAsync_ShouldReturnMatchingTemplates()
        {
            // Arrange
            await CleanDatabaseAsync();
            
            var schoolType1 = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "LO",
                FullName = "Liceum Ogólnokształcące",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };
            
            var schoolType2 = new SchoolType
            {
                Id = Guid.NewGuid().ToString(),
                ShortName = "TECH",
                FullName = "Technikum",
                Description = "Test",
                CreatedBy = "test_user",
                IsActive = true
            };
            
            await Context.SchoolTypes.AddRangeAsync(schoolType1, schoolType2);
            await Context.SaveChangesAsync();

            var templates = new List<TeamTemplate>
            {
                CreateTemplate("LO Template 1", schoolType1.Id, false, false),
                CreateTemplate("LO Template 2", schoolType1.Id, true, false),
                CreateTemplate("TECH Template 1", schoolType2.Id, false, false),
                CreateTemplate("Universal Template", null, false, true),
                CreateTemplate("Inactive LO Template", schoolType1.Id, false, false, true) // aktywny na początku
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync();

            // Dezaktywuj jeden szablon
            var inactiveTemplate = templates.Find(t => t.Name == "Inactive LO Template");
            inactiveTemplate!.IsActive = false;
            _repository.Update(inactiveTemplate);
            await SaveChangesAsync();

            // Act
            var result = await _repository.GetTemplatesBySchoolTypeAsync(schoolType1.Id);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(t => t.SchoolTypeId == schoolType1.Id && t.IsActive);
            result.Select(t => t.Name).Should().Contain(new[] { "LO Template 1", "LO Template 2" });
            result.All(t => t.SchoolType != null).Should().BeTrue(); // Sprawdzenie Include
        }

        [Theory]
        [InlineData("Klasowy", 2)]
        [InlineData("PROJEKT", 1)]
        [InlineData("test", 3)]
        [InlineData("nieistniejacy", 0)]
        [InlineData("", 5)] // pusty string zwraca wszystkie aktywne
        [InlineData(null, 5)] // null również zwraca wszystkie aktywne
        public async Task SearchTemplatesAsync_ShouldReturnMatchingTemplates(string searchTerm, int expectedCount)
        {
            // Arrange
            await CleanDatabaseAsync();
            
            var templates = new List<TeamTemplate>
            {
                CreateTemplateWithDetails("Szablon Klasowy", "Szablon dla klas", "Klasy"),
                CreateTemplateWithDetails("Szablon Klasowy Rozszerzony", "Rozszerzony szablon", "Klasy"),
                CreateTemplateWithDetails("Szablon Projektowy", "Do projektów", "Projekty"),
                CreateTemplateWithDetails("Inny Szablon", "Test opisu", "Inne"),
                CreateTemplateWithDetails("Szablon Testowy", "Opis zawiera test", "Testy"),
                CreateTemplateWithDetails("Nieaktywny Klasowy", "Nieaktywny", "Klasy", false) // nieaktywny
            };

            await Context.TeamTemplates.AddRangeAsync(templates);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.SearchTemplatesAsync(searchTerm);

            // Assert
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
            // Arrange
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
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.TeamTemplates.AddAsync(template);
            await Context.SaveChangesAsync();

            // Act
            template.Name = "Updated Name";
            template.Template = "{Updated} - {New}";
            template.Description = "Updated Description";
            template.Category = "Updated";
            template.SortOrder = 10;
            template.UsageCount = 5;
            template.LastUsedDate = DateTime.UtcNow;
            template.MarkAsModified("updater");

            _repository.Update(template);
            await SaveChangesAsync();

            // Assert
            var updatedTemplate = await Context.TeamTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
            updatedTemplate.Should().NotBeNull();
            updatedTemplate!.Name.Should().Be("Updated Name");
            updatedTemplate.Template.Should().Be("{Updated} - {New}");
            updatedTemplate.Description.Should().Be("Updated Description");
            updatedTemplate.Category.Should().Be("Updated");
            updatedTemplate.SortOrder.Should().Be(10);
            updatedTemplate.UsageCount.Should().Be(5);
            updatedTemplate.LastUsedDate.Should().NotBeNull();
            updatedTemplate.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedTemplate.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldMarkTemplateAsInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            var template = CreateTemplate("To Delete", null, false, true);
            await Context.TeamTemplates.AddAsync(template);
            await Context.SaveChangesAsync();

            // Act
            template.MarkAsDeleted("deleter");
            _repository.Update(template);
            await SaveChangesAsync();

            // Assert
            var deletedTemplate = await Context.TeamTemplates.FirstOrDefaultAsync(t => t.Id == template.Id);
            deletedTemplate.Should().NotBeNull();
            deletedTemplate!.IsActive.Should().BeFalse();
            deletedTemplate.ModifiedBy.Should().Be("system@teamsmanager.local");
            deletedTemplate.ModifiedDate.Should().NotBeNull();
        }

        #region Helper Methods

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
                CreatedBy = "test_user",
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
                IsUniversal = true,
                Category = category,
                Language = "pl-PL",
                SortOrder = 1,
                CreatedBy = "test_user",
                IsActive = isActive
            };
        }

        #endregion
    }
    
} 