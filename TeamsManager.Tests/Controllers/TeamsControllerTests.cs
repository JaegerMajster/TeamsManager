using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla TeamsController
    /// Pokrycie: endpointy CRUD zespołów, członkowie, archiwizacja
    /// </summary>
    public class TeamsControllerTests
    {
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamsController>> _mockLogger;
        private readonly TeamsController _controller;

        public TeamsControllerTests()
        {
            _mockTeamService = new Mock<ITeamService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamsController>>();

            _controller = new TeamsController(
                _mockTeamService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);

            // Setup HttpContext dla testów
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        #region CreateTeam Tests

        [Fact]
        public async Task CreateTeam_WithValidData_ShouldReturnCreatedResult()
        {
            // Arrange
            var requestDto = new CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                Description = "Test Description",
                OwnerUpn = "owner@test.com",
                Visibility = TeamVisibility.Private,
                TeamTemplateId = "template-123",
                SchoolTypeId = "school-type-123",
                SchoolYearId = "school-year-123"
            };

            var createdTeam = CreateTestTeam("team-123", requestDto.DisplayName, 
                requestDto.Description, requestDto.OwnerUpn);

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.CreateTeamAsync(
                requestDto.DisplayName,
                requestDto.Description,
                requestDto.OwnerUpn,
                requestDto.Visibility,
                "valid-token",
                requestDto.TeamTemplateId,
                requestDto.SchoolTypeId,
                requestDto.SchoolYearId,
                requestDto.AdditionalTemplateValues))
                .ReturnsAsync(createdTeam);

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = result as CreatedAtActionResult;
            createdResult!.Value.Should().Be(createdTeam);
            createdResult.ActionName.Should().Be(nameof(TeamsController.GetTeamById));
        }

        [Fact]
        public async Task CreateTeam_WithoutAuthorizationHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            var requestDto = new CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                Description = "Test Description",
                OwnerUpn = "owner@test.com"
            };

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task CreateTeam_WhenServiceReturnsNull_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new CreateTeamRequestDto
            {
                DisplayName = "Test Team",
                Description = "Test Description",
                OwnerUpn = "owner@test.com"
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.CreateTeamAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TeamVisibility>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync((Team?)null);

            // Act
            var result = await _controller.CreateTeam(requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetTeamById Tests

        [Fact]
        public async Task GetTeamById_WithValidId_ShouldReturnTeam()
        {
            // Arrange
            var teamId = "team-123";
            var expectedTeam = CreateTestTeam(teamId, "Test Team", "Description", "owner@test.com");

            _mockTeamService.Setup(x => x.GetTeamByIdAsync(teamId, false, false, false, null))
                .ReturnsAsync(expectedTeam);

            // Act
            var result = await _controller.GetTeamById(teamId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().Be(expectedTeam);
        }

        [Fact]
        public async Task GetTeamById_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var teamId = "non-existent";

            _mockTeamService.Setup(x => x.GetTeamByIdAsync(teamId, false, false, false, null))
                .ReturnsAsync((Team?)null);

            // Act
            var result = await _controller.GetTeamById(teamId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetTeamById_WithIncludeMembers_ShouldPassCorrectParameters()
        {
            // Arrange
            var teamId = "team-123";
            var includeMembers = true;
            var includeChannels = false;

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.GetTeamByIdAsync(teamId, includeMembers, includeChannels, false, "valid-token"))
                .ReturnsAsync(CreateTestTeam(teamId, "Test Team", "Description", "owner@test.com"));

            // Act
            var result = await _controller.GetTeamById(teamId, includeMembers, includeChannels);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockTeamService.Verify(x => x.GetTeamByIdAsync(teamId, includeMembers, includeChannels, false, "valid-token"), Times.Once);
        }

        #endregion

        #region GetAllTeams Tests

        [Fact]
        public async Task GetAllTeams_ShouldReturnAllTeams()
        {
            // Arrange
            var teams = new List<Team>
            {
                CreateTestTeam("1", "Team 1", "Description 1", "owner1@test.com"),
                CreateTestTeam("2", "Team 2", "Description 2", "owner2@test.com")
            };

            _mockTeamService.Setup(x => x.GetAllTeamsAsync(false, null))
                .ReturnsAsync(teams);

            // Act
            var result = await _controller.GetAllTeams();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(teams);
        }

        #endregion

        #region GetActiveTeams Tests

        [Fact]
        public async Task GetActiveTeams_ShouldReturnOnlyActiveTeams()
        {
            // Arrange
            var activeTeams = new List<Team>
            {
                CreateTestTeam("1", "Active Team 1", "Description 1", "owner1@test.com"),
                CreateTestTeam("2", "Active Team 2", "Description 2", "owner2@test.com")
            };

            _mockTeamService.Setup(x => x.GetActiveTeamsAsync(false, null))
                .ReturnsAsync(activeTeams);

            // Act
            var result = await _controller.GetActiveTeams();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(activeTeams);
        }

        #endregion

        #region UpdateTeam Tests

        [Fact]
        public async Task UpdateTeam_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team-123";
            var requestDto = new UpdateTeamRequestDto
            {
                DisplayName = "Updated Team",
                Description = "Updated Description",
                OwnerUpn = "new-owner@test.com",
                Visibility = TeamVisibility.Public
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            var existingTeam = CreateTestTeam(teamId, "Old Name", "Old Description", "old-owner@test.com");

            _mockTeamService.Setup(x => x.GetTeamByIdAsync(teamId, false, false, false, "valid-token"))
                .ReturnsAsync(existingTeam);

            _mockTeamService.Setup(x => x.UpdateTeamAsync(It.IsAny<Team>(), "valid-token"))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateTeam(teamId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockTeamService.Verify(x => x.UpdateTeamAsync(It.IsAny<Team>(), "valid-token"), Times.Once);
        }

        [Fact]
        public async Task UpdateTeam_WithNonExistentTeam_ShouldReturnNotFound()
        {
            // Arrange
            var teamId = "non-existent";
            var requestDto = new UpdateTeamRequestDto
            {
                DisplayName = "Updated Team",
                Description = "Updated Description"
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.GetTeamByIdAsync(teamId, false, false, false, null))
                .ReturnsAsync((Team?)null);

            // Act
            var result = await _controller.UpdateTeam(teamId, requestDto);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region ArchiveTeam Tests

        [Fact]
        public async Task ArchiveTeam_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team-123";
            var requestDto = new ArchiveTeamRequestDto
            {
                Reason = "End of school year"
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.ArchiveTeamAsync(teamId, requestDto.Reason, "valid-token"))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.ArchiveTeam(teamId, requestDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ArchiveTeam_WhenServiceReturnsFalse_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team-123";
            var requestDto = new ArchiveTeamRequestDto
            {
                Reason = "Test reason"
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.ArchiveTeamAsync(teamId, requestDto.Reason, "valid-token"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.ArchiveTeam(teamId, requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region AddMember Tests

        [Fact]
        public async Task AddMember_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team-123";
            var requestDto = new AddMemberRequestDto
            {
                UserUpn = "user@test.com",
                Role = TeamMemberRole.Member
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            var createdMember = new TeamMember
            {
                Id = "member-123",
                TeamId = teamId,
                UserId = "user-123",
                Role = requestDto.Role,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "admin@test.com"
            };

            _mockTeamService.Setup(x => x.AddMemberAsync(teamId, requestDto.UserUpn, requestDto.Role, "valid-token"))
                .ReturnsAsync(createdMember);

            // Act
            var result = await _controller.AddMember(teamId, requestDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task AddMember_WhenServiceReturnsNull_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team-123";
            var requestDto = new AddMemberRequestDto
            {
                UserUpn = "user@test.com",
                Role = TeamMemberRole.Member
            };

            _controller.HttpContext.Request.Headers.Authorization = "Bearer valid-token";

            _mockTeamService.Setup(x => x.AddMemberAsync(teamId, requestDto.UserUpn, requestDto.Role, "valid-token"))
                .ReturnsAsync((TeamMember?)null);

            // Act
            var result = await _controller.AddMember(teamId, requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region Helper Methods

        private static Team CreateTestTeam(string id, string displayName, string description, string owner)
        {
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Owner = owner,
                Status = TeamStatus.Active,
                Visibility = TeamVisibility.Private,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 