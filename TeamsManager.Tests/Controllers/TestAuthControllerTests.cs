using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla TestAuthController
    /// Pokrycie: 2 endpointy - whoami (uwierzytelnianie) i publicinfo (publiczny)
    /// </summary>
    public class TestAuthControllerTests
    {
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TestAuthController>> _mockLogger;
        private readonly TestAuthController _controller;

        public TestAuthControllerTests()
        {
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TestAuthController>>();

            _controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new TestAuthController(_mockCurrentUserService.Object, _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new TestAuthController(null!, _mockLogger.Object));
            
            exception.ParamName.Should().Be("currentUserService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new TestAuthController(_mockCurrentUserService.Object, null!));
            
            exception.ParamName.Should().Be("logger");
        }

        #endregion

        #region WhoAmI Tests

        [Fact]
        public void WhoAmI_WithAuthenticatedUser_ShouldReturnOkWithUserInfo()
        {
            // Arrange
            var userUpn = "john.doe@test.com";
            var userId = "user-123";
            var claims = new List<Claim>
            {
                new("upn", userUpn),
                new("oid", userId),
                new("name", "John Doe")
            };

            SetupAuthenticatedUser(claims);
            
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns(userUpn);
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns(userId);

            // Act
            var result = _controller.WhoAmI();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            // Sprawdzenie czy response zawiera oczekiwane właściwości
            var responseType = response.GetType();
            responseType.GetProperty("Message")?.GetValue(response)?.ToString()
                .Should().Be("Jesteś pomyślnie uwierzytelniony!");
            responseType.GetProperty("UserPrincipalName")?.GetValue(response)?.ToString()
                .Should().Be(userUpn);
            responseType.GetProperty("ObjectId")?.GetValue(response)?.ToString()
                .Should().Be(userId);
        }

        [Fact]
        public void WhoAmI_WithUnauthenticatedUser_ShouldReturnUnauthorized()
        {
            // Arrange
            SetupUnauthenticatedUser();

            // Act
            var result = _controller.WhoAmI();

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            var response = unauthorizedResult.Value.Should().BeAssignableTo<object>().Subject;
            
            var responseType = response.GetType();
            responseType.GetProperty("Message")?.GetValue(response)?.ToString()
                .Should().Contain("Użytkownik nie jest uwierzytelniony");
        }

        [Fact]
        public void WhoAmI_WithAuthenticatedUserButInvalidUpn_ShouldReturnInternalServerError()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new("upn", "invalid@test.com"),
                new("name", "Test User")
            };

            SetupAuthenticatedUser(claims);
            
            // Symulacja problemu z ICurrentUserService - zwraca wartość domyślną
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("system@teamsmanager.local");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("system-id");

            // Act
            var result = _controller.WhoAmI();

            // Assert
            var serverErrorResult = result.Should().BeOfType<ObjectResult>().Subject;
            serverErrorResult.StatusCode.Should().Be(500);
            
            var response = serverErrorResult.Value.Should().BeAssignableTo<object>().Subject;
            var responseType = response.GetType();
            responseType.GetProperty("Message")?.GetValue(response)?.ToString()
                .Should().Contain("Nie udało się poprawnie zidentyfikować użytkownika");
        }

        [Fact]
        public void WhoAmI_WithEmptyUpnFromCurrentUserService_ShouldReturnInternalServerError()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new("upn", "test@test.com"),
                new("name", "Test User")
            };

            SetupAuthenticatedUser(claims);
            
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns(string.Empty);
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns((string?)null);

            // Act
            var result = _controller.WhoAmI();

            // Assert
            var serverErrorResult = result.Should().BeOfType<ObjectResult>().Subject;
            serverErrorResult.StatusCode.Should().Be(500);
        }

        [Fact]
        public void WhoAmI_WithUnknownUpnFromCurrentUserService_ShouldReturnInternalServerError()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new("upn", "test@test.com"),
                new("name", "Test User")
            };

            SetupAuthenticatedUser(claims);
            
            _mockCurrentUserService.Setup(x => x.GetCurrentUserUpn()).Returns("unknown@teamsmanager.local");
            _mockCurrentUserService.Setup(x => x.GetCurrentUserId()).Returns("unknown-id");

            // Act
            var result = _controller.WhoAmI();

            // Assert
            var serverErrorResult = result.Should().BeOfType<ObjectResult>().Subject;
            serverErrorResult.StatusCode.Should().Be(500);
        }

        #endregion

        #region PublicInfo Tests

        [Fact]
        public void PublicInfo_ShouldReturnOkWithPublicMessage()
        {
            // Arrange & Act
            var result = _controller.PublicInfo();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
            
            var responseType = response.GetType();
            responseType.GetProperty("Message")?.GetValue(response)?.ToString()
                .Should().Be("To jest publiczny endpoint, dostępny bez logowania.");
        }

        [Fact]
        public void PublicInfo_ShouldNotRequireAuthentication()
        {
            // Arrange
            SetupUnauthenticatedUser(); // Brak uwierzytelniania

            // Act
            var result = _controller.PublicInfo();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public void PublicInfo_ShouldWorkWithAuthenticatedUser()
        {
            // Arrange
            var claims = new List<Claim> { new("upn", "test@test.com") };
            SetupAuthenticatedUser(claims);

            // Act
            var result = _controller.PublicInfo();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region Helper Methods

        private void SetupAuthenticatedUser(List<Claim> claims)
        {
            var identity = new ClaimsIdentity(claims, "Bearer");
            var principal = new ClaimsPrincipal(identity);
            
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private void SetupUnauthenticatedUser()
        {
            var identity = new ClaimsIdentity(); // Brak claims i authenticationType
            var principal = new ClaimsPrincipal(identity);
            
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #endregion
    }
} 