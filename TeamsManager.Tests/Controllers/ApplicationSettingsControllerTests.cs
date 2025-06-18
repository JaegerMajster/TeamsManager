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
    /// Testy jednostkowe dla ApplicationSettingsController
    /// Pokrycie: endpointy CRUD ustawień aplikacji, walidacja, obsługa błędów
    /// </summary>
    public class ApplicationSettingsControllerTests
    {
        private readonly Mock<IApplicationSettingService> _mockApplicationSettingService;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<ApplicationSettingsController>> _mockLogger;
        private readonly ApplicationSettingsController _controller;

        public ApplicationSettingsControllerTests()
        {
            _mockApplicationSettingService = new Mock<IApplicationSettingService>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<ApplicationSettingsController>>();

            _controller = new ApplicationSettingsController(
                _mockApplicationSettingService.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetSettingByKey Tests

        [Fact]
        public async Task GetSettingByKey_WithValidKey_ShouldReturnOk()
        {
            // Arrange
            var key = "TestSetting";
            var setting = CreateTestSetting(key, "TestValue", SettingType.String);

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(key, false))
                .ReturnsAsync(setting);

            // Act
            var result = await _controller.GetSettingByKey(key);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().Be(setting);
        }

        [Fact]
        public async Task GetSettingByKey_WithNonExistentKey_ShouldReturnNotFound()
        {
            // Arrange
            var key = "NonExistentSetting";

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(key, false))
                .ReturnsAsync((ApplicationSetting?)null);

            // Act
            var result = await _controller.GetSettingByKey(key);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().BeEquivalentTo(new { Message = $"Ustawienie aplikacji o kluczu '{key}' nie zostało znalezione." });
        }

        [Fact]
        public async Task GetSettingByKey_WithUrlEncodedKey_ShouldDecodeKey()
        {
            // Arrange
            var originalKey = "Test Setting With Spaces";
            var encodedKey = "Test%20Setting%20With%20Spaces";
            var setting = CreateTestSetting(originalKey, "TestValue", SettingType.String);

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(originalKey, false))
                .ReturnsAsync(setting);

            // Act
            var result = await _controller.GetSettingByKey(encodedKey);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockApplicationSettingService.Verify(x => x.GetSettingByKeyAsync(originalKey, false), Times.Once);
        }

        #endregion

        #region GetAllSettings Tests

        [Fact]
        public async Task GetAllSettings_ShouldReturnOkWithSettings()
        {
            // Arrange
            var settings = new List<ApplicationSetting>
            {
                CreateTestSetting("Setting1", "Value1", SettingType.String),
                CreateTestSetting("Setting2", "123", SettingType.Integer),
                CreateTestSetting("Setting3", "true", SettingType.Boolean)
            };

            _mockApplicationSettingService.Setup(x => x.GetAllSettingsAsync(false))
                .ReturnsAsync(settings);

            // Act
            var result = await _controller.GetAllSettings();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var returnedSettings = okResult!.Value.Should().BeAssignableTo<IEnumerable<ApplicationSetting>>().Subject;
            returnedSettings.Should().HaveCount(3);
            returnedSettings.Should().BeEquivalentTo(settings);
        }

        [Fact]
        public async Task GetAllSettings_WithEmptyResult_ShouldReturnOkWithEmptyList()
        {
            // Arrange
            _mockApplicationSettingService.Setup(x => x.GetAllSettingsAsync(false))
                .ReturnsAsync(new List<ApplicationSetting>());

            // Act
            var result = await _controller.GetAllSettings();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var returnedSettings = okResult!.Value.Should().BeAssignableTo<IEnumerable<ApplicationSetting>>().Subject;
            returnedSettings.Should().BeEmpty();
        }

        #endregion

        #region GetSettingsByCategory Tests

        [Fact]
        public async Task GetSettingsByCategory_WithValidCategory_ShouldReturnOk()
        {
            // Arrange
            var category = "General";
            var settings = new List<ApplicationSetting>
            {
                CreateTestSetting("Setting1", "Value1", SettingType.String, category: category),
                CreateTestSetting("Setting2", "Value2", SettingType.String, category: category)
            };

            _mockApplicationSettingService.Setup(x => x.GetSettingsByCategoryAsync(category, false))
                .ReturnsAsync(settings);

            // Act
            var result = await _controller.GetSettingsByCategory(category);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            var returnedSettings = okResult!.Value.Should().BeAssignableTo<IEnumerable<ApplicationSetting>>().Subject;
            returnedSettings.Should().HaveCount(2);
            returnedSettings.Should().BeEquivalentTo(settings);
        }

        [Fact]
        public async Task GetSettingsByCategory_WithUrlEncodedCategory_ShouldDecodeCategory()
        {
            // Arrange
            var originalCategory = "Test Category";
            var encodedCategory = "Test%20Category";
            var settings = new List<ApplicationSetting>
            {
                CreateTestSetting("Setting1", "Value1", SettingType.String, category: originalCategory)
            };

            _mockApplicationSettingService.Setup(x => x.GetSettingsByCategoryAsync(originalCategory, false))
                .ReturnsAsync(settings);

            // Act
            var result = await _controller.GetSettingsByCategory(encodedCategory);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockApplicationSettingService.Verify(x => x.GetSettingsByCategoryAsync(originalCategory, false), Times.Once);
        }

        #endregion

        #region SaveSetting Tests

        [Fact]
        public async Task SaveSetting_WithValidData_ShouldReturnOkWithSavedSetting()
        {
            // Arrange
            var requestDto = new SaveSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue",
                Type = SettingType.String,
                Description = "Test description",
                Category = "General"
            };

            var savedSetting = CreateTestSetting(requestDto.Key, requestDto.Value, requestDto.Type, 
                requestDto.Description, requestDto.Category);

            _mockApplicationSettingService.Setup(x => x.SaveSettingAsync(
                requestDto.Key, requestDto.Value, requestDto.Type, requestDto.Description, requestDto.Category))
                .ReturnsAsync(true);

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync(savedSetting);

            // Act
            var result = await _controller.SaveSetting(requestDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().Be(savedSetting);
        }

        [Fact]
        public async Task SaveSetting_WhenSavedSettingCannotBeRetrieved_ShouldReturnOkWithMessage()
        {
            // Arrange
            var requestDto = new SaveSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue",
                Type = SettingType.String
            };

            _mockApplicationSettingService.Setup(x => x.SaveSettingAsync(
                requestDto.Key, requestDto.Value, requestDto.Type, requestDto.Description, requestDto.Category))
                .ReturnsAsync(true);

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync((ApplicationSetting?)null);

            // Act
            var result = await _controller.SaveSetting(requestDto);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(new { Message = $"Ustawienie '{requestDto.Key}' zostało przetworzone." });
        }

        [Fact]
        public async Task SaveSetting_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var requestDto = new SaveSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue",
                Type = SettingType.String
            };

            _mockApplicationSettingService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SettingType>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.SaveSetting(requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zapisać/zaktualizować ustawienia. Sprawdź logi serwera." });
        }

        #endregion

        #region UpdateSetting Tests

        [Fact]
        public async Task UpdateSetting_WithValidData_ShouldReturnNoContent()
        {
            // Arrange
            var settingId = "setting-123";
            var requestDto = new UpdateSettingRequestDto
            {
                Key = "TestSetting",
                Value = "UpdatedValue",
                Type = SettingType.String,
                Description = "Updated description",
                Category = "General",
                IsRequired = true,
                IsVisible = true,
                DefaultValue = "DefaultValue",
                ValidationPattern = @"^\w+$",
                ValidationMessage = "Invalid format",
                DisplayOrder = 1,
                IsActive = true
            };

            var existingSetting = CreateTestSetting(requestDto.Key, "OldValue", SettingType.String);
            existingSetting.Id = settingId;

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync(existingSetting);

            _mockApplicationSettingService.Setup(x => x.UpdateSettingAsync(It.IsAny<ApplicationSetting>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateSetting(settingId, requestDto);

            // Assert
            result.Should().BeOfType<NoContentResult>();

            // Verify that UpdateSettingAsync was called with correct values
            _mockApplicationSettingService.Verify(x => x.UpdateSettingAsync(It.Is<ApplicationSetting>(s =>
                s.Id == settingId &&
                s.Key == requestDto.Key &&
                s.Value == requestDto.Value &&
                s.Type == requestDto.Type &&
                s.Description == requestDto.Description &&
                s.Category == requestDto.Category &&
                s.IsRequired == requestDto.IsRequired &&
                s.IsVisible == requestDto.IsVisible &&
                s.DefaultValue == requestDto.DefaultValue &&
                s.ValidationPattern == requestDto.ValidationPattern &&
                s.ValidationMessage == requestDto.ValidationMessage &&
                s.DisplayOrder == requestDto.DisplayOrder &&
                s.IsActive == requestDto.IsActive)), Times.Once);
        }

        [Fact]
        public async Task UpdateSetting_WithNonExistentSetting_ShouldReturnNotFound()
        {
            // Arrange
            var settingId = "non-existent-id";
            var requestDto = new UpdateSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue"
            };

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync((ApplicationSetting?)null);

            // Act
            var result = await _controller.UpdateSetting(settingId, requestDto);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().BeEquivalentTo(new { Message = $"Ustawienie o ID '{settingId}' (lub kluczu '{requestDto.Key}') nie zostało znalezione." });
        }

        [Fact]
        public async Task UpdateSetting_WithMismatchedSettingId_ShouldReturnNotFound()
        {
            // Arrange
            var settingId = "setting-123";
            var requestDto = new UpdateSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue"
            };

            var existingSetting = CreateTestSetting(requestDto.Key, "OldValue", SettingType.String);
            existingSetting.Id = "different-id"; // Different ID

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync(existingSetting);

            // Act
            var result = await _controller.UpdateSetting(settingId, requestDto);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result as NotFoundObjectResult;
            notFoundResult!.Value.Should().BeEquivalentTo(new { Message = $"Ustawienie o ID '{settingId}' (lub kluczu '{requestDto.Key}') nie zostało znalezione." });
        }

        [Fact]
        public async Task UpdateSetting_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var settingId = "setting-123";
            var requestDto = new UpdateSettingRequestDto
            {
                Key = "TestSetting",
                Value = "TestValue"
            };

            var existingSetting = CreateTestSetting(requestDto.Key, "OldValue", SettingType.String);
            existingSetting.Id = settingId;

            _mockApplicationSettingService.Setup(x => x.GetSettingByKeyAsync(requestDto.Key, false))
                .ReturnsAsync(existingSetting);

            _mockApplicationSettingService.Setup(x => x.UpdateSettingAsync(It.IsAny<ApplicationSetting>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.UpdateSetting(settingId, requestDto);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().BeEquivalentTo(new { Message = "Nie udało się zaktualizować ustawienia." });
        }

        #endregion

        #region DeleteSetting Tests

        [Fact]
        public async Task DeleteSetting_WithValidKey_ShouldReturnOk()
        {
            // Arrange
            var key = "TestSetting";

            _mockApplicationSettingService.Setup(x => x.DeleteSettingAsync(key))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSetting(key);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            var okResult = result as OkObjectResult;
            okResult!.Value.Should().BeEquivalentTo(new { Message = "Ustawienie aplikacji usunięte (zdezaktywowane) pomyślnie." });
        }

        [Fact]
        public async Task DeleteSetting_WithUrlEncodedKey_ShouldDecodeKey()
        {
            // Arrange
            var originalKey = "Test Setting";
            var encodedKey = "Test%20Setting";

            _mockApplicationSettingService.Setup(x => x.DeleteSettingAsync(originalKey))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.DeleteSetting(encodedKey);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _mockApplicationSettingService.Verify(x => x.DeleteSettingAsync(originalKey), Times.Once);
        }

        [Fact]
        public async Task DeleteSetting_WithServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var key = "TestSetting";

            _mockApplicationSettingService.Setup(x => x.DeleteSettingAsync(key))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.DeleteSetting(key);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
            var badRequestResult = result as BadRequestObjectResult;
            badRequestResult!.Value.Should().BeEquivalentTo(new { Message = "Nie udało się usunąć (zdezaktywować) ustawienia aplikacji." });
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullApplicationSettingService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ApplicationSettingsController(
                null!,
                _mockCurrentUserService.Object,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullCurrentUserService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ApplicationSettingsController(
                _mockApplicationSettingService.Object,
                null!,
                _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ApplicationSettingsController(
                _mockApplicationSettingService.Object,
                _mockCurrentUserService.Object,
                null!));
        }

        #endregion

        #region Helper Methods

        private static ApplicationSetting CreateTestSetting(string key, string value, SettingType type, 
            string? description = null, string? category = null)
        {
            return new ApplicationSetting
            {
                Id = Guid.NewGuid().ToString(),
                Key = key,
                Value = value,
                Type = type,
                Description = description ?? "Test description",
                Category = category ?? "General",
                IsRequired = false,
                IsVisible = true,
                DefaultValue = null,
                ValidationPattern = null,
                ValidationMessage = null,
                DisplayOrder = 0,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test@test.com"
            };
        }

        #endregion
    }
} 