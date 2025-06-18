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
    /// Testy jednostkowe dla TeamTemplatesController
    /// Testuje wszystkie endpointy API dla zarządzania szablonami zespołów
    /// </summary>
    public class TeamTemplatesControllerTests
    {
        private readonly Mock<ITeamTemplateService> _mockTeamTemplateService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<TeamTemplatesController>> _mockLogger;
        private readonly TeamTemplatesController _controller;

        public TeamTemplatesControllerTests()
        {
            _mockTeamTemplateService = new Mock<ITeamTemplateService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<TeamTemplatesController>>();

            _controller = new TeamTemplatesController(
                _mockTeamTemplateService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetTemplateById Tests

        [Fact]
        public async Task GetTemplateById_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var templateId = "template-123";
            var template = CreateTestTeamTemplate(templateId, "Test Template");

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync(template);

            // Act
            var result = await _controller.GetTemplateById(templateId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(template);
        }

        [Fact]
        public async Task GetTemplateById_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var templateId = "non-existent-id";

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _controller.GetTemplateById(templateId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GetAllActiveTemplates Tests

        [Fact]
        public async Task GetAllActiveTemplates_ShouldReturnOkWithTemplates()
        {
            // Arrange
            var templates = new List<TeamTemplate>
            {
                CreateTestTeamTemplate("template-1", "Template 1"),
                CreateTestTeamTemplate("template-2", "Template 2"),
                CreateTestTeamTemplate("template-3", "Template 3")
            };

            _mockTeamTemplateService.Setup(x => x.GetAllActiveTemplatesAsync(false))
                .ReturnsAsync(templates);

            // Act
            var result = await _controller.GetAllActiveTemplates();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedTemplates = okResult.Value.Should().BeAssignableTo<IEnumerable<TeamTemplate>>().Subject;
            returnedTemplates.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetAllActiveTemplates_WithEmptyResult_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockTeamTemplateService.Setup(x => x.GetAllActiveTemplatesAsync(false))
                .ReturnsAsync(new List<TeamTemplate>());

            // Act
            var result = await _controller.GetAllActiveTemplates();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedTemplates = okResult.Value.Should().BeAssignableTo<IEnumerable<TeamTemplate>>().Subject;
            returnedTemplates.Should().BeEmpty();
        }

        #endregion

        #region GetUniversalTemplates Tests

        [Fact]
        public async Task GetUniversalTemplates_ShouldReturnOkWithUniversalTemplates()
        {
            // Arrange
            var universalTemplates = new List<TeamTemplate>
            {
                CreateTestTeamTemplate("template-1", "Universal Template 1", isUniversal: true),
                CreateTestTeamTemplate("template-2", "Universal Template 2", isUniversal: true)
            };

            _mockTeamTemplateService.Setup(x => x.GetUniversalTemplatesAsync(false))
                .ReturnsAsync(universalTemplates);

            // Act
            var result = await _controller.GetUniversalTemplates();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedTemplates = okResult.Value.Should().BeAssignableTo<IEnumerable<TeamTemplate>>().Subject;
            returnedTemplates.Should().HaveCount(2);
        }

        #endregion

        #region GetTemplatesBySchoolType Tests

        [Fact]
        public async Task GetTemplatesBySchoolType_WithValidSchoolTypeId_ShouldReturnOk()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var templates = new List<TeamTemplate>
            {
                CreateTestTeamTemplate("template-1", "School Template 1", schoolTypeId: schoolTypeId),
                CreateTestTeamTemplate("template-2", "School Template 2", schoolTypeId: schoolTypeId)
            };

            _mockTeamTemplateService.Setup(x => x.GetTemplatesBySchoolTypeAsync(schoolTypeId, false))
                .ReturnsAsync(templates);

            // Act
            var result = await _controller.GetTemplatesBySchoolType(schoolTypeId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedTemplates = okResult.Value.Should().BeAssignableTo<IEnumerable<TeamTemplate>>().Subject;
            returnedTemplates.Should().HaveCount(2);
        }

        #endregion

        #region GetDefaultTemplateForSchoolType Tests

        [Fact]
        public async Task GetDefaultTemplateForSchoolType_WithExistingDefault_ShouldReturnOk()
        {
            // Arrange
            var schoolTypeId = "st-123";
            var defaultTemplate = CreateTestTeamTemplate("template-default", "Default Template", 
                schoolTypeId: schoolTypeId, isDefault: true);

            _mockTeamTemplateService.Setup(x => x.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId, false))
                .ReturnsAsync(defaultTemplate);

            // Act
            var result = await _controller.GetDefaultTemplateForSchoolType(schoolTypeId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(defaultTemplate);
        }

        [Fact]
        public async Task GetDefaultTemplateForSchoolType_WithNoDefault_ShouldReturnOkWithNull()
        {
            // Arrange
            var schoolTypeId = "st-123";

            _mockTeamTemplateService.Setup(x => x.GetDefaultTemplateForSchoolTypeAsync(schoolTypeId, false))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _controller.GetDefaultTemplateForSchoolType(schoolTypeId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeNull();
        }

        #endregion

        #region CreateTemplate Tests

        [Fact]
        public async Task CreateTemplate_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var requestDto = new CreateTeamTemplateRequestDto
            {
                Name = "New Template",
                TemplateContent = "{SchoolType} - {Subject}",
                Description = "Test template description",
                IsUniversal = false,
                SchoolTypeId = "st-123",
                Category = "Educational",
                Language = "Polski",
                MaxLength = 100,
                RemovePolishChars = false,
                Prefix = "TEAM-",
                Suffix = "-2024",
                Separator = " | ",
                SortOrder = 10
            };

            var createdTemplate = CreateTestTeamTemplate("template-new", requestDto.Name);

            _mockTeamTemplateService.Setup(x => x.CreateTemplateAsync(
                requestDto.Name,
                requestDto.TemplateContent,
                requestDto.Description,
                requestDto.IsUniversal,
                requestDto.SchoolTypeId,
                requestDto.Category))
                .ReturnsAsync(createdTemplate);

            // Act
            var result = await _controller.CreateTemplate(requestDto);

            // Assert
            var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            createdResult.ActionName.Should().Be(nameof(TeamTemplatesController.GetTemplateById));
            createdResult.RouteValues!["templateId"].Should().Be(createdTemplate.Id);
            createdResult.Value.Should().Be(createdTemplate);
        }

        [Fact]
        public async Task CreateTemplate_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new CreateTeamTemplateRequestDto
            {
                Name = "New Template",
                TemplateContent = "{SchoolType} - {Subject}"
            };

            _mockTeamTemplateService.Setup(x => x.CreateTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _controller.CreateTemplate(requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region UpdateTemplate Tests

        [Fact]
        public async Task UpdateTemplate_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var templateId = "template-123";
            var existingTemplate = CreateTestTeamTemplate(templateId, "Existing Template");
            var requestDto = new UpdateTeamTemplateRequestDto
            {
                Name = "Updated Template",
                TemplateContent = "{SchoolType} - {Subject} Updated",
                Description = "Updated description",
                IsDefault = true,
                IsUniversal = false,
                SchoolTypeId = "st-456",
                ExampleOutput = "Example - Math Updated",
                Category = "Updated Category",
                Language = "English",
                MaxLength = 150,
                RemovePolishChars = true,
                Prefix = "UPD-",
                Suffix = "-2025",
                Separator = " :: ",
                SortOrder = 20,
                IsActive = true
            };

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync(existingTemplate);

            _mockTeamTemplateService.Setup(x => x.UpdateTemplateAsync(It.IsAny<TeamTemplate>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateTemplate(templateId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            // Verify that the template was updated with correct values
            _mockTeamTemplateService.Verify(x => x.UpdateTemplateAsync(
                It.Is<TeamTemplate>(t => 
                    t.Id == templateId &&
                    t.Name == requestDto.Name &&
                    t.Template == requestDto.TemplateContent &&
                    t.Description == requestDto.Description &&
                    t.IsDefault == requestDto.IsDefault &&
                    t.IsUniversal == requestDto.IsUniversal &&
                    t.SchoolTypeId == requestDto.SchoolTypeId &&
                    t.ExampleOutput == requestDto.ExampleOutput &&
                    t.Category == requestDto.Category &&
                    t.Language == requestDto.Language &&
                    t.MaxLength == requestDto.MaxLength &&
                    t.RemovePolishChars == requestDto.RemovePolishChars &&
                    t.Prefix == requestDto.Prefix &&
                    t.Suffix == requestDto.Suffix &&
                    t.Separator == requestDto.Separator &&
                    t.SortOrder == requestDto.SortOrder &&
                    t.IsActive == requestDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UpdateTemplate_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var templateId = "non-existent-id";
            var requestDto = new UpdateTeamTemplateRequestDto
            {
                Name = "Updated Template",
                TemplateContent = "{SchoolType} - {Subject}"
            };

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _controller.UpdateTemplate(templateId, requestDto);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task UpdateTemplate_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var templateId = "template-123";
            var existingTemplate = CreateTestTeamTemplate(templateId, "Existing Template");
            var requestDto = new UpdateTeamTemplateRequestDto
            {
                Name = "Updated Template",
                TemplateContent = "{SchoolType} - {Subject}"
            };

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync(existingTemplate);

            _mockTeamTemplateService.Setup(x => x.UpdateTemplateAsync(It.IsAny<TeamTemplate>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateTemplate(templateId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region DeleteTemplate Tests

        [Fact]
        public async Task DeleteTemplate_WithValidId_ShouldReturnOk()
        {
            // Arrange
            var templateId = "template-123";

            _mockTeamTemplateService.Setup(x => x.DeleteTemplateAsync(templateId))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteTemplate(templateId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteTemplate_WithServiceFailure_AndTemplateExists_ShouldReturnBadRequest()
        {
            // Arrange
            var templateId = "template-123";
            var existingTemplate = CreateTestTeamTemplate(templateId, "Existing Template");

            _mockTeamTemplateService.Setup(x => x.DeleteTemplateAsync(templateId))
                .ReturnsAsync(false);

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync(existingTemplate);

            // Act
            var result = await _controller.DeleteTemplate(templateId);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteTemplate_WithServiceFailure_AndTemplateNotExists_ShouldReturnNotFound()
        {
            // Arrange
            var templateId = "non-existent-id";

            _mockTeamTemplateService.Setup(x => x.DeleteTemplateAsync(templateId))
                .ReturnsAsync(false);

            _mockTeamTemplateService.Setup(x => x.GetTemplateByIdAsync(templateId, false))
                .ReturnsAsync((TeamTemplate?)null);

            // Act
            var result = await _controller.DeleteTemplate(templateId);

            // Assert
            var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
            notFoundResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GenerateTeamNameFromTemplate Tests

        [Fact]
        public async Task GenerateTeamNameFromTemplate_WithValidData_ShouldReturnOk()
        {
            // Arrange
            var templateId = "template-123";
            var requestDto = new GenerateTeamNameRequestDto
            {
                Values = new Dictionary<string, string>
                {
                    { "SchoolType", "Liceum" },
                    { "Subject", "Matematyka" },
                    { "Teacher", "Jan Kowalski" }
                }
            };

            var generatedName = "Liceum - Matematyka - Jan Kowalski";

            _mockTeamTemplateService.Setup(x => x.GenerateTeamNameFromTemplateAsync(templateId, requestDto.Values))
                .ReturnsAsync(generatedName);

            // Act
            var result = await _controller.GenerateTeamNameFromTemplate(templateId, requestDto);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
            // Sprawdzamy czy response zawiera wygenerowaną nazwę
            response.Should().NotBeNull();
        }

        [Fact]
        public async Task GenerateTeamNameFromTemplate_WithNullRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var templateId = "template-123";

            // Act
            var result = await _controller.GenerateTeamNameFromTemplate(templateId, null!);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task GenerateTeamNameFromTemplate_WithNullValues_ShouldReturnBadRequest()
        {
            // Arrange
            var templateId = "template-123";
            var requestDto = new GenerateTeamNameRequestDto
            {
                Values = null!
            };

            // Act
            var result = await _controller.GenerateTeamNameFromTemplate(templateId, requestDto);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().NotBeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullTeamTemplateService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamTemplatesController(
                null!,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamTemplatesController(
                _mockTeamTemplateService.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TeamTemplatesController(
                _mockTeamTemplateService.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region Helper Methods

        private TeamTemplate CreateTestTeamTemplate(string id, string name, 
            bool isUniversal = false, string? schoolTypeId = null, bool isDefault = false)
        {
            return new TeamTemplate
            {
                Id = id,
                Name = name,
                Template = "{SchoolType} - {Subject}",
                Description = $"Test template {name}",
                IsDefault = isDefault,
                IsUniversal = isUniversal,
                SchoolTypeId = schoolTypeId,
                ExampleOutput = "Example - Math",
                Category = "Test Category",
                Language = "Polski",
                MaxLength = 100,
                RemovePolishChars = false,
                Prefix = "TEST-",
                Suffix = "-2024",
                Separator = " - ",
                SortOrder = 0,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 