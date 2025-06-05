using System;
using System.Collections.Generic;
using Xunit;
using TeamsManager.Core.Helpers.PowerShell;
using TeamsManager.Core.Enums;

namespace TeamsManager.Tests.Helpers.PowerShell
{
    public class PSParameterValidatorTests
    {
        #region ValidateAndSanitizeString Tests

        [Fact]
        public void ValidateAndSanitizeString_ValidInput_ReturnsEscapedString()
        {
            // Arrange
            var input = "Test'String$With`Special\"Chars";
            
            // Act
            var result = PSParameterValidator.ValidateAndSanitizeString(input, "testParam");
            
            // Assert
            Assert.Contains("''", result); // Escaped single quote
            Assert.Contains("`$", result); // Escaped dollar
            Assert.Contains("``", result); // Escaped backtick
            Assert.Contains("`\"", result); // Escaped double quote
        }

        [Fact]
        public void ValidateAndSanitizeString_EmptyStringAllowed_ReturnsEmpty()
        {
            // Arrange
            var input = "";
            
            // Act
            var result = PSParameterValidator.ValidateAndSanitizeString(input, "testParam", allowEmpty: true);
            
            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ValidateAndSanitizeString_EmptyStringNotAllowed_ThrowsException()
        {
            // Arrange
            var input = "";
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "testParam", allowEmpty: false));
            Assert.Contains("testParam", exception.Message);
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Fact]
        public void ValidateAndSanitizeString_ExceedsMaxLength_ThrowsException()
        {
            // Arrange
            var input = new string('a', 100);
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "testParam", maxLength: 50));
            Assert.Contains("exceeds maximum length", exception.Message);
            Assert.Contains("50", exception.Message);
            Assert.Contains("100", exception.Message);
        }

        [Fact]
        public void ValidateAndSanitizeString_TeamDisplayName_UsesCorrectMaxLength()
        {
            // Arrange
            var input = new string('a', 300); // Przekracza limit 256 dla team displayname
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "teamDisplayName"));
            Assert.Contains("256", exception.Message);
        }

        [Fact]
        public void ValidateAndSanitizeString_ChannelDisplayName_UsesCorrectMaxLength()
        {
            // Arrange
            var input = new string('a', 60); // Przekracza limit 50 dla channel displayname
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "channelDisplayName"));
            Assert.Contains("50", exception.Message);
        }

        [Fact]
        public void ValidateAndSanitizeString_GraphNameWithInvalidChars_ThrowsException()
        {
            // Arrange
            var input = "Team/Name#With?Invalid*Chars";
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "displayName"));
            Assert.Contains("not allowed in Microsoft Graph names", exception.Message);
        }

        [Fact]
        public void ValidateAndSanitizeString_SpecialCharsNotAllowed_ThrowsException()
        {
            // Arrange
            var input = "Test<>String";
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateAndSanitizeString(input, "testParam", allowSpecialChars: false));
            Assert.Contains("invalid characters", exception.Message);
        }

        #endregion

        #region ValidateEmail Tests

        [Theory]
        [InlineData("user@domain.com")]
        [InlineData("test.user@example.org")]
        [InlineData("user+tag@domain.co.uk")]
        [InlineData("user123@domain123.com")]
        public void ValidateEmail_ValidEmails_ReturnsLowercase(string email)
        {
            // Act
            var result = PSParameterValidator.ValidateEmail(email);
            
            // Assert
            Assert.Equal(email.ToLowerInvariant(), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void ValidateEmail_EmptyOrWhitespace_ThrowsException(string email)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateEmail(email));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Theory]
        [InlineData("invalid-email")]
        [InlineData("user@")]
        [InlineData("@domain.com")]
        [InlineData("user.domain.com")]
        [InlineData("user@domain")]
        [InlineData("user@domain.")]
        public void ValidateEmail_InvalidEmails_ThrowsException(string email)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateEmail(email));
            Assert.Contains("not a valid email address", exception.Message);
        }

        #endregion

        #region ValidateGuid Tests

        [Theory]
        [InlineData("12345678-1234-1234-1234-123456789abc")]
        [InlineData("ABCDEF12-3456-7890-ABCD-EF1234567890")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void ValidateGuid_ValidGuids_ReturnsLowercase(string guid)
        {
            // Act
            var result = PSParameterValidator.ValidateGuid(guid);
            
            // Assert
            Assert.Equal(guid.ToLowerInvariant(), result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void ValidateGuid_EmptyOrWhitespace_ThrowsException(string guid)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateGuid(guid));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Theory]
        [InlineData("invalid-guid")]
        [InlineData("12345678-1234-1234-1234")]
        [InlineData("12345678-1234-1234-1234-123456789abcdef")]
        [InlineData("12345678-1234-1234-1234-123456789abg")]
        [InlineData("12345678_1234_1234_1234_123456789abc")]
        public void ValidateGuid_InvalidGuids_ThrowsException(string guid)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateGuid(guid));
            Assert.Contains("not a valid GUID", exception.Message);
        }

        #endregion

        #region ValidateEnum Tests

        [Fact]
        public void ValidateEnum_ValidValue_ReturnsEnum()
        {
            // Act
            var result = PSParameterValidator.ValidateEnum<TeamVisibility>("Private", "visibility");
            
            // Assert
            Assert.Equal(TeamVisibility.Private, result);
        }

        [Fact]
        public void ValidateEnum_ValidValueCaseInsensitive_ReturnsEnum()
        {
            // Act
            var result = PSParameterValidator.ValidateEnum<TeamVisibility>("public", "visibility");
            
            // Assert
            Assert.Equal(TeamVisibility.Public, result);
        }

        [Fact]
        public void ValidateEnum_InvalidValue_ThrowsException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateEnum<TeamVisibility>("InvalidValue", "visibility"));
            Assert.Contains("invalid value", exception.Message);
            Assert.Contains("Private, Public", exception.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void ValidateEnum_EmptyOrWhitespace_ThrowsException(string value)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateEnum<TeamVisibility>(value, "visibility"));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        #endregion

        #region ValidateStringArray Tests

        [Fact]
        public void ValidateStringArray_ValidArray_ReturnsTrimmedArray()
        {
            // Arrange
            var input = new[] { " value1 ", "value2", " value3 " };
            
            // Act
            var result = PSParameterValidator.ValidateStringArray(input, "testArray");
            
            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal("value1", result[0]);
            Assert.Equal("value2", result[1]);
            Assert.Equal("value3", result[2]);
        }

        [Fact]
        public void ValidateStringArray_EmptyArrayAllowed_ReturnsEmpty()
        {
            // Arrange
            var input = new string[0];
            
            // Act
            var result = PSParameterValidator.ValidateStringArray(input, "testArray", allowEmpty: true);
            
            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ValidateStringArray_EmptyArrayNotAllowed_ThrowsException()
        {
            // Arrange
            var input = new string[0];
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateStringArray(input, "testArray", allowEmpty: false));
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        [Fact]
        public void ValidateStringArray_ExceedsMaxCount_ThrowsException()
        {
            // Arrange
            var input = new[] { "value1", "value2", "value3" };
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateStringArray(input, "testArray", maxCount: 2));
            Assert.Contains("too many items", exception.Message);
            Assert.Contains("Maximum: 2", exception.Message);
            Assert.Contains("Actual: 3", exception.Message);
        }

        [Fact]
        public void ValidateStringArray_ContainsEmptyElement_ThrowsException()
        {
            // Arrange
            var input = new[] { "value1", "", "value3" };
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateStringArray(input, "testArray"));
            Assert.Contains("[1]", exception.Message);
            Assert.Contains("cannot be null or empty", exception.Message);
        }

        #endregion

        #region CreateSafeParameters Tests

        [Fact]
        public void CreateSafeParameters_MixedTypes_ReturnsCorrectDictionary()
        {
            // Arrange
            var stringValue = "test";
            var boolValue = true;
            var intValue = 42;
            var arrayValue = new[] { "item1", "item2" };
            var enumValue = TeamVisibility.Private;
            
            // Act
            var result = PSParameterValidator.CreateSafeParameters(
                ("StringParam", stringValue),
                ("BoolParam", boolValue),
                ("IntParam", intValue),
                ("ArrayParam", arrayValue),
                ("EnumParam", enumValue),
                ("NullParam", null)
            );
            
            // Assert
            Assert.Equal(5, result.Count); // null should be excluded
            Assert.Equal(stringValue, result["StringParam"]);
            Assert.Equal(boolValue, result["BoolParam"]);
            Assert.Equal(intValue, result["IntParam"]);
            Assert.Equal(arrayValue, result["ArrayParam"]);
            Assert.Equal("Private", result["EnumParam"]);
            Assert.False(result.ContainsKey("NullParam"));
        }

        [Fact]
        public void CreateSafeParameters_CustomObject_ConvertsToString()
        {
            // Arrange
            var customObject = new { Name = "Test", Value = 123 };
            
            // Act
            var result = PSParameterValidator.CreateSafeParameters(
                ("CustomParam", customObject)
            );
            
            // Assert
            Assert.Single(result);
            Assert.IsType<string>(result["CustomParam"]);
        }

        #endregion

        #region ValidateGraphConnectionParams Tests

        [Fact]
        public void ValidateGraphConnectionParams_DefaultScopes_ReturnsDefaultScope()
        {
            // Act
            var (scopes, tenantId) = PSParameterValidator.ValidateGraphConnectionParams(null, null);
            
            // Assert
            Assert.Single(scopes);
            Assert.Equal("https://graph.microsoft.com/.default", scopes[0]);
            Assert.Equal("common", tenantId);
        }

        [Fact]
        public void ValidateGraphConnectionParams_CustomScopes_ReturnsValidatedScopes()
        {
            // Arrange
            var inputScopes = new[] { "User.Read", "Group.ReadWrite.All" };
            
            // Act
            var (scopes, tenantId) = PSParameterValidator.ValidateGraphConnectionParams(inputScopes, null);
            
            // Assert
            Assert.Equal(2, scopes.Length);
            Assert.Equal("User.Read", scopes[0]);
            Assert.Equal("Group.ReadWrite.All", scopes[1]);
        }

        [Fact]
        public void ValidateGraphConnectionParams_ValidGuidTenant_ReturnsLowercase()
        {
            // Arrange
            var tenantGuid = "12345678-1234-1234-1234-123456789ABC";
            
            // Act
            var (scopes, tenantId) = PSParameterValidator.ValidateGraphConnectionParams(null, tenantGuid);
            
            // Assert
            Assert.Equal(tenantGuid.ToLowerInvariant(), tenantId);
        }

        [Fact]
        public void ValidateGraphConnectionParams_ValidDomainTenant_ReturnsLowercase()
        {
            // Arrange
            var tenantDomain = "CONTOSO.COM";
            
            // Act
            var (scopes, tenantId) = PSParameterValidator.ValidateGraphConnectionParams(null, tenantDomain);
            
            // Assert
            Assert.Equal("contoso.com", tenantId);
        }

        [Fact]
        public void ValidateGraphConnectionParams_InvalidTenant_ThrowsException()
        {
            // Arrange
            var invalidTenant = "invalid-tenant";
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateGraphConnectionParams(null, invalidTenant));
            Assert.Contains("must be a valid GUID or domain name", exception.Message);
        }

        [Fact]
        public void ValidateGraphConnectionParams_TooManyScopes_ThrowsException()
        {
            // Arrange
            var tooManyScopes = new string[51]; // Przekracza limit 50
            for (int i = 0; i < 51; i++)
            {
                tooManyScopes[i] = $"Scope{i}";
            }
            
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                PSParameterValidator.ValidateGraphConnectionParams(tooManyScopes, null));
            Assert.Contains("too many items", exception.Message);
        }

        #endregion

        #region Edge Cases and Special Characters Tests

        [Fact]
        public void ValidateAndSanitizeString_AllEscapeCharacters_EscapesCorrectly()
        {
            // Arrange
            var input = "'\"`$\r\n\t\0\\";
            
            // Act
            var result = PSParameterValidator.ValidateAndSanitizeString(input, "testParam");
            
            // Assert
            Assert.Contains("''", result);      // Single quote
            Assert.Contains("`\"", result);     // Double quote
            Assert.Contains("``", result);      // Backtick
            Assert.Contains("`$", result);      // Dollar
            Assert.Contains("`r", result);      // Carriage return
            Assert.Contains("`n", result);      // New line
            Assert.Contains("`t", result);      // Tab
            Assert.Contains("`0", result);      // Null
            Assert.Contains("\\\\", result);    // Backslash
        }

        [Fact]
        public void ValidateEmail_EdgeCaseEmails_HandlesCorrectly()
        {
            // Valid edge cases - should not throw exceptions
            var exception1 = Record.Exception(() => PSParameterValidator.ValidateEmail("a@b.co"));
            var exception2 = Record.Exception(() => PSParameterValidator.ValidateEmail("test.email+tag@domain.com"));
            var exception3 = Record.Exception(() => PSParameterValidator.ValidateEmail("user123@domain-name.org"));
            
            Assert.Null(exception1);
            Assert.Null(exception2);
            Assert.Null(exception3);
        }

        [Fact]
        public void ValidateGuid_EdgeCaseGuids_HandlesCorrectly()
        {
            // All zeros - should not throw exception
            var exception1 = Record.Exception(() => PSParameterValidator.ValidateGuid("00000000-0000-0000-0000-000000000000"));
            Assert.Null(exception1);
            
            // All F's - should not throw exception
            var exception2 = Record.Exception(() => PSParameterValidator.ValidateGuid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"));
            Assert.Null(exception2);
        }

        #endregion
    }
} 