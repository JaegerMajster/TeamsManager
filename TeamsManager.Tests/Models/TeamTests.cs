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
            return new User { Id = id, UPN = upn, FirstName = "Test", LastName = "User", IsActive = isActive, Role = UserRole.Nauczyciel, CreatedBy = "test_setup", CreatedDate = DateTime.UtcNow };
        }

        private TeamMember CreateTestTeamMember(User user, Team team, TeamMemberRole role = TeamMemberRole.Member, string memberId = "member-1", bool isActive = true)
        {
            return new TeamMember
            {
                Id = memberId,
                User = user,
                UserId = user.Id,
                Team = team,
                TeamId = team.Id,
                Role = role,
                AddedDate = DateTime.UtcNow.AddDays(-1), // Data dodania w przeszłości
                IsActive = isActive, // Aktywność samego członkostwa
                CreatedBy = "test_setup",
                CreatedDate = DateTime.UtcNow
            };
        }

        private Channel CreateTestChannel(string id = "channel-1", string displayName = "General", bool isActive = true, ChannelStatus status = ChannelStatus.Active)
        {
            return new Channel { Id = id, DisplayName = displayName, IsActive = isActive, Status = status, CreatedBy = "test_setup", CreatedDate = DateTime.UtcNow };
        }


        [Fact]
        public void Team_WhenCreated_ShouldHaveDefaultValuesAndCorrectBaseEntityInitialization()
        {
            // Przygotowanie i Wykonanie
            var creationTime = DateTime.UtcNow; // Czas tuż przed utworzeniem
            var team = new Team { CreatedBy = "init_user" }; // Symulacja ustawienia CreatedBy przy tworzeniu

            // Sprawdzenie pól bezpośrednich
            team.Id.Should().Be(string.Empty); // Zakładamy, że ID jest stringiem i domyślnie puste
            team.DisplayName.Should().Be(string.Empty);
            team.Description.Should().Be(string.Empty);
            team.Owner.Should().Be(string.Empty);
            team.Status.Should().Be(TeamStatus.Active);

            team.StatusChangeDate.Should().BeNull();
            team.StatusChangedBy.Should().BeNull();
            team.StatusChangeReason.Should().BeNull();

            team.TemplateId.Should().BeNull();
            team.SchoolTypeId.Should().BeNull();
            team.SchoolYearId.Should().BeNull();
            team.AcademicYear.Should().BeNullOrEmpty();
            team.Semester.Should().BeNullOrEmpty();
            team.StartDate.Should().BeNull();
            team.EndDate.Should().BeNull();
            team.MaxMembers.Should().BeNull();
            team.ExternalId.Should().BeNullOrEmpty();
            team.CourseCode.Should().BeNullOrEmpty();
            team.TotalHours.Should().BeNull();
            team.Level.Should().BeNullOrEmpty();
            team.Language.Should().Be("Polski");
            team.Tags.Should().BeNullOrEmpty();
            team.Notes.Should().BeNullOrEmpty();
            team.Visibility.Should().Be(TeamVisibility.Private);
            team.RequiresApproval.Should().BeTrue();
            team.LastActivityDate.Should().BeNull();

            // Sprawdzenie pól z BaseEntity
            team.IsActive.Should().BeTrue(); // Domyślne z BaseEntity
            // Jeśli CreatedDate jest ustawiane przez DbContext.SaveChanges, to tutaj będzie default.
            // Jeśli BaseEntity ma logikę w konstruktorze lub jest ustawiane przy tworzeniu obiektu:
            // team.CreatedDate.Should().BeCloseTo(creationTime, TimeSpan.FromSeconds(1));
            // team.CreatedBy.Should().Be("init_user");
            team.ModifiedDate.Should().BeNull();
            team.ModifiedBy.Should().BeNull();


            // Sprawdzenie kolekcji nawigacyjnych
            team.Members.Should().NotBeNull().And.BeEmpty();
            team.Channels.Should().NotBeNull().And.BeEmpty();
            team.Template.Should().BeNull();
            team.SchoolType.Should().BeNull();
            team.SchoolYear.Should().BeNull();

            // Sprawdzenie właściwości obliczanych
            team.IsEffectivelyActive.Should().BeTrue();
            team.IsFullyOperational.Should().BeTrue(); // Brak dat Start/End, więc jest operacyjny jeśli aktywny
            team.MemberCount.Should().Be(0);
            team.OwnerCount.Should().Be(0);
            team.RegularMemberCount.Should().Be(0);
            team.IsAtCapacity.Should().BeFalse();
            team.CapacityPercentage.Should().BeNull();
            team.ChannelCount.Should().Be(0);
            team.DaysUntilEnd.Should().BeNull();
            team.DaysSinceStart.Should().BeNull();
            team.CompletionPercentage.Should().BeNull();
            team.Owners.Should().NotBeNull().And.BeEmpty();
            team.RegularMembers.Should().NotBeNull().And.BeEmpty();
            team.AllActiveUsers.Should().NotBeNull().And.BeEmpty();
            team.DisplayNameWithStatus.Should().Be(string.Empty);
            team.ShortDescription.Should().Be("Zespół");
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
            team.Status = TeamStatus.Archived; // Bezpośrednie ustawienie dla testu, normalnie przez Archive()
            team.StatusChangeDate = now.AddMinutes(-5);
            team.StatusChangedBy = "status_changer";
            team.StatusChangeReason = "Powód zmiany statusu";
            team.TemplateId = "tpl-1";
            team.SchoolTypeId = "sch-type-1";
            team.SchoolYearId = "syear-1";
            team.AcademicYear = "2024/2025";
            team.Semester = "Letni";
            team.StartDate = now.AddDays(-30);
            team.EndDate = now.AddDays(60);
            team.MaxMembers = 25;
            team.ExternalId = "EXT001";
            team.CourseCode = "PROG101";
            team.TotalHours = 120;
            team.Level = "Podstawowy";
            team.Language = "Angielski";
            team.Tags = "programowanie, csharp";
            team.Notes = "Ważne notatki";
            team.Visibility.Should().Be(TeamVisibility.Private);
            team.RequiresApproval = false;
            team.LastActivityDate = now.AddDays(-1);
            team.IsActive = false; // Ustawienie BaseEntity.IsActive
            team.CreatedDate = now.AddDays(-10); // Ustawienie BaseEntity
            team.CreatedBy = "creator"; // Ustawienie BaseEntity
            team.ModifiedDate = now; // Ustawienie BaseEntity
            team.ModifiedBy = "modifier"; // Ustawienie BaseEntity


            // Sprawdzenie
            team.Id.Should().Be("team-xyz");
            team.DisplayName.Should().Be("Super Zespół");
            team.Status.Should().Be(TeamStatus.Archived);
            team.TemplateId.Should().Be("tpl-1");
            team.SchoolTypeId.Should().Be("sch-type-1");
            team.SchoolYearId.Should().Be("syear-1");
            team.AcademicYear.Should().Be("2024/2025");
            team.Semester.Should().Be("Letni");
            team.StartDate.Should().Be(now.AddDays(-30));
            team.EndDate.Should().Be(now.AddDays(60));
            team.MaxMembers.Should().Be(25);
            team.ExternalId.Should().Be("EXT001");
            team.CourseCode.Should().Be("PROG101");
            team.TotalHours.Should().Be(120);
            team.Level.Should().Be("Podstawowy");
            team.Language.Should().Be("Angielski");
            team.Tags.Should().Be("programowanie, csharp");
            team.Notes.Should().Be("Ważne notatki");
            team.Visibility.Should().Be(TeamVisibility.Private);
            team.RequiresApproval.Should().BeFalse();
            team.LastActivityDate.Should().Be(now.AddDays(-1));
            team.StatusChangeDate.Should().Be(now.AddMinutes(-5));
            team.StatusChangedBy.Should().Be("status_changer");
            team.StatusChangeReason.Should().Be("Powód zmiany statusu");

            // Sprawdzenie pól z BaseEntity
            team.IsActive.Should().BeFalse();
            team.CreatedDate.Should().Be(now.AddDays(-10));
            team.CreatedBy.Should().Be("creator");
            team.ModifiedDate.Should().Be(now);
            team.ModifiedBy.Should().Be("modifier");
        }

        [Fact]
        public void Team_IsEffectivelyActive_ShouldReflectStatus()
        {
            var team = new Team();

            team.Status = TeamStatus.Active;
            team.IsEffectivelyActive.Should().BeTrue();

            team.Status = TeamStatus.Archived;
            team.IsEffectivelyActive.Should().BeFalse();
        }

        [Fact]
        public void Team_IsFullyOperational_ShouldReflectBaseIsActiveAndStatusAndDates()
        {
            var team = new Team { StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(1) };

            // Scenariusz 1: Wszystko aktywne
            team.IsActive = true; // BaseEntity
            team.Status = TeamStatus.Active;
            team.IsFullyOperational.Should().BeTrue();

            // Scenariusz 2: BaseEntity.IsActive = false
            team.IsActive = false;
            team.Status = TeamStatus.Active;
            team.IsFullyOperational.Should().BeFalse();
            team.IsActive = true; // Reset

            // Scenariusz 3: Status = Archived
            team.Status = TeamStatus.Archived;
            team.IsFullyOperational.Should().BeFalse();
            team.Status = TeamStatus.Active; // Reset

            // Scenariusz 4: Data przyszła
            team.StartDate = DateTime.Today.AddDays(1);
            team.IsFullyOperational.Should().BeFalse();
            team.StartDate = DateTime.Today.AddDays(-1); // Reset

            // Scenariusz 5: Data przeszła
            team.EndDate = DateTime.Today.AddDays(-1);
            team.IsFullyOperational.Should().BeFalse();
        }


        [Fact]
        public void Team_ComputedProperties_MemberCounts_ShouldWorkCorrectly()
        {
            // Przygotowanie
            var team = new Team { IsActive = true, Status = TeamStatus.Active }; // Upewniamy się, że zespół jest operacyjny
            var ownerUser = CreateTestUser("owner-user", "owner@example.com", isActive: true);
            var memberUser1 = CreateTestUser("member-user1", "member1@example.com", isActive: true);
            var memberUser2 = CreateTestUser("member-user2", "member2@example.com", isActive: true);
            var inactiveUser = CreateTestUser("inactive-user", "inactive@example.com", isActive: false); // Użytkownik nieaktywny
            var userForInactiveMembership = CreateTestUser("user-for-inactive-m", "user-fim@example.com", isActive: true);


            team.Members.Add(CreateTestTeamMember(ownerUser, team, TeamMemberRole.Owner, "m1", isActive: true));
            team.Members.Add(CreateTestTeamMember(memberUser1, team, TeamMemberRole.Member, "m2", isActive: true));
            team.Members.Add(CreateTestTeamMember(memberUser2, team, TeamMemberRole.Member, "m3", isActive: true));
            team.Members.Add(CreateTestTeamMember(inactiveUser, team, TeamMemberRole.Member, "m4", isActive: true)); // Członkostwo aktywne, ale Użytkownik nieaktywny
            team.Members.Add(CreateTestTeamMember(userForInactiveMembership, team, TeamMemberRole.Member, "m5", isActive: false)); // Członkostwo nieaktywne

            // Sprawdzenie
            team.MemberCount.Should().Be(3); // Tylko m1, m2, m3 (aktywne członkostwa aktywnych użytkowników)
            team.OwnerCount.Should().Be(1);  // Tylko m1
            team.RegularMemberCount.Should().Be(2); // Tylko m2, m3

            team.Owners.Should().HaveCount(1).And.Contain(ownerUser);
            team.RegularMembers.Should().HaveCount(2).And.Contain(memberUser1).And.Contain(memberUser2);
            team.AllActiveUsers.Should().HaveCount(3)
                .And.Contain(ownerUser)
                .And.Contain(memberUser1)
                .And.Contain(memberUser2);
        }

        [Theory]
        [InlineData(null, 2, false, null)] // Brak limitu, 2 członków
        [InlineData(3, 2, false, 66.7)]    // Limit 3, 2 członkowie
        [InlineData(3, 3, true, 100.0)]   // Limit 3, 3 członkowie (na limicie)
        [InlineData(3, 4, true, 133.3)]   // Limit 3, 4 członkowie (powyżej limitu, CapacityPercentage > 100)
        [InlineData(0, 0, true, null)] // Limit 0, 0 członków (IsAtCapacity = true, bo nie można dodać, CapacityPercentage = null bo dzielenie przez 0)
        public void Team_IsAtCapacity_And_CapacityPercentage_ShouldWorkCorrectly(int? maxMembers, int currentActiveMembersCount, bool expectedIsAtCapacity, double? expectedPercentage)
        {
            // Przygotowanie
            var team = new Team { MaxMembers = maxMembers, IsActive = true, Status = TeamStatus.Active };
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
            var team = new Team();
            team.Channels.Add(CreateTestChannel("c1", "Active Channel 1", isActive: true, status: ChannelStatus.Active));
            team.Channels.Add(CreateTestChannel("c2", "Active Channel 2", isActive: true, status: ChannelStatus.Active));
            team.Channels.Add(CreateTestChannel("c3", "Archived Channel", isActive: true, status: ChannelStatus.Archived));
            team.Channels.Add(CreateTestChannel("c4", "Inactive Record Channel", isActive: false, status: ChannelStatus.Active));

            team.ChannelCount.Should().Be(2);
        }


        [Fact]
        public void Team_DateComputedProperties_ShouldWorkCorrectly()
        {
            var team = new Team { IsActive = true, Status = TeamStatus.Active }; // Założenie dla tych testów
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
            team.EndDate = today.AddDays(29); // Łącznie 30 dni (0-29)
            team.IsFullyOperational.Should().BeTrue();
            team.DaysSinceStart.Should().Be(0);
            team.DaysUntilEnd.Should().Be(29);
            // (0 / 30) * 100, ale dla pierwszego dnia może być subtelne
            team.CompletionPercentage.Should().BeApproximately(0, 0.1); // Dzień 0 z 30


            // Scenariusz 2b: Kurs trwający (w połowie)
            team.StartDate = today.AddDays(-15);
            team.EndDate = today.AddDays(14); // Łącznie 30 dni
            team.IsFullyOperational.Should().BeTrue();
            team.DaysSinceStart.Should().Be(15);
            team.DaysUntilEnd.Should().Be(14);
            team.CompletionPercentage.Should().BeApproximately(51.7, 0.1); 

            // Scenariusz 3: Kurs zakończony
            team.StartDate = today.AddDays(-40);
            team.EndDate = today.AddDays(-10);
            team.IsFullyOperational.Should().BeFalse();
            team.DaysSinceStart.Should().Be(40);
            team.DaysUntilEnd.Should().Be(0);
            team.CompletionPercentage.Should().Be(100);

            // Scenariusz 4: Brak dat
            team.StartDate = null;
            team.EndDate = null;
            team.IsFullyOperational.Should().BeTrue(); // Bo brak dat nie ogranicza (jeśli IsActive i Status.Active)
            team.DaysSinceStart.Should().BeNull();
            team.DaysUntilEnd.Should().BeNull();
            team.CompletionPercentage.Should().BeNull();
        }

        [Fact]
        public void Team_DisplayNameWithStatus_ShouldFormatCorrectly()
        {
            var team = new Team { DisplayName = "Mój Zespół" };

            team.Status = TeamStatus.Active;
            team.DisplayNameWithStatus.Should().Be("Mój Zespół"); // Ta asercja jest OK

            team.Status = TeamStatus.Archived; // Zmieniamy tylko Status, ale NIE wywołujemy team.Archive()
            team.DisplayNameWithStatus.Should().Be("ARCHIWALNY - Mój Zespół"); // BŁĄD TUTAJ
        }

        [Fact]
        public void Team_ShortDescription_ShouldFormatCorrectly()
        {
            var team = new Team { IsActive = true, Status = TeamStatus.Active }; // Aktywny zespół
            var activeUser = CreateTestUser("u1", isActive: true);
            var activeMembership = CreateTestTeamMember(activeUser, team, isActive: true);


            team.ShortDescription.Should().Be("Zespół");

            team.AcademicYear = "2023/24";
            team.ShortDescription.Should().Be("2023/24");

            team.Semester = "Zimowy";
            team.ShortDescription.Should().Be("2023/24 • Zimowy");

            team.SchoolType = new SchoolType { ShortName = "LO", IsActive = true };
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO");

            // Dodajemy aktywne członkostwo z aktywnym użytkownikiem
            team.Members.Add(activeMembership);
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO • 1 osób");

            // Dodajemy drugie takie członkostwo
            var anotherActiveUser = CreateTestUser("u2", isActive: true);
            team.Members.Add(CreateTestTeamMember(anotherActiveUser, team, memberId: "m2", isActive: true));
            team.ShortDescription.Should().Be("2023/24 • Zimowy • LO • 2 osób");

            // Dodajemy członkostwo z nieaktywnym użytkownikiem - nie powinno być liczone
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
            var initialModifiedDate = team.ModifiedDate;

            // Wykonanie - Archiwizacja
            team.Archive(archiveReason, userUpn);

            // Sprawdzenie po archiwizacji
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse(); // Z BaseEntity
            team.DisplayName.Should().Be("ARCHIWALNY - Projekt Alfa");
            team.Description.Should().Be("ARCHIWALNY - Ważny projekt");
            team.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            team.StatusChangedBy.Should().Be(userUpn);
            team.StatusChangeReason.Should().Be(archiveReason);
            team.ModifiedBy.Should().Be(userUpn);
            team.ModifiedDate.Should().NotBeNull();
            if (initialModifiedDate.HasValue) team.ModifiedDate.Should().BeAfter(initialModifiedDate.Value);


            // Wykonanie - Przywrócenie
            var restoreReason = "Przywrócono z archiwum";
            initialModifiedDate = team.ModifiedDate; // Zapisz datę modyfikacji po archiwizacji
            team.Restore(userUpn);

            // Sprawdzenie po przywróceniu
            team.Status.Should().Be(TeamStatus.Active);
            team.IsActive.Should().BeTrue(); // Z BaseEntity
            team.DisplayName.Should().Be("Projekt Alfa");
            team.Description.Should().Be("Ważny projekt");
            team.StatusChangeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            team.StatusChangedBy.Should().Be(userUpn);
            team.StatusChangeReason.Should().Be(restoreReason);
            team.ModifiedBy.Should().Be(userUpn);
            team.ModifiedDate.Should().NotBeNull();
            if (initialModifiedDate.HasValue) team.ModifiedDate.Should().BeAfter(initialModifiedDate.Value);
        }


        [Fact]
        public void HasMember_ShouldReturnCorrectValue_ConsideringUserAndMembershipActivity()
        {
            var team = new Team();
            var activeUser = CreateTestUser("user1", isActive: true);
            var inactiveUser = CreateTestUser("user2", isActive: false);

            team.Members.Add(CreateTestTeamMember(activeUser, team, memberId: "m1", isActive: true));
            team.Members.Add(CreateTestTeamMember(inactiveUser, team, memberId: "m2", isActive: true));
            team.Members.Add(CreateTestTeamMember(CreateTestUser("user3"), team, memberId: "m3", isActive: false));

            team.HasMember("user1").Should().BeTrue(); // Aktywny user, aktywne członkostwo
            team.HasMember("user2").Should().BeFalse(); // Nieaktywny user, aktywne członkostwo
            team.HasMember("user3").Should().BeFalse(); // Aktywny user, nieaktywne członkostwo
            team.HasMember("non-existent-user").Should().BeFalse();
        }

        [Fact]
        public void HasOwner_ShouldReturnCorrectValue_ConsideringUserAndMembershipActivity()
        {
            var team = new Team();
            var activeOwner = CreateTestUser("owner1", isActive: true);
            var inactiveOwnerUser = CreateTestUser("owner2", isActive: false); // User nieaktywny
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
            var team = new Team();
            var activeUser = CreateTestUser("user1", isActive: true);
            var inactiveUser = CreateTestUser("user2", isActive: false);

            var membershipActive = CreateTestTeamMember(activeUser, team, memberId: "m1", isActive: true);
            var membershipWithInactiveUser = CreateTestTeamMember(inactiveUser, team, memberId: "m2", isActive: true);
            var inactiveMembership = CreateTestTeamMember(CreateTestUser("user3"), team, memberId: "m3", isActive: false);

            team.Members.Add(membershipActive);
            team.Members.Add(membershipWithInactiveUser);
            team.Members.Add(inactiveMembership);

            team.GetMembership("user1").Should().Be(membershipActive);
            team.GetMembership("user2").Should().BeNull(); // Bo User jest nieaktywny
            team.GetMembership("user3").Should().BeNull(); // Bo członkostwo jest nieaktywne
            team.GetMembership("non-existent-user").Should().BeNull();
        }

        [Fact]
        public void CanAddMoreMembers_ShouldRespectMaxMembersAndActiveCount()
        {
            var team = new Team { IsActive = true, Status = TeamStatus.Active };
            team.CanAddMoreMembers().Should().BeTrue();

            team.MaxMembers = 2;
            team.CanAddMoreMembers().Should().BeTrue();

            team.Members.Add(CreateTestTeamMember(CreateTestUser("u1", isActive: true), team, memberId: "m1", isActive: true));
            team.CanAddMoreMembers().Should().BeTrue();
            team.MemberCount.Should().Be(1);

            team.Members.Add(CreateTestTeamMember(CreateTestUser("u2", isActive: true), team, memberId: "m2", isActive: true));
            team.CanAddMoreMembers().Should().BeFalse();
            team.MemberCount.Should().Be(2);

            // Dodanie nieaktywnego członkostwa lub członkostwa z nieaktywnym użytkownikiem nie powinno blokować
            team.Members.Add(CreateTestTeamMember(CreateTestUser("u3", isActive: false), team, memberId: "m3", isActive: true)); // Nieaktywny user
            team.CanAddMoreMembers().Should().BeFalse(); // Nadal 2 aktywnych członków
            team.MemberCount.Should().Be(2);

            team.Members.First(m => m.Id == "m1").IsActive = false; // Dezaktywuj jedno z aktywnych członkostw
            team.CanAddMoreMembers().Should().BeTrue(); // Teraz jest miejsce
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
    }
}