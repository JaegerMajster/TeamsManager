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
            return new Department
            {
                Id = id,
                Name = name,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test_setup"
            };
        }

        private User CreateTestUser(
            string id = "user-1",
            string upn = "test.user@example.com",
            Department? department = null,
            UserRole role = UserRole.Nauczyciel,
            bool isActive = true,
            string firstName = "Test", // DODANY PARAMETR z wartością domyślną
            string lastName = ""      // DODANY PARAMETR z wartością domyślną (lub generuj jak wcześniej)
        )
        {
            var dept = department ?? CreateTestDepartment();
            // Jeśli lastName ma być domyślnie generowane, gdy nie podano:
            var effectiveLastName = string.IsNullOrEmpty(lastName) ? $"User_{id}" : lastName;

            return new User
            {
                Id = id,
                FirstName = firstName, // Użycie parametru
                LastName = effectiveLastName, // Użycie parametru lub wygenerowanej wartości
                UPN = upn,
                Role = role,
                DepartmentId = dept.Id,
                Department = dept,
                IsActive = isActive,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test_setup"
            };
        }

        private SchoolType CreateTestSchoolType(string id = "stype-1", string shortName = "LO", string fullName = "Liceum Ogólnokształcące")
        {
            return new SchoolType
            {
                Id = id,
                ShortName = shortName,
                FullName = fullName,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test_setup"
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
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test_setup"
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
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "test_setup"
            };
        }

        private Team CreateBasicTeam(string id = "team-1", string displayName = "Test Team", User? owner = null)
        {
            var teamOwner = owner ?? CreateTestUser("owner-basic", "owner.basic@example.com");
            return new Team
            {
                Id = id,
                DisplayName = displayName,
                Owner = teamOwner.UPN, // UPN właściciela
                Status = TeamStatus.Active,
                IsActive = true,
                CreatedBy = "test_setup",
                CreatedDate = DateTime.UtcNow
            };
        }

        private TeamMember CreateTestTeamMember(User user, Team team, TeamMemberRole role = TeamMemberRole.Member, string memberIdPrefix = "member", bool isActive = true) // DODANY PARAMETR isActive
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
                IsActive = isActive, // Użycie parametru metody
                CreatedBy = "test_setup",
                CreatedDate = DateTime.UtcNow
            };
        }

        private Channel CreateTestChannel(string id, Team team, string displayName = "General", bool isGeneral = false)
        {
            return new Channel
            {
                Id = id,
                DisplayName = displayName,
                Team = team,
                TeamId = team.Id,
                IsGeneral = isGeneral,
                Status = ChannelStatus.Active,
                IsActive = true,
                CreatedBy = "test_setup",
                CreatedDate = DateTime.UtcNow
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
            // W rzeczywistej aplikacji EF Core zarządzałby kolekcją zwrotną,
            // dla testu w pamięci możemy ją ustawić, jeśli właściwość SchoolType.Teams jest używana w asercjach
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

            // Wykonanie - Symulacja dodawania przez serwisy/repozytoria (ustawianie relacji dwukierunkowych)
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
            team.SchoolType.Should().Be(schoolType);
            team.SchoolYear.Should().Be(schoolYear);
            team.Template.Should().Be(template);
            team.Owner.Should().Be(mainOwner.UPN);

            // Sprawdzenie członków
            team.Members.Should().HaveCount(2);
            team.MemberCount.Should().Be(2);
            team.OwnerCount.Should().Be(1);
            team.Owners.Should().Contain(mainOwner);
            team.RegularMemberCount.Should().Be(1);
            team.RegularMembers.Should().Contain(memberUser);
            team.AllActiveUsers.Should().HaveCount(2).And.Contain(mainOwner).And.Contain(memberUser);
            team.HasOwner(mainOwner.Id).Should().BeTrue();
            team.HasMember(memberUser.Id).Should().BeTrue();

            // Sprawdzenie kanałów
            team.Channels.Should().HaveCount(2);
            team.ChannelCount.Should().Be(2);
            generalChannel.Team.Should().Be(team);
            projectChannel.Team.Should().Be(team);

            // Sprawdzenie, czy główny właściciel zespołu (z Team.Owner) jest faktycznie wśród członków z rolą Owner
            var ownerMember = team.Members.FirstOrDefault(m => m.User?.UPN == team.Owner && m.Role == TeamMemberRole.Owner);
            ownerMember.Should().NotBeNull();
            ownerMember?.User.Should().Be(mainOwner);

            // Sprawdzenie relacji zwrotnych
            mainOwner.TeamMemberships.Should().Contain(ownerMembership);
            memberUser.TeamMemberships.Should().Contain(studentMembership);
            schoolType.Teams.Should().Contain(team);
            schoolYear.Teams.Should().Contain(team);
            template.Teams.Should().Contain(team);
        }

        // Wcześniejsze testy z TeamIntegrationTests.cs (te, które nie są duplikatami lub są nadal relevantne):
        // - Team_WhenAddingMembers_ShouldMaintainCorrectRelationships (został rozbudowany w Team_WithFullContext)
        // - Team_WhenAddingChannels_ShouldMaintainCorrectRelationships (został rozbudowany w Team_WithFullContext)
        // - User_WhenInMultipleTeams_ShouldMaintainAllMemberships (ten test bardziej pasuje do UserIntegrationTests.cs lub UserTests.cs, ale można go tu dostosować)

        [Fact]
        public void User_WhenAssignedToMultipleTeams_ShouldReflectInUserMemberships()
        {
            // Przygotowanie
            var user = CreateTestUser("user-multi", "multi.team@example.com");
            var team1 = CreateBasicTeam("team-A", "Zespół Alfa");
            var team2 = CreateBasicTeam("team-B", "Zespół Beta");

            var membership1 = CreateTestTeamMember(user, team1, TeamMemberRole.Owner);
            var membership2 = CreateTestTeamMember(user, team2, TeamMemberRole.Member);

            // Wykonanie
            user.TeamMemberships.Add(membership1);
            user.TeamMemberships.Add(membership2);
            // Symulujemy też dodanie do kolekcji zespołów (EF Core by to zrobił)
            team1.Members.Add(membership1);
            team2.Members.Add(membership2);


            // Sprawdzenie
            user.TeamMemberships.Should().HaveCount(2)
                .And.Contain(membership1)
                .And.Contain(membership2);

            user.ActiveMembershipsCount.Should().Be(2);
            user.OwnedTeamsCount.Should().Be(1); // Właściciel w team1

            team1.HasOwner(user.Id).Should().BeTrue();
            team2.HasMember(user.Id).Should().BeTrue();
            team2.HasOwner(user.Id).Should().BeFalse();
        }

        // ===== DODATKOWE TESTY INTEGRACYJNE =====

        [Fact]
        public void SchoolType_WhenManagingTeacherAssignments_ShouldReflectInUserAndSchoolType()
        {
            // Przygotowanie
            var schoolType = CreateTestSchoolType("st-math", "Matematyczny");
            var teacher1 = CreateTestUser("teacher-math1", "math.teacher1@example.com");
            var teacher2 = CreateTestUser("teacher-math2", "math.teacher2@example.com");

            var assignment1 = new UserSchoolType
            {
                Id = "ust-m1",
                User = teacher1,
                UserId = teacher1.Id,
                SchoolType = schoolType,
                SchoolTypeId = schoolType.Id,
                AssignedDate = DateTime.UtcNow,
                IsCurrentlyActive = true,
                IsActive = true, // Z BaseEntity
                CreatedBy = "test_setup"
            };

            var assignment2 = new UserSchoolType
            {
                Id = "ust-m2",
                User = teacher2,
                UserId = teacher2.Id,
                SchoolType = schoolType,
                SchoolTypeId = schoolType.Id,
                AssignedDate = DateTime.UtcNow,
                IsCurrentlyActive = true,
                IsActive = true,
                CreatedBy = "test_setup"
            };

            // Wykonanie - Symulacja dodania przypisań
            schoolType.TeacherAssignments.Add(assignment1);
            schoolType.TeacherAssignments.Add(assignment2);
            teacher1.SchoolTypeAssignments.Add(assignment1);
            teacher2.SchoolTypeAssignments.Add(assignment2);

            // Sprawdzenie
            schoolType.TeacherAssignments.Should().HaveCount(2);
            schoolType.AssignedTeachersCount.Should().Be(2); // Zakładając, że logika w SchoolType jest poprawna
            schoolType.AssignedTeachers.Should().Contain(teacher1).And.Contain(teacher2);

            teacher1.SchoolTypeAssignments.Should().Contain(assignment1);
            teacher1.AssignedSchoolTypes.Should().Contain(schoolType); // Zakładając, że AssignedSchoolTypes w User działa poprawnie
            teacher2.IsAssignedToSchoolType(schoolType.Id).Should().BeTrue();
        }

        [Fact]
        public void User_WhenSupervisingSchoolTypes_ShouldReflectRelationship()
        {
            // Przygotowanie
            var viceDirector = CreateTestUser("vd-1", "vd1@example.com", role: UserRole.Wicedyrektor);
            var schoolType1 = CreateTestSchoolType("st-a", "Typ A");
            var schoolType2 = CreateTestSchoolType("st-b", "Typ B");

            // Wykonanie - Symulacja przypisania nadzoru
            viceDirector.SupervisedSchoolTypes.Add(schoolType1);
            viceDirector.SupervisedSchoolTypes.Add(schoolType2);
            // W rzeczywistości EF Core zarządzałoby tabelą pośrednią UserSchoolTypeSupervision
            // Dla testu w pamięci, jeśli SchoolType.SupervisingViceDirectors jest istotne dla asercji, też byśmy je ustawili.
            schoolType1.SupervisingViceDirectors.Add(viceDirector);
            schoolType2.SupervisingViceDirectors.Add(viceDirector);


            // Sprawdzenie
            viceDirector.SupervisedSchoolTypes.Should().HaveCount(2).And.Contain(schoolType1).And.Contain(schoolType2);
            viceDirector.SupervisesSchoolType(schoolType1.Id).Should().BeTrue();
            viceDirector.SupervisesSchoolType(schoolType2.Id).Should().BeTrue();
            viceDirector.SupervisesSchoolType("st-nonexistent").Should().BeFalse();

            // Sprawdzenie roli wyświetlanej
            viceDirector.RoleDisplayName.Should().Contain(schoolType1.ShortName).And.Contain(schoolType2.ShortName);

            schoolType1.SupervisingViceDirectors.Should().Contain(viceDirector);
            schoolType2.SupervisingViceDirectors.Should().Contain(viceDirector);
        }

        [Fact]
        public void Subject_WhenAssigningTeachers_ShouldReflectInUserAndSubject()
        {
            // Przygotowanie
            var subject = new Subject { Id = "subj-cs", Name = "Computer Science", IsActive = true, CreatedBy = "test_setup" };
            var teacher1 = CreateTestUser("teacher-cs1", "cs.teacher1@example.com");
            var teacher2 = CreateTestUser("teacher-cs2", "cs.teacher2@example.com");

            var assignment1 = new UserSubject
            {
                Id = "usj-cs1",
                User = teacher1,
                UserId = teacher1.Id,
                Subject = subject,
                SubjectId = subject.Id,
                AssignedDate = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = "test_setup"
            };

            var assignment2 = new UserSubject
            {
                Id = "usj-cs2",
                User = teacher2,
                UserId = teacher2.Id,
                Subject = subject,
                SubjectId = subject.Id,
                AssignedDate = DateTime.UtcNow,
                IsActive = true,
                CreatedBy = "test_setup"
            };

            // Wykonanie - Symulacja dodania przypisań
            subject.TeacherAssignments.Add(assignment1);
            subject.TeacherAssignments.Add(assignment2);
            teacher1.TaughtSubjects.Add(assignment1);
            teacher2.TaughtSubjects.Add(assignment2);

            // Sprawdzenie
            subject.TeacherAssignments.Should().HaveCount(2);
            // Jeśli Subject miałby właściwość obliczaną np. AssignedTeachers, można by ją tu testować
            // np. subject.AssignedTeachers.Should().HaveCount(2).And.Contain(teacher1).And.Contain(teacher2);

            teacher1.TaughtSubjects.Should().Contain(assignment1);
            // Jeśli User miałby właściwość obliczaną np. SubjectsUserTeaches, można by ją tu testować
            // np. teacher1.SubjectsUserTeaches.Should().Contain(subject);
            teacher2.TaughtSubjects.Should().Contain(assignment2);
        }

        [Fact]
        public void Team_ShortDescription_ShouldBeCorrect_WithVariousAssociations()
        {
            // Przygotowanie
            var team = CreateBasicTeam(displayName: "Testowy Zespół");
            var user1 = CreateTestUser("u1");
            var user2 = CreateTestUser("u2");

            // Scenariusz 1: Pusty zespół
            team.ShortDescription.Should().Be("Zespół");

            // Scenariusz 2: Z rokiem akademickim
            team.AcademicYear = "2025/2026";
            team.ShortDescription.Should().Be("2025/2026");

            // Scenariusz 3: Z semestrem
            team.Semester = "Letni";
            team.ShortDescription.Should().Be("2025/2026 • Letni");

            // Scenariusz 4: Z typem szkoły
            var schoolType = CreateTestSchoolType(shortName: "GIM", fullName: "Gimnazjum");
            team.SchoolType = schoolType;
            team.ShortDescription.Should().Be("2025/2026 • Letni • GIM");

            // Scenariusz 5: Z członkami
            team.Members.Add(CreateTestTeamMember(user1, team, isActive: true)); // User domyślnie aktywny
            team.ShortDescription.Should().Be("2025/2026 • Letni • GIM • 1 osób");

            team.Members.Add(CreateTestTeamMember(user2, team, isActive: true));
            team.ShortDescription.Should().Be("2025/2026 • Letni • GIM • 2 osób");

            // Scenariusz 6: Członek nieaktywny (nie powinien być liczony przez MemberCount)
            team.Members.First().IsActive = false; // Dezaktywuj członkostwo user1
            team.ShortDescription.Should().Be("2025/2026 • Letni • GIM • 1 osób");
        }

        // ===== CZĘŚĆ 3: ZAAWANSOWANE SCENARIUSZE INTEGRACYJNE DLA TEAM =====

        [Fact]
        public void Team_IsFullyOperational_ShouldReflectAllConditions()
        {
            // Przygotowanie
            var team = CreateBasicTeam(displayName: "Operational Check Team");
            var today = DateTime.Today;

            // Scenariusz 1: Wszystko aktywne i w zakresie dat
            team.IsActive = true; // BaseEntity.IsActive
            team.Status = TeamStatus.Active;
            team.StartDate = today.AddDays(-1);
            team.EndDate = today.AddDays(1);
            team.IsFullyOperational.Should().BeTrue();

            // Scenariusz 2: BaseEntity.IsActive = false
            team.IsActive = false;
            team.IsFullyOperational.Should().BeFalse();
            team.IsActive = true; // Reset

            // Scenariusz 3: Status = Archived
            team.Status = TeamStatus.Archived;
            team.IsFullyOperational.Should().BeFalse();
            team.Status = TeamStatus.Active; // Reset

            // Scenariusz 4: StartDate w przyszłości
            team.StartDate = today.AddDays(1);
            team.IsFullyOperational.Should().BeFalse();
            team.StartDate = today.AddDays(-1); // Reset

            // Scenariusz 5: EndDate w przeszłości
            team.EndDate = today.AddDays(-1);
            team.IsFullyOperational.Should().BeFalse();
        }

        [Theory]
        [InlineData(-10, 20, 33.3)]  // Rozpoczęty, w trakcie (10 dni z 30)
        [InlineData(0, 29, 0.0)]     // Rozpoczyna się dzisiaj (0 dni z 30, jeśli liczymy pełne dni)
        [InlineData(-29, 0, 100.0)]  // Kończy się dzisiaj (29 dni z 29)
        [InlineData(-40, -10, 100.0)] // Zakończony
        [InlineData(10, 40, 0.0)]    // W przyszłości
        [InlineData(-10, -5, 100.0)] // Zakończony (EndDate < StartDate, ale StartDate w przeszłości)
        [InlineData(10, 5, 0.0)]     // W przyszłości (EndDate < StartDate)
        public void Team_CompletionPercentage_ShouldBeCorrectForDateRanges(int startOffsetDays, int endOffsetDays, double expectedPercentage)
        {
            // Przygotowanie
            var team = CreateBasicTeam();
            var today = DateTime.Today;
            team.StartDate = today.AddDays(startOffsetDays);
            team.EndDate = today.AddDays(endOffsetDays);

            // Sprawdzenie
            team.CompletionPercentage.Should().BeApproximately(expectedPercentage, 0.1);
        }

        [Fact]
        public void Team_CompletionPercentage_ShouldHandleNullDates()
        {
            var team = CreateBasicTeam();
            team.StartDate = null;
            team.EndDate = null;
            team.CompletionPercentage.Should().BeNull();

            team.StartDate = DateTime.Today;
            team.EndDate = null;
            team.CompletionPercentage.Should().BeNull();

            team.StartDate = null;
            team.EndDate = DateTime.Today;
            team.CompletionPercentage.Should().BeNull();
        }


        [Fact]
        public void Team_MaxMembersAndCapacity_ShouldInteractCorrectly()
        {
            // Przygotowanie
            var team = CreateBasicTeam();
            var user1 = CreateTestUser("u1");
            var user2 = CreateTestUser("u2");
            var user3 = CreateTestUser("u3");

            team.MaxMembers = 2;

            // Wykonanie i Sprawdzenie
            team.CanAddMoreMembers().Should().BeTrue();
            team.IsAtCapacity.Should().BeFalse();
            team.CapacityPercentage.Should().BeApproximately(0, 0.1);

            team.Members.Add(CreateTestTeamMember(user1, team));
            team.CanAddMoreMembers().Should().BeTrue();
            team.IsAtCapacity.Should().BeFalse();
            team.CapacityPercentage.Should().BeApproximately(50.0, 0.1);

            team.Members.Add(CreateTestTeamMember(user2, team));
            team.CanAddMoreMembers().Should().BeFalse();
            team.IsAtCapacity.Should().BeTrue();
            team.CapacityPercentage.Should().BeApproximately(100.0, 0.1);

            // Próba dodania kolejnego nie powinna zmienić MemberCount, jeśli logika serwisu by na to nie pozwoliła
            // Tutaj testujemy tylko właściwości obliczane na podstawie kolekcji Members
            team.Members.Add(CreateTestTeamMember(user3, team));
            team.CanAddMoreMembers().Should().BeFalse(); // Nadal false, bo MemberCount będzie 3, a MaxMembers 2
            team.IsAtCapacity.Should().BeTrue();      // Nadal true
            team.CapacityPercentage.Should().BeApproximately(150.0, 0.1); // MemberCount (3) / MaxMembers (2)
        }

        // Ten test jest bardziej koncepcyjny, bo OperationHistory jest zwykle tworzone przez serwis,
        // ale możemy zasymulować powiązanie.
        [Fact]
        public void Team_WhenOperationIsLogged_ItCanBeRelatedIfModelAllows()
        {
            // Przygotowanie
            var team = CreateBasicTeam(id: "team-op-hist");
            var userPerformingOperation = CreateTestUser(id: "admin-user");

            var operation = new OperationHistory
            {
                Id = "op-hist-1",
                Type = OperationType.TeamCreated,
                TargetEntityType = nameof(Team),
                TargetEntityId = team.Id,
                TargetEntityName = team.DisplayName,
                Status = OperationStatus.Completed,
                CreatedBy = userPerformingOperation.UPN, // Kto wykonał operację
                CreatedDate = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
                CompletedAt = DateTime.UtcNow
            };

            // Założenie: W przyszłości moglibyśmy chcieć dodać do Team kolekcję OperationHistory
            // np. public List<OperationHistory> RelatedOperations { get; set; } = new List<OperationHistory>();
            // Na razie Team nie ma takiej właściwości, więc ten test jest bardziej teoretyczny
            // lub sprawdza, czy możemy ręcznie powiązać koncepcyjnie.

            // Sprawdzenie (koncepcyjne)
            operation.TargetEntityId.Should().Be(team.Id);
            operation.TargetEntityType.Should().Be(nameof(Team));
            operation.Type.Should().Be(OperationType.TeamCreated);

            // Jeśli Team miałby kolekcję operacji:
            // team.RelatedOperations.Add(operation);
            // team.RelatedOperations.Should().Contain(operation);
        }
        // ===== CZĘŚĆ 4: DODATKOWE SCENARIUSZE INTEGRACYJNE I BRZEGOWE DLA TEAM =====

        [Fact]
        public void Team_WithOwnerNotExistingAsMember_ShouldStillBeValidButOwnerCountIsZero()
        {
            // Przygotowanie
            // Ten scenariusz testuje, co się dzieje, gdy UPN w Team.Owner
            // nie odpowiada żadnemu aktywnemu członkowi z rolą Owner.
            // W praktyce, logika serwisowa powinna zapewnić, że właściciel jest zawsze członkiem.
            var ownerUserNotInMembers = CreateTestUser("ghostowner", "ghost.owner@example.com");
            var team = CreateBasicTeam("team-ghost-owner", "Zespół z Właścicielem Widmo", ownerUserNotInMembers);

            var memberUser = CreateTestUser("memberonly", "member.only@example.com");
            var studentMembership = CreateTestTeamMember(memberUser, team, TeamMemberRole.Member, "tm-student-ghost");
            team.Members.Add(studentMembership);
            memberUser.TeamMemberships.Add(studentMembership);


            // Sprawdzenie
            team.Owner.Should().Be(ownerUserNotInMembers.UPN);
            team.OwnerCount.Should().Be(0); // Bo UPN z Team.Owner nie jest aktywnym członkiem-właścicielem
            team.Owners.Should().BeEmpty();
            team.HasOwner(ownerUserNotInMembers.Id).Should().BeFalse(); // Bo nie ma go w Members z rolą Owner
        }

        [Fact]
        public void Team_WhenAllMembersAreInactive_CountsShouldBeZero()
        {
            // Przygotowanie
            var team = CreateBasicTeam("team-all-inactive", "Zespół z Nieaktywnymi Członkami");
            var user1 = CreateTestUser("u1");
            var user2 = CreateTestUser("u2");

            team.Members.Add(CreateTestTeamMember(user1, team, TeamMemberRole.Owner, memberIdPrefix: "m", isActive: false)); // Członkostwo nieaktywne
            team.Members.Add(CreateTestTeamMember(user2, team, TeamMemberRole.Member, memberIdPrefix: "m", isActive: false)); // Członkostwo nieaktywne

            // Sprawdzenie
            team.MemberCount.Should().Be(0);
            team.OwnerCount.Should().Be(0);
            team.RegularMemberCount.Should().Be(0);
            team.AllActiveUsers.Should().BeEmpty();
            team.Owners.Should().BeEmpty();
            team.RegularMembers.Should().BeEmpty();
        }

        [Fact]
        public void Team_WhenAssociatedEntitiesAreInactive_ShouldReflectInCountsAndLists()
        {
            // Przygotowanie
            var schoolTypeInactive = CreateTestSchoolType(id: "st-inactive", shortName: "OLD");
            schoolTypeInactive.IsActive = false; // Typ szkoły nieaktywny

            var schoolYearInactive = CreateTestSchoolYear(id: "sy-inactive", name: "2000/2001");
            schoolYearInactive.IsActive = false; // Rok szkolny nieaktywny

            var templateInactive = CreateTestTeamTemplate(id: "tpl-inactive", name: "Old Template");
            templateInactive.IsActive = false; // Szablon nieaktywny

            var owner = CreateTestUser();
            var team = CreateBasicTeam("team-with-inactive-refs", "Test Inactive Refs", owner);
            team.SchoolType = schoolTypeInactive;
            team.SchoolTypeId = schoolTypeInactive.Id;
            team.SchoolYear = schoolYearInactive;
            team.SchoolYearId = schoolYearInactive.Id;
            team.Template = templateInactive;
            team.TemplateId = templateInactive.Id;

            // Dodajemy aktywny kanał
            team.Channels.Add(CreateTestChannel("ch-active", team, "Active Channel", isGeneral: true));
            // Dodajemy nieaktywny kanał
            var inactiveChannel = CreateTestChannel("ch-inactive", team, "Inactive Channel");
            inactiveChannel.IsActive = false; // Rekord Channel nieaktywny
            team.Channels.Add(inactiveChannel);


            // Sprawdzenie (tutaj logika właściwości obliczanych w Team
            // np. ShortDescription, powinna odpowiednio obsługiwać nieaktywne encje powiązane, jeśli tak zdecydujemy)

            // ShortDescription w obecnej implementacji sprawdza SchoolType.IsActive
            team.ShortDescription.Should().NotContain(schoolTypeInactive.ShortName);

            // ChannelCount w obecnej implementacji sprawdza Channel.IsActive i Channel.Status
            team.ChannelCount.Should().Be(1);
        }

        [Fact]
        public void Team_WithComplexNameAndDescriptionFromTemplate_AndArchival()
        {
            // Przygotowanie
            var schoolType = CreateTestSchoolType(shortName: "CKZiU");
            var owner = CreateTestUser(upn: "koordynator@example.com", firstName: "Emil", lastName: "Kacprzak"); // Ustawiamy imię i nazwisko dla Nauczyciela

            var template = CreateTestTeamTemplate(
                name: "Szablon Kursu Zawodowego",
                schoolType: schoolType,
                templateContent: "{TypSzkoly} {Oddzial} - {Przedmiot} - {Nauczyciel}"
            );

            var team = CreateBasicTeam(displayName: "Początkowa Nazwa Do Zmiany", owner: owner); // Początkowa nazwa
            team.Template = template; // Przypisanie szablonu do zespołu

            var templateValues = new Dictionary<string, string>
                {
                    {"TypSzkoly", schoolType.ShortName},        // "CKZiU"
                    {"Oddzial", "DRM.04-A"},                   // Przykładowy oddział
                    {"Przedmiot", "Maszyny i urządzenia"},     // Przykładowy przedmiot
                    {"Nauczyciel", owner.FullName}             // "Emil Kacprzak"
                };

            // Wykonanie - generowanie nazwy i opisu
            // Ta linia MUSI być PRZED asercjami na DisplayName i PRZED przypisaniem do initialDisplayName
            team.DisplayName = template.GenerateTeamName(templateValues);

            // Użyjmy wartości z templateValues dla spójności, jeśli "Kurs" i "Rok" nie są już placeholderami
            // team.Description = $"Zespół dla kursu {templateValues["Przedmiot"]} w roku szkolnym."; // Poprawiony opis
            // Jeśli "Rok" i "Kurs" nie są w templateValues, to ich nie używajmy w opisie lub dodajmy je do templateValues
            // Załóżmy, że chcemy bardziej generyczny opis lub opis bazujący na już dostępnych danych:
            team.Description = $"Zespół dla przedmiotu '{templateValues["Przedmiot"]}' prowadzonego przez {templateValues["Nauczyciel"]}.";


            // Teraz initialDisplayName przechowuje poprawnie wygenerowaną nazwę
            var initialDisplayName = team.DisplayName;
            var initialDescription = team.Description;

            // Sprawdzenie początkowe (oczekiwana nazwa)
            string expectedName = $"{schoolType.ShortName} {templateValues["Oddzial"]} - {templateValues["Przedmiot"]} - {templateValues["Nauczyciel"]}";
            initialDisplayName.Should().Be(expectedName); // Sprawdzamy, czy initialDisplayName jest tym, czego oczekujemy
            team.DisplayName.Should().Be(expectedName);   // Sprawdzamy, czy team.DisplayName jest tym, czego oczekujemy


            // Wykonanie - Archiwizacja
            team.Archive("Kurs zakończony", "admin@example.com");

            // Sprawdzenie po archiwizacji
            team.Status.Should().Be(TeamStatus.Archived);
            team.IsActive.Should().BeFalse();
            team.DisplayName.Should().Be($"ARCHIWALNY - {initialDisplayName}");
            team.Description.Should().Be($"ARCHIWALNY - {initialDescription}");
            team.DisplayNameWithStatus.Should().Be($"ARCHIWALNY - {initialDisplayName}");

            // Wykonanie - Przywrócenie
            team.Restore("admin@example.com");

            // Sprawdzenie po przywróceniu
            team.Status.Should().Be(TeamStatus.Active);
            team.IsActive.Should().BeTrue();
            team.DisplayName.Should().Be(initialDisplayName); // Powinno wrócić do oryginalnej wygenerowanej nazwy
            team.Description.Should().Be(initialDescription); // Powinno wrócić do oryginalnego opisu
            team.DisplayNameWithStatus.Should().Be(initialDisplayName);
        }
    }
}