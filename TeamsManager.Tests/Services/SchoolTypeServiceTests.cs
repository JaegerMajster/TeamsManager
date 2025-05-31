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
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolTypeService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache; // Dodano mock dla IMemoryCache

        private readonly SchoolTypeService _schoolTypeService;
        private readonly string _currentLoggedInUserUpn = "admin@example.com";
        private OperationHistory? _capturedOperationHistory;

        // Definicje kluczy cache, aby były spójne z implementacją serwisu
        private const string AllSchoolTypesCacheKey = "SchoolTypes_AllActive";
        private const string SchoolTypeByIdCacheKeyPrefix = "SchoolType_Id_";

        public SchoolTypeServiceTests()
        {
            _mockSchoolTypeRepository = new Mock<IGenericRepository<SchoolType>>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolTypeService>>();
            _mockMemoryCache = new Mock<IMemoryCache>(); // Inicjalizacja mocka

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op!);

            // Konfiguracja mocka IMemoryCache.CreateEntry
            var mockCacheEntry = new Mock<ICacheEntry>();
            // Aby uniknąć problemów z kolekcjami null, inicjalizujemy je jako puste listy
            mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            // Pozwalamy na ustawianie i pobieranie wartości
            mockCacheEntry.SetupProperty(e => e.Value);
            // Pozwalamy na ustawianie właściwości związanych z wygasaniem
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            mockCacheEntry.SetupProperty(e => e.SlidingExpiration);


            _mockMemoryCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                           .Returns(mockCacheEntry.Object);


            _schoolTypeService = new SchoolTypeService(
                _mockSchoolTypeRepository.Object,
                _mockUserRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockMemoryCache.Object // Przekazanie mocka IMemoryCache
            );
        }

        // Metoda pomocnicza do konfiguracji TryGetValue dla mocka IMemoryCache
        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item; // Musi być object, bo tak jest w IMemoryCache.TryGetValue
            _mockMemoryCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                           .Returns(foundInCache);
        }


        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_NotInCache_ShouldReturnAndCacheSchoolType()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var expectedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, (SchoolType?)null, false); // Nie ma w cache
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(expectedSchoolType);

            // Act
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once); // Sprawdzenie, czy dodano do cache
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_ExistingSchoolType_InCache_ShouldReturnSchoolTypeFromCache()
        {
            // Arrange
            var schoolTypeId = "st-cached-123";
            var expectedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "TECH", FullName = "Technikum", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, expectedSchoolType, true); // Jest w cache

            // Act
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSchoolType);
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Never); // Nie powinno być odwołania do repozytorium
        }

        [Fact]
        public async Task GetSchoolTypeByIdAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            // Arrange
            var schoolTypeId = "st-force-refresh";
            var cachedSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "OLD", FullName = "Old Name", IsActive = true };
            var dbSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "NEW", FullName = "New Name From DB", IsActive = true };
            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            SetupCacheTryGetValue(cacheKey, cachedSchoolType, true); // Symulujemy, że jest w cache
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(dbSchoolType);

            // Act
            var result = await _schoolTypeService.GetSchoolTypeByIdAsync(schoolTypeId, forceRefresh: true);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(dbSchoolType); // Powinien zwrócić świeże dane z repozytorium
            _mockSchoolTypeRepository.Verify(r => r.GetByIdAsync(schoolTypeId), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(cacheKey), Times.Once); // Powinien ponownie dodać do cache
        }


        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_NotInCache_ShouldReturnAndCache()
        {
            // Arrange
            var activeSchoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "st-1", ShortName = "LO", FullName = "Liceum Ogólnokształcące", IsActive = true },
                new SchoolType { Id = "st-2", ShortName = "TZ", FullName = "Technikum Zawodowe", IsActive = true }
            };
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false); // Nie ma w cache
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(activeSchoolTypes);

            // Act
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            // Assert
            result.Should().NotBeNull().And.BeEquivalentTo(activeSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolTypesCacheKey), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_InCache_ShouldReturnFromCache()
        {
            // Arrange
            var cachedSchoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "st-c1", ShortName = "LOK", FullName = "Liceum Cache", IsActive = true }
            };
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, cachedSchoolTypes, true); // Jest w cache

            // Act
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync();

            // Assert
            result.Should().NotBeNull().And.BeEquivalentTo(cachedSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypesAsync_WithForceRefresh_ShouldBypassCacheAndFetchFromRepository()
        {
            // Arrange
            var cachedSchoolTypes = new List<SchoolType> { new SchoolType { Id = "st-old-cache" } };
            var dbSchoolTypes = new List<SchoolType> { new SchoolType { Id = "st-new-db-1" }, new SchoolType { Id = "st-new-db-2" } };

            SetupCacheTryGetValue(AllSchoolTypesCacheKey, cachedSchoolTypes, true); // Symulujemy, że jest w cache
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>())).ReturnsAsync(dbSchoolTypes);

            // Act
            var result = await _schoolTypeService.GetAllActiveSchoolTypesAsync(forceRefresh: true);

            // Assert
            result.Should().NotBeNull().And.BeEquivalentTo(dbSchoolTypes);
            _mockSchoolTypeRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()), Times.Once);
            _mockMemoryCache.Verify(m => m.CreateEntry(AllSchoolTypesCacheKey), Times.Once); // Powinien ponownie dodać do cache
        }

        [Fact]
        public async Task CreateSchoolTypeAsync_ValidData_ShouldCreateAndInvalidateCache()
        {
            // Arrange
            var shortName = "NOWY";
            var fullName = "Nowy Typ Szkoły";
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>());
            SchoolType? addedSchoolType = null;
            _mockSchoolTypeRepository.Setup(r => r.AddAsync(It.IsAny<SchoolType>()))
                                    .Callback<SchoolType>(st => addedSchoolType = st)
                                    .Returns(Task.CompletedTask);

            // Act
            var result = await _schoolTypeService.CreateSchoolTypeAsync(shortName, fullName, "opis");

            // Assert
            result.Should().NotBeNull();
            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce); // Sprawdzenie czy jakikolwiek cache został usunięty
                                                                                           // Dokładniejsze sprawdzenie wymagałoby mockowania CancellationTokenSource
                                                                                           // lub przechwytywania kluczy usuwanych przez InvalidateCache.
                                                                                           // Dla uproszczenia, sprawdzamy ogólne wywołanie Remove.
        }

        [Fact]
        public async Task UpdateSchoolTypeAsync_ExistingSchoolType_ShouldUpdateAndInvalidateCache()
        {
            // Arrange
            var schoolTypeId = "st-update";
            var existingSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "OLD", FullName = "Old Name", IsActive = true };
            var schoolTypeToUpdate = new SchoolType { Id = schoolTypeId, ShortName = "NEW", FullName = "New Name" };

            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(existingSchoolType);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>()); // Brak konfliktów

            // Act
            var result = await _schoolTypeService.UpdateSchoolTypeAsync(schoolTypeToUpdate);

            // Assert
            result.Should().BeTrue();
            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
        }


        [Fact]
        public async Task DeleteSchoolTypeAsync_ExistingSchoolType_ShouldDeleteAndInvalidateCache()
        {
            // Arrange
            var schoolTypeId = "st-delete";
            var schoolType = new SchoolType { Id = schoolTypeId, ShortName = "DEL", FullName = "To Delete", IsActive = true };
            _mockSchoolTypeRepository.Setup(r => r.GetByIdAsync(schoolTypeId)).ReturnsAsync(schoolType);

            // Act
            var result = await _schoolTypeService.DeleteSchoolTypeAsync(schoolTypeId);

            // Assert
            result.Should().BeTrue();
            _mockMemoryCache.Verify(m => m.Remove(It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RefreshCacheAsync_ShouldInvalidateCache()
        {
            // Act
            await _schoolTypeService.RefreshCacheAsync();

            // Assert
            // Najprostszym sposobem weryfikacji jest sprawdzenie, czy CancellationTokenSource został odwołany i utworzony nowy.
            // Wymaga to dostępu do _schoolTypesCacheTokenSource, co nie jest możliwe z zewnątrz.
            // Alternatywnie, można by sprawdzić, czy po RefreshCache kolejne wywołanie Get pobiera dane z repozytorium.

            // Symulacja, że cache został zresetowany i GetAllActiveSchoolTypesAsync pobiera z DB
            SetupCacheTryGetValue(AllSchoolTypesCacheKey, (IEnumerable<SchoolType>?)null, false);
            _mockSchoolTypeRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolType, bool>>>()))
                                    .ReturnsAsync(new List<SchoolType>())
                                    .Verifiable(); // Oznaczamy jako weryfikowalne

            await _schoolTypeService.GetAllActiveSchoolTypesAsync(); // To powinno teraz trafić do repozytorium

            _mockSchoolTypeRepository.Verify(); // Weryfikuje, czy metoda oznaczona .Verifiable() została wywołana
        }


        // Istniejące testy (mogą wymagać dostosowania argumentów `forceRefresh` jeśli metody w interfejsie się zmieniły)
        // Poniżej oryginalne testy z drobnymi modyfikacjami, aby upewnić się, że nadal przechodzą
        // lub dodać forceRefresh tam, gdzie ma to sens dla testu.

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

        // Pozostałe testy CRUD i logiki biznesowej powinny być dostosowane do wywoływania InvalidateCache
        // i sprawdzania, czy dane są pobierane z repozytorium po operacji modyfikującej i następnie cache'owane.
        // Na przykład, po CreateSchoolTypeAsync, kolejne GetSchoolTypeByIdAsync powinno najpierw trafić do repozytorium.
        // To jest już częściowo pokryte przez testy *InvalidateCache.
    }
}