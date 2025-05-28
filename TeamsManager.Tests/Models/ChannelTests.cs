using FluentAssertions;
using TeamsManager.Core.Models;

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
            channel.CreatedDate.Should().Be(default(DateTime));
            channel.TeamId.Should().Be(string.Empty);
            channel.Team.Should().BeNull();
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
            var createdDate = DateTime.Now;

            // Wykonanie
            channel.Id = channelId;
            channel.DisplayName = displayName;
            channel.Description = description;
            channel.TeamId = teamId;
            channel.CreatedDate = createdDate;

            // Sprawdzenie
            channel.Id.Should().Be(channelId);
            channel.DisplayName.Should().Be(displayName);
            channel.Description.Should().Be(description);
            channel.TeamId.Should().Be(teamId);
            channel.CreatedDate.Should().Be(createdDate);
        }

        [Theory]
        [InlineData("Ogólny", "Główny kanał")]
        [InlineData("Projekty", "Kanał do omawiania projektów")]
        [InlineData("", "")] // Przypadek brzegowy - puste wartości
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
    }
}