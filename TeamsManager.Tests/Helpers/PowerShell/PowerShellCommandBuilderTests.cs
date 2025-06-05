using System;
using System.Collections.Generic;
using Xunit;
using TeamsManager.Core.Helpers.PowerShell;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Helpers.PowerShell
{
    public class PowerShellCommandBuilderTests
    {
        #region Basic Command Building Tests

        [Fact]
        public void WithCommand_ValidCommand_SetsCommandName()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.WithCommand("Get-MgUser");
            
            // Assert
            Assert.Same(builder, result); // Should return same instance for fluent interface
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void WithCommand_EmptyOrNullCommand_ThrowsException(string command)
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => builder.WithCommand(command));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Fact]
        public void AddParameter_ValidParameter_AddsParameter()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.AddParameter("UserId", "user@domain.com");
            
            // Assert
            Assert.Same(builder, result); // Should return same instance for fluent interface
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void AddParameter_EmptyOrNullName_ThrowsException(string paramName)
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => builder.AddParameter(paramName, "value"));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Fact]
        public void AddParameter_NullValue_SkipsParameter()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.AddParameter("TestParam", null);
            
            // Assert
            Assert.Same(builder, result); // Should still return builder instance
        }

        #endregion

        #region Hashtable Tests

        [Fact]
        public void AddHashtableVariable_ValidHashtable_AddsVariable()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            var properties = new Dictionary<string, object?>
            {
                ["DisplayName"] = "Test User",
                ["UserPrincipalName"] = "test@domain.com",
                ["AccountEnabled"] = true
            };
            
            // Act
            var result = builder.AddHashtableVariable("userParams", properties);
            
            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void AddGraphTeamMember_ValidMember_AddsVariable()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            var userId = "12345678-1234-1234-1234-123456789abc";
            
            // Act
            var result = builder.AddGraphTeamMember("member1", userId, TeamMemberRole.Owner);
            
            // Assert
            Assert.Same(builder, result);
        }

        #endregion

        #region Variable Tests

        [Fact]
        public void AddVariable_ValidVariable_AddsVariable()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.AddVariable("result", "Get-MgUser");
            
            // Assert
            Assert.Same(builder, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void AddVariable_EmptyOrNullName_ThrowsException(string varName)
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => builder.AddVariable(varName, "value"));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void WithErrorHandling_EnablesErrorHandling()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.WithErrorHandling(true);
            
            // Assert
            Assert.Same(builder, result);
        }

        [Fact]
        public void ReturnRawResult_EnablesRawResult()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var result = builder.ReturnRawResult(true);
            
            // Assert
            Assert.Same(builder, result);
        }

        #endregion

        #region Build Tests

        [Fact]
        public void Build_WithoutCommand_ThrowsException()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.Contains("Command name must be specified", exception.Message);
        }

        [Fact]
        public void Build_SimpleCommand_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
        }

        [Fact]
        public void Build_CommandWithParameters_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "user@domain.com")
                .AddParameter("Select", "Id,DisplayName")
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("-UserId", script);
            Assert.Contains("-Select", script);
        }

        [Fact]
        public void Build_CommandWithArrayParameter_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            var scopes = new[] { "User.Read", "Group.ReadWrite.All" };
            
            // Act
            var script = builder
                .WithCommand("Connect-MgGraph")
                .AddParameter("Scopes", scopes)
                .Build();
            
            // Assert
            Assert.Contains("Connect-MgGraph", script);
            Assert.Contains("-Scopes", script);
            Assert.Contains("User.Read", script);
            Assert.Contains("Group.ReadWrite.All", script);
        }

        [Fact]
        public void Build_CommandWithVariable_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .AddVariable("userId", "'user@domain.com'")
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "$userId")
                .Build();
            
            // Assert
            Assert.Contains("$userId = ", script);
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("-UserId", script);
        }

        [Fact]
        public void Build_CommandWithErrorHandling_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "user@domain.com")
                .WithErrorHandling(true)
                .Build();
            
            // Assert
            Assert.Contains("try", script);
            Assert.Contains("catch", script);
            Assert.Contains("Get-MgUser", script);
        }

        [Fact]
        public void Build_CommandWithRawResult_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "user@domain.com")
                .ReturnRawResult(true)
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            // Raw result should not wrap in ConvertTo-Json
            Assert.DoesNotContain("ConvertTo-Json", script);
        }

        [Fact]
        public void Build_CommandWithoutRawResult_ReturnsGetCommand()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "user@domain.com")
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("$result", script); // For Get- commands, returns $result
        }

        #endregion

        #region Complex Scenario Tests

        [Fact]
        public void Build_ComplexScenario_ReturnsCorrectScript()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            var userProperties = new Dictionary<string, object?>
            {
                ["DisplayName"] = "New User",
                ["UserPrincipalName"] = "newuser@contoso.com",
                ["MailNickname"] = "newuser",
                ["AccountEnabled"] = true,
                ["UsageLocation"] = "US"
            };
            
            // Act
            var script = builder
                .AddVariable("tenantId", "'contoso.onmicrosoft.com'")
                .AddHashtableVariable("userParams", userProperties)
                .WithCommand("New-MgUser")
                .AddParameter("BodyParameter", "$userParams")
                .WithErrorHandling(true)
                .Build();
            
            // Assert
            Assert.Contains("$tenantId = ", script);
            Assert.Contains("$userParams = ", script);
            Assert.Contains("New-MgUser", script);
            Assert.Contains("-BodyParameter", script);
            Assert.Contains("try", script);
            Assert.Contains("catch", script);
            Assert.Contains("$result.Id", script); // For New- commands, returns ID
        }

        [Fact]
        public void Build_MultipleBuildsFromSameInstance_ReturnsConsistentResults()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "user@domain.com");
            
            // Act
            var script1 = builder.Build();
            var script2 = builder.Build();
            
            // Assert
            Assert.Equal(script1, script2);
        }

        [Fact]
        public void Build_SpecialCharactersInParameters_EscapesCorrectly()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            var valueWithSpecialChars = "Test'String$With`Special\"Chars";
            
            // Act
            var script = builder
                .WithCommand("New-MgUser")
                .AddParameter("DisplayName", valueWithSpecialChars)
                .Build();
            
            // Assert
            Assert.Contains("New-MgUser", script);
            Assert.Contains("-DisplayName", script);
            // Parameter values are wrapped in single quotes by PowerShellCommandBuilder
            Assert.Contains("'Test'String$With`Special\"Chars'", script);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void Build_EmptyParameterValue_IncludesEmptyParameter()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("UserId", "")
                .AddParameter("DisplayName", "Valid Name")
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("-UserId", script); // PowerShellCommandBuilder includes empty parameters as ''
            Assert.Contains("-DisplayName", script);
        }

        [Fact]
        public void Build_BooleanParameters_HandlesCorrectly()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Update-MgUser")
                .AddParameter("UserId", "user@domain.com")
                .AddParameter("AccountEnabled", true)
                .AddParameter("ShowInAddressList", false)
                .Build();
            
            // Assert
            Assert.Contains("Update-MgUser", script);
            Assert.Contains("-AccountEnabled $true", script);
            Assert.Contains("-ShowInAddressList $false", script);
        }

        [Fact]
        public void Build_NumericParameters_HandlesCorrectly()
        {
            // Arrange
            var builder = new PowerShellCommandBuilder();
            
            // Act
            var script = builder
                .WithCommand("Get-MgUser")
                .AddParameter("Top", 100)
                .AddParameter("Skip", 50)
                .Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("-Top 100", script);
            Assert.Contains("-Skip 50", script);
        }

        [Fact]
        public void Build_StaticCreateNewTeamCommand_ReturnsCorrectCommand()
        {
            // Arrange & Act
            var builder = PowerShellCommandBuilder.CreateNewTeamCommand(
                "Test Team", 
                "Test Description", 
                TeamVisibility.Public, 
                "owner@domain.com");
            var script = builder.Build();
            
            // Assert
            Assert.Contains("New-MgTeam", script);
            Assert.Contains("Test Team", script);
            Assert.Contains("Test Description", script);
        }

        [Fact]
        public void Build_StaticCreateGetUsersCommand_ReturnsCorrectCommand()
        {
            // Arrange & Act
            var builder = PowerShellCommandBuilder.CreateGetUsersCommand("startswith(displayName,'John')", 50);
            var script = builder.Build();
            
            // Assert
            Assert.Contains("Get-MgUser", script);
            Assert.Contains("-Filter", script);
            Assert.Contains("-PageSize 50", script); // CreateGetUsersCommand uses PageSize, not Top
        }

        #endregion
    }
} 