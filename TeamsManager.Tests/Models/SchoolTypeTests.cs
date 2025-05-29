using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Potrzebne dla TeamStatus
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class SchoolTypeTests
    {
        // Metody pomocnicze
        private User CreateTestUser(string id, bool isActive = true)
        {
            return new User { Id = id, FirstName = "Nauczyciel", LastName = id, IsActive = isActive, CreatedBy = "test" };
        }

        private Team CreateTestTeam(string id, bool isActive = true, TeamStatus status = TeamStatus.Active)
        {
            return new Team { Id = id, DisplayName = $"Zespół {id}", IsActive = isActive, Status = status, CreatedBy = "test" };
        }

        private TeamTemplate CreateTestTemplate(string id, bool isActive = true)
        {
            return new TeamTemplate { Id = id, Name = $"Szablon {id}", IsActive = isActive, CreatedBy = "test" };
        }


        [Fact]
        public void SchoolType_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var schoolType = new SchoolType();

            // Sprawdzenie pól bezpośrednich
            schoolType.Id.Should().Be(string.Empty);
            schoolType.ShortName.Should().Be(string.Empty);
            schoolType.FullName.Should().Be(string.Empty);
            schoolType.Description.Should().Be(string.Empty);
            schoolType.ColorCode.Should().BeNull();
            schoolType.SortOrder.Should().Be(0);

            // Pola z BaseEntity
            schoolType.IsActive.Should().BeTrue();
            // schoolType.CreatedDate - zależne od logiki BaseEntity/DbContext

            // Kolekcje nawigacyjne
            schoolType.SupervisingViceDirectors.Should().NotBeNull().And.BeEmpty();
            schoolType.TeacherAssignments.Should().NotBeNull().And.BeEmpty();
            schoolType.Teams.Should().NotBeNull().And.BeEmpty();
            schoolType.Templates.Should().NotBeNull().And.BeEmpty();

            // Właściwości obliczane
            schoolType.DisplayName.Should().Be(" - "); // Bo ShortName i FullName są puste
            schoolType.ActiveTeamsCount.Should().Be(0);
            schoolType.AssignedTeachersCount.Should().Be(0);
            schoolType.AssignedTeachers.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void SchoolType_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var schoolType = new SchoolType();
            var id = "st-lo-gl";
            var shortName = "LO-GL";
            var fullName = "Liceum Ogólnokształcące Główne";
            var description = "Główny typ szkoły licealnej w mieście";
            var colorCode = "#123456";
            var sortOrder = 5;

            // Wykonanie
            schoolType.Id = id;
            schoolType.ShortName = shortName;
            schoolType.FullName = fullName;
            schoolType.Description = description;
            schoolType.ColorCode = colorCode;
            schoolType.SortOrder = sortOrder;
            schoolType.IsActive = false; // Testujemy zmianę z BaseEntity

            // Sprawdzenie
            schoolType.Id.Should().Be(id);
            schoolType.ShortName.Should().Be(shortName);
            schoolType.FullName.Should().Be(fullName);
            schoolType.Description.Should().Be(description);
            schoolType.ColorCode.Should().Be(colorCode);
            schoolType.SortOrder.Should().Be(sortOrder);
            schoolType.IsActive.Should().BeFalse();
        }

        [Fact]
        public void DisplayName_ShouldFormatCorrectly_WithShortAndFullName()
        {
            // Przygotowanie
            var schoolType = new SchoolType
            {
                ShortName = "TECH",
                FullName = "Technikum Informatyczne"
            };

            // Sprawdzenie
            schoolType.DisplayName.Should().Be("TECH - Technikum Informatyczne");
        }

        [Fact]
        public void DisplayName_ShouldHandleEmptyParts_Gracefully()
        {
            // Przygotowanie
            var schoolType1 = new SchoolType { ShortName = "LO" }; // Brak FullName
            var schoolType2 = new SchoolType { FullName = "Szkoła Podstawowa" }; // Brak ShortName
            var schoolType3 = new SchoolType(); // Oba puste

            // Sprawdzenie
            schoolType1.DisplayName.Should().Be("LO - ");
            schoolType2.DisplayName.Should().Be(" - Szkoła Podstawowa");
            schoolType3.DisplayName.Should().Be(" - ");
        }

        [Fact]
        public void ActiveTeamsCount_ShouldCountOnlyActiveTeamsWithActiveStatus()
        {
            // Przygotowanie
            var schoolType = new SchoolType();
            schoolType.Teams.Add(CreateTestTeam("t1", isActive: true, status: TeamStatus.Active));
            schoolType.Teams.Add(CreateTestTeam("t2", isActive: true, status: TeamStatus.Active));
            schoolType.Teams.Add(CreateTestTeam("t3", isActive: true, status: TeamStatus.Archived)); // Zespół zarchiwizowany
            schoolType.Teams.Add(CreateTestTeam("t4", isActive: false, status: TeamStatus.Active)); // Rekord zespołu nieaktywny

            // Sprawdzenie
            schoolType.ActiveTeamsCount.Should().Be(2);
        }

        [Fact]
        public void AssignedTeachersCount_And_AssignedTeachers_ShouldWorkCorrectly()
        {
            // Przygotowanie
            var schoolType = new SchoolType();
            var teacher1 = CreateTestUser("teacher1", isActive: true);
            var teacher2 = CreateTestUser("teacher2", isActive: true);
            var inactiveTeacher = CreateTestUser("teacher_inactive", isActive: false);
            var teacherForInactiveAssignment = CreateTestUser("teacher_for_inactive_assign", isActive: true);

            schoolType.TeacherAssignments.Add(new UserSchoolType { SchoolType = schoolType, User = teacher1, UserId = teacher1.Id, IsActive = true, IsCurrentlyActive = true });
            schoolType.TeacherAssignments.Add(new UserSchoolType { SchoolType = schoolType, User = teacher2, UserId = teacher2.Id, IsActive = true, IsCurrentlyActive = true });
            schoolType.TeacherAssignments.Add(new UserSchoolType { SchoolType = schoolType, User = inactiveTeacher, UserId = inactiveTeacher.Id, IsActive = true, IsCurrentlyActive = true }); // Nauczyciel nieaktywny
            schoolType.TeacherAssignments.Add(new UserSchoolType { SchoolType = schoolType, User = teacherForInactiveAssignment, UserId = teacherForInactiveAssignment.Id, IsActive = false, IsCurrentlyActive = true }); // Przypisanie nieaktywne
            schoolType.TeacherAssignments.Add(new UserSchoolType { SchoolType = schoolType, User = CreateTestUser("t_temp"), UserId = "t_temp", IsActive = true, IsCurrentlyActive = false }); // Przypisanie nie IsCurrentlyActive

            // Sprawdzenie
            schoolType.AssignedTeachersCount.Should().Be(2); // Tylko teacher1 i teacher2 (aktywne przypisania aktywnych nauczycieli)

            var assignedTeachersList = schoolType.AssignedTeachers;
            assignedTeachersList.Should().HaveCount(2);
            assignedTeachersList.Should().Contain(teacher1);
            assignedTeachersList.Should().Contain(teacher2);
            assignedTeachersList.Should().NotContain(inactiveTeacher);
            assignedTeachersList.Should().NotContain(teacherForInactiveAssignment);
        }

        // Testy dla kolekcji SupervisingViceDirectors i Templates zostaną dodane, gdy
        // będziemy mieli bardziej złożone scenariusze lub metody pomocnicze w SchoolType do zarządzania nimi.
        // Na razie ich podstawowe działanie (inicjalizacja jako puste listy) jest sprawdzane w pierwszym teście.
    }
}