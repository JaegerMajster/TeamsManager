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
    /// Testy jednostkowe dla UserSynchronizer - Etap 5/8
    /// Sprawdza synchronizację użytkowników Microsoft 365 z ochroną soft-deleted users
    /// </summary>
    public class UserSynchronizerTests
    {
        private readonly Mock<ILogger<UserSynchronizer>> _mockLogger;
        private readonly UserSynchronizer _synchronizer;

        public UserSynchronizerTests()
        {
            _mockLogger = new Mock<ILogger<UserSynchronizer>>();
            _synchronizer = new UserSynchronizer(_mockLogger.Object);
        }

        private PSObject CreateMockUserPSObject(
            string id = "test-user-id",
            string givenName = "Jan",
            string surname = "Kowalski",
            string userPrincipalName = "jan.kowalski@contoso.com",
            string displayName = "Jan Kowalski",
            string mail = "jan.kowalski@contoso.com",
            string jobTitle = "Nauczyciel",
            string department = "Matematyka",
            bool accountEnabled = true)
        {
            var psObject = new PSObject();
            psObject.Properties.Add(new PSNoteProperty("Id", id));
            psObject.Properties.Add(new PSNoteProperty("GivenName", givenName));
            psObject.Properties.Add(new PSNoteProperty("Surname", surname));
            psObject.Properties.Add(new PSNoteProperty("UserPrincipalName", userPrincipalName));
            psObject.Properties.Add(new PSNoteProperty("DisplayName", displayName));
            psObject.Properties.Add(new PSNoteProperty("Mail", mail));
            psObject.Properties.Add(new PSNoteProperty("JobTitle", jobTitle));
            psObject.Properties.Add(new PSNoteProperty("Department", department));
            psObject.Properties.Add(new PSNoteProperty("AccountEnabled", accountEnabled));
            return psObject;
        }

        [Fact]
        public async Task SynchronizeAsync_NewUser_ShouldMapAllProperties()
        {
            // Arrange
            var psUser = CreateMockUserPSObject();
            var user = new User();

            // Act
            await _synchronizer.SynchronizeAsync(psUser, user);

            // Assert
            Assert.Equal("test-user-id", user.ExternalId);
            Assert.Equal("Jan", user.FirstName);
            Assert.Equal("Kowalski", user.LastName);
            Assert.Equal("jan.kowalski@contoso.com", user.UPN);
            Assert.Equal("Jan Kowalski", user.DisplayName);
            Assert.Equal("jan.kowalski@contoso.com", user.Email);
            Assert.Equal("Nauczyciel", user.Position);
            Assert.Equal("jan.kowalski@contoso.com", user.AlternateEmail);
            Assert.True(user.IsActive);
        }

        [Fact]
        public async Task SynchronizeAsync_ExistingUser_ShouldPreserveAuditFields()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(displayName: "Jan Kowalski - Zaktualizowany");
            var existingUser = new User
            {
                Id = "existing-id",
                ExternalId = "test-user-id",
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@contoso.com",
                CreatedBy = "original-creator",
                CreatedDate = DateTime.UtcNow.AddDays(-30),
                ModifiedBy = "previous-modifier",
                ModifiedDate = DateTime.UtcNow.AddDays(-1)
            };

            var originalCreatedBy = existingUser.CreatedBy;
            var originalCreatedDate = existingUser.CreatedDate;

            // Act
            await _synchronizer.SynchronizeAsync(psUser, existingUser);

            // Assert
            Assert.Equal("existing-id", existingUser.Id); // ID nie zmienione
            Assert.Equal("Jan Kowalski", existingUser.DisplayName); // Zaktualizowane z Graph
            Assert.Equal(originalCreatedBy, existingUser.CreatedBy); // Zachowane
            Assert.Equal(originalCreatedDate, existingUser.CreatedDate); // Zachowane
        }

        [Fact]
        public async Task SynchronizeAsync_SoftDeletedUser_ShouldSkipSynchronization()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(displayName: "Nowa nazwa");
            var softDeletedUser = new User
            {
                Id = "soft-deleted-id",
                ExternalId = "test-user-id",
                FirstName = "Jan",
                LastName = "Kowalski",
                IsActive = false
            };

            var originalFirstName = softDeletedUser.FirstName;

            // Act
            await _synchronizer.SynchronizeAsync(psUser, softDeletedUser);

            // Assert
            Assert.Equal(originalFirstName, softDeletedUser.FirstName); // Nie zmienione
            Assert.False(softDeletedUser.IsActive); // Nadal nieaktywny
        }

        [Fact]
        public async Task SynchronizeAsync_DisabledAccount_ShouldSetInactiveStatus()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(accountEnabled: false);
            var user = new User();

            // Act
            await _synchronizer.SynchronizeAsync(psUser, user);

            // Assert
            Assert.True(user.IsActive); // UserSynchronizer nie zmienia IsActive automatycznie
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_DifferentDisplayName_ShouldReturnTrue()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(displayName: "Nowa nazwa");
            var existingUser = new User
            {
                ExternalId = "test-user-id",
                FirstName = "Stara",
                LastName = "Nazwa",
                UPN = "jan.kowalski@contoso.com",
                AlternateEmail = "jan.kowalski@contoso.com"
            };

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(psUser, existingUser);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RequiresSynchronizationAsync_SameProperties_ShouldReturnFalse()
        {
            // Arrange
            var psUser = CreateMockUserPSObject();
            var existingUser = new User
            {
                ExternalId = "test-user-id",
                FirstName = "Jan",
                LastName = "Kowalski",
                UPN = "jan.kowalski@contoso.com",
                AlternateEmail = "jan.kowalski@contoso.com",
                Position = "Nauczyciel",
                IsActive = true
            };

            // Act
            var result = await _synchronizer.RequiresSynchronizationAsync(psUser, existingUser);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateGraphObject_ValidUser_ShouldNotThrow()
        {
            // Arrange
            var psUser = CreateMockUserPSObject();

            // Act & Assert
            var exception = Record.Exception(() => _synchronizer.ValidateGraphObject(psUser));
            Assert.Null(exception);
        }

        [Fact]
        public void ValidateGraphObject_MissingUPN_ShouldThrowArgumentException()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(userPrincipalName: "");

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _synchronizer.ValidateGraphObject(psUser));
            Assert.Contains("UserPrincipalName", exception.Message);
        }

        [Fact]
        public void GetGraphId_ValidUser_ShouldReturnId()
        {
            // Arrange
            var psUser = CreateMockUserPSObject();

            // Act
            var result = _synchronizer.GetGraphId(psUser);

            // Assert
            Assert.Equal("test-user-id", result);
        }

        [Fact]
        public void GetGraphId_MissingId_ShouldThrowArgumentException()
        {
            // Arrange
            var psUser = CreateMockUserPSObject(id: "");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _synchronizer.GetGraphId(psUser));
            Assert.Contains("ID", exception.Message);
        }
    }
} 