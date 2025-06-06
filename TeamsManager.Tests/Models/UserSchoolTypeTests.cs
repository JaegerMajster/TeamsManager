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

        [Theory]
        [InlineData(-10, null, true, true, true)]  // Aktywne przypisanie bez daty końcowej
        [InlineData(-10, 10, true, true, true)]    // Aktywne przypisanie z przyszłą datą końcową
        [InlineData(-10, -5, true, true, false)]   // Przypisanie zakończone w przeszłości
        [InlineData(5, null, true, true, false)]   // Przypisanie rozpocznie się w przyszłości
        [InlineData(-10, null, false, true, false)] // IsCurrentlyActive = false
        [InlineData(-10, null, true, false, false)] // IsActive = false (z BaseEntity)
        public void UserSchoolType_IsActiveOnDate_ShouldReturnCorrectValue(
            int assignedDaysOffset, 
            int? endDaysOffset, 
            bool isCurrentlyActive, 
            bool isActive,
            bool expectedResult)
        {
            // Przygotowanie
            var today = DateTime.Today;
            var ust = new UserSchoolType
            {
                AssignedDate = today.AddDays(assignedDaysOffset),
                EndDate = endDaysOffset.HasValue ? today.AddDays(endDaysOffset.Value) : (DateTime?)null,
                IsCurrentlyActive = isCurrentlyActive,
                IsActive = isActive
            };

            // Wykonanie
            var result = ust.IsActiveOnDate(today);

            // Sprawdzenie
            result.Should().Be(expectedResult);
        }

        [Fact]
        public void UserSchoolType_IsActiveOnDate_EdgeCases_ShouldHandleCorrectly()
        {
            // Przygotowanie
            var ust = new UserSchoolType
            {
                AssignedDate = new DateTime(2025, 1, 1),
                EndDate = new DateTime(2025, 12, 31),
                IsCurrentlyActive = true,
                IsActive = true
            };

            // Sprawdzenie - dokładnie na datach granicznych
            ust.IsActiveOnDate(new DateTime(2025, 1, 1)).Should().BeTrue(); // Pierwszy dzień
            ust.IsActiveOnDate(new DateTime(2025, 12, 31)).Should().BeTrue(); // Ostatni dzień
            ust.IsActiveOnDate(new DateTime(2024, 12, 31)).Should().BeFalse(); // Dzień przed
            ust.IsActiveOnDate(new DateTime(2026, 1, 1)).Should().BeFalse(); // Dzień po
        }

        [Fact]
        public void UserSchoolType_IsActiveToday_WhenActiveAssignment_ShouldReturnTrue()
        {
            // Przygotowanie
            var ust = new UserSchoolType
            {
                AssignedDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today.AddDays(30),
                IsCurrentlyActive = true,
                IsActive = true
            };

            // Sprawdzenie
            ust.IsActiveToday.Should().BeTrue();
        }

        [Fact]
        public void UserSchoolType_IsActiveToday_WhenInactiveAssignment_ShouldReturnFalse()
        {
            // Przygotowanie - różne scenariusze nieaktywności
            var scenarios = new[]
            {
                new UserSchoolType // Zakończone w przeszłości
                {
                    AssignedDate = DateTime.Today.AddDays(-60),
                    EndDate = DateTime.Today.AddDays(-30),
                    IsCurrentlyActive = true,
                    IsActive = true
                },
                new UserSchoolType // Rozpocznie się w przyszłości
                {
                    AssignedDate = DateTime.Today.AddDays(10),
                    EndDate = null,
                    IsCurrentlyActive = true,
                    IsActive = true
                },
                new UserSchoolType // IsCurrentlyActive = false
                {
                    AssignedDate = DateTime.Today.AddDays(-10),
                    EndDate = null,
                    IsCurrentlyActive = false,
                    IsActive = true
                },
                new UserSchoolType // IsActive = false (soft delete)
                {
                    AssignedDate = DateTime.Today.AddDays(-10),
                    EndDate = null,
                    IsCurrentlyActive = true,
                    IsActive = false
                }
            };

            // Sprawdzenie
            foreach (var ust in scenarios)
            {
                ust.IsActiveToday.Should().BeFalse();
            }
        }

        [Theory]
        [InlineData(-30, null, 30)]      // 30 dni temu, bez końca = 30 dni
        [InlineData(-30, -10, 30)]       // Od 30 dni temu (EndDate nie wpływa na DaysAssigned)
        [InlineData(-30, 10, 30)]        // Od 30 dni temu (EndDate nie wpływa na DaysAssigned)
        [InlineData(0, null, 0)]         // Dzisiaj rozpoczęte = 0 dni
        [InlineData(0, 10, 0)]          // Dzisiaj rozpoczęte = 0 dni
        [InlineData(-1, -1, 1)]         // Wczoraj rozpoczęte = 1 dzień
        public void UserSchoolType_DaysAssigned_ShouldCalculateCorrectly(
            int assignedDaysOffset,
            int? endDaysOffset,
            int expectedDays)
        {
            // Przygotowanie
            var today = DateTime.Today;
            var ust = new UserSchoolType
            {
                AssignedDate = today.AddDays(assignedDaysOffset),
                EndDate = endDaysOffset.HasValue ? today.AddDays(endDaysOffset.Value) : (DateTime?)null
            };

            // Sprawdzenie
            ust.DaysAssigned.Should().Be(expectedDays);
        }

        [Fact]
        public void UserSchoolType_DaysAssigned_WithFutureAssignment_ShouldReturnNegative()
        {
            // Przygotowanie
            var ust = new UserSchoolType
            {
                AssignedDate = DateTime.Today.AddDays(10), // Rozpocznie się za 10 dni
                EndDate = null
            };

            // Sprawdzenie - dla przyszłych przypisań powinno zwrócić wartość ujemną
            ust.DaysAssigned.Should().Be(-10);
        }

        [Fact]
        public void UserSchoolType_AllCalculatedProperties_ShouldWorkTogether()
        {
            // Przygotowanie
            var user = CreateTestUser();
            var schoolType = CreateTestSchoolType();
            var assignedDate = DateTime.Today.AddDays(-365); // Rok temu
            var endDate = DateTime.Today.AddDays(30); // Kończy się za miesiąc
            
            var ust = new UserSchoolType
            {
                Id = "ust-integration",
                User = user,
                UserId = user.Id,
                SchoolType = schoolType,
                SchoolTypeId = schoolType.Id,
                AssignedDate = assignedDate,
                EndDate = endDate,
                IsCurrentlyActive = true,
                IsActive = true,
                Notes = "Roczne przypisanie",
                WorkloadPercentage = 100m
            };

            // Sprawdzenie
            ust.IsActiveToday.Should().BeTrue();
            ust.IsActiveOnDate(DateTime.Today.AddDays(-180)).Should().BeTrue(); // Pół roku temu
            ust.IsActiveOnDate(DateTime.Today.AddDays(60)).Should().BeFalse(); // Za 2 miesiące
            ust.DaysAssigned.Should().Be(365); // 365 dni od przypisania
            ust.AssignmentDescription.Should().Be($"{user.FullName} -> {schoolType.DisplayName}");
        }
    }
}