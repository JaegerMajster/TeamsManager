using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class SchoolYearServiceTests
    {
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolYearService>> _mockLogger;
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly SchoolYearService _schoolYearService;
        private readonly string _currentLoggedInUserUpn = "test.admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u (spójne z serwisem)
        private const string AllSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";

        public SchoolYearServiceTests()
        {
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearService>>();
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            var mockCacheEntry = new Mock<ICacheEntry>();
            var changeTokens = new List<IChangeToken>(); // Potrzebne dla ExpirationTokens
            var postEvictionCallbacks = new List<PostEvictionCallbackRegistration>(); // Potrzebne dla PostEvictionCallbacks

            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(changeTokens);
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(postEvictionCallbacks);
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            _schoolYearService = new SchoolYearService(
                _mockSchoolYearRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockTeamRepository.Object,
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

        [Fact]
        public async Task GetSchoolYearByIdAsync_ExistingId_ShouldReturnSchoolYearAndCacheIt()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-2023";
            var expectedSchoolYear = new SchoolYear { Id = schoolYearId, Name = "2023/2024", IsActive = true };
            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;
            SetupCacheTryGetValue(cacheKey, (SchoolYear?)null, false);
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(expectedSchoolYear);

            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSchoolYear);
            _mockSchoolYearRepository.Verify(r => r.GetByIdAsync(schoolYearId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_WhenActiveYearsExist_ShouldReturnActiveSchoolYearsAndCacheThem()
        {
            ResetCapturedOperationHistory();
            var activeSchoolYears = new List<SchoolYear>
            {
                new SchoolYear { Id = "sy-1", Name = "2022/2023", IsActive = true },
                new SchoolYear { Id = "sy-2", Name = "2023/2024", IsActive = true }
            };
            SetupCacheTryGetValue(AllSchoolYearsCacheKey, (IEnumerable<SchoolYear>?)null, false);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync(activeSchoolYears);

            var result = await _schoolYearService.GetAllActiveSchoolYearsAsync();

            result.Should().NotBeNull().And.HaveCount(2).And.BeEquivalentTo(activeSchoolYears);
            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolYearsCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_OneIsCurrent_ShouldReturnItAndCacheIt()
        {
            ResetCapturedOperationHistory();
            var currentSchoolYear = new SchoolYear { Id = "sy-current", Name = "2024/2025", IsCurrent = true, IsActive = true };
            SetupCacheTryGetValue(CurrentSchoolYearCacheKey, (SchoolYear?)null, false);
            _mockSchoolYearRepository.Setup(r => r.GetCurrentSchoolYearAsync()).ReturnsAsync(currentSchoolYear);

            var result = await _schoolYearService.GetCurrentSchoolYearAsync();

            result.Should().NotBeNull().And.BeEquivalentTo(currentSchoolYear);
            _mockSchoolYearRepository.Verify(r => r.GetCurrentSchoolYearAsync(), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(CurrentSchoolYearCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_ExistingId_InCache_ShouldReturnFromCache()
        {
            var schoolYearId = "sy-cached-id";
            var expectedSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Cached Year", IsActive = true };
            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;
            SetupCacheTryGetValue(cacheKey, expectedSchoolYear, true);

            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);

            result.Should().BeEquivalentTo(expectedSchoolYear);
            _mockSchoolYearRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithForceRefresh_ShouldBypassCacheAndReCache()
        {
            var schoolYearId = "sy-force-id";
            var cachedYear = new SchoolYear { Id = schoolYearId, Name = "Cached Data" };
            var dbYear = new SchoolYear { Id = schoolYearId, Name = "DB Data" };
            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;

            SetupCacheTryGetValue(cacheKey, cachedYear, true);
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(dbYear);

            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbYear);
            _mockSchoolYearRepository.Verify(r => r.GetByIdAsync(schoolYearId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_InCache_ShouldReturnFromCache()
        {
            var cachedYears = new List<SchoolYear> { new SchoolYear { Id = "sy-all-cached", Name = "All Cached Year" } };
            SetupCacheTryGetValue(AllSchoolYearsCacheKey, cachedYears, true);

            var result = await _schoolYearService.GetAllActiveSchoolYearsAsync();

            result.Should().BeEquivalentTo(cachedYears);
            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Never);
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_WithForceRefresh_ShouldBypassCacheAndReCache()
        {
            var cachedYears = new List<SchoolYear> { new SchoolYear { Id = "sy-all-cached-old" } };
            var dbYears = new List<SchoolYear> { new SchoolYear { Id = "sy-all-db-new" } };
            SetupCacheTryGetValue(AllSchoolYearsCacheKey, cachedYears, true);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>())).ReturnsAsync(dbYears);

            var result = await _schoolYearService.GetAllActiveSchoolYearsAsync(forceRefresh: true);

            result.Should().BeEquivalentTo(dbYears);
            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolYearsCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_InCache_ShouldReturnFromCache()
        {
            var currentYear = new SchoolYear { Id = "sy-current-cached", Name = "Current Cached", IsCurrent = true };
            SetupCacheTryGetValue(CurrentSchoolYearCacheKey, currentYear, true);

            var result = await _schoolYearService.GetCurrentSchoolYearAsync();

            result.Should().BeEquivalentTo(currentYear);
            _mockSchoolYearRepository.Verify(r => r.GetCurrentSchoolYearAsync(), Times.Never);
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_WithForceRefresh_ShouldBypassCacheAndReCache()
        {
            var cachedCurrentYear = new SchoolYear { Id = "sy-current-cached-old", IsCurrent = true };
            var dbCurrentYear = new SchoolYear { Id = "sy-current-db-new", IsCurrent = true };
            SetupCacheTryGetValue(CurrentSchoolYearCacheKey, cachedCurrentYear, true);
            _mockSchoolYearRepository.Setup(r => r.GetCurrentSchoolYearAsync()).ReturnsAsync(dbCurrentYear);

            var result = await _schoolYearService.GetCurrentSchoolYearAsync(forceRefresh: true);

            result.Should().BeEquivalentTo(dbCurrentYear);
            _mockSchoolYearRepository.Verify(r => r.GetCurrentSchoolYearAsync(), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(CurrentSchoolYearCacheKey), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolYearAsync_ShouldInvalidateRelevantCacheKeys()
        {
            ResetCapturedOperationHistory();
            var name = "2026/2027";
            var startDate = new DateTime(2026, 9, 1);
            var endDate = new DateTime(2027, 6, 20);
            // SchoolYear schoolYearAddedToRepo = null; // Niepotrzebne, jeśli użyjemy ID z wyniku

            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(name)).ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.AddAsync(It.IsAny<SchoolYear>()))
                // .Callback<SchoolYear>(sy => schoolYearAddedToRepo = sy) // Można usunąć callback, jeśli ID bierzemy z wyniku
                .Returns(Task.CompletedTask);

            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate);

            result.Should().NotBeNull();
            var createdId = result!.Id; // ID jest generowane w serwisie i zwracane

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + createdId), Times.Once, $"Klucz dla ID {createdId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never, "CurrentSchoolYearCacheKey nie powinien być usuwany przy tworzeniu standardowego roku.");
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_ShouldInvalidateRelevantCacheKeys_WhenNotCurrent()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-update-cache";
            var existingSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Old Name", StartDate = DateTime.Now.Date, EndDate = DateTime.Now.AddMonths(1).Date, IsActive = true, IsCurrent = false };
            var updatedData = new SchoolYear { Id = schoolYearId, Name = "New Name Cache", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 20), IsActive = true, IsCurrent = false };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(existingSchoolYear);
            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedData.Name)).ReturnsAsync((SchoolYear?)null);

            await _schoolYearService.UpdateSchoolYearAsync(updatedData);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.Once, $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never, "CurrentSchoolYearCacheKey nie powinien być usuwany, gdy rok nie był i nie jest bieżący.");
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_ShouldInvalidateRelevantCacheKeys_WhenWasCurrent()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-update-was-current";
            var existingSchoolYear = new SchoolYear
            {
                Id = schoolYearId,
                Name = "Old Current Name",
                StartDate = new DateTime(2023, 9, 1),
                EndDate = new DateTime(2024, 6, 30),
                IsActive = true,
                IsCurrent = true
            };

            var updatedData = new SchoolYear
            {
                Id = schoolYearId,
                Name = "New Name For Was Current",
                StartDate = new DateTime(2023, 9, 1),
                EndDate = new DateTime(2024, 6, 30),
                IsActive = true,
                IsCurrent = true // Serwis ignoruje zmianę IsCurrent w tej metodzie
            };

            // Setup wszystkich wymaganych mocków - KLUCZOWE
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId))
                                    .ReturnsAsync(existingSchoolYear);

            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedData.Name))
                                    .ReturnsAsync((SchoolYear?)null); // Brak konfliktu nazwy

            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));

            // Setup dla SaveOperationHistoryAsync - BARDZO WAŻNE
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
                                          .ReturnsAsync((OperationHistory?)null);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                          .Returns(Task.CompletedTask);

            // Act
            var result = await _schoolYearService.UpdateSchoolYearAsync(updatedData);

            // Assert
            result.Should().BeTrue();

            // Verify że cache został inwalidowany
            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once,
                "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.Once,
                $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once,
                "CurrentSchoolYearCacheKey powinien zostać usunięty, gdy rok był bieżący.");
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_ShouldInvalidateRelevantCacheKeys_WhenNotCurrent()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete-cache";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "To Delete Cache", IsCurrent = false, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(schoolYearToDelete);
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>())).ReturnsAsync(new List<Team>());

            await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.Once, $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never, "CurrentSchoolYearCacheKey nie powinien być usuwany, gdy usuwany rok nie był bieżący.");
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_ShouldInvalidateRelevantCaches()
        {
            ResetCapturedOperationHistory();
            var oldCurrentYearId = "sy-old-current-cache";
            var newCurrentYearId = "sy-new-current-cache";
            var oldCurrentYear = new SchoolYear { Id = oldCurrentYearId, Name = "Old Current Cache", IsCurrent = true, IsActive = true };
            var newCurrentYear = new SchoolYear { Id = newCurrentYearId, Name = "New Current Cache", IsCurrent = false, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(newCurrentYearId)).ReturnsAsync(newCurrentYear);
            // Upewnij się, że predykat FindAsync jest poprawny, jeśli jest bardziej złożony w serwisie
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync(new List<SchoolYear> { oldCurrentYear });

            await _schoolYearService.SetCurrentSchoolYearAsync(newCurrentYearId);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.AtLeastOnce, "AllSchoolYearsCacheKey powinien zostać usunięty (prawdopodobnie dwa razy).");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once, "CurrentSchoolYearCacheKey powinien zostać usunięty raz (przy ustawianiu nowego bieżącego).");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + newCurrentYearId), Times.Once, $"Klucz ID dla nowego bieżącego roku ({newCurrentYearId}) powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + oldCurrentYearId), Times.Once, $"Klucz ID dla starego bieżącego roku ({oldCurrentYearId}) powinien zostać usunięty.");
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateAllRelevantCacheKeysAndAllowReFetch()
        {
            await _schoolYearService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty przez RefreshCacheAsync.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once, "CurrentSchoolYearCacheKey powinien zostać usunięty przez RefreshCacheAsync.");
            // Nie ma konkretnego ID, więc nie weryfikujemy SchoolYearByIdCacheKeyPrefix w tym ogólnym odświeżaniu

            SetupCacheTryGetValue(AllSchoolYearsCacheKey, (IEnumerable<SchoolYear>?)null, false);
            var dbYears = new List<SchoolYear> { new SchoolYear { Id = "refreshed-year" } };
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                  .ReturnsAsync(dbYears)
                                  .Verifiable();

            await _schoolYearService.GetAllActiveSchoolYearsAsync();

            _mockSchoolYearRepository.Verify();
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolYearsCacheKey), Times.Once, "Dane powinny zostać ponownie zapisane w cache po odświeżeniu i pobraniu.");
        }
    }
}