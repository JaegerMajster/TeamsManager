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
        
        private static readonly ConcurrentDictionary<string, string> _connections = new();
        private static readonly ConcurrentDictionary<string, DateTime> _connectionTimes = new();

        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Obsługa nowych połączeń z zarządzaniem grupami
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var userIdentifier = Context.UserIdentifier;
            var userUpn = Context.User?.FindFirst(ClaimTypes.Upn)?.Value ??
                         Context.User?.FindFirst(ClaimTypes.Email)?.Value ??
                         Context.User?.Identity?.Name;

            _logger.LogInformation("Nowe połączenie: {ConnectionId}, Użytkownik: {UserUpn}", 
                connectionId, userUpn);

            try
            {
                if (!string.IsNullOrWhiteSpace(userUpn))
                {
                    _connections[connectionId] = userUpn;
                    _connectionTimes[connectionId] = DateTime.UtcNow;

                    await Groups.AddToGroupAsync(connectionId, $"User_{userUpn}");
                    _logger.LogDebug("Dodano do grupy użytkownika: User_{UserUpn}", userUpn);

                    var userRoles = Context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? 
                                   Enumerable.Empty<string>();

                    foreach (var role in userRoles)
                    {
                        await Groups.AddToGroupAsync(connectionId, role);
                        _logger.LogDebug("Dodano do grupy roli: {Role}", role);
                    }

                    if (userRoles.Contains("Administrator") || userRoles.Contains("Admin"))
                    {
                        await Groups.AddToGroupAsync(connectionId, "Administrators");
                        _logger.LogDebug("Dodano do grupy Administratorów");
                    }

                    await Groups.AddToGroupAsync(connectionId, "AllUsers");
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
                _logger.LogInformation("Konfiguracja połączenia zakończona dla {UserUpn}", userUpn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas konfiguracji połączenia dla {UserUpn}", userUpn);
                throw;
            }
        }

        /// <summary>
        /// Obsługa rozłączeń z czyszczeniem
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            if (exception != null)
            {
                _logger.LogError(exception, "Rozłączenie z błędem. ConnectionId: {ConnectionId}, Użytkownik: {UserUpn}", 
                    connectionId, userUpn);
            }
            else
            {
                _logger.LogInformation("Normalne rozłączenie. ConnectionId: {ConnectionId}, Użytkownik: {UserUpn}", 
                    connectionId, userUpn);
            }

            try
            {
                if (_connectionTimes.TryGetValue(connectionId, out var connectionTime))
                {
                    var sessionDuration = DateTime.UtcNow - connectionTime;
                    _logger.LogInformation("Czas trwania sesji dla {UserUpn}: {Duration}", 
                        userUpn, sessionDuration);
                    _connectionTimes.TryRemove(connectionId, out _);
                }

                _connections.TryRemove(connectionId, out _);

                _logger.LogDebug("Czyszczenie połączenia zakończone dla {UserUpn}", userUpn);

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia rozłączenia dla {UserUpn}", userUpn);
            }
        }

        #region Metody wywoływane przez klienta

        /// <summary>
        /// Klient może poprosić o dołączenie do określonych grup powiadomień
        /// </summary>
        public async Task JoinNotificationGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return;

            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            try
            {
                if (IsAllowedGroup(groupName))
                {
                    await Groups.AddToGroupAsync(connectionId, groupName);
                    _logger.LogInformation("Użytkownik {UserUpn} dołączył do grupy: {GroupName}", userUpn, groupName);

                    await Clients.Caller.SendAsync("GroupJoined", new
                    {
                        GroupName = groupName,
                        Message = $"Dołączono do grupy powiadomień: {groupName}",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogWarning("Użytkownik {UserUpn} próbował dołączyć do nieautoryzowanej grupy: {GroupName}", 
                        userUpn, groupName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd dołączania do grupy {GroupName} dla użytkownika {UserUpn}", 
                    groupName, userUpn);
            }
        }

        /// <summary>
        /// Klient może opuścić grupy powiadomień
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
                _logger.LogInformation("Użytkownik {UserUpn} opuścił grupę: {GroupName}", userUpn, groupName);

                await Clients.Caller.SendAsync("GroupLeft", new
                {
                    GroupName = groupName,
                    Message = $"Opuszczono grupę powiadomień: {groupName}",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd opuszczania grupy {GroupName} dla użytkownika {UserUpn}", 
                    groupName, userUpn);
            }
        }

        /// <summary>
        /// Pobieranie statystyk połączeń
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
            _logger.LogDebug("Statystyki połączeń wysłane do {UserUpn}", userUpn);
        }

        #endregion

        #region Metody pomocnicze

        private static bool IsAllowedGroup(string groupName)
        {
            var allowedPatterns = new[]
            {
                "User_",
                "Department_",
                "Team_",
                "Project_",
                "AllUsers",
                "Notifications"
            };

            return allowedPatterns.Any(pattern => groupName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)) ||
                   groupName.Equals("AllUsers", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Pobieranie aktualnych metryk hub
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
    /// Model metryk hub
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