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
            // Arrange
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
                CreatedBy = "test_user",
                IsActive = true
            };

            // Act
            await _repository.AddAsync(channel);
            await SaveChangesAsync();

            // Assert
            var savedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            savedChannel.Should().NotBeNull();
            savedChannel!.DisplayName.Should().Be("Ogłoszenia");
            savedChannel.TeamId.Should().Be(team.Id);
            savedChannel.Status.Should().Be(ChannelStatus.Active);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectChannel()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "General", true);

            // Act
            var result = await _repository.GetByIdAsync(channel.Id);

            // Assert
            result.Should().NotBeNull();
            result!.DisplayName.Should().Be("General");
            result.IsGeneral.Should().BeTrue();
            result.TeamId.Should().Be(team.Id);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllChannels()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team1 = await CreateTeamAsync("Team A");
            var team2 = await CreateTeamAsync("Team B");
            
            var channels = new List<Channel>
            {
                await CreateChannelAsync(team1.Id, "General", true),
                await CreateChannelAsync(team1.Id, "Announcements"),
                await CreateChannelAsync(team2.Id, "General", true),
                await CreateChannelAsync(team2.Id, "Resources")
            };

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().HaveCount(4);
            result.Select(c => c.DisplayName).Should().Contain(new[] { "General", "Announcements", "Resources" });
        }

        [Fact]
        public async Task FindAsync_ShouldReturnFilteredChannels()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            
            // Explicite tworzenie kanałów
            var channel1 = await CreateChannelAsync(team.Id, "Public Channel 1", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: true);
            var channel2 = await CreateChannelAsync(team.Id, "Public Channel 2", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: true);
            var channel3 = await CreateChannelAsync(team.Id, "Private Channel", isGeneral: false, isPrivate: true, status: ChannelStatus.Active, isActive: true);
            var channel4 = await CreateChannelAsync(team.Id, "Inactive Channel", isGeneral: false, isPrivate: false, status: ChannelStatus.Active, isActive: false);

            await SaveChangesAsync();
            
            // Act
            var publicChannels = await _repository.FindAsync(c => !c.IsPrivate && c.IsActive);
            
            // Assert
            publicChannels.Should().HaveCount(2, "Powinna znaleźć 2 publiczne aktywne kanały");
            publicChannels.Should().Contain(c => c.DisplayName == "Public Channel 1");
            publicChannels.Should().Contain(c => c.DisplayName == "Public Channel 2");
            publicChannels.Should().NotContain(c => c.DisplayName == "Private Channel", "bo jest prywatny");
            publicChannels.Should().NotContain(c => c.DisplayName == "Inactive Channel", "bo nie jest aktywny");
        }


        [Fact]
        public async Task Update_ShouldModifyChannelData()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "Original Channel", false, false, ChannelStatus.Active);

            // Act
            channel.DisplayName = "Updated Channel";  // Zmieniono z 'Name' na 'DisplayName'
            channel.Description = "Updated description";
            channel.IsPrivate = true;
            channel.Status = ChannelStatus.Archived;
            channel.MarkAsModified("updater");

            _repository.Update(channel);
            await SaveChangesAsync();

            // Assert
            var updatedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            updatedChannel.Should().NotBeNull();
            updatedChannel!.DisplayName.Should().Be("Updated Channel");  // Zmieniono z 'Name' na 'DisplayName'
            updatedChannel.Description.Should().Be("Updated description");
            updatedChannel.IsPrivate.Should().BeTrue();
            updatedChannel.Status.Should().Be(ChannelStatus.Archived);
            updatedChannel.ModifiedBy.Should().Be("system@teamsmanager.local");
            updatedChannel.ModifiedDate.Should().NotBeNull();
        }

        [Fact]
        public async Task Delete_ShouldRemoveChannelFromDatabase()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync();
            var channel = await CreateChannelAsync(team.Id, "To Delete");

            // Act
            _repository.Delete(channel);
            await SaveChangesAsync();

            // Assert
            var deletedChannel = await Context.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            deletedChannel.Should().BeNull();
        }

        [Fact]
        public async Task ComplexScenario_TeamWithMultipleChannels()
        {
            // Arrange
            await CleanDatabaseAsync();
            var team = await CreateTeamAsync("Complex Team");
            
            // Utwórz różne typy kanałów
            var generalChannel = await CreateChannelAsync(team.Id, "General", true, false, ChannelStatus.Active);
            var announcementsChannel = await CreateChannelAsync(team.Id, "Announcements", false, false, ChannelStatus.Active, true, true);
            var privateChannel = await CreateChannelAsync(team.Id, "Private Discussion", false, true, ChannelStatus.Active);
            var archivedChannel = await CreateChannelAsync(team.Id, "Old Project", false, false, ChannelStatus.Archived);

            // Act - pobierz wszystkie aktywne kanały zespołu
            var activeChannels = await _repository.FindAsync(c => 
                c.TeamId == team.Id && 
                c.Status != ChannelStatus.Archived && 
                c.IsActive);

            // Assert
            activeChannels.Should().HaveCount(3);
            activeChannels.Should().Contain(c => c.IsGeneral); // powinien zawierać kanał główny
            activeChannels.Should().Contain(c => c.IsPrivate); // powinien zawierać kanał prywatny
            activeChannels.Should().Contain(c => c.IsReadOnly); // powinien zawierać kanał tylko do odczytu
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
                CreatedBy = "test_user",
                IsActive = true
            };
            await Context.Teams.AddAsync(team);
            await Context.SaveChangesAsync();
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
                ChannelType = isGeneral ? "General" : "Standard",
                Status = status,
                IsGeneral = isGeneral,
                IsPrivate = isPrivate,
                IsReadOnly = isReadOnly,
                CreatedBy = "test_user",
                IsActive = isActive
            };
            await Context.Channels.AddAsync(channel);
            await Context.SaveChangesAsync();
            return channel;
        }

        #endregion
    }
} 