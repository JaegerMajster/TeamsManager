using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Api.Hubs;
using System;
using System.Collections.Concurrent;

namespace TeamsManager.Api.Services
{
    /// <summary>
    /// [P2-REALTIME] Real-time notification service using SignalR
    /// Replaces StubNotificationService with actual hub functionality
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;
        
        // [P2-OPTIMIZATION] Track active connections for better performance
        private static readonly ConcurrentDictionary<string, DateTime> _activeConnections = new();
        private static readonly ConcurrentDictionary<string, int> _notificationsSent = new();

        public SignalRNotificationService(
            IHubContext<NotificationHub> hubContext,
            ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// [P2-REALTIME] Send operation progress notification via SignalR
        /// </summary>
        public async Task SendOperationProgressToUserAsync(string userUpn, string operationId, int progressPercentage, string message)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("[P2-SIGNALR] Cannot send progress notification - userUpn is empty");
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("[P2-SIGNALR] Sending progress notification: {UserUpn}, Operation: {OperationId}, Progress: {Progress}%", 
                    userUpn, operationId, progressPercentage);

                var notificationData = new
                {
                    Type = "OperationProgress",
                    UserUpn = userUpn,
                    OperationId = operationId,
                    ProgressPercentage = progressPercentage,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    Icon = GetProgressIcon(progressPercentage),
                    Color = GetProgressColor(progressPercentage)
                };

                // Send to specific user group
                await _hubContext.Clients.Group($"User_{userUpn}")
                    .SendAsync("ReceiveOperationProgress", notificationData);

                // [P2-OPTIMIZATION] Also send to admin group for monitoring
                await _hubContext.Clients.Group("Administrators")
                    .SendAsync("ReceiveOperationProgress", notificationData);

                stopwatch.Stop();

                // Track metrics
                RecordNotificationSent(userUpn);

                _logger.LogInformation("[P2-SIGNALR] Progress notification sent successfully. " +
                    "User: {UserUpn}, Operation: {OperationId}, Progress: {Progress}%, Duration: {ElapsedMs}ms",
                    userUpn, operationId, progressPercentage, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[P2-SIGNALR] Failed to send progress notification. " +
                    "User: {UserUpn}, Operation: {OperationId}, Duration: {ElapsedMs}ms",
                    userUpn, operationId, stopwatch.ElapsedMilliseconds);
                
                // Don't throw - notification failures shouldn't break business logic
            }
        }

        /// <summary>
        /// [P2-REALTIME] Send general notification via SignalR
        /// </summary>
        public async Task SendNotificationToUserAsync(string userUpn, string message, string type)
        {
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogWarning("[P2-SIGNALR] Cannot send notification - userUpn is empty");
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("[P2-SIGNALR] Sending notification: {UserUpn}, Type: {Type}", userUpn, type);

                var notificationData = new
                {
                    Type = "GeneralNotification",
                    UserUpn = userUpn,
                    Message = message,
                    NotificationType = type,
                    Timestamp = DateTime.UtcNow,
                    Icon = GetNotificationIcon(type),
                    Color = GetNotificationColor(type),
                    AutoHide = ShouldAutoHide(type),
                    Duration = GetNotificationDuration(type)
                };

                // Send to specific user
                await _hubContext.Clients.Group($"User_{userUpn}")
                    .SendAsync("ReceiveNotification", notificationData);

                // [P2-ENHANCEMENT] Send critical notifications to admin group
                if (IsCriticalNotification(type))
                {
                    await _hubContext.Clients.Group("Administrators")
                        .SendAsync("ReceiveAdminNotification", notificationData);
                }

                stopwatch.Stop();

                // Track metrics
                RecordNotificationSent(userUpn);

                _logger.LogInformation("[P2-SIGNALR] Notification sent successfully. " +
                    "User: {UserUpn}, Type: {Type}, Duration: {ElapsedMs}ms",
                    userUpn, type, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[P2-SIGNALR] Failed to send notification. " +
                    "User: {UserUpn}, Type: {Type}, Duration: {ElapsedMs}ms",
                    userUpn, type, stopwatch.ElapsedMilliseconds);
                
                // Don't throw - notification failures shouldn't break business logic
            }
        }

        #region P2 Real-time Enhancements

        /// <summary>
        /// [P2-REALTIME] Send broadcast notification to all connected users
        /// </summary>
        public async Task SendBroadcastNotificationAsync(string message, string type, string? excludeUserUpn = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogDebug("[P2-SIGNALR] Sending broadcast notification: Type: {Type}", type);

                var notificationData = new
                {
                    Type = "BroadcastNotification",
                    Message = message,
                    NotificationType = type,
                    Timestamp = DateTime.UtcNow,
                    Icon = GetNotificationIcon(type),
                    Color = GetNotificationColor(type),
                    IsBroadcast = true
                };

                var clientProxy = _hubContext.Clients.All;
                
                // [P2-OPTIMIZATION] Exclude specific user if needed
                if (!string.IsNullOrWhiteSpace(excludeUserUpn))
                {
                    clientProxy = _hubContext.Clients.GroupExcept("AllUsers", $"User_{excludeUserUpn}");
                }

                await clientProxy.SendAsync("ReceiveBroadcastNotification", notificationData);

                stopwatch.Stop();
                _logger.LogInformation("[P2-SIGNALR] Broadcast notification sent. Type: {Type}, Duration: {ElapsedMs}ms",
                    type, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[P2-SIGNALR] Failed to send broadcast notification. Type: {Type}, Duration: {ElapsedMs}ms",
                    type, stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// [P2-MONITORING] Get SignalR notification statistics
        /// </summary>
        public SignalRMetrics GetNotificationMetrics()
        {
            return new SignalRMetrics
            {
                ActiveConnections = _activeConnections.Count,
                TotalNotificationsSent = _notificationsSent.Values.Sum(),
                ConnectionsByUser = _activeConnections.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                NotificationsByUser = _notificationsSent.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                MeasuredAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Helper Methods

        private static string GetProgressIcon(int progress) => progress switch
        {
            < 25 => "⏳",
            < 50 => "🔄",
            < 75 => "⚡",
            < 100 => "🚀",
            _ => "✅"
        };

        private static string GetProgressColor(int progress) => progress switch
        {
            < 25 => "#FFA726", // Orange
            < 50 => "#42A5F5", // Blue
            < 75 => "#66BB6A", // Green
            < 100 => "#26A69A", // Teal
            _ => "#4CAF50"     // Success Green
        };

        private static string GetNotificationIcon(string type) => type.ToLowerInvariant() switch
        {
            "success" => "✅",
            "error" => "❌",
            "warning" => "⚠️",
            "info" => "ℹ️",
            "critical" => "🚨",
            _ => "📢"
        };

        private static string GetNotificationColor(string type) => type.ToLowerInvariant() switch
        {
            "success" => "#4CAF50",
            "error" => "#F44336",
            "warning" => "#FF9800",
            "info" => "#2196F3",
            "critical" => "#E91E63",
            _ => "#607D8B"
        };

        private static bool ShouldAutoHide(string type) => type.ToLowerInvariant() switch
        {
            "error" => false,
            "critical" => false,
            _ => true
        };

        private static int GetNotificationDuration(string type) => type.ToLowerInvariant() switch
        {
            "success" => 3000,
            "info" => 5000,
            "warning" => 7000,
            "error" => 0,      // Don't auto-hide
            "critical" => 0,   // Don't auto-hide
            _ => 4000
        };

        private static bool IsCriticalNotification(string type) => type.ToLowerInvariant() switch
        {
            "error" => true,
            "critical" => true,
            _ => false
        };

        private static void RecordNotificationSent(string userUpn)
        {
            _notificationsSent.AddOrUpdate(userUpn, 1, (key, value) => value + 1);
        }

        #endregion
    }

    /// <summary>
    /// [P2-MONITORING] SignalR metrics model
    /// </summary>
    public class SignalRMetrics
    {
        public int ActiveConnections { get; set; }
        public int TotalNotificationsSent { get; set; }
        public Dictionary<string, DateTime> ConnectionsByUser { get; set; } = new();
        public Dictionary<string, int> NotificationsByUser { get; set; } = new();
        public DateTime MeasuredAt { get; set; }

        public override string ToString()
        {
            return $"SignalR Metrics: {ActiveConnections} connections, {TotalNotificationsSent} notifications sent";
        }
    }
} 