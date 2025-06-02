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
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    public class ChannelsControllerTests
    {
        private readonly Mock<IChannelService> _mockChannelService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<ChannelsController>> _mockLogger;
        private readonly ChannelsController _controller;
        private readonly string _validAccessToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private readonly string _accessTokenValue = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";

        public ChannelsControllerTests()
        {
            _mockChannelService = new Mock<IChannelService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<ChannelsController>>();

            // ChannelsController ma teraz 3 parametry w konstruktorze: IChannelService, ICurrentUserService, ILogger
            _controller = new ChannelsController(_mockChannelService.Object, _mockCurrentUserService.Object, _mockLogger.Object);
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

        #region GetTeamChannels Tests

        [Fact]
        public async Task GetTeamChannels_WithValidToken_ShouldReturnChannels()
        {
            // Arrange
            var teamId = "team123";
            var channels = new List<Channel>
            {
                new Channel { Id = "channel1", DisplayName = "General" },
                new Channel { Id = "channel2", DisplayName = "Random" }
            };
            SetupControllerContext(_validAccessToken);
            
            _mockChannelService.Setup(s => s.GetTeamChannelsAsync(teamId, _accessTokenValue, false))
                              .ReturnsAsync(channels);

            // Act
            var result = await _controller.GetTeamChannels(teamId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(channels);
        }

        [Fact]
        public async Task GetTeamChannels_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var teamId = "team123";
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.GetTeamChannels(teamId);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak tokenu dostępu." });
            _mockChannelService.Verify(s => s.GetTeamChannelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        #endregion

        #region CreateTeamChannel Tests

        [Fact]
        public async Task CreateTeamChannel_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.CreateChannelRequestDto
            {
                DisplayName = "Test Channel",
                Description = "Test description",
                IsPrivate = false
            };
            var createdChannel = new Channel { Id = "channel123", DisplayName = requestDto.DisplayName };
            SetupControllerContext(_validAccessToken);
            
            _mockChannelService.Setup(s => s.CreateTeamChannelAsync(teamId, requestDto.DisplayName, _accessTokenValue, requestDto.Description, requestDto.IsPrivate))
                              .ReturnsAsync(createdChannel);

            // Act
            var result = await _controller.CreateTeamChannel(teamId, requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(ChannelsController.GetTeamChannelById));
            createdResult.RouteValues.Should().ContainKey("teamId").WhoseValue.Should().Be(teamId);
            createdResult.RouteValues.Should().ContainKey("channelGraphId").WhoseValue.Should().Be(createdChannel.Id);
            createdResult.Value.Should().Be(createdChannel);
        }

        [Fact]
        public async Task CreateTeamChannel_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new TeamsManager.Api.Controllers.CreateChannelRequestDto { DisplayName = "Test Channel" };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.CreateTeamChannel(teamId, requestDto);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { Message = "Brak tokenu dostępu." });
            _mockChannelService.Verify(s => s.CreateTeamChannelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        #endregion

        #region UpdateTeamChannel Tests

        [Fact]
        public async Task UpdateTeamChannel_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            var channelId = "channel123";
            var requestDto = new TeamsManager.Api.Controllers.UpdateChannelRequestDto
            {
                NewDisplayName = "Updated Channel",
                NewDescription = "Updated description"
            };
            var updatedChannel = new Channel { Id = channelId, DisplayName = requestDto.NewDisplayName };
            SetupControllerContext(_validAccessToken);
            
            _mockChannelService.Setup(s => s.UpdateTeamChannelAsync(teamId, channelId, _accessTokenValue, requestDto.NewDisplayName, requestDto.NewDescription))
                              .ReturnsAsync(updatedChannel);

            // Act
            var result = await _controller.UpdateTeamChannel(teamId, channelId, requestDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(updatedChannel);
        }

        [Fact]
        public async Task UpdateTeamChannel_WithEmptyData_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team123";
            var channelId = "channel123";
            var requestDto = new TeamsManager.Api.Controllers.UpdateChannelRequestDto(); // Empty data
            SetupControllerContext(_validAccessToken);

            // Act
            var result = await _controller.UpdateTeamChannel(teamId, channelId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Należy podać przynajmniej nową nazwę lub nowy opis." });
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
            var teamId = "team123";
            SetupControllerContext(authHeader);
            
            if (shouldExtractToken)
            {
                string expectedToken = authHeader.Substring("Bearer ".Length).Trim();
                var channels = new List<Channel> { new Channel { Id = "channel1", DisplayName = "Test Channel" } };
                
                _mockChannelService.Setup(s => s.GetTeamChannelsAsync(teamId, expectedToken, false))
                                  .ReturnsAsync(channels);

                // Act
                var result = await _controller.GetTeamChannels(teamId);

                // Assert
                result.Should().BeOfType<OkObjectResult>();
                _mockChannelService.Verify(s => s.GetTeamChannelsAsync(teamId, expectedToken, false), Times.Once);
            }
            else
            {
                // Act
                var result = await _controller.GetTeamChannels(teamId);

                // Assert
                result.Should().BeOfType<UnauthorizedObjectResult>();
                _mockChannelService.Verify(s => s.GetTeamChannelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullChannelService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(_mockChannelService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(_mockChannelService.Object, _mockCurrentUserService.Object, null!));
        }

        #endregion
    }
} 