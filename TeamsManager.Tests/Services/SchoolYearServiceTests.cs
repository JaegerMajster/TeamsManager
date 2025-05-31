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
using TeamsManager.Core.Enums;
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
            // Usunięto setup dla Update, zakładając AddAsync dla nowych operacji serwisu.

            var mockCacheEntry = new Mock<ICacheEntry>();
            var changeTokens = new List<IChangeToken>();
            var postEvictionCallbacks = new List<PostEvictionCallbackRegistration>();

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

        private void AssertCacheInvalidationByReFetchingAllActiveSchoolYears(List<SchoolYear> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllSchoolYearsCacheKey, (IEnumerable<SchoolYear>?)null, false);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _schoolYearService.GetAllActiveSchoolYearsAsync().Result;

            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Once, "GetAllActiveSchoolYearsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolYearsCacheKey), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }

        private void AssertCacheInvalidationByReFetchingCurrentSchoolYear(SchoolYear? expectedDbItemAfterOperation)
        {
            SetupCacheTryGetValue(CurrentSchoolYearCacheKey, (SchoolYear?)null, false);
            _mockSchoolYearRepository.Setup(r => r.GetCurrentSchoolYearAsync())
                                 .ReturnsAsync(expectedDbItemAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _schoolYearService.GetCurrentSchoolYearAsync().Result;

            _mockSchoolYearRepository.Verify(r => r.GetCurrentSchoolYearAsync(), Times.Once, "GetCurrentSchoolYearAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(CurrentSchoolYearCacheKey), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
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
            var dbYear = new SchoolYear { Id = schoolYearId, Name = "DB Data", IsActive = true };
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
            var dbYears = new List<SchoolYear> { new SchoolYear { Id = "sy-all-db-new", IsActive = true } };
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
            var dbCurrentYear = new SchoolYear { Id = "sy-current-db-new", IsCurrent = true, IsActive = true };
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
            var createdSchoolYear = new SchoolYear { Id = "new-sy-id", Name = name, StartDate = startDate, EndDate = endDate, IsActive = true, IsCurrent = false };

            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(name)).ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.AddAsync(It.IsAny<SchoolYear>()))
                .Callback<SchoolYear>(sy => sy.Id = createdSchoolYear.Id)
                .Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate);
            result.Should().NotBeNull();
            var createdId = result!.Id;

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityName == name && op.Type == OperationType.SchoolYearCreated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + createdId), Times.AtLeastOnce, $"Klucz dla ID {createdId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never);

            AssertCacheInvalidationByReFetchingAllActiveSchoolYears(new List<SchoolYear> { result });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearCreated);
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_ShouldInvalidateRelevantCacheKeys_WhenNotCurrent()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-update-cache";
            var existingSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Old Name", StartDate = DateTime.Now.Date, EndDate = DateTime.Now.AddMonths(1).Date, IsActive = true, IsCurrent = false, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedData = new SchoolYear { Id = schoolYearId, Name = "New Name Cache", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 20), IsActive = true, IsCurrent = false };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(existingSchoolYear);
            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedData.Name)).ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var updateResult = await _schoolYearService.UpdateSchoolYearAsync(updatedData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == schoolYearId && op.Type == OperationType.SchoolYearUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.AtLeastOnce, $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never, "CurrentSchoolYearCacheKey nie powinien być usuwany, gdy rok nie był i nie jest bieżący.");

            var expectedAfterUpdate = new SchoolYear { Id = schoolYearId, Name = "New Name Cache", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 20), IsActive = true, IsCurrent = false, CreatedBy = existingSchoolYear.CreatedBy, CreatedDate = existingSchoolYear.CreatedDate };
            AssertCacheInvalidationByReFetchingAllActiveSchoolYears(new List<SchoolYear> { expectedAfterUpdate });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearUpdated);
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
                IsCurrent = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            var updatedData = new SchoolYear
            {
                Id = schoolYearId,
                Name = "New Name For Was Current",
                StartDate = new DateTime(2023, 9, 1),
                EndDate = new DateTime(2024, 6, 30),
                IsActive = true,
                IsCurrent = true
            };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId))
                                    .ReturnsAsync(existingSchoolYear);
            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedData.Name))
                                    .ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var updateResult = await _schoolYearService.UpdateSchoolYearAsync(updatedData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == schoolYearId && op.Type == OperationType.SchoolYearUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once,
                "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.AtLeastOnce,
                $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once,
                "CurrentSchoolYearCacheKey powinien zostać usunięty, gdy rok był bieżący.");

            var expectedAfterUpdate = new SchoolYear { Id = schoolYearId, Name = "New Name For Was Current", StartDate = new DateTime(2023, 9, 1), EndDate = new DateTime(2024, 6, 30), IsActive = true, IsCurrent = true, CreatedBy = existingSchoolYear.CreatedBy, CreatedDate = existingSchoolYear.CreatedDate };
            AssertCacheInvalidationByReFetchingAllActiveSchoolYears(new List<SchoolYear> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingCurrentSchoolYear(expectedAfterUpdate);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearUpdated);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_ShouldInvalidateRelevantCacheKeys_WhenNotCurrent()
        {
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete-cache";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "To Delete Cache", IsCurrent = false, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(schoolYearToDelete);
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>())).ReturnsAsync(new List<Team>());
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var deleteResult = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == schoolYearId && op.Type == OperationType.SchoolYearDeleted)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId), Times.AtLeastOnce, $"Klucz dla ID {schoolYearId} powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Never, "CurrentSchoolYearCacheKey nie powinien być usuwany, gdy usuwany rok nie był bieżący.");

            AssertCacheInvalidationByReFetchingAllActiveSchoolYears(new List<SchoolYear>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearDeleted);
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_ShouldInvalidateRelevantCaches()
        {
            ResetCapturedOperationHistory();
            var oldCurrentYearId = "sy-old-current-cache";
            var newCurrentYearId = "sy-new-current-cache";
            var oldCurrentYear = new SchoolYear { Id = oldCurrentYearId, Name = "Old Current Cache", IsCurrent = true, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-2) };
            var newCurrentYear = new SchoolYear { Id = newCurrentYearId, Name = "New Current Cache", IsCurrent = false, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(newCurrentYearId)).ReturnsAsync(newCurrentYear);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolYear, bool>>>(
                ex => ex.Compile().Invoke(oldCurrentYear)
            ))).ReturnsAsync(new List<SchoolYear> { oldCurrentYear });
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolYear, bool>>>(
                ex => !ex.Compile().Invoke(oldCurrentYear) && !ex.Compile().Invoke(newCurrentYear)
            ))).ReturnsAsync(new List<SchoolYear>());
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne


            var setResult = await _schoolYearService.SetCurrentSchoolYearAsync(newCurrentYearId);
            setResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == newCurrentYearId && op.Type == OperationType.SchoolYearSetAsCurrent)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.AtLeastOnce, "AllSchoolYearsCacheKey powinien zostać usunięty (prawdopodobnie dwa razy).");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once, "CurrentSchoolYearCacheKey powinien zostać usunięty raz (przy ustawianiu nowego bieżącego).");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + newCurrentYearId), Times.AtLeastOnce, $"Klucz ID dla nowego bieżącego roku ({newCurrentYearId}) powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(SchoolYearByIdCacheKeyPrefix + oldCurrentYearId), Times.AtLeastOnce, $"Klucz ID dla starego bieżącego roku ({oldCurrentYearId}) powinien zostać usunięty.");

            var expectedOldCurrentYearAfter = new SchoolYear { Id = oldCurrentYearId, Name = "Old Current Cache", IsCurrent = false, IsActive = true, CreatedBy = oldCurrentYear.CreatedBy, CreatedDate = oldCurrentYear.CreatedDate };
            var expectedNewCurrentYearAfter = new SchoolYear { Id = newCurrentYearId, Name = "New Current Cache", IsCurrent = true, IsActive = true, CreatedBy = newCurrentYear.CreatedBy, CreatedDate = newCurrentYear.CreatedDate };

            AssertCacheInvalidationByReFetchingAllActiveSchoolYears(new List<SchoolYear> { expectedOldCurrentYearAfter, expectedNewCurrentYearAfter });
            AssertCacheInvalidationByReFetchingCurrentSchoolYear(expectedNewCurrentYearAfter);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearSetAsCurrent);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateAllRelevantCacheKeysAndAllowReFetch()
        {
            await _schoolYearService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolYearsCacheKey), Times.Once, "AllSchoolYearsCacheKey powinien zostać usunięty przez RefreshCacheAsync.");
            _mockMemoryCache.Verify(m => m.Remove(CurrentSchoolYearCacheKey), Times.Once, "CurrentSchoolYearCacheKey powinien zostać usunięty przez RefreshCacheAsync.");

            SetupCacheTryGetValue(AllSchoolYearsCacheKey, (IEnumerable<SchoolYear>?)null, false);
            var dbYears = new List<SchoolYear> { new SchoolYear { Id = "refreshed-year", IsActive = true } };
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                  .ReturnsAsync(dbYears)
                                  .Verifiable();

            await _schoolYearService.GetAllActiveSchoolYearsAsync();

            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolYearsCacheKey), Times.Once, "Dane powinny zostać ponownie zapisane w cache po odświeżeniu i pobraniu.");
        }
    }
}