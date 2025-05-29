using System;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Tabela pośrednia łącząca użytkowników z typami szkół
    /// Umożliwia przypisanie nauczyciela do jednego lub wielu typów szkół
    /// Jeden nauczyciel może prowadzić zajęcia w LO, KKZ i PNZ jednocześnie
    /// </summary>
    public class UserSchoolType : BaseEntity
    {
        /// <summary>
        /// Identyfikator użytkownika
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator typu szkoły
        /// </summary>
        public string SchoolTypeId { get; set; } = string.Empty;

        /// <summary>
        /// Data przypisania użytkownika do typu szkoły
        /// </summary>
        public DateTime AssignedDate { get; set; }

        /// <summary>
        /// Data zakończenia przypisania (opcjonalna)
        /// Jeśli null - przypisanie jest bezterminowe
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Czy przypisanie jest obecnie aktywne
        /// Może być używane do tymczasowego wyłączenia bez usuwania rekordu
        /// </summary>
        public bool IsCurrentlyActive { get; set; } = true;

        /// <summary>
        /// Dodatkowe informacje o przypisaniu
        /// Np. "Główny nauczyciel matematyki", "Zastępstwo"
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Procent czasu pracy przypisany do tego typu szkoły
        /// Pomocne przy rozliczaniu etatów (np. 50% w LO, 50% w Technikum)
        /// </summary>
        public decimal? WorkloadPercentage { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Użytkownik przypisany do typu szkoły
        /// </summary>
        public User User { get; set; } = null!;

        /// <summary>
        /// Typ szkoły do którego przypisany jest użytkownik
        /// </summary>
        public SchoolType SchoolType { get; set; } = null!;

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Czy przypisanie jest aktywne w danej dacie
        /// Sprawdza czy data mieści się w przedziale AssignedDate - EndDate
        /// </summary>
        public bool IsActiveOnDate(DateTime date)
        {
            if (!IsActive || !IsCurrentlyActive) return false;
            if (date < AssignedDate) return false;
            if (EndDate.HasValue && date > EndDate.Value) return false;
            return true;
        }

        /// <summary>
        /// Czy przypisanie jest obecnie aktywne (dzisiaj)
        /// </summary>
        public bool IsActiveToday => IsActiveOnDate(DateTime.Today);

        /// <summary>
        /// Liczba dni od przypisania
        /// </summary>
        public int DaysAssigned => (DateTime.Today - AssignedDate.Date).Days;

        /// <summary>
        /// Opis przypisania w formacie "Użytkownik -> Typ szkoły"
        /// </summary>
        public string AssignmentDescription => $"{User?.FullName} -> {SchoolType?.DisplayName}";
    }
}