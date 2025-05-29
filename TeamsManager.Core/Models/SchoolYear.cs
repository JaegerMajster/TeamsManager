using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Rok szkolny/akademicki
    /// Definiuje okres funkcjonowania zespołów edukacyjnych
    /// </summary>
    public class SchoolYear : BaseEntity
    {
        /// <summary>
        /// Nazwa roku szkolnego (np. "2024/2025", "2025/2026")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data rozpoczęcia roku szkolnego
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Data zakończenia roku szkolnego
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Czy to jest aktualny rok szkolny
        /// Tylko jeden rok może być oznaczony jako bieżący
        /// </summary>
        public bool IsCurrent { get; set; } = false;

        /// <summary>
        /// Dodatkowy opis roku szkolnego, uwagi specjalne
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Data rozpoczęcia pierwszego semestru
        /// </summary>
        public DateTime? FirstSemesterStart { get; set; }

        /// <summary>
        /// Data zakończenia pierwszego semestru
        /// </summary>
        public DateTime? FirstSemesterEnd { get; set; }

        /// <summary>
        /// Data rozpoczęcia drugiego semestru
        /// </summary>
        public DateTime? SecondSemesterStart { get; set; }

        /// <summary>
        /// Data zakończenia drugiego semestru
        /// </summary>
        public DateTime? SecondSemesterEnd { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Zespoły funkcjonujące w tym roku szkolnym
        /// </summary>
        public List<Team> Teams { get; set; } = new List<Team>();

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Czy rok szkolny już się rozpoczął
        /// </summary>
        public bool HasStarted => DateTime.Today >= StartDate.Date;

        /// <summary>
        /// Czy rok szkolny już się zakończył
        /// </summary>
        public bool HasEnded => DateTime.Today > EndDate.Date;

        /// <summary>
        /// Czy rok szkolny jest obecnie aktywny (trwa)
        /// </summary>
        public bool IsCurrentlyActive => HasStarted && !HasEnded;

        /// <summary>
        /// Ile dni pozostało do zakończenia roku szkolnego
        /// </summary>
        public int DaysRemaining => HasEnded ? 0 : Math.Max(0, (EndDate.Date - DateTime.Today).Days);

        /// <summary>
        /// Procent ukończenia roku szkolnego (0-100)
        /// </summary>
        public double CompletionPercentage
        {
            get
            {
                // Zmiana StartDate i EndDate na StartDate.Date i EndDate.Date dla spójności
                if (StartDate.Date > DateTime.Today) return 0; // Jeśli jeszcze się nie zaczął (bazując na dacie)
                if (EndDate.Date < DateTime.Today) return 100;   // Jeśli już się zakończył (bazując na dacie)

                var totalDays = (EndDate.Date - StartDate.Date).TotalDays;
                if (totalDays <= 0) // Jeśli StartDate jest taka sama lub późniejsza niż EndDate
                {
                    // Jeśli StartDate jest dzisiaj lub w przeszłości, a totalDays <=0 (EndDate <= StartDate) to uznajemy za 100%
                    return StartDate.Date <= DateTime.Today ? 100 : 0;
                }

                var elapsedDays = (DateTime.Today - StartDate.Date).TotalDays;
                // elapsedDays nie powinno być ujemne jeśli HasStarted jest true, ale dla pewności:
                if (elapsedDays < 0) return 0;

                // Jeśli elapsedDays jest większe lub równe totalDays, to jest 100%
                if (elapsedDays >= totalDays) return 100;


                return Math.Round((elapsedDays / totalDays) * 100, 1);
            }
        }

        /// <summary>
        /// Aktualny semestr (1 lub 2), null jeśli poza semestrami
        /// </summary>
        public int? CurrentSemester
        {
            get
            {
                var nowDate = DateTime.Today; // Używamy DateTime.Today

                if (FirstSemesterStart.HasValue && FirstSemesterEnd.HasValue &&
                    nowDate >= FirstSemesterStart.Value.Date && nowDate <= FirstSemesterEnd.Value.Date)
                    return 1;

                if (SecondSemesterStart.HasValue && SecondSemesterEnd.HasValue &&
                    nowDate >= SecondSemesterStart.Value.Date && nowDate <= SecondSemesterEnd.Value.Date)
                    return 2;

                return null;
            }
        }

        /// <summary>
        /// Liczba aktywnych zespołów w tym roku szkolnym
        /// </summary>
        public int ActiveTeamsCount => Teams?.Count(t => t.IsActive && t.Status == TeamStatus.Active) ?? 0;
    }
}