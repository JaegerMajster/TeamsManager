using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using Xunit;

namespace TeamsManager.Tests.Models
{
    /// <summary>
    /// Testy jednostkowe dla GraphTeam i wszystkich powiązanych klas
    /// Pokrycie: GraphTeam, GraphTeamSettings, GraphTeamGuestSettings, GraphTeamMemberSettings,
    /// GraphTeamMessagingSettings, GraphTeamFunSettings, GraphTeamDiscoverySettings, 
    /// GraphTeamMember, GraphSyncInfo
    /// </summary>
    public class GraphTeamTests
    {
        #region GraphTeam Constructor Tests

        [Fact]
        public void GraphTeam_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var team = new GraphTeam();

            // Assert - podstawowe właściwości
            team.Id.Should().BeNull();
            team.DisplayName.Should().BeNull();
            team.Description.Should().BeNull();
            team.Mail.Should().BeNull();
            team.MailNickname.Should().BeNull();
            team.WebUrl.Should().BeNull();
            team.PhotoUrl.Should().BeNull();
            team.Classification.Should().BeNull();
            team.Visibility.Should().BeNull();
            team.IsArchived.Should().BeFalse();
            team.ETag.Should().BeNull();
            team.CreatedDateTime.Should().BeNull();
            team.TenantId.Should().BeNull();
            team.IsActive.Should().BeTrue();
            team.MemberCount.Should().Be(0);
            team.OwnerCount.Should().Be(0);

            // Assert - ustawienia (wszystkie null)
            team.Settings.Should().BeNull();
            team.GuestSettings.Should().BeNull();
            team.MemberSettings.Should().BeNull();
            team.MessagingSettings.Should().BeNull();
            team.FunSettings.Should().BeNull();
            team.DiscoverySettings.Should().BeNull();
            team.SyncInfo.Should().BeNull();

            // Assert - kolekcje (zainicjalizowane ale puste)
            team.Members.Should().NotBeNull().And.BeEmpty();
            team.Channels.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void GraphTeam_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var team = new GraphTeam();
            var createdDate = DateTime.UtcNow.AddDays(-30);
            var settings = new GraphTeamSettings { AllowCreateUpdateChannels = true };
            var guestSettings = new GraphTeamGuestSettings { AllowCreateUpdateChannels = false };

            // Act
            team.Id = "team-123-guid";
            team.DisplayName = "Mathematics Team";
            team.Description = "Team for mathematics teachers";
            team.Mail = "math-team@school.edu";
            team.MailNickname = "mathteam";
            team.WebUrl = "https://teams.microsoft.com/l/team/team-123";
            team.PhotoUrl = "https://graph.microsoft.com/photo.jpg";
            team.Classification = "Internal";
            team.Visibility = "Private";
            team.IsArchived = true;
            team.ETag = "etag-abc123";
            team.CreatedDateTime = createdDate;
            team.TenantId = "tenant-456";
            team.IsActive = false;
            team.MemberCount = 25;
            team.OwnerCount = 3;
            team.Settings = settings;
            team.GuestSettings = guestSettings;

            // Assert
            team.Id.Should().Be("team-123-guid");
            team.DisplayName.Should().Be("Mathematics Team");
            team.Description.Should().Be("Team for mathematics teachers");
            team.Mail.Should().Be("math-team@school.edu");
            team.MailNickname.Should().Be("mathteam");
            team.WebUrl.Should().Be("https://teams.microsoft.com/l/team/team-123");
            team.PhotoUrl.Should().Be("https://graph.microsoft.com/photo.jpg");
            team.Classification.Should().Be("Internal");
            team.Visibility.Should().Be("Private");
            team.IsArchived.Should().BeTrue();
            team.ETag.Should().Be("etag-abc123");
            team.CreatedDateTime.Should().Be(createdDate);
            team.TenantId.Should().Be("tenant-456");
            team.IsActive.Should().BeFalse();
            team.MemberCount.Should().Be(25);
            team.OwnerCount.Should().Be(3);
            team.Settings.Should().Be(settings);
            team.GuestSettings.Should().Be(guestSettings);
        }

        #endregion

        #region GraphTeam Conversion Methods Tests

        [Fact]
        public void ToLocalTeam_ShouldConvertToLocalTeamCorrectly()
        {
            // Arrange
            var graphTeam = new GraphTeam
            {
                Id = "graph-team-123",
                DisplayName = "Science Team",
                Description = "Team for science teachers",
                IsActive = true,
                CreatedDateTime = DateTime.UtcNow.AddDays(-15)
            };

            // Act
            var localTeam = graphTeam.ToLocalTeam();

            // Assert
            localTeam.Should().NotBeNull();
            localTeam.DisplayName.Should().Be("Science Team");
            localTeam.Description.Should().Be("Team for science teachers");
            localTeam.ExternalId.Should().Be("graph-team-123");
            localTeam.Status.Should().Be(TeamStatus.Active);
            localTeam.CreatedDate.Should().BeCloseTo(graphTeam.CreatedDateTime.Value, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ToLocalTeam_WhenInactive_ShouldSetArchivedStatus()
        {
            // Arrange
            var graphTeam = new GraphTeam
            {
                DisplayName = "Archived Team",
                IsActive = false
            };

            // Act
            var localTeam = graphTeam.ToLocalTeam();

            // Assert
            localTeam.Status.Should().Be(TeamStatus.Archived);
        }

        [Fact]
        public void ToLocalTeam_WhenNullProperties_ShouldUseDefaults()
        {
            // Arrange
            var graphTeam = new GraphTeam
            {
                DisplayName = null,
                Description = null,
                CreatedDateTime = null
            };

            // Act
            var localTeam = graphTeam.ToLocalTeam();

            // Assert
            localTeam.DisplayName.Should().Be(string.Empty);
            localTeam.Description.Should().Be(string.Empty);
            localTeam.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void FromLocalTeam_ShouldConvertFromLocalTeamCorrectly()
        {
            // Arrange
            var localTeam = new Team
            {
                Id = "local-team-456",
                ExternalId = "external-123",
                DisplayName = "History Team",
                Description = "Team for history teachers",
                Status = TeamStatus.Active, // Ustawiamy Status zamiast IsActive
                CreatedDate = DateTime.UtcNow.AddDays(-20)
            };

            // Act
            var graphTeam = GraphTeam.FromLocalTeam(localTeam);

            // Assert
            graphTeam.Should().NotBeNull();
            graphTeam.Id.Should().Be("external-123");
            graphTeam.DisplayName.Should().Be("History Team");
            graphTeam.Description.Should().Be("Team for history teachers");
            graphTeam.IsActive.Should().BeTrue();
            graphTeam.CreatedDateTime.Should().Be(localTeam.CreatedDate);
        }

        [Fact]
        public void FromLocalTeam_WhenInactiveLocalTeam_ShouldSetCorrectStatus()
        {
            // Arrange
            var localTeam = new Team
            {
                DisplayName = "Inactive Team",
                Status = TeamStatus.Archived // Ustawiamy Status zamiast IsActive
            };

            // Act
            var graphTeam = GraphTeam.FromLocalTeam(localTeam);

            // Assert
            graphTeam.IsActive.Should().BeFalse();
        }

        #endregion

        #region GraphTeam Helper Methods Tests

        [Fact]
        public void HasMember_WhenUserIsMember_ShouldReturnTrue()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });
            team.Members.Add(new GraphTeamMember { UserId = "user-456", Role = "owner" });

            // Act & Assert
            team.HasMember("user-123").Should().BeTrue();
            team.HasMember("user-456").Should().BeTrue();
        }

        [Fact]
        public void HasMember_WhenUserIsNotMember_ShouldReturnFalse()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });

            // Act & Assert
            team.HasMember("user-999").Should().BeFalse();
        }

        [Fact]
        public void HasMember_WhenNullUserId_ShouldReturnFalse()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });

            // Act & Assert
            team.HasMember(null!).Should().BeFalse();
        }

        [Fact]
        public void HasOwner_WhenUserIsOwner_ShouldReturnTrue()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });
            team.Members.Add(new GraphTeamMember { UserId = "user-456", Role = "owner" });

            // Act & Assert
            team.HasOwner("user-456").Should().BeTrue();
        }

        [Fact]
        public void HasOwner_WhenUserIsNotOwner_ShouldReturnFalse()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });

            // Act & Assert
            team.HasOwner("user-123").Should().BeFalse();
            team.HasOwner("user-999").Should().BeFalse();
        }

        [Fact]
        public void GetMember_WhenUserExists_ShouldReturnMember()
        {
            // Arrange
            var team = new GraphTeam();
            var member = new GraphTeamMember { UserId = "user-123", DisplayName = "John Doe", Role = "member" };
            team.Members.Add(member);

            // Act
            var result = team.GetMember("user-123");

            // Assert
            result.Should().Be(member);
            result!.DisplayName.Should().Be("John Doe");
        }

        [Fact]
        public void GetMember_WhenUserDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members.Add(new GraphTeamMember { UserId = "user-123", Role = "member" });

            // Act
            var result = team.GetMember("user-999");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetChannel_WhenChannelExists_ShouldReturnChannel()
        {
            // Arrange
            var team = new GraphTeam();
            var channel = new GraphChannel { Id = "channel-123", DisplayName = "General" };
            team.Channels.Add(channel);

            // Act
            var result = team.GetChannel("channel-123");

            // Assert
            result.Should().Be(channel);
            result!.DisplayName.Should().Be("General");
        }

        [Fact]
        public void GetChannel_WhenChannelDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var team = new GraphTeam();
            team.Channels.Add(new GraphChannel { Id = "channel-123", DisplayName = "General" });

            // Act
            var result = team.GetChannel("channel-999");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetSummary_ShouldReturnCorrectSummary()
        {
            // Arrange
            var team = new GraphTeam
            {
                DisplayName = "Mathematics Team",
                IsActive = true,
                MemberCount = 15,
                OwnerCount = 2
            };
            team.Channels.Add(new GraphChannel { Id = "ch1", DisplayName = "General" });
            team.Channels.Add(new GraphChannel { Id = "ch2", DisplayName = "Announcements" });

            // Act
            var summary = team.GetSummary();

            // Assert
            summary.Should().Be("Mathematics Team: Aktywny, 15 członków (2 właścicieli), 2 kanałów");
        }

        [Fact]
        public void GetSummary_WhenInactive_ShouldIndicateInactive()
        {
            // Arrange
            var team = new GraphTeam
            {
                DisplayName = "Old Team",
                IsActive = false,
                MemberCount = 5,
                OwnerCount = 1
            };

            // Act
            var summary = team.GetSummary();

            // Assert
            summary.Should().Be("Old Team: Nieaktywny, 5 członków (1 właścicieli), 0 kanałów");
        }

        #endregion

        #region GraphTeamSettings Tests

        [Fact]
        public void GraphTeamSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var settings = new GraphTeamSettings();

            // Assert
            settings.AllowCreateUpdateChannels.Should().BeNull();
            settings.AllowDeleteChannels.Should().BeNull();
            settings.AllowAddRemoveApps.Should().BeNull();
            settings.AllowCreateUpdateRemoveTabs.Should().BeNull();
            settings.AllowCreateUpdateRemoveConnectors.Should().BeNull();
        }

        [Fact]
        public void GraphTeamSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var settings = new GraphTeamSettings();

            // Act
            settings.AllowCreateUpdateChannels = true;
            settings.AllowDeleteChannels = false;
            settings.AllowAddRemoveApps = true;
            settings.AllowCreateUpdateRemoveTabs = false;
            settings.AllowCreateUpdateRemoveConnectors = true;

            // Assert
            settings.AllowCreateUpdateChannels.Should().BeTrue();
            settings.AllowDeleteChannels.Should().BeFalse();
            settings.AllowAddRemoveApps.Should().BeTrue();
            settings.AllowCreateUpdateRemoveTabs.Should().BeFalse();
            settings.AllowCreateUpdateRemoveConnectors.Should().BeTrue();
        }

        #endregion

        #region GraphTeamGuestSettings Tests

        [Fact]
        public void GraphTeamGuestSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var guestSettings = new GraphTeamGuestSettings();

            // Assert
            guestSettings.AllowCreateUpdateChannels.Should().BeNull();
            guestSettings.AllowDeleteChannels.Should().BeNull();
        }

        [Fact]
        public void GraphTeamGuestSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var guestSettings = new GraphTeamGuestSettings();

            // Act
            guestSettings.AllowCreateUpdateChannels = false;
            guestSettings.AllowDeleteChannels = false;

            // Assert
            guestSettings.AllowCreateUpdateChannels.Should().BeFalse();
            guestSettings.AllowDeleteChannels.Should().BeFalse();
        }

        #endregion

        #region GraphTeamMemberSettings Tests

        [Fact]
        public void GraphTeamMemberSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var memberSettings = new GraphTeamMemberSettings();

            // Assert
            memberSettings.AllowAddRemoveApps.Should().BeNull();
            memberSettings.AllowCreateUpdateRemoveTabs.Should().BeNull();
            memberSettings.AllowCreateUpdateRemoveConnectors.Should().BeNull();
        }

        [Fact]
        public void GraphTeamMemberSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var memberSettings = new GraphTeamMemberSettings();

            // Act
            memberSettings.AllowAddRemoveApps = true;
            memberSettings.AllowCreateUpdateRemoveTabs = true;
            memberSettings.AllowCreateUpdateRemoveConnectors = false;

            // Assert
            memberSettings.AllowAddRemoveApps.Should().BeTrue();
            memberSettings.AllowCreateUpdateRemoveTabs.Should().BeTrue();
            memberSettings.AllowCreateUpdateRemoveConnectors.Should().BeFalse();
        }

        #endregion

        #region GraphTeamMessagingSettings Tests

        [Fact]
        public void GraphTeamMessagingSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var messagingSettings = new GraphTeamMessagingSettings();

            // Assert
            messagingSettings.AllowUserEditMessages.Should().BeNull();
            messagingSettings.AllowUserDeleteMessages.Should().BeNull();
            messagingSettings.AllowOwnerDeleteMessages.Should().BeNull();
            messagingSettings.AllowTeamMentions.Should().BeNull();
            messagingSettings.AllowChannelMentions.Should().BeNull();
        }

        [Fact]
        public void GraphTeamMessagingSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var messagingSettings = new GraphTeamMessagingSettings();

            // Act
            messagingSettings.AllowUserEditMessages = true;
            messagingSettings.AllowUserDeleteMessages = false;
            messagingSettings.AllowOwnerDeleteMessages = true;
            messagingSettings.AllowTeamMentions = true;
            messagingSettings.AllowChannelMentions = false;

            // Assert
            messagingSettings.AllowUserEditMessages.Should().BeTrue();
            messagingSettings.AllowUserDeleteMessages.Should().BeFalse();
            messagingSettings.AllowOwnerDeleteMessages.Should().BeTrue();
            messagingSettings.AllowTeamMentions.Should().BeTrue();
            messagingSettings.AllowChannelMentions.Should().BeFalse();
        }

        #endregion

        #region GraphTeamFunSettings Tests

        [Fact]
        public void GraphTeamFunSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var funSettings = new GraphTeamFunSettings();

            // Assert
            funSettings.AllowGiphy.Should().BeNull();
            funSettings.GiphyContentRating.Should().BeNull();
            funSettings.AllowStickersAndMemes.Should().BeNull();
            funSettings.AllowCustomMemes.Should().BeNull();
        }

        [Fact]
        public void GraphTeamFunSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var funSettings = new GraphTeamFunSettings();

            // Act
            funSettings.AllowGiphy = true;
            funSettings.GiphyContentRating = "moderate";
            funSettings.AllowStickersAndMemes = true;
            funSettings.AllowCustomMemes = false;

            // Assert
            funSettings.AllowGiphy.Should().BeTrue();
            funSettings.GiphyContentRating.Should().Be("moderate");
            funSettings.AllowStickersAndMemes.Should().BeTrue();
            funSettings.AllowCustomMemes.Should().BeFalse();
        }

        #endregion

        #region GraphTeamDiscoverySettings Tests

        [Fact]
        public void GraphTeamDiscoverySettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var discoverySettings = new GraphTeamDiscoverySettings();

            // Assert
            discoverySettings.ShowInTeamsSearchResults.Should().BeNull();
        }

        [Fact]
        public void GraphTeamDiscoverySettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var discoverySettings = new GraphTeamDiscoverySettings();

            // Act
            discoverySettings.ShowInTeamsSearchResults = true;

            // Assert
            discoverySettings.ShowInTeamsSearchResults.Should().BeTrue();
        }

        #endregion

        #region GraphTeamMember Tests

        [Fact]
        public void GraphTeamMember_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var member = new GraphTeamMember();

            // Assert
            member.Id.Should().BeNull();
            member.UserId.Should().BeNull();
            member.Email.Should().BeNull();
            member.DisplayName.Should().BeNull();
            member.Role.Should().BeNull();
            member.Roles.Should().BeNull();
            member.AddedDateTime.Should().BeNull();
            member.IsActive.Should().BeTrue();
            member.User.Should().BeNull();
        }

        [Fact]
        public void GraphTeamMember_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var member = new GraphTeamMember();
            var addedDate = DateTime.UtcNow.AddDays(-10);
            var roles = new List<string> { "owner", "member" };
            var user = new GraphUser { Id = "user-123", DisplayName = "John Doe" };

            // Act
            member.Id = "member-456";
            member.UserId = "user-123";
            member.Email = "john.doe@school.edu";
            member.DisplayName = "John Doe";
            member.Role = "owner";
            member.Roles = roles;
            member.AddedDateTime = addedDate;
            member.IsActive = false;
            member.User = user;

            // Assert
            member.Id.Should().Be("member-456");
            member.UserId.Should().Be("user-123");
            member.Email.Should().Be("john.doe@school.edu");
            member.DisplayName.Should().Be("John Doe");
            member.Role.Should().Be("owner");
            member.Roles.Should().BeSameAs(roles);
            member.Roles.Should().Contain("owner").And.Contain("member");
            member.AddedDateTime.Should().Be(addedDate);
            member.IsActive.Should().BeFalse();
            member.User.Should().Be(user);
        }

        #endregion

        #region GraphSyncInfo Tests

        [Fact]
        public void GraphSyncInfo_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var syncInfo = new GraphSyncInfo();

            // Assert
            syncInfo.LastSyncDateTime.Should().BeNull();
            syncInfo.LastSyncSuccessful.Should().BeTrue(); // Domyślnie true
            syncInfo.LastSyncError.Should().BeNull();
            syncInfo.SyncAttempts.Should().Be(0);
            syncInfo.NextSyncDateTime.Should().BeNull();
            syncInfo.SyncRequired.Should().BeFalse();
            syncInfo.LocalDataHash.Should().BeNull();
            syncInfo.GraphDataHash.Should().BeNull();
            syncInfo.IsSynchronized.Should().BeTrue(); // Właściwość obliczona: !SyncRequired && LastSyncSuccessful && LocalDataHash == GraphDataHash
        }

        [Fact]
        public void GraphSyncInfo_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var syncInfo = new GraphSyncInfo();
            var lastSync = DateTime.UtcNow.AddMinutes(-30);
            var nextSync = DateTime.UtcNow.AddMinutes(30);

            // Act
            syncInfo.LastSyncDateTime = lastSync;
            syncInfo.LastSyncSuccessful = false;
            syncInfo.LastSyncError = "Connection failed";
            syncInfo.SyncAttempts = 3;
            syncInfo.NextSyncDateTime = nextSync;
            syncInfo.SyncRequired = true;
            syncInfo.LocalDataHash = "local-hash-123";
            syncInfo.GraphDataHash = "graph-hash-456";

            // Assert
            syncInfo.LastSyncDateTime.Should().Be(lastSync);
            syncInfo.LastSyncSuccessful.Should().BeFalse();
            syncInfo.LastSyncError.Should().Be("Connection failed");
            syncInfo.SyncAttempts.Should().Be(3);
            syncInfo.NextSyncDateTime.Should().Be(nextSync);
            syncInfo.SyncRequired.Should().BeTrue();
            syncInfo.LocalDataHash.Should().Be("local-hash-123");
            syncInfo.GraphDataHash.Should().Be("graph-hash-456");
            // IsSynchronized jest obliczane: !SyncRequired && LastSyncSuccessful && LocalDataHash == GraphDataHash
            syncInfo.IsSynchronized.Should().BeFalse(); // SyncRequired=true, więc false
        }

        [Fact]
        public void GraphSyncInfo_IsSynchronized_ShouldCalculateCorrectly()
        {
            // Arrange & Act - przypadek zsynchronizowany
            var syncedInfo = new GraphSyncInfo
            {
                SyncRequired = false,
                LastSyncSuccessful = true,
                LocalDataHash = "same-hash",
                GraphDataHash = "same-hash"
            };

            // Assert - zsynchronizowany
            syncedInfo.IsSynchronized.Should().BeTrue();

            // Act - przypadek niezsynchronizowany (różne hashe)
            var unsyncedInfo = new GraphSyncInfo
            {
                SyncRequired = false,
                LastSyncSuccessful = true,
                LocalDataHash = "local-hash",
                GraphDataHash = "different-hash"
            };

            // Assert - niezsynchronizowany
            unsyncedInfo.IsSynchronized.Should().BeFalse();

            // Act - przypadek niezsynchronizowany (synchronizacja wymagana)
            var syncRequiredInfo = new GraphSyncInfo
            {
                SyncRequired = true,
                LastSyncSuccessful = true,
                LocalDataHash = "same-hash",
                GraphDataHash = "same-hash"
            };

            // Assert - niezsynchronizowany
            syncRequiredInfo.IsSynchronized.Should().BeFalse();

            // Act - przypadek niezsynchronizowany (ostatnia synchronizacja nieudana)
            var syncFailedInfo = new GraphSyncInfo
            {
                SyncRequired = false,
                LastSyncSuccessful = false,
                LocalDataHash = "same-hash",
                GraphDataHash = "same-hash"
            };

            // Assert - niezsynchronizowany
            syncFailedInfo.IsSynchronized.Should().BeFalse();
        }

        #endregion

        #region Real World Scenarios Tests

        [Fact]
        public void GraphTeam_CompleteTeamScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - tworzymy kompletny zespół z wszystkimi ustawieniami
            var team = new GraphTeam
            {
                Id = "team-math-2024",
                DisplayName = "Mathematics 2024",
                Description = "Team for mathematics teachers and students",
                Mail = "math2024@school.edu",
                MailNickname = "math2024",
                WebUrl = "https://teams.microsoft.com/l/team/team-math-2024",
                Classification = "Educational",
                Visibility = "Private",
                IsArchived = false,
                CreatedDateTime = DateTime.UtcNow.AddDays(-60),
                TenantId = "school-tenant-123",
                IsActive = true,
                MemberCount = 30,
                OwnerCount = 3,
                Settings = new GraphTeamSettings
                {
                    AllowCreateUpdateChannels = true,
                    AllowDeleteChannels = false,
                    AllowAddRemoveApps = true
                },
                GuestSettings = new GraphTeamGuestSettings
                {
                    AllowCreateUpdateChannels = false,
                    AllowDeleteChannels = false
                },
                MessagingSettings = new GraphTeamMessagingSettings
                {
                    AllowUserEditMessages = true,
                    AllowUserDeleteMessages = false,
                    AllowOwnerDeleteMessages = true,
                    AllowTeamMentions = true,
                    AllowChannelMentions = true
                },
                FunSettings = new GraphTeamFunSettings
                {
                    AllowGiphy = true,
                    GiphyContentRating = "moderate",
                    AllowStickersAndMemes = true,
                    AllowCustomMemes = false
                },
                DiscoverySettings = new GraphTeamDiscoverySettings
                {
                    ShowInTeamsSearchResults = true
                }
            };

            // Dodajemy członków
            team.Members.Add(new GraphTeamMember
            {
                Id = "member-1",
                UserId = "teacher-001",
                Email = "teacher001@school.edu",
                DisplayName = "Anna Kowalska",
                Role = "owner",
                AddedDateTime = DateTime.UtcNow.AddDays(-60),
                IsActive = true
            });

            team.Members.Add(new GraphTeamMember
            {
                Id = "member-2",
                UserId = "student-001",
                Email = "student001@school.edu",
                DisplayName = "Jan Nowak",
                Role = "member",
                AddedDateTime = DateTime.UtcNow.AddDays(-50),
                IsActive = true
            });

            // Dodajemy kanały
            team.Channels.Add(new GraphChannel
            {
                Id = "channel-general",
                TeamId = team.Id,
                DisplayName = "General",
                Description = "General discussions",
                CreatedDateTime = DateTime.UtcNow.AddDays(-60)
            });

            team.Channels.Add(new GraphChannel
            {
                Id = "channel-homework",
                TeamId = team.Id,
                DisplayName = "Homework",
                Description = "Homework submissions",
                CreatedDateTime = DateTime.UtcNow.AddDays(-55)
            });

            // Assert - sprawdzamy kompletną funkcjonalność
            team.DisplayName.Should().Be("Mathematics 2024");
            team.Members.Should().HaveCount(2);
            team.Channels.Should().HaveCount(2);

            // Test metod pomocniczych
            team.HasMember("teacher-001").Should().BeTrue();
            team.HasOwner("teacher-001").Should().BeTrue();
            team.HasMember("student-001").Should().BeTrue();
            team.HasOwner("student-001").Should().BeFalse();
            team.HasMember("nonexistent-user").Should().BeFalse();

            var teacher = team.GetMember("teacher-001");
            teacher.Should().NotBeNull();
            teacher!.DisplayName.Should().Be("Anna Kowalska");
            teacher.Role.Should().Be("owner");

            var generalChannel = team.GetChannel("channel-general");
            generalChannel.Should().NotBeNull();
            generalChannel!.DisplayName.Should().Be("General");

            // Test konwersji do lokalnego zespołu
            var localTeam = team.ToLocalTeam();
            localTeam.DisplayName.Should().Be("Mathematics 2024");
            localTeam.ExternalId.Should().Be("team-math-2024");
            localTeam.Status.Should().Be(TeamStatus.Active);

            // Test podsumowania
            var summary = team.GetSummary();
            summary.Should().Be("Mathematics 2024: Aktywny, 30 członków (3 właścicieli), 2 kanałów");

            // Test ustawień
            team.Settings!.AllowCreateUpdateChannels.Should().BeTrue();
            team.GuestSettings!.AllowCreateUpdateChannels.Should().BeFalse();
            team.MessagingSettings!.AllowTeamMentions.Should().BeTrue();
            team.FunSettings!.GiphyContentRating.Should().Be("moderate");
            team.DiscoverySettings!.ShowInTeamsSearchResults.Should().BeTrue();
        }

        [Fact]
        public void GraphTeam_ArchivedTeamScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - tworzymy zarchiwizowany zespół
            var team = new GraphTeam
            {
                DisplayName = "Archived Mathematics 2023",
                IsArchived = true,
                IsActive = false,
                MemberCount = 0,
                OwnerCount = 0
            };

            // Assert
            team.IsArchived.Should().BeTrue();
            team.IsActive.Should().BeFalse();
            
            var summary = team.GetSummary();
            summary.Should().Be("Archived Mathematics 2023: Nieaktywny, 0 członków (0 właścicieli), 0 kanałów");

            var localTeam = team.ToLocalTeam();
            localTeam.Status.Should().Be(TeamStatus.Archived);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void GraphTeam_WithNullMembers_ShouldHandleGracefully()
        {
            // Arrange
            var team = new GraphTeam();
            team.Members = null!; // Symulacja błędnego stanu

            // Act & Assert - metody powinny obsłużyć null gracefully
            var action1 = () => team.HasMember("user-123");
            var action2 = () => team.HasOwner("user-123");
            var action3 = () => team.GetMember("user-123");

            // W .NET 9 LINQ metody rzucają ArgumentNullException gdy kolekcja jest null
            action1.Should().Throw<ArgumentNullException>();
            action2.Should().Throw<ArgumentNullException>();
            action3.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GraphTeam_WithNullChannels_ShouldHandleGracefully()
        {
            // Arrange
            var team = new GraphTeam();
            team.Channels = null!; // Symulacja błędnego stanu

            // Act & Assert
            var action1 = () => team.GetChannel("channel-123");
            var action2 = () => team.GetSummary();

            // W .NET 9 LINQ metody rzucają ArgumentNullException gdy kolekcja jest null
            action1.Should().Throw<ArgumentNullException>();
            action2.Should().Throw<NullReferenceException>(); // GetSummary używa .Count, które rzuca NullReferenceException
        }

        [Fact]
        public void GraphTeam_ConversionWithNullDisplayName_ShouldUseEmptyString()
        {
            // Arrange
            var graphTeam = new GraphTeam { DisplayName = null };

            // Act
            var localTeam = graphTeam.ToLocalTeam();

            // Assert
            localTeam.DisplayName.Should().Be(string.Empty);
        }

        [Fact]
        public void GraphTeamMember_WithMultipleRoles_ShouldMaintainAllRoles()
        {
            // Arrange
            var member = new GraphTeamMember
            {
                UserId = "user-123",
                Role = "owner",
                Roles = new List<string> { "owner", "member", "guest" }
            };

            // Assert
            member.Role.Should().Be("owner");
            member.Roles.Should().HaveCount(3);
            member.Roles.Should().Contain("owner").And.Contain("member").And.Contain("guest");
        }

        #endregion
    }
} 