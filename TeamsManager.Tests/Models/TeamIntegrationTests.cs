using FluentAssertions;
using TeamsManager.Core.Models;

namespace TeamsManager.Tests.Models
{
    public class TeamIntegrationTests
    {
        [Fact]
        public void Team_WhenAddingMembers_ShouldMaintainCorrectRelationships()
        {
            // Przygotowanie
            var team = new Team
            {
                Id = "team-123",
                DisplayName = "Zespół Deweloperski",
                Owner = "manager@firma.com"
            };

            var member1 = new TeamMember
            {
                Id = "member-1",
                Email = "jan.kowalski@firma.com",
                DisplayName = "Jan Kowalski",
                Role = TeamMemberRole.Member,
                TeamId = team.Id,
                Team = team
            };

            var member2 = new TeamMember
            {
                Id = "member-2",
                Email = "anna.nowak@firma.com",
                DisplayName = "Anna Nowak",
                Role = TeamMemberRole.Owner,
                TeamId = team.Id,
                Team = team
            };

            // Wykonanie
            team.Members.Add(member1);
            team.Members.Add(member2);

            // Sprawdzenie
            team.Members.Should().HaveCount(2);
            team.Members.Should().Contain(member1);
            team.Members.Should().Contain(member2);

            // Sprawdzenie relacji zwrotnych
            member1.Team.Should().Be(team);
            member1.TeamId.Should().Be(team.Id);
            member2.Team.Should().Be(team);
            member2.TeamId.Should().Be(team.Id);
        }

        [Fact]
        public void Team_WhenAddingChannels_ShouldMaintainCorrectRelationships()
        {
            // Przygotowanie
            var team = new Team
            {
                Id = "team-456",
                DisplayName = "Zespół Marketingu"
            };

            var generalChannel = new Channel
            {
                Id = "channel-1",
                DisplayName = "Ogólny",
                Description = "Główny kanał zespołu",
                TeamId = team.Id,
                Team = team
            };

            var projectsChannel = new Channel
            {
                Id = "channel-2",
                DisplayName = "Projekty",
                Description = "Omawianie bieżących projektów",
                TeamId = team.Id,
                Team = team
            };

            // Wykonanie
            team.Channels.Add(generalChannel);
            team.Channels.Add(projectsChannel);

            // Sprawdzenie
            team.Channels.Should().HaveCount(2);
            team.Channels.Should().Contain(generalChannel);
            team.Channels.Should().Contain(projectsChannel);

            // Sprawdzenie relacji zwrotnych
            generalChannel.Team.Should().Be(team);
            generalChannel.TeamId.Should().Be(team.Id);
            projectsChannel.Team.Should().Be(team);
            projectsChannel.TeamId.Should().Be(team.Id);
        }

        [Fact]
        public void Team_WithMembersAndChannels_ShouldMaintainAllRelationships()
        {
            // Przygotowanie - kompletny zespół
            var team = new Team
            {
                Id = "team-789",
                DisplayName = "Pełny Zespół",
                Description = "Zespół z członkami i kanałami",
                Owner = "leader@firma.com",
                CreatedDate = DateTime.Now,
                IsArchived = false
            };

            // Dodanie członków
            var owner = new TeamMember
            {
                Id = "owner-1",
                Email = "leader@firma.com",
                DisplayName = "Lider Zespołu",
                Role = TeamMemberRole.Owner,
                TeamId = team.Id,
                Team = team,
                AddedDate = DateTime.Now
            };

            var member = new TeamMember
            {
                Id = "member-1",
                Email = "pracownik@firma.com",
                DisplayName = "Pracownik",
                Role = TeamMemberRole.Member,
                TeamId = team.Id,
                Team = team,
                AddedDate = DateTime.Now
            };

            // Dodanie kanałów
            var channel = new Channel
            {
                Id = "channel-1",
                DisplayName = "Główny",
                Description = "Kanał główny",
                TeamId = team.Id,
                Team = team,
                CreatedDate = DateTime.Now
            };

            // Wykonanie
            team.Members.Add(owner);
            team.Members.Add(member);
            team.Channels.Add(channel);

            // Sprawdzenie kompletnych relacji
            team.Members.Should().HaveCount(2);
            team.Channels.Should().HaveCount(1);

            // Sprawdzenie ról
            team.Members.Where(m => m.Role == TeamMemberRole.Owner).Should().HaveCount(1);
            team.Members.Where(m => m.Role == TeamMemberRole.Member).Should().HaveCount(1);

            // Sprawdzenie czy owner zespołu jest wśród członków
            team.Members.Should().Contain(m => m.Email == team.Owner);
        }
    }
}