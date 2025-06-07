using Microsoft.AspNetCore.SignalR;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Api.Hubs;
using System.Threading.Tasks;
using System;

namespace TeamsManager.Api.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task SendOperationProgressToUserAsync(string userUpn, string operationId, int progressPercentage, string message)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("OperationProgress", new
            {
                OperationId = operationId,
                ProgressPercentage = progressPercentage,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendNotificationToUserAsync(string userUpn, string message, string type)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("ReceiveNotification", new
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendProcessStartedNotificationAsync(string userUpn, string processId, string processType, string processName)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("ProcessStarted", new
            {
                ProcessId = processId,
                ProcessType = processType,
                ProcessName = processName,
                StartTime = DateTime.UtcNow
            });
        }

        public async Task SendProcessCompletedNotificationAsync(string userUpn, string processId, string processType, string processName, bool success, long executionTimeMs, string summary)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("ProcessCompleted", new
            {
                ProcessId = processId,
                ProcessType = processType,
                ProcessName = processName,
                Success = success,
                ExecutionTimeMs = executionTimeMs,
                Summary = summary,
                CompletedTime = DateTime.UtcNow
            });
        }

        public async Task SendProcessCancelledNotificationAsync(string userUpn, string processId, string processType, string processName, string reason)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("ProcessCancelled", new
            {
                ProcessId = processId,
                ProcessType = processType,
                ProcessName = processName,
                Reason = reason,
                CancelledTime = DateTime.UtcNow
            });
        }

        public async Task SendBroadcastNotificationAsync(string message, string type, string? excludeUserUpn = null)
        {
            var clientProxy = excludeUserUpn != null 
                ? _hubContext.Clients.AllExcept(excludeUserUpn)
                : _hubContext.Clients.All;

            await clientProxy.SendAsync("ReceiveBroadcast", new
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendCriticalErrorToAdminsAsync(string errorMessage, string contextInfo, string sourceComponent)
        {
            await _hubContext.Clients.Group("Administrators").SendAsync("CriticalError", new
            {
                ErrorMessage = errorMessage,
                ContextInfo = contextInfo,
                SourceComponent = sourceComponent,
                Timestamp = DateTime.UtcNow
            });
        }

        public async Task SendBulkOperationSummaryAsync(string userUpn, string operationId, string operationType, int totalItems, int processedItems, int successCount, int errorCount)
        {
            await _hubContext.Clients.User(userUpn).SendAsync("BulkOperationSummary", new
            {
                OperationId = operationId,
                OperationType = operationType,
                TotalItems = totalItems,
                ProcessedItems = processedItems,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
