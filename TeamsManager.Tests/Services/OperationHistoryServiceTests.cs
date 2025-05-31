using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    public class OperationHistoryServiceTests
    {
        // Mocki dla zależności serwisu
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<OperationHistoryService>> _mockLogger;

        // Testowany serwis
        private readonly IOperationHistoryService _operationHistoryService;

        // Przykładowy UPN użytkownika wykonującego operacje
        private readonly string _currentLoggedInUserUpn = "test.operator@example.com";
        // Przechwycony obiekt OperationHistory do weryfikacji logowania
        private OperationHistory? _capturedOperationHistory;

        public OperationHistoryServiceTests()
        {
            // Inicjalizacja mocków
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<OperationHistoryService>>();

            // Konfiguracja ICurrentUserService
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja OperationHistoryRepository do przechwytywania logów
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);

            // Inicjalizacja serwisu
            _operationHistoryService = new OperationHistoryService(
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        // Metoda pomocnicza do resetowania przechwyconej historii operacji
        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        // Testy dla LogOperationAsync
        [Fact]
        public async Task LogOperationAsync_ValidParameters_ShouldCreateAndReturnOperationHistory()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationType = OperationType.TeamCreated;
            var operationStatus = OperationStatus.InProgress;
            var targetEntityType = nameof(Team);
            var targetEntityId = "team-123";
            var targetEntityName = "Nowy Zespół Testowy";
            var details = "{\"info\":\"Tworzenie zespołu\"}";
            var parentOpId = "parent-op-001";

            // Act
            var result = await _operationHistoryService.LogOperationAsync(
                operationType,
                operationStatus,
                targetEntityType,
                targetEntityId,
                targetEntityName,
                details,
                parentOpId
            );

            // Assert
            result.Should().NotBeNull();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory.Should().BeEquivalentTo(result, options => options.Excluding(o => o.Id).Excluding(o => o.StartedAt));

            _capturedOperationHistory!.Id.Should().NotBeNullOrEmpty();
            _capturedOperationHistory.Type.Should().Be(operationType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);
            _capturedOperationHistory.TargetEntityType.Should().Be(targetEntityType);
            _capturedOperationHistory.TargetEntityId.Should().Be(targetEntityId);
            _capturedOperationHistory.TargetEntityName.Should().Be(targetEntityName);
            _capturedOperationHistory.OperationDetails.Should().Be(details);
            _capturedOperationHistory.ParentOperationId.Should().Be(parentOpId);
            _capturedOperationHistory.CreatedBy.Should().Be(_currentLoggedInUserUpn);
            _capturedOperationHistory.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _capturedOperationHistory.CompletedAt.Should().BeNull();

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
        }

        [Fact]
        public async Task LogOperationAsync_StatusCompleted_ShouldSetStartedAtAndCompletedAt()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationType = OperationType.SystemBackup;
            var operationStatus = OperationStatus.Completed;
            var targetEntityType = "System";
            var targetEntityName = "Pełna kopia zapasowa";
            var details = "Kopia zakończona pomyślnie.";

            // Act
            var result = await _operationHistoryService.LogOperationAsync(
                operationType,
                operationStatus,
                targetEntityType,
                targetEntityName: targetEntityName,
                details: details
            );

            // Assert
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.StartedAt.Should().NotBe(default(DateTime));
            _capturedOperationHistory.CompletedAt.Should().HaveValue();
            _capturedOperationHistory.CompletedAt.Value.Should().BeOnOrAfter(_capturedOperationHistory.StartedAt);
            _capturedOperationHistory.Duration.Should().HaveValue();
            _capturedOperationHistory.OperationDetails.Should().Be(details);
        }


        // Testy dla UpdateOperationStatusAsync
        [Fact]
        public async Task UpdateOperationStatusAsync_ExistingOperation_ShouldUpdateStatusAndFieldsAndReturnTrue()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-to-update";
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, StartedAt = DateTime.UtcNow.AddMinutes(-5), CreatedBy = "initial_user" };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            var newStatus = OperationStatus.Completed;
            var message = "Operacja zakończona sukcesem.";

            // Act
            var result = await _operationHistoryService.UpdateOperationStatusAsync(operationId, newStatus, message);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Id.Should().Be(operationId);
            _capturedOperationHistory.Status.Should().Be(newStatus);
            _capturedOperationHistory.OperationDetails.Should().Contain(message);
            _capturedOperationHistory.CompletedAt.Should().HaveValue();
            _capturedOperationHistory.CompletedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _capturedOperationHistory.Duration.Should().HaveValue();
            _capturedOperationHistory.Duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
            _capturedOperationHistory.ModifiedBy.Should().Be(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Verify(r => r.Update(It.Is<OperationHistory>(op => op.Id == operationId)), Times.Once);
        }

        [Fact]
        public async Task UpdateOperationStatusAsync_ExistingOperationToFailed_ShouldUpdateStatusAndErrorFields()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-to-fail";
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, StartedAt = DateTime.UtcNow.AddMinutes(-2) };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            var newStatus = OperationStatus.Failed;
            var errorMessage = "Wystąpił błąd krytyczny.";
            var stackTrace = "Szczegóły błędu...";

            // Act
            var result = await _operationHistoryService.UpdateOperationStatusAsync(operationId, newStatus, errorMessage, stackTrace);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(newStatus);
            _capturedOperationHistory.ErrorMessage.Should().Be(errorMessage);
            _capturedOperationHistory.ErrorStackTrace.Should().Be(stackTrace);
            _capturedOperationHistory.CompletedAt.Should().HaveValue();
            _capturedOperationHistory.Duration.Should().HaveValue();
        }

        [Fact]
        public async Task UpdateOperationStatusAsync_OperationNotFound_ShouldReturnFalse()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-non-existent";
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync((OperationHistory?)null);

            // Act
            var result = await _operationHistoryService.UpdateOperationStatusAsync(operationId, OperationStatus.Completed);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);
            _capturedOperationHistory.Should().BeNull();
        }

        // Testy dla UpdateOperationProgressAsync
        [Fact]
        public async Task UpdateOperationProgressAsync_ExistingOperation_ShouldUpdateProgressAndStatus()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-progress";
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, TotalItems = 100, ProcessedItems = 10, FailedItems = 1, CreatedBy = "initial_user" };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            var processed = 50;
            var failed = 5;
            var total = 100;

            // Act
            var result = await _operationHistoryService.UpdateOperationProgressAsync(operationId, processed, failed, total);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.ProcessedItems.Should().Be(processed);
            _capturedOperationHistory.FailedItems.Should().Be(failed);
            _capturedOperationHistory.TotalItems.Should().Be(total);
            _capturedOperationHistory.ProgressPercentage.Should().BeApproximately(50.0, 0.1);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress);
            _capturedOperationHistory.ModifiedBy.Should().Be(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Verify(r => r.Update(It.Is<OperationHistory>(op => op.Id == operationId)), Times.Once);
        }

        [Fact]
        public async Task UpdateOperationProgressAsync_CompletesOperationWithPartialSuccess_ShouldUpdateStatusCorrectly()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-partial-success";
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, TotalItems = 10, CreatedBy = "initial_user" };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            // Act
            var result = await _operationHistoryService.UpdateOperationProgressAsync(operationId, processedItems: 10, failedItems: 2, totalItems: 10);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.ProcessedItems.Should().Be(10);
            _capturedOperationHistory.FailedItems.Should().Be(2);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.PartialSuccess);
        }

        [Fact]
        public async Task UpdateOperationProgressAsync_CompletesOperationWithFullSuccess_ShouldUpdateStatusToCompleted()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-full-success";
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, TotalItems = 10, CreatedBy = "initial_user" };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            // Act
            var result = await _operationHistoryService.UpdateOperationProgressAsync(operationId, processedItems: 10, failedItems: 0, totalItems: 10);

            // Assert
            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.ProcessedItems.Should().Be(10);
            _capturedOperationHistory.FailedItems.Should().Be(0);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
        }


        [Fact]
        public async Task UpdateOperationProgressAsync_OperationNotFound_ShouldReturnFalse()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-progress-non-existent";
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync((OperationHistory?)null);

            // Act
            var result = await _operationHistoryService.UpdateOperationProgressAsync(operationId, 50, 0);

            // Assert
            result.Should().BeFalse();
            _mockOperationHistoryRepository.Verify(r => r.Update(It.IsAny<OperationHistory>()), Times.Never);
        }

        // Testy dla GetOperationByIdAsync
        [Fact]
        public async Task GetOperationByIdAsync_ExistingId_ShouldReturnOperation()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-get-by-id";
            var expectedOperation = new OperationHistory { Id = operationId, Type = OperationType.SystemBackup };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(expectedOperation);

            // Act
            var result = await _operationHistoryService.GetOperationByIdAsync(operationId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedOperation);
        }

        [Fact]
        public async Task GetOperationByIdAsync_NonExistingId_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var operationId = "op-get-by-id-non-existent";
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync((OperationHistory?)null);

            // Act
            var result = await _operationHistoryService.GetOperationByIdAsync(operationId);

            // Assert
            result.Should().BeNull();
        }

        // Testy dla GetHistoryForEntityAsync
        [Fact]
        public async Task GetHistoryForEntityAsync_WhenHistoryExists_ShouldReturnHistory()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var entityType = nameof(Team);
            var entityId = "team-hist-1";
            var historyList = new List<OperationHistory>
            {
                new OperationHistory { TargetEntityType = entityType, TargetEntityId = entityId, Type = OperationType.TeamCreated },
                new OperationHistory { TargetEntityType = entityType, TargetEntityId = entityId, Type = OperationType.MemberAdded }
            };
            _mockOperationHistoryRepository.Setup(r => r.GetHistoryForEntityAsync(entityType, entityId, null)).ReturnsAsync(historyList);

            // Act
            var result = await _operationHistoryService.GetHistoryForEntityAsync(entityType, entityId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(historyList);
        }

        [Fact]
        public async Task GetHistoryForEntityAsync_WithCount_ShouldReturnLimitedHistory()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var entityType = nameof(User);
            var entityId = "user-hist-1";
            var count = 1;
            var historyList = new List<OperationHistory>
            {
                new OperationHistory { TargetEntityType = entityType, TargetEntityId = entityId, Type = OperationType.UserUpdated, StartedAt = DateTime.UtcNow }
            };
            _mockOperationHistoryRepository.Setup(r => r.GetHistoryForEntityAsync(entityType, entityId, count)).ReturnsAsync(historyList);


            // Act
            var result = await _operationHistoryService.GetHistoryForEntityAsync(entityType, entityId, count);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(count);
        }

        // Testy dla GetHistoryByUserAsync
        [Fact]
        public async Task GetHistoryByUserAsync_WhenHistoryExists_ShouldReturnHistory()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var userUpn = "user.test@example.com";
            var historyList = new List<OperationHistory>
            {
                new OperationHistory { CreatedBy = userUpn, Type = OperationType.TeamCreated },
                new OperationHistory { CreatedBy = userUpn, Type = OperationType.ChannelCreated }
            };
            _mockOperationHistoryRepository.Setup(r => r.GetHistoryByUserAsync(userUpn, null)).ReturnsAsync(historyList);

            // Act
            var result = await _operationHistoryService.GetHistoryByUserAsync(userUpn);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(historyList);
        }

        // Testy dla GetHistoryByFilterAsync
        [Fact]
        public async Task GetHistoryByFilterAsync_WithDateRange_ShouldCallRepositoryWithCorrectParameters()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var startDate = DateTime.UtcNow.AddDays(-7).Date; // Używamy .Date dla spójności z logiką serwisu
            var endDate = DateTime.UtcNow.Date;
            var expectedHistoryItem = new OperationHistory { StartedAt = startDate.AddHours(1) };
            var allOperations = new List<OperationHistory>
            {
                expectedHistoryItem,
                new OperationHistory { StartedAt = startDate.AddDays(-1) }, // Poza zakresem
                new OperationHistory { StartedAt = endDate.AddDays(2) }    // Poza zakresem
            };

            _mockOperationHistoryRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OperationHistory, bool>>>()))
                                         .ReturnsAsync((Expression<Func<OperationHistory, bool>> predicate) =>
                                             allOperations.Where(predicate.Compile()).ToList());

            // Act
            var result = await _operationHistoryService.GetHistoryByFilterAsync(startDate: startDate, endDate: endDate);

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainSingle().Which.Should().BeEquivalentTo(expectedHistoryItem);

            // Weryfikacja, że FindAsync zostało wywołane (nie musimy już weryfikować GetHistoryByDateRangeAsync)
            _mockOperationHistoryRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<OperationHistory, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByFilterAsync_WithMultipleFiltersAndPagination_ShouldApplyFiltersAndPagination()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var baseDate = DateTime.UtcNow.Date;
            var filterType = OperationType.UserCreated;
            var filterStatus = OperationStatus.Completed;
            var filterCreatedBy = "admin@example.com";
            var page = 2;
            var pageSize = 3; // Zmniejszono dla łatwiejszego testowania paginacji

            var allOperations = new List<OperationHistory>
            {
                // Pasujące rekordy
                new OperationHistory { Id = "op1", StartedAt = baseDate.AddDays(-1), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op2", StartedAt = baseDate.AddDays(-2), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op3", StartedAt = baseDate.AddDays(-3), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op4", StartedAt = baseDate.AddDays(-4), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op5", StartedAt = baseDate.AddDays(-5), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op6", StartedAt = baseDate.AddDays(-6), Type = filterType, Status = filterStatus, CreatedBy = filterCreatedBy },
                // Niepasujące rekordy
                new OperationHistory { Id = "op7", StartedAt = baseDate.AddDays(-1), Type = OperationType.TeamCreated, Status = filterStatus, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op8", StartedAt = baseDate.AddDays(-1), Type = filterType, Status = OperationStatus.Failed, CreatedBy = filterCreatedBy },
                new OperationHistory { Id = "op9", StartedAt = baseDate.AddDays(-1), Type = filterType, Status = filterStatus, CreatedBy = "otheruser@example.com" },
            };

            _mockOperationHistoryRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<OperationHistory, bool>>>()))
                                         .ReturnsAsync((Expression<Func<OperationHistory, bool>> predicate) =>
                                             allOperations.Where(predicate.Compile()).ToList());

            // Act
            var result = await _operationHistoryService.GetHistoryByFilterAsync(
                startDate: null,
                endDate: null,
                operationType: filterType,
                operationStatus: filterStatus,
                createdBy: filterCreatedBy,
                page: page,
                pageSize: pageSize
            );

            // Assert
            var expectedMatchingOperations = allOperations
                .Where(oh => oh.Type == filterType && oh.Status == filterStatus && oh.CreatedBy == filterCreatedBy)
                .OrderByDescending(oh => oh.StartedAt)
                .ToList();

            result.Should().NotBeNull();
            // Na stronie 2, z pageSize = 3, powinniśmy dostać rekordy od 4 do 6 z posortowanej listy (6 pasujących)
            result.Should().HaveCount(pageSize);
            result.First().Id.Should().Be("op4"); // Najstarszy z drugiej strony
            result.Last().Id.Should().Be("op6");  // Najnowszy z drugiej strony (bo sortujemy malejąco)

            // Bardziej precyzyjne sprawdzenie zawartości strony
            var expectedPageContent = expectedMatchingOperations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            result.Should().BeEquivalentTo(expectedPageContent, options => options.WithStrictOrdering());
        }
    }
}
