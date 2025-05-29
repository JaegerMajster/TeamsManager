using FluentAssertions;
using TeamsManager.Core.Enums;
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
            member.Role.Should().Be(TeamMemberRole.Member); // Domyślna rola w zespole
            member.AddedDate.Should().Be(default(DateTime));
            member.TeamId.Should().Be(string.Empty);
            member.UserId.Should().Be(string.Empty); // Teraz wymagane
            member.Team.Should().BeNull();
            member.User.Should().BeNull();

            // Te właściwości są teraz computed properties
            member.Email.Should().Be(string.Empty); // Z User?.Email ?? string.Empty
            member.DisplayName.Should().Be(string.Empty); // Z User?.DisplayName ?? string.Empty
            member.FullName.Should().Be(string.Empty); // Z User?.FullName ?? string.Empty
        }

        [Fact]
        public void TeamMember_WhenSettingRole_ShouldAcceptValidRoles()
        {
            // Przygotowanie
            var member = new TeamMember();

            // Wykonanie i Sprawdzenie - Właściciel zespołu
            member.Role = TeamMemberRole.Owner;
            member.Role.Should().Be(TeamMemberRole.Owner);

            // Wykonanie i Sprawdzenie - Członek zespołu
            member.Role = TeamMemberRole.Member;
            member.Role.Should().Be(TeamMemberRole.Member);
        }

        [Fact]
        public void TeamMember_WhenAssociatedWithUser_ShouldReturnUserProperties()
        {
            // Przygotowanie
            var user = new User
            {
                Id = "user-123",
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@szkola.edu.pl",
                Role = UserRole.Nauczyciel
            };

            var member = new TeamMember
            {
                Id = "member-123",
                UserId = user.Id,
                User = user, // Symulacja relacji
                TeamId = "team-456",
                Role = TeamMemberRole.Owner
            };

            // Sprawdzenie - computed properties z User
            member.Email.Should().Be("jan.kowalski@szkola.edu.pl"); // Z User.Email
            member.DisplayName.Should().Be("Jan Kowalski"); // Z User.DisplayName  
            member.FullName.Should().Be("Jan Kowalski"); // Z User.FullName
        }

        [Fact]
        public void TeamMember_WhenUserIsNull_ShouldReturnEmptyProperties()
        {
            // Przygotowanie - TeamMember bez przypisanego User
            var member = new TeamMember
            {
                Id = "member-123",
                UserId = "user-456", // ID jest ustawione
                User = null, // Ale obiekt User nie jest załadowany
                TeamId = "team-789"
            };

            // Sprawdzenie - powinny zwrócić puste stringi gdy User == null
            member.Email.Should().Be(string.Empty);
            member.DisplayName.Should().Be(string.Empty);
            member.FullName.Should().Be(string.Empty);
        }

        [Fact]
        public void TeamMember_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var member = new TeamMember();
            var memberId = "member-789";
            var teamId = "team-123";
            var userId = "user-456";
            var addedDate = DateTime.Now;

            // Wykonanie
            member.Id = memberId;
            member.TeamId = teamId;
            member.UserId = userId;
            member.Role = TeamMemberRole.Owner;
            member.AddedDate = addedDate;

            // Sprawdzenie
            member.Id.Should().Be(memberId);
            member.TeamId.Should().Be(teamId);
            member.UserId.Should().Be(userId);
            member.Role.Should().Be(TeamMemberRole.Owner);
            member.AddedDate.Should().Be(addedDate);
        }
    }
}