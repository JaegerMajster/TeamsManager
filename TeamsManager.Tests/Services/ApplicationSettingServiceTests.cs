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

        private const string AllSettingsCacheKey = "ApplicationSettings_All";
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

            // General setup for CreateEntry to return a well-behaved mock ICacheEntry
            // This will be used by all tests that might trigger a cache write.
            SetupCacheCreateEntry();


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

        // This method now just performs the setup on the class-level _mockMemoryCache
        private void SetupCacheCreateEntry()
        {
            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Returns(() => { // Use a factory function to return a new mock entry each time CreateEntry is called
                    var mockEntry = new Mock<ICacheEntry>();
                    mockEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
                    mockEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
                    mockEntry.SetupProperty(e => e.Value);
                    mockEntry.SetupProperty(e => e.AbsoluteExpiration, null);
                    mockEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow, null);
                    mockEntry.SetupProperty(e => e.SlidingExpiration, null);
                    mockEntry.SetupProperty(e => e.Priority, CacheItemPriority.Normal);
                    mockEntry.SetupProperty(e => e.Size, null);
                    return mockEntry.Object;
                });
        }


        // Testy dla GetSettingValueAsync<T>
        [Fact]
        public async Task GetSettingValueAsync_ExistingStringSetting_ShouldReturnValue()
        {
            var key = "TestStringKey";
            var expectedValue = "Test Value";
            var setting = new ApplicationSetting { Key = key, Value = expectedValue, Type = SettingType.String };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);
            // SetupCacheCreateEntry() is called in constructor

            var result = await _applicationSettingService.GetSettingValueAsync<string>(key);
            result.Should().Be(expectedValue);
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingStringSetting_FromCache_ShouldReturnValue()
        {
            var key = "TestStringKeyCached";
            var expectedValue = "Test Value Cached";
            var setting = new ApplicationSetting { Key = key, Value = expectedValue, Type = SettingType.String };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, setting, true);

            var result = await _applicationSettingService.GetSettingValueAsync<string>(key);
            result.Should().Be(expectedValue);
            _mockSettingsRepository.Verify(r => r.GetSettingByKeyAsync(key), Times.Never);
        }


        [Fact]
        public async Task GetSettingValueAsync_ExistingIntSetting_ShouldReturnValue()
        {
            var key = "TestIntKey";
            var expectedValue = 123;
            var setting = new ApplicationSetting { Key = key, Value = expectedValue.ToString(), Type = SettingType.Integer };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<int>(key);
            result.Should().Be(expectedValue);
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingBoolSetting_ShouldReturnValue()
        {
            var key = "TestBoolKey";
            var setting = new ApplicationSetting { Key = key, Value = "true", Type = SettingType.Boolean };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<bool>(key);
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingDateTimeSetting_ShouldReturnValue()
        {
            var key = "TestDateTimeKey";
            var expectedValue = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var setting = new ApplicationSetting { Key = key, Value = expectedValue.ToString("yyyy-MM-dd HH:mm:ss"), Type = SettingType.DateTime };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<DateTime>(key);
            result.Should().Be(expectedValue);
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingDecimalSetting_ShouldReturnValue()
        {
            var key = "TestDecimalKey";
            var expectedValue = 123.45m;
            var setting = new ApplicationSetting { Key = key, Value = expectedValue.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = SettingType.Decimal };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<decimal>(key);
            result.Should().Be(expectedValue);
        }

        [Fact]
        public async Task GetSettingValueAsync_ExistingJsonSetting_ShouldDeserializeAndReturnValue()
        {
            var key = "TestJsonKey";
            var payload = new JsonTestPayload { PropertyA = "Data", PropertyB = 99 };
            var jsonString = JsonSerializer.Serialize(payload);
            var setting = new ApplicationSetting { Key = key, Value = jsonString, Type = SettingType.Json };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<JsonTestPayload>(key);
            result.Should().NotBeNull();
            result!.PropertyA.Should().Be(payload.PropertyA);
            result.PropertyB.Should().Be(payload.PropertyB);
        }

        [Fact]
        public async Task GetSettingValueAsync_SettingNotFound_ShouldReturnDefaultValue()
        {
            var key = "NonExistentKey";
            var defaultValue = "Default";
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync((ApplicationSetting?)null);

            var result = await _applicationSettingService.GetSettingValueAsync<string>(key, defaultValue);
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task GetSettingValueAsync_SettingValueNullOrWhitespace_ShouldReturnDefaultValue()
        {
            var key = "KeyWithNullValue";
            var defaultValue = "DefaultForNull";
            var settingWithNullValue = new ApplicationSetting { Key = key, Value = null!, Type = SettingType.String };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(settingWithNullValue);

            var keyWithWhitespace = "KeyWithWhitespaceValue";
            var settingWithWhitespace = new ApplicationSetting { Key = keyWithWhitespace, Value = "   ", Type = SettingType.String };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + keyWithWhitespace, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(keyWithWhitespace)).ReturnsAsync(settingWithWhitespace);

            var resultNull = await _applicationSettingService.GetSettingValueAsync<string>(key, defaultValue);
            var resultWhitespace = await _applicationSettingService.GetSettingValueAsync<string>(keyWithWhitespace, defaultValue);
            resultNull.Should().Be(defaultValue);
            resultWhitespace.Should().Be(defaultValue);
        }


        [Fact]
        public async Task GetSettingValueAsync_TypeMismatch_ShouldReturnDefaultValueAndLogWarning()
        {
            var key = "IntSettingAsString";
            var defaultValue = 0;
            var setting = new ApplicationSetting { Key = key, Value = "NotAnInt", Type = SettingType.String };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<int>(key, defaultValue);
            result.Should().Be(defaultValue);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Niezgodność typu dla ustawienia '{key}'. Oczekiwano Int32, znaleziono String (String).")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }


        [Fact]
        public async Task GetSettingValueAsync_InvalidValueForType_ShouldReturnDefaultValueAndLogWarning()
        {
            var key = "InvalidIntSetting";
            var defaultValuePassedToService = -1;
            var expectedResultBasedOnCurrentLikelyServiceLogic = 0;

            var setting = new ApplicationSetting { Key = key, Value = "abc", Type = SettingType.Integer };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(setting);

            var result = await _applicationSettingService.GetSettingValueAsync<int>(key, defaultValuePassedToService);
            result.Should().Be(expectedResultBasedOnCurrentLikelyServiceLogic);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v != null &&
                        v.ToString()!.Contains($"Pobieranie wartości ustawienia o kluczu: {key}") &&
                        v.ToString()!.Contains($"jako typ {typeof(int).Name}")
                    ),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }


        // Testy dla GetSettingByKeyAsync
        [Fact]
        public async Task GetSettingByKeyAsync_ExistingKey_NotInCache_ShouldReturnSettingAndCacheIt()
        {
            var key = "ExistingKeyForGet";
            var expectedSetting = new ApplicationSetting { Key = key, Value = "SomeValue" };
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
            var expectedSetting = new ApplicationSetting { Key = key, Value = "CachedValue" };
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, expectedSetting, true);

            var result = await _applicationSettingService.GetSettingByKeyAsync(key);
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSetting);
            _mockSettingsRepository.Verify(r => r.GetSettingByKeyAsync(key), Times.Never);
        }


        [Fact]
        public async Task GetSettingByKeyAsync_NonExistingKey_ShouldReturnNull()
        {
            var key = "NonExistingKeyForGet";
            SetupCacheTryGetValue(SettingByKeyCacheKeyPrefix + key, (ApplicationSetting?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync((ApplicationSetting?)null);

            var result = await _applicationSettingService.GetSettingByKeyAsync(key);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSettingByKeyAsync_EmptyKey_ShouldReturnNullAndLogWarning()
        {
            var key = "";
            var result = await _applicationSettingService.GetSettingByKeyAsync(key);
            result.Should().BeNull();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Próba pobrania ustawienia z pustym kluczem.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }


        // Testy dla GetAllSettingsAsync
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

        [Fact]
        public async Task GetAllSettingsAsync_WhenSettingsExist_InCache_ShouldReturnFromCache()
        {
            var cachedSettings = new List<ApplicationSetting> { new ApplicationSetting { Key = "CachedKey1" } };
            SetupCacheTryGetValue(AllSettingsCacheKey, cachedSettings, true);

            var result = await _applicationSettingService.GetAllSettingsAsync();
            result.Should().BeEquivalentTo(cachedSettings);
            _mockSettingsRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetAllSettingsAsync_ForceRefresh_ShouldFetchFromRepositoryAndReCache()
        {
            var initialCachedSettings = new List<ApplicationSetting> { new ApplicationSetting { Key = "InitialCachedKey" } };
            var freshSettingsFromDb = new List<ApplicationSetting> { new ApplicationSetting { Key = "FreshKey1" }, new ApplicationSetting { Key = "FreshKey2" } };

            SetupCacheTryGetValue(AllSettingsCacheKey, initialCachedSettings, true);
            _mockSettingsRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()))
                                 .ReturnsAsync(freshSettingsFromDb);

            var result = await _applicationSettingService.GetAllSettingsAsync(forceRefresh: true);
            result.Should().BeEquivalentTo(freshSettingsFromDb);
            _mockSettingsRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<ApplicationSetting, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSettingsCacheKey), Times.Once);
        }



        // Testy dla GetSettingsByCategoryAsync
        [Fact]
        public async Task GetSettingsByCategoryAsync_ExistingCategoryWithSettings_NotInCache_ShouldReturnAndCache()
        {
            var category = "General";
            var cacheKey = SettingsByCategoryCacheKeyPrefix + category;
            var settingsInCategory = new List<ApplicationSetting>
            {
                new ApplicationSetting { Key = "SiteName", Category = category, IsActive = true }
            };
            SetupCacheTryGetValue(cacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingsByCategoryAsync(category)).ReturnsAsync(settingsInCategory);

            var result = await _applicationSettingService.GetSettingsByCategoryAsync(category);
            result.Should().NotBeNull().And.ContainSingle();
            result.Should().BeEquivalentTo(settingsInCategory);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_ExistingCategoryWithSettings_InCache_ShouldReturnFromCache()
        {
            var category = "GeneralCached";
            var cacheKey = SettingsByCategoryCacheKeyPrefix + category;
            var cachedSettings = new List<ApplicationSetting> { new ApplicationSetting { Key = "CachedSiteName", Category = category } };
            SetupCacheTryGetValue(cacheKey, cachedSettings, true);

            var result = await _applicationSettingService.GetSettingsByCategoryAsync(category);
            result.Should().BeEquivalentTo(cachedSettings);
            _mockSettingsRepository.Verify(r => r.GetSettingsByCategoryAsync(category), Times.Never);
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_ForceRefresh_ShouldFetchFromRepositoryAndReCache()
        {
            var category = "GeneralForceRefresh";
            var cacheKey = SettingsByCategoryCacheKeyPrefix + category;
            var initialCachedSettings = new List<ApplicationSetting> { new ApplicationSetting { Key = "InitialCatCachedKey", Category = category } };
            var freshSettingsFromDb = new List<ApplicationSetting> { new ApplicationSetting { Key = "FreshCatKey1", Category = category } };

            SetupCacheTryGetValue(cacheKey, initialCachedSettings, true);
            _mockSettingsRepository.Setup(r => r.GetSettingsByCategoryAsync(category)).ReturnsAsync(freshSettingsFromDb);

            var result = await _applicationSettingService.GetSettingsByCategoryAsync(category, forceRefresh: true);
            result.Should().BeEquivalentTo(freshSettingsFromDb);
            _mockSettingsRepository.Verify(r => r.GetSettingsByCategoryAsync(category), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(cacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        [Fact]
        public async Task GetSettingsByCategoryAsync_CategoryNotFoundOrEmpty_ShouldReturnEmptyList()
        {
            var category = "NonExistentCat";
            var cacheKey = SettingsByCategoryCacheKeyPrefix + category;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<ApplicationSetting>?)null, false);
            _mockSettingsRepository.Setup(r => r.GetSettingsByCategoryAsync(category)).ReturnsAsync(new List<ApplicationSetting>());
            // SetupCacheCreateEntry() jest wywoływane w konstruktorze, więc CreateEntry będzie zamockowane

            var result = await _applicationSettingService.GetSettingsByCategoryAsync(category);
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once); // Sprawdzamy, czy pusta lista jest cache'owana
        }

        [Fact]
        public async Task GetSettingsByCategoryAsync_EmptyCategory_ShouldReturnEmptyListAndLogWarning()
        {
            var result = await _applicationSettingService.GetSettingsByCategoryAsync("   ");
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Warning,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Próba pobrania ustawień dla pustej kategorii.")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }


        // Testy dla SaveSettingAsync
        [Fact]
        public async Task SaveSettingAsync_NewSetting_ShouldCreateAndReturnTrueAndLogAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var key = "NewKeyToSave";
            var value = "NewValue";
            var type = SettingType.String;
            var category = "NewCat";
            ApplicationSetting? addedSetting = null;

            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync((ApplicationSetting?)null);
            _mockSettingsRepository.Setup(r => r.AddAsync(It.IsAny<ApplicationSetting>()))
                                 .Callback<ApplicationSetting>(s => addedSetting = s)
                                 .Returns(Task.CompletedTask);

            var result = await _applicationSettingService.SaveSettingAsync(key, value, type, "New Description", category);
            result.Should().BeTrue();
            addedSetting.Should().NotBeNull();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.ApplicationSettingCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + key), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + category), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.Once);
        }

        [Fact]
        public async Task SaveSettingAsync_ExistingSetting_ShouldUpdateAndReturnTrueAndLogAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var key = "ExistingKeyToSave";
            var oldValue = "OldValue";
            var newValue = "UpdatedValue";
            var type = SettingType.String;
            var oldCategory = "OldCat";
            var newCategory = "UpdatedCat";
            var existingSetting = new ApplicationSetting { Id = "setting-id-1", Key = key, Value = oldValue, Type = type, Category = oldCategory, CreatedBy = "initial_user" };

            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(existingSetting);

            var result = await _applicationSettingService.SaveSettingAsync(key, newValue, type, "Updated Desc", newCategory);
            result.Should().BeTrue();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.ApplicationSettingUpdated);

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + key), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + newCategory), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + oldCategory), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.Once);
        }

        [Fact]
        public async Task SaveSettingAsync_EmptyKey_ShouldReturnFalseAndLogOperationHistory()
        {
            ResetCapturedOperationHistory();
            var result = await _applicationSettingService.SaveSettingAsync("", "value", SettingType.String);
            result.Should().BeFalse();
            _mockSettingsRepository.Verify(r => r.AddAsync(It.IsAny<ApplicationSetting>()), Times.Never);
            _mockSettingsRepository.Verify(r => r.Update(It.IsAny<ApplicationSetting>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Klucz ustawienia nie może być pusty.");
        }

        // Testy dla UpdateSettingAsync
        [Fact]
        public async Task UpdateSettingAsync_ExistingSettingWithValidData_ShouldUpdateAndReturnTrueAndLogAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var settingId = "setting-to-update-001";
            var oldKey = "OldKey";
            var oldCategory = "CategoryOld";
            var newKey = "NewUpdatedKey";
            var newCategory = "CategoryNew";

            var existingSetting = new ApplicationSetting { Id = settingId, Key = oldKey, Value = "OldValue", Category = oldCategory, CreatedBy = "initial_user" };
            var updatedData = new ApplicationSetting { Id = settingId, Key = newKey, Value = "NewUpdatedValue", Description = "Updated Description", Type = SettingType.Integer, Category = newCategory, IsActive = true };

            _mockSettingsRepository.Setup(r => r.GetByIdAsync(settingId)).ReturnsAsync(existingSetting);
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(newKey)).ReturnsAsync((ApplicationSetting?)null);

            var result = await _applicationSettingService.UpdateSettingAsync(updatedData);
            result.Should().BeTrue();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.ApplicationSettingUpdated);

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + newKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + oldKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + newCategory), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + oldCategory), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.Once);
        }


        // Testy dla DeleteSettingAsync
        [Fact]
        public async Task DeleteSettingAsync_ExistingSetting_ShouldSoftDeleteAndReturnTrueAndLogAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var key = "KeyToDelete";
            var category = "CatToDelete";
            var settingToDelete = new ApplicationSetting { Id = "setting-del-1", Key = key, Value = "Val", Category = category, IsActive = true };
            _mockSettingsRepository.Setup(r => r.GetSettingByKeyAsync(key)).ReturnsAsync(settingToDelete);

            var result = await _applicationSettingService.DeleteSettingAsync(key);
            result.Should().BeTrue();

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.ApplicationSettingDeleted);

            _mockMemoryCache.Verify(m => m.Remove(SettingByKeyCacheKeyPrefix + key), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + category), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllSettingsCacheKey), Times.Once);
        }

        // Testy dla RefreshCacheAsync
        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCancellationTokenAndLog()
        {
            await _applicationSettingService.RefreshCacheAsync();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rozpoczynanie odświeżania całego cache'a ustawień aplikacji.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Information,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cache ustawień aplikacji został zresetowany.")),
                   null,
                   It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
               Times.Once);
        }
    }
}
