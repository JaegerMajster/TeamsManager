using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions.Services;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    public class ChannelsControllerTests
    {
        private readonly Mock<IPowerShellService> _mockPowerShellService;
        private readonly Mock<ITeamService> _mockTeamService;
        private readonly Mock<ILogger<ChannelsController>> _mockLogger;
        private readonly ChannelsController _controller;
        private readonly string _validAccessToken = "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";
        private readonly string _accessTokenValue = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test.token";

        public ChannelsControllerTests()
        {
            _mockPowerShellService = new Mock<IPowerShellService>();
            _mockTeamService = new Mock<ITeamService>();
            _mockLogger = new Mock<ILogger<ChannelsController>>();
            _controller = new ChannelsController(_mockPowerShellService.Object, _mockTeamService.Object, _mockLogger.Object);

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

        private PSObject CreateMockPSObject(string id, string displayName, string? description = null)
        {
            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty("Id", id));
            psObject.Properties.Add(new PSNoteProperty("DisplayName", displayName));
            if (description != null)
            {
                psObject.Properties.Add(new PSNoteProperty("Description", description));
            }
            return psObject;
        }

        #region GetTeamChannels Tests

        [Fact]
        public async Task GetTeamChannels_WithValidToken_ShouldReturnChannels()
        {
            // Arrange
            var teamId = "team123";
            var channels = new Collection<PSObject>
            {
                CreateMockPSObject("channel1", "General", "General channel"),
                CreateMockPSObject("channel2", "Private Channel", "Private channel")
            };
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, 
                new[] { "Group.ReadWrite.All", "Channel.ReadBasic.All", "Channel.ReadWrite.All" }))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.GetTeamChannelsAsync(teamId))
                                  .ReturnsAsync(channels);

            // Act
            var result = await _controller.GetTeamChannels(teamId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedChannels = okResult.Value as IEnumerable<object>;
            returnedChannels.Should().HaveCount(2);
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
            var serviceUnavailableResult = result.Should().BeOfType<ObjectResult>().Subject;
            serviceUnavailableResult.StatusCode.Should().Be(503);
            serviceUnavailableResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
        }

        [Fact]
        public async Task GetTeamChannels_ConnectionFails_ShouldReturnServiceUnavailable()
        {
            // Arrange
            var teamId = "team123";
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(false);

            // Act
            var result = await _controller.GetTeamChannels(teamId);

            // Assert
            var serviceUnavailableResult = result.Should().BeOfType<ObjectResult>().Subject;
            serviceUnavailableResult.StatusCode.Should().Be(503);
            serviceUnavailableResult.Value.Should().BeEquivalentTo(new { Message = "Nie można połączyć się z Microsoft Graph API." });
        }

        #endregion

        #region GetTeamChannel Tests

        [Fact]
        public async Task GetTeamChannel_WithValidToken_ShouldReturnChannel()
        {
            // Arrange
            var teamId = "team123";
            var channelName = "General";
            var channel = CreateMockPSObject("channel1", channelName, "General channel");
            var channels = new Collection<PSObject> { channel };
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.GetTeamChannelAsync(teamId, channelName))
                                  .ReturnsAsync(channel);

            // Act
            var result = await _controller.GetTeamChannel(teamId, channelName);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task GetTeamChannel_ChannelNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var teamId = "team123";
            var channelName = "NonExistent";
            var emptyChannels = new Collection<PSObject>();
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.GetTeamChannelAsync(teamId, channelName))
                                  .ReturnsAsync((PSObject?)null);

            // Act
            var result = await _controller.GetTeamChannel(teamId, channelName);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Kanał '{channelName}' nie został znaleziony w zespole o ID '{teamId}'." });
        }

        #endregion

        #region CreateTeamChannel Tests

        [Fact]
        public async Task CreateTeamChannel_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new CreateChannelRequestDto
            {
                DisplayName = "New Channel",
                Description = "New channel description",
                IsPrivate = false
            };
            var createdChannel = CreateMockPSObject("newchannel1", requestDto.DisplayName, requestDto.Description);
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.CreateTeamChannelAsync(teamId, requestDto.DisplayName, requestDto.IsPrivate, requestDto.Description))
                                  .ReturnsAsync(createdChannel);

            // Act
            var result = await _controller.CreateTeamChannel(teamId, requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(ChannelsController.GetTeamChannel));
            createdResult.RouteValues.Should().ContainKey("teamId").WhoseValue.Should().Be(teamId);
            createdResult.RouteValues.Should().ContainKey("channelDisplayName").WhoseValue.Should().Be(requestDto.DisplayName);
        }

        [Fact]
        public async Task CreateTeamChannel_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange
            var teamId = "team123";
            var requestDto = new CreateChannelRequestDto { DisplayName = "New Channel" };
            SetupControllerContext(); // No token

            // Act
            var result = await _controller.CreateTeamChannel(teamId, requestDto);

            // Assert
            var serviceUnavailableResult = result.Should().BeOfType<ObjectResult>().Subject;
            serviceUnavailableResult.StatusCode.Should().Be(503);
            serviceUnavailableResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się połączyć z usługą Microsoft Graph." });
        }

        #endregion

        #region UpdateTeamChannel Tests

        [Fact]
        public async Task UpdateTeamChannel_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            var channelName = "Old Channel";
            var requestDto = new UpdateChannelRequestDto
            {
                NewDisplayName = "Updated Channel",
                NewDescription = "Updated description"
            };
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.UpdateTeamChannelAsync(teamId, channelName, requestDto.NewDisplayName, requestDto.NewDescription))
                                  .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateTeamChannel(teamId, channelName, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateTeamChannel_EmptyRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var teamId = "team123";
            var channelName = "Channel";
            var requestDto = new UpdateChannelRequestDto(); // Empty request
            SetupControllerContext(_validAccessToken);

            // Act
            var result = await _controller.UpdateTeamChannel(teamId, channelName, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Należy podać przynajmniej nową nazwę lub nowy opis." });
        }

        #endregion

        #region RemoveTeamChannel Tests

        [Fact]
        public async Task RemoveTeamChannel_ValidChannel_ShouldReturnOk()
        {
            // Arrange
            var teamId = "team123";
            var channelName = "TestChannel";
            SetupControllerContext(_validAccessToken);
            
            _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(_accessTokenValue, It.IsAny<string[]>()))
                                  .ReturnsAsync(true);
            
            _mockPowerShellService.Setup(s => s.RemoveTeamChannelAsync(teamId, channelName))
                                  .ReturnsAsync(true);

            // Act
            var result = await _controller.RemoveTeamChannel(teamId, channelName);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Kanał usunięty pomyślnie." });
        }

        [Theory]
        [InlineData("General")]
        [InlineData("general")]
        [InlineData("GENERAL")]
        [InlineData("Ogólny")]
        [InlineData("ogólny")]
        [InlineData("OGÓLNY")]
        public async Task RemoveTeamChannel_GeneralChannel_ShouldReturnBadRequest(string channelName)
        {
            // Arrange
            var teamId = "team123";
            SetupControllerContext(_validAccessToken);

            // Act
            var result = await _controller.RemoveTeamChannel(teamId, channelName);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie można usunąć kanału General/Ogólny." });
        }

        #endregion

        #region Token Extraction Tests

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
                
                _mockPowerShellService.Setup(s => s.ConnectWithAccessTokenAsync(expectedToken, It.IsAny<string[]>()))
                                      .ReturnsAsync(true);
                
                _mockPowerShellService.Setup(s => s.GetTeamChannelsAsync(teamId))
                                      .ReturnsAsync(new Collection<PSObject>());
            }

            // Act
            var result = await _controller.GetTeamChannels(teamId);

            // Assert
            if (shouldExtractToken)
            {
                result.Should().BeOfType<OkObjectResult>();
                _mockPowerShellService.Verify(s => s.ConnectWithAccessTokenAsync(It.IsAny<string>(), It.IsAny<string[]>()), Times.Once);
            }
            else
            {
                var serviceUnavailableResult = result.Should().BeOfType<ObjectResult>().Subject;
                serviceUnavailableResult.StatusCode.Should().Be(503);
                _mockPowerShellService.Verify(s => s.ConnectWithAccessTokenAsync(It.IsAny<string>(), It.IsAny<string[]>()), Times.Never);
            }
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullPowerShellService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(null!, _mockTeamService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullTeamService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(_mockPowerShellService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ChannelsController(_mockPowerShellService.Object, _mockTeamService.Object, null!));
        }

        #endregion
    }

    // DTOs for testing - these should match the actual DTOs from the controller
    public class CreateChannelRequestDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPrivate { get; set; } = false;
    }

    public class UpdateChannelRequestDto
    {
        public string? NewDisplayName { get; set; }
        public string? NewDescription { get; set; }
    }
} 