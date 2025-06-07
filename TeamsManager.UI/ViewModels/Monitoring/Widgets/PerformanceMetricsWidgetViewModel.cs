using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class PerformanceMetricsWidgetViewModel : BaseViewModel
    {
        private readonly IMonitoringDataService _dataService;
        private readonly ILogger<PerformanceMetricsWidgetViewModel> _logger;
        
        private double _cpuUsage;
        private double _memoryUsage;
        private double _diskUsage;
        private double _networkThroughput;
        private int _activeConnections;
        private int _requestsPerMinute;
        private double _averageResponseTime;
        private double _errorRate;
        private DateTime _lastUpdateTime = DateTime.Now;
        private bool _isLoading;
        
        public double CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }
        
        public double MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }
        
        public double DiskUsage
        {
            get => _diskUsage;
            set => SetProperty(ref _diskUsage, value);
        }
        
        public double NetworkThroughput
        {
            get => _networkThroughput;
            set => SetProperty(ref _networkThroughput, value);
        }
        
        public int ActiveConnections
        {
            get => _activeConnections;
            set => SetProperty(ref _activeConnections, value);
        }
        
        public int RequestsPerMinute
        {
            get => _requestsPerMinute;
            set => SetProperty(ref _requestsPerMinute, value);
        }
        
        public double AverageResponseTime
        {
            get => _averageResponseTime;
            set => SetProperty(ref _averageResponseTime, value);
        }
        
        public double ErrorRate
        {
            get => _errorRate;
            set => SetProperty(ref _errorRate, value);
        }
        
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetProperty(ref _lastUpdateTime, value);
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        public RelayCommand RefreshCommand { get; }
        
        public PerformanceMetricsWidgetViewModel(
            IMonitoringDataService dataService,
            ILogger<PerformanceMetricsWidgetViewModel> logger)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsLoading);
        }
        
        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                _logger.LogDebug("[METRICS-WIDGET] Refreshing performance metrics");
                
                var metrics = await _dataService.GetPerformanceMetricsAsync();
                
                CpuUsage = metrics.CpuUsagePercent;
                MemoryUsage = metrics.MemoryUsagePercent;
                DiskUsage = metrics.DiskUsagePercent;
                NetworkThroughput = metrics.NetworkThroughputMbps;
                ActiveConnections = metrics.ActiveConnections;
                RequestsPerMinute = metrics.RequestsPerMinute;
                AverageResponseTime = metrics.AverageResponseTimeMs;
                ErrorRate = metrics.ErrorRate;
                LastUpdateTime = metrics.Timestamp;
                
                _logger.LogDebug("[METRICS-WIDGET] Performance metrics refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[METRICS-WIDGET] Error refreshing performance metrics");
            }
            finally
            {
                IsLoading = false;
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
        
        public void ProcessMetricsUpdate(object metrics)
        {
            try
            {
                _logger.LogDebug("[METRICS-WIDGET] Processing metrics update");
                
                // In a real implementation, we would deserialize the metrics object
                // For now, just trigger a refresh
                _ = Task.Run(async () => await RefreshAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[METRICS-WIDGET] Error processing metrics update");
            }
        }
    }
} 