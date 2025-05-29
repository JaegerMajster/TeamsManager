using System;
using System.Text.Json;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class OperationHistoryTests
    {
        // Metoda pomocnicza do tworzenia obiektu szczegółów dla testów
        private class TestOperationDetails
        {
            public string Info { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        [Fact]
        public void OperationHistory_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var operation = new OperationHistory();

            // Sprawdzenie pól bezpośrednich
            operation.Id.Should().Be(string.Empty);
            operation.Type.Should().Be(default(OperationType)); // Zazwyczaj pierwsza wartość enuma (np. 0)
            operation.TargetEntityType.Should().Be(string.Empty);
            operation.TargetEntityId.Should().Be(string.Empty);
            operation.TargetEntityName.Should().Be(string.Empty);
            operation.OperationDetails.Should().Be(string.Empty);
            operation.Status.Should().Be(OperationStatus.Pending); // Domyślna wartość z modelu
            operation.ErrorMessage.Should().BeNull();
            operation.ErrorStackTrace.Should().BeNull();
            operation.StartedAt.Should().Be(default(DateTime)); // Ustawiane przez MarkAsStarted
            operation.CompletedAt.Should().BeNull();
            operation.Duration.Should().BeNull();
            operation.UserIpAddress.Should().BeNull();
            operation.UserAgent.Should().BeNull();
            operation.SessionId.Should().BeNull();
            operation.ParentOperationId.Should().BeNull();
            operation.SequenceNumber.Should().BeNull();
            operation.TotalItems.Should().BeNull();
            operation.ProcessedItems.Should().BeNull();
            operation.FailedItems.Should().BeNull();
            operation.Tags.Should().BeNull();

            // Pola z BaseEntity
            operation.IsActive.Should().BeTrue();
            // operation.CreatedDate - zależne od BaseEntity/DbContext

            // Właściwości obliczane
            operation.IsInProgress.Should().BeFalse();
            operation.IsCompleted.Should().BeFalse();
            operation.IsSuccessful.Should().BeFalse();
            operation.ProgressPercentage.Should().Be(0);
            operation.DurationInSeconds.Should().Be(0);
            operation.StatusDescription.Should().Be("Oczekująca");
            // ShortDescription będzie zależeć od domyślnego OperationType.ToString() i pustego TargetEntityName
            operation.ShortDescription.Should().Be("Nieznana operacja - ");
        }

        [Fact]
        public void MarkAsStarted_ShouldSetStatusAndStartedAt()
        {
            // Przygotowanie
            var operation = new OperationHistory();

            // Wykonanie
            operation.MarkAsStarted();

            // Sprawdzenie
            operation.Status.Should().Be(OperationStatus.InProgress);
            operation.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            operation.IsInProgress.Should().BeTrue();
            operation.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void MarkAsCompleted_ShouldSetStatusDatesAndDuration()
        {
            // Przygotowanie
            var operation = new OperationHistory();
            operation.MarkAsStarted(); // Operacja musi być rozpoczęta
            var startedAt = operation.StartedAt;
            System.Threading.Thread.Sleep(10); // Mała pauza dla Duration

            // Wykonanie
            operation.MarkAsCompleted();

            // Sprawdzenie
            operation.Status.Should().Be(OperationStatus.Completed);
            operation.CompletedAt.Should().HaveValue();
            operation.CompletedAt.Value.Should().BeOnOrAfter(startedAt).And.BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            operation.Duration.Should().HaveValue();
            operation.Duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
            operation.IsInProgress.Should().BeFalse();
            operation.IsCompleted.Should().BeTrue();
            operation.IsSuccessful.Should().BeTrue();
            operation.StatusDescription.Should().Be("Zakończona sukcesem");
        }

        [Fact]
        public void MarkAsFailed_ShouldSetStatusDatesDurationAndErrorDetails()
        {
            // Przygotowanie
            var operation = new OperationHistory();
            operation.MarkAsStarted();
            var startedAt = operation.StartedAt;
            var errorMessage = "Wystąpił krytyczny błąd.";
            var stackTrace = "Szczegółowy stos wywołań błędu...";
            System.Threading.Thread.Sleep(10);

            // Wykonanie
            operation.MarkAsFailed(errorMessage, stackTrace);

            // Sprawdzenie
            operation.Status.Should().Be(OperationStatus.Failed);
            operation.CompletedAt.Should().HaveValue();
            operation.CompletedAt.Value.Should().BeOnOrAfter(startedAt).And.BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            operation.Duration.Should().HaveValue();
            operation.Duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
            operation.ErrorMessage.Should().Be(errorMessage);
            operation.ErrorStackTrace.Should().Be(stackTrace);
            operation.IsInProgress.Should().BeFalse();
            operation.IsCompleted.Should().BeTrue();
            operation.IsSuccessful.Should().BeFalse();
            operation.StatusDescription.Should().Be("Nieudana");
        }

        [Fact]
        public void MarkAsCancelled_ShouldSetStatusDatesAndDuration()
        {
            // Przygotowanie
            var operation = new OperationHistory();
            operation.MarkAsStarted();
            var startedAt = operation.StartedAt;
            System.Threading.Thread.Sleep(10);

            // Wykonanie
            operation.MarkAsCancelled();

            // Sprawdzenie
            operation.Status.Should().Be(OperationStatus.Cancelled);
            operation.CompletedAt.Should().HaveValue();
            operation.CompletedAt.Value.Should().BeOnOrAfter(startedAt).And.BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            operation.Duration.Should().HaveValue();
            operation.Duration.Value.Should().BeGreaterThan(TimeSpan.Zero);
            operation.IsInProgress.Should().BeFalse();
            operation.IsCompleted.Should().BeTrue();
            operation.IsSuccessful.Should().BeFalse();
            operation.StatusDescription.Should().Be("Anulowana");
        }

        [Theory]
        [InlineData(10, 5, 0, 50.0, OperationStatus.InProgress)] // W trakcie
        [InlineData(10, 10, 0, 100.0, OperationStatus.Completed)] // Zakończona sukcesem
        [InlineData(10, 8, 2, 80.0, OperationStatus.InProgress)]  // Nadal w trakcie, mimo błędów, bo nie wszystkie przetworzone
        [InlineData(10, 10, 2, 100.0, OperationStatus.PartialSuccess)] // Wszystkie przetworzone, ale z błędami
        [InlineData(0, 0, 0, 0.0, OperationStatus.InProgress)] // TotalItems = 0, powinno być InProgress jeśli wystartowane
        [InlineData(5, 5, 5, 100.0, OperationStatus.PartialSuccess)] // Wszystkie z błędami
        public void UpdateProgress_ShouldCorrectlyUpdateProgressAndStatus(
            int totalItems, int processedCount, int failedCount, double expectedPercentage, OperationStatus expectedStatusAfterUpdate)
        {
            // Przygotowanie
            var operation = new OperationHistory { TotalItems = totalItems };
            operation.MarkAsStarted(); // Status InProgress

            // Wykonanie
            operation.UpdateProgress(processedCount, failedCount);

            // Sprawdzenie
            operation.ProcessedItems.Should().Be(processedCount);
            operation.FailedItems.Should().Be(failedCount);
            operation.ProgressPercentage.Should().Be(expectedPercentage);
            operation.Status.Should().Be(expectedStatusAfterUpdate);
        }

        [Fact]
        public void UpdateProgress_WhenTotalItemsNotSet_ShouldNotChangeStatusFromInProgress()
        {
            // Przygotowanie
            var operation = new OperationHistory { TotalItems = null }; // TotalItems nie jest ustawione
            operation.MarkAsStarted();

            // Wykonanie
            operation.UpdateProgress(5, 0);

            // Sprawdzenie
            operation.ProcessedItems.Should().Be(5);
            operation.FailedItems.Should().Be(0);
            operation.ProgressPercentage.Should().Be(0); // Bo TotalItems jest null
            operation.Status.Should().Be(OperationStatus.InProgress); // Status nie powinien się zmienić na Completed/PartialSuccess
        }

        [Fact]
        public void SetAndGetOperationDetails_ShouldSerializeAndDeserializeObjectCorrectly()
        {
            // Przygotowanie
            var operation = new OperationHistory();
            var details = new TestOperationDetails { Info = "To są szczegóły operacji", Value = 123 };

            // Wykonanie
            operation.SetOperationDetails(details);
            var retrievedDetails = operation.GetOperationDetails<TestOperationDetails>();

            // Sprawdzenie
            operation.OperationDetails.Should().NotBeNullOrWhiteSpace();
            // Sprawdzenie, czy JSON zawiera oczekiwane fragmenty
            operation.OperationDetails.Should().Contain("\"Info\": \"To są szczegóły operacji\"");
            operation.OperationDetails.Should().Contain("\"Value\": 123");

            retrievedDetails.Should().NotBeNull();
            retrievedDetails.Info.Should().Be("To są szczegóły operacji");
            retrievedDetails.Value.Should().Be(123);
        }

        [Fact]
        public void GetOperationDetails_WhenDetailsAreInvalidJson_ShouldReturnNull()
        {
            // Przygotowanie
            var operation = new OperationHistory { OperationDetails = "to nie jest poprawny json" };

            // Wykonanie
            var retrievedDetails = operation.GetOperationDetails<TestOperationDetails>();

            // Sprawdzenie
            retrievedDetails.Should().BeNull();
        }

        [Fact]
        public void GetOperationDetails_WhenDetailsAreEmpty_ShouldReturnNull()
        {
            // Przygotowanie
            var operation = new OperationHistory { OperationDetails = string.Empty };

            // Wykonanie
            var retrievedDetails = operation.GetOperationDetails<TestOperationDetails>();

            // Sprawdzenie
            retrievedDetails.Should().BeNull();
        }

        [Fact]
        public void ShortDescription_ShouldFormatCorrectly()
        {
            // Przygotowanie
            var operation = new OperationHistory
            {
                Type = OperationType.TeamCreated,
                TargetEntityName = "Nowy Fantastyczny Zespół"
            };

            // Sprawdzenie
            operation.ShortDescription.Should().Be("Utworzenie zespołu - Nowy Fantastyczny Zespół");
        }

        // Dodatkowe testy dla właściwości obliczanych w różnych stanach
        [Theory]
        [InlineData(OperationStatus.Pending, false, false, false, "Oczekująca")]
        [InlineData(OperationStatus.InProgress, true, false, false, "W trakcie")]
        [InlineData(OperationStatus.Completed, false, true, true, "Zakończona sukcesem")]
        [InlineData(OperationStatus.Failed, false, true, false, "Nieudana")]
        [InlineData(OperationStatus.Cancelled, false, true, false, "Anulowana")]
        [InlineData(OperationStatus.PartialSuccess, false, true, true, "Częściowy sukces")]
        public void ComputedStatusProperties_ShouldReflectStatus(OperationStatus status, bool expectedIsInProgress, bool expectedIsCompleted, bool expectedIsSuccessful, string expectedStatusDescription)
        {
            // Przygotowanie
            var operation = new OperationHistory { Status = status };

            // Sprawdzenie
            operation.IsInProgress.Should().Be(expectedIsInProgress);
            operation.IsCompleted.Should().Be(expectedIsCompleted);
            operation.IsSuccessful.Should().Be(expectedIsSuccessful);
            operation.StatusDescription.Should().Be(expectedStatusDescription);
        }
    }
}