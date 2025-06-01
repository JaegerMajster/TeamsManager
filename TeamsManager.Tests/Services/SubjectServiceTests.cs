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
            // Celowo nie mockujemy Update dla OperationHistory, aby upewnić się, że nie jest wołane przez SaveOperationHistoryAsync

            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

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

        private void AssertCacheInvalidationByReFetchingAllActiveSubjects(List<Subject> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync())
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _subjectService.GetAllActiveSubjectsAsync().Result;

            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once, "GetAllActiveSubjectsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSubjectsCacheKey), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }

        private void AssertCacheInvalidationForTeachersList(string subjectId, List<User> expectedTeachers)
        {
            SetupCacheTryGetValue(TeachersForSubjectCacheKeyPrefix + subjectId, (IEnumerable<User>?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId))
                              .ReturnsAsync(new Subject { Id = subjectId, IsActive = true });
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId))
                                 .ReturnsAsync(expectedTeachers)
                                 .Verifiable();

            var resultAfterInvalidation = _subjectService.GetTeachersForSubjectAsync(subjectId).Result;

            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once, $"GetTeachersForSubjectAsync({subjectId}) powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedTeachers);
            _mockMemoryCache.Verify(m => m.CreateEntry(TeachersForSubjectCacheKeyPrefix + subjectId), Times.AtLeastOnce);
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

            SetupCacheTryGetValue(cacheKey, (Subject?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(subjectFromRepo);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull();
            result!.DefaultSchoolType.Should().NotBeNull().And.BeEquivalentTo(schoolTypeFromDb);
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
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Never);
        }

        // --- Testy GetTeachersForSubjectAsync ---
        [Fact]
        public async Task GetTeachersForSubjectAsync_NotInCache_ShouldReturnAndCacheWithShortDuration()
        {
            var subjectId = "subj-teachers";
            var teachers = new List<User> { new User { Id = "t1", FirstName = "Nauczyciel" } };
            var subject = new Subject { Id = subjectId, IsActive = true };
            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId)).ReturnsAsync(teachers);

            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            result.Should().BeEquivalentTo(teachers);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once);
        }

        // --- Testy inwalidacji ---
        [Fact]
        public async Task CreateSubjectAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var newSubjectName = "Nowy Przedmiot";
            var newSubjectCode = "NP001";
            var createdSubject = new Subject { Id = "new-subj-id", Name = newSubjectName, Code = newSubjectCode, IsActive = true };

            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(newSubjectCode)).ReturnsAsync((Subject?)null);
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>()))
                                 .Callback<Subject>(s => s.Id = createdSubject.Id)
                                 .Returns(Task.CompletedTask);

            var result = await _subjectService.CreateSubjectAsync(newSubjectName, code: newSubjectCode);
            result.Should().NotBeNull();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityName == newSubjectName && op.Type == OperationType.SubjectCreated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(TeachersForSubjectCacheKeyPrefix + result!.Id), Times.Never);

            AssertCacheInvalidationByReFetchingAllActiveSubjects(new List<Subject> { result });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SubjectCreated);
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
                Category = "Stara Kategoria",
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };
            var updatedSubjectData = new Subject
            {
                Id = subjectId,
                Name = "Nowy Przedmiot",
                Code = "NEW01",
                IsActive = true,
                Description = "Nowy opis",
                DefaultSchoolTypeId = schoolTypeForSubject.Id,
                Category = "Nowa Kategoria"
            };

            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(existingSubject);
            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(updatedSubjectData.Code)).ReturnsAsync((Subject?)null);
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));
            if (!string.IsNullOrEmpty(updatedSubjectData.DefaultSchoolTypeId))
            {
                _mockSchoolTypeRepository.Setup(sr => sr.GetByIdAsync(updatedSubjectData.DefaultSchoolTypeId)).ReturnsAsync(schoolTypeForSubject);
            }

            var updateResult = await _subjectService.UpdateSubjectAsync(updatedSubjectData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == subjectId && op.Type == OperationType.SubjectUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);
            // Klucz TeachersForSubjectCacheKeyPrefix nie jest jawnie usuwany gdy invalidateAll: true - serwis polega na CancellationTokenSource

            var expectedAfterUpdate = new Subject
            {
                Id = subjectId,
                Name = "Nowy Przedmiot",
                Code = "NEW01",
                IsActive = true,
                Description = "Nowy opis",
                DefaultSchoolTypeId = schoolTypeForSubject.Id,
                DefaultSchoolType = schoolTypeForSubject,
                Category = "Nowa Kategoria",
                CreatedBy = existingSubject.CreatedBy,
                CreatedDate = existingSubject.CreatedDate
            };
            AssertCacheInvalidationByReFetchingAllActiveSubjects(new List<Subject> { expectedAfterUpdate });
            AssertCacheInvalidationForTeachersList(subjectId, new List<User>()); // Zakładając, że aktualizacja mogła wpłynąć na nauczycieli (np. zmiana statusu przedmiotu)

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SubjectUpdated);
        }

        [Fact]
        public async Task DeleteSubjectAsync_ShouldInvalidateSubjectAndTeachersCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-delete-cache";
            var subjectToDelete = new Subject { Id = subjectId, Name = "Do Usunięcia", IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(new List<UserSubject>());
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));

            var deleteResult = await _subjectService.DeleteSubjectAsync(subjectId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == subjectId && op.Type == OperationType.SubjectDeleted)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            // Klucz TeachersForSubjectCacheKeyPrefix nie jest jawnie usuwany gdy invalidateAll: true - serwis polega na CancellationTokenSource
            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingAllActiveSubjects(new List<Subject>());
            AssertCacheInvalidationForTeachersList(subjectId, new List<User>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SubjectDeleted);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _subjectService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.Once);
            // Token jest resetowany, co unieważni wpisy ID i TeachersList

            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync())
                                   .ReturnsAsync(new List<Subject>())
                                   .Verifiable();

            await _subjectService.GetAllActiveSubjectsAsync();
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once);
        }

        [Fact]
        public async Task InvalidateTeachersCacheForSubjectAsync_ShouldRemoveSpecificCacheKey()
        {
            // Arrange
            var subjectId = "subject-with-teachers";
            var cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            // Act
            await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectId);

            // Assert
            // Metoda InvalidateCache resetuje token, co jest głównym mechanizmem.
            // Dodatkowo, jeśli subjectId jest podany i invalidateTeachersList jest true (co jest w InvalidateTeachersCacheForSubjectAsync),
            // to klucz TeachersForSubjectCacheKeyPrefix + subjectId jest usuwany.
            _mockMemoryCache.Verify(m => m.Remove(cacheKey), Times.Once);
            // Również AllSubjectsCacheKey jest usuwany przez wywołanie InvalidateCache(subjectId, true)
            _mockMemoryCache.Verify(m => m.Remove(AllSubjectsCacheKey), Times.Once);
        }


        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_OriginalTest_ShouldReturnSubject()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-math-001-orig";
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true, DefaultSchoolType = new SchoolType() };
            SetupCacheTryGetValue(SubjectByIdCacheKeyPrefix + subjectId, (Subject?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }
    }
}