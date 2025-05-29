using FluentAssertions;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Models // lub TeamsManager.Tests.Core.Enums
{
    public class SettingTypeTests
    {
        [Fact]
        public void SettingType_ShouldHaveCorrectValues()
        {
            ((int)SettingType.String).Should().Be(0);
            ((int)SettingType.Integer).Should().Be(1);
            ((int)SettingType.Boolean).Should().Be(2);
            ((int)SettingType.Json).Should().Be(3);
            ((int)SettingType.DateTime).Should().Be(4);
            ((int)SettingType.Decimal).Should().Be(5);
        }

        [Fact]
        public void SettingType_ShouldHaveCorrectNames()
        {
            SettingType.String.ToString().Should().Be("String");
            SettingType.Integer.ToString().Should().Be("Integer");
            SettingType.Boolean.ToString().Should().Be("Boolean");
            SettingType.Json.ToString().Should().Be("Json");
            SettingType.DateTime.ToString().Should().Be("DateTime");
            SettingType.Decimal.ToString().Should().Be("Decimal");
        }

        [Theory]
        [InlineData(SettingType.String)]
        [InlineData(SettingType.Integer)]
        [InlineData(SettingType.Boolean)]
        [InlineData(SettingType.Json)]
        [InlineData(SettingType.DateTime)]
        [InlineData(SettingType.Decimal)]
        public void SettingType_AllDefinedValues_ShouldBeValid(SettingType type)
        {
            Enum.IsDefined(typeof(SettingType), type).Should().BeTrue();
        }

        [Fact]
        public void SettingType_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            ((SettingType)0).Should().Be(SettingType.String);
            ((SettingType)1).Should().Be(SettingType.Integer);
            ((SettingType)2).Should().Be(SettingType.Boolean);
            ((SettingType)3).Should().Be(SettingType.Json);
            ((SettingType)4).Should().Be(SettingType.DateTime);
            ((SettingType)5).Should().Be(SettingType.Decimal);
        }
    }
}