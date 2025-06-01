using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Dla TeamStatus, jeśli będzie potrzebne w metodach pomocniczych
using Xunit;

namespace TeamsManager.Tests.Models
{
    public class SchoolYearTests
    {
        // Metoda pomocnicza do tworzenia zespołu
        private Team CreateTestTeam(string id, bool isActive = true, TeamStatus status = TeamStatus.Active)
        {
            return new Team { Id = id, DisplayName = $"Zespół {id}", Status = status };
        }

        [Fact]
        public void SchoolYear_WhenCreated_ShouldHaveDefaultValues()
        {
            // Przygotowanie i Wykonanie
            var schoolYear = new SchoolYear();
            var today = DateTime.Today;

            // Sprawdzenie pól bezpośrednich
            schoolYear.Id.Should().Be(string.Empty);
            schoolYear.Name.Should().Be(string.Empty);
            schoolYear.StartDate.Should().Be(default(DateTime));
            schoolYear.EndDate.Should().Be(default(DateTime));
            schoolYear.IsCurrent.Should().BeFalse();
            schoolYear.Description.Should().Be(string.Empty);
            schoolYear.FirstSemesterStart.Should().BeNull();
            schoolYear.FirstSemesterEnd.Should().BeNull();
            schoolYear.SecondSemesterStart.Should().BeNull();
            schoolYear.SecondSemesterEnd.Should().BeNull();

            // Pola z BaseEntity
            schoolYear.IsActive.Should().BeTrue();
            // schoolYear.CreatedDate - zależne od logiki BaseEntity/DbContext

            // Kolekcje nawigacyjne
            schoolYear.Teams.Should().NotBeNull().And.BeEmpty();

            // Właściwości obliczane - wartości dla domyślnych dat (01/01/0001)
            // Zachowanie HasStarted i HasEnded zależy od porównania z DateTime.Now
            // Dla default(DateTime), StartDate i EndDate są w przeszłości względem DateTime.Now
            schoolYear.HasStarted.Should().BeTrue();
            schoolYear.HasEnded.Should().BeTrue();
            schoolYear.IsCurrentlyActive.Should().BeFalse(); // Bo HasEnded jest true
            schoolYear.DaysRemaining.Should().Be(0);
            // Dla CompletionPercentage, jeśli StartDate i EndDate to default(DateTime), totalDuration może być 0.
            // Należy to obsłużyć w logice CompletionPercentage, aby uniknąć dzielenia przez zero.
            // Przy StartDate = EndDate = default, totalDays jest 0.
            // Zgodnie z logiką: if (totalDays <= 0) return StartDate.Value.Date <= DateTime.Today ? 100 : 0;
            // Ponieważ default(DateTime).Date <= DateTime.Today jest true, powinno być 100.
            schoolYear.CompletionPercentage.Should().Be(100);
            schoolYear.CurrentSemester.Should().BeNull();
            schoolYear.ActiveTeamsCount.Should().Be(0);
        }

        [Fact]
        public void SchoolYear_WhenSettingProperties_ShouldRetainValues()
        {
            // Przygotowanie
            var schoolYear = new SchoolYear();
            var id = "sy-2025-26";
            var name = "2025/2026";
            var startDate = new DateTime(2025, 9, 1);
            var endDate = new DateTime(2026, 6, 20);
            var description = "Rok szkolny dla klas maturalnych";
            var fsStart = new DateTime(2025, 9, 1);
            var fsEnd = new DateTime(2026, 1, 15);
            var ssStart = new DateTime(2026, 1, 20);
            var ssEnd = new DateTime(2026, 6, 10);

            // Wykonanie
            schoolYear.Id = id;
            schoolYear.Name = name;
            schoolYear.StartDate = startDate;
            schoolYear.EndDate = endDate;
            schoolYear.IsCurrent = true;
            schoolYear.Description = description;
            schoolYear.FirstSemesterStart = fsStart;
            schoolYear.FirstSemesterEnd = fsEnd;
            schoolYear.SecondSemesterStart = ssStart;
            schoolYear.SecondSemesterEnd = ssEnd;
            schoolYear.IsActive = false;

            // Sprawdzenie
            schoolYear.Id.Should().Be(id);
            schoolYear.Name.Should().Be(name);
            schoolYear.StartDate.Should().Be(startDate);
            schoolYear.EndDate.Should().Be(endDate);
            schoolYear.IsCurrent.Should().BeTrue();
            schoolYear.Description.Should().Be(description);
            schoolYear.FirstSemesterStart.Should().Be(fsStart);
            schoolYear.FirstSemesterEnd.Should().Be(fsEnd);
            schoolYear.SecondSemesterStart.Should().Be(ssStart);
            schoolYear.SecondSemesterEnd.Should().Be(ssEnd);
            schoolYear.IsActive.Should().BeFalse();
        }

        [Fact]
        public void SchoolYear_DateBasedComputedProperties_ShouldCalculateCorrectly()
        {
            var schoolYear = new SchoolYear { IsActive = true }; // Zakładamy, że rekord jest aktywny
            var today = DateTime.Today;

            // Scenariusz 1: Rok szkolny w przyszłości
            schoolYear.StartDate = today.AddDays(30);
            schoolYear.EndDate = today.AddDays(30 + 270); // Około 9 miesięcy
            schoolYear.HasStarted.Should().BeFalse();
            schoolYear.HasEnded.Should().BeFalse();
            schoolYear.IsCurrentlyActive.Should().BeFalse();
            schoolYear.DaysRemaining.Should().Be(300);
            schoolYear.CompletionPercentage.Should().Be(0);

            // Scenariusz 2: Rok szkolny trwa (np. w połowie)
            schoolYear.StartDate = today.AddDays(-100);
            schoolYear.EndDate = today.AddDays(170); // Łącznie 270 dni
            schoolYear.HasStarted.Should().BeTrue();
            schoolYear.HasEnded.Should().BeFalse();
            schoolYear.IsCurrentlyActive.Should().BeTrue();
            schoolYear.DaysRemaining.Should().Be(170);
            schoolYear.CompletionPercentage.Should().BeApproximately(100.0 / 270.0 * 100, 0.1); // (100 / 270) * 100

            // Scenariusz 3: Rok szkolny zakończony
            schoolYear.StartDate = today.AddDays(-300);
            schoolYear.EndDate = today.AddDays(-30);
            schoolYear.HasStarted.Should().BeTrue();
            schoolYear.HasEnded.Should().BeTrue();
            schoolYear.IsCurrentlyActive.Should().BeFalse();
            schoolYear.DaysRemaining.Should().Be(0);
            schoolYear.CompletionPercentage.Should().Be(100);
        }

        [Fact]
        public void SchoolYear_CurrentSemester_ShouldBeCalculatedCorrectly()
        {
            var schoolYear = new SchoolYear { IsActive = true };
            var today = DateTime.Today;

            // Scenariusz 1: Brak zdefiniowanych semestrów
            schoolYear.CurrentSemester.Should().BeNull();

            // Scenariusz 2: W trakcie pierwszego semestru
            schoolYear.FirstSemesterStart = today.AddDays(-10);
            schoolYear.FirstSemesterEnd = today.AddDays(20);
            schoolYear.SecondSemesterStart = today.AddDays(30);
            schoolYear.SecondSemesterEnd = today.AddDays(60);
            schoolYear.CurrentSemester.Should().Be(1);

            // Scenariusz 3: W trakcie drugiego semestru
            schoolYear.FirstSemesterStart = today.AddDays(-60);
            schoolYear.FirstSemesterEnd = today.AddDays(-30);
            schoolYear.SecondSemesterStart = today.AddDays(-10);
            schoolYear.SecondSemesterEnd = today.AddDays(20);
            schoolYear.CurrentSemester.Should().Be(2);

            // Scenariusz 4: Pomiędzy semestrami
            schoolYear.FirstSemesterStart = today.AddDays(-60);
            schoolYear.FirstSemesterEnd = today.AddDays(-30);
            schoolYear.SecondSemesterStart = today.AddDays(10); // Drugi semestr w przyszłości
            schoolYear.SecondSemesterEnd = today.AddDays(40);
            schoolYear.CurrentSemester.Should().BeNull();

            // Scenariusz 5: Przed pierwszym semestrem
            schoolYear.FirstSemesterStart = today.AddDays(10);
            schoolYear.CurrentSemester.Should().BeNull();

            // Scenariusz 6: Po drugim semestrze
            schoolYear.FirstSemesterStart = today.AddDays(-100);
            schoolYear.FirstSemesterEnd = today.AddDays(-70);
            schoolYear.SecondSemesterStart = today.AddDays(-60);
            schoolYear.SecondSemesterEnd = today.AddDays(-30);
            schoolYear.CurrentSemester.Should().BeNull();
        }

        [Fact]
        public void SchoolYear_ActiveTeamsCount_ShouldCountOnlyActiveTeamsInActiveYear()
        {
            // Przygotowanie
            var schoolYear = new SchoolYear { IsActive = true };
            schoolYear.Teams.Add(CreateTestTeam("t1", status: TeamStatus.Active));
            schoolYear.Teams.Add(CreateTestTeam("t2", status: TeamStatus.Active));
            schoolYear.Teams.Add(CreateTestTeam("t3", status: TeamStatus.Archived)); // Zespół zarchiwizowany
            schoolYear.Teams.Add(CreateTestTeam("t4", status: TeamStatus.Active)); // Rekord zespołu nieaktywny

            // Sprawdzenie
            schoolYear.ActiveTeamsCount.Should().Be(3);

        }
    }
}