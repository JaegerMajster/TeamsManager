using System;
using FluentAssertions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class ChannelTests
    {
        [Fact]
        public void Channel_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var channel = new Channel();

            // Sprawdzenie
            channel.Id.Should().Be(string.Empty);
            channel.DisplayName.Should().Be(string.Empty);
            channel.Description.Should().Be(string.Empty);

            channel.TeamId.Should().Be(string.Empty);
            channel.Team.Should().BeNull();

            // Sprawdzenie Status i obliczeniowego IsActive
            channel.Status.Should().Be(ChannelStatus.Active); // Domyślny status
            channel.IsActive.Should().BeTrue(); // Obliczone na podstawie Status
            channel.IsCurrentlyActive.Should().BeTrue(); // Również oparte na nowym IsActive

            channel.StatusChangeDate.Should().BeNull();
            channel.StatusChangedBy.Should().BeNull();
            channel.StatusChangeReason.Should().BeNull();

            channel.ChannelType.Should().Be("Standard");
            channel.IsGeneral.Should().BeFalse();
            channel.IsPrivate.Should().BeFalse();
            channel.IsReadOnly.Should().BeFalse();
        }

        [Fact]
        public void Channel_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var channel = new Channel();
            var channelId = "channel-123";
            var displayName = "Ogólny";
            var description = "Główny kanał zespołu";
            var teamId = "team-456";
            var createdDate = DateTime.UtcNow;
            var statusChangedBy = "user@test.com";
            var statusChangeReason = "Testowa zmiana statusu";

            // Wykonanie
            channel.Id = channelId;
            channel.DisplayName = displayName;
            channel.Description = description;
            channel.TeamId = teamId;
            // channel.CreatedDate = createdDate; // CreatedDate jest z BaseEntity, nie ustawiamy go bezpośrednio w ten sposób w teście modelu
            channel.Status = ChannelStatus.Archived; // Testujemy ustawienie Status
            channel.StatusChangeDate = createdDate;
            channel.StatusChangedBy = statusChangedBy;
            channel.StatusChangeReason = statusChangeReason;
            channel.ChannelType = "Private";
            channel.IsGeneral = true;
            channel.IsPrivate = true;
            channel.IsReadOnly = true;
            // ((BaseEntity)channel).IsActive = false; // Bezpośrednie ustawienie IsActive z BaseEntity nie jest już głównym mechanizmem

            // Sprawdzenie
            channel.Id.Should().Be(channelId);
            channel.DisplayName.Should().Be(displayName);
            channel.Description.Should().Be(description);
            channel.TeamId.Should().Be(teamId);
            // channel.CreatedDate.Should().Be(createdDate); // Ta asercja może być problematyczna bez kontroli nad BaseEntity
            channel.Status.Should().Be(ChannelStatus.Archived);
            channel.IsActive.Should().BeFalse(); // Obliczone na podstawie Status
            channel.IsCurrentlyActive.Should().BeFalse(); // Obliczone
            channel.StatusChangeDate.Should().Be(createdDate);
            channel.StatusChangedBy.Should().Be(statusChangedBy);
            channel.StatusChangeReason.Should().Be(statusChangeReason);
            channel.ChannelType.Should().Be("Private");
            channel.IsGeneral.Should().BeTrue();
            channel.IsPrivate.Should().BeTrue();
            channel.IsReadOnly.Should().BeTrue();
        }

        [Theory]
        [InlineData("Ogólny", "Główny kanał")]
        [InlineData("Projekty", "Kanał do omawiania projektów")]
        [InlineData("", "")]
        public void Channel_WhenSettingNameAndDescription_ShouldRetainValues(string name, string description)
        {
            // Przygotowanie
            var channel = new Channel();

            // Wykonanie
            channel.DisplayName = name;
            channel.Description = description;

            // Sprawdzenie
            channel.DisplayName.Should().Be(name);
            channel.Description.Should().Be(description);
        }

        [Fact]
        public void Archive_WhenChannelIsGeneral_ShouldThrowInvalidOperationException()
        {
            // Przygotowanie
            var channel = new Channel { IsGeneral = true, Status = ChannelStatus.Active };

            // Wykonanie
            Action act = () => channel.Archive("Test archiwizacji", "admin@test.com");

            // Sprawdzenie
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Nie można zarchiwizować kanału ogólnego.");
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.IsActive.Should().BeTrue(); // Powiązane ze Statusem
        }

        [Fact]
        public void Archive_WhenChannelIsActive_ShouldSetStatusToArchivedAndAuditFields()
        {
            // Przygotowanie
            var channel = new Channel { DisplayName = "Kanał do archiwizacji", Status = ChannelStatus.Active, IsGeneral = false };
            var archiver = "admin@test.com";
            var reason = "Test archiwizacji";
            var initialModifiedBy = channel.ModifiedBy; // Z BaseEntity
            var initialModifiedDate = channel.ModifiedDate; // Z BaseEntity

            // Wykonanie
            channel.Archive(reason, archiver);

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Archived);
            channel.IsActive.Should().BeFalse(); // Obliczone
            channel.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            channel.StatusChangedBy.Should().Be(archiver);
            channel.StatusChangeReason.Should().Be(reason);
            channel.ModifiedBy.Should().Be(archiver); // Z BaseEntity.MarkAsModified
            channel.ModifiedDate.Should().HaveValue();
            if (initialModifiedDate.HasValue)
            {
                channel.ModifiedDate.Should().BeAfter(initialModifiedDate.Value);
            }
            else
            {
                channel.ModifiedDate.Should().NotBeNull();
            }
        }

        [Fact]
        public void Archive_WhenChannelIsAlreadyArchived_ShouldDoNothing()
        {
            // Przygotowanie
            var initialStatusChangeDate = DateTime.UtcNow.AddMinutes(-5);
            var channel = new Channel
            {
                DisplayName = "Już zarchiwizowany",
                Status = ChannelStatus.Archived, // Już zarchiwizowany
                IsGeneral = false,
                StatusChangeDate = initialStatusChangeDate,
                StatusChangedBy = "initial_archiver",
                StatusChangeReason = "Initial reason"
            };
            var initialModifiedDate = channel.ModifiedDate;

            // Wykonanie
            channel.Archive("Próba ponownej archiwizacji", "admin_again@test.com");

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Archived);
            channel.IsActive.Should().BeFalse();
            channel.StatusChangeDate.Should().Be(initialStatusChangeDate); // Nie powinno się zmienić
            channel.StatusChangedBy.Should().Be("initial_archiver");
            channel.StatusChangeReason.Should().Be("Initial reason");
            // ModifiedDate z BaseEntity NIE powinno się zmienić, bo metoda powinna wyjść na początku
            channel.ModifiedDate.Should().Be(initialModifiedDate);
        }

        [Fact]
        public void Restore_WhenChannelIsArchived_ShouldSetStatusToActiveAndAuditFields()
        {
            // Przygotowanie
            var channel = new Channel
            {
                DisplayName = "Kanał do przywrócenia",
                Status = ChannelStatus.Archived, // Ustawiamy jako zarchiwizowany
                IsGeneral = false,
                StatusChangeReason = "Został zarchiwizowany"
            };
            var restorer = "admin@test.com";
            var initialModifiedDate = channel.ModifiedDate;

            // Wykonanie
            channel.Restore(restorer);

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.IsActive.Should().BeTrue(); // Obliczone
            channel.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            channel.StatusChangedBy.Should().Be(restorer);
            channel.StatusChangeReason.Should().Be("Przywrócono z archiwum");
            channel.ModifiedBy.Should().Be(restorer);
            channel.ModifiedDate.Should().HaveValue();
            if (initialModifiedDate.HasValue)
            {
                channel.ModifiedDate.Should().BeAfter(initialModifiedDate.Value);
            }
            else
            {
                channel.ModifiedDate.Should().NotBeNull();
            }
        }

        [Fact]
        public void Restore_WhenChannelIsAlreadyActive_ShouldDoNothing()
        {
            // Przygotowanie
            var initialStatusChangeDate = DateTime.UtcNow.AddMinutes(-5);
            var channel = new Channel
            {
                DisplayName = "Już aktywny",
                Status = ChannelStatus.Active, // Już aktywny
                IsGeneral = false,
                StatusChangeDate = initialStatusChangeDate,
                StatusChangedBy = "initial_restorer",
                StatusChangeReason = "Initial reason for active"
            };
            var initialModifiedDate = channel.ModifiedDate;

            // Wykonanie
            channel.Restore("admin_again@test.com");

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.IsActive.Should().BeTrue();
            channel.StatusChangeDate.Should().Be(initialStatusChangeDate); // Nie powinno się zmienić
            channel.StatusChangedBy.Should().Be("initial_restorer");
            channel.StatusChangeReason.Should().Be("Initial reason for active");
            channel.ModifiedDate.Should().Be(initialModifiedDate); // Nie powinno się zmienić
        }

        // Test dla IsCurrentlyActive
        [Fact]
        public void IsCurrentlyActive_ShouldReflectNewIsActiveLogic()
        {
            var channel = new Channel();

            channel.Status = ChannelStatus.Active;
            channel.IsCurrentlyActive.Should().BeTrue(); // Bo IsActive (new) jest true

            channel.Status = ChannelStatus.Archived;
            channel.IsCurrentlyActive.Should().BeFalse(); // Bo IsActive (new) jest false
        }

        // Test dla StatusDescription
        [Fact]
        public void StatusDescription_ShouldCorrectlyDescribeStatus()
        {
            var channel = new Channel();

            channel.Status = ChannelStatus.Active;
            channel.IsPrivate = false;
            channel.IsReadOnly = false;
            channel.StatusDescription.Should().Be("Aktywny");

            channel.Status = ChannelStatus.Active;
            channel.IsPrivate = true;
            channel.StatusDescription.Should().Be("Prywatny");

            channel.Status = ChannelStatus.Active;
            channel.IsPrivate = false;
            channel.IsReadOnly = true;
            channel.StatusDescription.Should().Be("Tylko do odczytu");

            channel.Status = ChannelStatus.Archived;
            channel.StatusDescription.Should().Be("Zarchiwizowany");

            // Test dla przypadku, gdyby base.IsActive było false, a Status był Active (teoretycznie niemożliwe z nową logiką)
            // W tym celu musielibyśmy mieć dostęp do setter-a base.IsActive
            // Jednak z nową logiką, jeśli Channel.IsActive jest false, to znaczy, że Status nie jest Active.
            // Poniższy fragment jest trudny do przetestowania bez bezpośredniej manipulacji base.IsActive
            // przy jednoczesnym utrzymaniu Channel.Status = ChannelStatus.Active.
            // Założenie: Nowe `Channel.IsActive` jest jedynym źródłem prawdy o aktywności kanału bazującej na statusie.

            // var channelNonStandard = new Channel { Status = (ChannelStatus)99 }; // Jakaś nieznana wartość
            // channelNonStandard.StatusDescription.Should().Be("Nieznany status"); // Zakładając, że enum nie ma tej wartości
        }
    }
}