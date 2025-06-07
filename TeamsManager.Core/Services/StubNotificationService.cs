// Plik: TeamsManager.Core/Services/StubNotificationService.cs
using Microsoft.Extensions.Logging; // Dla ILogger
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services; // Dla INotificationService

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Stub (zaślepka) implementacji serwisu powiadomień.
    /// Loguje wywołania, ale nie wykonuje faktycznej logiki wysyłania powiadomień (np. przez SignalR).
    /// 
    /// Wzorzec: Stub Pattern - używany w testach i podczas developmentu
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
                                   "UserUPN='{UserUpn}', OperationID='{OperationId}', Progress={ProgressPercentage}%, Message='{Message}'",
                                   userUpn, operationId, progressPercentage, message);
            return Task.CompletedTask;
        }

        public Task SendNotificationToUserAsync(string userUpn, string message, string type)
        {
            _logger.LogInformation("[STUB INotificationService] SendNotificationToUserAsync: " +
                                   "UserUPN='{UserUpn}', Type='{Type}', Message='{Message}'",
                                   userUpn, type, message);
            return Task.CompletedTask;
        }

        // ===== NOWE METODY STUB DLA ORKIESTRATORÓW =====

        public Task SendProcessStartedNotificationAsync(string userUpn, string processId, string processType, string processName)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessStartedNotificationAsync: " +
                                   "UserUPN='{UserUpn}', ProcessId='{ProcessId}', ProcessType='{ProcessType}', ProcessName='{ProcessName}'",
                                   userUpn, processId, processType, processName);
            return Task.CompletedTask;
        }

        public Task SendProcessCompletedNotificationAsync(string userUpn, string processId, string processType, string processName, bool success, long executionTimeMs, string summary)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessCompletedNotificationAsync: " +
                                   "UserUPN='{UserUpn}', ProcessId='{ProcessId}', ProcessType='{ProcessType}', ProcessName='{ProcessName}', " +
                                   "Success={Success}, ExecutionTime={ExecutionTimeMs}ms, Summary='{Summary}'",
                                   userUpn, processId, processType, processName, success, executionTimeMs, summary);
            return Task.CompletedTask;
        }

        public Task SendProcessCancelledNotificationAsync(string userUpn, string processId, string processType, string processName, string reason)
        {
            _logger.LogInformation("[STUB INotificationService] SendProcessCancelledNotificationAsync: " +
                                   "UserUPN='{UserUpn}', ProcessId='{ProcessId}', ProcessType='{ProcessType}', ProcessName='{ProcessName}', Reason='{Reason}'",
                                   userUpn, processId, processType, processName, reason);
            return Task.CompletedTask;
        }

        public Task SendBroadcastNotificationAsync(string message, string type, string? excludeUserUpn = null)
        {
            _logger.LogInformation("[STUB INotificationService] SendBroadcastNotificationAsync: " +
                                   "Message='{Message}', Type='{Type}', ExcludeUserUpn='{ExcludeUserUpn}'",
                                   message, type, excludeUserUpn ?? "null");
            return Task.CompletedTask;
        }

        public Task SendCriticalErrorToAdminsAsync(string errorMessage, string contextInfo, string sourceComponent)
        {
            _logger.LogInformation("[STUB INotificationService] SendCriticalErrorToAdminsAsync: " +
                                   "ErrorMessage='{ErrorMessage}', ContextInfo='{ContextInfo}', SourceComponent='{SourceComponent}'",
                                   errorMessage, contextInfo, sourceComponent);
            return Task.CompletedTask;
        }

        public Task SendBulkOperationSummaryAsync(string userUpn, string operationId, string operationType, int totalItems, int processedItems, int successCount, int errorCount)
        {
            _logger.LogInformation("[STUB INotificationService] SendBulkOperationSummaryAsync: " +
                                   "UserUPN='{UserUpn}', OperationId='{OperationId}', OperationType='{OperationType}', " +
                                   "TotalItems={TotalItems}, ProcessedItems={ProcessedItems}, SuccessCount={SuccessCount}, ErrorCount={ErrorCount}",
                                   userUpn, operationId, operationType, totalItems, processedItems, successCount, errorCount);
            return Task.CompletedTask;
        }
    }
}