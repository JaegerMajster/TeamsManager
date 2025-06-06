using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora procesów związanych z rokiem szkolnym
    /// Realizuje główne scenariusze biznesowe automatyzacji procesu tworzenia zespołów
    /// </summary>
    public interface ISchoolYearProcessOrchestrator
    {
        /// <summary>
        /// Główny proces: Tworzy zespoły dla nowego roku szkolnego na podstawie szablonów
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego</param>
        /// <param name="templateIds">Lista ID szablonów do użycia</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <param name="options">Opcjonalne ustawienia procesu</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> CreateTeamsForNewSchoolYearAsync(
            string schoolYearId, 
            string[] templateIds, 
            string apiAccessToken,
            SchoolYearProcessOptions? options = null);

        /// <summary>
        /// Archiwizuje zespoły z poprzedniego roku szkolnego
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego do archiwizacji</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <param name="options">Opcjonalne ustawienia procesu</param>
        /// <returns>Wynik operacji masowej</returns>
        Task<BulkOperationResult> ArchiveTeamsFromPreviousSchoolYearAsync(
            string schoolYearId,
            string apiAccessToken,
            SchoolYearProcessOptions? options = null);

        /// <summary>
        /// Kompleksowy proces przejścia na nowy rok szkolny
        /// (archiwizacja starych + tworzenie nowych zespołów)
        /// </summary>
        /// <param name="oldSchoolYearId">ID starego roku szkolnego</param>
        /// <param name="newSchoolYearId">ID nowego roku szkolnego</param>
        /// <param name="templateIds">Szablony dla nowych zespołów</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <param name="options">Opcjonalne ustawienia procesu</param>
        /// <returns>Wynik operacji masowej</returns>
        Task<BulkOperationResult> TransitionToNewSchoolYearAsync(
            string oldSchoolYearId,
            string newSchoolYearId,
            string[] templateIds,
            string apiAccessToken,
            SchoolYearProcessOptions? options = null);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów
        /// </summary>
        /// <returns>Status aktywnych procesów</returns>
        Task<IEnumerable<SchoolYearProcessStatus>> GetActiveProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelProcessAsync(string processId);
    }

    /// <summary>
    /// Opcje konfiguracji procesu roku szkolnego
    /// </summary>
    public class SchoolYearProcessOptions
    {
        /// <summary>
        /// Rozmiar batcha dla operacji masowych (domyślnie 10)
        /// </summary>
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach, domyślnie 60)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Czy wysyłać powiadomienia administratorom
        /// </summary>
        public bool SendAdminNotifications { get; set; } = true;

        /// <summary>
        /// Czy wysyłać powiadomienia użytkownikom końcowym
        /// </summary>
        public bool SendUserNotifications { get; set; } = false;

        /// <summary>
        /// Czy symulować operacje (dry run) bez rzeczywistych zmian
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Typy szkół do uwzględnienia (jeśli puste, wszystkie)
        /// </summary>
        public string[]? SchoolTypeIds { get; set; }

        /// <summary>
        /// Departamenty do uwzględnienia (jeśli puste, wszystkie)
        /// </summary>
        public string[]? DepartmentIds { get; set; }

        /// <summary>
        /// Dodatkowe tagi dla operacji w historii
        /// </summary>
        public string? AdditionalTags { get; set; }

        /// <summary>
        /// Maksymalna liczba równoległych operacji
        /// </summary>
        public int MaxConcurrency { get; set; } = 5;

        /// <summary>
        /// Czy kontynuować przy błędach (true) czy zatrzymać cały proces (false)
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Procent akceptowalnych błędów (0-100, domyślnie 10%)
        /// </summary>
        public double AcceptableErrorPercentage { get; set; } = 10.0;
    }

    /// <summary>
    /// Status procesu roku szkolnego
    /// </summary>
    public class SchoolYearProcessStatus
    {
        public string ProcessId { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty;
        public string SchoolYearId { get; set; } = string.Empty;
        public string SchoolYearName { get; set; } = string.Empty;
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
    }
} 