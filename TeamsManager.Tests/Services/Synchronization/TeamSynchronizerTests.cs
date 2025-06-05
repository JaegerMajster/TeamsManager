using System;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Services.Synchronization;
using Xunit;

namespace TeamsManager.Tests.Services.Synchronization
{
    public class TeamSynchronizerTests
    {
        private readonly Mock<ILogger<TeamSynchronizer>> _loggerMock;
        private readonly TeamSynchronizer _synchronizer;

        public TeamSynchronizerTests()
        {
            _loggerMock = new Mock<ILogger<TeamSynchronizer>>();
            _synchronizer = new TeamSynchronizer(_loggerMock.Object);
        }

        [Fact]
        public async Task SynchronizeAsync_NewTeam_CreatesCorrectEntity()
        {
            // Arrange
            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Test Team",
                Description = "Test Description",
                Visibility = "Public",
                IsArchived = false,
                CreatedDateTime = DateTime.UtcNow.AddDays(-7)
            });

            // Act
            var result = await _synchronizer.SynchronizeAsync(graphObject, null, "test@contoso.com");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("12345-67890", result.ExternalId);
            Assert.Equal("Test Team", result.DisplayName);
            Assert.Equal("Test Description", result.Description);
            Assert.Equal(TeamVisibility.Public, result.Visibility);
            Assert.Equal(TeamStatus.Active, result.Status);
            Assert.Equal("test@contoso.com", result.CreatedBy);
            Assert.NotEqual(default(DateTime), result.CreatedDate);
        }

        [Fact]
        public async Task SynchronizeAsync_ExistingTeam_UpdatesCorrectly()
        {
            // Arrange
            var existingTeam = new Team
            {
                Id = "local-123",
                ExternalId = "12345-67890",
                DisplayName = "Old Name",
                Description = "Old Description",
                Visibility = TeamVisibility.Private,
                Status = TeamStatus.Active,
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                CreatedBy = "creator@contoso.com"
            };

            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Updated Team Name",
                Description = "Updated Description",
                Visibility = "Public",
                IsArchived = false
            });

            // Act
            var result = await _synchronizer.SynchronizeAsync(graphObject, existingTeam, "updater@contoso.com");

            // Assert
            Assert.Same(existingTeam, result);
            Assert.Equal("Updated Team Name", result.DisplayName);
            Assert.Equal("Updated Description", result.Description);
            Assert.Equal(TeamVisibility.Public, result.Visibility);
            Assert.Equal("updater@contoso.com", result.ModifiedBy);
            Assert.NotNull(result.ModifiedDate);
        }

        [Fact]
        public async Task SynchronizeAsync_ArchivedTeam_AddsPrefix()
        {
            // Arrange
            var existingTeam = new Team
            {
                Id = "local-123",
                ExternalId = "12345-67890",
                DisplayName = "Active Team",
                Description = "Team Description",
                Status = TeamStatus.Active
            };

            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Active Team",
                Description = "Team Description",
                Visibility = "Private",
                IsArchived = true
            });

            // Act
            var result = await _synchronizer.SynchronizeAsync(graphObject, existingTeam);

            // Assert
            Assert.Equal(TeamStatus.Archived, result.Status);
            Assert.Equal("ARCHIWALNY - Active Team", result.DisplayName);
            Assert.Equal("ARCHIWALNY - Team Description", result.Description);
        }

        [Fact]
        public async Task SynchronizeAsync_RestoredTeam_RemovesPrefix()
        {
            // Arrange
            var existingTeam = new Team
            {
                Id = "local-123",
                ExternalId = "12345-67890",
                DisplayName = "ARCHIWALNY - Restored Team",
                Description = "ARCHIWALNY - Team Description",
                Status = TeamStatus.Archived
            };

            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Restored Team",
                Description = "Team Description",
                Visibility = "Private",
                IsArchived = false
            });

            // Act
            var result = await _synchronizer.SynchronizeAsync(graphObject, existingTeam);

            // Assert
            Assert.Equal(TeamStatus.Active, result.Status);
            Assert.Equal("Restored Team", result.DisplayName);
            Assert.Equal("Team Description", result.Description);
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_NoChanges_ReturnsFalse()
        {
            // Arrange
            var existingTeam = new Team
            {
                Id = "local-123",
                ExternalId = "12345-67890",
                DisplayName = "Test Team",
                Description = "Test Description",
                Visibility = TeamVisibility.Private,
                Status = TeamStatus.Active
            };

            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Test Team",
                Description = "Test Description",
                Visibility = "Private",
                IsArchived = false
            });

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(graphObject, existingTeam);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_WithChanges_ReturnsTrue()
        {
            // Arrange
            var existingTeam = new Team
            {
                Id = "local-123",
                ExternalId = "12345-67890",
                DisplayName = "Old Name",
                Description = "Test Description",
                Visibility = TeamVisibility.Private,
                Status = TeamStatus.Active
            };

            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "New Name",
                Description = "Test Description",
                Visibility = "Private",
                IsArchived = false
            });

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(graphObject, existingTeam);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateGraphObject_MissingId_ThrowsException()
        {
            // Arrange
            var graphObject = CreatePSObject(new
            {
                DisplayName = "Test Team",
                Description = "Test Description"
            });

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _synchronizer.ValidateGraphObject(graphObject));
        }

        [Fact]
        public void ValidateGraphObject_MissingDisplayName_ThrowsException()
        {
            // Arrange
            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                Description = "Test Description"
            });

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _synchronizer.ValidateGraphObject(graphObject));
        }

        [Fact]
        public void GetGraphId_ReturnsCorrectId()
        {
            // Arrange
            var graphObject = CreatePSObject(new
            {
                Id = "12345-67890",
                DisplayName = "Test Team"
            });

            // Act
            var result = _synchronizer.GetGraphId(graphObject);

            // Assert
            Assert.Equal("12345-67890", result);
        }

        // Helper method to create PSObject for testing
        private PSObject CreatePSObject(object data)
        {
            var psObject = new PSObject();
            foreach (var prop in data.GetType().GetProperties())
            {
                psObject.Properties.Add(new PSNoteProperty(prop.Name, prop.GetValue(data)));
            }
            return psObject;
        }
    }
} 