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
    public class DepartmentsControllerTests
    {
        private readonly Mock<IDepartmentService> _mockDepartmentService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<DepartmentsController>> _mockLogger;
        private readonly DepartmentsController _controller;

        public DepartmentsControllerTests()
        {
            _mockDepartmentService = new Mock<IDepartmentService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<DepartmentsController>>();

            _controller = new DepartmentsController(_mockDepartmentService.Object, _mockCurrentUserService.Object, _mockLogger.Object);
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

        #region GetAllDepartments Tests

        [Fact]
        public async Task GetAllDepartments_ShouldReturnAllDepartments()
        {
            // Arrange
            var departments = new List<Department>
            {
                new Department { Id = "1", Name = "IT Department", IsActive = true },
                new Department { Id = "2", Name = "HR Department", IsActive = true }
            };
            
            _mockDepartmentService.Setup(s => s.GetAllDepartmentsAsync(false, false))
                                 .ReturnsAsync(departments);

            // Act
            var result = await _controller.GetAllDepartments();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(departments);
        }

        [Fact]
        public async Task GetAllDepartments_WithServiceException_ShouldReturnInternalServerError()
        {
            // Arrange
            _mockDepartmentService.Setup(s => s.GetAllDepartmentsAsync(It.IsAny<bool>(), It.IsAny<bool>()))
                                 .ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _controller.GetAllDepartments());
        }

        #endregion

        #region GetDepartmentById Tests

        [Fact]
        public async Task GetDepartmentById_WithValidId_ShouldReturnDepartment()
        {
            // Arrange
            var departmentId = "dept-123";
            var department = new Department { Id = departmentId, Name = "Test Department", IsActive = true };
            
            _mockDepartmentService.Setup(s => s.GetDepartmentByIdAsync(departmentId, false, false, false))
                                 .ReturnsAsync(department);

            // Act
            var result = await _controller.GetDepartmentById(departmentId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(department);
        }

        [Fact]
        public async Task GetDepartmentById_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var departmentId = "nonexistent";
            
            _mockDepartmentService.Setup(s => s.GetDepartmentByIdAsync(departmentId, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                                 .ReturnsAsync((Department?)null);

            // Act
            var result = await _controller.GetDepartmentById(departmentId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
        }

        #endregion

        #region CreateDepartment Tests

        [Fact]
        public async Task CreateDepartment_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var createDto = new CreateDepartmentRequestDto
            {
                Name = "New Department",
                Description = "New department description",
                DepartmentCode = "ND"
            };
            
            var createdDepartment = new Department 
            { 
                Id = "new-dept-123", 
                Name = createDto.Name,
                Description = createDto.Description,
                DepartmentCode = createDto.DepartmentCode,
                IsActive = true 
            };
            
            _mockDepartmentService.Setup(s => s.CreateDepartmentAsync(
                createDto.Name, createDto.Description, createDto.ParentDepartmentId, createDto.DepartmentCode))
                                 .ReturnsAsync(createdDepartment);

            // Act
            var result = await _controller.CreateDepartment(createDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(DepartmentsController.GetDepartmentById));
            createdResult.RouteValues.Should().ContainKey("departmentId").WhoseValue.Should().Be(createdDepartment.Id);
            createdResult.Value.Should().Be(createdDepartment);
        }

        [Fact]
        public async Task CreateDepartment_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var createDto = new CreateDepartmentRequestDto { Name = "Test Department" };
            
            _mockDepartmentService.Setup(s => s.CreateDepartmentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                 .ReturnsAsync((Department?)null);

            // Act
            var result = await _controller.CreateDepartment(createDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się utworzyć działu. Sprawdź logi serwera." });
        }

        #endregion

        #region UpdateDepartment Tests

        [Fact]
        public async Task UpdateDepartment_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var departmentId = "dept-123";
            var updateDto = new UpdateDepartmentRequestDto
            {
                Name = "Updated Department",
                Description = "Updated description",
                DepartmentCode = "UD"
            };
            
            var existingDepartment = new Department
            {
                Id = departmentId,
                Name = "Old Name",
                Description = "Old description",
                IsActive = true
            };
            
            _mockDepartmentService.Setup(s => s.GetDepartmentByIdAsync(departmentId, false, false, false))
                                 .ReturnsAsync(existingDepartment);
            
            _mockDepartmentService.Setup(s => s.UpdateDepartmentAsync(It.Is<Department>(d => 
                d.Id == departmentId && d.Name == updateDto.Name)))
                                 .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateDepartment(departmentId, updateDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task UpdateDepartment_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var departmentId = "dept-123";
            var updateDto = new UpdateDepartmentRequestDto { Name = "Updated Department" };
            
            var existingDepartment = new Department { Id = departmentId, Name = "Old Name", IsActive = true };
            _mockDepartmentService.Setup(s => s.GetDepartmentByIdAsync(departmentId, false, false, false))
                                 .ReturnsAsync(existingDepartment);
            
            _mockDepartmentService.Setup(s => s.UpdateDepartmentAsync(It.IsAny<Department>()))
                                 .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateDepartment(departmentId, updateDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zaktualizować działu." });
        }

        #endregion

        #region DeleteDepartment Tests

        [Fact]
        public async Task DeleteDepartment_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var departmentId = "dept-123";
            
            _mockDepartmentService.Setup(s => s.DeleteDepartmentAsync(departmentId))
                                 .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteDepartment(departmentId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { Message = "Dział usunięty (zdezaktywowany) pomyślnie." });
        }

        [Fact]
        public async Task DeleteDepartment_WithServiceFailure_ShouldReturnNotFound()
        {
            // Arrange
            var departmentId = "dept-123";
            
            _mockDepartmentService.Setup(s => s.DeleteDepartmentAsync(departmentId))
                                 .ReturnsAsync(false);
            
            _mockDepartmentService.Setup(s => s.GetDepartmentByIdAsync(departmentId, false, false, false))
                                 .ReturnsAsync((Department?)null);

            // Act
            var result = await _controller.DeleteDepartment(departmentId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().BeEquivalentTo(new { Message = $"Dział o ID '{departmentId}' nie został znaleziony." });
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullDepartmentService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DepartmentsController(null!, _mockCurrentUserService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DepartmentsController(_mockDepartmentService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DepartmentsController(_mockDepartmentService.Object, _mockCurrentUserService.Object, null!));
        }

        #endregion
    }
} 