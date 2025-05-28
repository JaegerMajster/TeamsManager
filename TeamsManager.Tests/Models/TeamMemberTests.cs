using FluentAssertions;
using TeamsManager.Core.Models;

namespace TeamsManager.Tests.Models
{
    public class TeamMemberTests
    {
        [Fact]
        public void TeamMember_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var member = new TeamMember();

            // Sprawdzenie
            member.Id.Should().Be(string.Empty);
            member.Email.Should().Be(string.Empty);
            member.DisplayName.Should().Be(string.Empty);
            member.Role.Should().Be(TeamMemberRole.Member); // Domyślna rola
            member.AddedDate.Should().Be(default(DateTime));
            member.TeamId.Should().Be(string.Empty);
            member.Team.Should().BeNull();
        }

        [Fact]
        public void TeamMember_WhenSettingRole_ShouldAcceptValidRoles()
        {
            // Przygotowanie
            var member = new TeamMember();

            // Wykonanie i Sprawdzenie - Właściciel
            member.Role = TeamMemberRole.Owner;
            member.Role.Should().Be(TeamMemberRole.Owner);

            // Wykonanie i Sprawdzenie - Członek
            member.Role = TeamMemberRole.Member;
            member.Role.Should().Be(TeamMemberRole.Member);
        }

        [Theory]
        [InlineData("test@example.com", "Jan Kowalski")]
        [InlineData("admin@company.com", "Administrator")]
        [InlineData("", "")] // Przypadek brzegowy - puste wartości
        public void TeamMember_WhenSettingEmailAndName_ShouldRetainValues(string email, string displayName)
        {
            // Przygotowanie
            var member = new TeamMember();

            // Wykonanie
            member.Email = email;
            member.DisplayName = displayName;

            // Sprawdzenie
            member.Email.Should().Be(email);
            member.DisplayName.Should().Be(displayName);
        }
    }
}