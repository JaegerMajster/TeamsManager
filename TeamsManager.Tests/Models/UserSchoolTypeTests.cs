using System;
using FluentAssertions;
using TeamsManager.Core.Models;
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class UserSchoolTypeTests
    {
        private User CreateTestUser(string id = "user-1") => new User { Id = id, FirstName = "Test", LastName = "User" };
        private SchoolType CreateTestSchoolType(string id = "st-1") => new SchoolType { Id = id, ShortName = "LO", FullName = "Liceum" };

        [Fact]
        public void UserSchoolType_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var ust = new UserSchoolType();

            // Sprawdzenie
            ust.Id.Should().Be(string.Empty);
            ust.UserId.Should().Be(string.Empty);
            ust.SchoolTypeId.Should().Be(string.Empty);
            ust.AssignedDate.Should().Be(default(DateTime));
            ust.EndDate.Should().BeNull();
            ust.IsCurrentlyActive.Should().BeTrue(); // Domyślna wartość z modelu
            ust.Notes.Should().BeNull();
            ust.WorkloadPercentage.Should().BeNull();
            ust.IsActive.Should().BeTrue(); // Z BaseEntity

            ust.User.Should().BeNull(); // Domyślnie, zanim zostanie przypisany
            ust.SchoolType.Should().BeNull();

            // Właściwości obliczane
            // ust.IsActiveToday.Should().BeFalse(); // Zależy od AssignedDate i EndDate
            // ust.DaysAssigned.Should().Be(0); // Zależy od AssignedDate
            ust.AssignmentDescription.Should().Be(" -> "); // Bo User i SchoolType są null
        }

        [Fact]
        public void UserSchoolType_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var ust = new UserSchoolType();
            var user = CreateTestUser();
            var schoolType = CreateTestSchoolType();
            var assignedDate = DateTime.UtcNow.AddDays(-10);
            var endDate = DateTime.UtcNow.AddDays(100);
            var notes = "Główny nauczyciel";
            var workload = 75.5m;

            // Wykonanie
            ust.Id = "ust-1";
            ust.User = user;
            ust.UserId = user.Id;
            ust.SchoolType = schoolType;
            ust.SchoolTypeId = schoolType.Id;
            ust.AssignedDate = assignedDate;
            ust.EndDate = endDate;
            ust.IsCurrentlyActive = false;
            ust.Notes = notes;
            ust.WorkloadPercentage = workload;
            ust.IsActive = false; // Z BaseEntity

            // Sprawdzenie
            ust.Id.Should().Be("ust-1");
            ust.UserId.Should().Be(user.Id);
            ust.User.Should().Be(user);
            ust.SchoolTypeId.Should().Be(schoolType.Id);
            ust.SchoolType.Should().Be(schoolType);
            ust.AssignedDate.Should().Be(assignedDate);
            ust.EndDate.Should().Be(endDate);
            ust.IsCurrentlyActive.Should().BeFalse();
            ust.Notes.Should().Be(notes);
            ust.WorkloadPercentage.Should().Be(workload);
            ust.IsActive.Should().BeFalse();
            ust.AssignmentDescription.Should().Be($"{user.FullName} -> {schoolType.DisplayName}");
        }

        // TODO: Dodać testy dla właściwości obliczanych IsActiveOnDate, IsActiveToday, DaysAssigned
        // z różnymi kombinacjami dat AssignedDate, EndDate i IsCurrentlyActive.
    }
}