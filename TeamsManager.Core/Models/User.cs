using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Helpers;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Użytkownik systemu - reprezentuje osobę w organizacji edukacyjnej
    /// Może być uczniem, nauczycielem, wicedyrektorem lub dyrektorem
    /// </summary>
    public class User : BaseEntity
    {
        /// <summary>
        /// Imię użytkownika
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Nazwisko użytkownika
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// User Principal Name - główny identyfikator w Microsoft 365
        /// Format: imie.nazwisko@domena.edu.pl
        /// </summary>
        public string UPN { get; set; } = string.Empty;

        /// <summary>
        /// Rola użytkownika w systemie edukacyjnym
        /// </summary>
        public UserRole Role { get; set; } = UserRole.Uczen;

        /// <summary>
        /// Identyfikator działu do którego przypisany jest użytkownik
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Numer telefonu (opcjonalny)
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Dodatkowy adres email (opcjonalny)
        /// </summary>
        public string? AlternateEmail { get; set; }

        /// <summary>
        /// Numer identyfikacyjny w systemie zewnętrznym
        /// Np. numer w dzienniku elektronicznym, numer studencki
        /// </summary>
        public string? ExternalId { get; set; }

        /// <summary>
        /// Data urodzenia (opcjonalna)
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Data zatrudnienia/zapisania do szkoły
        /// </summary>
        public DateTime? EmploymentDate { get; set; }

        /// <summary>
        /// Stanowisko/funkcja (dodatkowe informacje do roli)
        /// </summary>
        public string? Position { get; set; }

        /// <summary>
        /// Uwagi dotyczące użytkownika
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Czy użytkownik jest administratorem systemu
        /// </summary>
        public bool IsSystemAdmin { get; set; } = false;

        /// <summary>
        /// Data ostatniego logowania do systemu
        /// </summary>
        public DateTime? LastLoginDate { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Dział do którego przypisany jest użytkownik
        /// </summary>
        public Department? Department { get; set; }

        /// <summary>
        /// Członkostwa użytkownika w zespołach
        /// Jeden użytkownik może być członkiem wielu zespołów
        /// </summary>
        public List<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();

        /// <summary>
        /// Przypisania użytkownika do typów szkół (dla nauczycieli)
        /// Nauczyciel może uczyć w LO, Technikum i KKZ jednocześnie
        /// </summary>
        public List<UserSchoolType> SchoolTypeAssignments { get; set; } = new List<UserSchoolType>();

        /// <summary>
        /// Typy szkół nadzorowane przez użytkownika (dla wicedyrektorów)
        /// Wicedyrektor może nadzorować jeden lub więcej typów szkół
        /// </summary>
        public List<SchoolType> SupervisedSchoolTypes { get; set; } = new List<SchoolType>();

        /// <summary>
        /// Lista przedmiotów, które naucza ten użytkownik (jeśli jest nauczycielem).
        /// </summary>
        public List<UserSubject> TaughtSubjects { get; set; } = new List<UserSubject>();

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Pełne imię i nazwisko
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Nazwa wyświetlana (alias dla FullName)
        /// </summary>
        public string DisplayName => FullName;

        /// <summary>
        /// Email (alias dla UPN)
        /// </summary>
        public string Email => UPN;

        /// <summary>
        /// Inicjały użytkownika
        /// </summary>
        public string Initials
        {
            get
            {
                var first = !string.IsNullOrEmpty(FirstName) ? FirstName[0].ToString() : "";
                var last = !string.IsNullOrEmpty(LastName) ? LastName[0].ToString() : "";
                return $"{first}{last}".ToUpper();
            }
        }

        /// <summary>
        /// Wiek użytkownika (jeśli znana data urodzenia)
        /// </summary>
        public int? Age
        {
            get
            {
                if (!BirthDate.HasValue) return null;
                var age = DateTime.Today.Year - BirthDate.Value.Year;
                if (BirthDate.Value.Date > DateTime.Today.AddYears(-age)) age--;
                return age;
            }
        }

        /// <summary>
        /// Staż pracy w latach (jeśli znana data zatrudnienia)
        /// </summary>
        public double? YearsOfService
        {
            get
            {
                if (!EmploymentDate.HasValue) return null;
                return Math.Round((DateTime.Today - EmploymentDate.Value).TotalDays / 365.25, 1);
            }
        }

        /// <summary>
        /// Nazwa roli wyświetlana z dodatkowymi informacjami
        /// Dla wicedyrektorów zawiera typy nadzorowanych szkół
        /// </summary>
        public string RoleDisplayName
        {
            get
            {
                if (Role == UserRole.Wicedyrektor)
                {
                    // Najpierw filtrujemy aktywne i pobieramy ich ShortName
                    var supervisedActiveSchoolTypeNames = SupervisedSchoolTypes
                        .Where(st => st.IsActive)
                        .Select(st => st.ShortName)
                        .ToList(); // Materializujemy listę, aby uniknąć wielokrotnego zapytania

                    if (supervisedActiveSchoolTypeNames.Any()) // Sprawdzamy, czy lista nazw nie jest pusta
                    {
                        var schoolTypesString = string.Join(", ", supervisedActiveSchoolTypeNames);
                        return $"Wicedyrektor ({schoolTypesString})";
                    }
                    else
                    {
                        // Jeśli nie ma aktywnych nadzorowanych typów szkół, zwróć "Wicedyrektor"
                        return "Wicedyrektor";
                    }
                }

                return Role switch
                {
                    UserRole.Uczen => "Uczeń",
                    UserRole.Sluchacz => "Słuchacz",
                    UserRole.Nauczyciel => "Nauczyciel",
                    UserRole.Dyrektor => "Dyrektor",
                    _ => Role.ToString()
                };
            }
        }

        /// <summary>
        /// Liczba aktywnych członkostw w zespołach
        /// </summary>
        public int ActiveMembershipsCount => TeamMemberships?.Count(tm =>
            tm.IsActive && tm.Team != null && tm.Team.IsActive && tm.Team.Status == TeamStatus.Active) ?? 0;

        /// <summary>
        /// Liczba zespołów gdzie użytkownik jest właścicielem
        /// </summary>
        public int OwnedTeamsCount => TeamMemberships?.Count(tm =>
            tm.IsActive && tm.Role == TeamMemberRole.Owner &&
            tm.Team != null && tm.Team.IsActive && tm.Team.Status == TeamStatus.Active) ?? 0;

        /// <summary>
        /// Lista typów szkół do których przypisany jest użytkownik
        /// </summary>
        public List<SchoolType> AssignedSchoolTypes => SchoolTypeAssignments?
            .Where(sta => sta.IsActive && sta.IsCurrentlyActive && sta.SchoolType.IsActive)
            .Select(sta => sta.SchoolType)
            .ToList() ?? new List<SchoolType>();

        /// <summary>
        /// Czy użytkownik ma uprawnienia do zarządzania zespołami
        /// </summary>
        public bool CanManageTeams => Role >= UserRole.Nauczyciel || IsSystemAdmin;

        /// <summary>
        /// Czy użytkownik ma uprawnienia do zarządzania użytkownikami
        /// </summary>
        public bool CanManageUsers => Role >= UserRole.Wicedyrektor || IsSystemAdmin;

        /// <summary>
        /// Czy użytkownik ma uprawnienia administracyjne
        /// </summary>
        public bool HasAdminRights => Role >= UserRole.Dyrektor || IsSystemAdmin;

        /// <summary>
        /// Domyślna rola w zespole na podstawie roli systemowej
        /// </summary>
        public TeamMemberRole DefaultTeamRole => Role switch
        {
            UserRole.Uczen => TeamMemberRole.Member,
            UserRole.Sluchacz => TeamMemberRole.Member,
            UserRole.Nauczyciel => TeamMemberRole.Owner,
            UserRole.Wicedyrektor => TeamMemberRole.Owner,
            UserRole.Dyrektor => TeamMemberRole.Owner,
            _ => TeamMemberRole.Member
        };

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Sprawdza czy użytkownik jest przypisany do konkretnego typu szkoły
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        /// <returns>True jeśli jest przypisany</returns>
        public bool IsAssignedToSchoolType(string schoolTypeId)
        {
            return SchoolTypeAssignments.Any(sta =>
                sta.SchoolTypeId == schoolTypeId && sta.IsActive && sta.IsCurrentlyActive);
        }

        /// <summary>
        /// Sprawdza czy użytkownik nadzoruje konkretny typ szkoły
        /// </summary>
        /// <param name="schoolTypeId">ID typu szkoły</param>
        /// <returns>True jeśli nadzoruje</returns>
        public bool SupervisesSchoolType(string schoolTypeId)
        {
            return SupervisedSchoolTypes.Any(st => st.Id == schoolTypeId && st.IsActive);
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest członkiem konkretnego zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>True jeśli jest członkiem</returns>
        public bool IsMemberOfTeam(string teamId)
        {
            return TeamMemberships.Any(tm =>
                tm.TeamId == teamId && tm.IsActive &&
                tm.Team != null && tm.Team.IsActive);
        }

        /// <summary>
        /// Sprawdza czy użytkownik jest właścicielem konkretnego zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>True jeśli jest właścicielem</returns>
        public bool IsOwnerOfTeam(string teamId)
        {
            return TeamMemberships.Any(tm =>
                tm.TeamId == teamId && tm.IsActive && tm.Role == TeamMemberRole.Owner &&
                tm.Team != null && tm.Team.IsActive);
        }

        /// <summary>
        /// Oznacza ostatnie logowanie użytkownika
        /// </summary>
        /// <param name="modifiedBy">Osoba wykonująca aktualizację (UPN).</param>
        public void UpdateLastLogin(string? modifiedBy = null)
        {
            LastLoginDate = DateTime.UtcNow;
            MarkAsModified(modifiedBy ?? AuditHelper.SystemLoginUpdate);
        }
    }
}