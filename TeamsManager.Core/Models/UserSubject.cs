using System;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Tabela pośrednia łącząca Użytkowników (nauczycieli) z Przedmiotami.
    /// Pozwala na przypisanie nauczyciela do nauczania konkretnego przedmiotu
    /// oraz przechowywanie dodatkowych atrybutów tego przypisania.
    /// </summary>
    public class UserSubject : BaseEntity
    {
        /// <summary>
        /// Identyfikator użytkownika (nauczyciela).
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        /// <summary>
        /// Identyfikator przedmiotu.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;
        public Subject Subject { get; set; } = null!;

        /// <summary>
        /// Data, od której nauczyciel jest przypisany do nauczania tego przedmiotu.
        /// </summary>
        public DateTime AssignedDate { get; set; }

        /// <summary>
        /// Dodatkowe uwagi dotyczące przypisania (np. "Główny prowadzący", "Wykłady").
        /// </summary>
        public string? Notes { get; set; }

        // Można tu dodać inne specyficzne właściwości dla przypisania,
        // np. poziom nauczania, typ zajęć (wykład, ćwiczenia) itp.
    }
}