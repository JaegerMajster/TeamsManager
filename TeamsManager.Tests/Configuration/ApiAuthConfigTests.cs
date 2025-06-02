using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TeamsManager.Api.Configuration;
using Xunit;

namespace TeamsManager.Tests.Configuration
{
    public class ApiAuthConfigTests : IDisposable
    {
        private readonly string _testAppDataDir;
        private readonly string _testConfigFile;

        public ApiAuthConfigTests()
        {
            // Setup test environment
            _testAppDataDir = Path.Combine(Path.GetTempPath(), "TeamsManager_Tests", Guid.NewGuid().ToString());
            _testConfigFile = Path.Combine(_testAppDataDir, "oauth_config.json");
            Directory.CreateDirectory(_testAppDataDir);
        }

        #region LoadApiOAuthConfig Tests

        [Fact]
        public void LoadApiOAuthConfig_WithValidAppDataConfig_ShouldLoadFromFile()
        {
            // Arrange
            var uiConfig = new
            {
                AzureAd = new
                {
                    Instance = "https://login.microsoftonline.com/",
                    TenantId = "test-tenant-id",
                    ClientId = "test-client-id"
                },
                Scopes = new[] { "User.Read" }
            };

            var json = JsonSerializer.Serialize(uiConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            
            // Mock environment to point to our test directory
            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("test-tenant-id");
            result.AzureAd.ClientId.Should().Be("test-client-id");
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
        }

        [Fact]
        public void LoadApiOAuthConfig_WithMissingAppDataConfig_ShouldFallbackToIConfiguration()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            // Ensure no AppData config exists
            Environment.SetEnvironmentVariable("APPDATA", Path.GetTempPath());

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
        }

        [Fact]
        public void LoadApiOAuthConfig_WithInvalidAppDataConfig_ShouldFallbackToIConfiguration()
        {
            // Arrange
            var invalidConfig = new
            {
                InvalidProperty = "invalid-value"
            };

            var json = JsonSerializer.Serialize(invalidConfig);
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
        }

        [Fact]
        public void LoadApiOAuthConfig_WithCorruptedJson_ShouldFallbackToIConfiguration()
        {
            // Arrange
            File.WriteAllText(_testConfigFile, "{ invalid json content }");

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
        }

        [Theory]
        [InlineData(null, "test-client")]
        [InlineData("", "test-client")]
        [InlineData("test-tenant", null)]
        [InlineData("test-tenant", "")]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void LoadApiOAuthConfig_WithIncompleteAppDataConfig_ShouldFallbackToIConfiguration(
            string? tenantId, string? clientId)
        {
            // Arrange
            var incompleteConfig = new
            {
                AzureAd = new
                {
                    Instance = "https://login.microsoftonline.com/",
                    TenantId = tenantId,
                    ClientId = clientId
                }
            };

            var json = JsonSerializer.Serialize(incompleteConfig);
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
        }

        [Fact]
        public void LoadApiOAuthConfig_WithNullConfiguration_ShouldReturnDefaultValues()
        {
            // Arrange & Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(null!);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
            result.AzureAd.TenantId.Should().BeNull();
            result.AzureAd.ClientId.Should().BeNull();
        }

        [Fact]
        public void LoadApiOAuthConfig_WithEmptyConfiguration_ShouldReturnDefaultValues()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            // All values return null (default behavior)

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
            result.AzureAd.TenantId.Should().BeNull();
            result.AzureAd.ClientId.Should().BeNull();
        }

        #endregion

        #region Configuration Classes Tests

        [Fact]
        public void ApiAzureAdConfig_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var config = new ApiAuthConfig.ApiAzureAdConfig();

            // Assert
            config.Instance.Should().Be("https://login.microsoftonline.com/");
            config.TenantId.Should().BeNull();
            config.ClientId.Should().BeNull();
        }

        [Fact]
        public void ApiOAuthConfig_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var config = new ApiAuthConfig.ApiOAuthConfig();

            // Assert
            config.AzureAd.Should().NotBeNull();
            config.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
        }

        #endregion

        #region Serialization Tests

        [Fact]
        public void ApiOAuthConfig_JsonSerialization_ShouldWorkCorrectly()
        {
            // Arrange
            var originalConfig = new ApiAuthConfig.ApiOAuthConfig
            {
                AzureAd = new ApiAuthConfig.ApiAzureAdConfig
                {
                    Instance = "https://login.microsoftonline.com/",
                    TenantId = "test-tenant-id",
                    ClientId = "test-client-id",
                    ClientSecret = "test-secret",
                    Audience = "api://test-audience"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions { WriteIndented = true });
            var deserializedConfig = JsonSerializer.Deserialize<ApiAuthConfig.ApiOAuthConfig>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.AzureAd.Should().NotBeNull();
            deserializedConfig.AzureAd.Instance.Should().Be(originalConfig.AzureAd.Instance);
            deserializedConfig.AzureAd.TenantId.Should().Be(originalConfig.AzureAd.TenantId);
            deserializedConfig.AzureAd.ClientId.Should().Be(originalConfig.AzureAd.ClientId);
            deserializedConfig.AzureAd.ClientSecret.Should().Be(originalConfig.AzureAd.ClientSecret);
            deserializedConfig.AzureAd.Audience.Should().Be(originalConfig.AzureAd.Audience);
        }

        [Fact]
        public void ApiAzureAdConfig_CaseInsensitiveDeserialization_ShouldWork()
        {
            // Arrange
            var json = @"{
                ""instance"": ""https://login.microsoftonline.com/"",
                ""tenantid"": ""test-tenant-id"",
                ""clientid"": ""test-client-id"",
                ""clientsecret"": ""test-secret"",
                ""audience"": ""api://test-audience""
            }";

            // Act
            var deserializedConfig = JsonSerializer.Deserialize<ApiAuthConfig.ApiAzureAdConfig>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.Instance.Should().Be("https://login.microsoftonline.com/");
            deserializedConfig.TenantId.Should().Be("test-tenant-id");
            deserializedConfig.ClientId.Should().Be("test-client-id");
            deserializedConfig.ClientSecret.Should().Be("test-secret");
            deserializedConfig.Audience.Should().Be("api://test-audience");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void LoadApiOAuthConfig_WithValidCompleteConfig_ShouldMapCorrectly()
        {
            // Arrange
            var uiConfig = new
            {
                AzureAd = new
                {
                    Instance = "https://login.microsoftonline.com/custom/",
                    TenantId = "12345678-1234-1234-1234-123456789012",
                    ClientId = "87654321-4321-4321-4321-210987654321"
                },
                Scopes = new[] { "User.Read", "Teams.ReadBasic.All" }, // Should be ignored for API
                RedirectUri = "http://localhost:3000" // Should be ignored for API
            };

            var json = JsonSerializer.Serialize(uiConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.Should().NotBeNull();
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/custom/");
            result.AzureAd.TenantId.Should().Be("12345678-1234-1234-1234-123456789012");
            result.AzureAd.ClientId.Should().Be("87654321-4321-4321-4321-210987654321");
        }

        [Fact]
        public void LoadApiOAuthConfig_ConfigurationPriority_ShouldPreferAppDataOverIConfiguration()
        {
            // Arrange
            var appDataConfig = new
            {
                AzureAd = new
                {
                    Instance = "https://login.microsoftonline.com/",
                    TenantId = "appdata-tenant-id",
                    ClientId = "appdata-client-id"
                }
            };

            var json = JsonSerializer.Serialize(appDataConfig);
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("config-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("config-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("appdata-tenant-id"); // Should use AppData, not IConfiguration
            result.AzureAd.ClientId.Should().Be("appdata-client-id");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void LoadApiOAuthConfig_WithFileSystemException_ShouldFallbackGracefully()
        {
            // Arrange
            // Create a directory instead of a file to cause access issues
            Directory.CreateDirectory(_testConfigFile);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
        }

        [Fact]
        public void LoadApiOAuthConfig_WithNullValuesInAppData_ShouldFallbackToIConfiguration()
        {
            // Arrange
            var configWithNulls = new
            {
                AzureAd = new
                {
                    Instance = (string?)null,
                    TenantId = (string?)null,
                    ClientId = (string?)null
                }
            };

            var json = JsonSerializer.Serialize(configWithNulls);
            File.WriteAllText(_testConfigFile, json);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["AzureAd:Instance"]).Returns("https://login.microsoftonline.com/");
            mockConfig.Setup(x => x["AzureAd:TenantId"]).Returns("fallback-tenant-id");
            mockConfig.Setup(x => x["AzureAd:ClientId"]).Returns("fallback-client-id");

            Environment.SetEnvironmentVariable("APPDATA", Path.GetDirectoryName(_testAppDataDir));

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.TenantId.Should().Be("fallback-tenant-id");
            result.AzureAd.ClientId.Should().Be("fallback-client-id");
        }

        #endregion

        public void Dispose()
        {
            // Cleanup test files and directories
            try
            {
                if (Directory.Exists(_testAppDataDir))
                {
                    Directory.Delete(_testAppDataDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }

            // Reset environment variable
            Environment.SetEnvironmentVariable("APPDATA", null);
        }
    }
} 