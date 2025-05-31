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
    public class SchoolYearServiceTests
    {
        // Mocki dla zależności serwisu
        private readonly Mock<ISchoolYearRepository> _mockSchoolYearRepository;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolYearService>> _mockLogger;
        private readonly Mock<ITeamRepository> _mockTeamRepository; // Dodane dla testu DeleteSchoolYearAsync_SchoolYearInUse

        // Testowany serwis
        private readonly SchoolYearService _schoolYearService;

        // Przykładowy UPN użytkownika wykonującego operacje
        private readonly string _currentLoggedInUserUpn = "test.admin@example.com";
        // Przechwycony obiekt OperationHistory do weryfikacji logowania
        private OperationHistory? _capturedOperationHistory;

        public SchoolYearServiceTests()
        {
            // Inicjalizacja mocków
            _mockSchoolYearRepository = new Mock<ISchoolYearRepository>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearService>>();
            _mockTeamRepository = new Mock<ITeamRepository>(); // Inicjalizacja nowego mocka

            // Konfiguracja ICurrentUserService do zwracania UPN testowego użytkownika
            _mockCurrentUserService.Setup(s => s.GetCurrentUserUpn()).Returns(_currentLoggedInUserUpn);

            // Konfiguracja OperationHistoryRepository do przechwytywania logowanych operacji
            // Ta konfiguracja przechwytuje zarówno dodawane, jak i aktualizowane wpisy historii.
            _mockOperationHistoryRepository.Setup(r => r.AddAsync(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op)
                                         .Returns(Task.CompletedTask);
            _mockOperationHistoryRepository.Setup(r => r.Update(It.IsAny<OperationHistory>()))
                                         .Callback<OperationHistory>(op => _capturedOperationHistory = op);


            // Inicjalizacja testowanego serwisu z zamockowanymi zależnościami
            _schoolYearService = new SchoolYearService(
                _mockSchoolYearRepository.Object,
                _mockOperationHistoryRepository.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockTeamRepository.Object // Dodana zależność ITeamRepository
            );
        }

        // Metoda pomocnicza do resetowania przechwyconej historii operacji przed każdym testem, który tego wymaga.
        private void ResetCapturedOperationHistory()
        {
            _capturedOperationHistory = null;
        }

        // Testy dla GetSchoolYearByIdAsync
        [Fact]
        public async Task GetSchoolYearByIdAsync_ExistingId_ShouldReturnSchoolYear()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-2023";
            var expectedSchoolYear = new SchoolYear { Id = schoolYearId, Name = "2023/2024", IsActive = true };
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(expectedSchoolYear);

            // Act
            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedSchoolYear);
            _mockSchoolYearRepository.Verify(r => r.GetByIdAsync(schoolYearId), Times.Once);
        }

        [Fact]
        public async Task GetSchoolYearByIdAsync_NonExistingId_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-non-existent";
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _schoolYearService.GetSchoolYearByIdAsync(schoolYearId);

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(r => r.GetByIdAsync(schoolYearId), Times.Once);
        }

        // Testy dla GetAllActiveSchoolYearsAsync
        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_WhenActiveYearsExist_ShouldReturnActiveSchoolYears()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var activeSchoolYears = new List<SchoolYear>
            {
                new SchoolYear { Id = "sy-1", Name = "2022/2023", IsActive = true },
                new SchoolYear { Id = "sy-2", Name = "2023/2024", IsActive = true }
            };
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync((Expression<Func<SchoolYear, bool>> predicate) => activeSchoolYears.Where(predicate.Compile()));


            // Act
            var result = await _schoolYearService.GetAllActiveSchoolYearsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(activeSchoolYears);
            _mockSchoolYearRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetAllActiveSchoolYearsAsync_WhenNoActiveYearsExist_ShouldReturnEmptyList()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync(new List<SchoolYear>());

            // Act
            var result = await _schoolYearService.GetAllActiveSchoolYearsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        // Testy dla GetCurrentSchoolYearAsync
        [Fact]
        public async Task GetCurrentSchoolYearAsync_OneIsCurrent_ShouldReturnIt()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var currentSchoolYear = new SchoolYear { Id = "sy-current", Name = "2024/2025", IsCurrent = true, IsActive = true };
            _mockSchoolYearRepository.Setup(r => r.GetCurrentSchoolYearAsync()).ReturnsAsync(currentSchoolYear);

            // Act
            var result = await _schoolYearService.GetCurrentSchoolYearAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(currentSchoolYear);
            _mockSchoolYearRepository.Verify(r => r.GetCurrentSchoolYearAsync(), Times.Once);
        }

        [Fact]
        public async Task GetCurrentSchoolYearAsync_NoneIsCurrent_ShouldReturnNull()
        {
            // Arrange
            ResetCapturedOperationHistory();
            _mockSchoolYearRepository.Setup(r => r.GetCurrentSchoolYearAsync()).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _schoolYearService.GetCurrentSchoolYearAsync();

            // Assert
            result.Should().BeNull();
        }

        // Testy dla SetCurrentSchoolYearAsync
        [Fact]
        public async Task SetCurrentSchoolYearAsync_ValidNotCurrentYear_ShouldSetAndUnsetOthersAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var oldCurrentYearId = "sy-old-current";
            var newCurrentYearId = "sy-new-current";
            var oldCurrentYear = new SchoolYear { Id = oldCurrentYearId, Name = "Old Current", IsCurrent = true, IsActive = true };
            var newCurrentYear = new SchoolYear { Id = newCurrentYearId, Name = "New Current", IsCurrent = false, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(newCurrentYearId)).ReturnsAsync(newCurrentYear);
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync(new List<SchoolYear> { oldCurrentYear }); // Symuluje znalezienie starego bieżącego roku

            // Act
            var result = await _schoolYearService.SetCurrentSchoolYearAsync(newCurrentYearId);

            // Assert
            result.Should().BeTrue();
            newCurrentYear.IsCurrent.Should().BeTrue();
            oldCurrentYear.IsCurrent.Should().BeFalse(); // Sprawdzenie, czy stary został odznaczony
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy => sy.Id == oldCurrentYearId && !sy.IsCurrent)), Times.Once);
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy => sy.Id == newCurrentYearId && sy.IsCurrent)), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearSetAsCurrent);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(newCurrentYearId);
            _capturedOperationHistory.TargetEntityName.Should().Be(newCurrentYear.Name);
            _capturedOperationHistory.OperationDetails.Should().Contain("ustawiony jako bieżący");
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_YearAlreadyCurrent_ShouldReturnTrueAndLogNoAction()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var currentYearId = "sy-already-current";
            var currentYear = new SchoolYear { Id = currentYearId, Name = "Already Current", IsCurrent = true, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(currentYearId)).ReturnsAsync(currentYear);
            // FindAsync zwróci pustą listę, bo szukamy innych bieżących lat (sy.Id != schoolYearId)
            _mockSchoolYearRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SchoolYear, bool>>>()))
                                   .ReturnsAsync(new List<SchoolYear>());

            // Act
            var result = await _schoolYearService.SetCurrentSchoolYearAsync(currentYearId);

            // Assert
            result.Should().BeTrue();
            currentYear.IsCurrent.Should().BeTrue(); // Pozostaje true
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never); // Nie powinno być aktualizacji

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearSetAsCurrent);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(currentYearId);
            _capturedOperationHistory.OperationDetails.Should().Contain("był już bieżący");
        }

        [Fact]
        public async Task SetCurrentSchoolYearAsync_SchoolYearNotFoundOrInactive_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var nonExistentYearId = "sy-non-existent";
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(nonExistentYearId)).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _schoolYearService.SetCurrentSchoolYearAsync(nonExistentYearId);

            // Assert
            result.Should().BeFalse();
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearSetAsCurrent);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityId.Should().Be(nonExistentYearId);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Rok szkolny o ID '{nonExistentYearId}' nie istnieje lub jest nieaktywny.");
        }

        // Testy dla CreateSchoolYearAsync
        [Fact]
        public async Task CreateSchoolYearAsync_ValidData_ShouldCreateAndReturnSchoolYearAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "2025/2026";
            var startDate = new DateTime(2025, 9, 1);
            var endDate = new DateTime(2026, 6, 20);
            SchoolYear? addedSchoolYear = null;

            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(name)).ReturnsAsync((SchoolYear?)null);
            _mockSchoolYearRepository.Setup(r => r.AddAsync(It.IsAny<SchoolYear>()))
                                   .Callback<SchoolYear>(sy => addedSchoolYear = sy)
                                   .Returns(Task.CompletedTask);

            // Act
            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate, "Test description");

            // Assert
            result.Should().NotBeNull();
            addedSchoolYear.Should().NotBeNull();
            result.Should().BeEquivalentTo(addedSchoolYear, options => options.ExcludingMissingMembers()); // Porównaj właściwości
            result!.Name.Should().Be(name);
            result.StartDate.Should().Be(startDate.Date); // Serwis powinien zapisywać tylko datę
            result.EndDate.Should().Be(endDate.Date);
            result.IsCurrent.Should().BeFalse(); // Domyślnie nie jest bieżący
            result.IsActive.Should().BeTrue();
            result.CreatedBy.Should().Be(_currentLoggedInUserUpn);

            _mockSchoolYearRepository.Verify(r => r.AddAsync(It.IsAny<SchoolYear>()), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(result.Id);
            _capturedOperationHistory.TargetEntityName.Should().Be(name);
        }

        [Fact]
        public async Task CreateSchoolYearAsync_EmptyName_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var startDate = new DateTime(2025, 9, 1);
            var endDate = new DateTime(2026, 6, 20);

            // Act
            var result = await _schoolYearService.CreateSchoolYearAsync("", startDate, endDate);

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(r => r.AddAsync(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Nazwa roku szkolnego nie może być pusta.");
        }

        [Fact]
        public async Task CreateSchoolYearAsync_StartDateAfterEndDate_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "Błędny Rok";
            var startDate = new DateTime(2026, 6, 20);
            var endDate = new DateTime(2025, 9, 1);

            // Act
            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate);

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(r => r.AddAsync(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be("Data rozpoczęcia musi być wcześniejsza niż data zakończenia.");
        }

        [Fact]
        public async Task CreateSchoolYearAsync_NameConflict_ShouldReturnNullAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var name = "2023/2024";
            var startDate = new DateTime(2023, 9, 1);
            var endDate = new DateTime(2024, 6, 20);
            var existingSchoolYear = new SchoolYear { Id = "sy-existing", Name = name, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(name)).ReturnsAsync(existingSchoolYear);

            // Act
            var result = await _schoolYearService.CreateSchoolYearAsync(name, startDate, endDate);

            // Assert
            result.Should().BeNull();
            _mockSchoolYearRepository.Verify(r => r.AddAsync(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearCreated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Aktywny rok szkolny o nazwie '{name}' już istnieje.");
        }

        // Testy dla UpdateSchoolYearAsync
        [Fact]
        public async Task UpdateSchoolYearAsync_ExistingSchoolYearWithValidData_ShouldUpdateAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-update";
            var existingSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Old Name", StartDate = DateTime.Now.Date, EndDate = DateTime.Now.AddMonths(1).Date, IsActive = true, CreatedBy = "initial_user" };
            var updatedData = new SchoolYear { Id = schoolYearId, Name = "New Name 2026", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 20), Description = "Updated Desc", IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(existingSchoolYear);
            _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedData.Name)).ReturnsAsync((SchoolYear?)null); // Brak konfliktu nazw

            // Act
            var result = await _schoolYearService.UpdateSchoolYearAsync(updatedData);

            // Assert
            result.Should().BeTrue();
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy =>
                sy.Id == schoolYearId &&
                sy.Name == updatedData.Name &&
                sy.StartDate == updatedData.StartDate.Date &&
                sy.EndDate == updatedData.EndDate.Date &&
                sy.Description == updatedData.Description &&
                sy.ModifiedBy == _currentLoggedInUserUpn
            )), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(schoolYearId);
            _capturedOperationHistory.TargetEntityName.Should().Be(updatedData.Name); // Nazwa po aktualizacji
        }

        [Fact]
        public async Task UpdateSchoolYearAsync_SchoolYearNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearToUpdate = new SchoolYear { Id = "sy-non-existent", Name = "Non Existent", StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(1) };
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearToUpdate.Id)).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _schoolYearService.UpdateSchoolYearAsync(schoolYearToUpdate);

            // Assert
            result.Should().BeFalse();
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.TargetEntityId.Should().Be(schoolYearToUpdate.Id);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Rok szkolny o ID '{schoolYearToUpdate.Id}' nie istnieje.");
        }

        [Theory]
        [InlineData("New Name Conflict", "2025-09-01", "2026-06-20", "New Name Conflict", true, "Aktywny rok szkolny o nazwie 'New Name Conflict' już istnieje.")]
        [InlineData("Valid Name Update", "2026-09-01", "2025-06-20", null, false, "Niepoprawne dane wejściowe (nazwa, daty). Nazwa nie może być pusta, a data rozpoczęcia musi być wcześniejsza niż data zakończenia.")]
        public async Task UpdateSchoolYearAsync_InvalidDateRangeOrNameConflict_ShouldReturnFalseAndLogFailed(
            string updatedName, string startDateStr, string endDateStr, string? conflictingName, bool setupConflict, string expectedErrorMessage)
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-update-fail";
            var existingSchoolYear = new SchoolYear { Id = schoolYearId, Name = "Old Name For Update", StartDate = DateTime.Parse("2024-09-01"), EndDate = DateTime.Parse("2025-06-20"), IsActive = true };
            var schoolYearToUpdate = new SchoolYear
            {
                Id = schoolYearId,
                Name = updatedName,
                StartDate = DateTime.Parse(startDateStr),
                EndDate = DateTime.Parse(endDateStr)
            };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(existingSchoolYear);
            if (setupConflict && conflictingName != null)
            {
                // Symulujemy, że inny aktywny rok szkolny ma już taką nazwę
                _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedName))
                                       .ReturnsAsync(new SchoolYear { Id = "sy-conflict", Name = conflictingName, IsActive = true });
            }
            else
            {
                _mockSchoolYearRepository.Setup(r => r.GetSchoolYearByNameAsync(updatedName)).ReturnsAsync((SchoolYear?)null);
            }


            // Act
            var result = await _schoolYearService.UpdateSchoolYearAsync(schoolYearToUpdate);

            // Assert
            result.Should().BeFalse();
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearUpdated);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be(expectedErrorMessage); // Użycie Be() do dokładnego porównania
        }

        // Testy dla DeleteSchoolYearAsync
        [Fact]
        public async Task DeleteSchoolYearAsync_ExistingNotCurrentUnusedSchoolYear_ShouldSoftDeleteAndReturnTrueAndLog()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearId = "sy-to-delete";
            var schoolYearToDelete = new SchoolYear { Id = schoolYearId, Name = "To Delete", IsCurrent = false, IsActive = true };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearId)).ReturnsAsync(schoolYearToDelete);
            // Zakładamy, że rok szkolny nie jest używany przez żadne aktywne zespoły
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(
                expr => expr.Compile().Invoke(new Team { SchoolYearId = schoolYearId, IsActive = true, Status = TeamStatus.Active }) // Test z pasującym zespołem
            ))).ReturnsAsync(new List<Team>()); // Zwracamy pustą listę (brak użycia)

            // Act
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearId);

            // Assert
            result.Should().BeTrue();
            schoolYearToDelete.IsActive.Should().BeFalse(); // Sprawdzenie soft delete
            _mockSchoolYearRepository.Verify(r => r.Update(It.Is<SchoolYear>(sy => sy.Id == schoolYearId && !sy.IsActive)), Times.Once);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Completed);
            _capturedOperationHistory.TargetEntityId.Should().Be(schoolYearId);
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_SchoolYearNotFound_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var nonExistentYearId = "sy-non-existent-delete";
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(nonExistentYearId)).ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _schoolYearService.DeleteSchoolYearAsync(nonExistentYearId);

            // Assert
            result.Should().BeFalse();
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain($"Rok szkolny o ID '{nonExistentYearId}' nie istnieje.");
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_AttemptToDeleteCurrentSchoolYear_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var currentYearId = "sy-current-delete-attempt";
            var currentSchoolYear = new SchoolYear { Id = currentYearId, Name = "Current To Delete", IsCurrent = true, IsActive = true };
            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(currentYearId)).ReturnsAsync(currentSchoolYear);

            // Act
            var result = await _schoolYearService.DeleteSchoolYearAsync(currentYearId);

            // Assert
            result.Should().BeFalse();
            currentSchoolYear.IsActive.Should().BeTrue(); // Nie powinien zostać zdezaktywowany
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Contain("Nie można usunąć (dezaktywować) bieżącego roku szkolnego.");
        }

        [Fact]
        public async Task DeleteSchoolYearAsync_SchoolYearInUse_ShouldReturnFalseAndLogFailed()
        {
            // Arrange
            ResetCapturedOperationHistory();
            var schoolYearIdInUse = "sy-in-use";
            var schoolYear = new SchoolYear { Id = schoolYearIdInUse, Name = "Year In Use", IsCurrent = false, IsActive = true };
            var teamsUsingYear = new List<Team> { new Team { Id = "team1", SchoolYearId = schoolYearIdInUse, IsActive = true, Status = TeamStatus.Active } };

            _mockSchoolYearRepository.Setup(r => r.GetByIdAsync(schoolYearIdInUse)).ReturnsAsync(schoolYear);
            // Mock dla ITeamRepository - symuluje, że istnieją zespoły używające tego roku szkolnego.
            _mockTeamRepository.Setup(r => r.FindAsync(It.Is<Expression<Func<Team, bool>>>(
                expr => expr.Compile().Invoke(new Team { SchoolYearId = schoolYearIdInUse, IsActive = true, Status = TeamStatus.Active })
            ))).ReturnsAsync(teamsUsingYear);


            // Act
            var result = await _schoolYearService.DeleteSchoolYearAsync(schoolYearIdInUse);

            // Assert
            result.Should().BeFalse();
            schoolYear.IsActive.Should().BeTrue(); // Nie powinien zostać zdezaktywowany
            _mockSchoolYearRepository.Verify(r => r.Update(It.IsAny<SchoolYear>()), Times.Never);

            _capturedOperationHistory.Should().NotBeNull();
            _capturedOperationHistory!.Type.Should().Be(OperationType.SchoolYearDeleted);
            _capturedOperationHistory.Status.Should().Be(OperationStatus.Failed);
            _capturedOperationHistory.ErrorMessage.Should().Be($"Nie można usunąć roku szkolnego '{schoolYear.Name}', ponieważ jest nadal używany przez {teamsUsingYear.Count()} aktywnych zespołów."); // Poprawiona asercja na dokładny komunikat
        }
    }
}
