using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class TeamIntegrationTests
    {
        // ===== METODY POMOCNICZE DO TWORZENIA ENCJI TESTOWYCH =====

        private Department CreateTestDepartment(string id = "dept-1", string name = "Test Department")
        {
            // W testach integracyjnych modelu, pola audytu mogą być mniej istotne
            // lub ustawiane przez symulowany DbContext, jeśli taki jest używany.
            // Dla uproszczenia, tutaj je pomijamy.
            return new Department
            {
                Id = id,
                Name = name,
                IsActive = true
            };
        }

        private User CreateTestUser(
            string id = "user-1",
            string upn = "test.user@example.com",
            Department? department = null,
            UserRole role = UserRole.Nauczyciel,
            bool isActive = true,
            string firstName = "Test",
            string lastName = ""
        )
        {
            var dept = department ?? CreateTestDepartment();
            var effectiveLastName = string.IsNullOrEmpty(lastName) ? $"User_{id}" : lastName;

            return new User
            {
                Id = id,
                FirstName = firstName,
                LastName = effectiveLastName,
                UPN = upn,
                Role = role,
                DepartmentId = dept.Id,
                Department = dept,
                IsActive = isActive
            };
        }

        private SchoolType CreateTestSchoolType(string id = "stype-1", string shortName = "LO", string fullName = "Liceum Ogólnokształcące")
        {
            return new SchoolType
            {
                Id = id,
                ShortName = shortName,
                FullName = fullName,
                IsActive = true
            };
        }

        private SchoolYear CreateTestSchoolYear(string id = "syear-1", string name = "2024/2025", bool isCurrent = true)
        {
            return new SchoolYear
            {
                Id = id,
                Name = name,
                StartDate = new DateTime(DateTime.UtcNow.Year, 9, 1),
                EndDate = new DateTime(DateTime.UtcNow.Year + 1, 6, 20),
                IsCurrent = isCurrent,
                IsActive = true
            };
        }

        private TeamTemplate CreateTestTeamTemplate(string id = "tpl-1", string name = "Standard Template", SchoolType? schoolType = null, string templateContent = "{SchoolType} - {Subject} - {Teacher}")
        {
            return new TeamTemplate
            {
                Id = id,
                Name = name,
                Template = templateContent,
                IsUniversal = schoolType == null,
                SchoolTypeId = schoolType?.Id,
                SchoolType = schoolType,
                IsActive = true
            };
        }

        private Team CreateBasicTeam(string id = "team-1", string displayName = "Test Team", User? owner = null)
        {
            var teamOwner = owner ?? CreateTestUser("owner-basic", "owner.basic@example.com");
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                Owner = teamOwner.UPN,
                Status = TeamStatus.Active // Domyślnie aktywny
                // IsActive jest teraz właściwością obliczeniową
            };
        }

        private TeamMember CreateTestTeamMember(User user, Team team, TeamMemberRole role = TeamMemberRole.Member, string memberIdPrefix = "member", bool isActive = true)
        {
            return new TeamMember
            {
                Id = $"{memberIdPrefix}-{user.Id}-{team.Id}",
                User = user,
                UserId = user.Id,
                Team = team,
                TeamId = team.Id,
                Role = role,
                AddedDate = DateTime.UtcNow.AddDays(-1),
                IsActive = isActive // Aktywność samego członkostwa
            };
        }

        private Channel CreateTestChannel(string id, Team team, string displayName = "General", bool isGeneral = false)
        {
            // Zakładamy, że kanał jest aktywny, jeśli nie podano inaczej
            return new Channel
            {
                Id = id,
                DisplayName = displayName,
                Team = team,
                TeamId = team.Id,
                IsGeneral = isGeneral,
                Status = ChannelStatus.Active, // Domyślnie kanał aktywny
            };
        }

        // ===== TESTY INTEGRACYJNE DLA TEAM I POWIĄZANYCH ENCJI =====

        [Fact]
        public void Team_WhenAssociatedWithSchoolType_ShouldReflectRelationship()
        {
            // Przygotowanie
            var schoolType = CreateTestSchoolType("st-tech", "TECH", "Technikum Mechaniczne");
            var team = CreateBasicTeam("team-tech", "Klasa 1Tm - Mechanicy");

            // Wykonanie
            team.SchoolType = schoolType;
            team.SchoolTypeId = schoolType.Id;
            schoolType.Teams.Add(team);

            // Sprawdzenie
            team.SchoolType.Should().NotBeNull();
            team.SchoolType.Should().Be(schoolType);
            team.SchoolTypeId.Should().Be(schoolType.Id);
            schoolType.Teams.Should().Contain(team);
        }

        [Fact]
        public void Team_WhenAssociatedWithSchoolYear_ShouldReflectRelationship()
        {
            // Przygotowanie
            var schoolYear = CreateTestSchoolYear("sy-2324", "2023/2024");
            var team = CreateBasicTeam("team-hist", "Historia 2023/2024");

            // Wykonanie
            team.SchoolYear = schoolYear;
            team.SchoolYearId = schoolYear.Id;
            schoolYear.Teams.Add(team);

            // Sprawdzenie
            team.SchoolYear.Should().NotBeNull();
            team.SchoolYear.Should().Be(schoolYear);
            team.SchoolYearId.Should().Be(schoolYear.Id);
            schoolYear.Teams.Should().Contain(team);
        }

        [Fact]
        public void Team_WhenCreatedFromTemplate_ShouldReflectRelationship()
        {
            // Przygotowanie
            var template = CreateTestTeamTemplate("tpl-main", "Główny Szablon Edukacyjny");
            var team = CreateBasicTeam("team-tpl", "Zespół z Szablonu Głównego");

            // Wykonanie
            team.Template = template;
            team.TemplateId = template.Id;
            template.Teams.Add(team);

            // Sprawdzenie
            team.Template.Should().NotBeNull();
            team.Template.Should().Be(template);
            team.TemplateId.Should().Be(template.Id);
            template.Teams.Should().Contain(team);
        }

        [Fact]
        public void Team_WithFullContext_MembersChannelsAndAssociations_ShouldBeConsistent()
        {
            // Przygotowanie
            var department = CreateTestDepartment();
            var mainOwner = CreateTestUser("mainowner", "main.owner@example.com", department, UserRole.Nauczyciel);
            var memberUser = CreateTestUser("memberuser", "member.user@example.com", department, UserRole.Uczen);

            var schoolType = CreateTestSchoolType();
            var schoolYear = CreateTestSchoolYear();
            var template = CreateTestTeamTemplate(schoolType: schoolType);

            var team = CreateBasicTeam("team-complex", "Kompleksowy Zespół Edukacyjny", mainOwner);
            team.Status = TeamStatus.Active; // Jawne ustawienie dla testu, chociaż jest domyślne
            team.Description = "Zespół testujący wszystkie powiązania";
            team.SchoolType = schoolType;
            team.SchoolTypeId = schoolType.Id;
            team.SchoolYear = schoolYear;
            team.SchoolYearId = schoolYear.Id;
            team.Template = template;
            team.TemplateId = template.Id;
            team.AcademicYear = schoolYear.Name;
            team.Semester = "I Semestr";

            var ownerMembership = CreateTestTeamMember(mainOwner, team, TeamMemberRole.Owner, "tm-owner");
            var studentMembership = CreateTestTeamMember(memberUser, team, TeamMemberRole.Member, "tm-student");

            var generalChannel = CreateTestChannel("ch-general", team, "Ogólny", true);
            var projectChannel = CreateTestChannel("ch-project", team, "Projekty");

            // Wykonanie
            team.Members.Add(ownerMembership);
            team.Members.Add(studentMembership);
            mainOwner.TeamMemberships.Add(ownerMembership);
            memberUser.TeamMemberships.Add(studentMembership);

            team.Channels.Add(generalChannel);
            team.Channels.Add(projectChannel);

            schoolType.Teams.Add(team);
            schoolYear.Teams.Add(team);
            template.Teams.Add(team);

            // Sprawdzenie podstawowych asocjacji zespołu
            team.IsActive.Should().BeTrue(); // Test nowego IsActive
            team.SchoolType.Should().Be(schoolType);
            // ... (reszta asercji jak wcześniej) ...
            team.MemberCount.Should().Be(2); // Zakładamy, że wszyscy użytkownicy i członkostwa są aktywne
            team.AllActiveUsers.Should().HaveCount(2).And.Contain(mainOwner).And.Contain(memberUser);
        }


        [Fact]
        public void User_WhenAssignedToMultipleTeams_ShouldReflectInUserMemberships()
        {
            // Przygotowanie
            var user = CreateTestUser("user-multi", "multi.team@example.com");
            var team1 = CreateBasicTeam("team-A", "Zespół Alfa"); // Domyślnie aktywny
            var team2 = CreateBasicTeam("team-B", "Zespół Beta"); // Domyślnie aktywny

            var membership1 = CreateTestTeamMember(user, team1, TeamMemberRole.Owner);
            var membership2 = CreateTestTeamMember(user, team2, TeamMemberRole.Member);

            // Wykonanie
            user.TeamMemberships.Add(membership1);
            user.TeamMemberships.Add(membership2);
            team1.Members.Add(membership1);
            team2.Members.Add(membership2);

            // Sprawdzenie
            user.TeamMemberships.Should().HaveCount(2);
            user.ActiveMembershipsCount.Should().Be(2); // Obie drużyny są aktywne
            user.OwnedTeamsCount.Should().Be(1);

            team1.HasOwner(user.Id).Should().BeTrue();
            team2.HasMember(user.Id).Should().BeTrue();
            team2.HasOwner(user.Id).Should().BeFalse();
        }


        [Fact]
        public void Team_IsFullyOperational_ShouldReflectAllConditions()
        {
            // Przygotowanie
            var team = CreateBasicTeam(displayName: "Operational Check Team");
            var today = DateTime.Today;

            // Scenariusz 1: Wszystko aktywne i w zakresie dat
            team.Status = TeamStatus.Active; // Ustawia IsActive = true
            team.StartDate = today.AddDays(-1);
            team.EndDate = today.AddDays(1);
            team.IsFullyOperational.Should().BeTrue();

            // Scenariusz 2: Status = Archived (co implikuje IsActive = false)
            team.Status = TeamStatus.Archived;
            team.IsFullyOperational.Should().BeFalse();
            team.Status = TeamStatus.Active; // Reset

            // Scenariusz 3: StartDate w przyszłości
            team.Status = TeamStatus.Active;
            team.StartDate = today.AddDays(1);
            team.IsFullyOperational.Should().BeFalse();
            team.StartDate = today.AddDays(-1); // Reset

            // Scenariusz 4: EndDate w przeszłości
            team.EndDate = today.AddDays(-1);
            team.IsFullyOperational.Should().BeFalse();
        }


        [Fact]
        public void Team_WithComplexNameAndDescriptionFromTemplate_AndArchival()
        {
            // Przygotowanie
            var schoolType = CreateTestSchoolType(shortName: "CKZiU");
            var owner = CreateTestUser(upn: "koordynator@example.com", firstName: "Emil", lastName: "Kacprzak");
            var template = CreateTestTeamTemplate(name: "Szablon Kursu Zawodowego", schoolType: schoolType, templateContent: "{TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}");
            var team = CreateBasicTeam(displayName: "Początkowa Nazwa Do Zmiany", owner: owner);
            team.Template = template;

            var templateValues = new Dictionary<string, string>
            {
                {"TypSzkoly", schoolType.ShortName},
                {"Oddzial", "DRM.04-A"},
                {"Przedmiot", "Maszyny i urządzenia"},
                {"Nauczyciel", owner.FullName}
            };

            team.DisplayName = template.GenerateTeamName(templateValues);
            team.Description = $"Zespół dla przedmiotu '{templateValues["Przedmiot"]}' prowadzonego przez {templateValues["Nauczyciel"]}.";

            var initialDisplayName = team.DisplayName;
            var initialDescription = team.Description;
            string expectedName = $"{schoolType.ShortName} {templateValues["Oddzial"]} - {templateValues["Przedmiot"]} - {templateValues["Nauczyciel"]}";
            initialDisplayName.Should().Be(expectedName);

            // Wykonanie - Archiwizacja
            team.Archive("Kurs zakończony", "admin@example.com");

            // Sprawdzenie po archiwizacji
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse(); // Nowa asercja dla obliczeniowego IsActive
            team.DisplayName.Should().Be($"ARCHIWALNY - {initialDisplayName}");
            team.Description.Should().Be($"ARCHIWALNY - {initialDescription}");
            team.DisplayNameWithStatus.Should().Be($"ARCHIWALNY - {initialDisplayName}");

            // Wykonanie - Przywrócenie
            team.Restore("admin@example.com");

            // Sprawdzenie po przywróceniu
            team.Status.Should().Be(TeamStatus.Active);
            team.IsActive.Should().BeTrue(); // Nowa asercja dla obliczeniowego IsActive
            team.DisplayName.Should().Be(initialDisplayName);
            team.Description.Should().Be(initialDescription);
            team.DisplayNameWithStatus.Should().Be(initialDisplayName);
        }
    }
}