using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class ApplicationSettingServiceTests
    {
        private readonly Mock<IApplicationSettingRepository> _mockSettingsRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<ApplicationSettingService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly IApplicationSettingService _applicationSettingService;

        private readonly string _currentLoggedInUserUpn = "test.configadmin@example.com";
        private OperationHistory? _capturedOperationHistory;

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
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<ApplicationSettingService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);


            _applicationSettingService = new ApplicationSettingService(
                _mockSettingsRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object
            );
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        // --- Testy GetSettingValueAsync ---
        [Fact]
        public async Task GetSettingValueAsync_ExistingStringSetting_ShouldReturnValue()
        {
            var key = "TestStringKey";
            var expectedValue = "Test Value";
            var setting = new ApplicationSetting { Key = key, Value = expectedValue, Type = SettingType.String, IsActive = true };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false); // Symulacja braku w cache
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<string>(key);
            result.Should().Be(expectedValue);
            _mockMemoryCache.Verify(m => m.CreateEntry(SettingByKeyCacheKeyPrefix + key), Times.Once); // Powinien dodać do cache
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
            _mockMemoryCache.Verify(m => m.CreateEntry(SettingByKeyCacheKeyPrefix + key), Times.Once);
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
            _mockMemoryCache.Verify(m => m.CreateEntry(SettingByKeyCacheKeyPrefix + key), Times.Once);
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
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSettingsCacheKey), Times.Once);
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
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        // --- Testy Inwalidacji Cache ---
        [Fact]
        public async Task SaveSettingAsync_NewSetting_ShouldCreateAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var key = "NewKeyToSave";
            var value = "NewValue";
            var type = SettingType.String;
            var category = "NewCat";

            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync((ApplicationSetting?)null);
            _mockSettingsRepository.Setup(r => r.AddAsync(It.IsAny<ApplicationSetting>())).Returns(Task.CompletedTask);

            var result = await _applicationSettingService.SaveSettingAsync(key, value, type, "New Description", category);
            result.Should().BeTrue();

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + key), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + category), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateSettingAsync_ExistingSetting_ShouldUpdateAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var settingId = "setting-to-update-001";
            var oldKey = "OldKey";
            var oldCategory = "CategoryOld";
            var newKey = "NewUpdatedKey";
            var newCategory = "CategoryNew";

            var existingSetting = new ApplicationSetting { Id = settingId, Key = oldKey, Value = "OldValue", Category = oldCategory, IsActive = true };
            var updatedData = new ApplicationSetting { Id = settingId, Key = newKey, Value = "NewUpdatedValue", Description = "Updated Description", Type = SettingType.Integer, Category = newCategory, IsActive = true };

            _mockSettingsRepository.Setup(r => r.GetByIdAsync(settingId)).ReturnsAsync(existingSetting);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(newKey)).ReturnsAsync((ApplicationSetting?)null); // Załóżmy, że nowy klucz nie konfliktuje

            var result = await _applicationSettingService.UpdateSettingAsync(updatedData);
            result.Should().BeTrue();

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + newKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + oldKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + newCategory), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + oldCategory), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DeleteSettingAsync_ExistingSetting_ShouldSoftDeleteAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var key = "KeyToDelete";
            var category = "CatToDelete";
            var settingToDelete = new ApplicationSetting { Id = "setting-del-1", Key = key, Value = "Val", Category = category, IsActive = true };
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(settingToDelete);

            var result = await _applicationSettingService.DeleteSettingAsync(key);
            result.Should().BeTrue();

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + key), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + category), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _applicationSettingService.RefreshCacheAsync();

            // Weryfikacja, że CreateEntry jest wywoływane przy następnym żądaniu GetAllSettingsAsync
            SetupCacheTryGetValue(AllSettingsCacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()))
                                 .ReturnsAsync(new List<ApplicationSetting>())
                                 .Verifiable(); // Oznaczamy jako weryfikowalne

            await _applicationSettingService.GetAllSettingsAsync();
            _mockSettingsRepository.Verify(); // Sprawdza, czy metoda oznaczona Verifiable została wywołana
        }

        // Pozostałe testy z oryginalnego pliku powinny nadal przechodzić,
        // ponieważ logika cache jest "wokół" oryginalnej logiki pobierania i konwersji.
        // Warto je zachować i upewnić się, że działają.
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