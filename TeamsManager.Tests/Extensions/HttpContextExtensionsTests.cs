using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using System;
using System.Threading.Tasks;
using Xunit;
using TeamsManager.Api.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using FluentAssertions;
using System.Collections.Generic;

namespace TeamsManager.Tests.Extensions
{
    public class HttpContextExtensionsTests
    {
        private readonly string _validToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private readonly string _validBearerHeader = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";

        private static HttpContext CreateHttpContext(string? authorizationHeader = null)
        {
            var httpContext = new DefaultHttpContext();
            
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                httpContext.Request.Headers["Authorization"] = new StringValues(authorizationHeader);
            }

            return httpContext;
        }

        #region GetBearerTokenAsync Tests

        [Fact]
        public async Task GetBearerTokenAsync_WithValidBearerToken_ShouldReturnToken()
        {
            // Arrange
            var httpContext = CreateHttpContext(_validBearerHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(_validToken);
        }

        [Theory]
        [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token")]
        [InlineData("bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token")]
        [InlineData("BEARER eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token")]
        [InlineData("BeArEr eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token")]
        public async Task GetBearerTokenAsync_WithCaseInsensitiveBearer_ShouldReturnToken(string bearerHeader)
        {
            // Arrange
            var httpContext = CreateHttpContext(bearerHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(_validToken);
        }

        [Theory]
        [InlineData("Basic dXNlcjpwYXNzd29yZA==")]
        [InlineData("Digest username=\"user\"")]
        [InlineData("ApiKey 12345")]
        [InlineData("eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token")] // Token bez prefiksu Bearer
        public async Task GetBearerTokenAsync_WithNonBearerAuth_ShouldReturnNull(string authHeader)
        {
            // Arrange
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithoutAuthorizationHeader_ShouldReturnNull()
        {
            // Arrange
            var httpContext = CreateHttpContext();

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Bearer")]
        [InlineData("Bearer ")]
        [InlineData("Bearer    ")] // Same spacje
        public async Task GetBearerTokenAsync_WithEmptyOrInvalidBearerToken_ShouldReturnNull(string authHeader)
        {
            // Arrange
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithTokenContainingSpaces_ShouldTrimSpaces()
        {
            // Arrange
            var authHeader = "Bearer   " + _validToken + "   ";
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(_validToken);
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithNullHttpContext_ShouldReturnNull()
        {
            // Arrange
            HttpContext? httpContext = null;

            // Act
            var result = await httpContext!.GetBearerTokenAsync();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithMultipleAuthorizationHeaders_ShouldUseFirst()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var firstToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.first.token";
            var secondToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.second.token";
            
            httpContext.Request.Headers["Authorization"] = new StringValues(new[] 
            { 
                $"Bearer {firstToken}", 
                $"Bearer {secondToken}" 
            });

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(firstToken);
        }

        #endregion

        #region Cache Tests

        [Fact]
        public async Task GetBearerTokenAsync_CalledTwice_ShouldUseCacheSecondTime()
        {
            // Arrange
            var httpContext = CreateHttpContext(_validBearerHeader);

            // Act - pierwsze wywołanie
            var result1 = await httpContext.GetBearerTokenAsync();
            
            // Zmień nagłówek po pierwszym wywołaniu (cache powinien ignorować tę zmianę)
            httpContext.Request.Headers["Authorization"] = "Bearer different.token";
            
            // Drugie wywołanie
            var result2 = await httpContext.GetBearerTokenAsync();

            // Assert
            result1.Should().Be(_validToken);
            result2.Should().Be(_validToken); // Powinno zwrócić cached wartość
            result1.Should().Be(result2);
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithCacheCleared_ShouldReparseToken()
        {
            // Arrange
            var httpContext = CreateHttpContext(_validBearerHeader);
            var newToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.new.token";

            // Act - pierwsze wywołanie
            var result1 = await httpContext.GetBearerTokenAsync();
            
            // Wyczyść cache
            httpContext.ClearBearerTokenCache();
            
            // Zmień nagłówek
            httpContext.Request.Headers["Authorization"] = $"Bearer {newToken}";
            
            // Drugie wywołanie po wyczyszczeniu cache
            var result2 = await httpContext.GetBearerTokenAsync();

            // Assert
            result1.Should().Be(_validToken);
            result2.Should().Be(newToken);
            result1.Should().NotBe(result2);
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithNullTokenCached_ShouldReturnNullFromCache()
        {
            // Arrange
            var httpContext = CreateHttpContext(); // Brak Authorization header

            // Act - pierwsze wywołanie (powinno zwrócić null i cache'ować null)
            var result1 = await httpContext.GetBearerTokenAsync();
            
            // Dodaj nagłówek po pierwszym wywołaniu
            httpContext.Request.Headers["Authorization"] = _validBearerHeader;
            
            // Drugie wywołanie (powinno zwrócić cached null)
            var result2 = await httpContext.GetBearerTokenAsync();

            // Assert
            result1.Should().BeNull();
            result2.Should().BeNull(); // Cache ma pierwszeństwo
        }

        #endregion

        #region ClearBearerTokenCache Tests

        [Fact]
        public void ClearBearerTokenCache_WithValidHttpContext_ShouldNotThrow()
        {
            // Arrange
            var httpContext = CreateHttpContext();

            // Act & Assert
            var action = () => httpContext.ClearBearerTokenCache();
            action.Should().NotThrow();
        }

        [Fact]
        public void ClearBearerTokenCache_WithNullHttpContext_ShouldNotThrow()
        {
            // Arrange
            HttpContext? httpContext = null;

            // Act & Assert
            var action = () => httpContext!.ClearBearerTokenCache();
            action.Should().NotThrow();
        }

        [Fact]
        public async Task ClearBearerTokenCache_AfterCaching_ShouldRemoveCachedValue()
        {
            // Arrange
            var httpContext = CreateHttpContext(_validBearerHeader);

            // Cache token
            await httpContext.GetBearerTokenAsync();
            
            // Verify cache exists
            httpContext.Items.Should().ContainKey("_TeamsManager_BearerToken_Cache");

            // Act
            httpContext.ClearBearerTokenCache();

            // Assert
            httpContext.Items.Should().NotContainKey("_TeamsManager_BearerToken_Cache");
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public async Task GetBearerTokenAsync_WithAuthenticationException_ShouldFallbackToHeaderParsing()
        {
            // Arrange
            var httpContext = CreateHttpContext(_validBearerHeader);
            
            // HttpContext.GetTokenAsync prawdopodobnie rzuci InvalidOperationException
            // Ale nasz fallback powinien działać

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(_validToken);
        }

        [Theory]
        [InlineData("Bearer \t token\t ")] // Taby
        [InlineData("Bearer \n token\n ")] // Nowe linie
        [InlineData("Bearer \r token\r ")] // Carriage return
        public async Task GetBearerTokenAsync_WithWhitespaceInToken_ShouldTrimAllWhitespace(string authHeader)
        {
            // Arrange
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be("token");
        }

        [Fact]
        public async Task GetBearerTokenAsync_WithVeryLongToken_ShouldHandleCorrectly()
        {
            // Arrange
            var longToken = new string('a', 10000); // 10KB token
            var authHeader = $"Bearer {longToken}";
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(longToken);
        }

        [Theory]
        [InlineData("Bearer token.with.dots")]
        [InlineData("Bearer token_with_underscores")]
        [InlineData("Bearer token-with-dashes")]
        [InlineData("Bearer token123with456numbers")]
        [InlineData("Bearer tOkEn.WiTh.MiXeD.cAsE")]
        public async Task GetBearerTokenAsync_WithVariousTokenFormats_ShouldReturnToken(string authHeader)
        {
            // Arrange
            var expectedToken = authHeader.Substring("Bearer ".Length).Trim();
            var httpContext = CreateHttpContext(authHeader);

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            result.Should().Be(expectedToken);
        }

        #endregion

        #region Compatibility Tests - zgodność z istniejącymi kontrolerami

        [Theory]
        [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("BEARER eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("Basic dXNlcjpwYXNzd29yZA==", false)]
        [InlineData("eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", false)]
        [InlineData("", false)]
        public async Task GetBearerTokenAsync_CompatibilityWithControllerTests_ShouldMatchExpectedBehavior(
            string authHeader, bool shouldExtractToken)
        {
            // Arrange
            var httpContext = CreateHttpContext(authHeader);
            string? expectedToken = shouldExtractToken ? authHeader.Substring("Bearer ".Length).Trim() : null;

            // Act
            var result = await httpContext.GetBearerTokenAsync();

            // Assert
            if (shouldExtractToken)
            {
                result.Should().Be(expectedToken);
                result.Should().NotBeNull();
            }
            else
            {
                result.Should().BeNull();
            }
        }

        #endregion
    }
} 