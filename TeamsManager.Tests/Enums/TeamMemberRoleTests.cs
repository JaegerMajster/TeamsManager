using FluentAssertions;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Enums
{
    public class TeamMemberRoleTests
    {
        [Fact]
        public void TeamMemberRole_ShouldHaveCorrectValues()
        {
            // Sprawdzenie wartości liczbowych enum
            ((int)TeamMemberRole.Member).Should().Be(0);
            ((int)TeamMemberRole.Owner).Should().Be(1);
        }

        [Fact]
        public void TeamMemberRole_ShouldHaveCorrectNames()
        {
            // Sprawdzenie nazw enum
            TeamMemberRole.Member.ToString().Should().Be("Member");
            TeamMemberRole.Owner.ToString().Should().Be("Owner");
        }

        [Theory]
        [InlineData(TeamMemberRole.Member)]
        [InlineData(TeamMemberRole.Owner)]
        public void TeamMemberRole_AllValues_ShouldBeValid(TeamMemberRole role)
        {
            // Sprawdzenie czy wszystkie wartości enum są zdefiniowane
            Enum.IsDefined(typeof(TeamMemberRole), role).Should().BeTrue();
        }

        [Fact]
        public void TeamMemberRole_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            // Konwersja z int na enum
            ((TeamMemberRole)0).Should().Be(TeamMemberRole.Member);
            ((TeamMemberRole)1).Should().Be(TeamMemberRole.Owner);
        }
    }
}