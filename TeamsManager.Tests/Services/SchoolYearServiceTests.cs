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
        private readonly Mock<ITeamRepository> _mockTeamRepository; // Dodane dla testu DeleteSchoolYearAsync
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly SchoolYearService _schoolYearService;
        private readonly string _currentLoggedInUserUpn = "test.admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache
        private const string AllSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";

        public SchoolYearServiceTests()
        {
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearService>>();
            _mockTeamRepository = new Mock<ITeamRepository>(); // Inicjalizacja mocka
            _mockMemoryCache = new Mock<IMemoryCache>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);

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
                _mockTeamRepository.Object, // Przekazanie mocka
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

        // Przykładowy test dla DeleteSchoolYearAsync uwzględniający zmiany w Team.IsActive
        [Fact]
        public async Task DeleteSchoolYearAsync_WhenYearHasActiveTeams_ShouldReturnFalseAndLogOperation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-with-active-teams";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "Test Year", IsActive = true, IsCurrent = false };
            var activeTeam = new Team { Id = "team1", SchoolYearId = schoolYearId, Status = TeamStatus.Active }; // Team.IsActive będzie true

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(schoolYearToDelete);
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
            _capturedOperationHistory.ErrorMessage.Should().Contain("jest nadal używany przez");
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never); // Nie powinno dojść do Update
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_WhenYearIsCurrent_ShouldReturnFalse()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-current-to-delete";
            var currentSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Current Test Year", IsActive = true, IsCurrent = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(currentSchoolYear);

            // Działanie
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            // Asercja
            result.Should().BeFalse();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("Nie można usunąć (dezaktywować) bieżącego roku szkolnego");
        }


        [Fact]
        public async Task DeleteSchoolYearAsync_WhenNoActiveTeamsAndNotCurrent_ShouldDeactivateAndLogOperation()
        {
            // Przygotowanie
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete-ok";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "Old Year", IsActive = true, IsCurrent = false, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-10) };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(schoolYearToDelete);
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
            _capturedOperationHistory.TargetEntityId.Should().Be(schoolYearId);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolYearDeleted);

            // Weryfikacja, że rok szkolny został oznaczony jako nieaktywny
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy =>
                sy.Id == schoolYearId &&
                !sy.IsActive && // Flaga IsActive z BaseEntity
                sy.ModifiedBy == _currentLoggedInUserUpn
            )), Times.Once);
        }


        // --- Pozostałe testy z oryginalnego pliku SchoolYearServiceTests.cs ---
        // Zakładam, że metody takie jak GetSchoolYearByIdAsync, GetAllActiveSchoolYearsAsync,
        // GetCurrentSchoolYearAsync, CreateSchoolYearAsync, UpdateSchoolYearAsync, SetCurrentSchoolYearAsync
        // i RefreshCacheAsync oraz ich testy nie są bezpośrednio dotknięte zmianą
        // logiki Team.IsActive, więc ich kod pozostaje taki sam jak w dostarczonym pliku.
        // Poniżej przykładowe, które nie wymagają zmian.

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
        // ... (reszta testów z SchoolYearServiceTests.cs, które nie są bezpośrednio związane z DeleteSchoolYearAsync i TeamRepository)
    }
}