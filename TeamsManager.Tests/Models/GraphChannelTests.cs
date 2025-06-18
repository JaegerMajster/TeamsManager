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
    /// Testy jednostkowe dla GraphChannel i wszystkich powiązanych klas
    /// Pokrycie: GraphChannel, GraphChannelSettings, GraphChannelStats, GraphChannelMember, GraphChannelTab
    /// </summary>
    public class GraphChannelTests
    {
        #region GraphChannel Constructor Tests

        [Fact]
        public void GraphChannel_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var channel = new GraphChannel();

            // Assert - podstawowe właściwości
            channel.Id.Should().BeNull();
            channel.TeamId.Should().BeNull();
            channel.DisplayName.Should().BeNull();
            channel.Description.Should().BeNull();
            channel.Email.Should().BeNull();
            channel.WebUrl.Should().BeNull();
            channel.ETag.Should().BeNull();
            channel.CreatedDateTime.Should().BeNull();
            channel.TenantId.Should().BeNull();
            channel.MembershipType.Should().BeNull();
            channel.IsActive.Should().BeTrue();
            channel.IsGeneral.Should().BeFalse();
            channel.IsReadOnly.Should().BeFalse();
            channel.IsFavoriteByDefault.Should().BeFalse();

            // Assert - obiekty pomocnicze
            channel.Settings.Should().BeNull();
            channel.Stats.Should().BeNull();

            // Assert - kolekcje
            channel.Members.Should().NotBeNull().And.BeEmpty();
            channel.Tabs.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void GraphChannel_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var channel = new GraphChannel();
            var createdDate = DateTime.UtcNow.AddDays(-30);
            var settings = new GraphChannelSettings { AllowChannelMentions = true };
            var stats = new GraphChannelStats { MessageCount = 100 };
            var member = new GraphChannelMember { Id = "member-1", DisplayName = "Jan Kowalski" };
            var tab = new GraphChannelTab { Id = "tab-1", DisplayName = "Wiki" };

            // Act
            channel.Id = "channel-123";
            channel.TeamId = "team-456";
            channel.DisplayName = "Historia";
            channel.Description = "Kanał dla nauczycieli historii";
            channel.Email = "historia@school.edu";
            channel.WebUrl = "https://teams.microsoft.com/channel/123";
            channel.ETag = "etag-abc123";
            channel.CreatedDateTime = createdDate;
            channel.TenantId = "tenant-789";
            channel.MembershipType = "private";
            channel.IsActive = false;
            channel.IsGeneral = true;
            channel.IsReadOnly = true;
            channel.IsFavoriteByDefault = true;
            channel.Settings = settings;
            channel.Stats = stats;
            channel.Members.Add(member);
            channel.Tabs.Add(tab);

            // Assert
            channel.Id.Should().Be("channel-123");
            channel.TeamId.Should().Be("team-456");
            channel.DisplayName.Should().Be("Historia");
            channel.Description.Should().Be("Kanał dla nauczycieli historii");
            channel.Email.Should().Be("historia@school.edu");
            channel.WebUrl.Should().Be("https://teams.microsoft.com/channel/123");
            channel.ETag.Should().Be("etag-abc123");
            channel.CreatedDateTime.Should().Be(createdDate);
            channel.TenantId.Should().Be("tenant-789");
            channel.MembershipType.Should().Be("private");
            channel.IsActive.Should().BeFalse();
            channel.IsGeneral.Should().BeTrue();
            channel.IsReadOnly.Should().BeTrue();
            channel.IsFavoriteByDefault.Should().BeTrue();
            channel.Settings.Should().Be(settings);
            channel.Stats.Should().Be(stats);
            channel.Members.Should().Contain(member);
            channel.Tabs.Should().Contain(tab);
        }

        #endregion

        #region GraphChannel Computed Properties Tests

        [Fact]
        public void GraphChannel_IsPrivate_ShouldCalculateCorrectly()
        {
            // Arrange & Act - kanał publiczny
            var publicChannel = new GraphChannel { MembershipType = "standard" };
            publicChannel.IsPrivate.Should().BeFalse();

            // Act - kanał prywatny
            var privateChannel = new GraphChannel { MembershipType = "private" };
            privateChannel.IsPrivate.Should().BeTrue();

            // Act - kanał z nieznanym typem
            var unknownChannel = new GraphChannel { MembershipType = "unknownFutureValue" };
            unknownChannel.IsPrivate.Should().BeFalse();

            // Act - kanał bez ustawionego typu
            var noTypeChannel = new GraphChannel { MembershipType = null };
            noTypeChannel.IsPrivate.Should().BeFalse();
        }

        #endregion

        #region GraphChannel Methods Tests

        [Fact]
        public void HasMember_WhenUserIsMember_ShouldReturnTrue()
        {
            // Arrange
            var channel = new GraphChannel();
            channel.Members.Add(new GraphChannelMember { UserId = "user-123" });
            channel.Members.Add(new GraphChannelMember { UserId = "user-456" });

            // Act & Assert
            channel.HasMember("user-123").Should().BeTrue();
            channel.HasMember("user-456").Should().BeTrue();
        }

        [Fact]
        public void HasMember_WhenUserIsNotMember_ShouldReturnFalse()
        {
            // Arrange
            var channel = new GraphChannel();
            channel.Members.Add(new GraphChannelMember { UserId = "user-123" });

            // Act & Assert
            channel.HasMember("user-999").Should().BeFalse();
        }

        [Fact]
        public void GetMember_WhenUserExists_ShouldReturnMember()
        {
            // Arrange
            var channel = new GraphChannel();
            var member = new GraphChannelMember { UserId = "user-123", DisplayName = "Jan Kowalski" };
            channel.Members.Add(member);

            // Act
            var result = channel.GetMember("user-123");

            // Assert
            result.Should().Be(member);
            result!.DisplayName.Should().Be("Jan Kowalski");
        }

        [Fact]
        public void GetMember_WhenUserDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var channel = new GraphChannel();
            channel.Members.Add(new GraphChannelMember { UserId = "user-123" });

            // Act
            var result = channel.GetMember("user-999");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetTab_WhenTabExists_ShouldReturnTab()
        {
            // Arrange
            var channel = new GraphChannel();
            var tab = new GraphChannelTab { Id = "tab-123", DisplayName = "Wiki" };
            channel.Tabs.Add(tab);

            // Act
            var result = channel.GetTab("tab-123");

            // Assert
            result.Should().Be(tab);
            result!.DisplayName.Should().Be("Wiki");
        }

        [Fact]
        public void GetTab_WhenTabDoesNotExist_ShouldReturnNull()
        {
            // Arrange
            var channel = new GraphChannel();
            channel.Tabs.Add(new GraphChannelTab { Id = "tab-123" });

            // Act
            var result = channel.GetTab("tab-999");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void CanBeDeleted_WhenGeneralChannel_ShouldReturnFalse()
        {
            // Arrange
            var channel = new GraphChannel { IsGeneral = true };

            // Act & Assert
            channel.CanBeDeleted().Should().BeFalse();
        }

        [Fact]
        public void CanBeDeleted_WhenNotGeneralChannel_ShouldReturnTrue()
        {
            // Arrange
            var channel = new GraphChannel { IsGeneral = false };

            // Act & Assert
            channel.CanBeDeleted().Should().BeTrue();
        }

        [Fact]
        public void GetDeletionBlockReason_WhenGeneralChannel_ShouldReturnReason()
        {
            // Arrange
            var channel = new GraphChannel { IsGeneral = true };

            // Act
            var reason = channel.GetDeletionBlockReason();

            // Assert
            reason.Should().Be("Kanał ogólny nie może być usunięty");
        }

        [Fact]
        public void GetDeletionBlockReason_WhenNotGeneralChannel_ShouldReturnNull()
        {
            // Arrange
            var channel = new GraphChannel { IsGeneral = false };

            // Act
            var reason = channel.GetDeletionBlockReason();

            // Assert
            reason.Should().BeNull();
        }

        [Fact]
        public void GetSummary_ShouldReturnCorrectSummary()
        {
            // Arrange
            var channel = new GraphChannel
            {
                DisplayName = "Historia",
                MembershipType = "private",
                IsActive = true
            };
            channel.Members.Add(new GraphChannelMember { UserId = "user-1" });
            channel.Members.Add(new GraphChannelMember { UserId = "user-2" });
            channel.Tabs.Add(new GraphChannelTab { Id = "tab-1" });

            // Act
            var summary = channel.GetSummary();

            // Assert
            summary.Should().Be("Historia: Prywatny, Aktywny, 2 członków, 1 kart");
        }

        [Fact]
        public void GetSummary_WhenPublicChannel_ShouldShowAllMembers()
        {
            // Arrange
            var channel = new GraphChannel
            {
                DisplayName = "Ogólny",
                MembershipType = "standard",
                IsActive = false
            };

            // Act
            var summary = channel.GetSummary();

            // Assert
            summary.Should().Be("Ogólny: Publiczny, Nieaktywny, Wszyscy członkowie zespołu, 0 kart");
        }

        [Fact]
        public void GetDetailedInfo_ShouldReturnCompleteInformation()
        {
            // Arrange
            var channel = new GraphChannel
            {
                Id = "channel-123",
                TeamId = "team-456",
                DisplayName = "Historia",
                Description = "Kanał dla nauczycieli historii",
                MembershipType = "private",
                IsActive = true,
                IsGeneral = false,
                IsReadOnly = false,
                CreatedDateTime = new DateTime(2024, 1, 15, 10, 30, 0),
                WebUrl = "https://teams.microsoft.com/channel/123",
                Email = "historia@school.edu",
                Stats = new GraphChannelStats
                {
                    MessageCount = 150,
                    FilesCount = 25,
                    FilesSize = 1048576,
                    LastActivityDate = new DateTime(2024, 6, 10, 14, 30, 0),
                    LastMessageDate = new DateTime(2024, 6, 10, 14, 25, 0)
                }
            };
            channel.Members.Add(new GraphChannelMember { DisplayName = "Jan Kowalski", Role = "owner" });
            channel.Members.Add(new GraphChannelMember { DisplayName = "Anna Nowak", Role = "member" });
            channel.Tabs.Add(new GraphChannelTab { DisplayName = "Wiki", TeamsAppId = "wiki-app" });

            // Act
            var info = channel.GetDetailedInfo();

            // Assert
            info.Should().Contain("Nazwa: Historia");
            info.Should().Contain("Opis: Kanał dla nauczycieli historii");
            info.Should().Contain("ID: channel-123");
            info.Should().Contain("ID zespołu: team-456");
            info.Should().Contain("Typ: Prywatny");
            info.Should().Contain("Status: Aktywny");
            info.Should().Contain("Kanał ogólny: Nie");
            info.Should().Contain("Tylko odczyt: Nie");
            info.Should().Contain("Utworzony: 2024-01-15 10:30");
            info.Should().Contain("URL: https://teams.microsoft.com/channel/123");
            info.Should().Contain("Email: historia@school.edu");
            info.Should().Contain("=== STATYSTYKI ===");
            info.Should().Contain("Liczba wiadomości: 150");
            info.Should().Contain("Liczba plików: 25");
            info.Should().Contain("Rozmiar plików: 1048576 bajtów");
            info.Should().Contain("Ostatnia aktywność: 2024-06-10 14:30");
            info.Should().Contain("Ostatnia wiadomość: 2024-06-10 14:25");
            info.Should().Contain("=== CZŁONKOWIE (2) ===");
            info.Should().Contain("• Jan Kowalski (owner)");
            info.Should().Contain("• Anna Nowak (member)");
            info.Should().Contain("=== KARTY (1) ===");
            info.Should().Contain("• Wiki (wiki-app)");
        }

        #endregion

        #region GraphChannel Conversion Methods Tests

        [Fact]
        public void ToLocalChannel_ShouldConvertToLocalChannelCorrectly()
        {
            // Arrange
            var graphChannel = new GraphChannel
            {
                DisplayName = "Historia",
                Description = "Kanał dla nauczycieli historii",
                TeamId = "team-456",
                IsGeneral = true,
                MembershipType = "private",
                IsReadOnly = true,
                IsActive = true,
                CreatedDateTime = DateTime.UtcNow.AddDays(-30),
                Stats = new GraphChannelStats
                {
                    MessageCount = 100,
                    FilesCount = 15,
                    FilesSize = 512000,
                    LastActivityDate = DateTime.UtcNow.AddDays(-1),
                    LastMessageDate = DateTime.UtcNow.AddHours(-2)
                }
            };

            // Act
            var localChannel = graphChannel.ToLocalChannel();

            // Assert
            localChannel.Should().NotBeNull();
            localChannel.DisplayName.Should().Be("Historia");
            localChannel.Description.Should().Be("Kanał dla nauczycieli historii");
            localChannel.TeamId.Should().Be("team-456");
            localChannel.IsGeneral.Should().BeTrue();
            localChannel.IsPrivate.Should().BeTrue();
            localChannel.IsReadOnly.Should().BeTrue();
            localChannel.Status.Should().Be(ChannelStatus.Active);
            localChannel.MessageCount.Should().Be(100);
            localChannel.FilesCount.Should().Be(15);
            localChannel.FilesSize.Should().Be(512000);
            localChannel.LastActivityDate.Should().Be(graphChannel.Stats.LastActivityDate);
            localChannel.LastMessageDate.Should().Be(graphChannel.Stats.LastMessageDate);
        }

        [Fact]
        public void ToLocalChannel_WhenInactiveGraphChannel_ShouldSetArchivedStatus()
        {
            // Arrange
            var graphChannel = new GraphChannel
            {
                DisplayName = "Archived Channel",
                IsActive = false
            };

            // Act
            var localChannel = graphChannel.ToLocalChannel();

            // Assert
            localChannel.Status.Should().Be(ChannelStatus.Archived);
        }

        [Fact]
        public void FromLocalChannel_ShouldConvertFromLocalChannelCorrectly()
        {
            // Arrange
            var localChannel = new Channel
            {
                Id = "channel-123",
                DisplayName = "Geografia",
                Description = "Kanał dla nauczycieli geografii",
                TeamId = "team-789",
                IsGeneral = false,
                IsPrivate = false,
                IsReadOnly = false,
                Status = ChannelStatus.Active,
                CreatedDate = DateTime.UtcNow.AddDays(-60),
                MessageCount = 200,
                FilesCount = 30,
                FilesSize = 1024000,
                LastActivityDate = DateTime.UtcNow.AddDays(-3),
                LastMessageDate = DateTime.UtcNow.AddHours(-5)
            };

            // Act
            var graphChannel = GraphChannel.FromLocalChannel(localChannel);

            // Assert
            graphChannel.Should().NotBeNull();
            graphChannel.DisplayName.Should().Be("Geografia");
            graphChannel.Description.Should().Be("Kanał dla nauczycieli geografii");
            graphChannel.TeamId.Should().Be("team-789");
            graphChannel.IsGeneral.Should().BeFalse();
            graphChannel.IsReadOnly.Should().BeFalse();
            graphChannel.IsActive.Should().BeTrue(); // Channel.IsActive jest true domyślnie
            graphChannel.MembershipType.Should().Be("standard"); // IsPrivate = false
            graphChannel.CreatedDateTime.Should().Be(localChannel.CreatedDate);
            graphChannel.Stats.Should().NotBeNull();
            graphChannel.Stats!.MessageCount.Should().Be(200);
            graphChannel.Stats.FilesCount.Should().Be(30);
            graphChannel.Stats.FilesSize.Should().Be(1024000);
            graphChannel.Stats.LastActivityDate.Should().Be(localChannel.LastActivityDate);
            graphChannel.Stats.LastMessageDate.Should().Be(localChannel.LastMessageDate);
        }

        [Fact]
        public void FromLocalChannel_WhenPrivateLocalChannel_ShouldSetPrivateMembershipType()
        {
            // Arrange
            var localChannel = new Channel
            {
                DisplayName = "Private Channel",
                IsPrivate = true
            };

            // Act
            var graphChannel = GraphChannel.FromLocalChannel(localChannel);

            // Assert
            graphChannel.MembershipType.Should().Be("private");
            graphChannel.IsPrivate.Should().BeTrue();
        }

        #endregion

        #region GraphChannelSettings Tests

        [Fact]
        public void GraphChannelSettings_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var settings = new GraphChannelSettings();

            // Assert
            settings.AllowNewMessageFromBots.Should().BeNull();
            settings.AllowNewMessageFromConnectors.Should().BeNull();
            settings.AllowChannelMentions.Should().BeNull();
            settings.NotificationSettings.Should().BeNull();
            settings.IsModerationEnabled.Should().BeNull();
            settings.Category.Should().BeNull();
            settings.Tags.Should().BeNull();
            settings.SortOrder.Should().BeNull();
            settings.AdditionalSettings.Should().BeNull();
        }

        [Fact]
        public void GraphChannelSettings_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var settings = new GraphChannelSettings();
            var tags = new List<string> { "tag1", "tag2" };
            var additionalSettings = new Dictionary<string, object> { { "key", "value" } };

            // Act
            settings.AllowNewMessageFromBots = true;
            settings.AllowNewMessageFromConnectors = false;
            settings.AllowChannelMentions = true;
            settings.NotificationSettings = new { enabled = true };
            settings.IsModerationEnabled = true;
            settings.Category = "Education";
            settings.Tags = tags;
            settings.SortOrder = 5;
            settings.AdditionalSettings = additionalSettings;

            // Assert
            settings.AllowNewMessageFromBots.Should().BeTrue();
            settings.AllowNewMessageFromConnectors.Should().BeFalse();
            settings.AllowChannelMentions.Should().BeTrue();
            settings.NotificationSettings.Should().NotBeNull();
            settings.IsModerationEnabled.Should().BeTrue();
            settings.Category.Should().Be("Education");
            settings.Tags.Should().BeEquivalentTo(tags);
            settings.SortOrder.Should().Be(5);
            settings.AdditionalSettings.Should().BeEquivalentTo(additionalSettings);
        }

        #endregion

        #region GraphChannelStats Tests

        [Fact]
        public void GraphChannelStats_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var stats = new GraphChannelStats();

            // Assert
            stats.MessageCount.Should().Be(0);
            stats.FilesCount.Should().Be(0);
            stats.FilesSize.Should().Be(0);
            stats.LastActivityDate.Should().BeNull();
            stats.LastMessageDate.Should().BeNull();
            stats.ActiveMembersCount.Should().Be(0);
            stats.UsageStats.Should().BeNull();
        }

        [Fact]
        public void GraphChannelStats_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var stats = new GraphChannelStats();
            var lastActivity = DateTime.UtcNow.AddDays(-1);
            var lastMessage = DateTime.UtcNow.AddHours(-2);
            var usageStats = new Dictionary<string, object> { { "views", 100 } };

            // Act
            stats.MessageCount = 250;
            stats.FilesCount = 40;
            stats.FilesSize = 2048000;
            stats.LastActivityDate = lastActivity;
            stats.LastMessageDate = lastMessage;
            stats.ActiveMembersCount = 15;
            stats.UsageStats = usageStats;

            // Assert
            stats.MessageCount.Should().Be(250);
            stats.FilesCount.Should().Be(40);
            stats.FilesSize.Should().Be(2048000);
            stats.LastActivityDate.Should().Be(lastActivity);
            stats.LastMessageDate.Should().Be(lastMessage);
            stats.ActiveMembersCount.Should().Be(15);
            stats.UsageStats.Should().BeEquivalentTo(usageStats);
        }

        #endregion

        #region GraphChannelMember Tests

        [Fact]
        public void GraphChannelMember_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var member = new GraphChannelMember();

            // Assert
            member.Id.Should().BeNull();
            member.UserId.Should().BeNull();
            member.Email.Should().BeNull();
            member.DisplayName.Should().BeNull();
            member.Role.Should().BeNull();
            member.AddedDateTime.Should().BeNull();
            member.IsActive.Should().BeTrue();
            member.User.Should().BeNull();
        }

        [Fact]
        public void GraphChannelMember_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var member = new GraphChannelMember();
            var addedDate = DateTime.UtcNow.AddDays(-10);
            var user = new GraphUser { Id = "user-123", GivenName = "Jan" };

            // Act
            member.Id = "member-123";
            member.UserId = "user-456";
            member.Email = "jan.kowalski@school.edu";
            member.DisplayName = "Jan Kowalski";
            member.Role = "owner";
            member.AddedDateTime = addedDate;
            member.IsActive = false;
            member.User = user;

            // Assert
            member.Id.Should().Be("member-123");
            member.UserId.Should().Be("user-456");
            member.Email.Should().Be("jan.kowalski@school.edu");
            member.DisplayName.Should().Be("Jan Kowalski");
            member.Role.Should().Be("owner");
            member.AddedDateTime.Should().Be(addedDate);
            member.IsActive.Should().BeFalse();
            member.User.Should().Be(user);
        }

        #endregion

        #region GraphChannelTab Tests

        [Fact]
        public void GraphChannelTab_WhenCreated_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var tab = new GraphChannelTab();

            // Assert
            tab.Id.Should().BeNull();
            tab.DisplayName.Should().BeNull();
            tab.WebUrl.Should().BeNull();
            tab.Configuration.Should().BeNull();
            tab.TeamsAppId.Should().BeNull();
            tab.SortOrderIndex.Should().BeNull();
            tab.IsActive.Should().BeTrue();
            tab.CreatedDateTime.Should().BeNull();
            tab.Metadata.Should().BeNull();
        }

        [Fact]
        public void GraphChannelTab_WhenSettingProperties_ShouldRetainValues()
        {
            // Arrange
            var tab = new GraphChannelTab();
            var createdDate = DateTime.UtcNow.AddDays(-5);
            var metadata = new Dictionary<string, object> { { "type", "wiki" } };

            // Act
            tab.Id = "tab-123";
            tab.DisplayName = "Wiki";
            tab.WebUrl = "https://teams.microsoft.com/tab/123";
            tab.Configuration = "wiki-config";
            tab.TeamsAppId = "wiki-app-id";
            tab.SortOrderIndex = 2;
            tab.IsActive = false;
            tab.CreatedDateTime = createdDate;
            tab.Metadata = metadata;

            // Assert
            tab.Id.Should().Be("tab-123");
            tab.DisplayName.Should().Be("Wiki");
            tab.WebUrl.Should().Be("https://teams.microsoft.com/tab/123");
            tab.Configuration.Should().Be("wiki-config");
            tab.TeamsAppId.Should().Be("wiki-app-id");
            tab.SortOrderIndex.Should().Be(2);
            tab.IsActive.Should().BeFalse();
            tab.CreatedDateTime.Should().Be(createdDate);
            tab.Metadata.Should().BeEquivalentTo(metadata);
        }

        #endregion

        #region Real World Scenarios Tests

        [Fact]
        public void GraphChannel_CompleteChannelScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - tworzymy kompletny kanał prywatny
            var channel = new GraphChannel
            {
                Id = "ch-historia-101",
                TeamId = "team-szkola-podstawowa",
                DisplayName = "Historia - Klasa 8A",
                Description = "Kanał dla uczniów klasy 8A na przedmiot historia",
                Email = "historia8a@school.edu",
                WebUrl = "https://teams.microsoft.com/l/channel/19%3a...@thread.tacv2/Historia%2520-%2520Klasa%25208A",
                ETag = "\"1234567890\"",
                CreatedDateTime = DateTime.UtcNow.AddDays(-90),
                TenantId = "tenant-school-edu",
                MembershipType = "private",
                IsActive = true,
                IsGeneral = false,
                IsReadOnly = false,
                IsFavoriteByDefault = false
            };

            // Dodajemy ustawienia
            channel.Settings = new GraphChannelSettings
            {
                AllowNewMessageFromBots = true,
                AllowNewMessageFromConnectors = false,
                AllowChannelMentions = true,
                IsModerationEnabled = false,
                Category = "Przedmioty",
                Tags = new List<string> { "Historia", "8A", "Humanistyczne" },
                SortOrder = 3
            };

            // Dodajemy statystyki
            channel.Stats = new GraphChannelStats
            {
                MessageCount = 387,
                FilesCount = 42,
                FilesSize = 15728640, // 15 MB
                LastActivityDate = DateTime.UtcNow.AddHours(-3),
                LastMessageDate = DateTime.UtcNow.AddHours(-4),
                ActiveMembersCount = 28
            };

            // Dodajemy członków
            channel.Members.Add(new GraphChannelMember
            {
                Id = "member-nauczyciel",
                UserId = "user-anna-kowalska",
                Email = "anna.kowalska@school.edu",
                DisplayName = "Anna Kowalska",
                Role = "owner",
                AddedDateTime = DateTime.UtcNow.AddDays(-90),
                IsActive = true
            });

            channel.Members.Add(new GraphChannelMember
            {
                Id = "member-uczen1",
                UserId = "user-jan-nowak",
                Email = "jan.nowak@student.school.edu",
                DisplayName = "Jan Nowak",
                Role = "member",
                AddedDateTime = DateTime.UtcNow.AddDays(-85),
                IsActive = true
            });

            // Dodajemy karty
            channel.Tabs.Add(new GraphChannelTab
            {
                Id = "tab-wiki",
                DisplayName = "Materiały do historii",
                WebUrl = "https://teams.microsoft.com/l/entity/wiki-app",
                TeamsAppId = "com.microsoft.teamspace.tab.wiki",
                SortOrderIndex = 0,
                IsActive = true,
                CreatedDateTime = DateTime.UtcNow.AddDays(-80)
            });

            channel.Tabs.Add(new GraphChannelTab
            {
                Id = "tab-planer",
                DisplayName = "Planer lekcji",
                WebUrl = "https://teams.microsoft.com/l/entity/planner-app",
                TeamsAppId = "com.microsoft.teamspace.tab.planner",
                SortOrderIndex = 1,
                IsActive = true,
                CreatedDateTime = DateTime.UtcNow.AddDays(-70)
            });

            // Assert - sprawdzamy kompletną funkcjonalność
            channel.Id.Should().Be("ch-historia-101");
            channel.DisplayName.Should().Be("Historia - Klasa 8A");
            channel.IsPrivate.Should().BeTrue(); // obliczane z MembershipType
            channel.IsGeneral.Should().BeFalse();
            channel.IsActive.Should().BeTrue();

            // Test metod członkostwa
            channel.HasMember("user-anna-kowalska").Should().BeTrue();
            channel.HasMember("user-jan-nowak").Should().BeTrue();
            channel.HasMember("user-nieistniejacy").Should().BeFalse();

            var teacher = channel.GetMember("user-anna-kowalska");
            teacher.Should().NotBeNull();
            teacher!.Role.Should().Be("owner");
            teacher.DisplayName.Should().Be("Anna Kowalska");

            // Test metod kart
            var wiki = channel.GetTab("tab-wiki");
            wiki.Should().NotBeNull();
            wiki!.DisplayName.Should().Be("Materiały do historii");

            // Test możliwości usunięcia
            channel.CanBeDeleted().Should().BeTrue(); // nie jest kanałem ogólnym
            channel.GetDeletionBlockReason().Should().BeNull();

            // Test konwersji do lokalnego kanału
            var localChannel = channel.ToLocalChannel();
            localChannel.DisplayName.Should().Be("Historia - Klasa 8A");
            localChannel.IsPrivate.Should().BeTrue();
            localChannel.MessageCount.Should().Be(387);
            localChannel.FilesCount.Should().Be(42);
            localChannel.Status.Should().Be(ChannelStatus.Active);

            // Test podsumowania
            var summary = channel.GetSummary();
            summary.Should().Be("Historia - Klasa 8A: Prywatny, Aktywny, 2 członków, 2 kart");

            // Test szczegółowych informacji
            var detailedInfo = channel.GetDetailedInfo();
            detailedInfo.Should().Contain("Nazwa: Historia - Klasa 8A");
            detailedInfo.Should().Contain("Typ: Prywatny");
            detailedInfo.Should().Contain("Liczba wiadomości: 387");
            detailedInfo.Should().Contain("Rozmiar plików: 15728640 bajtów");
            detailedInfo.Should().Contain("• Anna Kowalska (owner)");
            detailedInfo.Should().Contain("• Jan Nowak (member)");
            detailedInfo.Should().Contain("• Materiały do historii (com.microsoft.teamspace.tab.wiki)");
            detailedInfo.Should().Contain("• Planer lekcji (com.microsoft.teamspace.tab.planner)");
        }

        [Fact]
        public void GraphChannel_GeneralChannelScenario_ShouldWorkCorrectly()
        {
            // Arrange & Act - kanał ogólny zespołu
            var channel = new GraphChannel
            {
                DisplayName = "Ogólny",
                MembershipType = "standard",
                IsGeneral = true,
                IsFavoriteByDefault = true,
                IsActive = true
            };

            // Assert
            channel.IsPrivate.Should().BeFalse();
            channel.CanBeDeleted().Should().BeFalse(); // kanał ogólny nie może być usunięty
            channel.GetDeletionBlockReason().Should().Be("Kanał ogólny nie może być usunięty");

            var summary = channel.GetSummary();
            summary.Should().Be("Ogólny: Publiczny, Aktywny, Wszyscy członkowie zespołu, 0 kart");
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void GraphChannel_WithManyMembers_ShouldLimitDetailedInfoDisplay()
        {
            // Arrange
            var channel = new GraphChannel { DisplayName = "Duży kanał" };

            // Dodajemy 15 członków
            for (int i = 1; i <= 15; i++)
            {
                channel.Members.Add(new GraphChannelMember
                {
                    UserId = $"user-{i}",
                    DisplayName = $"Użytkownik {i}",
                    Role = "member"
                });
            }

            // Act
            var detailedInfo = channel.GetDetailedInfo();

            // Assert
            detailedInfo.Should().Contain("=== CZŁONKOWIE (15) ===");
            detailedInfo.Should().Contain("• Użytkownik 1 (member)");
            detailedInfo.Should().Contain("• Użytkownik 10 (member)");
            detailedInfo.Should().Contain("... i 5 więcej"); // Pokazuje tylko 10, reszta w "więcej"
        }

        [Fact]
        public void GraphChannel_WithNullProperties_ShouldHandleGracefully()
        {
            // Arrange
            var channel = new GraphChannel
            {
                DisplayName = null,
                Description = null,
                MembershipType = null,
                Settings = null,
                Stats = null
            };

            // Act & Assert
            channel.IsPrivate.Should().BeFalse(); // null != "private"
            channel.CanBeDeleted().Should().BeTrue(); // nie jest ogólnym

            var summary = channel.GetSummary();
            summary.Should().Be(": Publiczny, Aktywny, Wszyscy członkowie zespołu, 0 kart");

            var localChannel = channel.ToLocalChannel();
            localChannel.DisplayName.Should().Be(string.Empty);
            localChannel.Description.Should().Be(string.Empty);
            localChannel.MessageCount.Should().Be(0); // z null Stats
        }

        [Fact]
        public void GraphChannel_WithEmptyCollections_ShouldHandleGracefully()
        {
            // Arrange
            var channel = new GraphChannel();

            // Act & Assert
            channel.Members.Should().BeEmpty();
            channel.Tabs.Should().BeEmpty();
            channel.HasMember("any-user").Should().BeFalse();
            channel.GetMember("any-user").Should().BeNull();
            channel.GetTab("any-tab").Should().BeNull();
        }

        #endregion
    }
} 