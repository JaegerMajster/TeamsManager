using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Data.Repositories;
using Xunit;

namespace TeamsManager.Tests.Repositories
{
    [Collection("Sequential")]
    public class ChannelRepositoryTests : RepositoryTestBase
    {
        private readonly GenericRepository<Channel> _repository;

        public ChannelRepositoryTests()
        {
            _repository = new GenericRepository<Channel>(Context);
        }

        [Fact]
        public async Task AddAsync_ShouldAddChannelToDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();

            var channel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "Ogłoszenia",
                Description = "Kanał do publikowania ogłoszeń",
                TeamId = team.Id,
                ChannelType = "Standard",
                Status = ChannelStatus.Active,
                IsGeneral = false,
                IsPrivate = false,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };

            // Działanie
            await _repository.AddAsync(channel);
            await SaveChangesAsync();

            // Weryfikacja
            var savedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            savedChannel.Should().NotBeNull();
            savedChannel!.DisplayName.Should().Be("Ogłoszenia");
            savedChannel.TeamId.Should().Be(team.Id);
            savedChannel.Status.Should().Be(ChannelStatus.Active);
            savedChannel.CreatedBy.Should().Be("test_user");
            savedChannel.CreatedDate.Should().NotBe(default(DateTime));
            savedChannel.ModifiedBy.Should().BeNull();
            savedChannel.ModifiedDate.Should().BeNull();
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectChannel()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "General", true); // Ta metoda używa SaveChangesAsync, więc audyt jest stosowany

            // Działanie
            var result = await _repository.GetByIdAsync(channel.Id);

            // Weryfikacja
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("General");
            result.IsGeneral.Should().BeTrue();
            result.TeamId.Should().Be(team.Id);
            result.CreatedBy.Should().Be("test_user");
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllChannels()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team1 = await CreateTeamAsync("Team A");
            var team2 = await CreateTeamAsync("Team B");

            // Kanały są tworzone i zapisywane przez CreateChannelAsync, więc będą miały ustawione pola audytu
            var channels = new List<Channel>
            {
                await CreateChannelAsync(team1.Id, "General", true),
                await CreateChannelAsync(team1.Id, "Announcements"),
                await CreateChannelAsync(team2.Id, "General", true),
                await CreateChannelAsync(team2.Id, "Resources")
            };

            // Działanie
            var result = await _repository.GetAllAsync();

            // Weryfikacja
            result.Should().HaveCount(4);
            result.Select(c => c.DisplayName).Should().Contain(new[] { "General", "Announcements", "Resources" });
            result.ToList().ForEach(c => c.CreatedBy.Should().Be("test_user")); // Wszystkie powinny mieć tego samego CreatedBy
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredChannels()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();

            var channel1 = await CreateChannelAsync(team.Id, "Public Channel 1", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: true);
            var channel2 = await CreateChannelAsync(team.Id, "Public Channel 2", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: true);
            var channel3 = await CreateChannelAsync(team.Id, "Private Channel", isGeneral: false, isPrivate: true, status: ChannelStatus.Active, isActive: true);
            var channel4 = await CreateChannelAsync(team.Id, "Inactive Channel", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: false); // Ten nie powinien być aktywny przez IsActive=false

            // Działanie
            var publicChannels = await _repository.FindAsync(c => !c.IsPrivate && c.IsActive);

            // Weryfikacja
            publicChannels.Should().HaveCount(2, "Powinna znaleźć 2 publiczne aktywne kanały");
            publicChannels.Should().Contain(c => c.DisplayName == "Public Channel 1");
            publicChannels.Should().Contain(c => c.DisplayName == "Public Channel 2");
            publicChannels.Should().NotContain(c => c.DisplayName == "Private Channel", "bo jest prywatny");
            publicChannels.Should().NotContain(c => c.DisplayName == "Inactive Channel", "bo nie jest aktywny (IsActive=false)");
        }


        [Fact]
        public async Task Update_ShouldModifyChannelData()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "Original Channel", false, false, ChannelStatus.Active);
            var initialCreatedBy = channel.CreatedBy;
            var initialCreatedDate = channel.CreatedDate;

            // Działanie
            channel.DisplayName = "Updated Channel";
            channel.Description = "Updated description";
            channel.IsPrivate = true;
            channel.Status = ChannelStatus.Archived;
            // channel.MarkAsModified("updater"); // Ta wartość zostanie nadpisana przez TestDbContext

            _repository.Update(channel);
            await SaveChangesAsync();

            // Weryfikacja
            var updatedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            updatedChannel.Should().NotBeNull();
            updatedChannel!.DisplayName.Should().Be("Updated Channel");
            updatedChannel.Description.Should().Be("Updated description");
            updatedChannel.IsPrivate.Should().BeTrue();
            updatedChannel.Status.Should().Be(ChannelStatus.Archived);
            updatedChannel.CreatedBy.Should().Be(initialCreatedBy);
            updatedChannel.CreatedDate.Should().Be(initialCreatedDate);
            updatedChannel.ModifiedBy.Should().Be("test_user"); // Oczekiwana wartość z TestDbContext
            updatedChannel.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldRemoveChannelFromDatabase()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "To Delete");

            // Działanie
            _repository.Delete(channel); // Fizyczne usunięcie
            await SaveChangesAsync();

            // Weryfikacja
            var deletedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            deletedChannel.Should().BeNull();
        }

        [Fact]
        public async Task ComplexScenario_TeamWithMultipleChannels()
        {
            // Przygotowanie
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync("Complex Team");

            var generalChannel = await CreateChannelAsync(team.Id, "General", true, false, ChannelStatus.Active);
            var announcementsChannel = await CreateChannelAsync(team.Id, "Announcements", false, false, ChannelStatus.Active, true, true);
            var privateChannel = await CreateChannelAsync(team.Id, "Private Discussion", false, true, ChannelStatus.Active);
            var archivedChannel = await CreateChannelAsync(team.Id, "Old Project", false, false, ChannelStatus.Archived);

            // Działanie
            var activeChannels = await _repository.FindAsync(c =>
                c.TeamId == team.Id &&
                c.Status == ChannelStatus.Active && // Zmieniono z c.Status != ChannelStatus.Archived dla jasności
                c.IsActive);

            // Weryfikacja
            activeChannels.Should().HaveCount(3);
            activeChannels.Should().Contain(c => c.IsGeneral);
            activeChannels.Should().Contain(c => c.IsPrivate);
            activeChannels.Should().Contain(c => c.IsReadOnly);
        }

        #region Helper Methods

        private async Task<Team> CreateTeamAsync(string displayName = "Test Team")
        {
            var team = new Team
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Opis zespołu {displayName}",
                Owner = "owner@test.com",
                Status = TeamStatus.Active,
                Visibility = TeamVisibility.Private,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync(); // Zapis z audytem
            return team;
        }

        private async Task<Channel> CreateChannelAsync(
            string teamId,
            string displayName,
            bool isGeneral = false,
            bool isPrivate = false,
            ChannelStatus status = ChannelStatus.Active,
            bool isActive = true,
            bool isReadOnly = false)
        {
            var channel = new Channel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = displayName,
                Description = $"Opis kanału {displayName}",
                TeamId = teamId,
                ChannelType = isGeneral ? "General" : (isPrivate ? "Private" : "Standard"), // Poprawka dla ChannelType
                Status = status,
                IsGeneral = isGeneral,
                IsPrivate = isPrivate,
                IsReadOnly = isReadOnly,
                // CreatedBy zostanie ustawione przez TestDbContext
                IsActive = isActive
            };
            await Context.Channels.AddAsync(channel);
            await Context.SaveChangesAsync(); // Zapis z audytem
            return channel;
        }

        #endregion
    }
}