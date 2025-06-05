// Plik: TeamsManager.Api/Hubs/NotificationHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Collections.Concurrent;
using System;

namespace TeamsManager.Api.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        
        // [P2-OPTIMIZATION] Track connections for better performance
        private static readonly ConcurrentDictionary<string, string> _connections = new();
        private static readonly ConcurrentDictionary<string, DateTime> _connectionTimes = new();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// [P2-REALTIME] Handle new connections with group management
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var userIdentifier = Context.UserIdentifier;
            var userUpn = Context.User?.FindFirst(ClaimTypes.Upn)?.Value ??
                         Context.User?.FindFirst(ClaimTypes.Email)?.Value ??
                         Context.User?.Identity?.Name;

            _logger.LogInformation("[P2-SIGNALR] New connection: {ConnectionId}, User: {UserUpn}", 
                connectionId, userUpn);

            try
            {
                // Track connection
                if (!string.IsNullOrWhiteSpace(userUpn))
                {
                    _connections[connectionId] = userUpn;
                    _connectionTimes[connectionId] = DateTime.UtcNow;

                    // Add to user-specific group
                    await Groups.AddToGroupAsync(connectionId, $"User_{userUpn}");
                    _logger.LogDebug("[P2-SIGNALR] Added to user group: User_{UserUpn}", userUpn);

                    // Add to role-based groups
                    var userRoles = Context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? 
                                   Enumerable.Empty<string>();

                    foreach (var role in userRoles)
                    {
                        await Groups.AddToGroupAsync(connectionId, role);
                        _logger.LogDebug("[P2-SIGNALR] Added to role group: {Role}", role);
                    }

                    // Add administrators to admin group
                    if (userRoles.Contains("Administrator") || userRoles.Contains("Admin"))
                    {
                        await Groups.AddToGroupAsync(connectionId, "Administrators");
                        _logger.LogDebug("[P2-SIGNALR] Added to Administrators group");
                    }

                    // Add all users to general group
                    await Groups.AddToGroupAsync(connectionId, "AllUsers");

                    // Send welcome message
                    await Clients.Caller.SendAsync("ReceiveNotification", new
                    {
                        Type = "ConnectionEstablished",
                        Message = "✅ Połączenie z systemem powiadomień zostało nawiązane",
                        NotificationType = "success",
                        Timestamp = DateTime.UtcNow,
                        Icon = "🔗",
                        Color = "#4CAF50",
                        AutoHide = true,
                        Duration = 3000
                    });
                }

                await base.OnConnectedAsync();
                _logger.LogInformation("[P2-SIGNALR] Connection setup completed for {UserUpn}", userUpn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[P2-SIGNALR] Error during connection setup for {UserUpn}", userUpn);
                throw;
            }
        }

        /// <summary>
        /// [P2-REALTIME] Handle disconnections with cleanup
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            if (exception != null)
            {
                _logger.LogError(exception, "[P2-SIGNALR] Disconnection with error. ConnectionId: {ConnectionId}, User: {UserUpn}", 
                    connectionId, userUpn);
            }
            else
            {
                _logger.LogInformation("[P2-SIGNALR] Normal disconnection. ConnectionId: {ConnectionId}, User: {UserUpn}", 
                    connectionId, userUpn);
            }

            try
            {
                // Calculate session duration
                if (_connectionTimes.TryGetValue(connectionId, out var connectionTime))
                {
                    var sessionDuration = DateTime.UtcNow - connectionTime;
                    _logger.LogInformation("[P2-SIGNALR] Session duration for {UserUpn}: {Duration}", 
                        userUpn, sessionDuration);
                    _connectionTimes.TryRemove(connectionId, out _);
                }

                // Cleanup connection tracking
                _connections.TryRemove(connectionId, out _);

                // Groups are automatically cleaned up by SignalR, but we log for monitoring
                _logger.LogDebug("[P2-SIGNALR] Connection cleanup completed for {UserUpn}", userUpn);

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[P2-SIGNALR] Error during disconnection cleanup for {UserUpn}", userUpn);
            }
        }

        #region P2 Client-callable Methods

        /// <summary>
        /// [P2-REALTIME] Client can request to join specific notification groups
        /// </summary>
        public async Task JoinNotificationGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return;

            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            try
            {
                // Validate group name (security check)
                if (IsAllowedGroup(groupName))
                {
                    await Groups.AddToGroupAsync(connectionId, groupName);
                    _logger.LogInformation("[P2-SIGNALR] User {UserUpn} joined group: {GroupName}", userUpn, groupName);

                    await Clients.Caller.SendAsync("GroupJoined", new
                    {
                        GroupName = groupName,
                        Message = $"Dołączono do grupy powiadomień: {groupName}",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogWarning("[P2-SIGNALR] User {UserUpn} attempted to join unauthorized group: {GroupName}", 
                        userUpn, groupName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[P2-SIGNALR] Error joining group {GroupName} for user {UserUpn}", 
                    groupName, userUpn);
            }
        }

        /// <summary>
        /// [P2-REALTIME] Client can leave notification groups
        /// </summary>
        public async Task LeaveNotificationGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return;

            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            try
            {
                await Groups.RemoveFromGroupAsync(connectionId, groupName);
                _logger.LogInformation("[P2-SIGNALR] User {UserUpn} left group: {GroupName}", userUpn, groupName);

                await Clients.Caller.SendAsync("GroupLeft", new
                {
                    GroupName = groupName,
                    Message = $"Opuszczono grupę powiadomień: {groupName}",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[P2-SIGNALR] Error leaving group {GroupName} for user {UserUpn}", 
                    groupName, userUpn);
            }
        }

        /// <summary>
        /// [P2-MONITORING] Get connection statistics
        /// </summary>
        public async Task GetConnectionStats()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            
            var stats = new
            {
                TotalConnections = _connections.Count,
                UserConnection = new
                {
                    UserUpn = userUpn,
                    ConnectionId = Context.ConnectionId,
                    ConnectedAt = _connectionTimes.TryGetValue(Context.ConnectionId, out var time) ? time : DateTime.UtcNow,
                    SessionDuration = _connectionTimes.TryGetValue(Context.ConnectionId, out var startTime) 
                        ? DateTime.UtcNow - startTime 
                        : TimeSpan.Zero
                },
                Timestamp = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("ConnectionStats", stats);
            _logger.LogDebug("[P2-SIGNALR] Connection stats sent to {UserUpn}", userUpn);
        }

        #endregion

        #region Helper Methods

        private static bool IsAllowedGroup(string groupName)
        {
            // [P2-SECURITY] Define allowed group patterns
            var allowedPatterns = new[]
            {
                "User_",           // User-specific groups
                "Department_",     // Department-specific groups  
                "Team_",          // Team-specific groups
                "Project_",       // Project-specific groups
                "AllUsers",       // General group
                "Notifications"   // General notifications
            };

            return allowedPatterns.Any(pattern => groupName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)) ||
                   groupName.Equals("AllUsers", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// [P2-MONITORING] Get current hub metrics
        /// </summary>
        public static HubMetrics GetHubMetrics()
        {
            return new HubMetrics
            {
                ActiveConnections = _connections.Count,
                ConnectionsByUser = _connections.GroupBy(kvp => kvp.Value)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageSessionDuration = _connectionTimes.Values.Any() 
                    ? TimeSpan.FromTicks((long)_connectionTimes.Values.Select(t => (DateTime.UtcNow - t).Ticks).Average())
                    : TimeSpan.Zero,
                MeasuredAt = DateTime.UtcNow
            };
        }

        #endregion
    }

    /// <summary>
    /// [P2-MONITORING] Hub metrics model
    /// </summary>
    public class HubMetrics
    {
        public int ActiveConnections { get; set; }
        public Dictionary<string, int> ConnectionsByUser { get; set; } = new();
        public TimeSpan AverageSessionDuration { get; set; }
        public DateTime MeasuredAt { get; set; }

        public override string ToString()
        {
            return $"Hub Metrics: {ActiveConnections} connections, avg session: {AverageSessionDuration:hh\\:mm\\:ss}";
        }
    }
}