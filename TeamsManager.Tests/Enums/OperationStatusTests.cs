using FluentAssertions;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Models // lub TeamsManager.Tests.Core.Enums
{
    public class OperationStatusTests
    {
        [Fact]
        public void OperationStatus_ShouldHaveCorrectValues()
        {
            ((int)OperationStatus.Pending).Should().Be(0);
            ((int)OperationStatus.InProgress).Should().Be(1);
            ((int)OperationStatus.Completed).Should().Be(2);
            ((int)OperationStatus.Failed).Should().Be(3);
            ((int)OperationStatus.Cancelled).Should().Be(4);
            ((int)OperationStatus.PartialSuccess).Should().Be(5);
        }

        [Fact]
        public void OperationStatus_ShouldHaveCorrectNames()
        {
            OperationStatus.Pending.ToString().Should().Be("Pending");
            OperationStatus.InProgress.ToString().Should().Be("InProgress");
            OperationStatus.Completed.ToString().Should().Be("Completed");
            OperationStatus.Failed.ToString().Should().Be("Failed");
            OperationStatus.Cancelled.ToString().Should().Be("Cancelled");
            OperationStatus.PartialSuccess.ToString().Should().Be("PartialSuccess");
        }

        [Theory]
        [InlineData(OperationStatus.Pending)]
        [InlineData(OperationStatus.InProgress)]
        [InlineData(OperationStatus.Completed)]
        [InlineData(OperationStatus.Failed)]
        [InlineData(OperationStatus.Cancelled)]
        [InlineData(OperationStatus.PartialSuccess)]
        public void OperationStatus_AllDefinedValues_ShouldBeValid(OperationStatus status)
        {
            Enum.IsDefined(typeof(OperationStatus), status).Should().BeTrue();
        }

        [Fact]
        public void OperationStatus_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            ((OperationStatus)0).Should().Be(OperationStatus.Pending);
            ((OperationStatus)1).Should().Be(OperationStatus.InProgress);
            ((OperationStatus)2).Should().Be(OperationStatus.Completed);
            ((OperationStatus)3).Should().Be(OperationStatus.Failed);
            ((OperationStatus)4).Should().Be(OperationStatus.Cancelled);
            ((OperationStatus)5).Should().Be(OperationStatus.PartialSuccess);
        }
    }
}