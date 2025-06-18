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
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Controllers
{
    /// <summary>
    /// Testy jednostkowe dla SubjectsController
    /// Testuje wszystkie endpointy API dla zarządzania przedmiotami
    /// </summary>
    public class SubjectsControllerTests
    {
        private readonly Mock<ISubjectService> _mockSubjectService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<SubjectsController>> _mockLogger;
        private readonly SubjectsController _controller;

        public SubjectsControllerTests()
        {
            _mockSubjectService = new Mock<ISubjectService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<SubjectsController>>();

            _controller = new SubjectsController(
                _mockSubjectService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetSubjectById Tests

        [Fact]
        public async Task GetSubjectById_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var subjectId = "subj-123";
            var subject = CreateTestSubject(subjectId, "Matematyka", "MAT");

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync(subject);

            // Act
            var result = await _controller.GetSubjectById(subjectId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(subject);
        }

        [Fact]
        public async Task GetSubjectById_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var subjectId = "non-existent-id";

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.GetSubjectById(subjectId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GetAllActiveSubjects Tests

        [Fact]
        public async Task GetAllActiveSubjects_ShouldReturnOkWithSubjects()
        {
            // Arrange
            var subjects = new List<Subject>
            {
                CreateTestSubject("subj-1", "Matematyka", "MAT"),
                CreateTestSubject("subj-2", "Fizyka", "FIZ"),
                CreateTestSubject("subj-3", "Chemia", "CHE")
            };

            _mockSubjectService.Setup(x => x.GetAllActiveSubjectsAsync(false))
                .ReturnsAsync(subjects);

            // Act
            var result = await _controller.GetAllActiveSubjects();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSubjects = okResult.Value.Should().BeAssignableTo<IEnumerable<Subject>>().Subject;
            returnedSubjects.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetAllActiveSubjects_WithEmptyResult_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockSubjectService.Setup(x => x.GetAllActiveSubjectsAsync(false))
                .ReturnsAsync(new List<Subject>());

            // Act
            var result = await _controller.GetAllActiveSubjects();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedSubjects = okResult.Value.Should().BeAssignableTo<IEnumerable<Subject>>().Subject;
            returnedSubjects.Should().BeEmpty();
        }

        #endregion

        #region CreateSubject Tests

        [Fact]
        public async Task CreateSubject_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var requestDto = new CreateSubjectRequestDto
            {
                Name = "Matematyka",
                Code = "MAT",
                Description = "Podstawy matematyki",
                Hours = 120,
                DefaultSchoolTypeId = "st-1",
                Category = "Nauki ścisłe"
            };

            var createdSubject = CreateTestSubject("subj-new", requestDto.Name, requestDto.Code);

            _mockSubjectService.Setup(x => x.CreateSubjectAsync(
                requestDto.Name,
                requestDto.Code,
                requestDto.Description,
                requestDto.Hours,
                requestDto.DefaultSchoolTypeId,
                requestDto.Category))
                .ReturnsAsync(createdSubject);

            // Act
            var result = await _controller.CreateSubject(requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(SubjectsController.GetSubjectById));
            createdResult.RouteValues!["subjectId"].Should().Be(createdSubject.Id);
            createdResult.Value.Should().Be(createdSubject);
        }

        [Fact]
        public async Task CreateSubject_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new CreateSubjectRequestDto
            {
                Name = "Matematyka",
                Code = "MAT"
            };

            _mockSubjectService.Setup(x => x.CreateSubjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.CreateSubject(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region UpdateSubject Tests

        [Fact]
        public async Task UpdateSubject_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var subjectId = "subj-123";
            var existingSubject = CreateTestSubject(subjectId, "Matematyka", "MAT");
            var requestDto = new UpdateSubjectRequestDto
            {
                Name = "Matematyka Zaawansowana",
                Code = "MAT_ADV",
                Description = "Zaawansowane zagadnienia matematyczne",
                Hours = 150,
                Category = "Nauki ścisłe",
                IsActive = true
            };

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync(existingSubject);

            _mockSubjectService.Setup(x => x.UpdateSubjectAsync(It.IsAny<Subject>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSubject(subjectId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            // Verify that the subject was updated with correct values
            _mockSubjectService.Verify(x => x.UpdateSubjectAsync(
                It.Is<Subject>(s => 
                    s.Id == subjectId &&
                    s.Name == requestDto.Name &&
                    s.Code == requestDto.Code &&
                    s.Description == requestDto.Description &&
                    s.Hours == requestDto.Hours &&
                    s.Category == requestDto.Category &&
                    s.IsActive == requestDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UpdateSubject_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var subjectId = "non-existent-id";
            var requestDto = new UpdateSubjectRequestDto
            {
                Name = "Matematyka",
                Code = "MAT"
            };

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.UpdateSubject(subjectId, requestDto);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateSubject_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var subjectId = "subj-123";
            var existingSubject = CreateTestSubject(subjectId, "Matematyka", "MAT");
            var requestDto = new UpdateSubjectRequestDto
            {
                Name = "Matematyka",
                Code = "MAT"
            };

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync(existingSubject);

            _mockSubjectService.Setup(x => x.UpdateSubjectAsync(It.IsAny<Subject>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSubject(subjectId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region DeleteSubject Tests

        [Fact]
        public async Task DeleteSubject_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var subjectId = "subj-123";

            _mockSubjectService.Setup(x => x.DeleteSubjectAsync(subjectId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSubject(subjectId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteSubject_WithServiceFailure_AndSubjectExists_ShouldReturnBadRequest()
        {
            // Arrange
            var subjectId = "subj-123";
            var existingSubject = CreateTestSubject(subjectId, "Matematyka", "MAT");

            _mockSubjectService.Setup(x => x.DeleteSubjectAsync(subjectId))
                .ReturnsAsync(false);

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync(existingSubject);

            // Act
            var result = await _controller.DeleteSubject(subjectId);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteSubject_WithServiceFailure_AndSubjectNotExists_ShouldReturnNotFound()
        {
            // Arrange
            var subjectId = "non-existent-id";

            _mockSubjectService.Setup(x => x.DeleteSubjectAsync(subjectId))
                .ReturnsAsync(false);

            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(subjectId, false))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.DeleteSubject(subjectId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullSubjectService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SubjectsController(
                null!,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SubjectsController(
                _mockSubjectService.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SubjectsController(
                _mockSubjectService.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region Edge Cases Tests

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task GetSubjectById_WithInvalidIdFormats_ShouldReturnNotFound(string invalidId)
        {
            // Arrange
            _mockSubjectService.Setup(x => x.GetSubjectByIdAsync(invalidId, false))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.GetSubjectById(invalidId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task CreateSubject_WithEmptyName_ShouldStillCallService()
        {
            // Arrange
            var requestDto = new CreateSubjectRequestDto
            {
                Name = "",
                Code = "TEST"
            };

            _mockSubjectService.Setup(x => x.CreateSubjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync((Subject?)null);

            // Act
            var result = await _controller.CreateSubject(requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _mockSubjectService.Verify(x => x.CreateSubjectAsync(
                "",
                "TEST",
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region Helper Methods

        private Subject CreateTestSubject(string id, string name, string? code = null)
        {
            return new Subject
            {
                Id = id,
                Name = name,
                Code = code,
                Description = $"Test subject {name}",
                Hours = 120,
                Category = "Test Category",
                DefaultSchoolTypeId = "st-test",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 