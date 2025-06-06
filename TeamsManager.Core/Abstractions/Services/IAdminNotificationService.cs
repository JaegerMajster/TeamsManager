using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za wysyłanie powiadomień email do administratorów systemu.
    /// Używa Microsoft.Graph do wysyłki podsumowań ważnych operacji.
    /// </summary>
    public interface IAdminNotificationService
    {
        /// <summary>
        /// Wysyła powiadomienie o utworzeniu nowego zespołu.
        /// </summary>
        Task SendTeamCreatedNotificationAsync(
            string teamName, 
            string teamId, 
            string createdBy, 
            int membersCount,
            Dictionary<string, object>? additionalInfo = null);
        
        /// <summary>
        /// Wysyła powiadomienie o masowej operacji na zespołach.
        /// </summary>
        Task SendBulkTeamsOperationNotificationAsync(
            string operationType,
            int totalTeams,
            int successCount,
            int failureCount,
            string performedBy,
            Dictionary<string, object>? details = null);
        
        /// <summary>
        /// Wysyła powiadomienie o utworzeniu nowego użytkownika.
        /// </summary>
        Task SendUserCreatedNotificationAsync(
            string userName,
            string userUpn,
            string userRole,
            string createdBy);
        
        /// <summary>
        /// Wysyła powiadomienie o masowej operacji na użytkownikach.
        /// </summary>
        Task SendBulkUsersOperationNotificationAsync(
            string operationType,
            string teamName,
            int totalUsers,
            int successCount,
            int failureCount,
            string performedBy);
        
        /// <summary>
        /// Wysyła powiadomienie o krytycznym błędzie.
        /// </summary>
        Task SendCriticalErrorNotificationAsync(
            string operationType,
            string errorMessage,
            string stackTrace,
            string occurredDuring,
            string? userId = null);
        
        /// <summary>
        /// Wysyła niestandardowe powiadomienie administracyjne.
        /// </summary>
        Task SendCustomAdminNotificationAsync(
            string subject,
            string message,
            Dictionary<string, object>? data = null);
    }
} 