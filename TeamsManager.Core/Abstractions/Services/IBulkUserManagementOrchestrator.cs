using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora zarządzania masowymi operacjami na użytkownikach
    /// Koordynuje kompleksowe operacje HR: onboarding, offboarding, zmiany ról i członkostwa w zespołach
    /// Następuje wzorce z ISchoolYearProcessOrchestrator i ITeamLifecycleOrchestrator
    /// </summary>
    public interface IBulkUserManagementOrchestrator
    {
        /// <summary>
        /// Masowy onboarding użytkowników - kompleksowy proces wprowadzania nowych użytkowników
        /// </summary>
        /// <param name="plans">Lista planów onboardingu użytkowników</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkUserOnboardingAsync(
            UserOnboardingPlan[] plans,
            string apiAccessToken);

        /// <summary>
        /// Masowy offboarding użytkowników - kompleksowy proces usuwania użytkowników z organizacji
        /// </summary>
        /// <param name="userIds">Lista ID użytkowników do offboardingu</param>
        /// <param name="options">Opcje procesu offboardingu</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkUserOffboardingAsync(
            string[] userIds,
            OffboardingOptions options,
            string apiAccessToken);

        /// <summary>
        /// Masowa zmiana ról użytkowników w systemie
        /// </summary>
        /// <param name="changes">Lista zmian ról użytkowników</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkRoleChangeAsync(
            UserRoleChange[] changes,
            string apiAccessToken);

        /// <summary>
        /// Masowe operacje członkostwa w zespołach (dodawanie/usuwanie z wielu zespołów)
        /// </summary>
        /// <param name="operations">Lista operacji członkostwa</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkTeamMembershipOperationAsync(
            TeamMembershipOperation[] operations,
            string apiAccessToken);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów
        /// </summary>
        /// <returns>Status aktywnych procesów</returns>
        Task<IEnumerable<UserManagementProcessStatus>> GetActiveProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelProcessAsync(string processId);
    }

    /// <summary>
    /// Plan onboardingu pojedynczego użytkownika
    /// </summary>
    public class UserOnboardingPlan
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
        /// User Principal Name
        /// </summary>
        public string UPN { get; set; } = string.Empty;

        /// <summary>
        /// Rola systemowa użytkownika
        /// </summary>
        public UserRole Role { get; set; }

        /// <summary>
        /// ID działu do przypisania
        /// </summary>
        public string DepartmentId { get; set; } = string.Empty;

        /// <summary>
        /// Hasło dla nowego konta M365
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Lista ID zespołów do których dodać użytkownika
        /// </summary>
        public string[]? TeamIds { get; set; }

        /// <summary>
        /// Lista ID typów szkół do przypisania (dla nauczycieli)
        /// </summary>
        public string[]? SchoolTypeIds { get; set; }

        /// <summary>
        /// Lista ID przedmiotów do przypisania (dla nauczycieli)
        /// </summary>
        public string[]? SubjectIds { get; set; }

        /// <summary>
        /// Czy wysłać email powitalny
        /// </summary>
        public bool SendWelcomeEmail { get; set; } = true;
    }

    /// <summary>
    /// Opcje procesu offboardingu użytkowników
    /// </summary>
    public class OffboardingOptions
    {
        /// <summary>
        /// Rozmiar batcha dla operacji masowych (domyślnie 20)
        /// </summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>
        /// Czy transferować własność zespołów na innych użytkowników
        /// </summary>
        public bool TransferTeamOwnership { get; set; } = true;

        /// <summary>
        /// ID użytkownika zastępczego dla przeniesienia własności zespołów
        /// Jeśli null, system automatycznie wybierze wicedyrektora lub dyrektora
        /// </summary>
        public string? FallbackOwnerId { get; set; }

        /// <summary>
        /// Czy utworzyć backup danych przed usunięciem
        /// </summary>
        public bool CreateDataBackup { get; set; } = true;

        /// <summary>
        /// Czy dezaktywować konta M365
        /// </summary>
        public bool DeactivateM365Accounts { get; set; } = true;

        /// <summary>
        /// Czy wysyłać powiadomienia HR o zakończeniu procesu
        /// </summary>
        public bool SendHRNotifications { get; set; } = true;

        /// <summary>
        /// Czy kontynuować przy błędach (true) czy zatrzymać cały proces (false)
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Procent akceptowalnych błędów (0-100, domyślnie 15%)
        /// </summary>
        public double AcceptableErrorPercentage { get; set; } = 15.0;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach, domyślnie 45)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 45;
    }

    /// <summary>
    /// Zmiana roli użytkownika
    /// </summary>
    public class UserRoleChange
    {
        /// <summary>
        /// ID użytkownika
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Obecna rola użytkownika
        /// </summary>
        public UserRole CurrentRole { get; set; }

        /// <summary>
        /// Nowa rola użytkownika
        /// </summary>
        public UserRole NewRole { get; set; }

        /// <summary>
        /// Powód zmiany roli
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Czy zaktualizować uprawnienia w M365
        /// </summary>
        public bool UpdateM365Permissions { get; set; } = true;

        /// <summary>
        /// Czy automatycznie dostosować członkostwa w zespołach
        /// </summary>
        public bool AdjustTeamMemberships { get; set; } = true;
    }

    /// <summary>
    /// Operacja członkostwa w zespole
    /// </summary>
    public class TeamMembershipOperation
    {
        /// <summary>
        /// Typ operacji
        /// </summary>
        public TeamMembershipOperationType OperationType { get; set; }

        /// <summary>
        /// ID użytkownika
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// ID zespołu
        /// </summary>
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Rola w zespole (dla operacji dodawania)
        /// </summary>
        public TeamMemberRole? Role { get; set; }

        /// <summary>
        /// Powód operacji
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Typ operacji członkostwa w zespole
    /// </summary>
    public enum TeamMembershipOperationType
    {
        Add = 1,        // Dodanie do zespołu
        Remove = 2,     // Usunięcie z zespołu
        ChangeRole = 3  // Zmiana roli w zespole
    }

    /// <summary>
    /// Status procesu zarządzania użytkownikami
    /// </summary>
    public class UserManagementProcessStatus
    {
        /// <summary>
        /// ID procesu
        /// </summary>
        public string ProcessId { get; set; } = string.Empty;

        /// <summary>
        /// Typ procesu
        /// </summary>
        public string ProcessType { get; set; } = string.Empty;

        /// <summary>
        /// Data rozpoczęcia
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Data zakończenia
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Status procesu
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Łączna liczba elementów do przetworzenia
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Liczba przetworzonych elementów
        /// </summary>
        public int ProcessedItems { get; set; }

        /// <summary>
        /// Liczba błędów
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// Procent postępu
        /// </summary>
        public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;

        /// <summary>
        /// Aktualna operacja
        /// </summary>
        public string? CurrentOperation { get; set; }

        /// <summary>
        /// Komunikat błędu
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Czy proces może być anulowany
        /// </summary>
        public bool CanBeCancelled { get; set; } = true;

        /// <summary>
        /// Lista ID użytkowników objętych procesem
        /// </summary>
        public string[]? AffectedUserIds { get; set; }

        /// <summary>
        /// Dodatkowe dane procesu
        /// </summary>
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
} 