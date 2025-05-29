using System; // Potrzebne dla DateTime
using FluentAssertions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Potrzebne dla ChannelStatus

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

            // NOWE ASERCJE dla statusu i powiązanych pól
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.StatusChangeDate.Should().BeNull();
            channel.StatusChangedBy.Should().BeNull();
            channel.StatusChangeReason.Should().BeNull();

            // Domyślne wartości dla innych nowych pól, jeśli mają znaczenie
            channel.ChannelType.Should().Be("Standard"); // Jak zdefiniowano w modelu
            channel.IsGeneral.Should().BeFalse();
            channel.IsPrivate.Should().BeFalse();
            channel.IsReadOnly.Should().BeFalse();
            // channel.IsArchived // USUNIĘTE - zastąpione przez Status
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
            var createdDate = DateTime.UtcNow; // Użyj UtcNow dla spójności
            var statusChangedBy = "user@test.com";
            var statusChangeReason = "Testowa zmiana statusu";

            // Wykonanie
            channel.Id = channelId;
            channel.DisplayName = displayName;
            channel.Description = description;
            channel.TeamId = teamId;
            channel.CreatedDate = createdDate; // To jest z BaseEntity, zwykle ustawiane przez SaveChanges
            channel.Status = ChannelStatus.Archived; // Testujemy ustawienie Status
            channel.StatusChangeDate = createdDate; // Przykład
            channel.StatusChangedBy = statusChangedBy;
            channel.StatusChangeReason = statusChangeReason;
            channel.ChannelType = "Private";
            channel.IsGeneral = true;
            channel.IsPrivate = true; // Zgodne z ChannelType = "Private"
            channel.IsReadOnly = true;


            // Sprawdzenie
            channel.Id.Should().Be(channelId);
            channel.DisplayName.Should().Be(displayName);
            channel.Description.Should().Be(description);
            channel.TeamId.Should().Be(teamId);
            channel.CreatedDate.Should().Be(createdDate);
            channel.Status.Should().Be(ChannelStatus.Archived); // Sprawdzenie Status
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
        [InlineData("", "")] // Przypadek brzegowy
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

        // ===== NOWE TESTY DLA LOGIKI ARCHIWIZACJI/PRZYWRACANIA =====

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
            channel.Status.Should().Be(ChannelStatus.Active); // Status nie powinien się zmienić
        }

        [Fact]
        public void Archive_WhenChannelIsActive_ShouldSetStatusToArchivedAndAuditFields()
        {
            // Przygotowanie
            var channel = new Channel { DisplayName = "Kanał do archiwizacji", Status = ChannelStatus.Active, IsGeneral = false };
            var archiver = "admin@test.com";
            var reason = "Test archiwizacji";
            var modificationDateBeforeArchive = channel.ModifiedDate; // Z BaseEntity

            // Wykonanie
            channel.Archive(reason, archiver);

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Archived);
            channel.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            channel.StatusChangedBy.Should().Be(archiver);
            channel.StatusChangeReason.Should().Be(reason);
            channel.ModifiedBy.Should().Be(archiver); // Z BaseEntity.MarkAsModified
            channel.ModifiedDate.Should().HaveValue();
            if (modificationDateBeforeArchive.HasValue)
            {
                channel.ModifiedDate.Should().BeAfter(modificationDateBeforeArchive.Value);
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
                Status = ChannelStatus.Archived,
                IsGeneral = false,
                StatusChangeDate = initialStatusChangeDate,
                StatusChangedBy = "initial_archiver",
                StatusChangeReason = "Initial reason"
            };

            // Wykonanie
            channel.Archive("Próba ponownej archiwizacji", "admin_again@test.com");

            // Sprawdzenie - status i pola audytu zmiany statusu nie powinny się zmienić
            channel.Status.Should().Be(ChannelStatus.Archived);
            channel.StatusChangeDate.Should().Be(initialStatusChangeDate);
            channel.StatusChangedBy.Should().Be("initial_archiver");
            channel.StatusChangeReason.Should().Be("Initial reason");
            // ModifiedDate i ModifiedBy z BaseEntity mogą się zmienić, jeśli MarkAsModified jest zawsze wołane,
            // ale logika `if (Status == ChannelStatus.Archived) return;` powinna zapobiec wywołaniu MarkAsModified
        }

        [Fact]
        public void Restore_WhenChannelIsArchived_ShouldSetStatusToActiveAndAuditFields()
        {
            // Przygotowanie
            var channel = new Channel
            {
                DisplayName = "Kanał do przywrócenia",
                Status = ChannelStatus.Archived,
                IsGeneral = false,
                StatusChangeReason = "Został zarchiwizowany" // Poprzedni powód
            };
            var restorer = "admin@test.com";
            var modificationDateBeforeRestore = channel.ModifiedDate;

            // Wykonanie
            channel.Restore(restorer);

            // Sprawdzenie
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            channel.StatusChangedBy.Should().Be(restorer);
            channel.StatusChangeReason.Should().Be("Przywrócono z archiwum");
            channel.ModifiedBy.Should().Be(restorer);
            channel.ModifiedDate.Should().HaveValue();
            if (modificationDateBeforeRestore.HasValue)
            {
                channel.ModifiedDate.Should().BeAfter(modificationDateBeforeRestore.Value);
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
                Status = ChannelStatus.Active,
                IsGeneral = false,
                StatusChangeDate = initialStatusChangeDate,
                StatusChangedBy = "initial_restorer",
                StatusChangeReason = "Initial reason for active"
            };

            // Wykonanie
            channel.Restore("admin_again@test.com");

            // Sprawdzenie - status i pola audytu zmiany statusu nie powinny się zmienić
            channel.Status.Should().Be(ChannelStatus.Active);
            channel.StatusChangeDate.Should().Be(initialStatusChangeDate);
            channel.StatusChangedBy.Should().Be("initial_restorer");
            channel.StatusChangeReason.Should().Be("Initial reason for active");
        }
    }
}