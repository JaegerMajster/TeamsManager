using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    public class UsersControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UsersController>> _mockLogger;
        private readonly UsersController _controller;
        private readonly string _validAccessToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private readonly string _accessTokenValue = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";

        public UsersControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UsersController>>();
            _controller = new UsersController(_mockUserService.Object, _mockCurrentUserService.Object, _mockLogger.Object);

            SetupControllerContext();
        }

        private void SetupControllerContext(string? authorizationHeader = null)
        {
            var httpContext = new DefaultHttpContext();
            
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                httpContext.Request.Headers["Authorization"] = new StringValues(authorizationHeader);
            }

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        #region GetUserById Tests

        [Fact]
        public async Task GetUserById_WithValidToken_ShouldReturnUser()
        {
            // Arrange
            var userId = "user123";
            var expectedUser = new User { Id = userId, FirstName = "John", LastName = "Doe", Upn = "john.doe@example.com" };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, _accessTokenValue))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedUser);
            _mockUserService.Verify(s => s.GetUserByIdAsync(userId, _accessTokenValue), Times.Once);
        }

        [Fact]
        public async Task GetUserById_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var userId = "user123";
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak wymaganego tokenu dostępu." });
            _mockUserService.Verify(s => s.GetUserByIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetUserById_UserNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var userId = "nonexistent";
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, _accessTokenValue))
                           .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Użytkownik o ID '{userId}' nie został znaleziony." });
        }

        #endregion

        #region GetUserByUpn Tests

        [Fact]
        public async Task GetUserByUpn_WithValidToken_ShouldReturnUser()
        {
            // Arrange
            var upn = "john.doe@example.com";
            var expectedUser = new User { Id = "user123", FirstName = "John", LastName = "Doe", Upn = upn };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(upn, _accessTokenValue))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserByUpn(upn);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedUser);
            _mockUserService.Verify(s => s.GetUserByUpnAsync(upn, _accessTokenValue), Times.Once);
        }

        [Fact]
        public async Task GetUserByUpn_WithEncodedUpn_ShouldDecodeAndCallService()
        {
            // Arrange
            var encodedUpn = "john.doe%40example.com";
            var decodedUpn = "john.doe@example.com";
            var expectedUser = new User { Id = "user123", Upn = decodedUpn };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(decodedUpn, _accessTokenValue))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserByUpn(encodedUpn);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockUserService.Verify(s => s.GetUserByUpnAsync(decodedUpn, _accessTokenValue), Times.Once);
        }

        #endregion

        #region CreateUser Tests

        [Fact]
        public async Task CreateUser_WithValidTokenAndData_ShouldReturnCreated()
        {
            // Arrange
            var requestDto = new CreateUserRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                Upn = "john.doe@example.com",
                Password = "SecurePassword123!",
                Role = UserRole.Uczen,
                DepartmentId = "dept123"
            };
            var createdUser = new User 
            { 
                Id = "user123", 
                FirstName = requestDto.FirstName, 
                LastName = requestDto.LastName,
                Upn = requestDto.Upn,
                Role = requestDto.Role,
                DepartmentId = requestDto.DepartmentId
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.CreateUserAsync(
                requestDto.FirstName,
                requestDto.LastName,
                requestDto.Upn,
                requestDto.Password,
                requestDto.Role,
                requestDto.DepartmentId,
                _accessTokenValue,
                requestDto.SendWelcomeEmail,
                requestDto.Phone,
                requestDto.AlternateEmail,
                requestDto.ExternalId,
                requestDto.BirthDate,
                requestDto.EmploymentDate,
                requestDto.Position,
                requestDto.Notes,
                requestDto.IsSystemAdmin))
                           .ReturnsAsync(createdUser);

            // Act
            var result = await _controller.CreateUser(requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(UsersController.GetUserById));
            createdResult.RouteValues.Should().ContainKey("userId").WhoseValue.Should().Be(createdUser.Id);
            createdResult.Value.Should().BeEquivalentTo(createdUser);
        }

        [Fact]
        public async Task CreateUser_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var requestDto = new CreateUserRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                Upn = "john.doe@example.com",
                Password = "SecurePassword123!"
            };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.CreateUser(requestDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak wymaganego tokenu dostępu." });
        }

        [Fact]
        public async Task CreateUser_ServiceReturnsNull_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new CreateUserRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                Upn = "john.doe@example.com",
                Password = "SecurePassword123!"
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), 
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), 
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
                           .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.CreateUser(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się utworzyć użytkownika." });
        }

        #endregion

        #region UpdateUser Tests

        [Fact]
        public async Task UpdateUser_WithValidTokenAndData_ShouldReturnNoContent()
        {
            // Arrange
            var userId = "user123";
            var requestDto = new UpdateUserRequestDto
            {
                FirstName = "Updated John",
                LastName = "Updated Doe"
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.UpdateUserAsync(It.IsAny<User>(), _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateUser(userId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockUserService.Verify(s => s.UpdateUserAsync(It.Is<User>(u => 
                u.Id == userId && u.FirstName == requestDto.FirstName), _accessTokenValue), Times.Once);
        }

        [Fact]
        public async Task UpdateUser_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var userId = "user123";
            var requestDto = new UpdateUserRequestDto { FirstName = "Updated" };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.UpdateUser(userId, requestDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak wymaganego tokenu dostępu." });
        }

        #endregion

        #region DeactivateUser Tests

        [Fact]
        public async Task DeactivateUser_WithValidToken_ShouldReturnOk()
        {
            // Arrange
            var userId = "user123";
            var reason = "Test deactivation";
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.DeactivateUserAsync(userId, reason, _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.DeactivateUser(userId, reason);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Użytkownik został pomyślnie dezaktywowany." });
        }

        #endregion

        #region ActivateUser Tests

        [Fact]
        public async Task ActivateUser_WithValidToken_ShouldReturnOk()
        {
            // Arrange
            var userId = "user123";
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.ActivateUserAsync(userId, _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.ActivateUser(userId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Użytkownik został pomyślnie aktywowany." });
        }

        #endregion

        #region Token Extraction Tests

        [Theory]
        [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("BEARER eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("Basic dXNlcjpwYXNzd29yZA==", false)]
        [InlineData("eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", false)]
        [InlineData("", false)]
        public async Task TokenExtraction_VariousAuthHeaders_ShouldHandleCorrectly(string authHeader, bool shouldExtractToken)
        {
            // Arrange
            var userId = "user123";
            var expectedUser = new User { Id = userId };
            SetupControllerContext(authHeader);
            
            string? expectedToken = shouldExtractToken ? authHeader.Substring("Bearer ".Length).Trim() : null;
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, expectedToken!))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            if (shouldExtractToken)
            {
                result.Should().BeOfType<OkObjectResult>();
                _mockUserService.Verify(s => s.GetUserByIdAsync(userId, expectedToken!), Times.Once);
            }
            else
            {
                result.Should().BeOfType<UnauthorizedObjectResult>();
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UsersController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UsersController(_mockUserService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new UsersController(_mockUserService.Object, _mockCurrentUserService.Object, null!));
        }

        #endregion
    }
} 