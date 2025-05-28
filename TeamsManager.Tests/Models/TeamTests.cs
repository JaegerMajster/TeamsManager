using FluentAssertions;
using TeamsManager.Core.Models;

namespace TeamsManager.Tests.Models
{
    public class TeamTests
    {
        [Fact]
        public void Team_WhenCreated_ShouldHaveEmptyCollections()
        {
            // Przygotowanie i Wykonanie
            var team = new Team();

            // Sprawdzenie
            team.Members.Should().NotBeNull();
            team.Members.Should().BeEmpty();
            team.Channels.Should().NotBeNull();
            team.Channels.Should().BeEmpty();
        }

        [Fact]
        public void Team_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i wykonanie
            var team = new Team();

            // Sprawdzenie
            team.Id.Should().Be(string.Empty);
            team.DisplayName.Should().Be(string.Empty);
            team.Description.Should().Be(string.Empty);
            team.Owner.Should().Be(string.Empty);
            team.IsArchived.Should().BeFalse();
            team.CreatedDate.Should().Be(default(DateTime));
        }

        [Fact]
        public void Team_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var team = new Team();
            var teamId = "team-123";
            var displayName = "Test Team";
            var description = "Test Description";
            var owner = "owner@test.com";
            var createdDate = DateTime.Now;

            // Wykonanie
            team.Id = teamId;
            team.DisplayName = displayName;
            team.Description = description;
            team.Owner = owner;
            team.CreatedDate = createdDate;
            team.IsArchived = true;

            // Sprawdzenie
            team.Id.Should().Be(teamId);
            team.DisplayName.Should().Be(displayName);
            team.Description.Should().Be(description);
            team.Owner.Should().Be(owner);
            team.CreatedDate.Should().Be(createdDate);
            team.IsArchived.Should().BeTrue();
        }
    }
}