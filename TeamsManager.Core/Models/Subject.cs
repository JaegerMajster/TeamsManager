using System;
using System.Collections.Generic;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Reprezentuje przedmiot nauczania lub kurs.
    /// </summary>
    public class Subject : BaseEntity
    {
        /// <summary>
        /// Pełna nazwa przedmiotu (np. "Matematyka dla klas pierwszych", "Programowanie w C# - poziom zaawansowany").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Skrócona nazwa lub kod przedmiotu (np. "MAT-101", "CSHARP-ADV").
        /// </summary>
        public string? Code { get; set; }

        /// <summary>
        /// Szczegółowy opis przedmiotu, zakres materiału, cele kształcenia.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Liczba godzin lekcyjnych/wykładowych przewidziana dla przedmiotu.
        /// </summary>
        public int? Hours { get; set; }

        /// <summary>
        /// Typ szkoły, dla którego ten przedmiot jest najczęściej przeznaczony (opcjonalnie).
        /// Można by też rozważyć relację wiele-do-wielu z SchoolType, jeśli jeden przedmiot może być w wielu typach szkół.
        /// </summary>
        public string? DefaultSchoolTypeId { get; set; }
        public SchoolType? DefaultSchoolType { get; set; }

        /// <summary>
        /// Kategoria przedmiotu (np. "Nauki ścisłe", "Języki obce", "Przedmioty zawodowe").
        /// </summary>
        public string? Category { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Lista przypisań nauczycieli do tego przedmiotu.
        /// </summary>
        public List<UserSubject> TeacherAssignments { get; set; } = new List<UserSubject>();

        // Można tu dodać inne właściwości, np. ECTS (dla studiów wyższych),
        // poziom zaawansowania (Podstawowy, Rozszerzony), itp.
        // oraz właściwości obliczane, np. liczba przypisanych nauczycieli.
    }
}