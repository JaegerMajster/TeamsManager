using System;
using FluentAssertions;
using TeamsManager.Core.Models; // Załóżmy, że BaseEntity jest tutaj
using Xunit;

namespace TeamsManager.Tests.Models
{
    // Aby przetestować klasę abstrakcyjną, musimy stworzyć konkretną klasę testową, która po niej dziedziczy.
    public class ConcreteTestEntity : BaseEntity
    {
        public string TestProperty { get; set; } = string.Empty;
    }

    public class BaseEntityTests
    {
        [Fact]
        public void BaseEntity_WhenCreated_ShouldHaveDefaultAuditValues()
        {
            // Przygotowanie i Wykonanie
            var entity = new ConcreteTestEntity();
            var initialCreationTime = DateTime.UtcNow; // Czas tuż przed potencjalnym ustawieniem przez DbContext/konstruktor

            // Sprawdzenie
            entity.Id.Should().Be(string.Empty); // Jeśli Id jest stringiem
            entity.IsActive.Should().BeTrue();   // Domyślnie aktywne

            // CreatedDate jest ustawiane przez DbContext.SaveChanges lub w konstruktorze BaseEntity.
            // Jeśli nie jest ustawiane w konstruktorze BaseEntity, to tutaj będzie default(DateTime).
            // Jeśli jest ustawiane w konstruktorze BaseEntity na DateTime.UtcNow, to poniższa asercja byłaby lepsza:
            // entity.CreatedDate.Should().BeCloseTo(initialCreationTime, TimeSpan.FromSeconds(1));
            // Na razie załóżmy, że testujemy "surowy" obiekt przed interakcją z DbContext.
            // Jeśli w BaseEntity.cs masz logikę ustawiania CreatedDate w konstruktorze, dostosuj ten test.
            // Na podstawie Twojego kodu BaseEntity, CreatedDate nie jest tam inicjalizowane.
            entity.CreatedDate.Should().Be(default(DateTime));


            entity.CreatedBy.Should().Be(string.Empty); // Domyślnie pusty string
            entity.ModifiedDate.Should().BeNull();
            entity.ModifiedBy.Should().BeNull();
        }

        [Fact]
        public void MarkAsModified_ShouldSetModifiedDateAndModifiedBy()
        {
            // Przygotowanie
            var entity = new ConcreteTestEntity();
            var modifier = "test_user_modifier";
            var timeBeforeModification = DateTime.UtcNow;
            System.Threading.Thread.Sleep(10); // Mała pauza, aby upewnić się, że czas się zmieni

            // Wykonanie
            entity.MarkAsModified(modifier);

            // Sprawdzenie
            entity.ModifiedDate.Should().NotBeNull();
            entity.ModifiedDate.Should().BeOnOrAfter(timeBeforeModification);
            entity.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            entity.ModifiedBy.Should().Be(modifier);
        }

        [Fact]
        public void MarkAsDeleted_ShouldSetIsActiveToFalseAndCallMarkAsModified()
        {
            // Przygotowanie
            var entity = new ConcreteTestEntity();
            var deleter = "test_user_deleter";
            var timeBeforeDeletion = DateTime.UtcNow;
            System.Threading.Thread.Sleep(10);

            // Wykonanie
            entity.MarkAsDeleted(deleter);

            // Sprawdzenie
            entity.IsActive.Should().BeFalse();
            entity.ModifiedDate.Should().NotBeNull();
            entity.ModifiedDate.Should().BeOnOrAfter(timeBeforeDeletion);
            entity.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            entity.ModifiedBy.Should().Be(deleter); // MarkAsDeleted wywołuje MarkAsModified
        }

        [Fact]
        public void MarkAsModified_WhenCalledMultipleTimes_ShouldUpdateToLatest()
        {
            // Przygotowanie
            var entity = new ConcreteTestEntity();
            var firstModifier = "user1";
            var secondModifier = "user2";

            // Wykonanie - Pierwsza modyfikacja
            entity.MarkAsModified(firstModifier);
            DateTime? firstModificationDate = entity.ModifiedDate; // Jawnie typ DateTime?
            string? firstModifiedBy = entity.ModifiedBy;

            // Asercja, że pierwsza modyfikacja ustawiła datę
            firstModificationDate.Should().HaveValue("because the first call to MarkAsModified should set the ModifiedDate");

            System.Threading.Thread.Sleep(10); // Mała pauza, aby zapewnić, że czasy będą różne

            // Wykonanie - Druga modyfikacja
            entity.MarkAsModified(secondModifier);

            // Sprawdzenie
            entity.ModifiedDate.Should().HaveValue("because the second call to MarkAsModified should also set the ModifiedDate");

            // Porównaj wartości .Value - teraz jest to bezpieczne po asercjach .HaveValue()
            // Sprawdzamy, czy druga data modyfikacji jest późniejsza niż pierwsza.
            entity.ModifiedDate.Value.Should().BeAfter(firstModificationDate.Value);

            entity.ModifiedBy.Should().Be(secondModifier);
            entity.ModifiedBy.Should().NotBe(firstModifiedBy, "because ModifiedBy should be updated to the latest modifier");
        }
    }
}