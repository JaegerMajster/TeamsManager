using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
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
    /// Testy jednostkowe dla OperationHistoriesController
    /// Pokrycie: 4 endpointy GET dla historii operacji
    /// </summary>
    public class OperationHistoriesControllerTests
    {
        private readonly Mock<IOperationHistoryService> _mockOperationHistoryService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<OperationHistoriesController>> _mockLogger;
        private readonly OperationHistoriesController _controller;

        public OperationHistoriesControllerTests()
        {
            _mockOperationHistoryService = new Mock<IOperationHistoryService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<OperationHistoriesController>>();

            _controller = new OperationHistoriesController(
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange & Act
            var controller = new OperationHistoriesController(
                _mockOperationHistoryService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);

            // Assert
            controller.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullOperationHistoryService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OperationHistoriesController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
            
            exception.ParamName.Should().Be("operationHistoryService");
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OperationHistoriesController(_mockOperationHistoryService.Object, null!, _mockLogger.Object));
            
            exception.ParamName.Should().Be("currentUserService");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new OperationHistoriesController(_mockOperationHistoryService.Object, _mockCurrentUserService.Object, null!));
            
            exception.ParamName.Should().Be("logger");
        }

        #endregion

        #region GetOperationById Tests

        [Fact]
        public async Task GetOperationById_WithValidId_ShouldReturnOkWithOperation()
        {
            // Arrange
            var operationId = "op-123";
            var operation = CreateTestOperation(operationId, OperationType.TeamCreated, OperationStatus.Completed);

            _mockOperationHistoryService.Setup(x => x.GetOperationByIdAsync(operationId))
                .ReturnsAsync(operation);

            // Act
            var result = await _controller.GetOperationById(operationId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(operation);
            
            _mockOperationHistoryService.Verify(x => x.GetOperationByIdAsync(operationId), Times.Once);
        }

        [Fact]
        public async Task GetOperationById_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var operationId = "nonexistent-op";

            _mockOperationHistoryService.Setup(x => x.GetOperationByIdAsync(operationId))
                .ReturnsAsync((OperationHistory?)null);

            // Act
            var result = await _controller.GetOperationById(operationId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            var message = notFoundResult.Value.Should().BeAssignableTo<object>().Subject;
            message.ToString().Should().Contain($"Wpis historii operacji o ID '{operationId}' nie został znaleziony.");
            
            _mockOperationHistoryService.Verify(x => x.GetOperationByIdAsync(operationId), Times.Once);
        }

        #endregion

        #region GetHistoryForEntity Tests

        [Fact]
        public async Task GetHistoryForEntity_WithValidParameters_ShouldReturnOkWithHistory()
        {
            // Arrange
            var entityType = "Team";
            var entityId = "team-123";
            var count = 10;
            var history = new List<OperationHistory>
            {
                CreateTestOperation("op-1", OperationType.TeamCreated, OperationStatus.Completed),
                CreateTestOperation("op-2", OperationType.TeamUpdated, OperationStatus.Completed)
            };

            _mockOperationHistoryService.Setup(x => x.GetHistoryForEntityAsync(entityType, entityId, count))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryForEntity(entityType, entityId, count);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(history);
            
            _mockOperationHistoryService.Verify(x => x.GetHistoryForEntityAsync(entityType, entityId, count), Times.Once);
        }

        [Fact]
        public async Task GetHistoryForEntity_WithUrlEncodedParameters_ShouldDecodeAndProcess()
        {
            // Arrange
            var encodedEntityType = System.Net.WebUtility.UrlEncode("Team Type");
            var encodedEntityId = System.Net.WebUtility.UrlEncode("team-123");
            var decodedEntityType = "Team Type";
            var decodedEntityId = "team-123";
            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryForEntityAsync(decodedEntityType, decodedEntityId, null))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryForEntity(encodedEntityType, encodedEntityId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryForEntityAsync(decodedEntityType, decodedEntityId, null), Times.Once);
        }

        [Fact]
        public async Task GetHistoryForEntity_WithoutCount_ShouldPassNullToService()
        {
            // Arrange
            var entityType = "User";
            var entityId = "user-456";
            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryForEntityAsync(entityType, entityId, null))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryForEntity(entityType, entityId);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryForEntityAsync(entityType, entityId, null), Times.Once);
        }

        #endregion

        #region GetHistoryByUser Tests

        [Fact]
        public async Task GetHistoryByUser_WithValidUpn_ShouldReturnOkWithHistory()
        {
            // Arrange
            var userUpn = "john.doe@test.com";
            var count = 15;
            var history = new List<OperationHistory>
            {
                CreateTestOperation("op-1", OperationType.UserCreated, OperationStatus.Completed),
                CreateTestOperation("op-2", OperationType.UserUpdated, OperationStatus.Failed)
            };

            _mockOperationHistoryService.Setup(x => x.GetHistoryByUserAsync(userUpn, count))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByUser(userUpn, count);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(history);
            
            _mockOperationHistoryService.Verify(x => x.GetHistoryByUserAsync(userUpn, count), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByUser_WithUrlEncodedUpn_ShouldDecodeAndProcess()
        {
            // Arrange
            var encodedUpn = System.Net.WebUtility.UrlEncode("user@domain.com");
            var decodedUpn = "user@domain.com";
            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByUserAsync(decodedUpn, null))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByUser(encodedUpn);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByUserAsync(decodedUpn, null), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByUser_WithoutCount_ShouldPassNullToService()
        {
            // Arrange
            var userUpn = "jane.smith@test.com";
            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByUserAsync(userUpn, null))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByUser(userUpn);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByUserAsync(userUpn, null), Times.Once);
        }

        #endregion

        #region GetHistoryByFilter Tests

        [Fact]
        public async Task GetHistoryByFilter_WithValidFilter_ShouldReturnOkWithHistory()
        {
            // Arrange
            var filter = new OperationHistoryFilterDto
            {
                StartDate = DateTime.Today.AddDays(-7),
                EndDate = DateTime.Today,
                OperationType = OperationType.TeamCreated,
                OperationStatus = OperationStatus.Completed,
                CreatedBy = "admin@test.com",
                Page = 1,
                PageSize = 20
            };

            var history = new List<OperationHistory>
            {
                CreateTestOperation("op-1", OperationType.TeamCreated, OperationStatus.Completed)
            };

            _mockOperationHistoryService.Setup(x => x.GetHistoryByFilterAsync(
                filter.StartDate, filter.EndDate, filter.OperationType, filter.OperationStatus,
                filter.CreatedBy, filter.Page, filter.PageSize))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByFilter(filter);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(history);
            
            _mockOperationHistoryService.Verify(x => x.GetHistoryByFilterAsync(
                filter.StartDate, filter.EndDate, filter.OperationType, filter.OperationStatus,
                filter.CreatedBy, filter.Page, filter.PageSize), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByFilter_WithInvalidPage_ShouldCorrectToPage1()
        {
            // Arrange
            var filter = new OperationHistoryFilterDto
            {
                Page = 0, // Invalid page
                PageSize = 20
            };

            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 20)) // Should be corrected to page 1
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByFilter(filter);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 20), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByFilter_WithInvalidPageSize_ShouldCorrectToDefault()
        {
            // Arrange
            var filter = new OperationHistoryFilterDto
            {
                Page = 1,
                PageSize = 0 // Invalid page size
            };

            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 20)) // Should be corrected to default 20
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByFilter(filter);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 20), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByFilter_WithTooLargePageSize_ShouldLimitTo100()
        {
            // Arrange
            var filter = new OperationHistoryFilterDto
            {
                Page = 1,
                PageSize = 150 // Too large
            };

            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 100)) // Should be limited to 100
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByFilter(filter);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByFilterAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<OperationType?>(), It.IsAny<OperationStatus?>(),
                It.IsAny<string?>(), 1, 100), Times.Once);
        }

        [Fact]
        public async Task GetHistoryByFilter_WithEmptyFilter_ShouldUseDefaults()
        {
            // Arrange
            var filter = new OperationHistoryFilterDto(); // Empty filter with defaults
            var history = new List<OperationHistory>();

            _mockOperationHistoryService.Setup(x => x.GetHistoryByFilterAsync(
                null, null, null, null, null, 1, 20))
                .ReturnsAsync(history);

            // Act
            var result = await _controller.GetHistoryByFilter(filter);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockOperationHistoryService.Verify(x => x.GetHistoryByFilterAsync(
                null, null, null, null, null, 1, 20), Times.Once);
        }

        #endregion

        #region Helper Methods

        private static OperationHistory CreateTestOperation(string id, OperationType operationType, OperationStatus status)
        {
            return new OperationHistory
            {
                Id = id,
                Type = operationType,
                Status = status,
                TargetEntityType = "TestEntity",
                TargetEntityId = "test-entity-123",
                TargetEntityName = "Test Entity Name",
                CreatedBy = "test@test.com",
                CreatedDate = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = status == OperationStatus.Completed ? DateTime.UtcNow : null,
                Duration = status == OperationStatus.Completed ? TimeSpan.FromMinutes(5) : null,
                ErrorMessage = status == OperationStatus.Failed ? "Test error message" : null,
                ErrorStackTrace = status == OperationStatus.Failed ? "Test error stack trace" : null
            };
        }

        #endregion
    }
} 