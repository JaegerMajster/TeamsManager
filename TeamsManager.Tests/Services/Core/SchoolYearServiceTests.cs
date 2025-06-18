using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services.Core
{
    public class SchoolYearServiceTests
    {
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolYearService>> _mockLogger;
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<IGraphCacheService> _mockGraphCacheService;
        private readonly SchoolYearService _service;

        public SchoolYearServiceTests()
        {
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearService>>();
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockGraphCacheService = new Mock<IGraphCacheService>();

            // Setup mockowania IMemoryCache - tylko CreateEntry, Set to extension method
            var mockCacheEntry = new Mock<ICacheEntry>();
            mockCacheEntry.SetupAllProperties();
            _mockMemoryCache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            _service = new SchoolYearService(
                _mockSchoolYearRepository.Object,
                _mockOperationHistoryService.Object,
                _mockNotificationService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockTeamRepository.Object,
                _mockMemoryCache.Object,
                _mockGraphCacheService.Object
            );

            // Domyślne setup dla CurrentUserService
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("test@test.com");
        }

        #region GetSchoolYearByIdAsync Tests

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithValidId_ShouldReturnSchoolYear()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var expectedSchoolYear = CreateTestSchoolYear(schoolYearId, "2023/2024");
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(expectedSchoolYear);

            // Act
            var result = await _service.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(schoolYearId);
            result.Name.Should().Be("2023/2024");
            _mockSchoolYearRepository.Verify(x => x.GetByIdAsync(schoolYearId), Times.Once);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithEmptyId_ShouldReturnNull()
        {
            // Act
            var result = await _service.GetSchoolYearByIdAsync("");

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithNonExistentId_ShouldReturnNull()
        {
            // Arrange
            var schoolYearId = "non-existent";
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithInactiveSchoolYear_ShouldReturnNull()
        {
            // Arrange
            var schoolYearId = "sy-inactive";
            var inactiveSchoolYear = CreateTestSchoolYear(schoolYearId, "2022/2023");
            inactiveSchoolYear.IsActive = false;
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(inactiveSchoolYear);

            // Act
            var result = await _service.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_WithCachedData_ShouldReturnFromCache()
        {
            // Arrange
            var schoolYearId = "sy-cached";
            var cachedSchoolYear = CreateTestSchoolYear(schoolYearId, "2023/2024");
            object cachedValue = cachedSchoolYear;
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(true);

            // Act
            var result = await _service.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(schoolYearId);
            _mockSchoolYearRepository.Verify(x => x.GetByIdAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetCurrentSchoolYearAsync Tests

        [Fact]
        public async Task GetCurrentSchoolYearAsync_WhenCurrentExists_ShouldReturnCurrentSchoolYear()
        {
            // Arrange
            var currentSchoolYear = CreateTestSchoolYear("sy-current", "2023/2024");
            currentSchoolYear.IsCurrent = true;
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.GetCurrentSchoolYearAsync())
                .ReturnsAsync(currentSchoolYear);

            // Act
            var result = await _service.GetCurrentSchoolYearAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsCurrent.Should().BeTrue();
            result.Name.Should().Be("2023/2024");
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_WhenNoCurrentSet_ShouldReturnNull()
        {
            // Arrange
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.GetCurrentSchoolYearAsync())
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.GetCurrentSchoolYearAsync();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SetCurrentSchoolYearAsync Tests

        [Fact]
        public async Task SetCurrentSchoolYearAsync_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var schoolYear = CreateTestSchoolYear(schoolYearId, "2023/2024");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearSetAsCurrent, "SchoolYear", schoolYearId, null, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(schoolYear);
            _mockSchoolYearRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear>());

            // Act
            var result = await _service.SetCurrentSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeTrue();
            schoolYear.IsCurrent.Should().BeTrue();
            _mockSchoolYearRepository.Verify(x => x.Update(schoolYear), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var schoolYearId = "non-existent";
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearSetAsCurrent, "SchoolYear", schoolYearId, null, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.SetCurrentSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_WithInactiveSchoolYear_ShouldReturnFalse()
        {
            // Arrange
            var schoolYearId = "sy-inactive";
            var inactiveSchoolYear = CreateTestSchoolYear(schoolYearId, "2022/2023");
            inactiveSchoolYear.IsActive = false;
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearSetAsCurrent, "SchoolYear", schoolYearId, null, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(inactiveSchoolYear);

            // Act
            var result = await _service.SetCurrentSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region CreateSchoolYearAsync Tests

        [Fact]
        public async Task CreateSchoolYearAsync_WithValidData_ShouldReturnCreatedSchoolYear()
        {
            // Arrange
            var name = "2024/2025";
            var startDate = new DateTime(2024, 9, 1);
            var endDate = new DateTime(2025, 6, 30);
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearCreated, "SchoolYear", null, name, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetSchoolYearByNameAsync(name))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.CreateSchoolYearAsync(name, startDate, endDate, "Test description");

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name);
            result.StartDate.Should().Be(startDate.Date);
            result.EndDate.Should().Be(endDate.Date);
            result.IsActive.Should().BeTrue();
            result.IsCurrent.Should().BeFalse();
            _mockSchoolYearRepository.Verify(x => x.AddAsync(It.IsAny<SchoolYear>()), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolYearAsync_WithEmptyName_ShouldReturnNull()
        {
            // Arrange
            var startDate = new DateTime(2024, 9, 1);
            var endDate = new DateTime(2025, 6, 30);
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearCreated, "SchoolYear", null, "", null, null))
                .ReturnsAsync(operation);

            // Act
            var result = await _service.CreateSchoolYearAsync("", startDate, endDate, "Test description");

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(x => x.AddAsync(It.IsAny<SchoolYear>()), Times.Never);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolYearAsync_WithInvalidDates_ShouldReturnNull()
        {
            // Arrange
            var name = "2024/2025";
            var startDate = new DateTime(2025, 6, 30);
            var endDate = new DateTime(2024, 9, 1);
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearCreated, "SchoolYear", null, name, null, null))
                .ReturnsAsync(operation);

            // Act
            var result = await _service.CreateSchoolYearAsync(name, startDate, endDate, "Test description");

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(x => x.AddAsync(It.IsAny<SchoolYear>()), Times.Never);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task CreateSchoolYearAsync_WithDuplicateName_ShouldReturnNull()
        {
            // Arrange
            var name = "2024/2025";
            var startDate = new DateTime(2024, 9, 1);
            var endDate = new DateTime(2025, 6, 30);
            var existingSchoolYear = CreateTestSchoolYear("sy-existing", name);
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearCreated, "SchoolYear", null, name, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetSchoolYearByNameAsync(name))
                .ReturnsAsync(existingSchoolYear);

            // Act
            var result = await _service.CreateSchoolYearAsync(name, startDate, endDate, "Test description");

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(x => x.AddAsync(It.IsAny<SchoolYear>()), Times.Never);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region UpdateSchoolYearAsync Tests

        [Fact]
        public async Task UpdateSchoolYearAsync_WithValidData_ShouldReturnTrue()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var existingSchoolYear = CreateTestSchoolYear(schoolYearId, "2023/2024");
            var updatedSchoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearUpdated, "SchoolYear", schoolYearId, updatedSchoolYear.Name, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYearId))
                .ReturnsAsync(existingSchoolYear);
            _mockSchoolYearRepository.Setup(x => x.GetSchoolYearByNameAsync(updatedSchoolYear.Name))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.UpdateSchoolYearAsync(updatedSchoolYear);

            // Assert
            result.Should().BeTrue();
            _mockSchoolYearRepository.Verify(x => x.Update(It.IsAny<SchoolYear>()), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_WithNullSchoolYear_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateSchoolYearAsync(null));
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var schoolYear = CreateTestSchoolYear("non-existent", "2024/2025");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearUpdated, "SchoolYear", schoolYear.Id, schoolYear.Name, null, null))
                .ReturnsAsync(operation);
            _mockSchoolYearRepository.Setup(x => x.GetByIdAsync(schoolYear.Id))
                .ReturnsAsync((SchoolYear)null);

            // Act
            var result = await _service.UpdateSchoolYearAsync(schoolYear);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region DeleteSchoolYearAsync Tests

        [Fact]
        public async Task DeleteSchoolYearAsync_WithValidId_ShouldReturnTrue()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var schoolYear = CreateTestSchoolYear(schoolYearId, "2023/2024");
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearDeleted, "SchoolYear", schoolYearId, null, null, null))
                .ReturnsAsync(operation);
            
            // Mock FindAsync zamiast GetByIdAsync - implementacja używa FindAsync
            _mockSchoolYearRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear> { schoolYear });
            
            _mockTeamRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Team, bool>>>()))
                .ReturnsAsync(new List<Team>());

            // Act
            var result = await _service.DeleteSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeTrue();
            schoolYear.IsActive.Should().BeFalse();
            _mockSchoolYearRepository.Verify(x => x.Update(schoolYear), Times.Once);
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Completed, It.IsAny<string>(), null), Times.Once);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var schoolYearId = "non-existent";
            var operation = new OperationHistory { Id = "op-123" };

            _mockOperationHistoryService.Setup(x => x.CreateNewOperationEntryAsync(
                OperationType.SchoolYearDeleted, "SchoolYear", schoolYearId, null, null, null))
                .ReturnsAsync(operation);
            
            // Mock FindAsync zwracający pustą listę - implementacja używa FindAsync
            _mockSchoolYearRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<SchoolYear, bool>>>()))
                .ReturnsAsync(new List<SchoolYear>());

            // Act
            var result = await _service.DeleteSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryService.Verify(x => x.UpdateOperationStatusAsync(
                operation.Id, OperationStatus.Failed, It.IsAny<string>(), null), Times.Once);
        }

        #endregion

        #region GetAllActiveSchoolYearsAsync Tests

        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_ShouldReturnActiveSchoolYears()
        {
            // Arrange
            var activeSchoolYears = new List<SchoolYear>
            {
                CreateTestSchoolYear("sy-1", "2023/2024"),
                CreateTestSchoolYear("sy-2", "2024/2025")
            };
            
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                .Returns(false);
            _mockSchoolYearRepository.Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<SchoolYear, bool>>>()))
                .ReturnsAsync(activeSchoolYears);

            // Act
            var result = await _service.GetAllActiveSchoolYearsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.All(sy => sy.IsActive).Should().BeTrue();
        }

        #endregion

        #region Helper Methods

        private static SchoolYear CreateTestSchoolYear(string id, string name)
        {
            return new SchoolYear
            {
                Id = id,
                Name = name,
                StartDate = DateTime.Today.AddMonths(-6),
                EndDate = DateTime.Today.AddMonths(6),
                Description = "Test school year",
                IsCurrent = false,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 