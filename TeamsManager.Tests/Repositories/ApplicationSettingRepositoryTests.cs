using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]
    public class ApplicationSettingRepositoryTests : RepositoryTestBase
    {
        private readonly ApplicationSettingRepository _repository;

        public ApplicationSettingRepositoryTests()
        {
            _repository = new ApplicationSettingRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSettingToDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var setting = new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = "test.setting.key",
                Value = "Test Value",
                Description = "Test setting for unit tests",
                Type = SettingType.String,
                Category = "Testing",
                IsRequired = false,
                IsVisible = true,
                DefaultValue = "Default Test Value",
                ValidationPattern = null,
                DisplayOrder = 1,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Act
            await _repository.AddAsync(setting);
            await SaveChangesAsync();

            // Assert
            var savedSetting = await Context.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == setting.Id);
            savedSetting.Should().NotBeNull();
            savedSetting!.Key.Should().Be("test.setting.key");
            savedSetting.Value.Should().Be("Test Value");
            savedSetting.Category.Should().Be("Testing");
            savedSetting.Type.Should().Be(SettingType.String);
            // Dodane asercje dla pól audytu
            savedSetting.CreatedBy.Should().Be("test_user"); // Domyślna wartość z TestDbContext
            savedSetting.CreatedDate.Should().NotBe(default(DateTime));
            savedSetting.ModifiedBy.Should().BeNull(); // Przy tworzeniu ModifiedBy powinno być null
            savedSetting.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSetting()
        {
            // Arrange
            await CleanDatabaseAsync();
            var setting = CreateSetting("app.name", "TeamsManager", SettingType.String, "General");
            // CreatedBy zostanie ustawione przez TestDbContext podczas Add/SaveChangesAsync
            await Context.ApplicationSettings.AddAsync(setting);
            await SaveChangesAsync(); // Zapis z audytem

            // Act
            var result = await _repository.GetByIdAsync(setting.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Key.Should().Be("app.name");
            result.Value.Should().Be("TeamsManager");
            result.CreatedBy.Should().Be("test_user"); // Sprawdzenie CreatedBy
        }

        [Fact]
        public async Task GetSettingByKeyAsync_ShouldReturnCorrectSetting()
        {
            // Arrange
            await CleanDatabaseAsync();
            var targetKey = "api.timeout";

            var settings = new List<ApplicationSetting>
            {
                CreateSetting(targetKey, "30", SettingType.Integer, "API"),
                CreateSetting("api.retries", "3", SettingType.Integer, "API"),
                CreateSetting(targetKey, "60", SettingType.Integer, "API", false), // nieaktywne z tym samym kluczem
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await Context.SaveChangesAsync(); // Zapis z audytem

            // Act
            var result = await _repository.GetSettingByKeyAsync(targetKey);

            // Assert
            result.Should().NotBeNull();
            result!.Key.Should().Be(targetKey);
            result.Value.Should().Be("30"); // aktywne ustawienie
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be("test_user");
        }

        [Fact]
        public async Task GetSettingByKeyAsync_ShouldReturnNull_WhenKeyNotExists()
        {
            // Arrange
            await CleanDatabaseAsync();
            var settings = new List<ApplicationSetting>
            {
                CreateSetting("existing.key1", "value1", SettingType.String, "General"),
                CreateSetting("existing.key2", "value2", SettingType.String, "General"),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSettingByKeyAsync("non.existing.key");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_ShouldReturnCorrectSettings()
        {
            // Arrange
            await CleanDatabaseAsync();
            var targetCategory = "Email";

            var settings = new List<ApplicationSetting>
            {
                CreateSetting("email.smtp.host", "smtp.gmail.com", SettingType.String, targetCategory),
                CreateSetting("email.smtp.port", "587", SettingType.Integer, targetCategory),
                CreateSetting("email.from.address", "noreply@test.com", SettingType.String, targetCategory),
                CreateSetting("api.key", "12345", SettingType.String, "API"),
                CreateSetting("ui.theme", "dark", SettingType.String, "UI"),
                CreateSetting("email.enabled", "true", SettingType.Boolean, targetCategory, false), // nieaktywne
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSettingsByCategoryAsync(targetCategory);

            // Assert
            result.Should().HaveCount(3);
            result.Should().OnlyContain(s => s.Category == targetCategory && s.IsActive);
            result.Select(s => s.Key).Should().Contain(new[]
            {
                "email.smtp.host",
                "email.smtp.port",
                "email.from.address"
            });
        }

        [Theory]
        [InlineData("General", 3)]
        [InlineData("Security", 2)]
        [InlineData("Performance", 1)]
        [InlineData("NonExisting", 0)]
        public async Task GetSettingsByCategoryAsync_ShouldReturnCorrectCount(string category, int expectedCount)
        {
            // Arrange
            await CleanDatabaseAsync();
            var settings = new List<ApplicationSetting>
            {
                CreateSetting("app.name", "TeamsManager", SettingType.String, "General"),
                CreateSetting("app.version", "1.0.0", SettingType.String, "General"),
                CreateSetting("app.debug", "false", SettingType.Boolean, "General"),
                CreateSetting("security.token.lifetime", "3600", SettingType.Integer, "Security"),
                CreateSetting("security.password.minlength", "8", SettingType.Integer, "Security"),
                CreateSetting("performance.cache.enabled", "true", SettingType.Boolean, "Performance"),
                CreateSetting("app.inactive", "value", SettingType.String, "General", false), // nieaktywne
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await Context.SaveChangesAsync();

            // Act
            var result = await _repository.GetSettingsByCategoryAsync(category);

            // Assert
            result.Should().HaveCount(expectedCount);
        }

        [Fact]
        public async Task Update_ShouldModifySettingData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var setting = new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = "TestSetting",
                Value = "Original Value",
                Category = "Test",
                Type = SettingType.String,
                Description = "Original description",
                IsRequired = false,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.ApplicationSettings.AddAsync(setting);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = setting.CreatedBy;
            var initialCreatedDate = setting.CreatedDate;

            // Act
            setting.Value = "Updated Value";
            setting.Description = "Updated description";
            setting.IsRequired = true;
            // Wywołanie MarkAsModified nie jest potrzebne, jeśli polegamy na automatycznym audycie TestDbContext
            // Jeśli jednak chcemy przetestować samo MarkAsModified przed audytem, patrz plan.
            // setting.MarkAsModified("updater"); // Ta wartość zostanie nadpisana

            _repository.Update(setting);
            await SaveChangesAsync(); // Zapis z audytem dla ModifiedBy

            // Assert
            var updatedSetting = await Context.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == setting.Id);
            updatedSetting.Should().NotBeNull();
            updatedSetting!.Value.Should().Be("Updated Value");
            updatedSetting.Description.Should().Be("Updated description");
            updatedSetting.IsRequired.Should().BeTrue();
            // Sprawdzenie pól audytu
            updatedSetting.CreatedBy.Should().Be(initialCreatedBy); // Nie powinno się zmienić
            updatedSetting.CreatedDate.Should().Be(initialCreatedDate); // Nie powinno się zmienić
            updatedSetting.ModifiedBy.Should().Be("test_user"); // Oczekiwana wartość z TestDbContext
            updatedSetting.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Update_ShouldModifySettingData_WithSpecificUser()
        {
            // Arrange
            await CleanDatabaseAsync();
            var setting = new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = "UserSpecificSetting",
                Value = "Original Value",
                CreatedBy = "initial_creator_ignored", // Zostanie nadpisane przez TestDbContext
                IsActive = true
            };
            await Context.ApplicationSettings.AddAsync(setting);
            await SaveChangesAsync(); // Zapis z domyślnym "test_user" jako CreatedBy

            var currentUser = "specific_test_user_for_update";
            SetTestUser(currentUser); // Ustawiamy użytkownika dla tej operacji

            // Act
            var settingToUpdate = await _repository.GetByIdAsync(setting.Id);
            settingToUpdate!.Value = "Updated by Specific User";
            // settingToUpdate.MarkAsModified(currentUser); // Niepotrzebne, jeśli polegamy na TestDbContext
            _repository.Update(settingToUpdate);
            await SaveChangesAsync(); // Zapis z audytem, użyje `currentUser`

            // Assert
            var updatedSetting = await Context.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == setting.Id);
            updatedSetting.Should().NotBeNull();
            updatedSetting!.Value.Should().Be("Updated by Specific User");
            updatedSetting.ModifiedBy.Should().Be(currentUser); // Oczekujemy użytkownika ustawionego przez SetTestUser
            updatedSetting.CreatedBy.Should().Be("test_user"); // CreatedBy z pierwszego zapisu
        }

        [Fact]
        public async Task Delete_ShouldMarkSettingAsInactive()
        {
            // Arrange
            await CleanDatabaseAsync();
            var setting = CreateSetting("deprecated.setting", "old value", SettingType.String, "Deprecated");
            // CreatedBy zostanie ustawione przez TestDbContext
            await Context.ApplicationSettings.AddAsync(setting);
            await SaveChangesAsync(); // Zapis z audytem dla CreatedBy

            var initialCreatedBy = setting.CreatedBy;
            var initialCreatedDate = setting.CreatedDate;
            var userPerformingDelete = "deleter_user_test";
            SetTestUser(userPerformingDelete); // Ustawiamy użytkownika dla operacji Delete

            // Act
            // MarkAsDeleted ustawi IsActive = false i wywoła MarkAsModified
            // TestDbContext następnie ustawi ModifiedBy na _currentTestUser (czyli userPerformingDelete)
            setting.MarkAsDeleted(userPerformingDelete); // Ta wartość `deletedBy` w MarkAsDeleted jest wewnętrzna dla encji, ale zostanie nadpisana przez DbContext
            _repository.Update(setting); // Repozytorium tylko oznacza stan jako Modified
            await SaveChangesAsync(); // TestDbContext ustawi ModifiedBy

            // Assert
            var deletedSetting = await Context.ApplicationSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == setting.Id); // AsNoTracking, aby pobrać świeże dane
            deletedSetting.Should().NotBeNull();
            deletedSetting!.IsActive.Should().BeFalse();
            deletedSetting.CreatedBy.Should().Be(initialCreatedBy);
            deletedSetting.CreatedDate.Should().Be(initialCreatedDate);
            deletedSetting.ModifiedBy.Should().Be(userPerformingDelete); // Oczekiwana wartość z TestDbContext po SetTestUser
            deletedSetting.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task SettingValidation_ShouldWorkCorrectly()
        {
            // Arrange
            await CleanDatabaseAsync();
            var settings = new List<ApplicationSetting>
            {
                CreateSettingWithValidation("port", "8080", SettingType.Integer, @"^\d{1,5}$", "Must be a valid port number"),
                CreateSettingWithValidation("email", "test@example.com", SettingType.String, @"^[^@]+@[^@]+\.[^@]+$", "Must be a valid email"),
                CreateSettingWithValidation("percentage", "75", SettingType.Integer, @"^(100|[1-9]?\d)$", "Must be between 0 and 100"),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await Context.SaveChangesAsync();

            // Act & Assert
            foreach (var setting in settings)
            {
                var savedSetting = await _repository.GetByIdAsync(setting.Id);
                savedSetting.Should().NotBeNull();
                savedSetting!.ValidationPattern.Should().NotBeNullOrEmpty();
                savedSetting.ValidationMessage.Should().NotBeNullOrEmpty();

                // W rzeczywistej aplikacji walidacja byĹ‚aby wykonywana przez metodÄ™ w modelu
                // Tu tylko sprawdzamy, czy dane sÄ… poprawnie zapisane
                savedSetting.IsValid().Should().BeTrue();
            }
        }

        #region Helper Methods

        private ApplicationSetting CreateSetting(string key, string value, SettingType type, string category, bool isActive = true)
        {
            return new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = key,
                Value = value,
                Description = $"Setting for {key}",
                Type = type,
                Category = category,
                IsRequired = false,
                IsVisible = true,
                DisplayOrder = 1,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
        }

        private ApplicationSetting CreateSettingWithValidation(string key, string value, SettingType type, string pattern, string validationMessage)
        {
            var setting = CreateSetting(key, value, type, "Validation");
            setting.ValidationPattern = pattern;
            setting.ValidationMessage = validationMessage;
            return setting;
        }

        #endregion
    }
}