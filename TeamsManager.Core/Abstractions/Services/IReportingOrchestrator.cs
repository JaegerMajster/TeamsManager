using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora raportowania
    /// Odpowiedzialny za generowanie raportów biznesowych, compliance i eksport danych systemu
    /// Następuje wzorce z ISchoolYearProcessOrchestrator i IDataImportOrchestrator
    /// </summary>
    public interface IReportingOrchestrator
    {
        /// <summary>
        /// Generuje raport podsumowujący dla roku szkolnego
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego</param>
        /// <param name="options">Opcje generowania raportu</param>
        /// <returns>Wynik operacji z danymi raportu</returns>
        Task<ReportOperationResult> GenerateSchoolYearReportAsync(string schoolYearId, ReportOptions options);

        /// <summary>
        /// Generuje raport aktywności użytkowników w systemie
        /// </summary>
        /// <param name="fromDate">Data początkowa okresu</param>
        /// <param name="toDate">Data końcowa okresu</param>
        /// <returns>Wynik operacji z danymi aktywności</returns>
        Task<ReportOperationResult> GenerateUserActivityReportAsync(DateTime fromDate, DateTime toDate);

        /// <summary>
        /// Generuje raport compliance zgodnie z wymaganiami
        /// </summary>
        /// <param name="type">Typ raportu compliance</param>
        /// <returns>Wynik operacji z raportem compliance</returns>
        Task<ReportOperationResult> GenerateComplianceReportAsync(ComplianceReportType type);

        /// <summary>
        /// Eksportuje dane systemowe do pliku
        /// </summary>
        /// <param name="options">Opcje eksportu danych</param>
        /// <returns>Wynik operacji z plikiem eksportu</returns>
        Task<ReportOperationResult> ExportSystemDataAsync(ExportOptions options);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów raportowania
        /// </summary>
        /// <returns>Status aktywnych procesów raportowania</returns>
        Task<IEnumerable<ReportingProcessStatus>> GetActiveProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces raportowania (jeśli to możliwe)
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelProcessAsync(string processId);
    }

    /// <summary>
    /// Opcje generowania raportów
    /// </summary>
    public class ReportOptions
    {
        /// <summary>
        /// Format wyjściowy raportu (domyślnie PDF)
        /// </summary>
        public ReportFormat Format { get; set; } = ReportFormat.PDF;

        /// <summary>
        /// Czy dołączyć szczegółowe dane (domyślnie false)
        /// </summary>
        public bool IncludeDetailedData { get; set; } = false;

        /// <summary>
        /// Czy dołączyć wykresy i wizualizacje
        /// </summary>
        public bool IncludeCharts { get; set; } = true;

        /// <summary>
        /// Czy wysyłać powiadomienia po wygenerowaniu
        /// </summary>
        public bool SendNotifications { get; set; } = true;

        /// <summary>
        /// Lista typów szkół do uwzględnienia (null = wszystkie)
        /// </summary>
        public string[]? SchoolTypeIds { get; set; }

        /// <summary>
        /// Lista działów do uwzględnienia (null = wszystkie)
        /// </summary>
        public string[]? DepartmentIds { get; set; }

        /// <summary>
        /// Język raportu (domyślnie polski)
        /// </summary>
        public string Language { get; set; } = "pl-PL";

        /// <summary>
        /// Dodatkowe tagi dla historii operacji
        /// </summary>
        public string? AdditionalTags { get; set; }

        /// <summary>
        /// Maksymalny czas generowania w minutach (domyślnie 30)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Opcje eksportu danych systemowych
    /// </summary>
    public class ExportOptions
    {
        /// <summary>
        /// Typ danych do eksportu
        /// </summary>
        public ExportDataType DataType { get; set; } = ExportDataType.All;

        /// <summary>
        /// Format pliku eksportu
        /// </summary>
        public ExportFileFormat Format { get; set; } = ExportFileFormat.Excel;

        /// <summary>
        /// Czy dołączyć dane historyczne
        /// </summary>
        public bool IncludeHistoricalData { get; set; } = true;

        /// <summary>
        /// Zakres dat dla danych historycznych
        /// </summary>
        public DateRange? DateRange { get; set; }

        /// <summary>
        /// Czy kompresować wynikowy plik
        /// </summary>
        public bool CompressOutput { get; set; } = false;

        /// <summary>
        /// Czy wykluczyć dane osobowe (GDPR compliance)
        /// </summary>
        public bool ExcludePersonalData { get; set; } = false;

        /// <summary>
        /// Maksymalny rozmiar wynikowego pliku w MB
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 50;

        /// <summary>
        /// Dodatkowe tagi dla historii operacji
        /// </summary>
        public string? AdditionalTags { get; set; }
    }

    /// <summary>
    /// Zakres dat
    /// </summary>
    public class DateRange
    {
        /// <summary>
        /// Data początkowa
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Data końcowa
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Czy zakres jest prawidłowy
        /// </summary>
        public bool IsValid => StartDate <= EndDate;
    }

    /// <summary>
    /// Format raportu
    /// </summary>
    public enum ReportFormat
    {
        PDF,
        Excel,
        CSV,
        JSON,
        HTML
    }

    /// <summary>
    /// Typ raportu compliance
    /// </summary>
    public enum ComplianceReportType
    {
        DataProtection,    // GDPR/RODO
        UserAccess,        // Kontrola dostępu
        SystemAudit,       // Audit systemu
        ActivityLogs,      // Logi aktywności
        SecurityOverview   // Przegląd bezpieczeństwa
    }

    /// <summary>
    /// Typ danych do eksportu
    /// </summary>
    public enum ExportDataType
    {
        All,               // Wszystkie dane
        Users,             // Tylko użytkownicy
        Teams,             // Tylko zespoły
        OperationHistory,  // Historia operacji
        Configuration,     // Konfiguracja systemu
        Reports            // Wygenerowane raporty
    }

    /// <summary>
    /// Format pliku eksportu
    /// </summary>
    public enum ExportFileFormat
    {
        Excel,
        CSV,
        JSON,
        XML,
        ZIP
    }

    /// <summary>
    /// Wynik operacji raportowania
    /// </summary>
    public class ReportOperationResult
    {
        /// <summary>
        /// Czy operacja się powiodła
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Identyfikator wygenerowanego raportu
        /// </summary>
        public string? ReportId { get; set; }

        /// <summary>
        /// Nazwa pliku raportu
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Ścieżka do pliku raportu
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Strumień z danymi raportu (dla bezpośredniego pobierania)
        /// </summary>
        public Stream? ReportStream { get; set; }

        /// <summary>
        /// Rozmiar raportu w bajtach
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Typ MIME pliku
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Komunikat błędu w przypadku niepowodzenia
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Data wygenerowania raportu
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Czas generowania raportu
        /// </summary>
        public TimeSpan? GenerationTime { get; set; }

        /// <summary>
        /// Dodatkowe metadane raportu
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Lista błędów które wystąpiły podczas generowania
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Lista ostrzeżeń które wystąpiły podczas generowania
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Tworzy wynik sukcesu
        /// </summary>
        public static ReportOperationResult CreateSuccess(string reportId, string fileName, Stream? reportStream = null)
        {
            return new ReportOperationResult
            {
                Success = true,
                ReportId = reportId,
                FileName = fileName,
                ReportStream = reportStream,
                GeneratedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Tworzy wynik błędu
        /// </summary>
        public static ReportOperationResult CreateError(string errorMessage)
        {
            return new ReportOperationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Status procesu raportowania
    /// </summary>
    public class ReportingProcessStatus
    {
        /// <summary>
        /// Identyfikator procesu
        /// </summary>
        public string ProcessId { get; set; } = string.Empty;

        /// <summary>
        /// Typ procesu raportowania
        /// </summary>
        public string ProcessType { get; set; } = string.Empty;

        /// <summary>
        /// Typ raportu
        /// </summary>
        public string ReportType { get; set; } = string.Empty;

        /// <summary>
        /// Data rozpoczęcia procesu
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Data zakończenia procesu
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Status procesu
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Bieżąca operacja
        /// </summary>
        public string? CurrentOperation { get; set; }

        /// <summary>
        /// Procent ukończenia
        /// </summary>
        public double ProgressPercentage { get; set; }

        /// <summary>
        /// Komunikat błędu
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Czy proces może być anulowany
        /// </summary>
        public bool CanBeCancelled { get; set; } = true;

        /// <summary>
        /// Szacowany czas pozostały do ukończenia
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Nazwa pliku wynikowego
        /// </summary>
        public string? OutputFileName { get; set; }

        /// <summary>
        /// Użytkownik który uruchomił proces
        /// </summary>
        public string StartedBy { get; set; } = string.Empty;
    }
} 