using FluentAssertions;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Enums
{
    public class TeamStatusTests
    {
        [Fact]
        public void TeamStatus_ShouldHaveCorrectValues()
        {
            // Sprawdzenie wartości liczbowych enum
            ((int)TeamStatus.Active).Should().Be(0);
            ((int)TeamStatus.Archived).Should().Be(1);
        }

        [Fact]
        public void TeamStatus_ShouldHaveCorrectNames()
        {
            // Sprawdzenie nazw enum
            TeamStatus.Active.ToString().Should().Be("Active");
            TeamStatus.Archived.ToString().Should().Be("Archived");
        }

        [Theory]
        [InlineData(TeamStatus.Active)]
        [InlineData(TeamStatus.Archived)]
        public void TeamStatus_AllValues_ShouldBeValid(TeamStatus status)
        {
            // Sprawdzenie czy wszystkie wartości enum są zdefiniowane
            Enum.IsDefined(typeof(TeamStatus), status).Should().BeTrue();
        }

        [Fact]
        public void TeamStatus_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            // Konwersja z int na enum
            ((TeamStatus)0).Should().Be(TeamStatus.Active);
            ((TeamStatus)1).Should().Be(TeamStatus.Archived);
        }
    }
}