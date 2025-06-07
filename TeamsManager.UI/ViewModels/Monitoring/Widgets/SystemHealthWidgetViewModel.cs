using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class SystemHealthWidgetViewModel : BaseViewModel
    {
        private readonly IMonitoringDataService _dataService;
        private readonly ILogger<SystemHealthWidgetViewModel> _logger;
        
        private HealthCheck _overallStatus = HealthCheck.Healthy;
        private DateTime _lastUpdateTime = DateTime.Now;
        private bool _isLoading;
        
        public HealthCheck OverallStatus
        {
            get => _overallStatus;
            set => SetProperty(ref _overallStatus, value);
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
        
        public ObservableCollection<HealthComponentViewModel> HealthComponents { get; }
        
        public RelayCommand RefreshCommand { get; }
        
        // Computed properties for UI
        public int HealthyCount => HealthComponents.Count(c => c.Status == HealthCheck.Healthy);
        public int TotalCount => HealthComponents.Count;
        public double HealthPercentage => TotalCount > 0 ? (double)HealthyCount / TotalCount * 100 : 0;
        
        public SystemHealthWidgetViewModel(
            IMonitoringDataService dataService,
            ILogger<SystemHealthWidgetViewModel> logger)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            HealthComponents = new ObservableCollection<HealthComponentViewModel>();
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsLoading);
        }
        
        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                _logger.LogDebug("[HEALTH-WIDGET] Refreshing health data");
                
                var healthData = await _dataService.GetSystemHealthAsync();
                
                OverallStatus = healthData.OverallStatus;
                LastUpdateTime = healthData.LastUpdate;
                
                // Update components
                HealthComponents.Clear();
                foreach (var component in healthData.Components)
                {
                    HealthComponents.Add(new HealthComponentViewModel
                    {
                        Name = component.Name,
                        Status = component.Status,
                        Description = component.Description,
                        ResponseTime = component.ResponseTime.TotalMilliseconds
                    });
                }
                
                // Notify computed properties
                OnPropertyChanged(nameof(HealthyCount));
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(HealthPercentage));
                
                _logger.LogDebug("[HEALTH-WIDGET] Health data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HEALTH-WIDGET] Error refreshing health data");
            }
            finally
            {
                IsLoading = false;
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
        
        public void ProcessHealthUpdate(object update)
        {
            try
            {
                _logger.LogDebug("[HEALTH-WIDGET] Processing health update");
                
                // In a real implementation, we would deserialize the update object
                // For now, just trigger a refresh
                _ = Task.Run(async () => await RefreshAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HEALTH-WIDGET] Error processing health update");
            }
        }
    }
    
    public class HealthComponentViewModel
    {
        public string Name { get; set; } = string.Empty;
        public HealthCheck Status { get; set; }
        public string Description { get; set; } = string.Empty;
        public double ResponseTime { get; set; }
    }
} 