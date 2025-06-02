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

            // UsersController ma teraz 3 parametry w konstruktorze: IUserService, ICurrentUserService, ILogger
            _controller = new UsersController(_mockUserService.Object, _mockCurrentUserService.Object, _mockLogger.Object);
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
        public async Task GetUserById_WithValidId_ShouldReturnUser()
        {
            // Arrange
            var userId = "123";
            var user = new User { Id = userId, UPN = "test@example.com", FirstName = "Test", LastName = "User" };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, false, _accessTokenValue))
                           .ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(user);
        }

        [Fact]
        public async Task GetUserById_WithoutToken_ShouldReturnUser()
        {
            // Arrange
            var userId = "user123";
            var expectedUser = new User { Id = userId, UPN = "test@example.com", FirstName = "Test", LastName = "User" };
            SetupControllerContext(); // No token

            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, false, null))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(expectedUser);
            _mockUserService.Verify(s => s.GetUserByIdAsync(userId, false, null), Times.Once);
        }

        [Fact]
        public async Task GetUserById_UserNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var userId = "nonexistent";
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, false, _accessTokenValue))
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
        public async Task GetUserByUpn_WithValidUpn_ShouldReturnUser()
        {
            // Arrange
            var upn = "test@example.com";
            var user = new User { Id = "123", UPN = upn, FirstName = "Test", LastName = "User" };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(upn, false, _accessTokenValue))
                           .ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserByUpn(upn);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(user);
        }

        [Fact]
        public async Task GetUserByUpn_WithValidUpn_AndForceRefresh_ShouldReturnUser()
        {
            // Arrange
            var upn = "test@example.com";
            var user = new User { Id = "123", UPN = upn, FirstName = "Test", LastName = "User" };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(upn, true, _accessTokenValue))
                           .ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserByUpn(upn, forceRefresh: true);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(user);
        }

        #endregion

        #region GetAllActiveUsers Tests

        [Fact]
        public async Task GetAllActiveUsers_ShouldReturnUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = "1", UPN = "user1@example.com", FirstName = "User", LastName = "One" },
                new User { Id = "2", UPN = "user2@example.com", FirstName = "User", LastName = "Two" }
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetAllActiveUsersAsync(false, _accessTokenValue))
                           .ReturnsAsync(users);

            // Act
            var result = await _controller.GetAllActiveUsers();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        [Fact]
        public async Task GetAllActiveUsers_WithForceRefresh_ShouldReturnUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = "1", UPN = "user1@example.com", FirstName = "User", LastName = "One" }
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetAllActiveUsersAsync(true, _accessTokenValue))
                           .ReturnsAsync(users);

            // Act
            var result = await _controller.GetAllActiveUsers(forceRefresh: true);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        #endregion

        #region GetUsersByRole Tests

        [Fact]
        public async Task GetUsersByRole_WithValidRole_ShouldReturnUsers()
        {
            // Arrange
            var role = UserRole.Nauczyciel;
            var users = new List<User>
            {
                new User { Id = "1", UPN = "teacher1@example.com", FirstName = "Teacher", LastName = "One", Role = role }
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUsersByRoleAsync(role, false, _accessTokenValue))
                           .ReturnsAsync(users);

            // Act
            var result = await _controller.GetUsersByRole(role);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        [Fact]
        public async Task GetUsersByRole_WithForceRefresh_ShouldReturnUsers()
        {
            // Arrange
            var role = UserRole.Uczen;
            var users = new List<User>
            {
                new User { Id = "1", UPN = "student1@example.com", FirstName = "Student", LastName = "One", Role = role }
            };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUsersByRoleAsync(role, true, _accessTokenValue))
                           .ReturnsAsync(users);

            // Act
            var result = await _controller.GetUsersByRole(role, forceRefresh: true);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(users);
        }

        #endregion

        #region CreateUser Tests

        [Fact]
        public async Task CreateUser_WithValidData_ShouldReturnCreatedUser()
        {
            // Arrange
            var createDto = new TeamsManager.Api.Controllers.CreateUserRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                Upn = "john.doe@example.com",
                Role = UserRole.Uczen,
                DepartmentId = "dept-123",
                Password = "SecurePassword123!"
            };
            
            var createdUser = new User 
            { 
                Id = "new-user-123", 
                UPN = createDto.Upn, 
                FirstName = createDto.FirstName, 
                LastName = createDto.LastName,
                Role = createDto.Role 
            };
            
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.CreateUserAsync(
                createDto.FirstName,
                createDto.LastName, 
                createDto.Upn,
                createDto.Role,
                createDto.DepartmentId,
                createDto.Password,
                _accessTokenValue,
                createDto.SendWelcomeEmail))
                .ReturnsAsync(createdUser);

            // Act
            var result = await _controller.CreateUser(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(UsersController.GetUserById));
        }

        [Fact]
        public async Task CreateUser_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var createDto = new TeamsManager.Api.Controllers.CreateUserRequestDto
            {
                FirstName = "John",
                LastName = "Doe",
                Upn = "john.doe@example.com", 
                Role = UserRole.Uczen,
                DepartmentId = "dept-123",
                Password = "SecurePassword123!"
            };
            
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.CreateUser(createDto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
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
            
            _mockUserService.Setup(s => s.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<bool>()))
                           .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.CreateUser(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się utworzyć użytkownika. Sprawdź logi serwera." });
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
            
            // Mock dla GetUserByIdAsync - kontroler sprawdza czy użytkownik istnieje
            var existingUser = new User { Id = userId, UPN = "existing@example.com", FirstName = "John", LastName = "Doe" };
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, false, _accessTokenValue))
                           .ReturnsAsync(existingUser);
            
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
        public async Task DeactivateUser_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var userId = "user-123";
            var dto = new TeamsManager.Api.Controllers.UserActionM365Dto { PerformM365Action = true };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.DeactivateUserAsync(userId, _accessTokenValue, dto.PerformM365Action))
                           .ReturnsAsync(true);

            // Act  
            var result = await _controller.DeactivateUser(userId, dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region ActivateUser Tests

        [Fact]
        public async Task ActivateUser_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var userId = "user-123";
            var dto = new TeamsManager.Api.Controllers.UserActionM365Dto { PerformM365Action = true };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.ActivateUserAsync(userId, _accessTokenValue, dto.PerformM365Action))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.ActivateUser(userId, dto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region TokenExtraction Tests

        [Theory]
        [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("BEARER eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token", true)]
        [InlineData("Basic dXNlcjpwYXNzd29yZA==", false)]
        [InlineData("", false)]
        public async Task TokenExtraction_VariousAuthHeaders_ShouldHandleCorrectly(string authHeader, bool shouldExtractToken)
        {
            // Arrange
            var userId = "user123";
            var expectedUser = new User { Id = userId, UPN = "test@example.com", FirstName = "Test", LastName = "User" };
            SetupControllerContext(authHeader);
            
            string? expectedToken = shouldExtractToken ? authHeader.Substring("Bearer ".Length).Trim() : null;
            
            _mockUserService.Setup(s => s.GetUserByIdAsync(userId, false, expectedToken))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserById(userId);

            // Assert
            if (shouldExtractToken)
            {
                result.Should().BeOfType<OkObjectResult>();
                _mockUserService.Verify(s => s.GetUserByIdAsync(userId, false, expectedToken), Times.Once);
            }
            else
            {
                result.Should().BeOfType<OkObjectResult>();
                _mockUserService.Verify(s => s.GetUserByIdAsync(userId, false, null), Times.Once);
            }
        }

        [Fact]
        public async Task GetUserByUpn_WithoutToken_ShouldReturnUser()
        {
            // Arrange
            var upn = "test@example.com";
            var user = new User { Id = "123", UPN = upn, FirstName = "Test", LastName = "User" };
            SetupControllerContext(); // No token
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(upn, false, null))
                           .ReturnsAsync(user);

            // Act
            var result = await _controller.GetUserByUpn(upn);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(user);
        }

        [Fact]
        public async Task GetUserByUpn_WithEncodedUpn_ShouldDecodeAndCallService()
        {
            // Arrange
            var encodedUpn = "john.doe%40example.com";
            var decodedUpn = "john.doe@example.com";
            var expectedUser = new User { Id = "user123", UPN = decodedUpn };
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(decodedUpn, false, _accessTokenValue))
                           .ReturnsAsync(expectedUser);

            // Act
            var result = await _controller.GetUserByUpn(encodedUpn);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockUserService.Verify(s => s.GetUserByUpnAsync(decodedUpn, false, _accessTokenValue), Times.Once);
        }

        [Fact]
        public async Task GetUserByUpn_WithNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var upn = "notfound@example.com";
            SetupControllerContext(_validAccessToken);
            
            _mockUserService.Setup(s => s.GetUserByUpnAsync(upn, false, _accessTokenValue))
                           .ReturnsAsync((User?)null);

            // Act
            var result = await _controller.GetUserByUpn(upn);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Użytkownik o UPN '{upn}' nie został znaleziony." });
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