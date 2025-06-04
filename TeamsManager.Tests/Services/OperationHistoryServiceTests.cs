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
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<OperationHistoryService>> _mockLogger;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly string _currentLoggedInUserUpn = "test.operator@example.com";
        private OperationHistory? _capturedOperationHistory;

        public OperationHistoryServiceTests()
        {
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<OperationHistoryService>>();

            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja AddAsync do przechwytywania
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            // Konfiguracja Update do przechwytywania (jeśli Update jest używane w serwisie bezpośrednio)
            // W obecnej implementacji serwisu, UpdateOperationStatusAsync i UpdateOperationProgressAsync
            // pobierają encję, modyfikują ją i wywołują _operationHistoryRepository.Update(operation);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);


            _operationHistoryService = new OperationHistoryService(
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object
            );
        }

        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        [Fact]
        public async Task CreateNewOperationEntryAsync_ValidParameters_ShouldCreateAndReturnOperationHistory()
        {
            ResetCapturedOperationHistory();
            var operationType = OperationType.TeamCreated;
            var targetEntityType = nameof(Team);
            var targetEntityId = "team-123";
            var targetEntityName = "Nowy Zespół Testowy";
            var details = "{\"info\":\"Tworzenie zespołu\"}";
            var parentOpId = "parent-op-001";

            var result = await _operationHistoryService.CreateNewOperationEntryAsync(
                operationType, targetEntityType, targetEntityId, targetEntityName, details, parentOpId
            );

            result.Should().NotBeNull();
            _capturedOperationHistory.Should().NotBeNull();
            // Porównanie z pominięciem Id i StartedAt, które są generowane/ustawiane dynamicznie
            _capturedOperationHistory.Should().BeEquivalentTo(result, options =>
                options.Excluding(o => o.Id)
                       .Excluding(o => o.StartedAt) // StartedAt jest ustawiane na DateTime.UtcNow
            );

            _capturedOperationHistory!.Id.Should().NotBeNullOrEmpty();
            _capturedOperationHistory.Type.Should().Be(operationType);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.InProgress); // Zawsze InProgress
            _capturedOperationHistory.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2)); // Używamy większej tolerancji
            _capturedOperationHistory.CompletedAt.Should().BeNull(); // Bo status InProgress

            _mockOperationHistoryRepository.Verify(r => r.AddAsync(It.IsAny<OperationHistory>()), Times.Once);
        }

        [Fact]
        public async Task CreateNewOperationEntryAsync_ShouldAlwaysSetInProgressStatus()
        {
            ResetCapturedOperationHistory();
            var operationType = OperationType.SystemBackup;
            var targetEntityType = "System";
            var targetEntityName = "Pełna kopia zapasowa";
            var details = "Rozpoczynanie kopii zapasowej.";

            await _operationHistoryService.CreateNewOperationEntryAsync(
                operationType, targetEntityType, targetEntityName: targetEntityName, details: details
            );

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Status.Should().Be(OperationStatus.InProgress); // Zawsze InProgress
            _capturedOperationHistory.StartedAt.Should().NotBe(default(DateTime));
            _capturedOperationHistory.CompletedAt.Should().BeNull(); // Nie ustawione dla InProgress
            _capturedOperationHistory.Duration.Should().BeNull(); // Nie ustawione dla InProgress
            _capturedOperationHistory.OperationDetails.Should().Be(details);
        }

        [Fact]
        public async Task UpdateOperationStatusAsync_ExistingOperation_ShouldUpdateStatusAndFieldsAndReturnTrue()
        {
            ResetCapturedOperationHistory();
            var operationId = "op-to-update";
            var initialStartTime = DateTime.UtcNow.AddMinutes(-5);
            var existingOperation = new OperationHistory { Id = operationId, Status = OperationStatus.InProgress, StartedAt = initialStartTime, CreatedBy = "initial_user" };
            _mockOperationHistoryRepository.Setup(r => r.GetByIdAsync(operationId)).ReturnsAsync(existingOperation);

            var newStatus = OperationStatus.Completed;
            var message = "Operacja zakończona sukcesem.";

            var result = await _operationHistoryService.UpdateOperationStatusAsync(operationId, newStatus, message);

            result.Should().BeTrue();
            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Id.Should().Be(operationId);
            _capturedOperationHistory.Status.Should().Be(newStatus);
            _capturedOperationHistory.OperationDetails.Should().Contain(message); // Może zawierać poprzednie detale
            _capturedOperationHistory.CompletedAt.Should().HaveValue();
            _capturedOperationHistory.CompletedAt.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _capturedOperationHistory.Duration.Should().HaveValue();
            _capturedOperationHistory.Duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
            _capturedOperationHistory.ModifiedBy.Should().Be(_currentLoggedInUserUpn);

            _mockOperationHistoryRepository.Verify(r => r.Update(It.Is<OperationHistory>(op => op.Id == operationId)), Times.Once);
        }

        // Pozostałe testy dla OperationHistoryService (UpdateOperationStatusAsync, UpdateOperationProgressAsync, Get*, itd.)
        // wydają się być już dobrze napisane i powinny nadal działać po drobnych korektach w serwisie.
        // Główne zmiany w serwisie dotyczyły sposobu inicjalizacji dat i aktualizacji pól.
    }
}