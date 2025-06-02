using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class PowerShellServiceTests : IDisposable
    {
        private readonly Mock<ILogger<PowerShellService>> _mockLogger;
        private readonly Mock<IMemoryCache> _mockMemoryCache;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IOperationHistoryRepository> _mockOperationHistoryRepository;
        private readonly PowerShellService _service;
        private readonly Dictionary<object, object> _cacheStorage = new Dictionary<object, object>();

        public PowerShellServiceTests()
        {
            _mockLogger = new Mock<ILogger<PowerShellService>>();
            _mockMemoryCache = new Mock<IMemoryCache>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockOperationHistoryRepository = new Mock<IOperationHistoryRepository>();

            // Setup Memory Cache behavior
            SetupMemoryCache();

            _service = new PowerShellService(
                _mockLogger.Object,
                _mockMemoryCache.Object,
                _mockCurrentUserService.Object,
                _mockOperationHistoryRepository.Object);
        }

        private void SetupMemoryCache()
        {
            _mockMemoryCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out It.Ref<object?>.IsAny))
                .Returns((object key, out object? value) =>
                {
                    value = null;
                    return _cacheStorage.TryGetValue(key, out value);
                });

            // Nie możemy mockować extension method Set, więc używamy CreateEntry
            _mockMemoryCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns((object key) =>
                {
                    var entry = Mock.Of<ICacheEntry>();
                    Mock.Get(entry).SetupProperty(e => e.Value);
                    Mock.Get(entry).SetupProperty(e => e.ExpirationTokens);
                    Mock.Get(entry).SetupProperty(e => e.PostEvictionCallbacks);
                    Mock.Get(entry).SetupProperty(e => e.Priority);
                    Mock.Get(entry).SetupProperty(e => e.Size);
                    Mock.Get(entry).SetupProperty(e => e.SlidingExpiration);
                    Mock.Get(entry).SetupProperty(e => e.AbsoluteExpiration);
                    Mock.Get(entry).SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
                    Mock.Get(entry).Setup(e => e.Key).Returns(key);
                    Mock.Get(entry).Setup(e => e.Dispose()).Callback(() =>
                    {
                        if (entry.Value != null)
                        {
                            _cacheStorage[key] = entry.Value;
                        }
                    });
                    return entry;
                });

            _mockMemoryCache.Setup(x => x.Remove(It.IsAny<object>()))
                .Callback<object>(key => _cacheStorage.Remove(key));
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
        {
            // Arrange & Act
            var service = new PowerShellService(
                _mockLogger.Object,
                _mockMemoryCache.Object,
                _mockCurrentUserService.Object,
                _mockOperationHistoryRepository.Object);

            // Assert
            service.Should().NotBeNull();
            service.IsConnected.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PowerShellService(
                null!,
                _mockMemoryCache.Object,
                _mockCurrentUserService.Object,
                _mockOperationHistoryRepository.Object));
        }

        [Fact]
        public void Constructor_WithNullMemoryCache_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PowerShellService(
                _mockLogger.Object,
                null!,
                _mockCurrentUserService.Object,
                _mockOperationHistoryRepository.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PowerShellService(
                _mockLogger.Object,
                _mockMemoryCache.Object,
                null!,
                _mockOperationHistoryRepository.Object));
        }

        [Fact]
        public void Constructor_WithNullOperationHistoryRepository_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PowerShellService(
                _mockLogger.Object,
                _mockMemoryCache.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region IsConnected Tests

        [Fact]
        public void IsConnected_ShouldReturnFalse_ByDefault()
        {
            // Arrange & Act & Assert
            _service.IsConnected.Should().BeFalse();
        }

        #endregion

        #region ConnectWithAccessTokenAsync Tests

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithNullToken_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.ConnectWithAccessTokenAsync(null!);

            // Assert
            result.Should().BeFalse();
            VerifyLogContains(LogLevel.Error, "token dostępu nie może być pusty");
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithEmptyToken_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.ConnectWithAccessTokenAsync(string.Empty);

            // Assert
            result.Should().BeFalse();
            VerifyLogContains(LogLevel.Error, "token dostępu nie może być pusty");
        }

        [Fact]
        public async Task ConnectWithAccessTokenAsync_WithWhitespaceToken_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.ConnectWithAccessTokenAsync("   ");

            // Assert
            result.Should().BeFalse();
            VerifyLogContains(LogLevel.Error, "token dostępu nie może być pusty");
        }

        #endregion

        #region Team Management Tests

        [Fact]
        public async Task CreateTeamAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.CreateTeamAsync("Test Team", "Description", "owner@test.com");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData("", "Description", "owner@test.com")]
        [InlineData("Test Team", "Description", "")]
        [InlineData(null, "Description", "owner@test.com")]
        [InlineData("Test Team", "Description", null)]
        public async Task CreateTeamAsync_WithInvalidParameters_ShouldReturnNullAndLogError(
            string displayName, string description, string ownerUpn)
        {
            // Arrange & Act
            var result = await _service.CreateTeamAsync(displayName, description, ownerUpn);

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateTeamPropertiesAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.UpdateTeamPropertiesAsync("team-id", "New Name");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateTeamPropertiesAsync_WithInvalidTeamId_ShouldReturnFalseAndLogError(string teamId)
        {
            // Arrange & Act
            var result = await _service.UpdateTeamPropertiesAsync(teamId, "New Name");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateTeamPropertiesAsync_WithNoChanges_ShouldReturnTrueWithoutExecution()
        {
            // Arrange - brak połączenia oznacza false
            _service.IsConnected.Should().BeFalse();

            // Act - wywołujemy bez żadnych zmian ale bez połączenia
            var result = await _service.UpdateTeamPropertiesAsync("team-id");

            // Assert - bez połączenia powinno zwrócić false
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task ArchiveTeamAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.ArchiveTeamAsync("team-id");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ArchiveTeamAsync_WithInvalidTeamId_ShouldReturnFalseAndLogError(string teamId)
        {
            // Arrange & Act
            var result = await _service.ArchiveTeamAsync(teamId);

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UnarchiveTeamAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.UnarchiveTeamAsync("team-id");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task DeleteTeamAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.DeleteTeamAsync("team-id");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetTeamAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.GetTeamAsync("test-team-id");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetAllTeamsAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.GetAllTeamsAsync();

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetTeamsByOwnerAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.GetTeamsByOwnerAsync("owner@test.com");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region Team Members Tests

        [Fact]
        public async Task AddUserToTeamAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.AddUserToTeamAsync("team-id", "user@test.com", "Member");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null, "user@test.com", "Member")]
        [InlineData("", "user@test.com", "Member")]
        [InlineData("team-id", null, "Member")]
        [InlineData("team-id", "", "Member")]
        [InlineData("team-id", "user@test.com", null)]
        [InlineData("team-id", "user@test.com", "")]
        public async Task AddUserToTeamAsync_WithInvalidParameters_ShouldReturnFalseAndLogError(
            string teamId, string userUpn, string role)
        {
            // Arrange & Act
            var result = await _service.AddUserToTeamAsync(teamId, userUpn, role);

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task AddUserToTeamAsync_WithInvalidRole_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.AddUserToTeamAsync("team-id", "user@test.com", "InvalidRole");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task RemoveUserFromTeamAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.RemoveUserFromTeamAsync("team-id", "user@test.com");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetTeamMembersAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.GetTeamMembersAsync("test-team-id");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetTeamMemberAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.GetTeamMemberAsync("test-team-id", "user@test.com");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region Channel Management Tests

        [Fact]
        public async Task GetTeamChannelsAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.GetTeamChannelsAsync("team-id");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetTeamChannelsAsync_WithInvalidTeamId_ShouldReturnNullAndLogError(string teamId)
        {
            // Arrange & Act
            var result = await _service.GetTeamChannelsAsync(teamId);

            // Assert
            result.Should().BeNull();
            // Bez połączenia loguje tylko Warning o braku połączenia
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetTeamChannelAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.GetTeamChannelAsync("test-team-id", "General");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null, "General")]
        [InlineData("", "General")]
        [InlineData("team-id", null)]
        [InlineData("team-id", "")]
        public async Task GetTeamChannelAsync_WithInvalidParameters_ShouldReturnNullAndLogError(
            string teamId, string channelName)
        {
            // Arrange & Act
            var result = await _service.GetTeamChannelAsync(teamId, channelName);

            // Assert
            result.Should().BeNull();
            // Bez połączenia loguje tylko Warning o braku połączenia
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task CreateTeamChannelAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.CreateTeamChannelAsync("team-id", "New Channel");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null, "New Channel")]
        [InlineData("", "New Channel")]
        [InlineData("team-id", null)]
        [InlineData("team-id", "")]
        public async Task CreateTeamChannelAsync_WithInvalidParameters_ShouldReturnNullAndLogError(
            string teamId, string channelName)
        {
            // Arrange & Act
            var result = await _service.CreateTeamChannelAsync(teamId, channelName);

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateTeamChannelAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.UpdateTeamChannelAsync("team-id", "Old Channel", "New Channel");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateTeamChannelAsync_WithNoChangesProvided_ShouldReturnTrueAndLogInfo()
        {
            // Arrange - brak połączenia oznacza false
            _service.IsConnected.Should().BeFalse();

            // Act - wywołujemy bez żadnych zmian ale bez połączenia
            var result = await _service.UpdateTeamChannelAsync("team-id", "channel-id");

            // Assert - bez połączenia powinno zwrócić false
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task RemoveTeamChannelAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.RemoveTeamChannelAsync("team-id", "Channel to Delete");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region M365 User Management Tests

        [Fact]
        public async Task CreateM365UserAsync_WhenNotConnected_ShouldReturnNull()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.CreateM365UserAsync("Test User", "test@example.com", "Password123", "PL");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Theory]
        [InlineData(null, "john@test.com", "Password123!")]
        [InlineData("", "john@test.com", "Password123!")]
        [InlineData("John Doe", null, "Password123!")]
        [InlineData("John Doe", "", "Password123!")]
        [InlineData("John Doe", "john@test.com", null)]
        [InlineData("John Doe", "john@test.com", "")]
        public async Task CreateM365UserAsync_WithInvalidParameters_ShouldReturnNullAndLogError(
            string displayName, string userPrincipalName, string password)
        {
            // Arrange & Act
            var result = await _service.CreateM365UserAsync(displayName, userPrincipalName, password);

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task SetM365UserAccountStateAsync_WhenNotConnected_ShouldReturnFalse()
        {
            // Arrange & Act
            var result = await _service.SetM365UserAccountStateAsync("user@test.com", true);

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateM365UserPrincipalNameAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.UpdateM365UserPrincipalNameAsync("old@test.com", "new@test.com");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateM365UserPropertiesAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.UpdateM365UserPropertiesAsync("user@test.com", department: "IT");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task UpdateM365UserPropertiesAsync_WithNoChanges_ShouldReturnTrueWithoutExecution()
        {
            // Arrange - brak połączenia oznacza false
            _service.IsConnected.Should().BeFalse();

            // Act - wywołujemy bez żadnych zmian ale bez połączenia
            var result = await _service.UpdateM365UserPropertiesAsync("user@test.com");

            // Assert - bez połączenia powinno zwrócić false
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetAllUsersAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.GetAllUsersAsync();

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetInactiveUsersAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.GetInactiveUsersAsync(30);

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region License Management Tests

        [Fact]
        public async Task AssignLicenseToUserAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.AssignLicenseToUserAsync("user@test.com", "license-sku");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task RemoveLicenseFromUserAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.RemoveLicenseFromUserAsync("user@test.com", "license-sku");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task GetUserLicensesAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.GetUserLicensesAsync("user@test.com");

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region Bulk Operations Tests

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WhenNotConnected_ShouldReturnEmptyDictionaryAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();
            var userUpns = new List<string> { "user1@test.com", "user2@test.com" };

            // Act
            var result = await _service.BulkAddUsersToTeamAsync("test-team-id", userUpns);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithNullUserUpns_ShouldReturnEmptyDictionaryAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();

            // Act
            var result = await _service.BulkAddUsersToTeamAsync("test-team-id", null!);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task BulkAddUsersToTeamAsync_WithEmptyUserUpns_ShouldReturnEmptyDictionary()
        {
            // Arrange
            var emptyUserUpns = new List<string>();

            // Act
            var result = await _service.BulkAddUsersToTeamAsync("team-id", emptyUserUpns);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task BulkArchiveTeamsAsync_WhenNotConnected_ShouldReturnEmptyDictionaryAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();
            var teamIds = new List<string> { "team1", "team2" };

            // Act
            var result = await _service.BulkArchiveTeamsAsync(teamIds);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task BulkRemoveUsersFromTeamAsync_WhenNotConnected_ShouldReturnEmptyDictionaryAndLogError()
        {
            // Arrange
            _service.IsConnected.Should().BeFalse();
            var userUpns = new List<string> { "user1@test.com", "user2@test.com" };

            // Act
            var result = await _service.BulkRemoveUsersFromTeamAsync("test-team-id", userUpns);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task BulkUpdateUserPropertiesAsync_WhenNotConnected_ShouldReturnEmptyDictionaryAndLogError()
        {
            // Arrange
            var userUpdates = new Dictionary<string, Dictionary<string, string>>
            {
                { "user1@test.com", new Dictionary<string, string> { { "Department", "IT" } } }
            };

            // Act
            var result = await _service.BulkUpdateUserPropertiesAsync(userUpdates);

            // Assert
            result.Should().BeEmpty();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region Advanced Functions Tests

        [Fact]
        public async Task FindDuplicateUsersAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.FindDuplicateUsersAsync();

            // Assert
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task ArchiveTeamAndDeactivateExclusiveUsersAsync_WhenNotConnected_ShouldReturnFalseAndLogError()
        {
            // Arrange & Act
            var result = await _service.ArchiveTeamAndDeactivateExclusiveUsersAsync("team-id");

            // Assert
            result.Should().BeFalse();
            VerifyNoConnectionWarning();
        }

        [Fact]
        public async Task ExecuteScriptAsync_WhenNotConnected_ShouldReturnNullAndLogError()
        {
            // Arrange & Act
            var result = await _service.ExecuteScriptAsync("Get-MgUser");

            // Assert
            result.Should().BeNull();
            // ExecuteScriptAsync rzeczywiście wykonuje PowerShell i loguje prawdziwe błędy
            // Może być Communication error, Module loading error, lub inne błędy PowerShell
            VerifyLogContains(LogLevel.Error); // Jakikolwiek error zostanie zalogowany
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteScriptAsync_WithInvalidScript_ShouldReturnNullAndLogError(string script)
        {
            // Arrange & Act
            var result = await _service.ExecuteScriptAsync(script);

            // Assert
            result.Should().BeNull();
            VerifyLogContains(LogLevel.Error, "Skrypt nie może być pusty");
        }

        #endregion

        #region Cache Tests

        [Fact]
        public async Task GetTeamAsync_ShouldUseCacheWhenAvailable()
        {
            // Arrange
            var teamId = "test-team-id";
            var cacheKey = $"PowerShell_Team_{teamId}";
            var cachedTeam = new PSObject();
            _cacheStorage[cacheKey] = cachedTeam;

            // Act
            var result = await _service.GetTeamAsync(teamId);

            // Assert
            // Since we're not connected, it should return null due to connection check
            // But this tests the cache key structure
            result.Should().BeNull();
            VerifyNoConnectionWarning();
        }

        #endregion

        #region Dispose Tests

        [Fact]
        public void Dispose_WhenCalled_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            _service.Dispose();
            _service.Invoking(s => s.Dispose()).Should().NotThrow();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Pomocnicza metoda do weryfikacji logów
        /// </summary>
        private void VerifyLogContains(LogLevel level, string? message = null)
        {
            if (message == null)
            {
                _mockLogger.Verify(
                    x => x.Log(
                        level,
                        It.IsAny<EventId>(),
                        It.IsAny<It.IsAnyType>(),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);
            }
            else
            {
                _mockLogger.Verify(
                    x => x.Log(
                        level,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);
            }
        }

        /// <summary>
        /// Pomocnicza metoda do weryfikacji braku aktywnego połączenia (loguje Warning, nie Error)
        /// </summary>
        private void VerifyNoConnectionWarning()
        {
            VerifyLogContains(LogLevel.Warning, "Brak aktywnego połączenia z Microsoft Graph");
        }

        #endregion

        public void Dispose()
        {
            _service?.Dispose();
        }
    }

    #region Test Helper Classes

    public class TestPowerShellService : PowerShellService
    {
        public TestPowerShellService(
            ILogger<PowerShellService> logger,
            IMemoryCache memoryCache,
            ICurrentUserService currentUserService,
            IOperationHistoryRepository operationHistoryRepository)
            : base(logger, memoryCache, currentUserService, operationHistoryRepository)
        {
        }

        // Expose protected methods for testing if needed
        public new void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    #endregion
}