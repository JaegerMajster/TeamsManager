using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories; // Upewnij się, że GenericRepository jest tutaj
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]
    public class ChannelRepositoryTests : RepositoryTestBase
    {
        private readonly GenericRepository<Channel> _repository; // Używamy GenericRepository<Channel>

        public ChannelRepositoryTests()
        {
            _repository = new GenericRepository<Channel>(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddChannelToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync(); // Metoda pomocnicza z poprzednich testów repozytorium

            var channel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Ogłoszenia",
                Description = "Kanał do publikowania ogłoszeń",
                TeamId = team.Id,
                ChannelType = "Standard",
                Status = ChannelStatus.Active, // Ustawiamy Status
                IsGeneral = false,
                IsPrivate = false
                // IsActive jest teraz obliczane
            };

            // Działanie
            await _repository.AddAsync(channel);
            await SaveChangesAsync(); // To ustawi pola audytu

            // Weryfikacja
            var savedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            savedChannel.Should().NotBeNull();
            savedChannel!.DisplayName.Should().Be("Ogłoszenia");
            savedChannel.TeamId.Should().Be(team.Id);
            savedChannel.Status.Should().Be(ChannelStatus.Active);
            savedChannel.IsActive.Should().BeTrue(); // Sprawdzenie obliczeniowego IsActive
            savedChannel.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectChannel()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "General", isGeneral: true, status: ChannelStatus.Active);

            // Działanie
            var result = await _repository.GetByIdAsync(channel.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("General");
            result.IsGeneral.Should().BeTrue();
            result.Status.Should().Be(ChannelStatus.Active);
            result.IsActive.Should().BeTrue();
            result.TeamId.Should().Be(team.Id);
            result.CreatedBy.Should().Be("test_user_integration_base_default");
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllChannels_IncludingInactiveBasedOnStatus()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team1 = await CreateTeamAsync("Team A");
            var team2 = await CreateTeamAsync("Team B");

            var channels = new List<Channel>
            {
                await CreateChannelAsync(team1.Id, "General TeamA", isGeneral: true, status: ChannelStatus.Active),
                await CreateChannelAsync(team1.Id, "Announcements TeamA", status: ChannelStatus.Active),
                await CreateChannelAsync(team2.Id, "General TeamB", isGeneral: true, status: ChannelStatus.Active),
                await CreateChannelAsync(team2.Id, "Archived TeamB Channel", status: ChannelStatus.Archived) // Kanał zarchiwizowany
            };

            // Działanie
            var result = await _repository.GetAllAsync(); // GenericRepository.GetAllAsync() nie filtruje po IsActive ani Status

            // Weryfikacja
            result.Should().HaveCount(4);
            result.Should().Contain(c => c.DisplayName == "Archived TeamB Channel" && c.Status == ChannelStatus.Archived && !c.IsActive);
            result.Where(c => c.Status == ChannelStatus.Active).Should().HaveCount(3);
            result.ToList().ForEach(c => c.CreatedBy.Should().Be("test_user_integration_base_default"));
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredChannels_BasedOnStatusAndOtherCriteria()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();

            var channel1 = await CreateChannelAsync(team.Id, "Public Active 1", isGeneral: false, isPrivate: false, status: ChannelStatus.Active);
            var channel2 = await CreateChannelAsync(team.Id, "Public Active 2", isGeneral: false, isPrivate: false, status: ChannelStatus.Active);
            var channel3 = await CreateChannelAsync(team.Id, "Private Active", isGeneral: false, isPrivate: true, status: ChannelStatus.Active);
            var channel4Archived = await CreateChannelAsync(team.Id, "Public Archived", isGeneral: false, isPrivate: false, status: ChannelStatus.Archived);

            // Działanie: znajdź publiczne, aktywne kanały (Status == Active)
            var publicActiveChannels = await _repository.FindAsync(c => !c.IsPrivate && c.Status == ChannelStatus.Active);
            // Alternatywnie, używając nowego IsActive:
            // var publicActiveChannels = await _repository.FindAsync(c => !c.IsPrivate && c.IsActive);


            // Weryfikacja
            publicActiveChannels.Should().HaveCount(2);
            publicActiveChannels.Select(c => c.DisplayName).Should().BeEquivalentTo(new[] { "Public Active 1", "Public Active 2" });
            publicActiveChannels.Should().NotContain(c => c.DisplayName == "Private Active");
            publicActiveChannels.Should().NotContain(c => c.DisplayName == "Public Archived");

            // Działanie: znajdź wszystkie zarchiwizowane kanały
            var archivedChannels = await _repository.FindAsync(c => c.Status == ChannelStatus.Archived);
            // Alternatywnie: var archivedChannels = await _repository.FindAsync(c => !c.IsActive && c.Status == ChannelStatus.Archived);
            // (choć c.IsActive już implikuje c.Status != ChannelStatus.Active)

            // Weryfikacja
            archivedChannels.Should().HaveCount(1);
            archivedChannels.First().DisplayName.Should().Be("Public Archived");
        }


        [Fact]
        public async Task Update_ShouldModifyChannelData_AndAuditFields()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "Original Channel", status: ChannelStatus.Active);
            var initialCreatedBy = channel.CreatedBy;
            var initialCreatedDate = channel.CreatedDate;

            var currentUser = "channel_updater_repo";
            SetTestUser(currentUser);

            // Działanie
            // Pobieramy świeżą instancję do aktualizacji, jeśli repozytorium nie śledzi zmian z CreateChannelAsync
            var channelToUpdate = await _repository.GetByIdAsync(channel.Id);
            channelToUpdate.Should().NotBeNull();

            channelToUpdate!.DisplayName = "Updated Channel";
            channelToUpdate.Description = "Updated description";
            channelToUpdate.IsPrivate = true;
            // Zmieniamy Status, co powinno wpłynąć na IsActive
            channelToUpdate.Status = ChannelStatus.Archived;
            // Pola związane z archiwizacją powinny być ustawiane przez logikę modelu (Archive/Restore)
            // ale dla testu repozytorium możemy ustawić je ręcznie, aby sprawdzić zapis
            channelToUpdate.StatusChangeDate = DateTime.UtcNow;
            channelToUpdate.StatusChangedBy = currentUser;
            channelToUpdate.StatusChangeReason = "Zmieniono status w teście repozytorium";


            _repository.Update(channelToUpdate);
            await SaveChangesAsync(); // To powinno ustawić ModifiedBy i ModifiedDate

            // Weryfikacja
            var updatedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            updatedChannel.Should().NotBeNull();
            updatedChannel!.DisplayName.Should().Be("Updated Channel");
            updatedChannel.Description.Should().Be("Updated description");
            updatedChannel.IsPrivate.Should().BeTrue();
            updatedChannel.Status.Should().Be(ChannelStatus.Archived);
            updatedChannel.IsActive.Should().BeFalse(); // Obliczone na podstawie Status

            updatedChannel.CreatedBy.Should().Be(initialCreatedBy);
            updatedChannel.CreatedDate.Should().Be(initialCreatedDate);
            updatedChannel.ModifiedBy.Should().Be(currentUser);
            updatedChannel.ModifiedDate.Should().NotBeNull();

            ResetTestUser();
        }

        [Fact]
        public async Task Delete_ShouldRemoveChannelFromDatabase_ForGenericRepository()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "To Delete");

            // Działanie
            _repository.Delete(channel); // GenericRepository wykonuje fizyczne usunięcie
            await SaveChangesAsync();

            // Weryfikacja
            var deletedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            deletedChannel.Should().BeNull();
        }


        #region Helper Methods

        private async Task<Team> CreateTeamAsync(string displayName = "Test Team Channel")
        {
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Owner = "owner@test.com",
                Status = TeamStatus.Active, // Domyślnie aktywny
                Visibility = TeamVisibility.Private
            };
            await Context.Teams.AddAsync(team);
            await SaveChangesAsync();
            return team;
        }

        // Metoda pomocnicza nie ustawia już BaseEntity.IsActive bezpośrednio, tylko Status
        private async Task<Channel> CreateChannelAsync(
            string teamId,
            string displayName,
            bool isGeneral = false,
            bool isPrivate = false,
            ChannelStatus status = ChannelStatus.Active, // Domyślnie Active
            bool isReadOnly = false) // Usunięto parametr isActive
        {
            var channel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Opis kanału {displayName}",
                TeamId = teamId,
                ChannelType = isGeneral ? "General" : (isPrivate ? "Private" : "Standard"),
                Status = status, // Ustawiamy Status
                IsGeneral = isGeneral,
                IsPrivate = isPrivate,
                IsReadOnly = isReadOnly
                // IsActive jest teraz obliczane na podstawie Status
            };
            await Context.Channels.AddAsync(channel);
            await Context.SaveChangesAsync();
            return channel;
        }

        #endregion
    }
}