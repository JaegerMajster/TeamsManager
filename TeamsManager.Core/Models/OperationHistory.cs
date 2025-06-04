using System;
using System.Text.Json;
using TeamsManager.Core.Enums;
using System.Text.Encodings.Web;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Historia operacji wykonywanych w systemie
    /// Służy do audytu, monitorowania i diagnostyki działań użytkowników
    /// </summary>
    public class OperationHistory : BaseEntity
    {
        /// <summary>
        /// Typ wykonywanej operacji
        /// </summary>
        public OperationType Type { get; set; }

        /// <summary>
        /// Nazwa typu encji na której wykonywana jest operacja
        /// Np. "Team", "User", "Department", "SchoolType"
        /// </summary>
        public string TargetEntityType { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator konkretnej encji na której wykonywana jest operacja
        /// </summary>
        public string TargetEntityId { get; set; } = string.Empty;

        /// <summary>
        /// Nazwa/opis encji docelowej dla czytelności
        /// Np. nazwa zespołu, imię i nazwisko użytkownika
        /// </summary>
        public string TargetEntityName { get; set; } = string.Empty;

        /// <summary>
        /// Szczegółowe informacje o operacji w formacie JSON
        /// Zawiera parametry wejściowe, zmiany, dodatkowe dane
        /// </summary>
        public string OperationDetails { get; set; } = string.Empty;

        /// <summary>
        /// Aktualny status operacji
        /// </summary>
        public OperationStatus Status { get; set; } = OperationStatus.Pending;

        /// <summary>
        /// Komunikat błędu jeśli operacja się nie powiodła
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Szczegółowy stos błędów dla diagnostyki
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// Data i czas rozpoczęcia operacji
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Data i czas zakończenia operacji
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Czas trwania operacji
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Adres IP użytkownika wykonującego operację
        /// </summary>
        public string? UserIpAddress { get; set; }

        /// <summary>
        /// Informacje o aplikacji klienckiej (User Agent)
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Identyfikator sesji użytkownika
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Identyfikator operacji nadrzędnej (dla operacji wsadowych)
        /// </summary>
        public string? ParentOperationId { get; set; }

        /// <summary>
        /// Kolejność operacji w ramach operacji wsadowej
        /// </summary>
        public int? SequenceNumber { get; set; }

        /// <summary>
        /// Liczba elementów do przetworzenia (dla operacji wsadowych)
        /// </summary>
        public int? TotalItems { get; set; }

        /// <summary>
        /// Liczba pomyślnie przetworzonych elementów
        /// </summary>
        public int? ProcessedItems { get; set; }

        /// <summary>
        /// Liczba elementów które nie zostały przetworzone z powodu błędów
        /// </summary>
        public int? FailedItems { get; set; }

        /// <summary>
        /// Dodatkowe znaczniki do kategoryzacji operacji
        /// Np. "Import", "Export", "Bulk", "Manual", "Scheduled"
        /// </summary>
        public string? Tags { get; set; }

        // ===== WŁAŚCIWOŚCI OBLICZANE =====

        /// <summary>
        /// Czy operacja jest w trakcie wykonywania
        /// </summary>
        public bool IsInProgress => Status == OperationStatus.InProgress;

        /// <summary>
        /// Czy operacja została zakończona (niezależnie od rezultatu)
        /// </summary>
        public bool IsCompleted => Status == OperationStatus.Completed ||
                                   Status == OperationStatus.Failed ||
                                   Status == OperationStatus.Cancelled ||
                                   Status == OperationStatus.PartialSuccess;

        /// <summary>
        /// Czy operacja zakończyła się sukcesem
        /// </summary>
        public bool IsSuccessful => Status == OperationStatus.Completed ||
                                    Status == OperationStatus.PartialSuccess;

        /// <summary>
        /// Procent ukończenia operacji (dla operacji wsadowych)
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (!TotalItems.HasValue || TotalItems.Value == 0) return 0;
                if (!ProcessedItems.HasValue) return 0;
                return Math.Round((double)ProcessedItems.Value / TotalItems.Value * 100, 1);
            }
        }

        /// <summary>
        /// Czas wykonywania operacji w sekundach
        /// </summary>
        public double DurationInSeconds => Duration?.TotalSeconds ?? 0;

        /// <summary>
        /// Czytelny opis statusu operacji
        /// </summary>
        public string StatusDescription => Status switch
        {
            OperationStatus.Pending => "Oczekująca",
            OperationStatus.InProgress => "W trakcie",
            OperationStatus.Completed => "Zakończona sukcesem",
            OperationStatus.Failed => "Nieudana",
            OperationStatus.Cancelled => "Anulowana",
            OperationStatus.PartialSuccess => "Częściowy sukces",
            _ => "Nieznany"
        };

        /// <summary>
        /// Krótki opis operacji do wyświetlenia w interfejsie
        /// </summary>
        public string ShortDescription => $"{GetOperationTypeDescription()} - {TargetEntityName}";

        // ===== METODY POMOCNICZE =====

        /// <summary>
        /// Oznacza operację jako rozpoczętą
        /// </summary>
        public void MarkAsStarted()
        {
            Status = OperationStatus.InProgress;
            StartedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Oznacza operację jako zakończoną sukcesem
        /// </summary>
        public void MarkAsCompleted(string? details = null)
        {
            if (Status == OperationStatus.InProgress || Status == OperationStatus.Pending)
            {
                Status = OperationStatus.Completed;
                CompletedAt = DateTime.UtcNow;
                if (StartedAt != default(DateTime) && CompletedAt.HasValue)
                {
                    Duration = CompletedAt.Value - StartedAt;
                }
                if (!string.IsNullOrWhiteSpace(details))
                {
                    OperationDetails = details; // Użycie przekazanego argumentu
                }
            }
        }

        /// <summary>
        /// Oznacza operację jako nieudaną
        /// </summary>
        /// <param name="errorMessage">Komunikat błędu</param>
        /// <param name="stackTrace">Stos wywołań błędu</param>
        public void MarkAsFailed(string errorMessage, string? stackTrace = null)
        {
            Status = OperationStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            Duration = CompletedAt - StartedAt;
            ErrorMessage = errorMessage;
            ErrorStackTrace = stackTrace;
        }

        /// <summary>
        /// Oznacza operację jako anulowaną
        /// </summary>
        public void MarkAsCancelled()
        {
            Status = OperationStatus.Cancelled;
            CompletedAt = DateTime.UtcNow;
            Duration = CompletedAt - StartedAt;
        }

        /// <summary>
        /// Aktualizuje postęp operacji wsadowej
        /// </summary>
        /// <param name="processedCount">Liczba przetworzonych elementów</param>
        /// <param name="failedCount">Liczba elementów z błędami</param>
        public void UpdateProgress(int processedCount, int failedCount = 0)
        {
            ProcessedItems = processedCount;
            FailedItems = failedCount;

            // Automatycznie ustaw odpowiedni status tylko jeśli jest coś do przetworzenia
            if (TotalItems.HasValue && TotalItems.Value > 0 && processedCount >= TotalItems.Value) // ZMIANA: TotalItems.Value > 0
            {
                if (failedCount > 0)
                    Status = OperationStatus.PartialSuccess;
                else
                    MarkAsCompleted();
            }
            // Jeśli TotalItems jest 0 lub null, status nie jest automatycznie zmieniany przez UpdateProgress
            // (pozostaje InProgress, jeśli tak był ustawiony przez MarkAsStarted)
            // Chyba że chcemy inną logikę dla TotalItems = 0, np. od razu Complete.
        }

        /// <summary>
        /// Dodaje szczegóły operacji w formacie JSON
        /// </summary>
        /// <param name="details">Obiekt z detalami operacji</param>
        public void SetOperationDetails<T>(T details) where T : class
        {
            try
            {
                OperationDetails = JsonSerializer.Serialize(details, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // NOWA OPCJA
                });
            }
            catch (Exception ex)
            {
                OperationDetails = $"Błąd serializacji: {ex.Message}";
            }
        }

        /// <summary>
        /// Pobiera szczegóły operacji jako obiekt
        /// </summary>
        /// <typeparam name="T">Typ obiektu szczegółów</typeparam>
        /// <returns>Obiekt szczegółów lub null w przypadku błędu</returns>
        public T? GetOperationDetails<T>() where T : class
        {
            if (string.IsNullOrWhiteSpace(OperationDetails)) return null;

            try
            {
                return JsonSerializer.Deserialize<T>(OperationDetails);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Zwraca czytelny opis typu operacji
        /// </summary>
        private string GetOperationTypeDescription() => Type switch
        {
            OperationType.TeamCreated => "Utworzenie zespołu",
            OperationType.TeamUpdated => "Aktualizacja zespołu",
            OperationType.TeamArchived => "Archiwizacja zespołu",
            OperationType.TeamUnarchived => "Przywrócenie zespołu",
            OperationType.TeamDeleted => "Usunięcie zespołu",
            OperationType.MemberAdded => "Dodanie członka",
            OperationType.MemberRemoved => "Usunięcie członka",
            OperationType.MemberRoleChanged => "Zmiana roli członka",
            OperationType.TeamMembersAdded => "Masowe dodawanie członków",
            OperationType.TeamMembersRemoved => "Masowe usuwanie członków",
            OperationType.ChannelCreated => "Utworzenie kanału",
            OperationType.ChannelUpdated => "Aktualizacja kanału",
            OperationType.ChannelDeleted => "Usunięcie kanału",
            OperationType.UserCreated => "Utworzenie użytkownika",
            OperationType.UserUpdated => "Aktualizacja użytkownika",
            OperationType.UserImported => "Import użytkownika",
            OperationType.UserDeactivated => "Dezaktywacja użytkownika",
            OperationType.BulkTeamCreation => "Masowe tworzenie zespołów",
            OperationType.BulkUserImport => "Masowy import użytkowników",
            OperationType.BulkArchiving => "Masowa archiwizacja",
            OperationType.SystemBackup => "Kopia zapasowa systemu",
            OperationType.SystemRestore => "Przywracanie systemu",
            OperationType.ConfigurationChanged => "Zmiana konfiguracji",
            _ => "Nieznana operacja"
        };
    }
}