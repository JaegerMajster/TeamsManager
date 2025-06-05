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
    /// <summary>
    /// Testy jednostkowe dla ChannelSynchronizer - Etap 5/8
    /// Sprawdza synchronizację kanałów Microsoft Teams z wykorzystaniem istniejącej logiki MapPsObjectToLocalChannel
    /// </summary>
    public class ChannelSynchronizerTests
    {
        private readonly Mock<ILogger<ChannelSynchronizer>> _mockLogger;
        private readonly ChannelSynchronizer _synchronizer;

        public ChannelSynchronizerTests()
        {
            _mockLogger = new Mock<ILogger<ChannelSynchronizer>>();
            _synchronizer = new ChannelSynchronizer(_mockLogger.Object);
        }

        private PSObject CreateMockChannelPSObject(
            string id = "test-channel-id",
            string displayName = "Ogólny",
            string description = "Kanał ogólny",
            string membershipType = "Standard",
            string webUrl = "https://teams.microsoft.com/l/channel/...",
            int filesCount = 5,
            long filesSize = 1024000,
            int messageCount = 100,
            bool isModerationEnabled = false,
            string category = "General",
            string tags = "important",
            int sortOrder = 1)
        {
            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty("Id", id));
            psObject.Properties.Add(new PSNoteProperty("DisplayName", displayName));
            psObject.Properties.Add(new PSNoteProperty("Description", description));
            psObject.Properties.Add(new PSNoteProperty("MembershipType", membershipType));
            psObject.Properties.Add(new PSNoteProperty("WebUrl", webUrl));
            psObject.Properties.Add(new PSNoteProperty("FilesCount", filesCount));
            psObject.Properties.Add(new PSNoteProperty("FilesSize", filesSize));
            psObject.Properties.Add(new PSNoteProperty("MessageCount", messageCount));
            psObject.Properties.Add(new PSNoteProperty("IsModerationEnabled", isModerationEnabled));
            psObject.Properties.Add(new PSNoteProperty("Category", category));
            psObject.Properties.Add(new PSNoteProperty("Tags", tags));
            psObject.Properties.Add(new PSNoteProperty("SortOrder", sortOrder));
            psObject.Properties.Add(new PSNoteProperty("LastActivityDate", DateTime.UtcNow.AddDays(-1)));
            psObject.Properties.Add(new PSNoteProperty("LastMessageDate", DateTime.UtcNow.AddHours(-2)));
            psObject.Properties.Add(new PSNoteProperty("NotificationSettings", "AllActivity"));
            return psObject;
        }

        [Fact]
        public async Task SynchronizeAsync_NewChannel_ShouldMapAllProperties()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject();
            var channel = new Channel { TeamId = "test-team-id" };

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, channel);

            // Assert
            Assert.Equal("test-channel-id", channel.Id);
            Assert.Equal("Ogólny", channel.DisplayName);
            Assert.Equal("Kanał ogólny", channel.Description);
            Assert.Equal("test-team-id", channel.TeamId);
            Assert.Equal("Standard", channel.ChannelType);
            Assert.Equal("https://teams.microsoft.com/l/channel/...", channel.ExternalUrl);
            Assert.Equal(5, channel.FilesCount);
            Assert.Equal(1024000, channel.FilesSize);
            Assert.Equal(100, channel.MessageCount);
            Assert.False(channel.IsModerationEnabled);
            Assert.Equal("General", channel.Category);
            Assert.Equal("important", channel.Tags);
            Assert.Equal(1, channel.SortOrder);
            Assert.True(channel.IsGeneral); // "Ogólny" powinien być oznaczony jako General
            Assert.Equal(ChannelStatus.Active, channel.Status);
        }

        [Fact]
        public async Task SynchronizeAsync_PrivateChannel_ShouldSetIsPrivateTrue()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(
                displayName: "Prywatny kanał",
                membershipType: "private"
            );
            var channel = new Channel { TeamId = "test-team-id" };

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, channel);

            // Assert
            Assert.True(channel.IsPrivate);
            Assert.Equal("private", channel.ChannelType);
            Assert.False(channel.IsGeneral); // Prywatny kanał nie może być General
        }

        [Fact]
        public async Task SynchronizeAsync_ExistingChannel_ShouldPreserveAuditFields()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(displayName: "Zaktualizowany kanał");
            var existingChannel = new Channel
            {
                Id = "test-channel-id",
                TeamId = "test-team-id",
                DisplayName = "Stary kanał",
                CreatedBy = "original-creator",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                ModifiedBy = "previous-modifier",
                ModifiedDate = DateTime.UtcNow.AddDays(-1)
            };

            var originalCreatedBy = existingChannel.CreatedBy;
            var originalCreatedDate = existingChannel.CreatedDate;
            var originalTeamId = existingChannel.TeamId;

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, existingChannel);

            // Assert
            Assert.Equal("Zaktualizowany kanał", existingChannel.DisplayName); // Zaktualizowane
            Assert.Equal(originalTeamId, existingChannel.TeamId); // Zachowane
            Assert.Equal(originalCreatedBy, existingChannel.CreatedBy); // Zachowane
            Assert.Equal(originalCreatedDate, existingChannel.CreatedDate); // Zachowane
        }

        [Fact]
        public async Task SynchronizeAsync_GeneralChannel_ShouldSetIsGeneralTrue()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(displayName: "General");
            var channel = new Channel { TeamId = "test-team-id" };

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, channel);

            // Assert
            Assert.True(channel.IsGeneral);
            Assert.Equal("Standard", channel.ChannelType); // Powinien być ustawiony na Standard
        }

        [Fact]
        public async Task SynchronizeAsync_NegativeValues_ShouldNormalizeToZero()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(
                filesCount: -5,
                filesSize: -1000,
                messageCount: -10
            );
            var channel = new Channel { TeamId = "test-team-id" };

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, channel);

            // Assert
            Assert.Equal(0, channel.FilesCount);
            Assert.Equal(0, channel.FilesSize);
            Assert.Equal(0, channel.MessageCount);
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_DifferentDisplayName_ShouldReturnTrue()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(displayName: "Nowa nazwa");
            var existingChannel = new Channel
            {
                Id = "test-channel-id",
                DisplayName = "Stara nazwa",
                Description = "Kanał ogólny",
                ChannelType = "Standard",
                FilesCount = 5,
                MessageCount = 100
            };

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(psChannel, existingChannel);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_SameProperties_ShouldReturnFalse()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject();
            var existingChannel = new Channel
            {
                Id = "test-channel-id",
                DisplayName = "Ogólny",
                Description = "Kanał ogólny",
                ChannelType = "Standard",
                ExternalUrl = "https://teams.microsoft.com/l/channel/...",
                FilesCount = 5,
                FilesSize = 1024000,
                MessageCount = 100,
                IsModerationEnabled = false,
                Category = "General",
                Tags = "important",
                SortOrder = 1,
                IsGeneral = true,
                IsPrivate = false
            };

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(psChannel, existingChannel);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateGraphObject_ValidChannel_ShouldNotThrow()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject();

            // Act & Assert
            var exception = Record.Exception(() => _synchronizer.ValidateGraphObject(psChannel));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateGraphObject_MissingDisplayName_ShouldThrowArgumentException()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(displayName: "");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _synchronizer.ValidateGraphObject(psChannel));
            Assert.Contains("DisplayName", exception.Message);
        }

        [Fact]
        public void GetGraphId_ValidChannel_ShouldReturnId()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject();

            // Act
            var result = _synchronizer.GetGraphId(psChannel);

            // Assert
            Assert.Equal("test-channel-id", result);
        }

        [Fact]
        public void GetGraphId_MissingId_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject(id: "");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _synchronizer.GetGraphId(psChannel));
            Assert.Contains("ID", exception.Message);
        }

        [Fact]
        public async Task SynchronizeAsync_ArchivedChannelRestored_ShouldSetActiveStatus()
        {
            // Arrange
            var psChannel = CreateMockChannelPSObject();
            var archivedChannel = new Channel
            {
                Id = "test-channel-id",
                TeamId = "test-team-id",
                Status = ChannelStatus.Archived,
                DisplayName = "Przywrócony kanał"
            };

            // Act
            await _synchronizer.SynchronizeAsync(psChannel, archivedChannel);

            // Assert
            Assert.Equal(ChannelStatus.Active, archivedChannel.Status);
            Assert.True(archivedChannel.IsActive);
        }
    }
} 