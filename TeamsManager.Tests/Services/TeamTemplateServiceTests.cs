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
using TeamsManager.Core.Services;
using Xunit;

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
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            var mockCacheEntry = new Mock<ICacheEntry>();
            var expirationTokens = new List<IChangeToken>(); // Potrzebne dla .ExpirationTokens
            var postEvictionCallbacks = new List<PostEvictionCallbackRegistration>(); // Potrzebne dla .PostEvictionCallbacks

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
            var dbTemplate = new TeamTemplate { Id = templateId, Name = "New From DB" };
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
            var activeTemplates = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-all-1", Name = "All Active 1" } };
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
            var universalTemplates = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-uni-1", IsUniversal = true } };
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
            var templatesForSchoolType = new List<TeamTemplate> { new TeamTemplate { Id = "tpl-st-1", SchoolTypeId = schoolTypeId } };
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
            var defaultTemplate = new TeamTemplate { Id = "tpl-default-st", SchoolTypeId = schoolTypeId, IsDefault = true };
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

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolType.Id)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>()))
                .Returns(Task.CompletedTask);

            // Act
            var createdTemplate = await _teamTemplateService.CreateTemplateAsync(
                templateName, templateContent, templateDescription, isUniversal, schoolType.Id);

            // Assert
            createdTemplate.Should().NotBeNull();
            var newTemplateId = createdTemplate!.Id;

            // Poprawiona weryfikacja:
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once,
                "AllTeamTemplatesCacheKey powinien zostać usunięty (isUniversalOrAllAffected=true).");
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once,
                "UniversalTeamTemplatesCacheKey powinien zostać usunięty (isUniversalOrAllAffected=true).");

            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + newTemplateId), Times.Once,
                $"Klucz ID dla nowo utworzonego szablonu ({newTemplateId}) powinien zostać usunięty.");

            if (!isUniversal && !string.IsNullOrWhiteSpace(schoolType.Id))
            {
                _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolType.Id), Times.Once,
                    $"Klucze dla typu szkoły {schoolType.Id} powinny zostać usunięte.");

                // isDefaultPotentiallyAffected jest false, bo nowy szablon nie jest domyślny
                _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolType.Id), Times.Never,
                    "Domyślny szablon dla typu szkoły nie powinien być usuwany, gdy nowy szablon nie jest domyślny.");
            }

            // Dodatkowa weryfikacja, aby upewnić się, że CreateEntry nie było wołane:
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never,
                "CreateEntry nie powinno być wołane przez operację Create, która tylko unieważnia cache.");
        }

        [Fact]
        public async Task UpdateTemplateAsync_NoChangeToDefaultOrUniversalOrSchoolType_ShouldInvalidateSpecificAndPotentiallyGeneral()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-update-simple";
            var schoolTypeId = "st-update-simple";
            var schoolType = CreateTestSchoolType(id: schoolTypeId);
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "old" };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Prosty", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "new" };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType); // Potrzebne dla przypisania SchoolType w serwisie

            await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // W InvalidateCache: isUniversalOrAllAffected = false, isDefaultPotentiallyAffected = false
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            // Zgodnie z obecną logiką serwisu, All i Universal nie są usuwane jeśli isUniversalOrAllAffected=false
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Never, "AllTeamTemplatesCacheKey nie powinien być usunięty, jeśli isUniversalOrAllAffected=false.");
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Never, "UniversalTeamTemplatesCacheKey nie powinien być usunięty, jeśli isUniversalOrAllAffected=false.");
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Never);
        }

        [Fact]
        public async Task UpdateTemplateAsync_SetAsDefault_ShouldInvalidateDefaultAndOthers()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-set-default";
            var schoolTypeId = "st-set-default";
            var schoolType = CreateTestSchoolType(id: schoolTypeId);
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary Niedomyślny", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true, Template = "content" };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Domyślny", SchoolTypeId = schoolTypeId, IsDefault = true, IsUniversal = false, IsActive = true, Template = "content" };

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                   .ReturnsAsync(new List<TeamTemplate>()); // Załóżmy, że nie ma innych domyślnych do odznaczenia

            await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // W InvalidateCache: isUniversalOrAllAffected = false, isDefaultPotentiallyAffected = true
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once, "Domyślny szablon powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Never); // Zgodnie z obecną logiką
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Never); // Zgodnie z obecną logiką
        }

        [Fact]
        public async Task UpdateTemplateAsync_ChangeToUniversal_ShouldInvalidateUniversalAndAll()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-to-universal";
            var existingTemplate = new TeamTemplate { Id = templateId, Name = "Stary Nieuniwersalny", SchoolTypeId = "st-old", IsDefault = false, IsUniversal = false, IsActive = true, Template = "content" };
            var updatedTemplateData = new TeamTemplate { Id = templateId, Name = "Nowy Uniwersalny", SchoolTypeId = null, IsDefault = false, IsUniversal = true, IsActive = true, Template = "content" }; // SchoolTypeId staje się null

            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(existingTemplate);
            // GetByIdAsync dla SchoolType nie będzie potrzebne, jeśli staje się uniwersalny

            await _teamTemplateService.UpdateTemplateAsync(updatedTemplateData);

            // W InvalidateCache: isUniversalOrAllAffected = true, isDefaultPotentiallyAffected = false
            // Dodatkowo będzie drugie wywołanie InvalidateCache dla oldSchoolTypeId
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.AtLeastOnce, "AllTeamTemplatesCacheKey powinien zostać usunięty."); // Co najmniej raz, może być więcej jeśli logika serwisu woła InvalidateCache dla starego schoolTypeId
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.AtLeastOnce, "UniversalTeamTemplatesCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + "st-old"), Times.Once, "Klucz dla starego typu szkoły powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + It.IsAny<string>()), Times.Never); // Nie zmieniamy IsDefault
        }


        [Fact]
        public async Task DeleteTemplateAsync_NotDefaultNotUniversal_ShouldInvalidateSpecifics()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-delete-specific";
            var schoolTypeId = "st-del-specific";
            var templateToDelete = new TeamTemplate { Id = templateId, Name = "Do Usunięcia Specyficzny", SchoolTypeId = schoolTypeId, IsDefault = false, IsUniversal = false, IsActive = true };
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateToDelete);

            await _teamTemplateService.DeleteTemplateAsync(templateId);

            // W InvalidateCache: templateId, schoolTypeId, IsUniversal=false, IsDefault=false
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + schoolTypeId), Times.Never); // Bo IsDefault = false
                                                                                                                                 // Zgodnie z obecną logiką, All i Universal nie są usuwane, jeśli isUniversalOrAllAffected (parametr z InvalidateCache) jest false
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Never);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Never);
        }

        [Fact]
        public async Task DeleteTemplateAsync_IsUniversal_ShouldInvalidateUniversalAndAll()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-delete-universal";
            var templateToDelete = new TeamTemplate { Id = templateId, Name = "Do Usunięcia Uniwersalny", SchoolTypeId = null, IsDefault = false, IsUniversal = true, IsActive = true };
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(templateToDelete);

            await _teamTemplateService.DeleteTemplateAsync(templateId);

            // W InvalidateCache: templateId, schoolTypeId=null, IsUniversal=true, IsDefault=false
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(It.Is<string>(s => s.StartsWith(TeamTemplatesBySchoolTypeIdCacheKeyPrefix))), Times.Never); // Bo SchoolTypeId jest null
            _mockMemoryCache.Verify(m => m.Remove(It.Is<string>(s => s.StartsWith(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix))), Times.Never); // Bo IsDefault = false i SchoolTypeId jest null
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
                IsDefault = false
            };

            // Setup cache dla oryginalnego szablonu - żeby GetTemplateByIdAsync nie wywoływał CreateEntry
            string originalCacheKey = TeamTemplateByIdCacheKeyPrefix + originalId;
            SetupCacheTryGetValue(originalCacheKey, originalTemplate, true); // Znajdzie w cache

            // Setup repository calls
            _mockTeamTemplateRepository.Setup(r => r.AddAsync(It.IsAny<TeamTemplate>())).Returns(Task.CompletedTask);
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                       .ReturnsAsync(new List<TeamTemplate>());

            var cloned = await _teamTemplateService.CloneTemplateAsync(originalId, "Nowy Sklonowany");

            cloned.Should().NotBeNull();
            var clonedId = cloned!.Id;
            var clonedSchoolTypeId = cloned.SchoolTypeId; // Powinien być taki sam jak oryginału

            // Weryfikacja inwalidacji cache - logika InvalidateCache dla klonowania jest taka sama jak dla Create
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once);
            _mockMemoryCache.Verify(m => m.Remove(TeamTemplateByIdCacheKeyPrefix + clonedId), Times.Once);

            if (!string.IsNullOrWhiteSpace(clonedSchoolTypeId))
            {
                _mockMemoryCache.Verify(m => m.Remove(TeamTemplatesBySchoolTypeIdCacheKeyPrefix + clonedSchoolTypeId), Times.Once);
                _mockMemoryCache.Verify(m => m.Remove(DefaultTeamTemplateBySchoolTypeIdCacheKeyPrefix + clonedSchoolTypeId), Times.Never); // Klon nie jest domyślny
            }

            // CreateEntry może być wywołane raz dla pobrania oryginalnego szablonu (jeśli nie był w cache)
            // ale ponieważ setupujemy cache dla oryginalnego szablonu, CreateEntry nie powinno być wywołane dla nowych operacji
            // Sprawdźmy czy CreateEntry było wywołane tylko dla oczekiwanych operacji lub wcale
            _mockMemoryCache.Verify(m => m.CreateEntry(It.IsAny<object>()), Times.Never,
                "CreateEntry nie powinno być wywołane podczas klonowania, ponieważ oryginalny szablon jest w cache");
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateGlobalKeysAndAllowReFetch()
        {
            await _teamTemplateService.RefreshCacheAsync();

            // InvalidateCache(invalidateAll: true) powinno usunąć klucze globalne
            _mockMemoryCache.Verify(m => m.Remove(AllTeamTemplatesCacheKey), Times.Once, "AllTeamTemplatesCacheKey powinien zostać usunięty.");
            _mockMemoryCache.Verify(m => m.Remove(UniversalTeamTemplatesCacheKey), Times.Once, "UniversalTeamTemplatesCacheKey powinien zostać usunięty.");
            // InvalidateCache przy invalidateAll=true nie usuwa specyficznych kluczy ID wg obecnej logiki, polega na tokenie

            // Sprawdzenie ponownego pobrania
            SetupCacheTryGetValue(AllTeamTemplatesCacheKey, (IEnumerable<TeamTemplate>?)null, false); // Cache jest pusty
            var dbTemplates = new List<TeamTemplate> { new TeamTemplate() };
            _mockTeamTemplateRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamTemplate, bool>>>()))
                                   .ReturnsAsync(dbTemplates)
                                   .Verifiable();

            await _teamTemplateService.GetAllActiveTemplatesAsync();
            _mockTeamTemplateRepository.Verify(); // Sprawdza, czy .FindAsync było wywołane
            _mockMemoryCache.Verify(m => m.CreateEntry(AllTeamTemplatesCacheKey), Times.Once); // Powinno dodać do cache
        }

        [Fact]
        public async Task GetTemplateByIdAsync_ExistingActiveTemplate_ShouldReturnTemplate()
        {
            ResetCapturedOperationHistory();
            var templateId = "tpl-exists-orig";
            var expectedTemplate = new TeamTemplate { Id = templateId, Name = "Test" };
            SetupCacheTryGetValue(TeamTemplateByIdCacheKeyPrefix + templateId, (TeamTemplate?)null, false);
            _mockTeamTemplateRepository.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(expectedTemplate);

            var result = await _teamTemplateService.GetTemplateByIdAsync(templateId);

            result.Should().NotBeNull().And.BeEquivalentTo(expectedTemplate);
            _mockMemoryCache.Verify(m => m.CreateEntry(TeamTemplateByIdCacheKeyPrefix + templateId), Times.Once); // Upewniamy się, że jest cachowany
        }
    }
}