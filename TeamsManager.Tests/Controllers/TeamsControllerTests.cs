using System;
using System.Collections.Generic;
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
    public class TeamsControllerTests
    {
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamsController>> _mockLogger;
        private readonly TeamsController _controller;
        private readonly string _validAccessToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private readonly string _accessTokenValue = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";

        public TeamsControllerTests()
        {
            _mockTeamService = new Mock<ITeamService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamsController>>();

            // TeamsController ma 3 parametry w konstruktorze
            _controller = new TeamsController(_mockTeamService.Object, _mockCurrentUserService.Object, _mockLogger.Object);

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

        #region CreateTeam Tests

        [Fact]
        public async Task CreateTeam_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var requestDto = new TeamsManager.Api.Controllers.CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                Description = "Test Description",
                OwnerUpn = "owner@example.com",
                Visibility = TeamVisibility.Private,
                AdditionalTemplateValues = new Dictionary<string, string> { { "key", "value" } }
            };
            var createdTeam = new Team { Id = "team123", DisplayName = requestDto.DisplayName };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.CreateTeamAsync(
                requestDto.DisplayName,
                requestDto.Description,
                requestDto.OwnerUpn,
                requestDto.Visibility,
                _accessTokenValue,
                requestDto.TeamTemplateId,
                requestDto.SchoolTypeId,
                requestDto.SchoolYearId,
                requestDto.AdditionalTemplateValues))
                .ReturnsAsync(createdTeam);

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(TeamsController.GetTeamById));
            createdResult.RouteValues.Should().ContainKey("teamId").WhoseValue.Should().Be(createdTeam.Id);
            createdResult.Value.Should().Be(createdTeam);
        }

        [Fact]
        public async Task CreateTeam_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var requestDto = new TeamsManager.Api.Controllers.CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                OwnerUpn = "owner@example.com"
            };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak wymaganego tokenu dostępu." });
        }

        [Fact]
        public async Task CreateTeam_ServiceReturnsNull_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new TeamsManager.Api.Controllers.CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                OwnerUpn = "owner@example.com"
            };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.CreateTeamAsync(It.IsAny<string>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<TeamVisibility>(), It.IsAny<string>(), 
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync((Team?)null);

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się utworzyć zespołu. Sprawdź logi serwera." });
        }

        #endregion

        #region GetTeamById Tests

        [Fact]
        public async Task GetTeamById_WithValidId_ShouldReturnTeam()
        {
            // Arrange
            var teamId = "team123";
            var team = new Team { Id = teamId, DisplayName = "Test Team" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.GetTeamByIdAsync(teamId, false, false, false, _accessTokenValue))
                           .ReturnsAsync(team);

            // Act
            var result = await _controller.GetTeamById(teamId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(team);
        }

        [Fact]
        public async Task GetTeamById_WithIncludeOptions_ShouldReturnTeamWithDetails()
        {
            // Arrange
            var teamId = "team123";
            var team = new Team { Id = teamId, DisplayName = "Test Team" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.GetTeamByIdAsync(teamId, true, true, false, _accessTokenValue))
                           .ReturnsAsync(team);

            // Act
            var result = await _controller.GetTeamById(teamId, includeMembers: true, includeChannels: true);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(team);
        }

        [Fact]
        public async Task GetTeamById_TeamNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var teamId = "nonexistent";
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.GetTeamByIdAsync(teamId, false, false, false, _accessTokenValue))
                           .ReturnsAsync((Team?)null);

            // Act
            var result = await _controller.GetTeamById(teamId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Zespół o ID '{teamId}' nie został znaleziony." });
        }

        #endregion

        #region UpdateTeam Tests

        [Fact]
        public async Task UpdateTeam_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.UpdateTeamRequestDto
            {
                DisplayName = "Updated Team",
                Description = "Updated Description",
                OwnerUpn = "newowner@example.com"
            };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.UpdateTeamAsync(It.IsAny<Team>(), _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateTeam(teamId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateTeam_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.UpdateTeamRequestDto { DisplayName = "Updated" };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.UpdateTeam(teamId, requestDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak wymaganego tokenu dostępu." });
        }

        [Fact]
        public async Task UpdateTeam_ServiceReturnsFalse_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.UpdateTeamRequestDto { DisplayName = "Updated" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.UpdateTeamAsync(It.IsAny<Team>(), _accessTokenValue))
                           .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateTeam(teamId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zaktualizować zespołu." });
        }

        #endregion

        #region ArchiveTeam Tests

        [Fact]
        public async Task ArchiveTeam_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.ArchiveTeamRequestDto { Reason = "Test reason" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.ArchiveTeamAsync(teamId, requestDto.Reason, _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.ArchiveTeam(teamId, requestDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Zespół zarchiwizowany pomyślnie." });
        }

        [Fact]
        public async Task ArchiveTeam_ServiceReturnsFalse_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.ArchiveTeamRequestDto { Reason = "Test reason" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.ArchiveTeamAsync(teamId, requestDto.Reason, _accessTokenValue))
                           .ReturnsAsync(false);

            // Act
            var result = await _controller.ArchiveTeam(teamId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zarchiwizować zespołu." });
        }

        [Fact]
        public async Task RestoreTeam_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.RestoreTeamAsync(teamId, _accessTokenValue))
                           .ReturnsAsync(true);

            // Act
            var result = await _controller.RestoreTeam(teamId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Zespół przywrócony pomyślnie." });
        }

        #endregion

        #region AddMember Tests

        [Fact]
        public async Task AddMember_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.AddMemberRequestDto
            {
                UserUpn = "user@example.com",
                Role = TeamMemberRole.Member
            };
            var addedMember = new TeamMember { Id = "member123", UserId = "user123" };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.AddMemberAsync(teamId, requestDto.UserUpn, requestDto.Role, _accessTokenValue))
                           .ReturnsAsync(addedMember);

            // Act
            var result = await _controller.AddMember(teamId, requestDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(addedMember);
        }

        [Fact]
        public async Task AddMember_ServiceReturnsNull_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.AddMemberRequestDto
            {
                UserUpn = "user@example.com",
                Role = TeamMemberRole.Member
            };
            SetupControllerContext(_validAccessToken);
            
            _mockTeamService.Setup(s => s.AddMemberAsync(teamId, requestDto.UserUpn, requestDto.Role, _accessTokenValue))
                           .ReturnsAsync((TeamMember?)null);

            // Act
            var result = await _controller.AddMember(teamId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się dodać członka do zespołu." });
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
            var teamId = "team123";
            var expectedTeam = new Team { Id = teamId };
            SetupControllerContext(authHeader);
            
            string? expectedToken = shouldExtractToken ? authHeader.Substring("Bearer ".Length).Trim() : null;
            
            _mockTeamService.Setup(s => s.GetTeamByIdAsync(teamId, false, false, false, expectedToken))
                           .ReturnsAsync(expectedTeam);

            // Act
            var result = await _controller.GetTeamById(teamId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockTeamService.Verify(s => s.GetTeamByIdAsync(teamId, false, false, false, expectedToken), Times.Once);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullTeamService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamsController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamsController(_mockTeamService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamsController(_mockTeamService.Object, _mockCurrentUserService.Object, null!));
        }

        #endregion
    }

    // Dummy DTOs for testing - these should match the actual DTOs from the controller
    public class CreateTeamRequestDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OwnerUpn { get; set; } = string.Empty;
        public TeamVisibility Visibility { get; set; } = TeamVisibility.Private;
        public string? TeamTemplateId { get; set; }
        public string? SchoolTypeId { get; set; }
        public string? SchoolYearId { get; set; }
        public Dictionary<string, string?> AdditionalTemplateValues { get; set; } = new Dictionary<string, string?>();
    }

    public class UpdateTeamRequestDto
    {
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }

    public class ArchiveTeamRequestDto
    {
        public string? Reason { get; set; }
    }

    public class AddMemberRequestDto
    {
        public string UserUpn { get; set; } = string.Empty;
        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
    }
} 