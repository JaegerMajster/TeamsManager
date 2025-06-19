using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Stub implementacji serwisu powiadomień.
    /// Loguje wywołania, ale nie wykonuje faktycznej logiki wysyłania powiadomień.
    /// </summary>
    public class StubNotificationService : INotificationService
    {
        private readonly ILogger<StubNotificationService> _logger;

        public StubNotificationService(ILogger<StubNotificationService> logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public Task SendOperationProgressToUserAsync(string userUpn, string operationId, int progressPercentage, string message)
        {
            _logger.LogInformation("[STUB INotificationService] SendOperationProgressToUserAsync: " +
                "UPN użytkownika='{UserUpn}', ID operacji='{OperationId}', Postęp={ProgressPercentage}%, Wiadomość='{Message}'",
                userUpn, operationId, progressPercentage, message);
            return Task.CompletedTask;
        }

        public Task SendNotificationToUserAsync(string userUpn, string message, string type)
        {
            _logger.LogInformation("[STUB INotificationService] SendNotificationToUserAsync: " +
                "UPN użytkownika='{UserUpn}', Typ='{Type}', Wiadomość='{Message}'",
                userUpn, type, message);
            return Task.CompletedTask;
        }

        public Task SendProcessStartedNotificationAsync(string userUpn, string processId, string processType, string processName)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessStartedNotificationAsync: " +
                "UPN użytkownika='{UserUpn}', ID procesu='{ProcessId}', Typ procesu='{ProcessType}', Nazwa procesu='{ProcessName}'",
                userUpn, processId, processType, processName);
            return Task.CompletedTask;
        }

        public Task SendProcessCompletedNotificationAsync(string userUpn, string processId, string processType, string processName, bool success, long executionTimeMs, string summary)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessCompletedNotificationAsync: " +
                "UPN użytkownika='{UserUpn}', ID procesu='{ProcessId}', Typ procesu='{ProcessType}', Nazwa procesu='{ProcessName}', " +
                "Sukces={Success}, Czas wykonania={ExecutionTimeMs}ms, Podsumowanie='{Summary}'",
                userUpn, processId, processType, processName, success, executionTimeMs, summary);
            return Task.CompletedTask;
        }

        public Task SendProcessCancelledNotificationAsync(string userUpn, string processId, string processType, string processName, string reason)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessCancelledNotificationAsync: " +
                "UPN użytkownika='{UserUpn}', ID procesu='{ProcessId}', Typ procesu='{ProcessType}', Nazwa procesu='{ProcessName}', Powód='{Reason}'",
                userUpn, processId, processType, processName, reason);
            return Task.CompletedTask;
        }

        public Task SendBroadcastNotificationAsync(string message, string type, string? excludeUserUpn = null)
        {
            _logger.LogInformation("[STUB INotificationService] SendBroadcastNotificationAsync: " +
                "Wiadomość='{Message}', Typ='{Type}', Wyklucz UPN użytkownika='{ExcludeUserUpn}'",
                message, type, excludeUserUpn ?? "null");
            return Task.CompletedTask;
        }

        public Task SendCriticalErrorToAdminsAsync(string errorMessage, string contextInfo, string sourceComponent)
        {
            _logger.LogInformation("[STUB INotificationService] SendCriticalErrorToAdminsAsync: " +
                "Komunikat błędu='{ErrorMessage}', Informacje kontekstowe='{ContextInfo}', Komponent źródłowy='{SourceComponent}'",
                errorMessage, contextInfo, sourceComponent);
            return Task.CompletedTask;
        }

        public Task SendBulkOperationSummaryAsync(string userUpn, string operationId, string operationType, int totalItems, int processedItems, int successCount, int errorCount)
        {
            _logger.LogInformation("[STUB INotificationService] SendBulkOperationSummaryAsync: " +
                "UPN użytkownika='{UserUpn}', ID operacji='{OperationId}', Typ operacji='{OperationType}', " +
                "Łączna liczba elementów={TotalItems}, Przetworzone elementy={ProcessedItems}, Liczba sukcesów={SuccessCount}, Liczba błędów={ErrorCount}",
                userUpn, operationId, operationType, totalItems, processedItems, successCount, errorCount);
            return Task.CompletedTask;
        }
    }
}