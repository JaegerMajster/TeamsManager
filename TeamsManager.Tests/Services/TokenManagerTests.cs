using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Client;
using Moq;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Services.Auth;
using Xunit;
using System.Collections.Generic;

namespace TeamsManager.Tests.Services
{
    public class TokenManagerTests
    {
        private readonly Mock<IConfidentialClientApplication> _mockApp;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<TokenManager>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly TokenManager _tokenManager;
        private readonly Mock<ICacheEntry> _mockCacheEntry;

        public TokenManagerTests()
        {
            _mockApp = new Mock<IConfidentialClientApplication>();
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<TokenManager>>();
            _mockConfiguration = new Mock<IConfiguration>();
            
            // Setup cache entry
            _mockCacheEntry = new Mock<ICacheEntry>();
            _mockCacheEntry.SetupGet(e => e.ExpirationTokens).Returns(new List<IChangeToken>());
            _mockCacheEntry.SetupGet(e => e.PostEvictionCallbacks).Returns(new List<PostEvictionCallbackRegistration>());
            _mockCacheEntry.SetupProperty(e => e.Value);
            _mockCacheEntry.SetupProperty(e => e.AbsoluteExpiration);
            _mockCacheEntry.SetupProperty(e => e.AbsoluteExpirationRelativeToNow);
            _mockCacheEntry.SetupProperty(e => e.SlidingExpiration);
            _mockCacheEntry.SetupProperty(e => e.Priority);

            _mockCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                      .Returns(_mockCacheEntry.Object);

            _tokenManager = new TokenManager(_mockApp.Object, _mockCache.Object, _mockLogger.Object, _mockConfiguration.Object);
        }

        private void SetupCacheTryGetValue<TItem>(string cacheKey, TItem? item, bool foundInCache)
        {
            object? outItem = item;
            _mockCache.Setup(m => m.TryGetValue(cacheKey, out outItem))
                      .Returns(foundInCache);
        }

        private AuthenticationResult CreateMockAuthenticationResult(string accessToken, DateTimeOffset expiresOn, string[] scopes)
        {
            var mockAccount = new Mock<IAccount>();
            mockAccount.Setup(a => a.Username).Returns("test@example.com");
            
            // Używamy mockowanych właściwości zamiast konstruktora
            var mockResult = new Mock<AuthenticationResult>();
            mockResult.SetupGet(r => r.AccessToken).Returns(accessToken);
            mockResult.SetupGet(r => r.ExpiresOn).Returns(expiresOn);
            mockResult.SetupGet(r => r.Account).Returns(mockAccount.Object);
            mockResult.SetupGet(r => r.Scopes).Returns(scopes);
            
            return mockResult.Object;
        }

        [Fact]
        public async Task GetValidAccessTokenAsync_WithValidCachedToken_ShouldReturnCachedToken()
        {
            // Arrange
            var userUpn = "test@example.com";
            var apiAccessToken = "api-token";
            var expectedToken = "valid-cached-token";
            var validExpiration = DateTimeOffset.UtcNow.AddHours(1);
            
            SetupCacheTryGetValue($"graph_token_{userUpn}", expectedToken, true);
            SetupCacheTryGetValue($"graph_expiration_{userUpn}", validExpiration, true);

            // Act
            var result = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);

            // Assert
            result.Should().Be(expectedToken);
            _mockApp.Verify(a => a.AcquireTokenOnBehalfOf(It.IsAny<string[]>(), It.IsAny<UserAssertion>()), Times.Never);
        }

        [Fact]
        public async Task GetValidAccessTokenAsync_WithExpiredToken_ShouldRefreshToken()
        {
            // Arrange
            var userUpn = "test@example.com";
            var apiAccessToken = "api-token";
            var oldToken = "expired-token";
            var newToken = "refreshed-token";
            var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            var newExpiration = DateTimeOffset.UtcNow.AddHours(1);
            
            SetupCacheTryGetValue($"graph_token_{userUpn}", oldToken, true);
            SetupCacheTryGetValue($"graph_expiration_{userUpn}", expiredTime, true);
            
            var newAuthResult = CreateMockAuthenticationResult(newToken, newExpiration, new[] { "scope1", "scope2" });
            
            var mockBuilder = new Mock<AcquireTokenOnBehalfOfParameterBuilder>();
            mockBuilder.Setup(b => b.ExecuteAsync(It.IsAny<System.Threading.CancellationToken>()))
                      .ReturnsAsync(newAuthResult);
            
            _mockApp.Setup(a => a.AcquireTokenOnBehalfOf(It.IsAny<string[]>(), It.IsAny<UserAssertion>()))
                    .Returns(mockBuilder.Object);

            // Act
            var result = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);

            // Assert
            result.Should().Be(newToken);
            _mockApp.Verify(a => a.AcquireTokenOnBehalfOf(It.IsAny<string[]>(), It.IsAny<UserAssertion>()), Times.Once);
        }

        [Fact]
        public async Task GetValidAccessTokenAsync_WhenMsalThrows_ShouldRethrow()
        {
            // Arrange
            var userUpn = "test@example.com";
            var apiAccessToken = "api-token";
            
            SetupCacheTryGetValue<string>($"graph_token_{userUpn}", null, false);
            SetupCacheTryGetValue<DateTimeOffset>($"graph_expiration_{userUpn}", default, false);
            
            var mockBuilder = new Mock<AcquireTokenOnBehalfOfParameterBuilder>();
            mockBuilder.Setup(b => b.ExecuteAsync(It.IsAny<System.Threading.CancellationToken>()))
                      .ThrowsAsync(new MsalUiRequiredException("error_code", "User interaction required"));
            
            _mockApp.Setup(a => a.AcquireTokenOnBehalfOf(It.IsAny<string[]>(), It.IsAny<UserAssertion>()))
                    .Returns(mockBuilder.Object);

            // Act & Assert
            await Assert.ThrowsAsync<MsalUiRequiredException>(async () =>
                await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken));
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenAccountExists_ShouldRefreshToken()
        {
            // Arrange
            var userUpn = "test@example.com";
            var refreshedToken = "refreshed-token";
            var newExpiration = DateTimeOffset.UtcNow.AddHours(1);
            
            var mockAccount = new Mock<IAccount>();
            mockAccount.Setup(a => a.Username).Returns(userUpn);
            
            var authResult = CreateMockAuthenticationResult(refreshedToken, newExpiration, new[] { "scope1" });
            
            _mockApp.Setup(a => a.GetAccountAsync($"{userUpn}"))
                    .ReturnsAsync(mockAccount.Object);
            
            var mockBuilder = new Mock<AcquireTokenSilentParameterBuilder>();
            mockBuilder.Setup(b => b.ExecuteAsync(It.IsAny<System.Threading.CancellationToken>()))
                      .ReturnsAsync(authResult);
            
            _mockApp.Setup(a => a.AcquireTokenSilent(It.IsAny<string[]>(), mockAccount.Object))
                    .Returns(mockBuilder.Object);

            // Act
            var result = await _tokenManager.RefreshTokenAsync(userUpn);

            // Assert
            result.Should().BeTrue();
            _mockApp.Verify(a => a.GetAccountAsync($"{userUpn}"), Times.Once);
            _mockApp.Verify(a => a.AcquireTokenSilent(It.IsAny<string[]>(), mockAccount.Object), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenAccountNotFound_ShouldReturnFalse()
        {
            // Arrange
            var userUpn = "test@example.com";
            
            _mockApp.Setup(a => a.GetAccountAsync($"{userUpn}"))
                    .ReturnsAsync((IAccount?)null);

            // Act
            var result = await _tokenManager.RefreshTokenAsync(userUpn);

            // Assert
            result.Should().BeFalse();
            _mockApp.Verify(a => a.GetAccountAsync($"{userUpn}"), Times.Once);
            _mockApp.Verify(a => a.AcquireTokenSilent(It.IsAny<string[]>(), It.IsAny<IAccount>()), Times.Never);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenMsalThrows_ShouldReturnFalse()
        {
            // Arrange
            var userUpn = "test@example.com";
            
            var mockAccount = new Mock<IAccount>();
            _mockApp.Setup(a => a.GetAccountAsync($"{userUpn}"))
                    .ReturnsAsync(mockAccount.Object);
            
            var mockBuilder = new Mock<AcquireTokenSilentParameterBuilder>();
            mockBuilder.Setup(b => b.ExecuteAsync(It.IsAny<System.Threading.CancellationToken>()))
                      .ThrowsAsync(new MsalException("refresh_failed", "Refresh failed"));
            
            _mockApp.Setup(a => a.AcquireTokenSilent(It.IsAny<string[]>(), mockAccount.Object))
                    .Returns(mockBuilder.Object);

            // Act
            var result = await _tokenManager.RefreshTokenAsync(userUpn);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasValidToken_WithNoToken_ShouldReturnFalse()
        {
            // Arrange
            var userUpn = "test@example.com";
            
            SetupCacheTryGetValue<string>($"graph_token_{userUpn}", null, false);
            SetupCacheTryGetValue<DateTimeOffset>($"graph_expiration_{userUpn}", default, false);

            // Act
            var result = _tokenManager.HasValidToken(userUpn);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasValidToken_WithValidToken_ShouldReturnTrue()
        {
            // Arrange
            var userUpn = "test@example.com";
            var validToken = "valid-token";
            var validExpiration = DateTimeOffset.UtcNow.AddHours(1);
            
            SetupCacheTryGetValue($"graph_token_{userUpn}", validToken, true);
            SetupCacheTryGetValue($"graph_expiration_{userUpn}", validExpiration, true);

            // Act
            var result = _tokenManager.HasValidToken(userUpn);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasValidToken_WithExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            var userUpn = "test@example.com";
            var expiredToken = "expired-token";
            var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-1);
            
            SetupCacheTryGetValue($"graph_token_{userUpn}", expiredToken, true);
            SetupCacheTryGetValue($"graph_expiration_{userUpn}", expiredTime, true);

            // Act
            var result = _tokenManager.HasValidToken(userUpn);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ClearUserTokens_ShouldRemoveAllTokensFromCache()
        {
            // Arrange
            var userUpn = "test@example.com";

            // Act
            _tokenManager.ClearUserTokens(userUpn);

            // Assert
            _mockCache.Verify(m => m.Remove($"graph_token_{userUpn}"), Times.Once);
            _mockCache.Verify(m => m.Remove($"graph_expiration_{userUpn}"), Times.Once);
            _mockCache.Verify(m => m.Remove($"graph_scopes_{userUpn}"), Times.Once);
        }

        [Fact]
        public async Task GetTokenInfoAsync_WithValidToken_ShouldReturnTokenInfo()
        {
            // Arrange
            var userUpn = "test@example.com";
            var token = "test-token";
            var expiration = DateTimeOffset.UtcNow.AddHours(1);
            var scopes = new[] { "scope1", "scope2" };
            
            SetupCacheTryGetValue($"graph_token_{userUpn}", token, true);
            SetupCacheTryGetValue($"graph_expiration_{userUpn}", expiration, true);
            SetupCacheTryGetValue($"graph_scopes_{userUpn}", scopes, true);

            // Act
            var result = await _tokenManager.GetTokenInfoAsync(userUpn);

            // Assert
            result.Should().NotBeNull();
            result!.AccessToken.Should().Be(token);
            result.ExpiresOn.Should().Be(expiration);
            result.Scopes.Should().BeEquivalentTo(scopes);
            result.IsExpired.Should().BeFalse();
        }

        [Fact]
        public async Task GetTokenInfoAsync_WithNoToken_ShouldReturnNull()
        {
            // Arrange
            var userUpn = "test@example.com";
            
            SetupCacheTryGetValue<string>($"graph_token_{userUpn}", null, false);

            // Act
            var result = await _tokenManager.GetTokenInfoAsync(userUpn);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task StoreAuthenticationResultAsync_ShouldCacheTokenWithCorrectExpiration()
        {
            // Arrange
            var userUpn = "test@example.com";
            var token = "stored-token";
            var expiration = DateTimeOffset.UtcNow.AddHours(1);
            var scopes = new[] { "scope1", "scope2" };
            
            var authResult = CreateMockAuthenticationResult(token, expiration, scopes);

            // Act
            await _tokenManager.StoreAuthenticationResultAsync(userUpn, authResult);

            // Assert
            _mockCache.Verify(m => m.CreateEntry($"graph_token_{userUpn}"), Times.Once);
            _mockCache.Verify(m => m.CreateEntry($"graph_expiration_{userUpn}"), Times.Once);
            _mockCache.Verify(m => m.CreateEntry($"graph_scopes_{userUpn}"), Times.Once);
            
            // Sprawdź czy wartości zostały ustawione
            _mockCacheEntry.VerifySet(e => e.Value = token, Times.Once);
            _mockCacheEntry.VerifySet(e => e.AbsoluteExpiration = expiration, Times.Once);
            _mockCacheEntry.VerifySet(e => e.Priority = CacheItemPriority.High, Times.AtLeastOnce);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetValidAccessTokenAsync_WithInvalidUserUpn_ShouldThrowArgumentNullException(string invalidUpn)
        {
            // Arrange
            var apiAccessToken = "api-token";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tokenManager.GetValidAccessTokenAsync(invalidUpn, apiAccessToken));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetValidAccessTokenAsync_WithInvalidApiToken_ShouldThrowArgumentNullException(string invalidApiToken)
        {
            // Arrange
            var userUpn = "test@example.com";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _tokenManager.GetValidAccessTokenAsync(userUpn, invalidApiToken));
        }
    }
} 