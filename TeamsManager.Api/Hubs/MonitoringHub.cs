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
        
        // Track connections for better performance (wzorzec z NotificationHub)
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
        /// Handle new monitoring connections
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var connectionId = Context.ConnectionId;
            var userUpn = Context.User?.FindFirst(ClaimTypes.Upn)?.Value ??
                         Context.User?.FindFirst(ClaimTypes.Email)?.Value ??
                         Context.User?.Identity?.Name;

            _logger.LogInformation("[MONITORING-HUB] New monitoring connection: {ConnectionId}, User: {UserUpn}", 
                connectionId, userUpn);

            try
            {
                // Track connection
                if (!string.IsNullOrWhiteSpace(userUpn))
                {
                    _connections[connectionId] = userUpn;
                    _connectionTimes[connectionId] = DateTime.UtcNow;

                    // Add to monitoring group
                    await Groups.AddToGroupAsync(connectionId, "MonitoringClients");
                    
                    // Add administrators to admin monitoring group
                    var userRoles = Context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? 
                                   Enumerable.Empty<string>();
                    
                    if (userRoles.Contains("Administrator") || userRoles.Contains("Admin"))
                    {
                        await Groups.AddToGroupAsync(connectionId, "AdminMonitoring");
                        _logger.LogDebug("[MONITORING-HUB] Added to AdminMonitoring group");
                    }

                    // Send initial system status
                    var initialStatus = await GetInitialSystemStatus();
                    await Clients.Caller.SendAsync("InitialSystemStatus", initialStatus);
                }

                await base.OnConnectedAsync();
                _logger.LogInformation("[MONITORING-HUB] Monitoring connection setup completed for {UserUpn}", userUpn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-HUB] Error during monitoring connection setup for {UserUpn}", userUpn);
                throw;
            }
        }

        /// <summary>
        /// Handle monitoring disconnections
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            var userUpn = _connections.TryGetValue(connectionId, out var upn) ? upn : "Unknown";

            if (exception != null)
            {
                _logger.LogError(exception, "[MONITORING-HUB] Monitoring disconnection with error. ConnectionId: {ConnectionId}, User: {UserUpn}", 
                    connectionId, userUpn);
            }
            else
            {
                _logger.LogInformation("[MONITORING-HUB] Normal monitoring disconnection. ConnectionId: {ConnectionId}, User: {UserUpn}", 
                    connectionId, userUpn);
            }

            try
            {
                // Calculate session duration
                if (_connectionTimes.TryGetValue(connectionId, out var connectionTime))
                {
                    var sessionDuration = DateTime.UtcNow - connectionTime;
                    _logger.LogInformation("[MONITORING-HUB] Monitoring session duration for {UserUpn}: {Duration}", 
                        userUpn, sessionDuration);
                    _connectionTimes.TryRemove(connectionId, out _);
                }

                // Cleanup connection tracking
                _connections.TryRemove(connectionId, out _);

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-HUB] Error during monitoring disconnection cleanup for {UserUpn}", userUpn);
            }
        }

        #region Client-callable Methods

        /// <summary>
        /// Request comprehensive health check
        /// </summary>
        public async Task RequestHealthCheck()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("[MONITORING-HUB] Health check requested by {UserUpn}", userUpn);

            try
            {
                // Note: W rzeczywistej implementacji potrzebujemy accessToken
                // Na razie używamy pustego string jako placeholder
                var result = await _healthOrchestrator.RunComprehensiveHealthCheckAsync("");
                
                // Broadcast to all monitoring clients
                await Clients.Group("MonitoringClients").SendAsync("HealthCheckResult", new
                {
                    Result = result,
                    RequestedBy = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-HUB] Error executing health check for {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("HealthCheckError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Request auto repair
        /// </summary>
        public async Task RequestAutoRepair()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("[MONITORING-HUB] Auto repair requested by {UserUpn}", userUpn);

            try
            {
                var repairOptions = new RepairOptions
                {
                    RepairPowerShellConnection = true,
                    ClearInvalidCache = true,
                    RestartStuckProcesses = true,
                    SendAdminNotifications = true,
                    DryRun = false
                };

                var result = await _healthOrchestrator.AutoRepairCommonIssuesAsync(repairOptions, "");
                
                // Broadcast to monitoring clients
                await Clients.Group("MonitoringClients").SendAsync("AutoRepairResult", new
                {
                    Result = result,
                    RequestedBy = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-HUB] Error executing auto repair for {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("AutoRepairError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get active operations
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
                _logger.LogError(ex, "[MONITORING-HUB] Error getting active operations for {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("ActiveOperationsError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Request cache optimization
        /// </summary>
        public async Task RequestCacheOptimization()
        {
            var userUpn = _connections.TryGetValue(Context.ConnectionId, out var upn) ? upn : "Unknown";
            _logger.LogInformation("[MONITORING-HUB] Cache optimization requested by {UserUpn}", userUpn);

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
                _logger.LogError(ex, "[MONITORING-HUB] Error executing cache optimization for {UserUpn}", userUpn);
                await Clients.Caller.SendAsync("CacheOptimizationError", new
                {
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Get monitoring statistics
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
            _logger.LogDebug("[MONITORING-HUB] Monitoring stats sent to {UserUpn}", userUpn);
        }

        #endregion

        #region Server-side Broadcasting Methods

        /// <summary>
        /// Broadcast health status update to all monitoring clients
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
        /// Broadcast operation progress update
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
        /// Broadcast system metrics update
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
        /// Broadcast system alert
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

        #region Helper Methods

        private async Task<object> GetInitialSystemStatus()
        {
            try
            {
                // Get basic system status
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
                _logger.LogError(ex, "[MONITORING-HUB] Error getting initial system status");
                return new
                {
                    SystemStatus = "Error",
                    ErrorMessage = ex.Message,
                    LastUpdate = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Get hub metrics for monitoring
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
    /// Metrics for monitoring hub
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