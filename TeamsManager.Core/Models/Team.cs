using System;
using System.Collections.Generic;
using System.Linq;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Zespół Microsoft Teams
    /// Reprezentuje grupę edukacyjną, klasę, kurs lub inną jednostkę dydaktyczną.
    /// Dziedziczy z BaseEntity dla wspólnych pól audytu i flagi aktywności.
    /// </summary>
    public class Team : BaseEntity
    {
        /// <summary>
        /// Nazwa wyświetlana zespołu.
        /// Może być generowana na podstawie szablonu lub wprowadzona ręcznie.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Szczegółowy opis zespołu, jego celów i zakresu.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// User Principal Name (UPN) głównego właściciela zespołu.
        /// </summary>
        public string Owner { get; set; } = string.Empty;

        /// <summary>
        /// Aktualny status zespołu (np. Aktywny, Zarchiwizowany).
        /// </summary>
        public TeamStatus Status { get; set; } = TeamStatus.Active;

        /// <summary>
        /// Data ostatniej zmiany statusu zespołu.
        /// </summary>
        public DateTime? StatusChangeDate { get; set; }

        /// <summary>
        /// UPN użytkownika, który ostatnio zmienił status zespołu.
        /// </summary>
        public string? StatusChangedBy { get; set; }

        /// <summary>
        /// Powód ostatniej zmiany statusu (np. powód archiwizacji).
        /// </summary>
        public string? StatusChangeReason { get; set; }

        /// <summary>
        /// Identyfikator szablonu (TeamTemplate), który został użyty do utworzenia zespołu (opcjonalnie).
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// Identyfikator typu szkoły (SchoolType), do którego przypisany jest ten zespół (opcjonalnie).
        /// </summary>
        public string? SchoolTypeId { get; set; }

        /// <summary>
        /// Identyfikator roku szkolnego (SchoolYear), w ramach którego zespół funkcjonuje (opcjonalnie).
        /// </summary>
        public string? SchoolYearId { get; set; }

        /// <summary>
        /// Nazwa roku akademickiego/szkolnego w formacie tekstowym (np. "2024/2025").
        /// </summary>
        public string? AcademicYear { get; set; }

        /// <summary>
        /// Nazwa semestru (np. "Zimowy", "Letni", "I", "II").
        /// </summary>
        public string? Semester { get; set; }

        /// <summary>
        /// Data planowanego rozpoczęcia kursu/zajęć w ramach zespołu.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Data planowanego zakończenia kursu/zajęć w ramach zespołu.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Maksymalna dopuszczalna liczba członków w zespole. Null oznacza brak limitu.
        /// </summary>
        public int? MaxMembers { get; set; }

        /// <summary>
        /// Zewnętrzny identyfikator zespołu, np. z systemu dziekanatowego lub dziennika elektronicznego.
        /// </summary>
        public string? ExternalId { get; set; }

        /// <summary>
        /// Kod kursu lub przedmiotu powiązanego z zespołem.
        /// </summary>
        public string? CourseCode { get; set; }

        /// <summary>
        /// Przewidywana całkowita liczba godzin dla kursu/przedmiotu.
        /// </summary>
        public int? TotalHours { get; set; }

        /// <summary>
        /// Poziom zaawansowania kursu/przedmiotu (np. "Podstawowy", "Średniozaawansowany", "Zaawansowany").
        /// </summary>
        public string? Level { get; set; }

        /// <summary>
        /// Główny język prowadzenia zajęć w zespole. Domyślnie "Polski".
        /// </summary>
        public string? Language { get; set; } = "Polski";

        /// <summary>
        /// Dodatkowe znaczniki (tagi) ułatwiające kategoryzację i wyszukiwanie zespołu.
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Dodatkowe uwagi lub notatki dotyczące zespołu.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Określa, czy zespół powinien być widoczny np. w publicznym katalogu zespołów.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Określa, czy dołączenie do zespołu wymaga zatwierdzenia przez właściciela.
        /// </summary>
        public bool RequiresApproval { get; set; } = true;

        /// <summary>
        /// Data ostatniej zarejestrowanej aktywności w zespole.
        /// </summary>
        public DateTime? LastActivityDate { get; set; }

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Lista członkostw (TeamMember) w tym zespole.
        /// </summary>
        public List<TeamMember> Members { get; set; } = new List<TeamMember>();

        /// <summary>
        /// Lista kanałów (Channel) należących do tego zespołu.
        /// </summary>
        public List<Channel> Channels { get; set; } = new List<Channel>();

        /// <summary>
        /// Szablon (TeamTemplate), na podstawie którego zespół został utworzony (jeśli dotyczy).
        /// </summary>
        public TeamTemplate? Template { get; set; }

        /// <summary>
        /// Typ szkoły (SchoolType), do którego przypisany jest ten zespół.
        /// </summary>
        public SchoolType? SchoolType { get; set; }

        /// <summary>
        /// Rok szkolny (SchoolYear), w ramach którego funkcjonuje ten zespół.
        /// </summary>
        public SchoolYear? SchoolYear { get; set; }

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Określa, czy zespół jest uznawany za funkcjonalnie aktywny na podstawie jego statusu domenowego.
        /// Używane do odróżnienia od BaseEntity.IsActive, które oznacza ogólną aktywność rekordu.
        /// </summary>
        public bool IsEffectivelyActive => Status == TeamStatus.Active;

        /// <summary>
        /// Sprawdza, czy zespół jest w pełni operacyjny:
        /// rekord jest aktywny (BaseEntity.IsActive), status domenowy to Aktywny,
        /// oraz bieżąca data mieści się w okresie funkcjonowania zespołu (StartDate - EndDate).
        /// </summary>
        public bool IsFullyOperational
        {
            get
            {
                if (!this.IsActive || this.Status != TeamStatus.Active) return false;

                var now = DateTime.Today;
                if (StartDate.HasValue && now < StartDate.Value.Date) return false;
                if (EndDate.HasValue && now > EndDate.Value.Date) return false;
                return true;
            }
        }

        /// <summary>
        /// Liczba aktywnych członkostw, gdzie zarówno samo członkostwo (TeamMember.IsActive),
        /// jak i powiązany użytkownik (User.IsActive) są aktywne.
        /// </summary>
        public int MemberCount => Members?.Count(m => m.IsActive && m.User != null && m.User.IsActive) ?? 0;

        /// <summary>
        /// Liczba aktywnych właścicieli zespołu (filtruje MemberCount po roli Owner).
        /// </summary>
        public int OwnerCount => Members?.Count(m => m.IsActive && m.Role == TeamMemberRole.Owner && m.User != null && m.User.IsActive) ?? 0;

        /// <summary>
        /// Liczba aktywnych zwykłych członków zespołu (filtruje MemberCount po roli Member).
        /// </summary>
        public int RegularMemberCount => Members?.Count(m => m.IsActive && m.Role == TeamMemberRole.Member && m.User != null && m.User.IsActive) ?? 0;

        /// <summary>
        /// Lista wszystkich aktywnych użytkowników (User) w zespole,
        /// na podstawie aktywnych członkostw i aktywnych użytkowników.
        /// </summary>
        public List<User> AllActiveUsers => Members?
            .Where(m => m.IsActive && m.User != null && m.User.IsActive)
            .Select(m => m.User!)
            .ToList() ?? new List<User>();

        /// <summary>
        /// Lista aktywnych właścicieli zespołu jako obiekty User.
        /// </summary>
        public List<User> Owners => Members?
            .Where(m => m.IsActive && m.Role == TeamMemberRole.Owner && m.User != null && m.User.IsActive)
            .Select(m => m.User!)
            .ToList() ?? new List<User>();

        /// <summary>
        /// Lista aktywnych zwykłych członków zespołu jako obiekty User.
        /// </summary>
        public List<User> RegularMembers => Members?
            .Where(m => m.IsActive && m.Role == TeamMemberRole.Member && m.User != null && m.User.IsActive)
            .Select(m => m.User!)
            .ToList() ?? new List<User>();

        /// <summary>
        /// Czy zespół osiągnął maksymalną dopuszczalną liczbę członków.
        /// </summary>
        public bool IsAtCapacity => MaxMembers.HasValue && MemberCount >= MaxMembers.Value;

        /// <summary>
        /// Procent zapełnienia zespołu, jeśli określono limit MaxMembers.
        /// </summary>
        public double? CapacityPercentage
        {
            get
            {
                if (!MaxMembers.HasValue || MaxMembers.Value == 0) return null;
                return Math.Round((double)MemberCount / MaxMembers.Value * 100, 1);
            }
        }

        /// <summary>
        /// Liczba aktywnych kanałów (Channel.IsActive i Channel.Status == Active) w zespole.
        /// </summary>
        public int ChannelCount => Channels?.Count(c => c.IsActive && c.Status == ChannelStatus.Active) ?? 0;

        /// <summary>
        /// Liczba dni pozostałych do planowanej daty zakończenia funkcjonowania zespołu.
        /// </summary>
        public int? DaysUntilEnd
        {
            get
            {
                if (!EndDate.HasValue) return null;
                var days = (EndDate.Value.Date - DateTime.Today).Days;
                return Math.Max(0, days);
            }
        }

        /// <summary>
        /// Liczba dni, które upłynęły od planowanej daty rozpoczęcia funkcjonowania zespołu.
        /// </summary>
        public int? DaysSinceStart
        {
            get
            {
                if (!StartDate.HasValue) return null;
                return Math.Max(0, (DateTime.Today - StartDate.Value.Date).Days);
            }
        }

        /// <summary>
        /// Procentowy postęp trwania zespołu/kursu (0-100) na podstawie StartDate i EndDate.
        /// </summary>
        public double? CompletionPercentage
        {
            get
            {
                if (!StartDate.HasValue || !EndDate.HasValue) return null;

                var totalDuration = (EndDate.Value - StartDate.Value).TotalDays;
                if (totalDuration <= 0) return StartDate.Value.Date <= DateTime.Today ? 100 : 0;

                var elapsedDuration = (DateTime.Today - StartDate.Value.Date).TotalDays;
                if (elapsedDuration < 0) return 0;
                if (elapsedDuration >= totalDuration) return 100;

                return Math.Round(elapsedDuration / totalDuration * 100, 1);
            }
        }

        /// <summary>
        /// Nazwa wyświetlana zespołu z dodanym prefiksem "ARCHIWALNY - ", jeśli zespół ma status Archived.
        /// </summary>
        public string DisplayNameWithStatus
        {
            get
            {
                if (Status == TeamStatus.Archived)
                {
                    var baseName = DisplayName.StartsWith("ARCHIWALNY - ")
                        ? DisplayName.Substring("ARCHIWALNY - ".Length)
                        : DisplayName;
                    return $"ARCHIWALNY - {baseName}";
                }
                return DisplayName.StartsWith("ARCHIWALNY - ")
                    ? DisplayName.Substring("ARCHIWALNY - ".Length)
                    : DisplayName;
            }
        }

        /// <summary>
        /// Krótki, zagregowany opis zespołu do wyświetlania w listach lub podglądach.
        /// </summary>
        public string ShortDescription
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(AcademicYear)) parts.Add(AcademicYear);
                if (!string.IsNullOrEmpty(Semester)) parts.Add(Semester);
                if (SchoolType != null && SchoolType.IsActive) parts.Add(SchoolType.ShortName);
                if (MemberCount > 0) parts.Add($"{MemberCount} osób");
                return parts.Any() ? string.Join(" • ", parts) : "Zespół";
            }
        }

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Archiwizuje zespół, ustawiając odpowiedni status i szczegóły operacji.
        /// Aktualizuje nazwę i opis dodając prefiks "ARCHIWALNY - ".
        /// Ustawia flagę BaseEntity.IsActive na false.
        /// </summary>
        /// <param name="reason">Powód archiwizacji.</param>
        /// <param name="archivedBy">UPN użytkownika dokonującego archiwizacji.</param>
        public void Archive(string reason, string archivedBy)
        {
            if (Status == TeamStatus.Archived) return;

            Status = TeamStatus.Archived;
            this.IsActive = false;

            StatusChangeDate = DateTime.UtcNow;
            StatusChangedBy = archivedBy;
            StatusChangeReason = reason;

            if (!DisplayName.StartsWith("ARCHIWALNY - ")) // Ta logika jest OK
            {
                DisplayName = $"ARCHIWALNY - {DisplayName}";
            }
            if (!string.IsNullOrEmpty(Description) && !Description.StartsWith("ARCHIWALNY - "))
            {
                Description = $"ARCHIWALNY - {Description}";
            }

            MarkAsModified(archivedBy);
        }

        /// <summary>
        /// Przywraca zespół z archiwum, ustawiając status na aktywny, usuwając prefiksy
        /// i ustawiając BaseEntity.IsActive na true.
        /// </summary>
        /// <param name="restoredBy">UPN użytkownika dokonującego przywrócenia.</param>
        public void Restore(string restoredBy)
        {
            if (Status == TeamStatus.Active) return;

            Status = TeamStatus.Active;
            this.IsActive = true;

            StatusChangeDate = DateTime.UtcNow;
            StatusChangedBy = restoredBy;
            StatusChangeReason = "Przywrócono z archiwum";

            if (DisplayName.StartsWith("ARCHIWALNY - ")) // Ta logika jest OK
            {
                DisplayName = DisplayName.Substring("ARCHIWALNY - ".Length);
            }
            if (!string.IsNullOrEmpty(Description) && Description.StartsWith("ARCHIWALNY - "))
            {
                Description = Description.Substring("ARCHIWALNY - ".Length);
            }

            MarkAsModified(restoredBy);
        }

        /// <summary>
        /// Sprawdza, czy użytkownik o podanym ID jest aktywnym członkiem zespołu
        /// (zarówno członkostwo, jak i sam użytkownik są aktywni).
        /// </summary>
        /// <param name="userId">ID użytkownika.</param>
        /// <returns>True, jeśli użytkownik jest aktywnym członkiem.</returns>
        public bool HasMember(string userId)
        {
            return Members.Any(m => m.UserId == userId && m.IsActive && m.User != null && m.User.IsActive);
        }

        /// <summary>
        /// Sprawdza, czy użytkownik o podanym ID jest aktywnym właścicielem zespołu
        /// (zarówno członkostwo, jak i sam użytkownik są aktywni, a rola to Owner).
        /// </summary>
        /// <param name="userId">ID użytkownika.</param>
        /// <returns>True, jeśli użytkownik jest aktywnym właścicielem.</returns>
        public bool HasOwner(string userId)
        {
            return Members.Any(m => m.UserId == userId && m.IsActive && m.Role == TeamMemberRole.Owner && m.User != null && m.User.IsActive);
        }

        /// <summary>
        /// Pobiera aktywne członkostwo konkretnego użytkownika w tym zespole.
        /// Uwzględnia aktywność członkostwa i użytkownika.
        /// </summary>
        /// <param name="userId">ID użytkownika.</param>
        /// <returns>Obiekt TeamMember reprezentujący aktywne członkostwo lub null, jeśli brak.</returns>
        public TeamMember? GetMembership(string userId)
        {
            return Members.FirstOrDefault(m => m.UserId == userId && m.IsActive && m.User != null && m.User.IsActive);
        }

        /// <summary>
        /// Sprawdza, czy można dodać więcej członków do zespołu, uwzględniając MaxMembers.
        /// Bazuje na MemberCount, które liczy aktywnych członków (aktywne członkostwa aktywnych użytkowników).
        /// </summary>
        /// <returns>True, jeśli można dodać więcej członków.</returns>
        public bool CanAddMoreMembers()
        {
            return !MaxMembers.HasValue || MemberCount < MaxMembers.Value;
        }

        /// <summary>
        /// Aktualizuje datę ostatniej aktywności zespołu na bieżącą datę i czas UTC.
        /// </summary>
        public void UpdateLastActivity()
        {
            LastActivityDate = DateTime.UtcNow;
        }
    }
}