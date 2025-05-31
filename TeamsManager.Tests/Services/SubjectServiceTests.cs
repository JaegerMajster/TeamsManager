using System;
using System.Collections.Generic;
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
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class SubjectServiceTests
    {
        // ZMIANA: Typ mocka na ISubjectRepository
        private readonly Mock<ISubjectRepository> _mockSubjectRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SubjectService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly SubjectService _subjectService;
        private readonly string _currentLoggedInUserUpn = "test.moderator@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";


        public SubjectServiceTests()
        {
            // ZMIANA: Inicjalizacja nowego typu mocka
            _mockSubjectRepository = new Mock<ISubjectRepository>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SubjectService>>();
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

            // ZMIANA: Przekazanie _mockSubjectRepository.Object do konstruktora SubjectService
            _subjectService = new SubjectService(
                _mockSubjectRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockUserRepository.Object,
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

        // --- Testy GetSubjectByIdAsync ---
        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_NotInCache_ShouldReturnAndCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-math-001";
            var expectedSchoolType = new SchoolType { Id = "st-1", ShortName = "LO" };
            var expectedSubject = new Subject
            {
                Id = subjectId,
                Name = "Matematyka",
                IsActive = true,
                DefaultSchoolTypeId = "st-1",
                DefaultSchoolType = expectedSchoolType
            };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, (Subject?)null, false);

            // ZMIANA: Użycie GetByIdWithDetailsAsync
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject, options => options.ExcludingMissingMembers());
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_InCache_ShouldReturnFromCache()
        {
            var subjectId = "subj-cached";
            var cachedSubject = new Subject { Id = subjectId, Name = "Cached Subject", IsActive = true, DefaultSchoolType = new SchoolType { Id = "st-cached", ShortName = "CS" } };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, cachedSubject, true);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().BeEquivalentTo(cachedSubject);
            // ZMIANA: Weryfikacja, że GetByIdWithDetailsAsync nie jest wołane
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            var subjectId = "subj-force";
            var cachedSubject = new Subject { Id = subjectId, Name = "Old Subject" };
            var dbSubject = new Subject { Id = subjectId, Name = "New Subject from DB", IsActive = true, DefaultSchoolType = new SchoolType() };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, cachedSubject, true);

            // ZMIANA: Użycie GetByIdWithDetailsAsync
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(dbSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_WithDefaultSchoolType_LogicMovedToRepository_ServicePassesThrough()
        {
            var subjectId = "subj-with-st";
            var schoolTypeId = "st-for-subject";
            // Symulujemy, że repozytorium ZAWSZE zwraca obiekt z załadowanym DefaultSchoolType
            var schoolTypeFromDb = new SchoolType { Id = schoolTypeId, ShortName = "LO" };
            var subjectFromRepo = new Subject
            {
                Id = subjectId,
                Name = "Subject With ST",
                IsActive = true,
                DefaultSchoolTypeId = schoolTypeId,
                DefaultSchoolType = schoolTypeFromDb
            };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;

            SetupCacheTryGetValue(cacheKey, (Subject?)null, false); // Nie ma w cache
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(subjectFromRepo);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull();
            result!.DefaultSchoolType.Should().NotBeNull().And.BeEquivalentTo(schoolTypeFromDb);
            // Nie ma już potrzeby weryfikowania _mockSchoolTypeRepository.GetByIdAsync,
            // bo ta logika jest wewnątrz SubjectRepository, którego tu mockujemy.
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }


        // --- Testy GetAllActiveSubjectsAsync ---
        [Fact]
        public async Task GetAllActiveSubjectsAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeSubjects = new List<Subject> {
                new Subject { Id = "s1", Name = "Fizyka", IsActive = true, DefaultSchoolType = new SchoolType{Id="st1"} }
            };
            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            // ZMIANA: Użycie GetAllActiveWithDetailsAsync
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync()).ReturnsAsync(activeSubjects);

            var result = await _subjectService.GetAllActiveSubjectsAsync();

            result.Should().BeEquivalentTo(activeSubjects);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSubjectsCacheKey), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSubjectsAsync_InCache_ShouldReturnFromCache()
        {
            var cachedSubjects = new List<Subject> {
                new Subject { Id = "s-cached", Name = "Cached Fizyka", IsActive = true, DefaultSchoolType = new SchoolType{Id="stc"} }
            };
            SetupCacheTryGetValue(AllSubjectsCacheKey, cachedSubjects, true);

            var result = await _subjectService.GetAllActiveSubjectsAsync();

            result.Should().BeEquivalentTo(cachedSubjects);
            // ZMIANA: Weryfikacja GetAllActiveWithDetailsAsync
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Never);
        }

        // --- Testy GetTeachersForSubjectAsync ---
        [Fact]
        public async Task GetTeachersForSubjectAsync_NotInCache_ShouldReturnAndCacheWithShortDuration()
        {
            var subjectId = "subj-teachers";
            var teachers = new List<User> { new User { Id = "t1", FirstName = "Nauczyciel" } };
            // Symulujemy, że przedmiot istnieje i jest aktywny (serwis to sprawdzi)
            var subject = new Subject { Id = subjectId, IsActive = true };
            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            // ZMIANA: Serwis najpierw sprawdzi, czy przedmiot istnieje i jest aktywny
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(subject);
            // ZMIANA: Główny mock na GetTeachersAsync
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId)).ReturnsAsync(teachers);
            // Mock dla _mockUserSubjectRepository.FindAsync nie jest już potrzebny tutaj

            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            result.Should().BeEquivalentTo(teachers);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once); // Weryfikacja sprawdzenia przedmiotu
            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once); // Weryfikacja pobrania nauczycieli
        }

        // --- Testy inwalidacji ---
        [Fact]
        public async Task CreateSubjectAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var newSubjectName = "Nowy Przedmiot";
            var newSubjectCode = "NP001";
            // ZMIANA: mock dla GetByCodeAsync
            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(newSubjectCode)).ReturnsAsync((Subject?)null);
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            await _subjectService.CreateSubjectAsync(newSubjectName, code: newSubjectCode);

            // _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
            // Lepsza weryfikacja z użyciem konkretnych kluczy lub tokenu
            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);
            // Dodatkowo, token powinien być zresetowany, co można przetestować w RefreshCacheAsync_ShouldTriggerCacheInvalidation
        }

        [Fact]
        public async Task UpdateSubjectAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-update-cache";
            var schoolTypeForSubject = new SchoolType { Id = "st-default", ShortName = "LO" };
            var existingSubject = new Subject
            {
                Id = subjectId,
                Name = "Stary Przedmiot",
                Code = "OLD01",
                IsActive = true,
                DefaultSchoolTypeId = schoolTypeForSubject.Id,
                DefaultSchoolType = schoolTypeForSubject,
                Category = "Stara Kategoria"
            };
            var updatedSubjectData = new Subject
            { // Dane do aktualizacji
                Id = subjectId,
                Name = "Nowy Przedmiot",
                Code = "NEW01",
                IsActive = true,
                Description = "Nowy opis",
                DefaultSchoolTypeId = schoolTypeForSubject.Id, // Załóżmy, że typ szkoły się nie zmienia
                Category = "Nowa Kategoria"
            };

            // Mock dla pobrania istniejącego przedmiotu
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(existingSubject);
            // Mock dla sprawdzenia unikalności nowego kodu (zakładamy brak konfliktu)
            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(updatedSubjectData.Code)).ReturnsAsync((Subject?)null);
            // Mock dla metody Update repozytorium
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));
            // Jeśli DefaultSchoolTypeId jest aktualizowany i nie jest nullem, serwis wywoła _schoolTypeRepository.GetByIdAsync
            if (!string.IsNullOrEmpty(updatedSubjectData.DefaultSchoolTypeId))
            {
                _mockSchoolTypeRepository.Setup(sr => sr.GetByIdAsync(updatedSubjectData.DefaultSchoolTypeId)).ReturnsAsync(schoolTypeForSubject);
            }

            // Act
            await _subjectService.UpdateSubjectAsync(updatedSubjectData);

            // Assert - Weryfikacja unieważnienia cache
            // 1. Cache dla konkretnego przedmiotu po ID powinien być usunięty.
            _mockMemoryCache.Verify(m => m.Remove(SubjectByIdCacheKeyPrefix + subjectId), Times.AtLeastOnce);

            // 2. Cache dla listy wszystkich aktywnych przedmiotów powinien być usunięty,
            //    ponieważ aktualizacja (np. zmiana nazwy, statusu IsActive) wpływa na tę listę.
            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);

            // 3. Cache dla listy nauczycieli tego przedmiotu NIE powinien być tutaj unieważniany,
            //    ponieważ UpdateSubjectAsync nie modyfikuje przypisań nauczycieli.
            //    Dlatego poniższa linia została USUNIĘTA:
            // _mockMemoryCache.Verify(m => m.Remove(TeachersForSubjectCacheKeyPrefix + subjectId), Times.AtLeastOnce); 

            // 4. Jeśli kategoria się zmieniła, cache dla starej i nowej kategorii powinien być unieważniony
            //    przez zresetowanie tokenu. Możemy też sprawdzić jawne usunięcie, jeśli InvalidateCache by to robiło.
            //    Obecna implementacja InvalidateCache resetuje token, co obejmuje wszystkie kategorie.
            //    Jeśli chcemy być bardziej precyzyjni, można dodać:
            // _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + existingSubject.Category), Times.AtLeastOnce); // Dla starej kategorii, jeśli była
            // _mockMemoryCache.Verify(m => m.Remove(SettingsByCategoryCacheKeyPrefix + updatedSubjectData.Category), Times.AtLeastOnce); // Dla nowej kategorii

            // Weryfikacja, że metoda Update na repozytorium została wywołana
            _mockSubjectRepository.Verify(r => r.Update(It.Is<Subject>(s => s.Id == subjectId && s.Name == updatedSubjectData.Name)), Times.Once);
        }

        [Fact]
        public async Task DeleteSubjectAsync_ShouldInvalidateSubjectAndTeachersCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-delete-cache";
            var subjectToDelete = new Subject { Id = subjectId, Name = "Do Usunięcia", IsActive = true };
            // ZMIANA: GetByIdAsync pochodzi teraz z ISubjectRepository (przez IGenericRepository)
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(new List<UserSubject>());
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));


            await _subjectService.DeleteSubjectAsync(subjectId);

            _mockMemoryCache.Verify(m => m.Remove(SubjectByIdCacheKeyPrefix + subjectId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(TeachersForSubjectCacheKeyPrefix + subjectId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _subjectService.RefreshCacheAsync();

            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            // ZMIANA: Weryfikacja GetAllActiveWithDetailsAsync
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync())
                                   .ReturnsAsync(new List<Subject>())
                                   .Verifiable();

            await _subjectService.GetAllActiveSubjectsAsync();
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once);
        }

        // Istniejące testy (sprawdź, czy nadal przechodzą)
        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_OriginalTest_ShouldReturnSubject()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-math-001-orig";
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true, DefaultSchoolType = new SchoolType() };
            SetupCacheTryGetValue(SubjectByIdCacheKeyPrefix + subjectId, (Subject?)null, false);
            // ZMIANA: Użycie GetByIdWithDetailsAsync
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }
    }
}