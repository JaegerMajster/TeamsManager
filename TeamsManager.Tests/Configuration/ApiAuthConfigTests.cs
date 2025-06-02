using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TeamsManager.Api.Configuration;
using Xunit;

namespace TeamsManager.Tests.Configuration
{
    public class ApiAuthConfigTests
    {
        #region Direct Method Tests

        [Fact]
        public void LoadApiOAuthConfig_WithNullConfiguration_ShouldReturnDefaultValues()
        {
            // Arrange & Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(null);

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
            var mockSection = new Mock<IConfigurationSection>();
            mockConfig.Setup(x => x.GetSection("AzureAd")).Returns(mockSection.Object);

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(mockConfig.Object);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
            result.AzureAd.TenantId.Should().BeNull();
            result.AzureAd.ClientId.Should().BeNull();
        }

        [Fact]
        public void LoadApiOAuthConfig_WithValidConfiguration_ShouldLoadCorrectly()
        {
            // Arrange - Używamy rzeczywistej konfiguracji zamiast mockowania extension method
            var configurationData = new Dictionary<string, string?>
            {
                ["AzureAd:Instance"] = "https://login.microsoftonline.com/custom/",
                ["AzureAd:TenantId"] = "test-tenant-id",
                ["AzureAd:ClientId"] = "test-client-id",
                ["AzureAd:ClientSecret"] = "test-client-secret",
                ["AzureAd:Audience"] = "api://test-audience"
            };

            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();

            // Act
            var result = ApiAuthConfig.LoadApiOAuthConfig(configuration);

            // Assert
            result.Should().NotBeNull();
            result.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/custom/");
            result.AzureAd.TenantId.Should().Be("test-tenant-id");
            result.AzureAd.ClientId.Should().Be("test-client-id");
            result.AzureAd.ClientSecret.Should().Be("test-client-secret");
            result.AzureAd.Audience.Should().Be("api://test-audience");
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
    }
} 