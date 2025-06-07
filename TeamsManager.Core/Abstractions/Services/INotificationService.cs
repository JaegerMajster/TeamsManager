// Plik: TeamsManager.Core/Abstractions/Services/INotificationService.cs
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za wysyłanie powiadomień,
    /// np. do klienta UI poprzez SignalR lub innymi kanałami.
    /// 
    /// Wzorzec: Service Interface Pattern - centralizuje wszystkie typy powiadomień
    /// Wykorzystuje: Dependency Injection Pattern
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Wysyła powiadomienie o postępie operacji do określonego użytkownika.
        /// Wzorzec: Progress Notification Pattern
        /// </summary>
        /// <param name="userUpn">UPN użytkownika, do którego ma trafić powiadomienie.</param>
        /// <param name="operationId">Unikalny identyfikator operacji.</param>
        /// <param name="progressPercentage">Postęp operacji w procentach (0-100).</param>
        /// <param name="message">Wiadomość opisująca aktualny stan/krok operacji.</param>
        Task SendOperationProgressToUserAsync(string userUpn, string operationId, int progressPercentage, string message);

        /// <summary>
        /// Wysyła ogólne powiadomienie do określonego użytkownika.
        /// Wzorzec: Standard Notification Pattern
        /// </summary>
        /// <param name="userUpn">UPN użytkownika, do którego ma trafić powiadomienie.</param>
        /// <param name="message">Treść powiadomienia.</param>
        /// <param name="type">Typ powiadomienia (np. "info", "warning", "error") używany do stylizacji po stronie klienta.</param>
        Task SendNotificationToUserAsync(string userUpn, string message, string type);

        // ===== NOWE METODY DLA ORKIESTRATORÓW =====

        /// <summary>
        /// Wysyła powiadomienie o rozpoczęciu procesu orkiestratora.
        /// Wzorzec: Process Lifecycle Notification Pattern
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="processId">ID procesu</param>
        /// <param name="processType">Typ procesu (SchoolYear, BulkUser, TeamLifecycle, etc.)</param>
        /// <param name="processName">Nazwa procesu czytelna dla użytkownika</param>
        Task SendProcessStartedNotificationAsync(string userUpn, string processId, string processType, string processName);

        /// <summary>
        /// Wysyła powiadomienie o zakończeniu procesu orkiestratora.
        /// Wzorzec: Process Lifecycle Notification Pattern  
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="processId">ID procesu</param>
        /// <param name="processType">Typ procesu</param>
        /// <param name="processName">Nazwa procesu</param>
        /// <param name="success">Czy proces zakończył się sukcesem</param>
        /// <param name="executionTimeMs">Czas wykonania w milisekundach</param>
        /// <param name="summary">Podsumowanie wyników</param>
        Task SendProcessCompletedNotificationAsync(string userUpn, string processId, string processType, string processName, bool success, long executionTimeMs, string summary);

        /// <summary>
        /// Wysyła powiadomienie o anulowaniu procesu orkiestratora.
        /// Wzorzec: Process Lifecycle Notification Pattern
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="processId">ID procesu</param>
        /// <param name="processType">Typ procesu</param>
        /// <param name="processName">Nazwa procesu</param>
        /// <param name="reason">Powód anulowania</param>
        Task SendProcessCancelledNotificationAsync(string userUpn, string processId, string processType, string processName, string reason);

        /// <summary>
        /// Wysyła powiadomienie broadcast do wszystkich połączonych użytkowników.
        /// Wzorzec: Broadcast Notification Pattern
        /// </summary>
        /// <param name="message">Treść powiadomienia</param>
        /// <param name="type">Typ powiadomienia</param>
        /// <param name="excludeUserUpn">Opcjonalnie wyklucz konkretnego użytkownika</param>
        Task SendBroadcastNotificationAsync(string message, string type, string? excludeUserUpn = null);

        /// <summary>
        /// Wysyła powiadomienie o błędzie krytycznym do administratorów.
        /// Wzorzec: Critical Error Notification Pattern
        /// </summary>
        /// <param name="errorMessage">Opis błędu</param>
        /// <param name="contextInfo">Dodatkowe informacje kontekstowe</param>
        /// <param name="sourceComponent">Komponent źródłowy błędu</param>
        Task SendCriticalErrorToAdminsAsync(string errorMessage, string contextInfo, string sourceComponent);

        /// <summary>
        /// Wysyła powiadomienie o operacji masowej (bulk operation).
        /// Wzorzec: Bulk Operation Notification Pattern
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="operationId">ID operacji</param>
        /// <param name="operationType">Typ operacji masowej</param>
        /// <param name="totalItems">Łączna liczba elementów</param>
        /// <param name="processedItems">Liczba przetworzonych elementów</param>
        /// <param name="successCount">Liczba sukcesów</param>
        /// <param name="errorCount">Liczba błędów</param>
        Task SendBulkOperationSummaryAsync(string userUpn, string operationId, string operationType, int totalItems, int processedItems, int successCount, int errorCount);
    }
}