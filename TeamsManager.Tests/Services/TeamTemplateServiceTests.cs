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
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Xunit;
using TeamsManager.Core.Services;

namespace TeamsManager.Tests.Services
{
    public class TeamTemplateServiceTests
    {
        private readonly Mock<ITeamTemplateRepository> _mockTeamTemplateRepository;
        private readonly Mock<IGenericRepository<SchoolType>> _mockSchoolTypeRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamTemplateService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;

        private readonly ITeamTemplateService _teamTemplateService;
        private readonly string _currentLoggedInUserUpn = "test.admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Klucze cache'u
        private const string AllTeamTemplatesCacheKey = "TeamTemplates_AllActive";
        private const string UniversalTeamTemplatesCacheKey = "TeamTemplates_UniversalActive";
        private const string TeamTemplatesBySchoolTypeIdCacheKeyPrefix = "TeamTemplates_BySchoolType_Id_";
        private const string DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix = "TeamTemplate_Default_BySchoolType_Id_";
        private const string TeamTemplateByIdCacheKeyPrefix = "TeamTemplate_Id_";

        public TeamTemplateServiceTests()
        {
            _mockTeamTemplateRepository = new Mock<ITeamTemplateRepository>();
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamTemplateService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            // Celowo nie mockujemy Update dla OperationHistoryRepository

            var mockCacheEntry = new Mock<ICacheEntry>();
            var expirationTokens = new List<IChangeToken>();
            var postEvictionCallbacks = new List<PostEvictionCallbackRegistration>();

            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(expirationTokens);
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(postEvictionCallbacks);
            mockCacheEntry.SetupProperty(e => e.Value);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);

            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);

            _teamTemplateService = new TeamTemplateService(
                _mockTeamTemplateRepository.Object,
                _mockSchoolTypeRepository.Object,
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

        private SchoolType CreateTestSchoolType(string id = "st-1", string shortName = "LO", bool isActive = true)
        {
            return new SchoolType { Id = id, ShortName = shortName, FullName = "Liceum Ogólnokształcące", IsActive = isActive };
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }

        private void AssertCacheInvalidationByReFetchingAll(List<TeamTemplate> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(AllTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                     .ReturnsAsync(expectedDbItemsAfterOperation)
                                     .Verifiable();

            var resultAfterInvalidation = _teamTemplateService.GetAllActiveTemplatesAsync().Result;

            _mockTeamTemplateRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()), Times.AtLeastOnce, "GetAllActiveTemplatesAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllTeamTemplatesCacheKey), Times.AtLeastOnce, "Dane powinny zostać ponownie zcache'owane po odczycie z repozytorium.");
        }

        private void AssertCacheInvalidationByReFetchingUniversal(List<TeamTemplate> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(UniversalTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetUniversalTemplatesAsync())
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamTemplateService.GetUniversalTemplatesAsync().Result;

            _mockTeamTemplateRepository.Verify(r => r.GetUniversalTemplatesAsync(), Times.Once, "GetUniversalTemplatesAsync powinno odpytać repozytorium po unieważnieniu cache.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);
        }

        private void AssertCacheInvalidationByReFetchingForSchoolType(string schoolTypeId, List<TeamTemplate> expectedDbItemsAfterOperation)
        {
            SetupCacheTryGetValue(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetTemplatesBySchoolTypeAsync(schoolTypeId))
                                 .ReturnsAsync(expectedDbItemsAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamTemplateService.GetTemplatesBySchoolTypeAsync(schoolTypeId).Result;

            _mockTeamTemplateRepository.Verify(r => r.GetTemplatesBySchoolTypeAsync(schoolTypeId), Times.Once, $"GetTemplatesBySchoolTypeAsync({schoolTypeId}) powinno odpytać repozytorium.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemsAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);
        }

        private void AssertCacheInvalidationByReFetchingDefaultForSchoolType(string schoolTypeId, TeamTemplate? expectedDbItemAfterOperation)
        {
            SetupCacheTryGetValue(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId, (TeamTemplate?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId))
                                 .ReturnsAsync(expectedDbItemAfterOperation)
                                 .Verifiable();

            var resultAfterInvalidation = _teamTemplateService.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId).Result;

            _mockTeamTemplateRepository.Verify(r => r.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId), Times.Once, $"GetDefaultTemplateForSchoolTypeAsync({schoolTypeId}) powinno odpytać repozytorium.");
            resultAfterInvalidation.Should().BeEquivalentTo(expectedDbItemAfterOperation);
            _mockMemoryCache.Verify(m => m.CreateEntry(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);
        }


        // --- Testy GetTemplateByIdAsync ---
        [Fact]
        public async Task GetTemplateByIdAsync_ExistingId_NotInCache_ShouldReturnAndCache()
        {
            var templateId = "tpl-1";
            var expectedTemplate = new TeamTemplate { Id = templateId, Name = "Test Template" };
            string cacheKey = TeamTemplateByIdCacheKeyPrefix + templateId;
            SetupCacheTryGetValue(cacheKey, (TeamTemplate?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(expectedTemplate);

            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            result.Should().BeEquivalentTo(expectedTemplate);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        [Fact]
        public async Task GetTemplateByIdAsync_ExistingId_InCache_ShouldReturnFromCache()
        {
            var templateId = "tpl-cached";
            var cachedTemplate = new TeamTemplate { Id = templateId, Name = "Cached Template" };
            string cacheKey = TeamTemplateByIdCacheKeyPrefix + templateId;
            SetupCacheTryGetValue(cacheKey, cachedTemplate, true);

            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            result.Should().BeEquivalentTo(cachedTemplate);
            _mockTeamTemplateRepository.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task GetTemplateByIdAsync_WithForceRefresh_ShouldBypassCacheAndReCache()
        {
            var templateId = "tpl-force";
            var cachedTemplate = new TeamTemplate { Id = templateId, Name = "Old Cached" };
            var dbTemplate = new TeamTemplate { Id = templateId, Name = "New From DB", IsActive = true };
            string cacheKey = TeamTemplateByIdCacheKeyPrefix + templateId;

            SetupCacheTryGetValue(cacheKey, cachedTemplate, true);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(dbTemplate);

            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId, forceRefresh: true);

            result.Should().BeEquivalentTo(dbTemplate);
            _mockTeamTemplateRepository.Verify(r => r.GetByIdAsync(templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }


        // --- Testy GetAllActiveTemplatesAsync ---
        [Fact]
        public async Task GetAllActiveTemplatesAsync_NotInCache_ShouldReturnAndCache()
        {
            var activeTemplates = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-all-1", Name = "All Active 1", IsActive = true } };
            SetupCacheTryGetValue(AllTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>())).ReturnsAsync(activeTemplates);

            var result = await _teamTemplateService.GetAllActiveTemplatesAsync();

            result.Should().BeEquivalentTo(activeTemplates);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllTeamTemplatesCacheKey), Times.Once);
        }

        // --- Testy GetUniversalTemplatesAsync ---
        [Fact]
        public async Task GetUniversalTemplatesAsync_NotInCache_ShouldReturnAndCache()
        {
            var universalTemplates = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-uni-1", IsUniversal = true, IsActive = true } };
            SetupCacheTryGetValue(UniversalTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetUniversalTemplatesAsync()).ReturnsAsync(universalTemplates);

            var result = await _teamTemplateService.GetUniversalTemplatesAsync();

            result.Should().BeEquivalentTo(universalTemplates);
            _mockMemoryCache.Verify(m => m.CreateEntry(UniversalTeamTemplatesCacheKey), Times.Once);
        }

        // --- Testy GetTemplatesBySchoolTypeAsync ---
        [Fact]
        public async Task GetTemplatesBySchoolTypeAsync_NotInCache_ShouldReturnAndCache()
        {
            var schoolTypeId = "st-1";
            var templatesForSchoolType = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-st-1", SchoolTypeId = schoolTypeId, IsActive = true } };
            string cacheKey = TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId;
            SetupCacheTryGetValue(cacheKey, (IEnumerable<TeamTemplate>?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetTemplatesBySchoolTypeAsync(schoolTypeId)).ReturnsAsync(templatesForSchoolType);

            var result = await _teamTemplateService.GetTemplatesBySchoolTypeAsync(schoolTypeId);

            result.Should().BeEquivalentTo(templatesForSchoolType);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy GetDefaultTemplateForSchoolTypeAsync ---
        [Fact]
        public async Task GetDefaultTemplateForSchoolTypeAsync_NotInCache_ShouldReturnAndCache()
        {
            var schoolTypeId = "st-default";
            var defaultTemplate = new TeamTemplate { Id = "tpl-default-st", SchoolTypeId = schoolTypeId, IsDefault = true, IsActive = true };
            string cacheKey = DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId;
            SetupCacheTryGetValue(cacheKey, (TeamTemplate?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId)).ReturnsAsync(defaultTemplate);

            var result = await _teamTemplateService.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId);

            result.Should().BeEquivalentTo(defaultTemplate);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once);
        }

        // --- Testy inwalidacji cache ---
        [Fact]
        public async Task CreateTemplateAsync_ShouldInvalidateRelevantCaches()
        {
            ResetCapturedOperationHistory();
            var schoolType = CreateTestSchoolType(id: "st-1-create", shortName: "LO-C");
            var templateName = "Nowy Szablon Create";
            var templateContent = "Treść szablonu";
            var templateDescription = "Opis nowego szablonu";
            var isUniversal = false;
            var createdTemplate = new TeamTemplate { Id = "new-tpl-id", Name = templateName, SchoolTypeId = schoolType.Id, IsUniversal = isUniversal, IsActive = true, Template = templateContent, IsDefault = false };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolType.Id)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                .Callback<TeamTemplate>(t => t.Id = createdTemplate.Id)
                .Returns(Task.CompletedTask);
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var result = await _teamTemplateService.CreateTemplateAsync(
                templateName, templateContent, templateDescription, isUniversal, schoolType.Id);

            result.Should().NotBeNull();
            var newTemplateId = result!.Id;

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityName == templateName && op.Type == OperationType.TeamTemplateCreated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + newTemplateId), Times.AtLeastOnce);

            if (!isUniversal && !string.IsNullOrWhiteSpace(schoolType.Id))
            {
                _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolType.Id), Times.AtLeastOnce);
                _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolType.Id), Times.AtLeastOnce);
            }

            AssertCacheInvalidationByReFetchingAll(new List<TeamTemplate> { result });
            if (isUniversal) AssertCacheInvalidationByReFetchingUniversal(new List<TeamTemplate> { result });
            else AssertCacheInvalidationByReFetchingForSchoolType(schoolType.Id, new List<TeamTemplate> { result });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateTemplateAsync_NoChangeToDefaultOrUniversalOrSchoolType_ShouldInvalidateSpecificAndPotentiallyGeneral()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-update-simple";
            var schoolTypeId = "st-update-simple";
            var schoolType = CreateTestSchoolType(id: schoolTypeId);
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "old", CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Prosty", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "new" };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.Update(It.IsAny<TeamTemplate>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == templateId && op.Type == OperationType.TeamTemplateUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);

            var expectedAfterUpdate = new TeamTemplate { Id = templateId, Name = "Nowy Prosty", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "new", SchoolType = schoolType, CreatedBy = existingTemplate.CreatedBy, CreatedDate = existingTemplate.CreatedDate };
            AssertCacheInvalidationByReFetchingForSchoolType(schoolTypeId, new List<TeamTemplate> { expectedAfterUpdate });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateTemplateAsync_SetAsDefault_ShouldInvalidateDefaultAndOthers()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-set-default";
            var schoolTypeId = "st-set-default";
            var schoolType = CreateTestSchoolType(id: schoolTypeId);
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary Niedomyślny", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "content", CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Domyślny", SchoolTypeId = schoolTypeId, IsDefault = true, IsUniversal = false, IsActive = true, Template = "content" };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                   .ReturnsAsync(new List<TeamTemplate>());
            _mockTeamTemplateRepository.Setup(r => r.Update(It.IsAny<TeamTemplate>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == templateId && op.Type == OperationType.TeamTemplateUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);

            var expectedAfterUpdate = new TeamTemplate { Id = templateId, Name = "Nowy Domyślny", SchoolTypeId = schoolTypeId, IsDefault = true, IsUniversal = false, IsActive = true, Template = "content", SchoolType = schoolType, CreatedBy = existingTemplate.CreatedBy, CreatedDate = existingTemplate.CreatedDate };
            AssertCacheInvalidationByReFetchingForSchoolType(schoolTypeId, new List<TeamTemplate> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingDefaultForSchoolType(schoolTypeId, expectedAfterUpdate);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task UpdateTemplateAsync_ChangeToUniversal_ShouldInvalidateUniversalAndAll()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-to-universal";
            var oldSchoolTypeId = "st-old";
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary Nieuniwersalny", SchoolTypeId = oldSchoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "content", CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Uniwersalny", SchoolTypeId = null, IsDefault = false, IsUniversal = true, IsActive = true, Template = "content" };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockTeamTemplateRepository.Setup(r => r.Update(It.IsAny<TeamTemplate>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne


            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == templateId && op.Type == OperationType.TeamTemplateUpdated)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + oldSchoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + It.IsAny<string>()), Times.Never);

            var expectedAfterUpdate = new TeamTemplate { Id = templateId, Name = "Nowy Uniwersalny", SchoolTypeId = null, IsDefault = false, IsUniversal = true, IsActive = true, Template = "content", CreatedBy = existingTemplate.CreatedBy, CreatedDate = existingTemplate.CreatedDate };
            AssertCacheInvalidationByReFetchingAll(new List<TeamTemplate> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingUniversal(new List<TeamTemplate> { expectedAfterUpdate });
            AssertCacheInvalidationByReFetchingForSchoolType(oldSchoolTypeId, new List<TeamTemplate>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }


        [Fact]
        public async Task DeleteTemplateAsync_NotDefaultNotUniversal_ShouldInvalidateSpecifics()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-delete-specific";
            var schoolTypeId = "st-del-specific";
            var templateToDelete = new TeamTemplate { Id = templateId, Name = "Do Usunięcia Specyficzny", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateToDelete);
            _mockTeamTemplateRepository.Setup(r => r.Update(It.IsAny<TeamTemplate>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne


            var deleteResult = await _teamTemplateService.DeleteTemplateAsync(templateId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == templateId && op.Type == OperationType.TeamTemplateDeleted)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce);

            AssertCacheInvalidationByReFetchingForSchoolType(schoolTypeId, new List<TeamTemplate>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task DeleteTemplateAsync_IsUniversal_ShouldInvalidateUniversalAndAll()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-delete-universal";
            var templateToDelete = new TeamTemplate { Id = templateId, Name = "Do Usunięcia Uniwersalny", SchoolTypeId = null, IsDefault = false, IsUniversal = true, IsActive = true, CreatedBy = "initial", CreatedDate = DateTime.UtcNow.AddDays(-1) };
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateToDelete);
            _mockTeamTemplateRepository.Setup(r => r.Update(It.IsAny<TeamTemplate>()));
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne

            var deleteResult = await _teamTemplateService.DeleteTemplateAsync(templateId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == templateId && op.Type == OperationType.TeamTemplateDeleted)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(It.Is<string>(s => s.StartsWith(TeamTemplatesBySchoolTypeIdCacheKeyPrefix))), Times.Never);
            _mockMemoryCache.Verify(m => m.Remove(It.Is<string>(s => s.StartsWith(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix))), Times.Never);

            AssertCacheInvalidationByReFetchingAll(new List<TeamTemplate>());
            AssertCacheInvalidationByReFetchingUniversal(new List<TeamTemplate>());

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }


        [Fact]
        public async Task CloneTemplateAsync_ShouldInvalidateRelevantCaches()
        {
            ResetCapturedOperationHistory();
            var originalId = "tpl-original-clone";
            var schoolTypeId = "st-clone";
            var originalTemplate = new TeamTemplate
            {
                Id = originalId,
                Name = "Oryginał",
                SchoolTypeId = schoolTypeId,
                IsUniversal = false,
                Template = "content",
                IsDefault = false,
                IsActive = true,
                CreatedBy = "initial",
                CreatedDate = DateTime.UtcNow.AddDays(-1)
            };

            SetupCacheTryGetValue(TeamTemplateByIdCacheKeyPrefix + originalId, originalTemplate, true);
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                .Callback<TeamTemplate>(t => t.Id = "cloned-id")
                .Returns(Task.CompletedTask);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                       .ReturnsAsync(new List<TeamTemplate>());
            // _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((OperationHistory?)null); // Już niepotrzebne


            var cloned = await _teamTemplateService.CloneTemplateAsync(originalId, "Nowy Sklonowany");

            cloned.Should().NotBeNull();
            var clonedId = cloned!.Id;
            var clonedSchoolTypeId = cloned.SchoolTypeId;

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.Is<OperationHistory>(op => op.TargetEntityId == originalId && op.Type == OperationType.TeamTemplateCloned)), Times.Once);
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);

            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + clonedId), Times.Once);

            if (!string.IsNullOrWhiteSpace(clonedSchoolTypeId))
            {
                _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + clonedSchoolTypeId), Times.AtLeastOnce);
                _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + clonedSchoolTypeId), Times.AtLeastOnce);
            }

            AssertCacheInvalidationByReFetchingAll(new List<TeamTemplate> { cloned });
            if (cloned.IsUniversal) AssertCacheInvalidationByReFetchingUniversal(new List<TeamTemplate> { cloned });
            else if (clonedSchoolTypeId != null) AssertCacheInvalidationByReFetchingForSchoolType(clonedSchoolTypeId, new List<TeamTemplate> { cloned });

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.TeamTemplateCloned);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateGlobalKeysAndAllowReFetch()
        {
            await _teamTemplateService.RefreshCacheAsync();

            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once);

            SetupCacheTryGetValue(AllTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false);
            var dbTemplates = new List<TeamTemplate> { new TeamTemplate { Id = "refreshed", Name = "Refreshed", IsActive = true } };
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                   .ReturnsAsync(dbTemplates)
                                   .Verifiable();

            await _teamTemplateService.GetAllActiveTemplatesAsync();
            _mockTeamTemplateRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllTeamTemplatesCacheKey), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetTemplateByIdAsync_ExistingActiveTemplate_ShouldReturnTemplate()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-exists-orig";
            var expectedTemplate = new TeamTemplate { Id = templateId, Name = "Test", IsActive = true };
            SetupCacheTryGetValue(TeamTemplateByIdCacheKeyPrefix + templateId, (TeamTemplate?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(expectedTemplate);

            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedTemplate);
            _mockMemoryCache.Verify(m => m.CreateEntry(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
        }
    }
}