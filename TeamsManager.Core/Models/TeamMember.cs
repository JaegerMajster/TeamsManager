using System;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Helpers;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Członkostwo użytkownika w zespole
    /// Tabela pośrednia łącząca użytkowników z zespołami (relacja M:N)
    /// Każdy użytkownik może być członkiem wielu zespołów z różnymi rolami
    /// </summary>
    public class TeamMember : BaseEntity
    {
        /// <summary>
        /// Rola użytkownika w konkretnym zespole
        /// </summary>
        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

        /// <summary>
        /// Data dodania użytkownika do zespołu
        /// </summary>
        public DateTime AddedDate { get; set; }

        /// <summary>
        /// Data usunięcia użytkownika z zespołu (opcjonalna)
        /// Jeśli null - członkostwo jest aktywne
        /// </summary>
        public DateTime? RemovedDate { get; set; }

        /// <summary>
        /// Powód usunięcia z zespołu
        /// </summary>
        public string? RemovalReason { get; set; }

        /// <summary>
        /// Osoba która dodała użytkownika do zespołu (UPN)
        /// </summary>
        public string? AddedBy { get; set; }

        /// <summary>
        /// Osoba która usunęła użytkownika z zespołu (UPN)
        /// </summary>
        public string? RemovedBy { get; set; }

        /// <summary>
        /// Data ostatniej zmiany roli w zespole
        /// </summary>
        public DateTime? RoleChangedDate { get; set; }

        /// <summary>
        /// Osoba która zmieniła rolę użytkownika (UPN)
        /// </summary>
        public string? RoleChangedBy { get; set; }

        /// <summary>
        /// Poprzednia rola przed ostatnią zmianą
        /// </summary>
        public TeamMemberRole? PreviousRole { get; set; }

        /// <summary>
        /// Czy członkostwo zostało zatwierdzone przez właściciela zespołu
        /// </summary>
        public bool IsApproved { get; set; } = true;

        /// <summary>
        /// Data zatwierdzenia członkostwa
        /// </summary>
        public DateTime? ApprovedDate { get; set; }

        /// <summary>
        /// Osoba która zatwierdziła członkostwo (UPN)
        /// </summary>
        public string? ApprovedBy { get; set; }

        /// <summary>
        /// Czy użytkownik może publikować w kanałach zespołu
        /// </summary>
        public bool CanPost { get; set; } = true;

        /// <summary>
        /// Czy użytkownik może moderować kanały zespołu
        /// </summary>
        public bool CanModerate { get; set; } = false;

        /// <summary>
        /// Dodatkowe uprawnienia specjalne dla tego użytkownika w zespole
        /// Format JSON z listą uprawnień
        /// </summary>
        public string? CustomPermissions { get; set; }

        /// <summary>
        /// Uwagi dotyczące członkostwa
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Data ostatniej aktywności użytkownika w zespole
        /// </summary>
        public DateTime? LastActivityDate { get; set; }

        /// <summary>
        /// Liczba wiadomości wysłanych przez użytkownika w zespole
        /// </summary>
        public int MessagesCount { get; set; } = 0;

        /// <summary>
        /// Źródło dodania do zespołu (np. "Manual", "Import", "Bulk", "Invitation")
        /// </summary>
        public string? Source { get; set; }

        // ===== KLUCZE OBCE =====

        /// <summary>
        /// Identyfikator zespołu
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator użytkownika
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        // ===== WŁAŚCIWOŚCI NAWIGACYJNE =====

        /// <summary>
        /// Zespół do którego należy użytkownik
        /// </summary>
        public Team? Team { get; set; }

        /// <summary>
        /// Użytkownik będący członkiem zespołu
        /// </summary>
        public User? User { get; set; }

        // ===== WŁAŚCIWOŚCI OBLICZANE (COMPUTED PROPERTIES) =====

        /// <summary>
        /// Email użytkownika (pobierany z User.Email)
        /// </summary>
        public string Email => User?.Email ?? string.Empty;

        /// <summary>
        /// Nazwa wyświetlana użytkownika (pobierana z User.DisplayName)
        /// </summary>
        public string DisplayName => User?.DisplayName ?? string.Empty;

        /// <summary>
        /// Pełne imię i nazwisko użytkownika (pobierane z User.FullName)
        /// </summary>
        public string FullName => User?.FullName ?? string.Empty;

        /// <summary>
        /// Czy członkostwo jest obecnie aktywne
        /// </summary>
        public bool IsMembershipActive => IsActive && !RemovedDate.HasValue && IsApproved;

        /// <summary>
        /// Czy użytkownik jest właścicielem zespołu
        /// </summary>
        public bool IsOwner => Role == TeamMemberRole.Owner;

        /// <summary>
        /// Czy użytkownik jest zwykłym członkiem zespołu
        /// </summary>
        public bool IsMember => Role == TeamMemberRole.Member;

        /// <summary>
        /// Czy użytkownik oczekuje na zatwierdzenie członkostwa
        /// </summary>
        public bool IsPendingApproval => IsActive && !IsApproved && !RemovedDate.HasValue;

        /// <summary>
        /// Liczba dni członkostwa w zespole
        /// </summary>
        public int DaysInTeam
        {
            get
            {
                var endDate = RemovedDate ?? DateTime.Today;
                return Math.Max(0, (endDate - AddedDate.Date).Days);
            }
        }

        /// <summary>
        /// Czy użytkownik był niedawno dodany (mniej niż 7 dni temu)
        /// </summary>
        public bool IsRecentlyAdded => (DateTime.Today - AddedDate.Date).Days < 7;

        /// <summary>
        /// Czy użytkownik był niedawno aktywny (mniej niż 30 dni temu)
        /// </summary>
        public bool IsRecentlyActive => LastActivityDate.HasValue &&
                                        (DateTime.UtcNow - LastActivityDate.Value).Days < 30;

        /// <summary>
        /// Opis roli w czytelnej formie
        /// </summary>
        public string RoleDescription => Role switch
        {
            TeamMemberRole.Owner => "Właściciel",
            TeamMemberRole.Member => "Członek",
            _ => "Nieznana rola"
        };

        /// <summary>
        /// Status członkostwa w czytelnej formie
        /// </summary>
        public string MembershipStatus
        {
            get
            {
                if (!IsActive) return "Nieaktywny";
                if (RemovedDate.HasValue) return "Usunięty";
                if (!IsApproved) return "Oczekuje zatwierdzenia";
                return "Aktywny";
            }
        }

        /// <summary>
        /// Krótki opis członkostwa do wyświetlania w listach
        /// </summary>
        public string MembershipSummary => $"{DisplayName} - {RoleDescription} ({MembershipStatus})";

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Zmienia rolę użytkownika w zespole
        /// </summary>
        /// <param name="newRole">Nowa rola</param>
        /// <param name="changedBy">Osoba wykonująca zmianę (UPN)</param>
        public void ChangeRole(TeamMemberRole newRole, string changedBy)
        {
            if (Role == newRole) return;

            PreviousRole = Role;
            Role = newRole;
            RoleChangedDate = DateTime.UtcNow;
            RoleChangedBy = changedBy;
            MarkAsModified(changedBy);

            // Aktualizuj uprawnienia na podstawie nowej roli
            UpdatePermissionsForRole();
        }

        /// <summary>
        /// Usuwa użytkownika z zespołu
        /// </summary>
        /// <param name="reason">Powód usunięcia</param>
        /// <param name="removedBy">Osoba wykonująca usunięcie (UPN)</param>
        public void RemoveFromTeam(string reason, string removedBy)
        {
            RemovedDate = DateTime.UtcNow;
            RemovalReason = reason;
            RemovedBy = removedBy;
            MarkAsModified(removedBy);
        }

        /// <summary>
        /// Przywraca użytkownika do zespołu (cofnięcie usunięcia)
        /// </summary>
        /// <param name="restoredBy">Osoba wykonująca przywrócenie (UPN)</param>
        public void RestoreToTeam(string restoredBy)
        {
            RemovedDate = null;
            RemovalReason = null;
            RemovedBy = null;
            MarkAsModified(restoredBy);
        }

        /// <summary>
        /// Zatwierdza członkostwo użytkownika
        /// </summary>
        /// <param name="approvedBy">Osoba zatwierdzająca (UPN)</param>
        public void ApproveMembership(string approvedBy)
        {
            IsApproved = true;
            ApprovedDate = DateTime.UtcNow;
            ApprovedBy = approvedBy;
            MarkAsModified(approvedBy);
        }

        /// <summary>
        /// Odrzuca członkostwo użytkownika
        /// </summary>
        /// <param name="reason">Powód odrzucenia</param>
        /// <param name="rejectedBy">Osoba odrzucająca (UPN)</param>
        public void RejectMembership(string reason, string rejectedBy)
        {
            RemoveFromTeam($"Członkostwo odrzucone: {reason}", rejectedBy);
        }

        /// <summary>
        /// Aktualizuje datę ostatniej aktywności
        /// </summary>
        /// <param name="modifiedBy">Osoba wykonująca aktualizację (UPN).</param>
        public void UpdateLastActivity(string? modifiedBy = null)
        {
            LastActivityDate = DateTime.UtcNow;
            MarkAsModified(modifiedBy ?? AuditHelper.SystemActivityUpdate);
        }

        /// <summary>
        /// Zwiększa licznik wysłanych wiadomości
        /// </summary>
        /// <param name="modifiedBy">Osoba wykonująca aktualizację (UPN).</param>
        public void IncrementMessageCount(string? modifiedBy = null)
        {
            MessagesCount++;
            UpdateLastActivity(modifiedBy);
        }

        /// <summary>
        /// Aktualizuje uprawnienia na podstawie roli
        /// </summary>
        private void UpdatePermissionsForRole()
        {
            switch (Role)
            {
                case TeamMemberRole.Owner:
                    CanPost = true;
                    CanModerate = true;
                    break;
                case TeamMemberRole.Member:
                    CanPost = true;
                    CanModerate = false;
                    break;
            }
        }

        /// <summary>
        /// Sprawdza czy użytkownik ma określone uprawnienie
        /// </summary>
        /// <param name="permission">Nazwa uprawnienia</param>
        /// <returns>True jeśli ma uprawnienie</returns>
        public bool HasPermission(string permission)
        {
            // Właściciele mają wszystkie uprawnienia
            if (Role == TeamMemberRole.Owner) return true;

            // Podstawowe uprawnienia na podstawie roli
            switch (permission.ToLower())
            {
                case "post":
                    return CanPost;
                case "moderate":
                    return CanModerate;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Sprawdza czy członkostwo jest ważne w określonej dacie
        /// </summary>
        /// <param name="date">Data do sprawdzenia</param>
        /// <returns>True jeśli członkostwo było aktywne w tej dacie</returns>
        public bool WasActiveOnDate(DateTime date)
        {
            if (!IsActive) return false;
            if (date < AddedDate.Date) return false;
            if (RemovedDate.HasValue && date > RemovedDate.Value.Date) return false;
            return true;
        }
    }
}