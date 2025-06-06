using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TeamsManager.Core.Models;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs orkiestratora importu danych CSV/Excel
    /// Realizuje główne scenariusze biznesowe automatyzacji procesu importu danych
    /// </summary>
    public interface IDataImportOrchestrator
    {
        /// <summary>
        /// Importuje użytkowników z pliku CSV
        /// </summary>
        /// <param name="csvData">Strumień danych CSV</param>
        /// <param name="options">Opcjonalne ustawienia importu</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> ImportUsersFromCsvAsync(
            Stream csvData, 
            ImportOptions options,
            string apiAccessToken);

        /// <summary>
        /// Importuje zespoły z pliku Excel
        /// </summary>
        /// <param name="excelData">Strumień danych Excel</param>
        /// <param name="options">Opcjonalne ustawienia importu</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> ImportTeamsFromExcelAsync(
            Stream excelData, 
            ImportOptions options,
            string apiAccessToken);

        /// <summary>
        /// Importuje strukturę szkoły z pliku
        /// </summary>
        /// <param name="data">Strumień danych</param>
        /// <param name="options">Opcjonalne ustawienia importu</param>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Wynik operacji masowej z detalami</returns>
        Task<BulkOperationResult> ImportSchoolStructureAsync(
            Stream data, 
            ImportOptions options,
            string apiAccessToken);

        /// <summary>
        /// Waliduje dane importu przed rzeczywistym importem
        /// </summary>
        /// <param name="data">Strumień danych do walidacji</param>
        /// <param name="type">Typ danych do importu</param>
        /// <param name="options">Opcjonalne ustawienia walidacji</param>
        /// <returns>Wynik walidacji z szczegółami błędów</returns>
        Task<ImportValidationResult> ValidateImportDataAsync(
            Stream data, 
            ImportDataType type,
            ImportOptions? options = null);

        /// <summary>
        /// Pobiera status aktualnie wykonywanych procesów importu
        /// </summary>
        /// <returns>Status aktywnych procesów importu</returns>
        Task<IEnumerable<ImportProcessStatus>> GetActiveImportProcessesStatusAsync();

        /// <summary>
        /// Anuluje aktywny proces importu
        /// </summary>
        /// <param name="processId">ID procesu do anulowania</param>
        /// <returns>True jeśli anulowanie powiodło się</returns>
        Task<bool> CancelImportProcessAsync(string processId);

        /// <summary>
        /// Generuje szablon importu
        /// </summary>
        /// <param name="type">Typ danych dla szablonu</param>
        /// <param name="format">Format pliku (CSV/Excel)</param>
        /// <returns>Strumień z szablonem do pobrania</returns>
        Task<Stream> GenerateImportTemplateAsync(ImportDataType type, ImportFileFormat format);
    }

    /// <summary>
    /// Opcje konfiguracji procesu importu
    /// </summary>
    public class ImportOptions
    {
        /// <summary>
        /// Rozmiar batcha dla operacji masowych (domyślnie 50)
        /// </summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>
        /// Maksymalny czas oczekiwania na operację (w minutach, domyślnie 30)
        /// </summary>
        public int TimeoutMinutes { get; set; } = 30;

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
        /// Czy kontynuować przy błędach (true) czy zatrzymać cały proces (false)
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Procent akceptowalnych błędów (0-100, domyślnie 10%)
        /// </summary>
        public double AcceptableErrorPercentage { get; set; } = 10.0;

        /// <summary>
        /// Maksymalna liczba równoległych operacji
        /// </summary>
        public int MaxConcurrency { get; set; } = 3;

        /// <summary>
        /// Czy aktualizować istniejące rekordy (true) czy tylko dodawać nowe (false)
        /// </summary>
        public bool UpdateExisting { get; set; } = true;

        /// <summary>
        /// Separator kolumn dla plików CSV (domyślnie ';')
        /// </summary>
        public char CsvDelimiter { get; set; } = ';';

        /// <summary>
        /// Kodowanie pliku (domyślnie UTF-8)
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";

        /// <summary>
        /// Czy pierwsza linia zawiera nagłówki
        /// </summary>
        public bool HasHeaders { get; set; } = true;

        /// <summary>
        /// Maksymalny rozmiar pliku w MB (domyślnie 10MB)
        /// </summary>
        public int MaxFileSizeMB { get; set; } = 10;

        /// <summary>
        /// Dodatkowe tagi dla operacji w historii
        /// </summary>
        public string? AdditionalTags { get; set; }

        /// <summary>
        /// Mapowanie kolumn (klucz = nazwa kolumny w pliku, wartość = nazwa właściwości modelu)
        /// </summary>
        public Dictionary<string, string>? ColumnMapping { get; set; }
    }

    /// <summary>
    /// Typ danych do importu
    /// </summary>
    public enum ImportDataType
    {
        Users,
        Teams,
        SchoolStructure,
        Departments,
        Subjects,
        TeamTemplates
    }

    /// <summary>
    /// Format pliku importu
    /// </summary>
    public enum ImportFileFormat
    {
        CSV,
        Excel,
        Json
    }

    /// <summary>
    /// Wynik walidacji importu danych
    /// </summary>
    public class ImportValidationResult
    {
        /// <summary>
        /// Czy walidacja przeszła pomyślnie
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Lista błędów walidacji
        /// </summary>
        public List<ImportValidationError> Errors { get; set; } = new List<ImportValidationError>();

        /// <summary>
        /// Lista ostrzeżeń walidacji
        /// </summary>
        public List<ImportValidationWarning> Warnings { get; set; } = new List<ImportValidationWarning>();

        /// <summary>
        /// Liczba rekordów do importu
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// Liczba prawidłowych rekordów
        /// </summary>
        public int ValidRecords { get; set; }

        /// <summary>
        /// Dane podglądu (pierwsze 10 rekordów)
        /// </summary>
        public List<Dictionary<string, object>> PreviewData { get; set; } = new List<Dictionary<string, object>>();

        /// <summary>
        /// Wykryte kolumny w pliku
        /// </summary>
        public List<string> DetectedColumns { get; set; } = new List<string>();

        /// <summary>
        /// Sugerowane mapowanie kolumn
        /// </summary>
        public Dictionary<string, string> SuggestedMapping { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Błąd walidacji importu
    /// </summary>
    public class ImportValidationError
    {
        /// <summary>
        /// Numer wiersza z błędem (1-based)
        /// </summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// Nazwa kolumny z błędem
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Wartość która spowodowała błąd
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Opis błędu
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Typ błędu
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Czy błąd jest krytyczny (blokuje import)
        /// </summary>
        public bool IsCritical { get; set; }
    }

    /// <summary>
    /// Ostrzeżenie walidacji importu
    /// </summary>
    public class ImportValidationWarning
    {
        /// <summary>
        /// Numer wiersza z ostrzeżeniem (1-based)
        /// </summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// Nazwa kolumny z ostrzeżeniem
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Wartość która spowodowała ostrzeżenie
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Opis ostrzeżenia
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Typ ostrzeżenia
        /// </summary>
        public string WarningType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status procesu importu danych
    /// </summary>
    public class ImportProcessStatus
    {
        public string ProcessId { get; set; } = string.Empty;
        public ImportDataType DataType { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int SuccessfulRecords { get; set; }
        public int FailedRecords { get; set; }
        public double ProgressPercentage => TotalRecords > 0 ? (double)ProcessedRecords / TotalRecords * 100 : 0;
        public string? CurrentOperation { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanBeCancelled { get; set; } = true;
        public ImportFileFormat FileFormat { get; set; }
        public long FileSizeBytes { get; set; }
        public string StartedBy { get; set; } = string.Empty;
    }
} 