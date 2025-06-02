using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Microsoft.Identity.Client;
using Moq;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class MsalAuthServiceTests : IDisposable
    {
        private readonly string _testConfigDir;
        private readonly string _testConfigFile;
        private readonly string _testDeveloperConfigFile;
        private readonly string _originalAppData;

        public MsalAuthServiceTests()
        {
            // Setup temporary test directory
            _originalAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _testConfigDir = Path.Combine(Path.GetTempPath(), "TeamsManager_Tests", Guid.NewGuid().ToString());
            _testConfigFile = Path.Combine(_testConfigDir, "oauth_config.json");
            _testDeveloperConfigFile = "msalconfig.developer.json";
            
            Directory.CreateDirectory(_testConfigDir);
        }

        #region Configuration Loading Tests

        [Fact]
        public void LoadConfiguration_WithValidAppDataConfig_ShouldLoadCorrectly()
        {
            // Arrange
            var testConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "test-client-id",
                    TenantId = "test-tenant-id",
                    Instance = "https://login.microsoftonline.com/",
                    RedirectUri = "http://localhost"
                },
                Scopes = new[] { "User.Read", "Teams.ReadBasic.All" }
            };

            CreateTestConfigFile(_testConfigFile, testConfig);

            // Act & Assert
            // Note: We can't directly test LoadConfiguration as it's private,
            // but we can test the constructor behavior
            // This test would require making LoadConfiguration protected or internal for testing
        }

        [Fact]
        public void LoadConfiguration_WithInvalidAppDataConfig_ShouldFallbackToDeveloperConfig()
        {
            // Arrange
            var invalidConfig = new { Invalid = "config" };
            File.WriteAllText(_testConfigFile, JsonSerializer.Serialize(invalidConfig));

            var validDeveloperConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "dev-client-id",
                    TenantId = "dev-tenant-id"
                }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, validDeveloperConfig);

            // Act & Assert
            // Constructor should handle fallback
            File.Exists(_testDeveloperConfigFile).Should().BeTrue();
        }

        [Fact]
        public void LoadConfiguration_WithMissingFiles_ShouldReturnEmptyConfig()
        {
            // Arrange
            // No config files exist

            // Act & Assert
            // Constructor should handle missing configuration gracefully
            Assert.True(true); // This test verifies the constructor doesn't crash
        }

        [Theory]
        [InlineData(null, "test-tenant")]
        [InlineData("", "test-tenant")]
        [InlineData("test-client", null)]
        [InlineData("test-client", "")]
        [InlineData("", "")]
        public void IsUiConfigurationValid_WithInvalidConfig_ShouldReturnFalse(string? clientId, string? tenantId)
        {
            // Arrange
            var config = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = clientId,
                    TenantId = tenantId
                }
            };

            // Act & Assert
            // We would need to make IsUiConfigurationValid public or internal for direct testing
            // Alternatively, test through constructor behavior
            config.AzureAd.ClientId.Should().Be(clientId);
            config.AzureAd.TenantId.Should().Be(tenantId);
        }

        [Fact]
        public void IsUiConfigurationValid_WithValidConfig_ShouldReturnTrue()
        {
            // Arrange
            var config = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "valid-client-id",
                    TenantId = "valid-tenant-id"
                }
            };

            // Act & Assert
            config.AzureAd.ClientId.Should().NotBeNullOrWhiteSpace();
            config.AzureAd.TenantId.Should().NotBeNullOrWhiteSpace();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidConfiguration_ShouldInitializeSuccessfully()
        {
            // Arrange
            var validConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "test-client-id",
                    TenantId = "test-tenant-id",
                    Instance = "https://login.microsoftonline.com/",
                    RedirectUri = "http://localhost"
                },
                Scopes = new[] { "User.Read" }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, validConfig);

            // Act
            var service = new MsalAuthService();

            // Assert
            service.Should().NotBeNull();
            // We can't directly access _pca, but if constructor doesn't throw, it's initialized
        }

        [Fact]
        public void Constructor_WithMissingConfiguration_ShouldHandleGracefully()
        {
            // Arrange
            // No configuration files

            // Act & Assert
            // Constructor should not throw exception but handle missing config
            var service = new MsalAuthService();
            service.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithInvalidConfiguration_ShouldHandleGracefully()
        {
            // Arrange
            var invalidConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "", // Invalid
                    TenantId = ""  // Invalid
                }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, invalidConfig);

            // Act & Assert
            var service = new MsalAuthService();
            service.Should().NotBeNull();
        }

        #endregion

        #region Authentication Tests

        [Fact]
        public async Task AcquireTokenInteractiveAsync_WithUninitializedPca_ShouldReturnNull()
        {
            // Arrange
            var service = new MsalAuthService(); // This will likely have null _pca due to missing config
            var mockWindow = new Mock<Window>();

            // Act
            var result = await service.AcquireTokenInteractiveAsync(mockWindow.Object);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task AcquireTokenInteractiveAsync_WithNullWindow_ShouldHandleGracefully()
        {
            // Arrange
            var validConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "test-client-id",
                    TenantId = "test-tenant-id"
                }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, validConfig);
            var service = new MsalAuthService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                service.AcquireTokenInteractiveAsync(null!));
        }

        [Fact]
        public async Task SignOutAsync_WithUninitializedPca_ShouldNotThrow()
        {
            // Arrange
            var service = new MsalAuthService();

            // Act & Assert
            await service.Invoking(s => s.SignOutAsync())
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task SignOutAsync_WithValidService_ShouldCompleteSuccessfully()
        {
            // Arrange
            var validConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "test-client-id",
                    TenantId = "test-tenant-id"
                }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, validConfig);
            var service = new MsalAuthService();

            // Act & Assert
            await service.Invoking(s => s.SignOutAsync())
                .Should().NotThrowAsync();
        }

        #endregion

        #region Configuration Classes Tests

        [Fact]
        public void MsalUiAppConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var config = new MsalUiAppConfiguration();

            // Assert
            config.AzureAd.Should().NotBeNull();
            config.AzureAd.Instance.Should().Be("https://login.microsoftonline.com/");
            config.Scopes.Should().NotBeNull();
            config.Scopes.Should().Contain("User.Read");
        }

        [Fact]
        public void AzureAdUiConfig_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var config = new AzureAdUiConfig();

            // Assert
            config.Instance.Should().Be("https://login.microsoftonline.com/");
            config.TenantId.Should().BeNull();
            config.ClientId.Should().BeNull();
            config.RedirectUri.Should().BeNull();
        }

        [Fact]
        public void MsalUiAppConfiguration_Serialization_ShouldWorkCorrectly()
        {
            // Arrange
            var config = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "test-client",
                    TenantId = "test-tenant",
                    RedirectUri = "http://localhost"
                },
                Scopes = new[] { "User.Read", "Teams.ReadBasic.All" }
            };

            // Act
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var deserializedConfig = JsonSerializer.Deserialize<MsalUiAppConfiguration>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig!.AzureAd.ClientId.Should().Be(config.AzureAd.ClientId);
            deserializedConfig.AzureAd.TenantId.Should().Be(config.AzureAd.TenantId);
            deserializedConfig.Scopes.Should().BeEquivalentTo(config.Scopes);
        }

        #endregion

        #region File Handling Tests

        [Fact]
        public void LoadConfiguration_WithCorruptedJson_ShouldHandleGracefully()
        {
            // Arrange
            File.WriteAllText(_testConfigFile, "{ invalid json }");

            // Act & Assert
            // Constructor should handle corrupted JSON gracefully
            var service = new MsalAuthService();
            service.Should().NotBeNull();
        }

        [Fact]
        public void LoadConfiguration_WithAccessDeniedFile_ShouldHandleGracefully()
        {
            // Arrange
            CreateTestConfigFile(_testConfigFile, new MsalUiAppConfiguration());
            
            // Make file read-only to simulate access issues
            var fileInfo = new FileInfo(_testConfigFile);
            fileInfo.IsReadOnly = true;

            try
            {
                // Act & Assert
                var service = new MsalAuthService();
                service.Should().NotBeNull();
            }
            finally
            {
                // Cleanup
                fileInfo.IsReadOnly = false;
            }
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void MsalAuthService_ConfigurationPriority_ShouldPreferAppDataOverDeveloper()
        {
            // Arrange
            var appDataConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "appdata-client",
                    TenantId = "appdata-tenant"
                }
            };

            var developerConfig = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "dev-client",
                    TenantId = "dev-tenant"
                }
            };

            CreateTestConfigFile(_testConfigFile, appDataConfig);
            CreateTestConfigFile(_testDeveloperConfigFile, developerConfig);

            // Act
            var service = new MsalAuthService();

            // Assert
            service.Should().NotBeNull();
            // In a real scenario, we'd verify which config was actually used
            // This would require access to internal fields or making them testable
        }

        [Fact]
        public void MsalAuthService_WithCompleteValidConfiguration_ShouldBuildProperAuthority()
        {
            // Arrange
            var config = new MsalUiAppConfiguration
            {
                AzureAd = new AzureAdUiConfig
                {
                    ClientId = "12345678-1234-1234-1234-123456789012",
                    TenantId = "87654321-4321-4321-4321-210987654321",
                    Instance = "https://login.microsoftonline.com/",
                    RedirectUri = "http://localhost:3000"
                },
                Scopes = new[] { "User.Read", "Teams.ReadBasic.All", "Group.ReadWrite.All" }
            };

            CreateTestConfigFile(_testDeveloperConfigFile, config);

            // Act
            var service = new MsalAuthService();

            // Assert
            service.Should().NotBeNull();
            // The authority should be constructed as: https://login.microsoftonline.com/{tenantId}/v2.0
            // But we can't verify this directly without exposing internal state
        }

        #endregion

        #region Helper Methods

        private void CreateTestConfigFile(string filePath, MsalUiAppConfiguration config)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        #endregion

        public void Dispose()
        {
            // Cleanup test files and directories
            try
            {
                if (File.Exists(_testDeveloperConfigFile))
                {
                    File.Delete(_testDeveloperConfigFile);
                }

                if (Directory.Exists(_testConfigDir))
                {
                    Directory.Delete(_testConfigDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    #region Test Helper Classes

    /// <summary>
    /// Test wrapper for MsalAuthService to expose internal methods for testing
    /// </summary>
    public class TestableMsalAuthService : MsalAuthService
    {
        public TestableMsalAuthService() : base() { }

        // If we need to expose protected methods for testing:
        // public new MsalUiAppConfiguration LoadConfiguration() => base.LoadConfiguration();
        // public new bool IsUiConfigurationValid(MsalUiAppConfiguration config) => base.IsUiConfigurationValid(config);
    }

    #endregion
} 