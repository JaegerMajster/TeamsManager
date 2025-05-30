using FluentAssertions;
using TeamsManager.Core.Services.UserContext;
using Xunit;

namespace TeamsManager.Tests.Services
{
    public class CurrentUserServiceTests
    {
        private readonly CurrentUserService _currentUserService;

        public CurrentUserServiceTests()
        {
            _currentUserService = new CurrentUserService();
        }

        [Fact]
        public void GetCurrentUserUpn_WithoutSettingUpn_ShouldReturnDefaultValue()
        {
            // Act
            var result = _currentUserService.GetCurrentUserUpn();

            // Assert
            result.Should().Be("system@teamsmanager.local");
        }

        [Fact]
        public void SetCurrentUserUpn_WithValidUpn_ShouldSetUpn()
        {
            // Arrange
            var expectedUpn = "test.user@example.com";

            // Act
            _currentUserService.SetCurrentUserUpn(expectedUpn);
            var result = _currentUserService.GetCurrentUserUpn();

            // Assert
            result.Should().Be(expectedUpn);
        }

        [Fact]
        public void SetCurrentUserUpn_WithNull_ShouldReturnDefaultValue()
        {
            // Arrange
            _currentUserService.SetCurrentUserUpn("test@example.com");

            // Act
            _currentUserService.SetCurrentUserUpn(null);
            var result = _currentUserService.GetCurrentUserUpn();

            // Assert
            result.Should().Be("system@teamsmanager.local");
        }

        [Fact]
        public void SetCurrentUserUpn_WithEmptyString_ShouldSetEmptyString()
        {
            // Arrange
            var emptyUpn = "";

            // Act
            _currentUserService.SetCurrentUserUpn(emptyUpn);
            var result = _currentUserService.GetCurrentUserUpn();

            // Assert
            result.Should().Be(emptyUpn);
        }

        [Fact]
        public void SetCurrentUserUpn_MultipleChanges_ShouldReturnLatestValue()
        {
            // Arrange
            var firstUpn = "first@example.com";
            var secondUpn = "second@example.com";

            // Act
            _currentUserService.SetCurrentUserUpn(firstUpn);
            var firstResult = _currentUserService.GetCurrentUserUpn();

            _currentUserService.SetCurrentUserUpn(secondUpn);
            var secondResult = _currentUserService.GetCurrentUserUpn();

            // Assert
            firstResult.Should().Be(firstUpn);
            secondResult.Should().Be(secondUpn);
        }
    }
} 