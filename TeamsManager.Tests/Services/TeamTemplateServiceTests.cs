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
using TeamsManager.Core.Abstractions.Services.PowerShell;
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
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamTemplateService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<IPowerShellCacheService> _mockPowerShellCacheService;

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
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamTemplateService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockPowerShellCacheService = new Mock<IPowerShellCacheService>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Setup dla SaveChangesAsync
            _mockTeamTemplateRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

            // Setup dla OperationHistoryService
            _mockOperationHistoryService.Setup(s => s.CreateNewOperationEntryAsync(
                    It.IsAny<OperationType>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .Callback<OperationType, string, string?, string?, string?, string?>(
                    (type, entityType, entityId, entityName, details, parentId) =>
                    {
                        _capturedOperationHistory = new OperationHistory 
                        { 
                            Id = "test-operation-id", 
                            Type = type, 
                            Status = OperationStatus.Completed,
                            TargetEntityType = entityType,
                            TargetEntityId = entityId ?? string.Empty,
                            TargetEntityName = entityName ?? string.Empty,
                            OperationDetails = details ?? string.Empty,
                            ParentOperationId = parentId
                        };
                    })
                .ReturnsAsync(new OperationHistory { Id = "test-operation-id" });

            _mockOperationHistoryService.Setup(s => s.UpdateOperationStatusAsync(
                    It.IsAny<string>(),
                    It.IsAny<OperationStatus>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(true);

            // Setup dla NotificationService
            _mockNotificationService.Setup(s => s.SendNotificationToUserAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Setup dla PowerShellCacheService
            _mockPowerShellCacheService.Setup(s => s.GetDefaultCacheEntryOptions())
                .Returns(new MemoryCacheEntryOptions());
            _mockPowerShellCacheService.Setup(s => s.InvalidateAllActiveTeamTemplatesList());
            _mockPowerShellCacheService.Setup(s => s.InvalidateTeamTemplateById(It.IsAny<string>()));
            _mockPowerShellCacheService.Setup(s => s.InvalidateTeamTemplatesBySchoolType(It.IsAny<string>()));
            _mockPowerShellCacheService.Setup(s => s.InvalidateAllCache());

            var mockObject = new object();
            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

            _teamTemplateService = new TeamTemplateService(
                _mockTeamTemplateRepository.Object,
                _mockSchoolTypeRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object,
                _mockPowerShellCacheService.Object
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
            object? outItem = (object?)item;
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

            var result = await _teamTemplateService.CreateTemplateAsync(
                templateName, templateContent, templateDescription, isUniversal, schoolType.Id);

            result.Should().NotBeNull();
            var newTemplateId = result!.Id;

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateCreated),
                    It.IsAny<string>(), // targetEntityType
                    It.IsAny<string>(), // targetEntityId
                    It.Is<string>(targetEntityName => targetEntityName == templateName),
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once);

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(newTemplateId), Times.AtLeastOnce);

            if (!isUniversal && !string.IsNullOrWhiteSpace(schoolType.Id))
            {
                _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(schoolType.Id), Times.AtLeastOnce);
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

            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateUpdated),
                    It.IsAny<string>(), // targetEntityType
                    It.Is<string>(targetEntityId => targetEntityId == templateId),
                    It.Is<string>(targetEntityName => targetEntityName == updatedTemplateData.Name),
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once); // parentOperationId

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(schoolTypeId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.AtLeastOnce);

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

            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateUpdated),
                    It.IsAny<string>(), // targetEntityType
                    It.Is<string>(targetEntityId => targetEntityId == templateId),
                    It.Is<string>(targetEntityName => targetEntityName == updatedTemplateData.Name),
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once); // parentOperationId

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(schoolTypeId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.AtLeastOnce);

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

            var updateResult = await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);
            updateResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateUpdated),
                    It.IsAny<string>(), // targetEntityType
                    It.Is<string>(targetEntityId => targetEntityId == templateId),
                    It.Is<string>(targetEntityName => targetEntityName == updatedTemplateData.Name),
                    It.IsAny<string>(), // details
                    It.IsAny<string>()), Times.Once); // parentOperationId

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.AtLeastOnce);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(oldSchoolTypeId), Times.Once);

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

            var deleteResult = await _teamTemplateService.DeleteTemplateAsync(templateId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateDeleted),
                    It.Is<string>(entityType => entityType == "TeamTemplate"),
                    It.Is<string>(targetEntityId => targetEntityId == templateId),
                    It.Is<string?>(targetEntityName => targetEntityName == null),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()), Times.Once);

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(schoolTypeId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.AtLeastOnce);

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

            var deleteResult = await _teamTemplateService.DeleteTemplateAsync(templateId);
            deleteResult.Should().BeTrue();

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateDeleted),
                    It.Is<string>(entityType => entityType == "TeamTemplate"),
                    It.Is<string>(targetEntityId => targetEntityId == templateId),
                    It.Is<string?>(targetEntityName => targetEntityName == null),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()), Times.Once);

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            // Dla uniwersalnego szablonu nie inwalidujemy cache typu szkoły
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(It.IsAny<string>()), Times.Never);

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

            var cloned = await _teamTemplateService.CloneTemplateAsync(originalId, "Nowy Sklonowany");

            cloned.Should().NotBeNull();
            var clonedId = cloned!.Id;
            var clonedSchoolTypeId = cloned.SchoolTypeId;

            _mockOperationHistoryService.Verify(r => r.CreateNewOperationEntryAsync(
                    It.Is<OperationType>(op => op == OperationType.TeamTemplateCloned),
                    It.Is<string>(entityType => entityType == "TeamTemplate"),
                    It.Is<string>(targetEntityId => targetEntityId == originalId),
                    It.Is<string?>(targetEntityName => targetEntityName == "Klonowanie -> Nowy Sklonowany"),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()), Times.Once);

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(clonedId), Times.Once);

            if (!string.IsNullOrWhiteSpace(clonedSchoolTypeId))
            {
                _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(clonedSchoolTypeId), Times.AtLeastOnce);
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

            // Weryfikacja granularnej inwalidacji PowerShellCacheService
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Once);

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

        [Fact]
        public async Task GenerateTeamNameFromTemplateAsync_Should_Save_Usage_Statistics()
        {
            // Arrange
            var templateId = "test-template-id";
            var values = new Dictionary<string, string> { { "Name", "Test" }, { "Subject", "Math" } };
            var template = new TeamTemplate 
            { 
                Id = templateId, 
                Template = "{Name} - {Subject}",
                IsActive = true,
                UsageCount = 5,
                LastUsedDate = DateTime.UtcNow.AddDays(-1)
            };
            
            _mockTeamTemplateRepository
                .Setup(x => x.GetByIdAsync(templateId))
                .ReturnsAsync(template);

            // Act
            var result = await _teamTemplateService.GenerateTeamNameFromTemplateAsync(templateId, values);

            // Assert
            result.Should().Be("Test - Math");
            _mockTeamTemplateRepository.Verify(x => x.Update(It.IsAny<TeamTemplate>()), Times.Once);
            _mockTeamTemplateRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
            template.UsageCount.Should().Be(6);
            template.LastUsedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task CreateTemplateAsync_Should_Save_To_Database()
        {
            // Arrange
            var name = "New Template";
            var templateContent = "{Name} - {Subject}";
            var description = "Test description";
            var isUniversal = true;

            // Act
            var result = await _teamTemplateService.CreateTemplateAsync(name, templateContent, description, isUniversal);

            // Assert
            result.Should().NotBeNull();
            _mockTeamTemplateRepository.Verify(x => x.AddAsync(It.IsAny<TeamTemplate>()), Times.Once);
            _mockTeamTemplateRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTemplateAsync_Should_Save_To_Database()
        {
            // Arrange
            var templateToUpdate = new TeamTemplate
            {
                Id = "template-1",
                Name = "Updated Template",
                Template = "{Name} - {Subject}",
                Description = "Updated description",
                IsUniversal = true,
                IsActive = true
            };

            var existingTemplate = new TeamTemplate
            {
                Id = "template-1",
                Name = "Original Template",
                Template = "{Name}",
                Description = "Original description",
                IsUniversal = false,
                IsActive = true
            };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync("template-1"))
                                     .ReturnsAsync(existingTemplate);

            // Act
            var result = await _teamTemplateService.UpdateTemplateAsync(templateToUpdate);

            // Assert
            result.Should().BeTrue();
            _mockTeamTemplateRepository.Verify(x => x.Update(It.IsAny<TeamTemplate>()), Times.Once);
            _mockTeamTemplateRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteTemplateAsync_Should_Save_To_Database()
        {
            // Arrange
            var templateId = "template-to-delete";
            var template = new TeamTemplate
            {
                Id = templateId,
                Name = "Template to Delete",
                IsActive = true
            };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId))
                                     .ReturnsAsync(template);

            // Act
            var result = await _teamTemplateService.DeleteTemplateAsync(templateId);

            // Assert
            result.Should().BeTrue();
            _mockTeamTemplateRepository.Verify(x => x.Update(It.IsAny<TeamTemplate>()), Times.Once);
            _mockTeamTemplateRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
            template.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task CloneTemplateAsync_Should_Save_To_Database()
        {
            // Arrange
            var originalTemplateId = "original-template";
            var newTemplateName = "Cloned Template";
            var originalTemplate = new TeamTemplate
            {
                Id = originalTemplateId,
                Name = "Original Template",
                Template = "{Name} - {Subject}",
                Description = "Original description",
                IsUniversal = true,
                IsActive = true
            };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(originalTemplateId))
                                     .ReturnsAsync(originalTemplate);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TeamTemplate, bool>>>()))
                                     .ReturnsAsync(new List<TeamTemplate>());

            // Act
            var result = await _teamTemplateService.CloneTemplateAsync(originalTemplateId, newTemplateName);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be(newTemplateName);
            _mockTeamTemplateRepository.Verify(x => x.AddAsync(It.IsAny<TeamTemplate>()), Times.Once);
            _mockTeamTemplateRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GenerateTeamNameFromTemplateAsync_Should_InvalidateOnlySpecificTemplate()
        {
            // Arrange
            var templateId = "test-template-granular";
            var template = new TeamTemplate 
            { 
                Id = templateId, 
                Template = "{Name}",
                IsActive = true,
                UsageCount = 0,
                LastUsedDate = null
            };
            
            _mockTeamTemplateRepository
                .Setup(x => x.GetByIdAsync(templateId))
                .ReturnsAsync(template);

            // Act
            var result = await _teamTemplateService.GenerateTeamNameFromTemplateAsync(templateId, new Dictionary<string, string> { { "Name", "Test" } });

            // Assert - tylko konkretny szablon, nie listy
            result.Should().Be("Test");
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplateById(templateId), Times.Once);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllActiveTeamTemplatesList(), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateTeamTemplatesBySchoolType(It.IsAny<string>()), Times.Never);
            _mockPowerShellCacheService.Verify(s => s.InvalidateAllCache(), Times.Never);
        }
    }
}