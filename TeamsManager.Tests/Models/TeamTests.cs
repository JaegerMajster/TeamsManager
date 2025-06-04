using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class TeamTests
    {
        // Metody pomocnicze do tworzenia obiektów
        private User CreateTestUser(string id = "user-1", string upn = "test.user@example.com", bool isActive = true)
        {
            // Założenie: CreatedBy i CreatedDate są ustawiane gdzie indziej (np. przez DbContext lub w BaseEntity)
            // Dla celów tych testów jednostkowych modelu, możemy je pominąć lub ustawić na wartości domyślne,
            // jeśli nie wpływają bezpośrednio na testowaną logikę Team.
            return new User { Id = id, UPN = upn, FirstName = "Test", LastName = "User", IsActive = isActive, Role = UserRole.Nauczyciel };
        }

        private TeamMember CreateTestTeamMember(User user, Team team, TeamMemberRole role = TeamMemberRole.Member, string memberId = "member-1", bool isActive = true)
        {
            // Podobnie jak w CreateTestUser, pomijamy CreatedBy/CreatedDate dla uproszczenia testów modelu.
            return new TeamMember
            {
                Id = memberId,
                User = user,
                UserId = user.Id,
                Team = team,
                TeamId = team.Id,
                Role = role,
                AddedDate = DateTime.UtcNow.AddDays(-1),
                IsActive = isActive // Aktywność samego członkostwa
            };
        }

        private Channel CreateTestChannel(string id = "channel-1", string displayName = "General", bool isActive = true, ChannelStatus status = ChannelStatus.Active)
        {
            // Pomijamy CreatedBy/CreatedDate
            return new Channel { Id = id, DisplayName = displayName, Status = status };
        }


        [Fact]
        public void Team_WhenCreated_ShouldHaveDefaultValuesAndCorrectBaseEntityInitialization()
        {
            // Przygotowanie i Wykonanie
            var team = new Team(); // Zakładamy, że BaseEntity ustawi IsActive = true i domyślne CreatedBy/Date

            // Sprawdzenie pól bezpośrednich
            team.Id.Should().Be(string.Empty);
            team.DisplayName.Should().Be(string.Empty);
            team.Description.Should().Be(string.Empty);
            team.Owner.Should().Be(string.Empty);
            team.Status.Should().Be(TeamStatus.Active); // Domyślny status

            // Sprawdzenie pól z BaseEntity i nowego IsActive
            // BaseEntity.IsActive (oryginalne pole) jest teraz ukryte przez 'new bool IsActive' w Team.
            // Testujemy nowe, obliczeniowe IsActive.
            team.IsActive.Should().BeTrue(); // Ponieważ domyślny Status to Active

            // Sprawdzenie kolekcji nawigacyjnych
            team.Members.Should().NotBeNull().And.BeEmpty();
            team.Channels.Should().NotBeNull().And.BeEmpty();

            // Sprawdzenie właściwości obliczanych
            // IsEffectivelyActive zostało usunięte lub zastąpione
            team.IsFullyOperational.Should().BeTrue(); // Brak dat Start/End, IsActive jest true
            team.MemberCount.Should().Be(0);
            team.OwnerCount.Should().Be(0);
            team.DisplayNameWithStatus.Should().Be(string.Empty); // Bo DisplayName jest puste
        }

        [Fact]
        public void Team_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var team = new Team();
            var now = DateTime.UtcNow;

            // Wykonanie
            team.Id = "team-xyz";
            team.DisplayName = "Super Zespół";
            team.Description = "Opis";
            team.Owner = "owner@example.com";
            team.Status = TeamStatus.Archived; // Bezpośrednie ustawienie Status
            team.StatusChangeDate = now.AddMinutes(-5);
            team.StatusChangedBy = "status_changer";
            // ... (reszta właściwości jak wcześniej)

            // BaseEntity.IsActive jest teraz ukryte.
            // Możemy ustawić oryginalne BaseEntity.IsActive (np. do celów testowych, jeśli coś z BaseEntity by go używało),
            // ale dla Team.IsActive nie będzie to miało znaczenia.
            // ((BaseEntity)team).IsActive = false; // To ustawiłoby pole z BaseEntity, ale nie wpłynie na Team.IsActive

            // Sprawdzenie
            team.Id.Should().Be("team-xyz");
            team.DisplayName.Should().Be("Super Zespół");
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse(); // Obliczone na podstawie Status
            // ... (reszta asercji)
        }

        /* Komentarz: Usuwam ten test, ponieważ IsEffectivelyActive zostało zastąpione przez nowe, obliczeniowe IsActive
        [Fact]
        public void Team_IsEffectivelyActive_ShouldReflectStatus()
        {
            var team = new Team();

            team.Status = TeamStatus.Active;
            team.IsEffectivelyActive.Should().BeTrue(); // Powinno być team.IsActive.Should().BeTrue();

            team.Status = TeamStatus.Archived;
            team.IsEffectivelyActive.Should().BeFalse(); // Powinno być team.IsActive.Should().BeFalse();
        }
        */

        [Fact]
        public void Team_IsFullyOperational_ShouldReflectIsActiveAndDates() // Zmieniono nazwę testu dla jasności
        {
            var team = new Team { StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(1) };

            // Scenariusz 1: Wszystko aktywne
            //((BaseEntity)team).IsActive = true; // To już nie jest potrzebne do ustawiania Team.IsActive
            team.Status = TeamStatus.Active; // To ustawia team.IsActive na true
            team.IsFullyOperational.Should().BeTrue();

            // Scenariusz 2: Status = Archived (co implikuje IsActive = false)
            team.Status = TeamStatus.Archived;
            team.IsFullyOperational.Should().BeFalse();
            team.Status = TeamStatus.Active; // Reset

            // Scenariusz 3: Data przyszła
            team.Status = TeamStatus.Active; // Upewniamy się, że jest aktywny
            team.StartDate = DateTime.Today.AddDays(1);
            team.IsFullyOperational.Should().BeFalse();
            team.StartDate = DateTime.Today.AddDays(-1); // Reset

            // Scenariusz 4: Data przeszła
            team.EndDate = DateTime.Today.AddDays(-1);
            team.IsFullyOperational.Should().BeFalse();
        }


        [Fact]
        public void Team_ComputedProperties_MemberCounts_ShouldWorkCorrectly()
        {
            // Przygotowanie
            var team = new Team { Status = TeamStatus.Active }; // IsActive będzie true
            var ownerUser = CreateTestUser("owner-user", "owner@example.com", isActive: true);
            var memberUser1 = CreateTestUser("member-user1", "member1@example.com", isActive: true);
            var memberUser2 = CreateTestUser("member-user2", "member2@example.com", isActive: true);
            var inactiveUser = CreateTestUser("inactive-user", "inactive@example.com", isActive: false);
            var userForInactiveMembership = CreateTestUser("user-for-inactive-m", "user-fim@example.com", isActive: true);

            team.Members.Add(CreateTestTeamMember(ownerUser, team, TeamMemberRole.Owner, "m1", isActive: true));
            team.Members.Add(CreateTestTeamMember(memberUser1, team, TeamMemberRole.Member, "m2", isActive: true));
            team.Members.Add(CreateTestTeamMember(memberUser2, team, TeamMemberRole.Member, "m3", isActive: true));
            team.Members.Add(CreateTestTeamMember(inactiveUser, team, TeamMemberRole.Member, "m4", isActive: true));
            team.Members.Add(CreateTestTeamMember(userForInactiveMembership, team, TeamMemberRole.Member, "m5", isActive: false));

            // Sprawdzenie
            team.MemberCount.Should().Be(3);
            team.OwnerCount.Should().Be(1);
            team.RegularMemberCount.Should().Be(2);

            team.Owners.Should().HaveCount(1).And.Contain(ownerUser);
            team.RegularMembers.Should().HaveCount(2).And.Contain(memberUser1).And.Contain(memberUser2);
            team.AllActiveUsers.Should().HaveCount(3)
                .And.Contain(ownerUser)
                .And.Contain(memberUser1)
                .And.Contain(memberUser2);
        }

        [Theory]
        [InlineData(null, 2, false, null)]
        [InlineData(3, 2, false, 66.7)]
        [InlineData(3, 3, true, 100.0)]
        [InlineData(3, 4, true, 133.3)]
        [InlineData(0, 0, true, null)]
        public void Team_IsAtCapacity_And_CapacityPercentage_ShouldWorkCorrectly(int? maxMembers, int currentActiveMembersCount, bool expectedIsAtCapacity, double? expectedPercentage)
        {
            // Przygotowanie
            var team = new Team { MaxMembers = maxMembers, Status = TeamStatus.Active }; // IsActive będzie true
            for (int i = 0; i < currentActiveMembersCount; i++)
            {
                var user = CreateTestUser($"u{i}", $"u{i}@example.com", isActive: true);
                team.Members.Add(CreateTestTeamMember(user, team, memberId: $"m{i}", isActive: true));
            }

            // Sprawdzenie
            team.IsAtCapacity.Should().Be(expectedIsAtCapacity);
            if (expectedPercentage.HasValue)
            {
                team.CapacityPercentage.Should().BeApproximately(expectedPercentage.Value, 0.1);
            }
            else
            {
                team.CapacityPercentage.Should().BeNull();
            }
        }

        [Fact]
        public void Team_ChannelCount_ShouldCountActiveChannelsWithActiveStatus()
        {
            var team = new Team(); // Domyślnie Status = Active, więc IsActive = true
            team.Channels.Add(CreateTestChannel("c1", "Active Channel 1", status: ChannelStatus.Active));
            team.Channels.Add(CreateTestChannel("c2", "Active Channel 2", status: ChannelStatus.Active));
            team.Channels.Add(CreateTestChannel("c3", "Archived Channel", status: ChannelStatus.Archived)); // Kanał zarchiwizowany
            team.Channels.Add(CreateTestChannel("c4", "Inactive Record Channel", status: ChannelStatus.Active)); // Rekord kanału nieaktywny

            team.ChannelCount.Should().Be(3); // Liczy tylko kanały Channel.Status = Active
        }


        [Fact]
        public void Team_DateComputedProperties_ShouldWorkCorrectly()
        {
            var team = new Team { Status = TeamStatus.Active }; // IsActive będzie true
            var today = DateTime.Today;

            // Scenariusz 1: Kurs przyszły
            team.StartDate = today.AddDays(10);
            team.EndDate = today.AddDays(40);
            team.IsFullyOperational.Should().BeFalse();
            team.DaysSinceStart.Should().Be(0);
            team.DaysUntilEnd.Should().Be(40);
            team.CompletionPercentage.Should().Be(0);

            // Scenariusz 2: Kurs trwający (rozpoczął się dzisiaj)
            team.StartDate = today;
            team.EndDate = today.AddDays(29);
            team.IsFullyOperational.Should().BeTrue();
            team.DaysSinceStart.Should().Be(0);
            team.DaysUntilEnd.Should().Be(29);
            team.CompletionPercentage.Should().BeApproximately(0, 0.1);


            // Scenariusz 2b: Kurs trwający (w połowie)
            team.StartDate = today.AddDays(-15);
            team.EndDate = today.AddDays(14);
            team.IsFullyOperational.Should().BeTrue();
            team.DaysSinceStart.Should().Be(15);
            team.DaysUntilEnd.Should().Be(14);
            team.CompletionPercentage.Should().BeApproximately(51.7, 0.1);

            // Scenariusz 3: Kurs zakończony
            team.StartDate = today.AddDays(-40);
            team.EndDate = today.AddDays(-10);
            team.IsFullyOperational.Should().BeFalse(); // Bo EndDate jest w przeszłości
            team.DaysSinceStart.Should().Be(40);
            team.DaysUntilEnd.Should().Be(0);
            team.CompletionPercentage.Should().Be(100);

            // Scenariusz 4: Brak dat
            team.StartDate = null;
            team.EndDate = null;
            team.IsFullyOperational.Should().BeTrue(); // Bo brak dat nie ogranicza (jeśli IsActive jest true)
            team.DaysSinceStart.Should().BeNull();
            team.DaysUntilEnd.Should().BeNull();
            team.CompletionPercentage.Should().BeNull();
        }

        [Fact]
        public void Team_DisplayNameWithStatus_ShouldFormatCorrectly()
        {
            var team = new Team { DisplayName = "Mój Zespół" };

            // Scenariusz 1: Status Aktywny, DisplayName bez prefiksu
            team.Status = TeamStatus.Active;
            team.DisplayName = "Mój Zespół"; // Upewniamy się, że jest bez prefiksu
            team.DisplayNameWithStatus.Should().Be("Mój Zespół");

            // Scenariusz 2: Status Zarchiwizowany, DisplayName bez prefiksu
            team.Status = TeamStatus.Archived;
            team.DisplayName = "Mój Zespół"; // Nadal bez prefiksu
            team.DisplayNameWithStatus.Should().Be("ARCHIWALNY - Mój Zespół");

            // Scenariusz 3: Status Aktywny, DisplayName (błędnie) z prefiksem
            team.Status = TeamStatus.Active;
            team.DisplayName = "ARCHIWALNY - Inny Zespół";
            team.DisplayNameWithStatus.Should().Be("Inny Zespół"); // Powinno usunąć prefiks

            // Scenariusz 4: Status Zarchiwizowany, DisplayName (poprawnie) z prefiksem
            team.Status = TeamStatus.Archived;
            team.DisplayName = "ARCHIWALNY - Stary Zespół";
            team.DisplayNameWithStatus.Should().Be("ARCHIWALNY - Stary Zespół"); // Powinno być bez zmian
        }


        [Fact]
        public void Team_ShortDescription_ShouldFormatCorrectly()
        {
            var team = new Team { Status = TeamStatus.Active };
            var activeUser = CreateTestUser("u1", isActive: true);
            var activeMembership = CreateTestTeamMember(activeUser, team, isActive: true);

            team.ShortDescription.Should().Be("Zespół");

            team.AcademicYear = "2023/24";
            team.ShortDescription.Should().Be("2023/24");

            team.Semester = "Zimowy";
            team.ShortDescription.Should().Be("2023/24 • Zimowy");

            team.SchoolType = new SchoolType { ShortName = "LO", IsActive = true };
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO");

            team.Members.Add(activeMembership);
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO • 1 osób");

            var anotherActiveUser = CreateTestUser("u2", isActive: true);
            team.Members.Add(CreateTestTeamMember(anotherActiveUser, team, memberId: "m2", isActive: true));
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO • 2 osób");

            var inactiveUser = CreateTestUser("u3", isActive: false);
            team.Members.Add(CreateTestTeamMember(inactiveUser, team, memberId: "m3", isActive: true));
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO • 2 osób");
        }

        [Fact]
        public void Team_ArchiveRestoreMethods_ShouldWorkCorrectly()
        {
            // Przygotowanie
            var team = new Team { DisplayName = "Projekt Alfa", Description = "Ważny projekt", CreatedBy = "system_init" };
            var userUpn = "admin@example.com";
            var archiveReason = "Zakończono etap 1";
            var initialModifiedDate = team.ModifiedDate; // Może być null

            // Wykonanie - Archiwizacja
            team.Archive(archiveReason, userUpn);

            // Sprawdzenie po archiwizacji
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse(); // Obliczeniowe
            team.DisplayName.Should().Be("ARCHIWALNY - Projekt Alfa");
            team.Description.Should().Be("ARCHIWALNY - Ważny projekt");
            team.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            team.StatusChangedBy.Should().Be(userUpn);
            team.StatusChangeReason.Should().Be(archiveReason);
            team.ModifiedBy.Should().Be(userUpn);
            team.ModifiedDate.Should().NotBeNull();
            if (initialModifiedDate.HasValue) team.ModifiedDate.Value.Should().BeAfter(initialModifiedDate.Value);


            // Wykonanie - Przywrócenie
            var restoreReason = "Przywrócono z archiwum"; // Domyślny powód z metody Restore
            initialModifiedDate = team.ModifiedDate;
            team.Restore(userUpn);

            // Sprawdzenie po przywróceniu
            team.Status.Should().Be(TeamStatus.Active);
            team.IsActive.Should().BeTrue(); // Obliczeniowe
            team.DisplayName.Should().Be("Projekt Alfa");
            team.Description.Should().Be("Ważny projekt");
            team.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            team.StatusChangedBy.Should().Be(userUpn);
            team.StatusChangeReason.Should().Be(restoreReason);
            team.ModifiedBy.Should().Be(userUpn);
            team.ModifiedDate.Should().NotBeNull();
            if (initialModifiedDate.HasValue) team.ModifiedDate.Value.Should().BeAfter(initialModifiedDate.Value);
        }


        [Fact]
        public void HasMember_ShouldReturnCorrectValue_ConsideringUserAndMembershipActivity()
        {
            var team = new Team(); // Domyślnie Status = Active, IsActive = true
            var activeUser = CreateTestUser("user1", isActive: true);
            var inactiveUser = CreateTestUser("user2", isActive: false);

            team.Members.Add(CreateTestTeamMember(activeUser, team, memberId: "m1", isActive: true));
            team.Members.Add(CreateTestTeamMember(inactiveUser, team, memberId: "m2", isActive: true));
            team.Members.Add(CreateTestTeamMember(CreateTestUser("user3"), team, memberId: "m3", isActive: false));

            team.HasMember("user1").Should().BeTrue();
            team.HasMember("user2").Should().BeFalse(); // Bo User jest nieaktywny
            team.HasMember("user3").Should().BeFalse(); // Bo członkostwo jest nieaktywne
            team.HasMember("non-existent-user").Should().BeFalse();
        }

        [Fact]
        public void HasOwner_ShouldReturnCorrectValue_ConsideringUserAndMembershipActivity()
        {
            var team = new Team(); // Domyślnie Status = Active, IsActive = true
            var activeOwner = CreateTestUser("owner1", isActive: true);
            var inactiveOwnerUser = CreateTestUser("owner2", isActive: false);
            var activeMember = CreateTestUser("member1", isActive: true);

            team.Members.Add(CreateTestTeamMember(activeOwner, team, TeamMemberRole.Owner, "m-owner1", isActive: true));
            team.Members.Add(CreateTestTeamMember(inactiveOwnerUser, team, TeamMemberRole.Owner, "m-owner2", isActive: true));
            team.Members.Add(CreateTestTeamMember(activeMember, team, TeamMemberRole.Member, "m-member1", isActive: true));
            team.Members.Add(CreateTestTeamMember(CreateTestUser("owner3"), team, TeamMemberRole.Owner, "m-owner3", isActive: false));

            team.HasOwner("owner1").Should().BeTrue();
            team.HasOwner("owner2").Should().BeFalse(); // User nieaktywny
            team.HasOwner("member1").Should().BeFalse(); // To jest Member
            team.HasOwner("owner3").Should().BeFalse(); // Członkostwo nieaktywne
        }

        [Fact]
        public void GetMembership_ShouldReturnCorrectMembership_ConsideringUserAndMembershipActivity()
        {
            var team = new Team(); // Domyślnie Status = Active, IsActive = true
            var activeUser = CreateTestUser("user1", isActive: true);
            var inactiveUser = CreateTestUser("user2", isActive: false);

            var membershipActive = CreateTestTeamMember(activeUser, team, memberId: "m1", isActive: true);
            var membershipWithInactiveUser = CreateTestTeamMember(inactiveUser, team, memberId: "m2", isActive: true);
            var inactiveMembership = CreateTestTeamMember(CreateTestUser("user3"), team, memberId: "m3", isActive: false);

            team.Members.Add(membershipActive);
            team.Members.Add(membershipWithInactiveUser);
            team.Members.Add(inactiveMembership);

            team.GetMembership("user1").Should().Be(membershipActive);
            team.GetMembership("user2").Should().BeNull();
            team.GetMembership("user3").Should().BeNull();
            team.GetMembership("non-existent-user").Should().BeNull();
        }

        [Fact]
        public void CanAddMoreMembers_ShouldRespectMaxMembersAndActiveCount()
        {
            var team = new Team { Status = TeamStatus.Active }; // IsActive będzie true
            team.CanAddMoreMembers().Should().BeTrue();

            team.MaxMembers = 2;
            team.CanAddMoreMembers().Should().BeTrue();

            team.Members.Add(CreateTestTeamMember(CreateTestUser("u1", isActive: true), team, memberId: "m1", isActive: true));
            team.CanAddMoreMembers().Should().BeTrue();
            team.MemberCount.Should().Be(1);

            team.Members.Add(CreateTestTeamMember(CreateTestUser("u2", isActive: true), team, memberId: "m2", isActive: true));
            team.CanAddMoreMembers().Should().BeFalse();
            team.MemberCount.Should().Be(2);

            team.Members.Add(CreateTestTeamMember(CreateTestUser("u3", isActive: false), team, memberId: "m3", isActive: true));
            team.CanAddMoreMembers().Should().BeFalse();
            team.MemberCount.Should().Be(2);

            team.Members.First(m => m.Id == "m1").IsActive = false;
            team.CanAddMoreMembers().Should().BeTrue();
            team.MemberCount.Should().Be(1);
        }

        [Fact]
        public void UpdateLastActivity_ShouldSetLastActivityDateToUtcNow()
        {
            var team = new Team();
            var initialDate = team.LastActivityDate;

            team.UpdateLastActivity();

            team.LastActivityDate.Should().NotBeNull();
            team.LastActivityDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            if (initialDate.HasValue)
                team.LastActivityDate.Value.Should().BeAfter(initialDate.Value);
        }

        // DODATKOWE TESTY SYNCHRONIZACJI - ETAP 4/4

        [Fact]
        public void GetBaseDisplayName_WithPrefix_ShouldRemovePrefix()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "ARCHIWALNY - Test Team" 
            };

            // Act
            var result = team.GetBaseDisplayName();

            // Assert
            result.Should().Be("Test Team");
        }

        [Fact]
        public void GetBaseDisplayName_WithoutPrefix_ShouldReturnOriginal()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "Test Team" 
            };

            // Act
            var result = team.GetBaseDisplayName();

            // Assert
            result.Should().Be("Test Team");
        }

        [Fact]
        public void GetBaseDisplayName_WithEmptyString_ShouldReturnEmpty()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "" 
            };

            // Act
            var result = team.GetBaseDisplayName();

            // Assert
            result.Should().Be("");
        }

        [Fact]
        public void GetBaseDisplayName_WithNull_ShouldReturnEmpty()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = null! 
            };

            // Act
            var result = team.GetBaseDisplayName();

            // Assert
            result.Should().Be("");
        }

        [Fact]
        public void GetBaseDescription_WithPrefix_ShouldRemovePrefix()
        {
            // Arrange
            var team = new Team 
            { 
                Description = "ARCHIWALNY - Test Description" 
            };

            // Act
            var result = team.GetBaseDescription();

            // Assert
            result.Should().Be("Test Description");
        }

        [Fact]
        public void GetBaseDescription_WithoutPrefix_ShouldReturnOriginal()
        {
            // Arrange
            var team = new Team 
            { 
                Description = "Test Description" 
            };

            // Act
            var result = team.GetBaseDescription();

            // Assert
            result.Should().Be("Test Description");
        }

        [Fact]
        public void Archive_TeamWithPrefix_ShouldNotDuplicatePrefix()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "ARCHIWALNY - Test Team",
                Description = "ARCHIWALNY - Test Description",
                Status = TeamStatus.Active
            };

            // Act
            team.Archive("reason", "user@test.com");

            // Assert
            team.DisplayName.Should().Be("ARCHIWALNY - Test Team"); // Bez duplikacji
            team.Description.Should().Be("ARCHIWALNY - Test Description");
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Archive_ActiveTeam_ShouldAddPrefixAndChangeStatus()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "Test Team",
                Description = "Test Description",
                Status = TeamStatus.Active
            };

            // Act
            team.Archive("Test reason", "user@test.com");

            // Assert
            team.Status.Should().Be(TeamStatus.Archived);
            team.DisplayName.Should().Be("ARCHIWALNY - Test Team");
            team.Description.Should().Be("ARCHIWALNY - Test Description");
            team.StatusChangeReason.Should().Be("Test reason");
            team.StatusChangedBy.Should().Be("user@test.com");
            team.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Restore_ArchivedTeam_ShouldRemovePrefixAndChangeStatus()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "ARCHIWALNY - Test Team",
                Description = "ARCHIWALNY - Test Description",
                Status = TeamStatus.Archived
            };

            // Act
            team.Restore("user@test.com");

            // Assert
            team.Status.Should().Be(TeamStatus.Active);
            team.DisplayName.Should().Be("Test Team");
            team.Description.Should().Be("Test Description");
            team.StatusChangeReason.Should().Be("Przywrócono z archiwum");
            team.StatusChangedBy.Should().Be("user@test.com");
            team.IsActive.Should().BeTrue();
        }

        [Fact]
        public void DisplayNameWithStatus_ActiveTeam_ShouldNotHavePrefix()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "Test Team",
                Status = TeamStatus.Active
            };

            // Act
            var result = team.DisplayNameWithStatus;

            // Assert
            result.Should().Be("Test Team");
        }

        [Fact]
        public void DisplayNameWithStatus_ArchivedTeam_ShouldHavePrefix()
        {
            // Arrange
            var team = new Team 
            { 
                DisplayName = "Test Team", // Bez prefiksu w bazie
                Status = TeamStatus.Archived
            };

            // Act
            var result = team.DisplayNameWithStatus;

            // Assert
            result.Should().Be("ARCHIWALNY - Test Team");
        }

        // KONIEC DODATKOWYCH TESTÓW SYNCHRONIZACJI
    }
}