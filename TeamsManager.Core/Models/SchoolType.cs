using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Typ szkoły/jednostki edukacyjnej
    /// Definiuje rodzaj instytucji (LO, Technikum, KKZ, PNZ, TDTMP, itp.)
    /// Może reprezentować również wydział, kierunek studiów lub inną jednostkę organizacyjną
    /// </summary>
    public class SchoolType : BaseEntity
    {
        /// <summary>
        /// Skrót nazwy typu szkoły (np. "LO", "KKZ", "PNZ")
        /// </summary>
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// Pełna nazwa typu szkoły (np. "Liceum Ogólnokształcące", "Kwalifikacyjne Kursy Zawodowe")
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Dodatkowy opis typu szkoły, szczegóły, specjalizacje
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Kolor używany w interfejsie do wizualnego odróżnienia typów szkół
        /// </summary>
        public string? ColorCode { get; set; } // np. "#FF5722" dla łatwego rozróżnienia w UI

        /// <summary>
        /// Kolejność sortowania przy wyświetlaniu listy typów szkół
        /// </summary>
        public int SortOrder { get; set; } = 0;

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Wicedyrektorzy nadzorujący ten typ szkoły
        /// Relacja wiele-do-wielu - jeden wicedyrektor może nadzorować wiele typów
        /// </summary>
        public List<User> SupervisingViceDirectors { get; set; } = new List<User>();

        /// <summary>
        /// Przypisania nauczycieli do tego typu szkoły
        /// Relacja przez tabelę pośrednią UserSchoolType
        /// </summary>
        public List<UserSchoolType> TeacherAssignments { get; set; } = new List<UserSchoolType>();

        /// <summary>
        /// Zespoły przypisane do tego typu szkoły
        /// </summary>
        public List<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Szablony nazw zespołów dedykowane dla tego typu szkoły
        /// </summary>
        public List<TeamTemplate> Templates { get; set; } = new List<TeamTemplate>();

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Nazwa wyświetlana w formacie "Skrót - Pełna nazwa"
        /// </summary>
        public string DisplayName => $"{ShortName} - {FullName}";

        /// <summary>
        /// Liczba aktywnych zespołów tego typu
        /// </summary>
        public int ActiveTeamsCount => Teams?.Count(t => t.IsActive && t.Status == TeamStatus.Active) ?? 0;

        /// <summary>
        /// Liczba aktualnie i w pełni aktywnych przypisań nauczycieli do tego typu szkoły.
        /// Uwzględnia aktywność rekordu UserSchoolType, flagę IsCurrentlyActive oraz aktywność samego Użytkownika.
        /// </summary>
        public int AssignedTeachersCount => TeacherAssignments?.Count(ta =>
            ta.IsActive &&           // Rekord UserSchoolType jest ogólnie aktywny
            ta.IsCurrentlyActive &&  // Przypisanie jest bieżąco aktywne (np. w ramach dat)
            ta.User != null &&       // Użytkownik istnieje
            ta.User.IsActive)        // Użytkownik jest aktywny
            ?? 0;

        /// <summary>
        /// Lista aktualnie i w pełni aktywnych nauczycieli przypisanych do tego typu szkoły.
        /// Uwzględnia aktywność rekordu UserSchoolType, flagę IsCurrentlyActive oraz aktywność samego Użytkownika.
        /// </summary>
        public List<User> AssignedTeachers => TeacherAssignments?
            .Where(ta =>
                ta.IsActive &&           // Rekord UserSchoolType jest ogólnie aktywny
                ta.IsCurrentlyActive &&  // Przypisanie jest bieżąco aktywne
                ta.User != null &&       // Użytkownik istnieje
                ta.User.IsActive)        // Użytkownik jest aktywny
            .Select(ta => ta.User!) // Używamy User! bo sprawdziliśmy User != null
            .ToList() ?? new List<User>();
    }
}