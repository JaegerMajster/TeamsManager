using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.Core.Models;

namespace TeamsManager.UI.Services
{
    public interface ISignalRService
    {
        IObservable<object> HealthUpdates { get; }
        IObservable<object> OperationUpdates { get; }
        IObservable<object> MetricsUpdates { get; }
        IObservable<object> AlertUpdates { get; }
        IObservable<ConnectionState> ConnectionStateChanged { get; }
        
        ConnectionState ConnectionState { get; }
        bool IsConnected { get; }
        
        Task ConnectAsync();
        Task DisconnectAsync();
        Task RequestHealthCheck();
        Task RequestAutoRepair();
        Task GetActiveOperations();
        Task RequestCacheOptimization();
        Task GetMonitoringStats();
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    public class SignalRService : ISignalRService, IDisposable
    {
        private readonly HubConnection _hubConnection;
        private readonly IMsalAuthService _authService;
        private readonly ILogger<SignalRService> _logger;
        
        private readonly Subject<object> _healthSubject = new();
        private readonly Subject<object> _operationSubject = new();
        private readonly Subject<object> _metricsSubject = new();
        private readonly Subject<object> _alertSubject = new();
        private readonly Subject<ConnectionState> _connectionStateSubject = new();

        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private bool _disposed = false;

        public IObservable<object> HealthUpdates => _healthSubject.AsObservable();
        public IObservable<object> OperationUpdates => _operationSubject.AsObservable();
        public IObservable<object> MetricsUpdates => _metricsSubject.AsObservable();
        public IObservable<object> AlertUpdates => _alertSubject.AsObservable();
        public IObservable<ConnectionState> ConnectionStateChanged => _connectionStateSubject.AsObservable();

        public ConnectionState ConnectionState 
        { 
            get => _connectionState;
            private set
            {
                if (_connectionState != value)
                {
                    _connectionState = value;
                    _connectionStateSubject.OnNext(value);
                }
            }
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public SignalRService(IMsalAuthService authService, ILogger<SignalRService> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5001/monitoringHub", options =>
                {
                    options.AccessTokenProvider = async () => 
                    {
                        try
                        {
                            return await _authService.GetAccessTokenAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[SIGNALR-SERVICE] Error getting access token");
                            return null;
                        }
                    };
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            // Health Updates
            _hubConnection.On<object>("HealthUpdate", update =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Health update received");
                _healthSubject.OnNext(update);
            });

            _hubConnection.On<object>("HealthCheckResult", result =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Health check result received");
                _healthSubject.OnNext(result);
            });

            _hubConnection.On<object>("HealthCheckError", error =>
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Health check error received");
                _healthSubject.OnNext(error);
            });

            // Operation Updates
            _hubConnection.On<object>("OperationUpdate", operation =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Operation update received");
                _operationSubject.OnNext(operation);
            });

            _hubConnection.On<object>("ActiveOperations", operations =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Active operations received");
                _operationSubject.OnNext(operations);
            });

            _hubConnection.On<object>("ActiveOperationsError", error =>
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Active operations error received");
                _operationSubject.OnNext(error);
            });

            // Metrics Updates
            _hubConnection.On<object>("MetricsUpdate", metrics =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Metrics update received");
                _metricsSubject.OnNext(metrics);
            });

            _hubConnection.On<object>("MonitoringStats", stats =>
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Monitoring stats received");
                _metricsSubject.OnNext(stats);
            });

            // System Alerts
            _hubConnection.On<object>("SystemAlert", alert =>
            {
                _logger.LogInformation("[SIGNALR-SERVICE] System alert received");
                _alertSubject.OnNext(alert);
            });

            // Initial Status
            _hubConnection.On<object>("InitialSystemStatus", status =>
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Initial system status received");
                _metricsSubject.OnNext(status);
            });

            // Auto Repair Results
            _hubConnection.On<object>("AutoRepairResult", result =>
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Auto repair result received");
                _healthSubject.OnNext(result);
            });

            _hubConnection.On<object>("AutoRepairError", error =>
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Auto repair error received");
                _healthSubject.OnNext(error);
            });

            // Cache Optimization Results
            _hubConnection.On<object>("CacheOptimizationResult", result =>
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Cache optimization result received");
                _metricsSubject.OnNext(result);
            });

            _hubConnection.On<object>("CacheOptimizationError", error =>
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cache optimization error received");
                _metricsSubject.OnNext(error);
            });

            // Connection State Events
            _hubConnection.Closed += OnClosed;
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;
        }

        private Task OnClosed(Exception? error)
        {
            if (error != null)
            {
                _logger.LogError(error, "[SIGNALR-SERVICE] Connection closed with error");
                ConnectionState = ConnectionState.Error;
            }
            else
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Connection closed normally");
                ConnectionState = ConnectionState.Disconnected;
            }
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception? error)
        {
            _logger.LogWarning(error, "[SIGNALR-SERVICE] Connection lost, attempting to reconnect");
            ConnectionState = ConnectionState.Reconnecting;
            return Task.CompletedTask;
        }

        private Task OnReconnected(string? connectionId)
        {
            _logger.LogInformation("[SIGNALR-SERVICE] Reconnected successfully with connection ID: {ConnectionId}", connectionId);
            ConnectionState = ConnectionState.Connected;
            return Task.CompletedTask;
        }

        public async Task ConnectAsync()
        {
            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Connection attempt ignored - already connected or connecting");
                return;
            }

            try
            {
                ConnectionState = ConnectionState.Connecting;
                _logger.LogInformation("[SIGNALR-SERVICE] Attempting to connect to monitoring hub");
                
                await _hubConnection.StartAsync();
                
                ConnectionState = ConnectionState.Connected;
                _logger.LogInformation("[SIGNALR-SERVICE] Successfully connected to monitoring hub");
            }
            catch (Exception ex)
            {
                ConnectionState = ConnectionState.Error;
                _logger.LogError(ex, "[SIGNALR-SERVICE] Failed to connect to monitoring hub");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                return;
            }

            try
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Disconnecting from monitoring hub");
                await _hubConnection.StopAsync();
                ConnectionState = ConnectionState.Disconnected;
                _logger.LogInformation("[SIGNALR-SERVICE] Successfully disconnected from monitoring hub");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error during disconnection");
                throw;
            }
        }

        public async Task RequestHealthCheck()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cannot request health check - not connected");
                throw new InvalidOperationException("Not connected to monitoring hub");
            }

            try
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Requesting health check");
                await _hubConnection.InvokeAsync("RequestHealthCheck");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error requesting health check");
                throw;
            }
        }

        public async Task RequestAutoRepair()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cannot request auto repair - not connected");
                throw new InvalidOperationException("Not connected to monitoring hub");
            }

            try
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Requesting auto repair");
                await _hubConnection.InvokeAsync("RequestAutoRepair");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error requesting auto repair");
                throw;
            }
        }

        public async Task GetActiveOperations()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cannot get active operations - not connected");
                throw new InvalidOperationException("Not connected to monitoring hub");
            }

            try
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Requesting active operations");
                await _hubConnection.InvokeAsync("GetActiveOperations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error getting active operations");
                throw;
            }
        }

        public async Task RequestCacheOptimization()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cannot request cache optimization - not connected");
                throw new InvalidOperationException("Not connected to monitoring hub");
            }

            try
            {
                _logger.LogInformation("[SIGNALR-SERVICE] Requesting cache optimization");
                await _hubConnection.InvokeAsync("RequestCacheOptimization");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error requesting cache optimization");
                throw;
            }
        }

        public async Task GetMonitoringStats()
        {
            if (!IsConnected)
            {
                _logger.LogWarning("[SIGNALR-SERVICE] Cannot get monitoring stats - not connected");
                throw new InvalidOperationException("Not connected to monitoring hub");
            }

            try
            {
                _logger.LogDebug("[SIGNALR-SERVICE] Requesting monitoring stats");
                await _hubConnection.InvokeAsync("GetMonitoringStats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error getting monitoring stats");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _hubConnection?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SIGNALR-SERVICE] Error during disposal");
            }

            _healthSubject?.Dispose();
            _operationSubject?.Dispose();
            _metricsSubject?.Dispose();
            _alertSubject?.Dispose();
            _connectionStateSubject?.Dispose();
        }
    }
} 