using FluentAssertions;
using TeamsManager.Core.Enums;
using Xunit;

namespace TeamsManager.Tests.Models // lub TeamsManager.Tests.Core.Enums
{
    public class OperationTypeTests
    {
        [Fact]
        public void OperationType_ShouldHaveCorrectValues()
        {
            // Operacje na zespołach
            ((int)OperationType.TeamCreated).Should().Be(1);
            ((int)OperationType.TeamUpdated).Should().Be(2);
            ((int)OperationType.TeamArchived).Should().Be(3);
            ((int)OperationType.TeamUnarchived).Should().Be(4);
            ((int)OperationType.TeamDeleted).Should().Be(5);

            // Operacje na członkach zespołów
            ((int)OperationType.MemberAdded).Should().Be(20);
            ((int)OperationType.MemberRemoved).Should().Be(21);
            ((int)OperationType.MemberRoleChanged).Should().Be(22);

            // Operacje na kanałach
            ((int)OperationType.ChannelCreated).Should().Be(25);
            ((int)OperationType.ChannelUpdated).Should().Be(26);
            ((int)OperationType.ChannelDeleted).Should().Be(27);

            // Operacje na użytkownikach
            ((int)OperationType.UserCreated).Should().Be(30);
            ((int)OperationType.UserUpdated).Should().Be(31);
            ((int)OperationType.UserImported).Should().Be(32);
            ((int)OperationType.UserDeactivated).Should().Be(33);

            // Operacje wsadowe
            ((int)OperationType.BulkTeamCreation).Should().Be(40);
            ((int)OperationType.BulkUserImport).Should().Be(41);
            ((int)OperationType.BulkArchiving).Should().Be(42);

            // Operacje systemowe
            ((int)OperationType.SystemBackup).Should().Be(50);
            ((int)OperationType.SystemRestore).Should().Be(51);
            ((int)OperationType.ConfigurationChanged).Should().Be(52);
        }

        [Fact]
        public void OperationType_ShouldHaveCorrectNames()
        {
            OperationType.TeamCreated.ToString().Should().Be("TeamCreated");
            OperationType.MemberAdded.ToString().Should().Be("MemberAdded");
            OperationType.ChannelCreated.ToString().Should().Be("ChannelCreated");
            OperationType.UserCreated.ToString().Should().Be("UserCreated");
            OperationType.BulkTeamCreation.ToString().Should().Be("BulkTeamCreation");
            OperationType.SystemBackup.ToString().Should().Be("SystemBackup");
            // Można dodać więcej asercji dla pozostałych nazw, jeśli jest taka potrzeba,
            // ale zazwyczaj kilka reprezentatywnych wystarczy, jeśli ufamy mechanizmowi ToString() enuma.
        }

        [Theory]
        [InlineData(OperationType.TeamCreated)]
        [InlineData(OperationType.TeamUpdated)]
        [InlineData(OperationType.TeamArchived)]
        [InlineData(OperationType.TeamUnarchived)]
        [InlineData(OperationType.TeamDeleted)]
        [InlineData(OperationType.MemberAdded)]
        [InlineData(OperationType.MemberRemoved)]
        [InlineData(OperationType.MemberRoleChanged)]
        [InlineData(OperationType.ChannelCreated)]
        [InlineData(OperationType.ChannelUpdated)]
        [InlineData(OperationType.ChannelDeleted)]
        [InlineData(OperationType.UserCreated)]
        [InlineData(OperationType.UserUpdated)]
        [InlineData(OperationType.UserImported)]
        [InlineData(OperationType.UserDeactivated)]
        [InlineData(OperationType.BulkTeamCreation)]
        [InlineData(OperationType.BulkUserImport)]
        [InlineData(OperationType.BulkArchiving)]
        [InlineData(OperationType.SystemBackup)]
        [InlineData(OperationType.SystemRestore)]
        [InlineData(OperationType.ConfigurationChanged)]
        public void OperationType_AllDefinedValues_ShouldBeValid(OperationType type)
        {
            Enum.IsDefined(typeof(OperationType), type).Should().BeTrue();
        }

        [Fact]
        public void OperationType_WhenConvertingFromInt_ShouldReturnCorrectEnum()
        {
            ((OperationType)1).Should().Be(OperationType.TeamCreated);
            ((OperationType)20).Should().Be(OperationType.MemberAdded);
            ((OperationType)25).Should().Be(OperationType.ChannelCreated);
            ((OperationType)30).Should().Be(OperationType.UserCreated);
            ((OperationType)40).Should().Be(OperationType.BulkTeamCreation);
            ((OperationType)50).Should().Be(OperationType.SystemBackup);
            // Podobnie, można dodać więcej asercji dla pozostałych wartości.
        }
    }
}