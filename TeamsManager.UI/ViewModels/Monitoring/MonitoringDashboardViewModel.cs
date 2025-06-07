using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using ReactiveUI;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels.Monitoring.Widgets;

namespace TeamsManager.UI.ViewModels.Monitoring
{
    public class MonitoringDashboardViewModel : BaseViewModel, IDisposable
    {
        private readonly ISignalRService _signalRService;
        private readonly IMonitoringDataService _dataService;
        private readonly ILogger<MonitoringDashboardViewModel> _logger;
        private readonly CompositeDisposable _disposables = new();
        
        // Widget ViewModels
        public SystemHealthWidgetViewModel SystemHealthViewModel { get; }
        public PerformanceMetricsWidgetViewModel PerformanceMetricsViewModel { get; }
        public ActiveOperationsWidgetViewModel ActiveOperationsViewModel { get; }
        public AlertsWidgetViewModel AlertsViewModel { get; }
        
        // UI Properties
        public SnackbarMessageQueue MessageQueue { get; }
        
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        private DateTime _lastUpdateTime;
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }
        
        private bool _isAutoRefreshEnabled;
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set 
            {
                if (SetProperty(ref _isAutoRefreshEnabled, value))
                {
                    HandleAutoRefreshToggle();
                }
            }
        }
        
        private ConnectionState _connectionState;
        public ConnectionState ConnectionState
        {
            get => _connectionState;
            set => SetProperty(ref _connectionState, value);
        }
        
        // Commands
        public RelayCommand RefreshCommand { get; }
        public AsyncRelayCommand RunHealthCheckCommand { get; }
        public AsyncRelayCommand AutoRepairCommand { get; }
        public AsyncRelayCommand CacheOptimizationCommand { get; }
        
        public MonitoringDashboardViewModel(
            ISignalRService signalRService,
            IMonitoringDataService dataService,
            SystemHealthWidgetViewModel systemHealthViewModel,
            PerformanceMetricsWidgetViewModel performanceMetricsViewModel,
            ActiveOperationsWidgetViewModel activeOperationsViewModel,
            AlertsWidgetViewModel alertsViewModel,
            ILogger<MonitoringDashboardViewModel> logger)
        {
            _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            SystemHealthViewModel = systemHealthViewModel ?? throw new ArgumentNullException(nameof(systemHealthViewModel));
            PerformanceMetricsViewModel = performanceMetricsViewModel ?? throw new ArgumentNullException(nameof(performanceMetricsViewModel));
            ActiveOperationsViewModel = activeOperationsViewModel ?? throw new ArgumentNullException(nameof(activeOperationsViewModel));
            AlertsViewModel = alertsViewModel ?? throw new ArgumentNullException(nameof(alertsViewModel));
            
            MessageQueue = new SnackbarMessageQueue();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(async () => await RefreshData(), () => !IsLoading);
            RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheck, _ => !IsLoading && _signalRService.IsConnected);
            AutoRepairCommand = new AsyncRelayCommand(RunAutoRepair, _ => !IsLoading && _signalRService.IsConnected);
            CacheOptimizationCommand = new AsyncRelayCommand(RunCacheOptimization, _ => !IsLoading && _signalRService.IsConnected);
            
            InitializeAsync();
        }
        
        private async void InitializeAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("[MONITORING-DASHBOARD] Initializing dashboard");
                
                // Connect to SignalR
                await _signalRService.ConnectAsync();
                MessageQueue.Enqueue("Connected to monitoring service");
                
                // Subscribe to SignalR updates
                SubscribeToSignalRUpdates();
                
                // Load initial data
                await RefreshData();
                
                // Enable auto-refresh by default
                IsAutoRefreshEnabled = true;
                
                _logger.LogInformation("[MONITORING-DASHBOARD] Dashboard initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Failed to initialize dashboard");
                MessageQueue.Enqueue($"Failed to initialize: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void SubscribeToSignalRUpdates()
        {
            // Connection state changes
            _signalRService.ConnectionStateChanged
                .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                .Subscribe(state =>
                {
                    ConnectionState = state;
                    UpdateCommandStates();
                    
                    var message = state switch
                    {
                        Services.ConnectionState.Connected => "Connected to monitoring service",
                        Services.ConnectionState.Connecting => "Connecting to monitoring service...",
                        Services.ConnectionState.Reconnecting => "Reconnecting to monitoring service...",
                        Services.ConnectionState.Disconnected => "Disconnected from monitoring service",
                        Services.ConnectionState.Error => "Connection error",
                        _ => $"Connection state: {state}"
                    };
                    
                    MessageQueue.Enqueue(message);
                })
                .DisposeWith(_disposables);
            
            // Health updates
            _signalRService.HealthUpdates
                .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                .Subscribe(update =>
                {
                    _logger.LogDebug("[MONITORING-DASHBOARD] Health update received");
                    SystemHealthViewModel.ProcessHealthUpdate(update);
                    LastUpdateTime = DateTime.Now;
                })
                .DisposeWith(_disposables);
            
            // Operation updates
            _signalRService.OperationUpdates
                .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                .Subscribe(update =>
                {
                    _logger.LogDebug("[MONITORING-DASHBOARD] Operation update received");
                    ActiveOperationsViewModel.ProcessOperationUpdate(update);
                    LastUpdateTime = DateTime.Now;
                })
                .DisposeWith(_disposables);
            
            // Metrics updates
            _signalRService.MetricsUpdates
                .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                .Subscribe(metrics =>
                {
                    _logger.LogDebug("[MONITORING-DASHBOARD] Metrics update received");
                    PerformanceMetricsViewModel.ProcessMetricsUpdate(metrics);
                    LastUpdateTime = DateTime.Now;
                })
                .DisposeWith(_disposables);
            
            // Alert updates
            _signalRService.AlertUpdates
                .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                .Subscribe(alert =>
                {
                    _logger.LogInformation("[MONITORING-DASHBOARD] Alert received");
                    AlertsViewModel.ProcessAlertUpdate(alert);
                    MessageQueue.Enqueue("New system alert received");
                })
                .DisposeWith(_disposables);
        }
        
        private async Task RefreshData()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                _logger.LogDebug("[MONITORING-DASHBOARD] Refreshing dashboard data");
                
                // Refresh all widgets in parallel
                var tasks = new[]
                {
                    SystemHealthViewModel.RefreshAsync(),
                    PerformanceMetricsViewModel.RefreshAsync(),
                    ActiveOperationsViewModel.RefreshAsync(),
                    AlertsViewModel.RefreshAsync()
                };
                
                await Task.WhenAll(tasks);
                
                LastUpdateTime = DateTime.Now;
                MessageQueue.Enqueue("Dashboard data refreshed");
                
                _logger.LogDebug("[MONITORING-DASHBOARD] Dashboard data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Error refreshing dashboard data");
                MessageQueue.Enqueue($"Refresh failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                UpdateCommandStates();
            }
        }
        
        private async Task RunHealthCheck()
        {
            try
            {
                _logger.LogInformation("[MONITORING-DASHBOARD] Running health check");
                MessageQueue.Enqueue("Health check initiated...");
                
                await _signalRService.RequestHealthCheck();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Error running health check");
                MessageQueue.Enqueue($"Health check failed: {ex.Message}");
            }
        }
        
        private async Task RunAutoRepair()
        {
            try
            {
                _logger.LogInformation("[MONITORING-DASHBOARD] Running auto repair");
                MessageQueue.Enqueue("Auto repair initiated...");
                
                await _signalRService.RequestAutoRepair();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Error running auto repair");
                MessageQueue.Enqueue($"Auto repair failed: {ex.Message}");
            }
        }
        
        private async Task RunCacheOptimization()
        {
            try
            {
                _logger.LogInformation("[MONITORING-DASHBOARD] Running cache optimization");
                MessageQueue.Enqueue("Cache optimization initiated...");
                
                await _signalRService.RequestCacheOptimization();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Error running cache optimization");
                MessageQueue.Enqueue($"Cache optimization failed: {ex.Message}");
            }
        }
        
        private void HandleAutoRefreshToggle()
        {
            if (IsAutoRefreshEnabled)
            {
                // Start auto-refresh timer (30 seconds)
                Observable.Interval(TimeSpan.FromSeconds(30))
                    .ObserveOn(SynchronizationContext.Current != null ? new SynchronizationContextScheduler(SynchronizationContext.Current) : Scheduler.CurrentThread)
                    .Subscribe(async _ => await RefreshData())
                    .DisposeWith(_disposables);
                    
                MessageQueue.Enqueue("Auto-refresh enabled (30s interval)");
                _logger.LogInformation("[MONITORING-DASHBOARD] Auto-refresh enabled");
            }
            else
            {
                // Clear existing subscriptions (will stop auto-refresh)
                _disposables.Clear();
                
                // Re-subscribe to SignalR (but not auto-refresh)
                SubscribeToSignalRUpdates();
                
                MessageQueue.Enqueue("Auto-refresh disabled");
                _logger.LogInformation("[MONITORING-DASHBOARD] Auto-refresh disabled");
            }
        }
        
        private void UpdateCommandStates()
        {
            RefreshCommand.RaiseCanExecuteChanged();
            RunHealthCheckCommand.RaiseCanExecuteChanged();
            AutoRepairCommand.RaiseCanExecuteChanged();
            CacheOptimizationCommand.RaiseCanExecuteChanged();
        }
        
        public void Dispose()
        {
            _logger.LogInformation("[MONITORING-DASHBOARD] Disposing dashboard");
            
            _disposables?.Dispose();
            
            try
            {
                _signalRService?.DisconnectAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MONITORING-DASHBOARD] Error during SignalR disconnection");
            }
        }
    }
} 
