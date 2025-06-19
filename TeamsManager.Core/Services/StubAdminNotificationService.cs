using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Stub implementacja serwisu powiadomień administratorów dla środowisk testowych.
    /// </summary>
    public class StubAdminNotificationService : IAdminNotificationService
    {
        private readonly ILogger<StubAdminNotificationService> _logger;

        public StubAdminNotificationService(ILogger<StubAdminNotificationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SendTeamCreatedNotificationAsync(string teamName, string teamId, string createdBy, int membersCount, Dictionary<string, object>? additionalInfo = null)
        {
            _logger.LogInformation("[STUB POWIADOMIENIE ADMIN] Utworzono zespół: {TeamName} (ID: {TeamId}) przez {CreatedBy} z {MembersCount} członkami", 
                teamName, teamId, createdBy, membersCount);
            return Task.CompletedTask;
        }

        public Task SendBulkTeamsOperationNotificationAsync(string operationType, int totalTeams, int successCount, int failureCount, string performedBy, Dictionary<string, object>? details = null)
        {
            _logger.LogInformation("[STUB POWIADOMIENIE ADMIN] Operacja masowa zespołów: {OperationType} - {Success}/{Total} zakończone sukcesem, wykonane przez {PerformedBy}", 
                operationType, successCount, totalTeams, performedBy);
            return Task.CompletedTask;
        }

        public Task SendUserCreatedNotificationAsync(string userName, string userUpn, string userRole, string createdBy)
        {
            _logger.LogInformation("[STUB POWIADOMIENIE ADMIN] Utworzono użytkownika: {UserName} ({UPN}) z rolą {Role} przez {CreatedBy}", 
                userName, userUpn, userRole, createdBy);
            return Task.CompletedTask;
        }

        public Task SendBulkUsersOperationNotificationAsync(string operationType, string teamName, int totalUsers, int successCount, int failureCount, string performedBy)
        {
            _logger.LogInformation("[STUB POWIADOMIENIE ADMIN] Operacja masowa użytkowników: {OperationType} w zespole {TeamName} - {Success}/{Total} zakończone sukcesem, wykonane przez {PerformedBy}", 
                operationType, teamName, successCount, totalUsers, performedBy);
            return Task.CompletedTask;
        }

        public Task SendCriticalErrorNotificationAsync(string operationType, string errorMessage, string stackTrace, string occurredDuring, string? userId = null)
        {
            _logger.LogError("[STUB POWIADOMIENIE ADMIN] BŁĄD KRYTYCZNY w {OperationType}: {ErrorMessage} wystąpił podczas {OccurredDuring} dla użytkownika {UserId}", 
                operationType, errorMessage, occurredDuring, userId ?? "N/A");
            return Task.CompletedTask;
        }

        public Task SendCustomAdminNotificationAsync(string subject, string message, Dictionary<string, object>? data = null)
        {
            _logger.LogInformation("[STUB POWIADOMIENIE ADMIN] Niestandardowe: {Subject} - {Message}", subject, message);
            return Task.CompletedTask;
        }

        public Task SendGraphApiErrorMetricsAsync(Dictionary<string, object> metrics)
        {
            var method = metrics.GetValueOrDefault("Method", "Unknown");
            var endpoint = metrics.GetValueOrDefault("Endpoint", "Unknown");
            var httpStatusCode = metrics.GetValueOrDefault("HttpStatusCode", 0);
            var graphErrorCode = metrics.GetValueOrDefault("GraphErrorCode", "Unknown");
            
            _logger.LogWarning("[STUB POWIADOMIENIE ADMIN] Metryki błędów Graph API: Metoda={Method}, Endpoint={Endpoint}, Kod statusu={StatusCode}, Kod błędu={ErrorCode}", 
                method, endpoint, httpStatusCode, graphErrorCode);
            return Task.CompletedTask;
        }
    }
} 