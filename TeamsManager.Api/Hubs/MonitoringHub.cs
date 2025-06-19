using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Collections.Concurrent;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Application.Services;

namespace TeamsManager.Api.Hubs
{
    [Authorize]
    public class MonitoringHub : Hub
    {
        private readonly IHealthMonitoringOrchestrator _healthOrchestrator;
        private readonly IOperationHistoryService _operationService;
        private readonly ILogger<MonitoringHub> _logger;
        
        // Śledzenie połączeń dla lepszej wydajności
        private static readonly ConcurrentDictionary<string, string> _connections = new();
        private static readonly ConcurrentDictionary<string, DateTime> _connectionTimes = new();

        public MonitoringHub(
            IHealthMonitoringOrchestrator healthOrchestrator,
            IOperationHistoryService operationService,
            ILogger<MonitoringHub> logger)
        {
            _healthOrchestrator = healthOrchestrator ?? throw new ArgumentNullException(nameof(healthOrchestrator));
            _operationService = operationService ?? throw new ArgumentNullException(nameof(operationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Obsługa nowych połączeń monitorowania
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var userIdentifier = Context.UserIdentifier;
            var userUpn = Context.User?.FindFirst(ClaimTypes.Upn)?.Value ??
                         Context.User?.FindFirst(ClaimTypes.Email)?.Value ??
                         Context.User?.Identity?.Name;

            _logger.LogInformation("Nowe połączenie monitorowania: {ConnectionId}, Użytkownik: {UserUpn}",
                connectionId, userUpn);

            try
            {
                if (!string.IsNullOrWhiteSpace(userUpn))
                {
                    _connections[connectionId] = userUpn;
                    _connectionTimes[connectionId] = DateTime.UtcNow;

                    await Groups.AddToGroupAsync(connectionId, "MonitoringClients");
                    
                    var userRoles = Context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? 
                                   Enumerable.Empty<string>();
                    
                    if (userRoles.Contains("Administrator") || userRoles.Contains("Admin"))
                    {
                        await Groups.AddToGroupAsync(connectionId, "AdminMonitoring");
                        _logger.LogDebug("Dodano do grupy AdminMonitoring");
                    }

                    var initialStatus = await GetInitialSystemStatus();
                    await Clients.Caller.SendAsync("InitialSystemStatus", initialStatus);
                }

                await base.OnConnectedAsync();
                _logger.LogInformation("Konfiguracja połączenia monitorowania zakończona dla {UserUpn}", userUpn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas konfiguracji połączenia monitorowania dla {UserUpn}", userUpn);
                throw;
            }
        }

        /// <summary>
        /// Obsługa rozłączeń monitorowania
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            if (exception != null)
            {
                _logger.LogError(exception, "Rozłączenie monitorowania z błędem. ConnectionId: {ConnectionId}, Użytkownik: {UserUpn}", 
                    connectionId, userUpn);
            }
            else
            {
                _logger.LogInformation("Normalne rozłączenie monitorowania. ConnectionId: {ConnectionId}, Użytkownik: {UserUpn}", 
                    connectionId, userUpn);
            }

            try
            {
                if (_connectionTimes.TryGetValue(connectionId, out var connectionTime))
                {
                    var sessionDuration = DateTime.UtcNow - connectionTime;
                    _logger.LogInformation("Czas trwania sesji monitorowania dla {UserUpn}: {Duration}", 
                        userUpn, sessionDuration);
                    _connectionTimes.TryRemove(connectionId, out _);
                }

                _connections.TryRemove(connectionId, out _);

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia rozłączenia monitorowania dla {UserUpn}", userUpn);
            }
        }

        #region Metody wywoływane przez klienta

        /// <summary>
        /// Żądanie kompleksowego sprawdzenia stanu zdrowia
        /// </summary>
        public async Task RequestHealthCheck()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("Sprawdzenie stanu zdrowia zażądane przez {UserUpn}", userUpn);

            try
            {
                // TODO: W rzeczywistej implementacji potrzebujemy accessToken
                var result = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("");
                
                await Clients.Group("MonitoringClients").SendAsync("HealthCheckResult", new
                {
                    Result = result,
                    RequestedBy = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wykonywania sprawdzenia stanu zdrowia dla {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("HealthCheckError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Żądanie automatycznej naprawy
        /// </summary>
        public async Task RequestAutoRepair()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("Automatyczna naprawa zażądana przez {UserUpn}", userUpn);

            try
            {
                var repairOptions = new RepairOptions
                {
                    RepairGraphConnection = true,
                    ClearInvalidCache = true,
                    RestartStuckProcesses = true,
                    SendAdminNotifications = true,
                    DryRun = false
                };

                var result = await _healthOrchestrator.AutoRepairCommonIssuesAsync(repairOptions, "");
                
                await Clients.Group("MonitoringClients").SendAsync("AutoRepairResult", new
                {
                    Result = result,
                    RequestedBy = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wykonywania automatycznej naprawy dla {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("AutoRepairError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Pobranie aktywnych operacji
        /// </summary>
        public async Task GetActiveOperations()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            
            try
            {
                var activeOperations = await _operationService.GetActiveOperationsAsync();
                var processStatuses = await _healthOrchestrator.GetActiveProcessesStatusAsync();
                
                await Clients.Caller.SendAsync("ActiveOperations", new
                {
                    Operations = activeOperations,
                    ProcessStatuses = processStatuses,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania aktywnych operacji dla {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("ActiveOperationsError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Żądanie optymalizacji cache
        /// </summary>
        public async Task RequestCacheOptimization()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("Optymalizacja cache zażądana przez {UserUpn}", userUpn);

            try
            {
                var result = await _healthOrchestrator.OptimizeCachePerformanceAsync("");
                
                await Clients.Group("MonitoringClients").SendAsync("CacheOptimizationResult", new
                {
                    Result = result,
                    RequestedBy = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wykonywania optymalizacji cache dla {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("CacheOptimizationError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Pobranie statystyk monitorowania
        /// </summary>
        public async Task GetMonitoringStats()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            
            var stats = new
            {
                TotalConnections = _connections.Count,
                ActiveMonitoringClients = _connections.Count,
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

            await Clients.Caller.SendAsync("MonitoringStats", stats);
            _logger.LogDebug("Statystyki monitorowania wysłane do {UserUpn}", userUpn);
        }

        #endregion

        #region Metody rozgłaszania po stronie serwera

        /// <summary>
        /// Rozgłoszenie aktualizacji stanu zdrowia do wszystkich klientów monitorowania
        /// </summary>
        public static async Task BroadcastHealthUpdate(IHubContext<MonitoringHub> hubContext, object healthUpdate)
        {
            await hubContext.Clients.Group("MonitoringClients").SendAsync("HealthUpdate", new
            {
                Update = healthUpdate,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Rozgłoszenie aktualizacji postępu operacji
        /// </summary>
        public static async Task BroadcastOperationUpdate(IHubContext<MonitoringHub> hubContext, object operationUpdate)
        {
            await hubContext.Clients.Group("MonitoringClients").SendAsync("OperationUpdate", new
            {
                Update = operationUpdate,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Rozgłoszenie aktualizacji metryk systemu
        /// </summary>
        public static async Task BroadcastMetricsUpdate(IHubContext<MonitoringHub> hubContext, object metricsUpdate)
        {
            await hubContext.Clients.Group("MonitoringClients").SendAsync("MetricsUpdate", new
            {
                Update = metricsUpdate,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Rozgłoszenie alertu systemowego
        /// </summary>
        public static async Task BroadcastSystemAlert(IHubContext<MonitoringHub> hubContext, object systemAlert)
        {
            await hubContext.Clients.Group("MonitoringClients").SendAsync("SystemAlert", new
            {
                Alert = systemAlert,
                Timestamp = DateTime.UtcNow
            });
        }

        #endregion

        #region Metody pomocnicze

        private async Task<object> GetInitialSystemStatus()
        {
            try
            {
                var activeOperations = await _operationService.GetActiveOperationsAsync();
                var processStatuses = await _healthOrchestrator.GetActiveProcessesStatusAsync();
                
                return new
                {
                    ActiveOperationsCount = activeOperations.Count(),
                    ActiveProcessesCount = processStatuses.Count(),
                    SystemStatus = "Running",
                    LastUpdate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania początkowego statusu systemu");
                return new
                {
                    SystemStatus = "Error",
                    ErrorMessage = ex.Message,
                    LastUpdate = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Pobranie metryk hub dla monitorowania
        /// </summary>
        public static MonitoringHubMetrics GetHubMetrics()
        {
            var connectionsByUser = _connections.Values
                .GroupBy(userUpn => userUpn)
                .ToDictionary(g => g.Key, g => g.Count());

            var sessionDurations = _connectionTimes.Values
                .Select(startTime => DateTime.UtcNow - startTime)
                .ToList();

            return new MonitoringHubMetrics
            {
                ActiveConnections = _connections.Count,
                ConnectionsByUser = connectionsByUser,
                AverageSessionDuration = sessionDurations.Any() 
                    ? TimeSpan.FromTicks((long)sessionDurations.Average(ts => ts.Ticks))
                    : TimeSpan.Zero,
                MeasuredAt = DateTime.UtcNow
            };
        }

        #endregion
    }

    /// <summary>
    /// Metryki dla hub monitorowania
    /// </summary>
    public class MonitoringHubMetrics
    {
        public int ActiveConnections { get; set; }
        public Dictionary<string, int> ConnectionsByUser { get; set; } = new();
        public TimeSpan AverageSessionDuration { get; set; }
        public DateTime MeasuredAt { get; set; }

        public override string ToString()
        {
            return $"MonitoringHub: {ActiveConnections} connections, avg session: {AverageSessionDuration:mm\\:ss}";
        }
    }
} 
