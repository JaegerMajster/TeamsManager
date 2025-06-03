// Plik: TeamsManager.Core/Services/StubNotificationService.cs
using Microsoft.Extensions.Logging; // Dla ILogger
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services; // Dla INotificationService

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Stub (zaślepka) implementacji serwisu powiadomień.
    /// Loguje wywołania, ale nie wykonuje faktycznej logiki wysyłania powiadomień (np. przez SignalR).
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
    }
}