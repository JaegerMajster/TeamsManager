using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class ApplicationSettingServiceTests
    {
        private readonly Mock<IApplicationSettingRepository> _mockSettingsRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<ApplicationSettingService>> _mockLogger;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

        private readonly IApplicationSettingService _applicationSettingService;

        private readonly string _currentLoggedInUserUpn = "test.configadmin@example.com";

        // Klucze cache
        private const string AllSettingsCacheKey = "ApplicationSettings_AllActive";
        private const string SettingsByCategoryCacheKeyPrefix = "ApplicationSettings_Category_";
        private const string SettingByKeyCacheKeyPrefix = "ApplicationSetting_Key_";

        private class JsonTestPayload
        {
            public string PropertyA { get; set; } = string.Empty;
            public int PropertyB { get; set; }
        }

        public ApplicationSettingServiceTests()
        {
            _mockSettingsRepository = new Mock<IApplicationSettingRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<ApplicationSettingService>>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            var mockOperationHistory = new OperationHistory { Id = "test-id", Status = OperationStatus.Completed };
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(mockOperationHistory);

            _applicationSettingService = new ApplicationSettingService(
                _mockSettingsRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockPowerShellCacheService.Object
            );
        }



        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            _mockPowerShellCacheService.Setup(m => m.TryGetValue(cacheKey, out item))
                                      .Returns(foundInCache);
        }

        private void AssertCacheInvalidationByReFetchingAll(List<ApplicationSetting> expectedDbSettingsAfterOperation)
        {
            SetupCacheTryGetValue(AllSettingsCacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()))
                                 .ReturnsAsync(expectedDbSettingsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _applicationSettingService.GetAllSettingsAsync().Result;

            _mockSettingsRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()), Times.Once, "GetAllSettingsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbSettingsAfterOperation);
            _mockPowerShellCacheService.Verify(m => m.Set(AllSettingsCacheKey, It.IsAny<IEnumerable<ApplicationSetting>>(), It.IsAny<TimeSpan?>()), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }


        // --- Testy GetSettingValueAsync ---
        [Fact]
        public async Task GetSettingValueAsync_ExistingStringSetting_ShouldReturnValue()
        {
            var key = "TestStringKey";
            var expectedValue = "Test Value";
            var setting = new ApplicationSetting { Key = key, Value = expectedValue, Type = SettingType.String, IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<string>(key);
            result.Should().Be(expectedValue);
            _mockPowerShellCacheService.Verify(m => m.Set(SettingByKeyCacheKeyPrefix + key, It.IsAny<ApplicationSetting>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy GetSettingByKeyAsync ---
        [Fact]
        public async Task GetSettingByKeyAsync_ExistingKey_NotInCache_ShouldReturnSettingAndCacheIt()
        {
            var key = "ExistingKeyForGet";
            var expectedSetting = new ApplicationSetting { Key = key, Value = "SomeValue", IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(expectedSetting);

            var result = await _applicationSettingService.GetSettingByKeyAsync(key);
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSetting);
            _mockSettingsRepository.Verify(r => r.GetSettingByKeyAsync(key), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(SettingByKeyCacheKeyPrefix + key, It.IsAny<ApplicationSetting>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task GetSettingByKeyAsync_ExistingKey_InCache_ShouldReturnSettingFromCache()
        {
            var key = "ExistingKeyInCache";
            var expectedSetting = new ApplicationSetting { Key = key, Value = "CachedValue", IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, expectedSetting, true);

            var result = await _applicationSettingService.GetSettingByKeyAsync(key);
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSetting);
            _mockSettingsRepository.Verify(r => r.GetSettingByKeyAsync(key), Times.Never);
        }

        [Fact]
        public async Task GetSettingByKeyAsync_WithForceRefresh_ShouldBypassCache()
        {
            var key = "KeyToForceRefresh";
            var cachedSetting = new ApplicationSetting { Key = key, Value = "Old Value" };
            var dbSetting = new ApplicationSetting { Key = key, Value = "New Value From DB", IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, cachedSetting, true);
            _mockSettingsRepository.Setup(s => s.GetSettingByKeyAsync(key)).ReturnsAsync(dbSetting);

            var result = await _applicationSettingService.GetSettingByKeyAsync(key, forceRefresh: true);

            result.Should().NotBeNull();
            result!.Value.Should().Be("New Value From DB");
            _mockSettingsRepository.Verify(s => s.GetSettingByKeyAsync(key), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(SettingByKeyCacheKeyPrefix + key, It.IsAny<ApplicationSetting>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy GetAllSettingsAsync ---
        [Fact]
        public async Task GetAllSettingsAsync_WhenSettingsExist_NotInCache_ShouldReturnAndCache()
        {
            var activeSettings = new List<ApplicationSetting>
            {
                new ApplicationSetting { Key = "Key1", Value = "Val1", IsActive = true },
                new ApplicationSetting { Key = "Key2", Value = "Val2", IsActive = true }
            };
            SetupCacheTryGetValue(AllSettingsCacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()))
                                 .ReturnsAsync(activeSettings);

            var result = await _applicationSettingService.GetAllSettingsAsync();
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(activeSettings);
            _mockPowerShellCacheService.Verify(m => m.Set(AllSettingsCacheKey, It.IsAny<IEnumerable<ApplicationSetting>>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        // --- Testy GetSettingsByCategoryAsync ---
        [Fact]
        public async Task GetSettingsByCategoryAsync_ExistingCategoryWithSettings_NotInCache_ShouldReturnAndCache()
        {
            var category = "General";
            var cacheKey = SettingsByCategoryCacheKeyPrefix + category;
            var settingsInCategory = new List<ApplicationSetting>
            {
                new ApplicationSetting { Key = "SiteName", Category = category, IsActive = true, Value = "Moja Strona" }
            };
            SetupCacheTryGetValue(cacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingsByCategoryAsync(category)).ReturnsAsync(settingsInCategory);

            var result = await _applicationSettingService.GetSettingsByCategoryAsync(category);
            result.Should().NotBeNull().And.ContainSingle();
            result.Should().BeEquivalentTo(settingsInCategory);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.IsAny<IEnumerable<ApplicationSetting>>(), It.IsAny<TimeSpan?>()), Times.Once);
        }


        // --- Testy Inwalidacji Cache ---
        [Fact]
        public async Task SaveSettingAsync_NewSetting_ShouldCreateAndInvalidateCache()
        {
            var key = "NewKeyToSave";
            var value = "NewValue";
            var type = SettingType.String;
            var category = "NewCat";
            var newSetting = new ApplicationSetting { Id = "new-id", Key = key, Value = value, Type = type, Category = category, IsActive = true };

            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync((ApplicationSetting?)null);
            _mockSettingsRepository.Setup(r => r.AddAsync(It.IsAny<ApplicationSetting>()))
                                 .Callback<ApplicationSetting>(s => s.Id = newSetting.Id)
                                 .Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Usunięte, bo nie jest już potrzebne

            var result = await _applicationSettingService.SaveSettingAsync(key, value, type, "New Description", category);
            result.Should().BeTrue();

            // Weryfikacja, że operacja historii została utworzona i zaktualizowana
            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingCreated,
                nameof(ApplicationSetting),
                It.IsAny<string?>(),
                key,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                It.IsAny<string>(),
                OperationStatus.Completed,
                It.IsAny<string>(),
                It.IsAny<string?>()), Times.Once);

            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingByKey(key), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingsByCategory(category), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSettingsList(), Times.AtLeastOnce);

            var expectedSettingsAfterCreate = new List<ApplicationSetting> { newSetting };
            AssertCacheInvalidationByReFetchingAll(expectedSettingsAfterCreate);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task UpdateSettingAsync_ExistingSetting_ShouldUpdateAndInvalidateCache()
        {
            var settingId = "setting-to-update-001";
            var oldKey = "OldKey";
            var oldCategory = "CategoryOld";
            var newKey = "NewUpdatedKey";
            var newCategory = "CategoryNew";

            var existingSettingInDb = new ApplicationSetting { Id = settingId, Key = oldKey, Value = "OldValue", Category = oldCategory, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedDataForService = new ApplicationSetting { Id = settingId, Key = newKey, Value = "NewUpdatedValue", Description = "Updated Description", Type = SettingType.Integer, Category = newCategory, IsActive = true };

            _mockSettingsRepository.Setup(r => r.GetByIdAsync(settingId)).ReturnsAsync(existingSettingInDb);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(newKey)).ReturnsAsync((ApplicationSetting?)null);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Usunięte

            var result = await _applicationSettingService.UpdateSettingAsync(updatedDataForService);
            result.Should().BeTrue();

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingUpdated,
                nameof(ApplicationSetting),
                settingId,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                It.IsAny<string>(),
                OperationStatus.Completed,
                It.IsAny<string>(),
                It.IsAny<string?>()), Times.Once);

            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingByKey(newKey), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingByKey(oldKey), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingsByCategory(newCategory), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingsByCategory(oldCategory), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSettingsList(), Times.AtLeastOnce);

            var expectedSettingsAfterUpdate = new List<ApplicationSetting>
            {
                new ApplicationSetting
                {
                    Id = settingId, Key = newKey, Value = "NewUpdatedValue", Category = newCategory, Type = SettingType.Integer,
                    Description = "Updated Description", IsActive = true, CreatedBy = existingSettingInDb.CreatedBy, CreatedDate = existingSettingInDb.CreatedDate
                }
            };
            AssertCacheInvalidationByReFetchingAll(expectedSettingsAfterUpdate);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task DeleteSettingAsync_ExistingSetting_ShouldSoftDeleteAndInvalidateCache()
        {
            var key = "KeyToDelete";
            var category = "CatToDelete";
            var settingToDelete = new ApplicationSetting { Id = "setting-del-1", Key = key, Value = "Val", Category = category, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(settingToDelete);
            _mockSettingsRepository.Setup(r => r.Update(It.IsAny<ApplicationSetting>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Usunięte

            var result = await _applicationSettingService.DeleteSettingAsync(key);
            result.Should().BeTrue();

            _mockOperationHistoryService.Verify(s => s.CreateNewOperationEntryAsync(
                OperationType.ApplicationSettingDeleted,
                nameof(ApplicationSetting),
                key, // Używa klucza, nie ID
                key,
                It.IsAny<string?>(),
                It.IsAny<string?>()), Times.Once);
            _mockOperationHistoryService.Verify(s => s.UpdateOperationStatusAsync(
                It.IsAny<string>(),
                OperationStatus.Completed,
                It.IsAny<string>(),
                It.IsAny<string?>()), Times.Once);

            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingByKey(key), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSettingsByCategory(category), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSettingsList(), Times.AtLeastOnce);

            var expectedSettingsAfterDelete = new List<ApplicationSetting>();
            AssertCacheInvalidationByReFetchingAll(expectedSettingsAfterDelete);

            // Weryfikujemy wywołania serwisu operacji - szczegóły statusu operacji są testowane w OperationHistoryServiceTests
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _applicationSettingService.RefreshCacheAsync();

            SetupCacheTryGetValue(AllSettingsCacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()))
                                 .ReturnsAsync(new List<ApplicationSetting>())
                                 .Verifiable();

            await _applicationSettingService.GetAllSettingsAsync();
            _mockSettingsRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingIntSetting_ShouldReturnValue()
        {
            var key = "TestIntKey";
            var expectedValue = 123;
            var setting = new ApplicationSetting { Key = key, Value = expectedValue.ToString(), Type = SettingType.Integer, IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<int>(key);
            result.Should().Be(expectedValue);
        }
    }
}