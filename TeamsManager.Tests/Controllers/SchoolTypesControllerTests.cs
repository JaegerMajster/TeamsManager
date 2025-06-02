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
    public class SchoolTypesControllerTests
    {
        private readonly Mock<ISchoolTypeService> _mockSchoolTypeService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SchoolTypesController>> _mockLogger;
        private readonly SchoolTypesController _controller;

        public SchoolTypesControllerTests()
        {
            _mockSchoolTypeService = new Mock<ISchoolTypeService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SchoolTypesController>>();

            _controller = new SchoolTypesController(_mockSchoolTypeService.Object, _mockCurrentUserService.Object, _mockLogger.Object);
        }

        #region GetAllActiveSchoolTypes Tests

        [Fact]
        public async Task GetAllActiveSchoolTypes_ShouldReturnAllSchoolTypes()
        {
            // Arrange
            var schoolTypes = new List<SchoolType>
            {
                new SchoolType { Id = "1", ShortName = "PS", FullName = "Primary School", IsActive = true },
                new SchoolType { Id = "2", ShortName = "HS", FullName = "High School", IsActive = true }
            };
            
            _mockSchoolTypeService.Setup(s => s.GetAllActiveSchoolTypesAsync(false))
                                 .ReturnsAsync(schoolTypes);

            // Act
            var result = await _controller.GetAllActiveSchoolTypes();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(schoolTypes);
        }

        [Fact]
        public async Task GetAllActiveSchoolTypes_WithServiceException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockSchoolTypeService.Setup(s => s.GetAllActiveSchoolTypesAsync(It.IsAny<bool>()))
                                 .ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _controller.GetAllActiveSchoolTypes());
        }

        #endregion

        #region GetSchoolTypeById Tests

        [Fact]
        public async Task GetSchoolTypeById_WithValidId_ShouldReturnSchoolType()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var schoolType = new SchoolType { Id = schoolTypeId, ShortName = "TST", FullName = "Test School Type", IsActive = true };
            
            _mockSchoolTypeService.Setup(s => s.GetSchoolTypeByIdAsync(schoolTypeId, false))
                                 .ReturnsAsync(schoolType);

            // Act
            var result = await _controller.GetSchoolTypeById(schoolTypeId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(schoolType);
        }

        [Fact]
        public async Task GetSchoolTypeById_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var schoolTypeId = "nonexistent";
            
            _mockSchoolTypeService.Setup(s => s.GetSchoolTypeByIdAsync(schoolTypeId, It.IsAny<bool>()))
                                 .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _controller.GetSchoolTypeById(schoolTypeId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony." });
        }

        #endregion

        #region CreateSchoolType Tests

        [Fact]
        public async Task CreateSchoolType_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var createDto = new CreateSchoolTypeRequestDto
            {
                ShortName = "NST",
                FullName = "New School Type",
                Description = "New school type description"
            };
            
            var createdSchoolType = new SchoolType 
            { 
                Id = "new-st-123", 
                ShortName = createDto.ShortName,
                FullName = createDto.FullName,
                Description = createDto.Description,
                IsActive = true 
            };
            
            _mockSchoolTypeService.Setup(s => s.CreateSchoolTypeAsync(
                createDto.ShortName, createDto.FullName, createDto.Description, createDto.ColorCode, createDto.SortOrder))
                                 .ReturnsAsync(createdSchoolType);

            // Act
            var result = await _controller.CreateSchoolType(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(SchoolTypesController.GetSchoolTypeById));
            createdResult.RouteValues.Should().ContainKey("schoolTypeId").WhoseValue.Should().Be(createdSchoolType.Id);
            createdResult.Value.Should().Be(createdSchoolType);
        }

        [Fact]
        public async Task CreateSchoolType_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateSchoolTypeRequestDto { ShortName = "TST", FullName = "Test School Type" };
            
            _mockSchoolTypeService.Setup(s => s.CreateSchoolTypeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                                 .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _controller.CreateSchoolType(createDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się utworzyć typu szkoły. Sprawdź logi serwera." });
        }

        #endregion

        #region UpdateSchoolType Tests

        [Fact]
        public async Task UpdateSchoolType_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var updateDto = new UpdateSchoolTypeRequestDto
            {
                ShortName = "UST",
                FullName = "Updated School Type",
                Description = "Updated description"
            };
            
            var existingSchoolType = new SchoolType
            {
                Id = schoolTypeId,
                ShortName = "OLD",
                FullName = "Old School Type",
                Description = "Old description",
                IsActive = true
            };
            
            _mockSchoolTypeService.Setup(s => s.GetSchoolTypeByIdAsync(schoolTypeId, false))
                                 .ReturnsAsync(existingSchoolType);
            
            _mockSchoolTypeService.Setup(s => s.UpdateSchoolTypeAsync(It.Is<SchoolType>(st => 
                st.Id == schoolTypeId && st.ShortName == updateDto.ShortName)))
                                 .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSchoolType(schoolTypeId, updateDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateSchoolType_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var updateDto = new UpdateSchoolTypeRequestDto { ShortName = "UST", FullName = "Updated School Type" };
            
            var existingSchoolType = new SchoolType { Id = schoolTypeId, ShortName = "OLD", FullName = "Old School Type", IsActive = true };
            _mockSchoolTypeService.Setup(s => s.GetSchoolTypeByIdAsync(schoolTypeId, false))
                                 .ReturnsAsync(existingSchoolType);
            
            _mockSchoolTypeService.Setup(s => s.UpdateSchoolTypeAsync(It.IsAny<SchoolType>()))
                                 .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSchoolType(schoolTypeId, updateDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zaktualizować typu szkoły." });
        }

        #endregion

        #region DeleteSchoolType Tests

        [Fact]
        public async Task DeleteSchoolType_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var schoolTypeId = "st-123";
            
            _mockSchoolTypeService.Setup(s => s.DeleteSchoolTypeAsync(schoolTypeId))
                                 .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSchoolType(schoolTypeId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Typ szkoły usunięty (zdezaktywowany) pomyślnie." });
        }

        [Fact]
        public async Task DeleteSchoolType_WithServiceFailure_ShouldReturnNotFound()
        {
            // Arrange
            var schoolTypeId = "st-123";
            
            _mockSchoolTypeService.Setup(s => s.DeleteSchoolTypeAsync(schoolTypeId))
                                 .ReturnsAsync(false);
            
            _mockSchoolTypeService.Setup(s => s.GetSchoolTypeByIdAsync(schoolTypeId, false))
                                 .ReturnsAsync((SchoolType?)null);

            // Act
            var result = await _controller.DeleteSchoolType(schoolTypeId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Typ szkoły o ID '{schoolTypeId}' nie został znaleziony." });
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSchoolTypeService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolTypesController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolTypesController(_mockSchoolTypeService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SchoolTypesController(_mockSchoolTypeService.Object, _mockCurrentUserService.Object, null!));
        }

        #endregion
    }
} 