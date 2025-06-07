using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.UI.Models.Monitoring;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class AlertsWidgetViewModel : BaseViewModel
    {
        private readonly IMonitoringDataService _dataService;
        private readonly ILogger<AlertsWidgetViewModel> _logger;
        
        private bool _isLoading;
        private bool _isEmpty;
        private bool _hasUnacknowledgedAlerts;
        
        public ObservableCollection<SystemAlertViewModel> RecentAlerts { get; }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        public bool IsEmpty
        {
            get => _isEmpty;
            set => SetProperty(ref _isEmpty, value);
        }
        
        public bool HasUnacknowledgedAlerts
        {
            get => _hasUnacknowledgedAlerts;
            set => SetProperty(ref _hasUnacknowledgedAlerts, value);
        }
        
        public int UnacknowledgedAlertsCount => RecentAlerts.Count(a => !a.IsAcknowledged);
        
        public RelayCommand RefreshCommand { get; }
        public RelayCommand<SystemAlertViewModel> AcknowledgeAlertCommand { get; }
        public RelayCommand AcknowledgeAllCommand { get; }
        public RelayCommand ClearAlertsCommand { get; }
        
        public AlertsWidgetViewModel(
            IMonitoringDataService dataService,
            ILogger<AlertsWidgetViewModel> logger)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            RecentAlerts = new ObservableCollection<SystemAlertViewModel>();
            RecentAlerts.CollectionChanged += (_, _) => UpdateAlertCounts();
            
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsLoading);
            AcknowledgeAlertCommand = new RelayCommand<SystemAlertViewModel>(
                async alert => await AcknowledgeAlert(alert), 
                alert => alert?.IsAcknowledged == false);
            AcknowledgeAllCommand = new RelayCommand(
                async () => await AcknowledgeAllAlerts(), 
                () => HasUnacknowledgedAlerts);
            ClearAlertsCommand = new RelayCommand(
                async () => await ClearAllAlerts(), 
                () => RecentAlerts.Any());
        }
        
        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                _logger.LogDebug("[ALERTS-WIDGET] Refreshing alerts");
                
                var alerts = await _dataService.GetRecentAlertsAsync();
                
                RecentAlerts.Clear();
                foreach (var alert in alerts.OrderByDescending(a => a.Timestamp))
                {
                    RecentAlerts.Add(new SystemAlertViewModel
                    {
                        Id = alert.Id,
                        Level = alert.Level,
                        Message = alert.Message,
                        Component = alert.Component,
                        Timestamp = alert.Timestamp,
                        IsAcknowledged = alert.IsAcknowledged,
                        Details = alert.Details
                    });
                }
                
                UpdateAlertCounts();
                
                _logger.LogDebug("[ALERTS-WIDGET] Alerts refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ALERTS-WIDGET] Error refreshing alerts");
            }
            finally
            {
                IsLoading = false;
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
        
        public void ProcessAlertUpdate(object alert)
        {
            try
            {
                _logger.LogDebug("[ALERTS-WIDGET] Processing alert update");
                
                // In a real implementation, we would deserialize the alert object
                // and add it to the collection
                _ = Task.Run(async () => await RefreshAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ALERTS-WIDGET] Error processing alert update");
            }
        }
        
        private async Task AcknowledgeAlert(SystemAlertViewModel alert)
        {
            if (alert == null || alert.IsAcknowledged) return;
            
            try
            {
                _logger.LogInformation("[ALERTS-WIDGET] Acknowledging alert {AlertId}", alert.Id);
                
                // In a real implementation, we would call the service to acknowledge
                alert.IsAcknowledged = true;
                
                UpdateAlertCounts();
                
                _logger.LogInformation("[ALERTS-WIDGET] Alert {AlertId} acknowledged", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ALERTS-WIDGET] Error acknowledging alert {AlertId}", alert.Id);
            }
        }
        
        private async Task AcknowledgeAllAlerts()
        {
            try
            {
                _logger.LogInformation("[ALERTS-WIDGET] Acknowledging all alerts");
                
                foreach (var alert in RecentAlerts.Where(a => !a.IsAcknowledged))
                {
                    alert.IsAcknowledged = true;
                }
                
                UpdateAlertCounts();
                
                _logger.LogInformation("[ALERTS-WIDGET] All alerts acknowledged");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ALERTS-WIDGET] Error acknowledging all alerts");
            }
        }
        
        private async Task ClearAllAlerts()
        {
            try
            {
                _logger.LogInformation("[ALERTS-WIDGET] Clearing all alerts");
                
                RecentAlerts.Clear();
                
                _logger.LogInformation("[ALERTS-WIDGET] All alerts cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ALERTS-WIDGET] Error clearing all alerts");
            }
        }
        
        private void UpdateAlertCounts()
        {
            IsEmpty = RecentAlerts.Count == 0;
            HasUnacknowledgedAlerts = RecentAlerts.Any(a => !a.IsAcknowledged);
            OnPropertyChanged(nameof(UnacknowledgedAlertsCount));
            
            // Update command states
            AcknowledgeAllCommand.RaiseCanExecuteChanged();
            ClearAlertsCommand.RaiseCanExecuteChanged();
        }
    }
    
    public class SystemAlertViewModel
    {
        public string Id { get; set; } = string.Empty;
        public AlertLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public bool IsAcknowledged { get; set; }
        public string Details { get; set; } = string.Empty;
    }
} 