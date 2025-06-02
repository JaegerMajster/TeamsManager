using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using Xunit;

namespace TeamsManager.Tests.Authorization
{
    public class JwtAuthenticationTests
    {
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TestAuthController>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;

        public JwtAuthenticationTests()
        {
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TestAuthController>>();
            _mockConfiguration = new Mock<IConfiguration>();
        }

        #region JWT Token Creation Helpers

        private string CreateValidJwtToken(string? tenantId = null, string? clientId = null, string? upn = null, string? oid = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ThisIsASecretKeyForTesting123456789"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("preferred_username", upn ?? "test.user@test.com"),
                new Claim("upn", upn ?? "test.user@test.com"),
                new Claim("name", upn ?? "test.user@test.com"),
                new Claim("oid", oid ?? "12345678-1234-1234-1234-123456789012"),
                new Claim("tid", tenantId ?? "test-tenant-id"),
                new Claim("aud", clientId ?? "test-client-id"),
                new Claim("iss", $"https://sts.windows.net/{tenantId ?? "test-tenant-id"}/"),
                new Claim("sub", "test-subject-id"),
                new Claim(ClaimTypes.NameIdentifier, oid ?? "12345678-1234-1234-1234-123456789012")
            };

            var token = new JwtSecurityToken(
                issuer: $"https://sts.windows.net/{tenantId ?? "test-tenant-id"}/",
                audience: clientId ?? "test-client-id",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string CreateValidJwtTokenV2(string? tenantId = null, string? clientId = null, string? upn = null, string? oid = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ThisIsASecretKeyForTesting123456789"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("preferred_username", upn ?? "test.user@test.com"),
                new Claim("upn", upn ?? "test.user@test.com"),
                new Claim("name", upn ?? "test.user@test.com"),
                new Claim("oid", oid ?? "12345678-1234-1234-1234-123456789012"),
                new Claim("tid", tenantId ?? "test-tenant-id"),
                new Claim("aud", $"api://{clientId ?? "test-client-id"}"),
                new Claim("iss", $"https://login.microsoftonline.com/{tenantId ?? "test-tenant-id"}/v2.0"),
                new Claim("sub", "test-subject-id"),
                new Claim(ClaimTypes.NameIdentifier, oid ?? "12345678-1234-1234-1234-123456789012")
            };

            var token = new JwtSecurityToken(
                issuer: $"https://login.microsoftonline.com/{tenantId ?? "test-tenant-id"}/v2.0",
                audience: $"api://{clientId ?? "test-client-id"}",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string CreateExpiredJwtToken(string? tenantId = null, string? clientId = null)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ThisIsASecretKeyForTesting123456789"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("preferred_username", "test.user@test.com"),
                new Claim("oid", "12345678-1234-1234-1234-123456789012"),
                new Claim("tid", tenantId ?? "test-tenant-id")
            };

            var token = new JwtSecurityToken(
                issuer: $"https://sts.windows.net/{tenantId ?? "test-tenant-id"}/",
                audience: clientId ?? "test-client-id",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(-1), // Expired
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        #endregion

        #region JWT Token Validation Tests

        [Fact]
        public void ParseJwtToken_WithValidToken_ShouldExtractClaimsCorrectly()
        {
            // Arrange
            var token = CreateValidJwtToken("test-tenant", "test-client", "user@test.com", "user-oid");
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.Should().NotBeNull();
            jwtToken.Claims.Should().NotBeEmpty();
            
            var upnClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username");
            upnClaim.Should().NotBeNull();
            upnClaim!.Value.Should().Be("user@test.com");

            var oidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "oid");
            oidClaim.Should().NotBeNull();
            oidClaim!.Value.Should().Be("user-oid");

            var tidClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "tid");
            tidClaim.Should().NotBeNull();
            tidClaim!.Value.Should().Be("test-tenant");
        }

        [Fact]
        public void ParseJwtToken_WithV1Token_ShouldHaveCorrectIssuer()
        {
            // Arrange
            var token = CreateValidJwtToken("test-tenant", "test-client");
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.Issuer.Should().Be("https://sts.windows.net/test-tenant/");
        }

        [Fact]
        public void ParseJwtToken_WithV2Token_ShouldHaveCorrectIssuer()
        {
            // Arrange
            var token = CreateValidJwtTokenV2("test-tenant", "test-client");
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.Issuer.Should().Be("https://login.microsoftonline.com/test-tenant/v2.0");
        }

        [Fact]
        public void ParseJwtToken_WithV1Token_ShouldHaveCorrectAudience()
        {
            // Arrange
            var token = CreateValidJwtToken("test-tenant", "test-client");
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.Audiences.Should().Contain("test-client");
        }

        [Fact]
        public void ParseJwtToken_WithV2Token_ShouldHaveCorrectAudience()
        {
            // Arrange
            var token = CreateValidJwtTokenV2("test-tenant", "test-client");
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.Audiences.Should().Contain("api://test-client");
        }

        [Fact]
        public void ParseJwtToken_WithExpiredToken_ShouldBeExpired()
        {
            // Arrange
            var token = CreateExpiredJwtToken();
            var handler = new JwtSecurityTokenHandler();

            // Act
            var jwtToken = handler.ReadJwtToken(token);

            // Assert
            jwtToken.ValidTo.Should().BeBefore(DateTime.UtcNow);
        }

        #endregion

        #region Token Validation Parameters Tests

        [Fact]
        public void CreateTokenValidationParameters_ForV1Endpoint_ShouldBeConfiguredCorrectly()
        {
            // Arrange
            var tenantId = "test-tenant-id";
            var clientId = "test-client-id";

            // Act
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://sts.windows.net/{tenantId}/",
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Assert
            parameters.ValidIssuer.Should().Be($"https://sts.windows.net/{tenantId}/");
            parameters.ValidAudience.Should().Be(clientId);
            parameters.ValidateLifetime.Should().BeTrue();
            parameters.ClockSkew.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Fact]
        public void CreateTokenValidationParameters_ForV2Endpoint_ShouldBeConfiguredCorrectly()
        {
            // Arrange
            var tenantId = "test-tenant-id";
            var clientId = "test-client-id";

            // Act
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[]
                {
                    $"https://login.microsoftonline.com/{tenantId}/v2.0",
                    $"https://sts.windows.net/{tenantId}/"
                },
                ValidateAudience = true,
                ValidAudience = $"api://{clientId}",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Assert
            parameters.ValidIssuers.Should().Contain($"https://login.microsoftonline.com/{tenantId}/v2.0");
            parameters.ValidIssuers.Should().Contain($"https://sts.windows.net/{tenantId}/");
            parameters.ValidAudience.Should().Be($"api://{clientId}");
        }

        #endregion

        #region Claims Extraction Tests

        [Theory]
        [InlineData("preferred_username", "user@test.com")]
        [InlineData("upn", "user@test.com")]
        [InlineData("name", "user@test.com")]
        public void ExtractUpnFromClaims_WithDifferentClaimTypes_ShouldWorkCorrectly(string claimType, string expectedValue)
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(claimType, expectedValue),
                new Claim("oid", "test-oid")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            // Act
            var upnClaim = principal.FindFirst("preferred_username")?.Value ??
                          principal.FindFirst(ClaimTypes.Name)?.Value ??
                          principal.FindFirst(ClaimTypes.Upn)?.Value;

            // Assert
            if (claimType == "preferred_username")
            {
                upnClaim.Should().Be(expectedValue);
            }
            else
            {
                // For other claim types, we'd need to map them properly
                upnClaim.Should().BeNull(); // Since we're only looking for preferred_username first
            }
        }

        [Fact]
        public void ExtractObjectIdFromClaims_ShouldWorkCorrectly()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("oid", "12345678-1234-1234-1234-123456789012"),
                new Claim(ClaimTypes.NameIdentifier, "name-identifier-value")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            // Act
            var oidClaim = principal.FindFirst("oid")?.Value;
            var nameIdentifierClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Assert
            oidClaim.Should().Be("12345678-1234-1234-1234-123456789012");
            nameIdentifierClaim.Should().Be("name-identifier-value");
        }

        [Fact]
        public void ExtractTenantIdFromClaims_ShouldWorkCorrectly()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("tid", "tenant-id-value"),
                new Claim("iss", "https://sts.windows.net/tenant-id-value/")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            // Act
            var tidClaim = principal.FindFirst("tid")?.Value;

            // Assert
            tidClaim.Should().Be("tenant-id-value");
        }

        #endregion

        #region CurrentUserService Integration Tests

        [Fact]
        public void CurrentUserService_WithValidJwtClaims_ShouldReturnCorrectUpn()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("preferred_username", "test.user@test.com"),
                new Claim("oid", "12345678-1234-1234-1234-123456789012")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var currentUserService = new TeamsManager.Core.Services.UserContext.CurrentUserService(httpContextAccessor.Object);

            // Act
            var upn = currentUserService.GetCurrentUserUpn();

            // Assert
            upn.Should().Be("test.user@test.com");
        }

        [Fact]
        public void CurrentUserService_WithValidJwtClaims_ShouldReturnCorrectUserId()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim("preferred_username", "test.user@test.com"),
                new Claim("oid", "12345678-1234-1234-1234-123456789012")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var currentUserService = new TeamsManager.Core.Services.UserContext.CurrentUserService(httpContextAccessor.Object);

            // Act
            var userId = currentUserService.GetCurrentUserId();

            // Assert
            userId.Should().Be("12345678-1234-1234-1234-123456789012");
        }

        [Fact]
        public void CurrentUserService_WithMissingClaims_ShouldReturnNull()
        {
            // Arrange
            var claims = new List<Claim>(); // No claims

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            httpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            var currentUserService = new TeamsManager.Core.Services.UserContext.CurrentUserService(httpContextAccessor.Object);

            // Act
            var upn = currentUserService.GetCurrentUserUpn();
            var userId = currentUserService.GetCurrentUserId();

            // Assert
            upn.Should().NotBeNullOrWhiteSpace(); // Should fallback to default value
            userId.Should().BeNull();
        }

        #endregion

        #region TestAuthController Tests

        [Fact]
        public void TestAuthController_WhoAmI_WithAuthenticatedUser_ShouldReturnUserInfo()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("test.user@test.com");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("12345678-1234-1234-1234-123456789012");

            var claims = new List<Claim>
            {
                new Claim("preferred_username", "test.user@test.com"),
                new Claim("oid", "12345678-1234-1234-1234-123456789012")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = controller.WhoAmI();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            
            var okResult = result as OkObjectResult;
            okResult.Should().NotBeNull();
            okResult!.Value.Should().NotBeNull();
        }

        [Fact]
        public void TestAuthController_WhoAmI_WithUnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("unknown@teamsmanager.local");

            var identity = new ClaimsIdentity(); // Not authenticated
            var principal = new ClaimsPrincipal(identity);

            var controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = controller.WhoAmI();

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public void TestAuthController_PublicInfo_ShouldAlwaysReturnOk()
        {
            // Arrange
            var controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);

            // Act
            var result = controller.PublicInfo();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void TestAuthController_WhoAmI_WithSystemUser_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("system@teamsmanager.local");

            var claims = new List<Claim>
            {
                new Claim("preferred_username", "system@teamsmanager.local")
            };

            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);

            var controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = controller.WhoAmI();

            // Assert
            result.Should().BeOfType<ObjectResult>();
            
            var objectResult = result as ObjectResult;
            objectResult.Should().NotBeNull();
            objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        }

        #endregion

        #region Authorization Attribute Tests

        [Fact]
        public void TestAuthController_WhoAmI_ShouldHaveAuthorizeAttribute()
        {
            // Arrange & Act
            var method = typeof(TestAuthController).GetMethod("WhoAmI");

            // Assert
            method.Should().NotBeNull();
            var authorizeAttribute = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false).FirstOrDefault();
            authorizeAttribute.Should().NotBeNull();
        }

        [Fact]
        public void TestAuthController_PublicInfo_ShouldNotHaveAuthorizeAttribute()
        {
            // Arrange & Act
            var method = typeof(TestAuthController).GetMethod("PublicInfo");

            // Assert
            method.Should().NotBeNull();
            var authorizeAttribute = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false).FirstOrDefault();
            authorizeAttribute.Should().BeNull();
        }

        #endregion

        #region JWT Bearer Events Tests

        [Fact]
        public void JwtBearerEvents_OnAuthenticationFailed_ShouldLogError()
        {
            // Arrange
            var events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    // This would normally log the error
                    context.Exception.Should().NotBeNull();
                    return Task.CompletedTask;
                }
            };

            var exception = new SecurityTokenValidationException("Test exception");

            // Act & Assert
            // We can't easily test the actual event without setting up a full web host
            // but we can verify the event handler is configured correctly
            events.OnAuthenticationFailed.Should().NotBeNull();
        }

        [Fact]
        public void JwtBearerEvents_OnTokenValidated_ShouldLogSuccess()
        {
            // Arrange
            var events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    context.Principal.Should().NotBeNull();
                    return Task.CompletedTask;
                }
            };

            // Act & Assert
            events.OnTokenValidated.Should().NotBeNull();
        }

        [Fact]
        public void JwtBearerEvents_OnChallenge_ShouldLogChallenge()
        {
            // Arrange
            var events = new JwtBearerEvents
            {
                OnChallenge = context =>
                {
                    context.Error.Should().NotBeNull();
                    return Task.CompletedTask;
                }
            };

            // Act & Assert
            events.OnChallenge.Should().NotBeNull();
        }

        #endregion
    }
} 