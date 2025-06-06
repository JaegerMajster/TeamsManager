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

        public Task SendTeamCreatedNotificationAsync(string teamName, string teamId, string createdBy, int membersCount, Dictionary<string, object> additionalInfo = null)
        {
            _logger.LogInformation("[STUB ADMIN NOTIFICATION] Team Created: {TeamName} (ID: {TeamId}) by {CreatedBy} with {MembersCount} members", 
                teamName, teamId, createdBy, membersCount);
            return Task.CompletedTask;
        }

        public Task SendBulkTeamsOperationNotificationAsync(string operationType, int totalTeams, int successCount, int failureCount, string performedBy, Dictionary<string, object> details = null)
        {
            _logger.LogInformation("[STUB ADMIN NOTIFICATION] Bulk Teams Operation: {OperationType} - {Success}/{Total} succeeded, performed by {PerformedBy}", 
                operationType, successCount, totalTeams, performedBy);
            return Task.CompletedTask;
        }

        public Task SendUserCreatedNotificationAsync(string userName, string userUpn, string userRole, string createdBy)
        {
            _logger.LogInformation("[STUB ADMIN NOTIFICATION] User Created: {UserName} ({UPN}) with role {Role} by {CreatedBy}", 
                userName, userUpn, userRole, createdBy);
            return Task.CompletedTask;
        }

        public Task SendBulkUsersOperationNotificationAsync(string operationType, string teamName, int totalUsers, int successCount, int failureCount, string performedBy)
        {
            _logger.LogInformation("[STUB ADMIN NOTIFICATION] Bulk Users Operation: {OperationType} in team {TeamName} - {Success}/{Total} succeeded, performed by {PerformedBy}", 
                operationType, teamName, successCount, totalUsers, performedBy);
            return Task.CompletedTask;
        }

        public Task SendCriticalErrorNotificationAsync(string operationType, string errorMessage, string stackTrace, string occurredDuring, string userId = null)
        {
            _logger.LogError("[STUB ADMIN NOTIFICATION] CRITICAL ERROR in {OperationType}: {ErrorMessage} occurred during {OccurredDuring} for user {UserId}", 
                operationType, errorMessage, occurredDuring, userId ?? "N/A");
            return Task.CompletedTask;
        }

        public Task SendCustomAdminNotificationAsync(string subject, string message, Dictionary<string, object> data = null)
        {
            _logger.LogInformation("[STUB ADMIN NOTIFICATION] Custom: {Subject} - {Message}", subject, message);
            return Task.CompletedTask;
        }
    }
} 