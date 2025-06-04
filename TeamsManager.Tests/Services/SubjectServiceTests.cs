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
using TeamsManager.Core.Abstractions.Services.PowerShell;
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
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;
        private readonly Mock<ILogger<SubjectService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly ISubjectService _subjectService;
        private readonly string _currentLoggedInUserUpn = "test.subject.admin@example.com";

        // Klucze cache'u
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectsBySchoolTypeCacheKeyPrefix = "Subjects_BySchoolType_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";

        public SubjectServiceTests()
        {
            _mockSubjectRepository = new Mock<ISubjectRepository>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserSubjectRepository = new Mock<IGenericRepository<UserSubject>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();
            _mockLogger = new Mock<ILogger<SubjectService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();

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
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockPowerShellCacheService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object
            );
        }

        private void AssertCacheInvalidationByReFetchingAllActiveSubjects(List<Subject> expectedDbItemsAfterOperation)
        {
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync())
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _subjectService.GetAllActiveSubjectsAsync().Result;

            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once, "GetAllActiveSubjectsAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockPowerShellCacheService.Verify(m => m.Set(AllSubjectsCacheKey, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }

        private void AssertCacheInvalidationForTeachersList(string subjectId, List<User> expectedTeachers)
        {
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId))
                              .ReturnsAsync(new Subject { Id = subjectId, IsActive = true });
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId))
                                 .ReturnsAsync(expectedTeachers)
                                 .Verifiable();

            var resultAfterInvalidation = _subjectService.GetTeachersForSubjectAsync(subjectId).Result;

            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once, $"GetTeachersForSubjectAsync({subjectId}) powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedTeachers);
            _mockPowerShellCacheService.Verify(m => m.Set(TeachersForSubjectCacheKeyPrefix + subjectId, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.AtLeastOnce);
        }


        // --- Testy GetSubjectByIdAsync ---
        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_NotInCache_ShouldReturnAndCache()
        {
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
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<Subject>(cacheKey, out It.Ref<Subject?>.IsAny))
                .Returns(false);

            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject, options => options.ExcludingMissingMembers());
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }

        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_InCache_ShouldReturnFromCache()
        {
            var subjectId = "subj-cached";
            var cachedSubject = new Subject { Id = subjectId, Name = "Cached Subject", IsActive = true, DefaultSchoolType = new SchoolType { Id = "st-cached", ShortName = "CS" } };
            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<Subject>(cacheKey, out It.Ref<Subject?>.IsAny))
                .Callback(new TryGetValueCallback<Subject>((string key, out Subject? value) =>
                {
                    value = cachedSubject;
                }))
                .Returns(true);

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
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<Subject>(cacheKey, out It.Ref<Subject?>.IsAny))
                .Returns(false);

            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(dbSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbSubject);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
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

            _mockPowerShellCacheService.Setup(m => m.TryGetValue<Subject>(cacheKey, out It.Ref<Subject?>.IsAny))
                .Returns(false);
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
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<IEnumerable<Subject>>(AllSubjectsCacheKey, out It.Ref<IEnumerable<Subject>?> .IsAny))
                .Returns(false);
            _mockSubjectRepository.Setup(r => r.GetAllActiveWithDetailsAsync()).ReturnsAsync(activeSubjects);

            var result = await _subjectService.GetAllActiveSubjectsAsync();

            result.Should().BeEquivalentTo(activeSubjects);
            _mockPowerShellCacheService.Verify(m => m.Set(AllSubjectsCacheKey, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetAllActiveWithDetailsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSubjectsAsync_InCache_ShouldReturnFromCache()
        {
            var cachedSubjects = new List<Subject>
            {
                new Subject { Id = "s-cached", Name = "Cached Fizyka", IsActive = true, DefaultSchoolType = new SchoolType{Id="stc"} }
            };
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<IEnumerable<Subject>>(AllSubjectsCacheKey, out It.Ref<IEnumerable<Subject>?> .IsAny))
                .Callback(new TryGetValueCallback<IEnumerable<Subject>>((string key, out IEnumerable<Subject>? value) =>
                {
                    value = cachedSubjects;
                }))
                .Returns(true);

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

            _mockPowerShellCacheService.Setup(m => m.TryGetValue<IEnumerable<User>>(cacheKey, out It.Ref<IEnumerable<User>?> .IsAny))
                .Returns(false);
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(subject);
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId)).ReturnsAsync(teachers);

            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            result.Should().BeEquivalentTo(teachers);
            _mockPowerShellCacheService.Verify(m => m.Set(cacheKey, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once);
        }

        // --- Testy inwalidacji ---
        [Fact]
        public async Task CreateSubjectAsync_ShouldUseGranularCacheInvalidation()
        {
            // Arrange
            var newSubjectName = "Test Subject";
            var newSubjectCode = "TST001";
            
            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(newSubjectCode))
                .ReturnsAsync((Subject?)null);
            
            // Act
            var result = await _subjectService.CreateSubjectAsync(newSubjectName, code: newSubjectCode);
            
            // Assert - sprawdź że NIE wywołano InvalidateAllCache
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllCache(), 
                Times.Never,
                "CreateSubjectAsync nie powinno resetować całego cache"
            );
            
            // Sprawdź że wywołano granularne metody
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllActiveSubjectsList(), 
                Times.Once
            );
            
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateSubjectById(It.IsAny<string>(), It.IsAny<string>()), 
                Times.Once
            );
        }

        [Fact]
        public async Task CreateSubjectAsync_ShouldInvalidateCache()
        {
            var newSubjectName = "Nowy Przedmiot";
            var newSubjectCode = "NP001";
            var createdSubject = new Subject { Id = "new-subj-id", Name = newSubjectName, Code = newSubjectCode, IsActive = true };

            _mockSubjectRepository.Setup(r => r.GetByCodeAsync(newSubjectCode)).ReturnsAsync((Subject?)null);
            _mockSubjectRepository.Setup(r => r.AddAsync(It.IsAny<Subject>()))
                                 .Callback<Subject>(s => s.Id = createdSubject.Id)
                                 .Returns(Task.CompletedTask);

            var result = await _subjectService.CreateSubjectAsync(newSubjectName, code: newSubjectCode);
            result.Should().NotBeNull();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                It.Is<OperationType>(op => op == OperationType.SubjectCreated),
                It.Is<string>(entityType => entityType == nameof(Subject)),
                It.IsAny<string>(), // targetEntityId
                It.Is<string>(targetEntityName => targetEntityName == newSubjectName),
                It.IsAny<string>(), // details
                It.IsAny<string>()), Times.Once); // parentOperationId

            // Sprawdź użycie granularnej inwalidacji zamiast globalnych Remove
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSubjectsList(), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSubjectById(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSubjectAsync_ShouldInvalidateCache()
        {
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

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.SubjectUpdated),
                    It.Is<string>(entityType => entityType == nameof(Subject)),
                    It.Is<string>(targetEntityId => targetEntityId == subjectId),
                    It.Is<string>(targetEntityName => targetEntityName == updatedSubjectData.Name),
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once); // parentOperationId

            // Sprawdź użycie granularnej inwalidacji zamiast globalnych Remove
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSubjectsList(), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSubjectById(subjectId, It.IsAny<string>()), Times.Once);

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
        }

        [Fact]
        public async Task DeleteSubjectAsync_ShouldInvalidateSubjectAndTeachersCache()
        {
            var subjectId = "subj-delete-cache";
            var subjectToDelete = new Subject { Id = subjectId, Name = "Do Usunięcia", IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockSubjectRepository.Setup(r => r.GetByIdIncludingInactiveAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(new List<UserSubject>());
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));

            var deleteResult = await _subjectService.DeleteSubjectAsync(subjectId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.SubjectDeleted),
                    It.Is<string>(entityType => entityType == nameof(Subject)),
                    It.Is<string>(targetEntityId => targetEntityId == subjectId),
                    It.IsAny<string>(), // targetEntityName - może być null
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once); // parentOperationId

            // Sprawdź użycie granularnej inwalidacji zamiast globalnych Remove
            _mockPowerShellCacheService.Verify(m => m.InvalidateAllActiveSubjectsList(), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.InvalidateSubjectById(subjectId, It.IsAny<string>()), Times.Once);
            _mockPowerShellCacheService.Verify(m => m.InvalidateTeachersForSubject(subjectId), Times.Once);

            AssertCacheInvalidationByReFetchingAllActiveSubjects(new List<Subject>());
            AssertCacheInvalidationForTeachersList(subjectId, new List<User>());
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldTriggerCacheInvalidation()
        {
            await _subjectService.RefreshCacheAsync();

            _mockPowerShellCacheService.Verify(m => m.InvalidateAllCache(), Times.Once);
            // Token jest resetowany, co unieważni wpisy ID i TeachersList

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

            // Act
            await _subjectService.InvalidateTeachersCacheForSubjectAsync(subjectId);

            // Assert - sprawdź wywołanie granularnej metody
            _mockPowerShellCacheService.Verify(m => m.InvalidateTeachersForSubject(subjectId), Times.Once);
        }


        [Fact]
        public async Task GetSubjectByIdAsync_ExistingId_OriginalTest_ShouldReturnSubject()
        {
            var subjectId = "subj-math-001-orig";
            var expectedSubject = new Subject { Id = subjectId, Name = "Matematyka", IsActive = true, DefaultSchoolType = new SchoolType() };
            _mockPowerShellCacheService.Setup(m => m.TryGetValue<Subject>(SubjectByIdCacheKeyPrefix + subjectId, out It.Ref<Subject?>.IsAny))
                .Returns(false);
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(expectedSubject);

            var result = await _subjectService.GetSubjectByIdAsync(subjectId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedSubject);
            _mockPowerShellCacheService.Verify(m => m.Set(SubjectByIdCacheKeyPrefix + subjectId, It.IsAny<object>(), It.IsAny<TimeSpan>()), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
        }

        [Fact]
        public async Task UpdateSubjectAsync_ShouldUseGranularCacheInvalidation()
        {
            // Arrange
            var subjectId = "subj-update-test";
            var existingSubject = new Subject
            {
                Id = subjectId,
                Name = "Original Subject",
                IsActive = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };
            var updatedSubjectData = new Subject
            {
                Id = subjectId,
                Name = "Updated Subject",
                IsActive = true
            };

            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(existingSubject);
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));

            // Act
            var result = await _subjectService.UpdateSubjectAsync(updatedSubjectData);

            // Assert
            result.Should().BeTrue();
            
            // Sprawdź że NIE wywołano InvalidateAllCache
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllCache(), 
                Times.Never,
                "UpdateSubjectAsync nie powinno resetować całego cache"
            );
            
            // Sprawdź że wywołano granularne metody
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllActiveSubjectsList(), 
                Times.Once
            );
            
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateSubjectById(subjectId, It.IsAny<string>()), 
                Times.Once
            );
        }

        [Fact]
        public async Task DeleteSubjectAsync_ShouldUseGranularCacheInvalidation()
        {
            // Arrange
            var subjectId = "subj-delete-test";
            var subjectToDelete = new Subject 
            { 
                Id = subjectId, 
                Name = "Subject To Delete", 
                IsActive = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };
            
            _mockSubjectRepository.Setup(r => r.GetByIdIncludingInactiveAsync(subjectId)).ReturnsAsync(subjectToDelete);
            _mockUserSubjectRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubject, bool>>>())).ReturnsAsync(new List<UserSubject>());
            _mockSubjectRepository.Setup(r => r.Update(It.IsAny<Subject>()));

            // Act
            var result = await _subjectService.DeleteSubjectAsync(subjectId);

            // Assert
            result.Should().BeTrue();
            
            // Sprawdź że NIE wywołano InvalidateAllCache
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllCache(), 
                Times.Never,
                "DeleteSubjectAsync nie powinno resetować całego cache"
            );
            
            // Sprawdź że wywołano granularne metody z invalidateTeachersList
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateAllActiveSubjectsList(), 
                Times.Once
            );
            
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateSubjectById(subjectId, It.IsAny<string>()), 
                Times.Once
            );
            
            _mockPowerShellCacheService.Verify(
                m => m.InvalidateTeachersForSubject(subjectId), 
                Times.Once
            );
        }

        [Fact]
        public async Task GetTeachersForSubjectAsync_ShouldUseGetByIdWithDetailsAsync()
        {
            // Arrange
            var subjectId = "subj-teachers-validation";
            var subject = new Subject { Id = subjectId, IsActive = true };
            var teachers = new List<User> { new User { Id = "t1", FirstName = "TestTeacher" } };
            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            _mockPowerShellCacheService.Setup(m => m.TryGetValue<IEnumerable<User>>(cacheKey, out It.Ref<IEnumerable<User>?> .IsAny))
                .Returns(false);
            _mockSubjectRepository.Setup(r => r.GetByIdWithDetailsAsync(subjectId)).ReturnsAsync(subject);
            _mockSubjectRepository.Setup(r => r.GetTeachersAsync(subjectId)).ReturnsAsync(teachers);

            // Act
            var result = await _subjectService.GetTeachersForSubjectAsync(subjectId);

            // Assert
            result.Should().BeEquivalentTo(teachers);
            
            // Sprawdź że używa GetByIdWithDetailsAsync do walidacji przedmiotu
            _mockSubjectRepository.Verify(r => r.GetByIdWithDetailsAsync(subjectId), Times.Once);
            _mockSubjectRepository.Verify(r => r.GetTeachersAsync(subjectId), Times.Once);
        }

        private delegate void TryGetValueCallback<TItem>(string key, out TItem? value);
    }
}