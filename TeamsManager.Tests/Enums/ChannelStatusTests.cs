using FluentAssertions;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Models // lub TeamsManager.Tests.Core.Enums
{
    public class ChannelStatusTests
    {
        [Fact]
        public void ChannelStatus_ShouldHaveCorrectValues()
        {
            // Sprawdzenie wartości liczbowych enum
            ((int)ChannelStatus.Active).Should().Be(0);
            ((int)ChannelStatus.Archived).Should().Be(1);
        }

        [Fact]
        public void ChannelStatus_ShouldHaveCorrectNames()
        {
            // Sprawdzenie nazw enum
            ChannelStatus.Active.ToString().Should().Be("Active");
            ChannelStatus.Archived.ToString().Should().Be("Archived");
        }

        [Theory]
        [InlineData(ChannelStatus.Active)]
        [InlineData(ChannelStatus.Archived)]
        public void ChannelStatus_AllDefinedValues_ShouldBeValid(ChannelStatus status)
        {
            // Sprawdzenie czy wszystkie wartości enum są zdefiniowane
            Enum.IsDefined(typeof(ChannelStatus), status).Should().BeTrue();
        }

        [Fact]
        public void ChannelStatus_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            // Konwersja z int na enum
            ((ChannelStatus)0).Should().Be(ChannelStatus.Active);
            ((ChannelStatus)1).Should().Be(ChannelStatus.Archived);
        }
    }
}