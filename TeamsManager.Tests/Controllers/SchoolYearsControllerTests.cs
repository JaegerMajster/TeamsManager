using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Api.Controllers;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla SchoolYearsController
    /// Testuje wszystkie endpointy API dla zarządzania latami szkolnymi
    /// </summary>
    public class SchoolYearsControllerTests
    {
        private readonly Mock<ISchoolYearService> _mockSchoolYearService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolYearsController>> _mockLogger;
        private readonly SchoolYearsController _controller;

        public SchoolYearsControllerTests()
        {
            _mockSchoolYearService = new Mock<ISchoolYearService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolYearsController>>();

            _controller = new SchoolYearsController(
                _mockSchoolYearService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetSchoolYearById Tests

        [Fact]
        public async Task GetSchoolYearById_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var schoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");

            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync(schoolYear);

            // Act
            var result = await _controller.GetSchoolYearById(schoolYearId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(schoolYear);
        }

        [Fact]
        public async Task GetSchoolYearById_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var schoolYearId = "non-existent-id";

            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _controller.GetSchoolYearById(schoolYearId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GetAllActiveSchoolYears Tests

        [Fact]
        public async Task GetAllActiveSchoolYears_ShouldReturnOkWithSchoolYears()
        {
            // Arrange
            var schoolYears = new List<SchoolYear>
            {
                CreateTestSchoolYear("sy-1", "2023/2024"),
                CreateTestSchoolYear("sy-2", "2024/2025"),
                CreateTestSchoolYear("sy-3", "2025/2026")
            };

            _mockSchoolYearService.Setup(x => x.GetAllActiveSchoolYearsAsync(false))
                .ReturnsAsync(schoolYears);

            // Act
            var result = await _controller.GetAllActiveSchoolYears();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSchoolYears = okResult.Value.Should().BeAssignableTo<IEnumerable<SchoolYear>>().Subject;
            returnedSchoolYears.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetAllActiveSchoolYears_WithEmptyResult_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockSchoolYearService.Setup(x => x.GetAllActiveSchoolYearsAsync(false))
                .ReturnsAsync(new List<SchoolYear>());

            // Act
            var result = await _controller.GetAllActiveSchoolYears();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSchoolYears = okResult.Value.Should().BeAssignableTo<IEnumerable<SchoolYear>>().Subject;
            returnedSchoolYears.Should().BeEmpty();
        }

        #endregion

        #region GetCurrentSchoolYear Tests

        [Fact]
        public async Task GetCurrentSchoolYear_WithCurrentYear_ShouldReturnOk()
        {
            // Arrange
            var currentSchoolYear = CreateTestSchoolYear("sy-current", "2024/2025", isCurrent: true);

            _mockSchoolYearService.Setup(x => x.GetCurrentSchoolYearAsync(false))
                .ReturnsAsync(currentSchoolYear);

            // Act
            var result = await _controller.GetCurrentSchoolYear();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(currentSchoolYear);
        }

        [Fact]
        public async Task GetCurrentSchoolYear_WithNoCurrentYear_ShouldReturnOkWithNull()
        {
            // Arrange
            _mockSchoolYearService.Setup(x => x.GetCurrentSchoolYearAsync(false))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _controller.GetCurrentSchoolYear();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeNull();
        }

        #endregion

        #region CreateSchoolYear Tests

        [Fact]
        public async Task CreateSchoolYear_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var requestDto = new CreateSchoolYearRequestDto
            {
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                Description = "Test school year",
                FirstSemesterStart = new DateTime(2024, 9, 1),
                FirstSemesterEnd = new DateTime(2025, 1, 31),
                SecondSemesterStart = new DateTime(2025, 2, 1),
                SecondSemesterEnd = new DateTime(2025, 6, 30)
            };

            var createdSchoolYear = CreateTestSchoolYear("sy-new", requestDto.Name);

            _mockSchoolYearService.Setup(x => x.CreateSchoolYearAsync(
                requestDto.Name,
                requestDto.StartDate,
                requestDto.EndDate,
                requestDto.Description,
                requestDto.FirstSemesterStart,
                requestDto.FirstSemesterEnd,
                requestDto.SecondSemesterStart,
                requestDto.SecondSemesterEnd))
                .ReturnsAsync(createdSchoolYear);

            // Act
            var result = await _controller.CreateSchoolYear(requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(SchoolYearsController.GetSchoolYearById));
            createdResult.RouteValues!["schoolYearId"].Should().Be(createdSchoolYear.Id);
            createdResult.Value.Should().Be(createdSchoolYear);
        }

        [Fact]
        public async Task CreateSchoolYear_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new CreateSchoolYearRequestDto
            {
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30)
            };

            _mockSchoolYearService.Setup(x => x.CreateSchoolYearAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _controller.CreateSchoolYear(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region UpdateSchoolYear Tests

        [Fact]
        public async Task UpdateSchoolYear_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var existingSchoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");
            var requestDto = new UpdateSchoolYearRequestDto
            {
                Name = "2024/2025 Updated",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                Description = "Updated description",
                IsActive = true
            };

            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync(existingSchoolYear);

            _mockSchoolYearService.Setup(x => x.UpdateSchoolYearAsync(It.IsAny<SchoolYear>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSchoolYear(schoolYearId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            // Verify that the school year was updated with correct values
            _mockSchoolYearService.Verify(x => x.UpdateSchoolYearAsync(
                It.Is<SchoolYear>(sy => 
                    sy.Id == schoolYearId &&
                    sy.Name == requestDto.Name &&
                    sy.Description == requestDto.Description &&
                    sy.IsActive == requestDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UpdateSchoolYear_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var schoolYearId = "non-existent-id";
            var requestDto = new UpdateSchoolYearRequestDto
            {
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30)
            };

            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _controller.UpdateSchoolYear(schoolYearId, requestDto);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateSchoolYear_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var schoolYearId = "sy-123";
            var existingSchoolYear = CreateTestSchoolYear(schoolYearId, "2024/2025");
            var requestDto = new UpdateSchoolYearRequestDto
            {
                Name = "2024/2025",
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30)
            };

            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync(existingSchoolYear);

            _mockSchoolYearService.Setup(x => x.UpdateSchoolYearAsync(It.IsAny<SchoolYear>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSchoolYear(schoolYearId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region SetCurrentSchoolYear Tests

        [Fact]
        public async Task SetCurrentSchoolYear_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var schoolYearId = "sy-123";

            _mockSchoolYearService.Setup(x => x.SetCurrentSchoolYearAsync(schoolYearId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.SetCurrentSchoolYear(schoolYearId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task SetCurrentSchoolYear_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var schoolYearId = "sy-123";

            _mockSchoolYearService.Setup(x => x.SetCurrentSchoolYearAsync(schoolYearId))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.SetCurrentSchoolYear(schoolYearId);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region DeleteSchoolYear Tests

        [Fact]
        public async Task DeleteSchoolYear_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var schoolYearId = "sy-123";

            _mockSchoolYearService.Setup(x => x.DeleteSchoolYearAsync(schoolYearId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSchoolYear(schoolYearId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteSchoolYear_WithServiceFailure_ShouldReturnNotFound()
        {
            // Arrange
            var schoolYearId = "sy-123";

            _mockSchoolYearService.Setup(x => x.DeleteSchoolYearAsync(schoolYearId))
                .ReturnsAsync(false);
            
            // Mock that school year doesn't exist
            _mockSchoolYearService.Setup(x => x.GetSchoolYearByIdAsync(schoolYearId, false))
                .ReturnsAsync((SchoolYear?)null);

            // Act
            var result = await _controller.DeleteSchoolYear(schoolYearId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteSchoolYear_WithException_ShouldReturnConflict()
        {
            // Arrange
            var schoolYearId = "sy-123";

            _mockSchoolYearService.Setup(x => x.DeleteSchoolYearAsync(schoolYearId))
                .ThrowsAsync(new InvalidOperationException("Cannot delete school year with active teams"));

            // Act
            var result = await _controller.DeleteSchoolYear(schoolYearId);

            // Assert
            var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
            conflictResult.Value.Should().NotBeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSchoolYearService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolYearsController(
                null!,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolYearsController(
                _mockSchoolYearService.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolYearsController(
                _mockSchoolYearService.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region Helper Methods

        private SchoolYear CreateTestSchoolYear(string id, string name, bool isCurrent = false)
        {
            return new SchoolYear
            {
                Id = id,
                Name = name,
                StartDate = new DateTime(2024, 9, 1),
                EndDate = new DateTime(2025, 6, 30),
                IsCurrent = isCurrent,
                Description = $"Test school year {name}",
                FirstSemesterStart = new DateTime(2024, 9, 1),
                FirstSemesterEnd = new DateTime(2025, 1, 31),
                SecondSemesterStart = new DateTime(2025, 2, 1),
                SecondSemesterEnd = new DateTime(2025, 6, 30),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 