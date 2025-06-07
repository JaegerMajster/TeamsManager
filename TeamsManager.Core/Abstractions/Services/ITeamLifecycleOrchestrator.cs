using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora cyklu życia zespołów
    /// Koordynuje kompleksowe operacje archiwizacji, przywracania i migracji zespołów
    /// Następuje wzorce z ISchoolYearProcessOrchestrator
    /// </summary>
    public interface ITeamLifecycleOrchestrator
    {
        /// <summary>
        /// Masowa archiwizacja zespołów z opcjonalnym cleanup
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do archiwizacji</param>
        /// <param name="options">Opcje procesu archiwizacji</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkArchiveTeamsWithCleanupAsync(
            string[] teamIds, 
            ArchiveOptions options,
            string apiAccessToken);

        /// <summary>
        /// Masowe przywracanie zespołów z walidacją
        /// </summary>
        /// <param name="teamIds">Lista ID zespołów do przywrócenia</param>
        /// <param name="options">Opcje procesu przywracania</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> BulkRestoreTeamsWithValidationAsync(
            string[] teamIds, 
            RestoreOptions options,
            string apiAccessToken);

        /// <summary>
        /// Migracja zespołów między latami szkolnymi
        /// </summary>
        /// <param name="plan">Plan migracji zespołów</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> MigrateTeamsBetweenSchoolYearsAsync(
            TeamMigrationPlan plan,
            string apiAccessToken);

        /// <summary>
        /// Konsolidacja nieaktywnych zespołów
        /// </summary>
        /// <param name="options">Opcje konsolidacji</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> ConsolidateInactiveTeamsAsync(
            ConsolidationOptions options,
            string apiAccessToken);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów cyklu życia
        /// </summary>
        /// <returns>Status aktywnych procesów</returns>
        Task<IEnumerable<TeamLifecycleProcessStatus>> GetActiveProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelProcessAsync(string processId);
    }

    /// <summary>
    /// Opcje archiwizacji zespołów
    /// </summary>
    public class ArchiveOptions
    {
        /// <summary>
        /// Powód archiwizacji
        /// </summary>
        public string Reason { get; set; } = "Masowa archiwizacja";

        /// <summary>
        /// Czy wykonać backup przed archiwizacją
        /// </summary>
        public bool CreateBackup { get; set; } = false;

        /// <summary>
        /// Czy wysyłać powiadomienia właścicielom zespołów
        /// </summary>
        public bool NotifyOwners { get; set; } = true;

        /// <summary>
        /// Czy usunąć niepotrzebne kanały
        /// </summary>
        public bool CleanupChannels { get; set; } = false;

        /// <summary>
        /// Czy usunąć nieaktywnych członków
        /// </summary>
        public bool RemoveInactiveMembers { get; set; } = false;

        /// <summary>
        /// Rozmiar batcha dla operacji masowych
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Czy symulować operacje (dry run)
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Czy kontynuować przy błędach
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Procent akceptowalnych błędów (0-100)
        /// </summary>
        public double AcceptableErrorPercentage { get; set; } = 10.0;
    }

    /// <summary>
    /// Opcje przywracania zespołów
    /// </summary>
    public class RestoreOptions
    {
        /// <summary>
        /// Czy walidować dostępność właścicieli
        /// </summary>
        public bool ValidateOwnerAvailability { get; set; } = true;

        /// <summary>
        /// Czy przywrócić członków zespołów
        /// </summary>
        public bool RestoreMembers { get; set; } = true;

        /// <summary>
        /// Czy przywrócić kanały
        /// </summary>
        public bool RestoreChannels { get; set; } = true;

        /// <summary>
        /// Rozmiar batcha dla operacji masowych
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Czy symulować operacje (dry run)
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Czy kontynuować przy błędach
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Procent akceptowalnych błędów (0-100)
        /// </summary>
        public double AcceptableErrorPercentage { get; set; } = 10.0;
    }

    /// <summary>
    /// Plan migracji zespołów między latami szkolnymi
    /// </summary>
    public class TeamMigrationPlan
    {
        /// <summary>
        /// ID starego roku szkolnego
        /// </summary>
        public string FromSchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// ID nowego roku szkolnego
        /// </summary>
        public string ToSchoolYearId { get; set; } = string.Empty;

        /// <summary>
        /// Lista ID zespołów do migracji
        /// </summary>
        public string[] TeamIds { get; set; } = [];

        /// <summary>
        /// Czy archiwizować stare zespoły
        /// </summary>
        public bool ArchiveSourceTeams { get; set; } = true;

        /// <summary>
        /// Czy kopiować członków zespołów
        /// </summary>
        public bool CopyMembers { get; set; } = true;

        /// <summary>
        /// Czy kopiować kanały
        /// </summary>
        public bool CopyChannels { get; set; } = false;

        /// <summary>
        /// Rozmiar batcha dla operacji masowych
        /// </summary>
        public int BatchSize { get; set; } = 5;

        /// <summary>
        /// Czy kontynuować przy błędach
        /// </summary>
        public bool ContinueOnError { get; set; } = true;
    }

    /// <summary>
    /// Opcje konsolidacji nieaktywnych zespołów
    /// </summary>
    public class ConsolidationOptions
    {
        /// <summary>
        /// Minimalna liczba dni nieaktywności do konsolidacji
        /// </summary>
        public int MinInactiveDays { get; set; } = 90;

        /// <summary>
        /// Maksymalna liczba członków dla konsolidacji
        /// </summary>
        public int MaxMembersCount { get; set; } = 5;

        /// <summary>
        /// Czy tylko zespoły bez aktywności
        /// </summary>
        public bool OnlyTeamsWithoutActivity { get; set; } = true;

        /// <summary>
        /// Lista typów szkół do uwzględnienia
        /// </summary>
        public string[]? SchoolTypeIds { get; set; }

        /// <summary>
        /// Rozmiar batcha dla operacji masowych
        /// </summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>
        /// Czy symulować operacje (dry run)
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Czy kontynuować przy błędach
        /// </summary>
        public bool ContinueOnError { get; set; } = true;
    }

    /// <summary>
    /// Status procesu cyklu życia zespołu
    /// </summary>
    public class TeamLifecycleProcessStatus
    {
        public string ProcessId { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int FailedItems { get; set; }
        public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
        public string? CurrentOperation { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanBeCancelled { get; set; } = true;
        public string[]? AffectedTeamIds { get; set; }
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
} 