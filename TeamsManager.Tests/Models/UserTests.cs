using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class UserTests
    {
        // Metoda pomocnicza do tworzenia domyślnego działu
        private Department CreateTestDepartment(string id = "dept-1", string name = "Test Department")
        {
            return new Department { Id = id, Name = name, IsActive = true };
        }

        // Metoda pomocnicza do tworzenia typu szkoły
        private SchoolType CreateTestSchoolType(string id = "stype-1", string shortName = "LO")
        {
            return new SchoolType { Id = id, ShortName = shortName, FullName = "Liceum Ogólnokształcące", IsActive = true };
        }

        [Fact]
        public void User_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var user = new User();

            // Sprawdzenie pól bezpośrednich
            user.Id.Should().Be(string.Empty);
            user.FirstName.Should().Be(string.Empty);
            user.LastName.Should().Be(string.Empty);
            user.UPN.Should().Be(string.Empty);
            user.Role.Should().Be(UserRole.Uczen); // Domyślna rola
            user.DepartmentId.Should().Be(string.Empty); // Domyślnie, jeśli Department nie jest ustawiany w konstruktorze Usera

            user.Phone.Should().BeNull();
            user.AlternateEmail.Should().BeNull();
            user.ExternalId.Should().BeNull();
            user.BirthDate.Should().BeNull();
            user.EmploymentDate.Should().BeNull();
            user.Position.Should().BeNull();
            user.Notes.Should().BeNull();
            user.IsSystemAdmin.Should().BeFalse();
            user.LastLoginDate.Should().BeNull();

            // Sprawdzenie pól z BaseEntity
            user.IsActive.Should().BeTrue();
            // user.CreatedDate.Should().Be(default(DateTime)); // Zależne od BaseEntity

            // Sprawdzenie właściwości nawigacyjnych i kolekcji
            user.Department.Should().BeNull();
            user.TeamMemberships.Should().NotBeNull().And.BeEmpty();
            user.SchoolTypeAssignments.Should().NotBeNull().And.BeEmpty();
            user.SupervisedSchoolTypes.Should().NotBeNull().And.BeEmpty();

            // Sprawdzenie wybranych właściwości obliczanych
            user.FullName.Should().Be(string.Empty); // Bo FirstName i LastName są puste
            user.DisplayName.Should().Be(string.Empty);
            user.Email.Should().Be(string.Empty); // Bo UPN jest pusty
            user.Initials.Should().Be(string.Empty);
            user.Age.Should().BeNull();
            user.YearsOfService.Should().BeNull();
            user.RoleDisplayName.Should().Be("Uczeń"); // Dla domyślnej roli Uczen
            user.ActiveMembershipsCount.Should().Be(0);
            user.OwnedTeamsCount.Should().Be(0);
            user.AssignedSchoolTypes.Should().NotBeNull().And.BeEmpty();
            user.CanManageTeams.Should().BeFalse(); // Bo rola Uczen
            user.CanManageUsers.Should().BeFalse(); // Bo rola Uczen
            user.HasAdminRights.Should().BeFalse(); // Bo rola Uczen i nie IsSystemAdmin
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Member); // Dla roli Uczen
        }

        [Fact]
        public void User_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var user = new User();
            var department = CreateTestDepartment();
            var now = DateTime.UtcNow;

            // Wykonanie
            user.Id = "user-xyz";
            user.FirstName = "Anna";
            user.LastName = "Projektantka";
            user.UPN = "anna.projekt@example.com";
            user.Role = UserRole.Nauczyciel;
            user.DepartmentId = department.Id;
            user.Department = department; // Ważne dla niektórych właściwości obliczanych
            user.Phone = "123-456-789";
            user.AlternateEmail = "ania.p@gmail.com";
            user.ExternalId = "EMP007";
            user.BirthDate = new DateTime(1990, 5, 15);
            user.EmploymentDate = new DateTime(2015, 9, 1);
            user.Position = "Starszy Nauczyciel";
            user.Notes = "Specjalista od .NET";
            user.IsSystemAdmin = true;
            user.LastLoginDate = now.AddHours(-1);
            user.IsActive = false; // Z BaseEntity
            user.CreatedDate = now.AddDays(-10);
            user.CreatedBy = "initializer";


            // Sprawdzenie
            user.Id.Should().Be("user-xyz");
            user.FirstName.Should().Be("Anna");
            user.LastName.Should().Be("Projektantka");
            user.UPN.Should().Be("anna.projekt@example.com");
            user.Role.Should().Be(UserRole.Nauczyciel);
            user.DepartmentId.Should().Be(department.Id);
            user.Department.Should().Be(department);
            user.Phone.Should().Be("123-456-789");
            user.AlternateEmail.Should().Be("ania.p@gmail.com");
            user.ExternalId.Should().Be("EMP007");
            user.BirthDate.Should().Be(new DateTime(1990, 5, 15));
            user.EmploymentDate.Should().Be(new DateTime(2015, 9, 1));
            user.Position.Should().Be("Starszy Nauczyciel");
            user.Notes.Should().Be("Specjalista od .NET");
            user.IsSystemAdmin.Should().BeTrue();
            user.LastLoginDate.Should().Be(now.AddHours(-1));
            user.IsActive.Should().BeFalse();
            user.CreatedDate.Should().Be(now.AddDays(-10));
            user.CreatedBy.Should().Be("initializer");
        }

        [Theory]
        [InlineData("Jan", "Kowalski", "Jan Kowalski")]
        [InlineData("Anna", "Nowak-Kowalska", "Anna Nowak-Kowalska")]
        [InlineData("", "Kowalski", "Kowalski")] // Imię puste
        [InlineData("Jan", "", "Jan")]         // Nazwisko puste
        [InlineData("", "", "")]               // Oba puste
        public void User_FullName_ShouldCombineFirstAndLastNameCorrectly(string firstName, string lastName, string expectedFullName)
        {
            var user = new User { FirstName = firstName, LastName = lastName };
            user.FullName.Should().Be(expectedFullName);
        }

        [Fact]
        public void User_DisplayNameAndEmail_ShouldReflectFullNameAndUPN()
        {
            var user = new User { FirstName = "Adam", LastName = "Nowicki", UPN = "adam.n@example.com" };
            user.DisplayName.Should().Be("Adam Nowicki");
            user.Email.Should().Be("adam.n@example.com");
        }

        [Theory]
        [InlineData("Piotr", "Zieliński", "PZ")]
        [InlineData("Anna", "", "A")]
        [InlineData("", "Kowalska", "K")]
        [InlineData("", "", "")]
        public void User_Initials_ShouldBeCalculatedCorrectly(string firstName, string lastName, string expectedInitials)
        {
            var user = new User { FirstName = firstName, LastName = lastName };
            user.Initials.Should().Be(expectedInitials);
        }

        [Fact]
        public void User_Age_ShouldBeCalculatedCorrectly()
        {
            var user = new User();
            user.Age.Should().BeNull(); // Brak daty urodzenia

            // Zakładając, że dzisiaj jest np. 2025-05-29
            var today = DateTime.Today; // Używamy DateTime.Today dla spójności z logiką Age
            user.BirthDate = today.AddYears(-30); // Urodziny dzisiaj
            user.Age.Should().Be(30);

            user.BirthDate = today.AddYears(-30).AddDays(1); // Urodziny jutro
            user.Age.Should().Be(29);

            user.BirthDate = today.AddYears(-30).AddDays(-1); // Urodziny wczoraj
            user.Age.Should().Be(30);
        }

        [Fact]
        public void User_YearsOfService_ShouldBeCalculatedCorrectly()
        {
            var user = new User();
            user.YearsOfService.Should().BeNull(); // Brak daty zatrudnienia

            // Zakładając, że dzisiaj jest np. 2025-05-29
            var today = DateTime.Today;
            user.EmploymentDate = today.AddYears(-5).AddDays(1); // Zatrudniony prawie 5 lat temu
            user.YearsOfService.Should().BeApproximately(5.0, 0.05);

            user.EmploymentDate = today.AddYears(-10);
            user.YearsOfService.Should().BeApproximately(10.0, 0.05);
        }

        [Fact]
        public void User_RoleDisplayName_ShouldFormatCorrectlyForAllRoles()
        {
            var user = new User();

            user.Role = UserRole.Uczen;
            user.RoleDisplayName.Should().Be("Uczeń");

            user.Role = UserRole.Sluchacz;
            user.RoleDisplayName.Should().Be("Słuchacz");

            user.Role = UserRole.Nauczyciel;
            user.RoleDisplayName.Should().Be("Nauczyciel");

            user.Role = UserRole.Wicedyrektor;
            user.RoleDisplayName.Should().Be("Wicedyrektor"); // Bez nadzorowanych typów szkół

            var schoolTypeLO = CreateTestSchoolType("st-lo", "LO");
            schoolTypeLO.IsActive = true;
            var schoolTypeTech = CreateTestSchoolType("st-tech", "Technikum");
            schoolTypeTech.IsActive = true;
            var schoolTypeInactive = CreateTestSchoolType("st-inactive", "NieaktywnyTyp");
            schoolTypeInactive.IsActive = false;

            user.SupervisedSchoolTypes.Add(schoolTypeLO);
            user.SupervisedSchoolTypes.Add(schoolTypeTech);
            user.SupervisedSchoolTypes.Add(schoolTypeInactive);
            user.RoleDisplayName.Should().Be("Wicedyrektor (LO, Technikum)"); // Powinno pokazywać tylko aktywne

            user.Role = UserRole.Dyrektor;
            user.RoleDisplayName.Should().Be("Dyrektor");
        }

        [Fact]
        public void User_PermissionProperties_ShouldReflectRoleAndAdminFlag()
        {
            var user = new User();

            // Uczeń
            user.Role = UserRole.Uczen;
            user.IsSystemAdmin = false;
            user.CanManageTeams.Should().BeFalse();
            user.CanManageUsers.Should().BeFalse();
            user.HasAdminRights.Should().BeFalse();
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Member);

            // Nauczyciel
            user.Role = UserRole.Nauczyciel;
            user.CanManageTeams.Should().BeTrue();
            user.CanManageUsers.Should().BeFalse();
            user.HasAdminRights.Should().BeFalse();
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Owner);

            // Wicedyrektor
            user.Role = UserRole.Wicedyrektor;
            user.CanManageTeams.Should().BeTrue();
            user.CanManageUsers.Should().BeTrue();
            user.HasAdminRights.Should().BeFalse(); // Nie jest jeszcze Dyrektorem ani SystemAdmin
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Owner);

            // Dyrektor
            user.Role = UserRole.Dyrektor;
            user.CanManageTeams.Should().BeTrue();
            user.CanManageUsers.Should().BeTrue();
            user.HasAdminRights.Should().BeTrue();
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Owner);

            // SystemAdmin (np. uczeń, ale z flagą admina)
            user.Role = UserRole.Uczen;
            user.IsSystemAdmin = true;
            user.CanManageTeams.Should().BeTrue();
            user.CanManageUsers.Should().BeTrue();
            user.HasAdminRights.Should().BeTrue();
            // DefaultTeamRole nadal powinno być Member, bo to wynika z roli w systemie edukacyjnym,
            // a nie z uprawnień administratora aplikacji.
            user.DefaultTeamRole.Should().Be(TeamMemberRole.Member);
        }

        [Fact]
        public void User_WhenAddingMemberships_ShouldUpdateCounts()
        {
            var user = new User { Id = "user-multi-team" };
            var team1 = new Team { Id = "t1", Status = TeamStatus.Active };
            var team2 = new Team { Id = "t2", Status = TeamStatus.Active };
            var teamArchived = new Team { Id = "t-arch", Status = TeamStatus.Archived };

            user.ActiveMembershipsCount.Should().Be(0);
            user.OwnedTeamsCount.Should().Be(0);

            var membership1 = new TeamMember { User = user, Team = team1, Role = TeamMemberRole.Owner, IsActive = true };
            user.TeamMemberships.Add(membership1);
            user.ActiveMembershipsCount.Should().Be(1);
            user.OwnedTeamsCount.Should().Be(1);

            var membership2 = new TeamMember { User = user, Team = team2, Role = TeamMemberRole.Member, IsActive = true };
            user.TeamMemberships.Add(membership2);
            user.ActiveMembershipsCount.Should().Be(2);
            user.OwnedTeamsCount.Should().Be(1); // Nadal jeden Owner

            var membershipArchivedTeam = new TeamMember { User = user, Team = teamArchived, Role = TeamMemberRole.Owner, IsActive = true };
            user.TeamMemberships.Add(membershipArchivedTeam);
            user.ActiveMembershipsCount.Should().Be(2); // Członkostwo w zarchiwizowanym zespole nie jest liczone jako "aktywne" członkostwo
            user.OwnedTeamsCount.Should().Be(1); // Podobnie dla posiadanych zespołów

            membership1.IsActive = false; // Dezaktywuj jedno członkostwo
            user.ActiveMembershipsCount.Should().Be(1);
            user.OwnedTeamsCount.Should().Be(0);
        }

        [Fact]
        public void User_AssignedSchoolTypes_ShouldFilterCorrectly()
        {
            var user = new User();
            var st1 = CreateTestSchoolType("st1", "LO");
            var st2 = CreateTestSchoolType("st2", "Tech");
            var st3_inactive = CreateTestSchoolType("st3", "Gim");
            st3_inactive.IsActive = false;

            user.SchoolTypeAssignments.Add(new UserSchoolType { User = user, SchoolType = st1, IsActive = true, IsCurrentlyActive = true });
            user.SchoolTypeAssignments.Add(new UserSchoolType { User = user, SchoolType = st2, IsActive = true, IsCurrentlyActive = false }); // Przypisanie nieaktywne
            user.SchoolTypeAssignments.Add(new UserSchoolType { User = user, SchoolType = st3_inactive, IsActive = true, IsCurrentlyActive = true }); // Typ szkoły nieaktywny

            var assignedTypes = user.AssignedSchoolTypes;
            assignedTypes.Should().HaveCount(1);
            assignedTypes.Should().Contain(st1);
            assignedTypes.Should().NotContain(st2);
            assignedTypes.Should().NotContain(st3_inactive);
        }

        // ===== Testy dla metod pomocniczych =====
        [Fact]
        public void User_IsAssignedToSchoolType_ShouldReturnCorrectly()
        {
            var user = new User();
            var st1 = CreateTestSchoolType("st1");
            user.SchoolTypeAssignments.Add(new UserSchoolType { UserId = user.Id, User = user, SchoolTypeId = st1.Id, SchoolType = st1, IsActive = true, IsCurrentlyActive = true });

            user.IsAssignedToSchoolType("st1").Should().BeTrue();
            user.IsAssignedToSchoolType("st-other").Should().BeFalse();
        }

        [Fact]
        public void User_SupervisesSchoolType_ShouldReturnCorrectly()
        {
            var user = new User();
            var st1 = CreateTestSchoolType("st1");
            st1.IsActive = true;
            user.SupervisedSchoolTypes.Add(st1);

            user.SupervisesSchoolType("st1").Should().BeTrue();
            user.SupervisesSchoolType("st-other").Should().BeFalse();
        }

        [Fact]
        public void User_IsMemberOfTeam_And_IsOwnerOfTeam_ShouldWorkCorrectly()
        {
            var user = new User { Id = "test-user" };
            var team1 = new Team { Id = "team1", Status = TeamStatus.Active };
            var team2 = new Team { Id = "team2", Status = TeamStatus.Active };
            var inactiveTeam = new Team { Id = "team-inactive", Status = TeamStatus.Archived };

            // Członek w team1
            user.TeamMemberships.Add(new TeamMember { User = user, UserId = user.Id, Team = team1, TeamId = team1.Id, Role = TeamMemberRole.Member, IsActive = true });
            // Właściciel w team2
            user.TeamMemberships.Add(new TeamMember { User = user, UserId = user.Id, Team = team2, TeamId = team2.Id, Role = TeamMemberRole.Owner, IsActive = true });
            // Członek w nieaktywnym zespole
            user.TeamMemberships.Add(new TeamMember { User = user, UserId = user.Id, Team = inactiveTeam, TeamId = inactiveTeam.Id, Role = TeamMemberRole.Member, IsActive = true });


            user.IsMemberOfTeam("team1").Should().BeTrue();
            user.IsOwnerOfTeam("team1").Should().BeFalse();

            user.IsMemberOfTeam("team2").Should().BeTrue(); // Właściciel jest też członkiem
            user.IsOwnerOfTeam("team2").Should().BeTrue();

            user.IsMemberOfTeam("team-nonexistent").Should().BeFalse();
            user.IsOwnerOfTeam("team-nonexistent").Should().BeFalse();

            user.IsMemberOfTeam("team-inactive").Should().BeFalse(); // Bo zespół jest nieaktywny
            user.IsOwnerOfTeam("team-inactive").Should().BeFalse();
        }

        [Fact]
        public void User_UpdateLastLogin_ShouldSetLastLoginDateToUtcNow()
        {
            var user = new User();
            DateTime? initialLoginDate = user.LastLoginDate;

            user.UpdateLastLogin();

            user.LastLoginDate.Should().NotBeNull();
            user.LastLoginDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            if (initialLoginDate.HasValue)
                user.LastLoginDate.Value.Should().BeAfter(initialLoginDate.Value);
        }
    }
}