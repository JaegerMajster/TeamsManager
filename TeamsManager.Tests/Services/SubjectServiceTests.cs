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
        private readonly Mock<IGenericRepository<Subject>> _mockSubjectRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IGenericRepository<UserSubject>> _mockUserSubjectRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SubjectService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache; // Dodano

        private readonly SubjectService _subjectService;
        private readonly string _currentLoggedInUserUpn = "test.moderator@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";


        public SubjectServiceTests()
        {
            _mockSubjectRepository = new Mock<IGenericRepository<Subject>>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SubjectService>>();
            _mockMemoryCache = new Mock<IMemoryCache>(); // Inicjalizacja

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

            _subjectService = new SubjectService(
                _mockSubjectRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockUserSubjectRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object // Przekazanie mocka
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
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, (Subject?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never); // Bo DefaultSchoolTypeId jest null
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_InCache_ShouldReturnFromCache()
        {
            var subjectId = "subj-cached";
            var cachedSubject = new Subject { Id = subjectId, Name = "Cached Subject" };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, cachedSubject, true);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().BeEquivalentTo(cachedSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            var subjectId = "subj-force";
            var cachedSubject = new Subject { Id = subjectId, Name = "Old Subject" };
            var dbSubject = new Subject { Id = subjectId, Name = "New Subject from DB" };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            SetupCacheTryGetValue(cacheKey, cachedSubject, true);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(dbSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdAsync(subjectId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_WithDefaultSchoolType_ShouldLoadItIfNotInCachedObject()
        {
            var subjectId = "subj-with-st";
            var schoolTypeId = "st-for-subject";
            var subjectInCache = new Subject { Id = subjectId, Name = "Subject With ST", DefaultSchoolTypeId = schoolTypeId, DefaultSchoolType = null }; // SchoolType niezaładowany
            var schoolTypeFromDb = new SchoolType { Id = schoolTypeId, ShortName = "LO" };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;

            SetupCacheTryGetValue(cacheKey, subjectInCache, true); // Jest w cache, ale bez SchoolType
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolTypeFromDb);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull();
            result!.DefaultSchoolType.Should().NotBeNull().And.BeEquivalentTo(schoolTypeFromDb);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once); // Powinno dociągnąć
        }


        // --- Testy GetAllActiveSubjectsAsync ---
        [Fact]
        public async Task GetAllActiveSubjectsAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeSubjects = new List<Subject> { new Subject { Id = "s1", Name = "Fizyka" } };
            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>())).ReturnsAsync(activeSubjects);

            var result = await _subjectService.GetAllActiveSubjectsAsync();

            result.Should().BeEquivalentTo(activeSubjects);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSubjectsCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSubjectsAsync_InCache_ShouldReturnFromCache()
        {
            var cachedSubjects = new List<Subject> { new Subject { Id = "s-cached", Name = "Cached Fizyka" } };
            SetupCacheTryGetValue(AllSubjectsCacheKey, cachedSubjects, true);

            var result = await _subjectService.GetAllActiveSubjectsAsync();

            result.Should().BeEquivalentTo(cachedSubjects);
            _mockSubjectRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()), Times.Never);
        }

        // --- Testy GetTeachersForSubjectAsync ---
        [Fact]
        public async Task GetTeachersForSubjectAsync_NotInCache_ShouldReturnAndCacheWithShortDuration()
        {
            var subjectId = "subj-teachers";
            var teachers = new List<User> { new User { Id = "t1", FirstName = "Nauczyciel" } };
            var subject = new Subject { Id = subjectId, IsActive = true };
            var assignments = new List<UserSubject> { new UserSubject { SubjectId = subjectId, UserId = "t1", User = teachers.First(), IsActive = true } };
            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            SetupCacheTryGetValue(cacheKey, (IEnumerable<User>?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subject);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(assignments);

            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            result.Should().BeEquivalentTo(teachers);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
            // Tutaj można by bardziej szczegółowo sprawdzić opcje cache'owania, ale to skomplikowane
        }

        // --- Testy inwalidacji ---
        [Fact]
        public async Task CreateSubjectAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>())).ReturnsAsync(new List<Subject>());
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

            await _subjectService.CreateSubjectAsync("Nowy Przedmiot");

            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task UpdateSubjectAsync_ShouldInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-update-cache";
            var existingSubject = new Subject { Id = subjectId, Name = "Stary" };
            var updatedSubject = new Subject { Id = subjectId, Name = "Nowy" };
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(existingSubject);

            await _subjectService.UpdateSubjectAsync(updatedSubject);

            _mockMemoryCache.Verify(m => m.Remove(SubjectByIdCacheKeyPrefix + subjectId), Times.AtLeastOnce);
            // Token powinien też unieważnić AllSubjectsCacheKey i potencjalnie TeachersForSubjectCacheKeyPrefix + subjectId
        }

        [Fact]
        public async Task DeleteSubjectAsync_ShouldInvalidateSubjectAndTeachersCache()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-delete-cache";
            var subjectToDelete = new Subject { Id = subjectId, Name = "Do Usunięcia", IsActive = true };
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(new List<UserSubject>());


            await _subjectService.DeleteSubjectAsync(subjectId);

            _mockMemoryCache.Verify(m => m.Remove(SubjectByIdCacheKeyPrefix + subjectId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(TeachersForSubjectCacheKeyPrefix + subjectId), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _subjectService.RefreshCacheAsync();

            SetupCacheTryGetValue(AllSubjectsCacheKey, (IEnumerable<Subject>?)null, false);
            _mockSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Subject, bool>>>()))
                                   .ReturnsAsync(new List<Subject>())
                                   .Verifiable();

            await _subjectService.GetAllActiveSubjectsAsync();
            _mockSubjectRepository.Verify();
        }

        // Istniejące testy (sprawdź, czy nadal przechodzą)
        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_OriginalTest_ShouldReturnSubject()
        {
            ResetCapturedOperationHistory();
            var subjectId = "subj-math-001-orig";
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true };
            SetupCacheTryGetValue(SubjectByIdCacheKeyPrefix + subjectId, (Subject?)null, false);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject);
        }
    }
}