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
            // Konstruktor RepositoryTestBase (poprzez IntegrationTestBase)
            // ustawi domyślnego użytkownika na "test_user_integration_base_default"
            // w CurrentUserService. Ten użytkownik będzie użyty do pól audytu,
            // jeśli test nie ustawi innego.
            _repository = new ApplicationSettingRepository(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddSettingToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            // Nie ma potrzeby wywoływania SetTestUser() tutaj, jeśli chcemy użyć domyślnego
            // użytkownika z IntegrationTestBase dla CreatedBy.

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
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(setting);
            await SaveChangesAsync();

            // Asercja
            var savedSetting = await Context.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == setting.Id);
            savedSetting.Should().NotBeNull();
            savedSetting!.Key.Should().Be("test.setting.key");
            savedSetting.Value.Should().Be("Test Value");
            savedSetting.Category.Should().Be("Testing");
            savedSetting.Type.Should().Be(SettingType.String);

            savedSetting.CreatedBy.Should().Be("test_user_integration_base_default");
            savedSetting.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            savedSetting.ModifiedBy.Should().BeNull();
            savedSetting.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectSetting()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var setting = CreateSetting("app.name", "TeamsManager", SettingType.String, "General");
            await Context.ApplicationSettings.AddAsync(setting);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetByIdAsync(setting.Id);

            // Asercja
            result.Should().NotBeNull();
            result!.Key.Should().Be("app.name");
            result.Value.Should().Be("TeamsManager");
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetSettingByKeyAsync_ShouldReturnCorrectSetting()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var targetKey = "api.timeout";

            var settings = new List<ApplicationSetting>
            {
                CreateSetting(targetKey, "30", SettingType.Integer, "API", isActive: true),
                CreateSetting("api.retries", "3", SettingType.Integer, "API", isActive: true),
                CreateSetting(targetKey, "60", SettingType.Integer, "API", isActive: false),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetSettingByKeyAsync(targetKey);

            // Asercja
            result.Should().NotBeNull();
            result!.Key.Should().Be(targetKey);
            result.Value.Should().Be("30");
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetSettingByKeyAsync_ShouldReturnNull_WhenKeyNotExists()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var settings = new List<ApplicationSetting>
            {
                CreateSetting("existing.key1", "value1", SettingType.String, "General"),
                CreateSetting("existing.key2", "value2", SettingType.String, "General"),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetSettingByKeyAsync("non.existing.key");

            // Asercja
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_ShouldReturnCorrectSettings()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var targetCategory = "Email";

            var settings = new List<ApplicationSetting>
            {
                CreateSetting("email.smtp.host", "smtp.gmail.com", SettingType.String, targetCategory, isActive: true),
                CreateSetting("email.smtp.port", "587", SettingType.Integer, targetCategory, isActive: true),
                CreateSetting("email.from.address", "noreply@test.com", SettingType.String, targetCategory, isActive: true),
                CreateSetting("api.key", "12345", SettingType.String, "API", isActive: true),
                CreateSetting("ui.theme", "dark", SettingType.String, "UI", isActive: true),
                CreateSetting("email.enabled", "true", SettingType.Boolean, targetCategory, isActive: false),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetSettingsByCategoryAsync(targetCategory);

            // Asercja
            result.Should().HaveCount(3);
            result.Should().OnlyContain(s => s.Category == targetCategory && s.IsActive);
            result.First().CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Theory]
        [InlineData("General", 3)]
        [InlineData("Security", 2)]
        [InlineData("Performance", 1)]
        [InlineData("NonExisting", 0)]
        public async Task GetSettingsByCategoryAsync_ShouldReturnCorrectCount(string category, int expectedCount)
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var settings = new List<ApplicationSetting>
            {
                CreateSetting("app.name", "TeamsManager", SettingType.String, "General", isActive: true),
                CreateSetting("app.version", "1.0.0", SettingType.String, "General", isActive: true),
                CreateSetting("app.debug", "false", SettingType.Boolean, "General", isActive: true),
                CreateSetting("security.token.lifetime", "3600", SettingType.Integer, "Security", isActive: true),
                CreateSetting("security.password.minlength", "8", SettingType.Integer, "Security", isActive: true),
                CreateSetting("performance.cache.enabled", "true", SettingType.Boolean, "Performance", isActive: true),
                CreateSetting("app.inactive", "value", SettingType.String, "General", isActive: false),
            };

            await Context.ApplicationSettings.AddRangeAsync(settings);
            await SaveChangesAsync();

            // Działanie
            var result = await _repository.GetSettingsByCategoryAsync(category);

            // Asercja
            result.Should().HaveCount(expectedCount);
            // Jeśli lista nie jest pusta, sprawdzamy CreatedBy pierwszego elementu
            if (result.Any())
            {
                result.First().CreatedBy.Should().Be("test_user_integration_base_default");
            }
        }

        [Fact]
        public async Task Update_ShouldModifySettingData_AndSetCorrectModifiedBy()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var initialCreator = "creator_user_for_update";
            SetTestUser(initialCreator);

            var setting = new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = "TestSettingToUpdate",
                Value = "Original Value",
                Category = "Test",
                Type = SettingType.String,
                Description = "Original description",
                IsRequired = false,
                IsActive = true
            };
            await _repository.AddAsync(setting);
            await SaveChangesAsync();

            var initialCreatedBy = setting.CreatedBy;
            var initialCreatedDate = setting.CreatedDate;

            var updaterUser = "updater_user_for_setting_test";
            SetTestUser(updaterUser);

            // Działanie
            var settingToUpdate = await _repository.GetByIdAsync(setting.Id);
            settingToUpdate.Should().NotBeNull();
            settingToUpdate!.Value = "Updated Value";
            settingToUpdate.Description = "Updated description";
            settingToUpdate.IsRequired = true;

            _repository.Update(settingToUpdate);
            await SaveChangesAsync();

            // Asercja
            var updatedSetting = await Context.ApplicationSettings.FirstOrDefaultAsync(s => s.Id == setting.Id);
            updatedSetting.Should().NotBeNull();
            updatedSetting!.Value.Should().Be("Updated Value");
            updatedSetting.Description.Should().Be("Updated description");
            updatedSetting.IsRequired.Should().BeTrue();

            updatedSetting.CreatedBy.Should().Be(initialCreator);
            updatedSetting.CreatedDate.Should().Be(initialCreatedDate);
            updatedSetting.ModifiedBy.Should().Be(updaterUser);
            updatedSetting.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldMarkSettingAsInactive_AndSetCorrectModifiedBy()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var initialCreator = "initial_creator_for_delete_test";
            SetTestUser(initialCreator);

            var setting = CreateSetting("deprecated.setting", "old value", SettingType.String, "Deprecated", isActive: true);
            await _repository.AddAsync(setting);
            await SaveChangesAsync();

            var initialCreatedBy = setting.CreatedBy;
            var initialCreatedDate = setting.CreatedDate;

            var deleterUser = "user_who_deactivates_setting_test";
            SetTestUser(deleterUser);

            // Działanie
            var settingToMarkAsInactive = await _repository.GetByIdAsync(setting.Id);
            settingToMarkAsInactive.Should().NotBeNull();

            settingToMarkAsInactive!.IsActive = false;

            _repository.Update(settingToMarkAsInactive);
            await SaveChangesAsync();

            // Weryfikacja
            var deactivatedSetting = await Context.ApplicationSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == setting.Id);

            deactivatedSetting.Should().NotBeNull();
            deactivatedSetting!.IsActive.Should().BeFalse();
            deactivatedSetting.ModifiedBy.Should().Be(deleterUser);
            deactivatedSetting.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            deactivatedSetting.CreatedBy.Should().Be(initialCreator);

            ResetTestUser();
        }

        // Testy dla SettingValidation_ShouldWorkCorrectly nie sprawdzają pól audytu,
        // więc nie wymagają zmian w tym kontekście.

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