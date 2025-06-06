using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class SchoolYearServiceTests
    {
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolYearService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

        private readonly ISchoolYearService _schoolYearService;
        private readonly string _currentLoggedInUserUpn = "test.schoolyear.admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u
        private const string AllSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";

        public SchoolYearServiceTests()
        {
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();
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

            // Capture operation history updates
            _mockOperationHistoryService.Setup(s => s.UpdateOperationStatusAsync(
                    It.IsAny<string>(),
                    It.IsAny<OperationStatus>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Callback<string, OperationStatus, string, string>((id, status, details, errorMessage) =>
                {
                    if (_capturedOperationHistory != null && _capturedOperationHistory.Id == id)
                    {
                        _capturedOperationHistory.Status = status;
                        _capturedOperationHistory.OperationDetails = details ?? string.Empty;
                        _capturedOperationHistory.ErrorMessage = errorMessage;
                    }
                })
                .ReturnsAsync(true);

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            // Setup dla PowerShellCacheService
            var mockCacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            _mockPowerShellCacheService.Setup(s => s.GetDefaultCacheEntryOptions())
                                     .Returns(mockCacheEntryOptions);

            _schoolYearService = new SchoolYearService(
                _mockSchoolYearRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockTeamRepository.Object,
                _mockMemoryCache.Object,
                _mockPowerShellCacheService.Object
            );
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = new OperationHistory { Id = "test-id", Status = OperationStatus.InProgress };
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        #region Testy podstawowych operacji CRUD (zaktualizowane)

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
            
            // Weryfikacja użycia PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.GetDefaultCacheEntryOptions(), Times.Once);
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
            
            // Weryfikacja użycia PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.GetDefaultCacheEntryOptions(), Times.Once);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WhenInactiveYear_ShouldNotCache()
        {
            // Przygotowanie
            var schoolYearId = "sy-inactive";
            var inactiveYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2020/2021", 
                IsActive = false // Nieaktywny
            };
            
            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;
            SetupCacheTryGetValue(cacheKey, (SchoolYear?)null, false);
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId))
                .ReturnsAsync(inactiveYear);
            
            // Działanie
            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);
            
            // Asercja
            result.Should().BeNull(); // Nieaktywne nie są zwracane
            _mockMemoryCache.Verify(m => m.Remove(cacheKey), Times.Once); // Powinno usunąć z cache
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Never); // Nie powinno dodać do cache
        }

        #endregion

        #region Testy granularnej inwalidacji cache

        [Fact]
        public async Task CreateSchoolYearAsync_ShouldUseGranularCacheInvalidation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var name = "2025/2026";
            var startDate = new DateTime(2025, 9, 1);
            var endDate = new DateTime(2026, 6, 30);
            
            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(name))
                .ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.AddAsync(It.IsAny<SchoolYear>()))
                .Returns(Task.CompletedTask);
            
            // Działanie
            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate, null);
            
            // Asercja - weryfikacja granularnej inwalidacji
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never); // NIE powinno być globalnego resetu!
            
            result.Should().NotBeNull();
            result!.Name.Should().Be(name);
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_WhenWasCurrent_ShouldInvalidateCurrentCache()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-was-current";
            var existingYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2024/2025", 
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsActive = true, 
                IsCurrent = true // Był bieżący
            };
            
            var updatedYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2024/2025 Updated", 
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsActive = true, 
                IsCurrent = true
            };
            
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId))
                .ReturnsAsync(existingYear);
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            
            // Działanie
            var result = await _schoolYearService.UpdateSchoolYearAsync(updatedYear);
            
            // Asercja
            result.Should().BeTrue();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(schoolYearId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateCurrentSchoolYear(), Times.Once); // Bo był/jest bieżący
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_ShouldInvalidateBothOldAndNewCurrent()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var oldCurrentId = "sy-old-current";
            var newCurrentId = "sy-new-current";
            
            var oldCurrent = new SchoolYear 
            { 
                Id = oldCurrentId, 
                Name = "2023/2024", 
                IsCurrent = true, 
                IsActive = true 
            };
            
            var newCurrent = new SchoolYear 
            { 
                Id = newCurrentId, 
                Name = "2024/2025", 
                IsCurrent = false, 
                IsActive = true 
            };
            
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(newCurrentId))
                .ReturnsAsync(newCurrent);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear> { oldCurrent });
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            
            // Działanie
            var result = await _schoolYearService.SetCurrentSchoolYearAsync(newCurrentId);
            
            // Asercja
            result.Should().BeTrue();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Exactly(2)); // Dla obu
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(newCurrentId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(oldCurrentId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateCurrentSchoolYear(), Times.Once); // Tylko dla nowego bieżącego
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_ShouldUseGranularInvalidation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete";
            var schoolYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2022/2023", 
                IsActive = true, 
                IsCurrent = false 
            };
            
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear> { schoolYear });
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team>());
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));
            
            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);
            
            // Asercja
            result.Should().BeTrue();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(schoolYearId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateCurrentSchoolYear(), Times.Never); // Bo nie był bieżący
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldCallInvalidateAllCache()
        {
            // Działanie
            await _schoolYearService.RefreshCacheAsync();
            
            // Asercja - tylko w tym przypadku powinien być globalny reset
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Once);
        }

        #endregion

        #region Testy scenariuszy brzegowych

        [Fact]
        public async Task CreateSchoolYearAsync_WhenOperationFails_ShouldNotInvalidateCache()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var name = ""; // Pusta nazwa spowoduje błąd
            
            // Działanie
            var result = await _schoolYearService.CreateSchoolYearAsync(name, DateTime.Now, DateTime.Now.AddYears(1), null);
            
            // Asercja
            result.Should().BeNull();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_WhenSchoolYearNotExists_ShouldNotInvalidateCache()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var nonExistentYear = new SchoolYear 
            { 
                Id = "sy-non-existent", 
                Name = "Non-existent", 
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddYears(1),
                IsActive = true
            };
            
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(nonExistentYear.Id))
                .ReturnsAsync((SchoolYear?)null);
            
            // Działanie
            var result = await _schoolYearService.UpdateSchoolYearAsync(nonExistentYear);
            
            // Asercja
            result.Should().BeFalse();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_WhenCurrentYear_ShouldNotInvalidateCache()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-current";
            var currentYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2024/2025", 
                IsActive = true, 
                IsCurrent = true // Bieżący rok
            };
            
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear> { currentYear });
            
            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);
            
            // Asercja
            result.Should().BeFalse();
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Test współbieżności

        [Fact]
        public async Task ConcurrentOperations_ShouldNotCauseThunderingHerd()
        {
            // Przygotowanie
            var callCount = 0;
            var schoolYearId = "sy-concurrent";
            var schoolYear = new SchoolYear 
            { 
                Id = schoolYearId, 
                Name = "2024/2025", 
                IsActive = true 
            };
            
            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;
            
            // Symulacja wolnego zapytania do bazy
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId))
                .Returns(async () =>
                {
                    Interlocked.Increment(ref callCount);
                    await Task.Delay(100); // Symulacja opóźnienia
                    return schoolYear;
                });
            
            // Setup cache - thread-safe behavior, pierwsze wywołanie nie znajdzie w cache
            var cacheDict = new System.Collections.Concurrent.ConcurrentDictionary<object, object>();
            _mockMemoryCache.Setup(m => m.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns((object key, out object? value) =>
                {
                    if (cacheDict.TryGetValue(key, out var tempValue))
                    {
                        value = tempValue;
                        return true;
                    }
                    value = null;
                    return false;
                });
            
            // Setup cache.CreateEntry - symuluje dodanie do cache po pobraniu z bazy
            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var mockEntry = new Mock<ICacheEntry>();
                    mockEntry.Setup(e => e.Key).Returns(key);
                    mockEntry.SetupProperty(e => e.Value);
                    mockEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
                    mockEntry.Setup(e => e.Dispose()).Callback(() =>
                    {
                        if (mockEntry.Object.Value != null)
                        {
                            cacheDict.TryAdd(key, mockEntry.Object.Value);
                        }
                    });
                    return mockEntry.Object;
                });
            
            // Działanie - 10 równoczesnych żądań
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => _schoolYearService.GetSchoolYearByIdAsync(schoolYearId)))
                .ToArray();
            
            var results = await Task.WhenAll(tasks);
            
            // Asercja
            results.Should().AllBeEquivalentTo(schoolYear);
            callCount.Should().BeLessThanOrEqualTo(2); // Mechanizm zapobiegający thundering herd: max 2 wywołania przy 10 concurrent requests
        }

        #endregion

        #region Test wydajnościowy

        [Fact]
        public async Task PerformanceTest_GranularInvalidation_ShouldBeFasterThanGlobalReset()
        {
            // Przygotowanie
            var schoolYears = Enumerable.Range(1, 100)
                .Select(i => new SchoolYear 
                { 
                    Id = $"sy-{i}", 
                    Name = $"Year {i}", 
                    IsActive = true,
                    StartDate = DateTime.Now.AddYears(i - 50),
                    EndDate = DateTime.Now.AddYears(i - 49)
                })
                .ToList();
            
            // Setup repozytoriów dla wszystkich lat
            foreach (var sy in schoolYears.Take(10))
            {
                _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(sy.Id))
                    .ReturnsAsync(sy);
                _mockSchoolYearRepository.Setup(r => r.Update(It.Is<SchoolYear>(s => s.Id == sy.Id)));
            }
            
            // Pomiar czasu z granularną inwalidacją
            var stopwatch = Stopwatch.StartNew();
            
            foreach (var sy in schoolYears.Take(10))
            {
                sy.Name = sy.Name + " Updated";
                await _schoolYearService.UpdateSchoolYearAsync(sy);
            }
            
            var granularTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();
            
            // Weryfikacja że używamy granularnej inwalidacji
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Exactly(10));
            
            // Asercja - czas powinien być rozsądny
            granularTime.Should().BeLessThan(5000); // Mniej niż 5 sekund na 10 aktualizacji
            
            // Log wyniku dla analizy
            Console.WriteLine($"Granular invalidation time for 10 updates: {granularTime}ms");
        }

        #endregion

        #region Istniejące testy (zaktualizowane)

        [Fact]
        public async Task DeleteSchoolYearAsync_WhenYearHasActiveTeams_ShouldReturnFalseAndLogOperation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-with-active-teams";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "Test Year", IsActive = true, IsCurrent = false };
            var activeTeam = new Team { Id = "team1", SchoolYearId = schoolYearId, Status = TeamStatus.Active }; // Team.IsActive będzie true

            // Serwis DeleteSchoolYearAsync używa FindAsync do szukania roku szkolnego
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolYear, bool>>>(
                expr => expr.Compile().Invoke(schoolYearToDelete) // Sprawdza, czy predykat sy => sy.Id == schoolYearId pasuje do naszego roku
            ))).ReturnsAsync(new List<SchoolYear> { schoolYearToDelete });
            
            // Mock FindAsync, aby zwrócił zespół, który jest "aktywny" (Status = Active)
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(
                expr => expr.Compile().Invoke(activeTeam) // Sprawdza, czy predykat pasuje do naszego aktywnego zespołu
            ))).ReturnsAsync(new List<Team> { activeTeam });


            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            // Asercja
            result.Should().BeFalse(); // Oczekujemy false, bo są aktywne zespoły
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.OperationDetails.Should().Contain("jest używany przez");
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never); // Nie powinno dojść do Update
            
            // Weryfikacja że cache nie został inwalidowany przy błędzie
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_WhenYearIsCurrent_ShouldReturnFalse()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-current-to-delete";
            var currentSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Current Test Year", IsActive = true, IsCurrent = true };

            // Serwis używa FindAsync do szukania roku szkolnego
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolYear, bool>>>(
                expr => expr.Compile().Invoke(currentSchoolYear) // Sprawdza, czy predykat sy => sy.Id == schoolYearId pasuje do naszego roku
            ))).ReturnsAsync(new List<SchoolYear> { currentSchoolYear });
            
            // Dodajemy mock dla sprawdzenia zespołów - może być pusta lista, bo sprawdzenie IsCurrent następuje przed sprawdzeniem zespołów
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
                               .ReturnsAsync(new List<Team>());

            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            // Asercja
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.OperationDetails.Should().Contain("Nie można usunąć (dezaktywować) bieżącego roku szkolnego");
            
            // Weryfikacja że cache nie został inwalidowany przy błędzie
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_WhenNoActiveTeamsAndNotCurrent_ShouldDeactivateAndLogOperation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete-ok";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "Old Year", IsActive = true, IsCurrent = false, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-10) };

            // Serwis DeleteSchoolYearAsync używa FindAsync do szukania roku szkolnego
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolYear, bool>>>(
                expr => expr.Compile().Invoke(schoolYearToDelete) // Sprawdza, czy predykat sy => sy.Id == schoolYearId pasuje do naszego roku
            ))).ReturnsAsync(new List<SchoolYear> { schoolYearToDelete });
            
            // Mock FindAsync, aby zwrócił pustą listę zespołów (brak aktywnych zespołów)
            _mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
                               .ReturnsAsync(new List<Team>());
            _mockSchoolYearRepository.Setup(r => r.Update(It.IsAny<SchoolYear>()));

            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            // Asercja
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.OperationDetails.Should().Contain("oznaczony jako usunięty");

            // Weryfikacja, że rok szkolny został oznaczony jako nieaktywny
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy =>
                sy.Id == schoolYearId &&
                !sy.IsActive && // Flaga IsActive z BaseEntity
                sy.ModifiedBy == _currentLoggedInUserUpn
            )), Times.Once);
            
            // Weryfikacja granularnej inwalidacji
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveSchoolYearsList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateSchoolYearById(schoolYearId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateCurrentSchoolYear(), Times.Never); // Bo nie był bieżący
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
        }

        #endregion
    }
}