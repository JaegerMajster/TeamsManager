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
    public class SchoolTypeServiceTests
    {
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolTypeService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly ISchoolTypeService _schoolTypeService;
        private readonly string _currentLoggedInUserUpn = "test.schooltype.admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        private const string AllSchoolTypesCacheKey = "SchoolTypes_AllActive";
        private const string SchoolTypeByIdCacheKeyPrefix = "SchoolType_Id_";

        public SchoolTypeServiceTests()
        {
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolTypeService>>();
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

            _schoolTypeService = new SchoolTypeService(
                _mockSchoolTypeRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object
            );
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        private void AssertCacheInvalidationByReFetchingAll(List<SchoolType> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                   .ReturnsAsync(expectedDbItemsAfterOperation)
                                   .Verifiable();

            var resultAfterInvalidation = _schoolTypeService.GetAllActiveSchoolTypesAsync().Result;

            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.AtLeastOnce, "GetAllActiveSchoolTypesAsync powinno odpytać repozytorium po unieważnieniu cache.");
        }


        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_NotInCache_ShouldReturnAndCacheSchoolType()
        {
            var schoolTypeId = "st-123";
            var expectedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, (SchoolType?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(expectedSchoolType);

            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_InCache_ShouldReturnSchoolTypeFromCache()
        {
            var schoolTypeId = "st-cached-123";
            var expectedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "TECH", FullName = "Technikum", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, expectedSchoolType, true);

            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Never);
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            var schoolTypeId = "st-force-refresh";
            var cachedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "OLD", FullName = "Old Name", IsActive = true };
            var dbSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "NEW", FullName = "New Name From DB", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, cachedSchoolType, true);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(dbSchoolType);

            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId, forceRefresh: true);

            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(dbSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeSchoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "st-1", ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true },
                new SchoolType { Id = "st-2", ShortName = "TZ", FullName = "Technikum Zawodowe", IsActive = true }
            };
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(activeSchoolTypes);

            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            result.Should().NotBeNull().And.BeEquivalentTo(activeSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolTypesCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_InCache_ShouldReturnFromCache()
        {
            var cachedSchoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "st-c1", ShortName = "LOK", FullName = "Liceum Cache", IsActive = true }
            };
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, cachedSchoolTypes, true);

            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            result.Should().NotBeNull().And.BeEquivalentTo(cachedSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            var cachedSchoolTypes = new List<SchoolType> { new SchoolType { Id = "st-old-cache" } };
            var dbSchoolTypes = new List<SchoolType> { new SchoolType { Id = "st-new-db-1" }, new SchoolType { Id = "st-new-db-2" } };

            SetupCacheTryGetValue(AllSchoolTypesCacheKey, cachedSchoolTypes, true);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>())).ReturnsAsync(dbSchoolTypes);

            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync(forceRefresh: true);

            result.Should().NotBeNull().And.BeEquivalentTo(dbSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolTypesCacheKey), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolTypeAsync_ValidData_ShouldCreateAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var shortName = "NOWY";
            var fullName = "Nowy Typ Szkoły";
            var newSchoolType = new SchoolType { Id = "new-id", ShortName = shortName, FullName = fullName, IsActive = true };

            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>());
            _mockSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<SchoolType>()))
                                    .Callback<SchoolType>(st => st.Id = newSchoolType.Id)
                                    .Returns(Task.CompletedTask);

            var result = await _schoolTypeService.CreateSchoolTypeAsync(shortName, fullName, "opis");

            result.Should().NotBeNull();
            result!.Id.Should().Be(newSchoolType.Id);

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                It.Is<OperationType>(op => op == OperationType.SchoolTypeCreated),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(targetEntityName => targetEntityName == $"{shortName} - {fullName}"),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolTypesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SchoolTypeByIdCacheKeyPrefix + result.Id), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingAll(new List<SchoolType> { result });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeCreated);
        }

        [Fact]
        public async Task UpdateSchoolTypeAsync_ExistingSchoolType_ShouldUpdateAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-update";
            var oldShortName = "OLD";
            var existingSchoolType = new SchoolType { Id = schoolTypeId, ShortName = oldShortName, FullName = "Old Name", IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var schoolTypeToUpdate = new SchoolType { Id = schoolTypeId, ShortName = "NEW", FullName = "New Name", IsActive = true };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(existingSchoolType);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<SchoolType, bool>>>(
                ex => ex.Compile().Invoke(new SchoolType { ShortName = "NEW", Id = "other-id", IsActive = true })
            ))).ReturnsAsync(new List<SchoolType>());

            var result = await _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate);

            result.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                It.Is<OperationType>(op => op == OperationType.SchoolTypeUpdated),
                It.IsAny<string>(),
                It.Is<string>(targetEntityId => targetEntityId == schoolTypeId),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolTypesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SchoolTypeByIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);

            var expectedAfterUpdate = new SchoolType { Id = schoolTypeId, ShortName = "NEW", FullName = "New Name", IsActive = true, CreatedBy = existingSchoolType.CreatedBy, CreatedDate = existingSchoolType.CreatedDate };
            AssertCacheInvalidationByReFetchingAll(new List<SchoolType> { expectedAfterUpdate });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeUpdated);
        }


        [Fact]
        public async Task DeleteSchoolTypeAsync_ExistingSchoolType_ShouldDeleteAndInvalidateCache()
        {
            ResetCapturedOperationHistory();
            var schoolTypeId = "st-delete";
            var shortName = "DEL";
            var schoolType = new SchoolType { Id = schoolTypeId, ShortName = shortName, FullName = "To Delete", IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockSchoolTypeRepository.Setup(r => r.Update(It.IsAny<SchoolType>())); // Delete to Update z IsActive=false

            var result = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);

            result.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                It.Is<OperationType>(op => op == OperationType.SchoolTypeDeleted),
                It.IsAny<string>(),
                It.Is<string>(targetEntityId => targetEntityId == schoolTypeId),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolTypesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(SchoolTypeByIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingAll(new List<SchoolType>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.Type.Should().Be(OperationType.SchoolTypeDeleted);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateCache()
        {
            await _schoolTypeService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllSchoolTypesCacheKey), Times.Once);

            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>())
                                    .Verifiable();

            await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_OriginalTest()
        {
            var schoolTypeId = "st-123-orig";
            var expectedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };
            SetupCacheTryGetValue(SchoolTypeByIdCacheKeyPrefix + schoolTypeId, (SchoolType?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(expectedSchoolType);
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);
            result.Should().NotBeNull().And.BeEquivalentTo(expectedSchoolType);
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_NonExistingSchoolType_OriginalTest()
        {
            var schoolTypeId = "non-existing-st-orig";
            SetupCacheTryGetValue(SchoolTypeByIdCacheKeyPrefix + schoolTypeId, (SchoolType?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync((SchoolType?)null);
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_OriginalTest()
        {
            var activeSchoolTypes = new List<SchoolType> { new SchoolType { Id = "st-active-orig", IsActive = true } };
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>())).ReturnsAsync(activeSchoolTypes);
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
            result.Should().NotBeNull().And.BeEquivalentTo(activeSchoolTypes);
        }
    }
}