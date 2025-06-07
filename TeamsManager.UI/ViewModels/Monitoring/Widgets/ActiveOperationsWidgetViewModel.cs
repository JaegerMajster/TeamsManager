using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class ActiveOperationsWidgetViewModel : BaseViewModel
    {
        private readonly IMonitoringDataService _dataService;
        private readonly ILogger<ActiveOperationsWidgetViewModel> _logger;
        
        private bool _isLoading;
        private bool _isEmpty;
        
        public ObservableCollection<ActiveOperationViewModel> ActiveOperations { get; }
        
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
        
        public int ActiveOperationsCount => ActiveOperations.Count;
        
        public RelayCommand RefreshCommand { get; }
        public RelayCommand<ActiveOperationViewModel> CancelOperationCommand { get; }
        
        public ActiveOperationsWidgetViewModel(
            IMonitoringDataService dataService,
            ILogger<ActiveOperationsWidgetViewModel> logger)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            ActiveOperations = new ObservableCollection<ActiveOperationViewModel>();
            ActiveOperations.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ActiveOperationsCount));
                IsEmpty = ActiveOperations.Count == 0;
            };
            
            RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsLoading);
            CancelOperationCommand = new RelayCommand<ActiveOperationViewModel>(
                async op => await CancelOperation(op), 
                op => op?.CanCancel == true);
        }
        
        public async Task RefreshAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            try
            {
                _logger.LogDebug("[OPERATIONS-WIDGET] Refreshing active operations");
                
                var operations = await _dataService.GetActiveOperationsAsync();
                
                ActiveOperations.Clear();
                foreach (var operation in operations)
                {
                    ActiveOperations.Add(new ActiveOperationViewModel
                    {
                        Id = operation.Id,
                        Name = operation.Name,
                        Type = operation.Type,
                        Status = operation.Status,
                        Progress = operation.Progress,
                        User = operation.User,
                        StartTime = operation.StartTime,
                        CanCancel = operation.CanCancel
                    });
                }
                
                _logger.LogDebug("[OPERATIONS-WIDGET] Active operations refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OPERATIONS-WIDGET] Error refreshing active operations");
            }
            finally
            {
                IsLoading = false;
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
        
        public void ProcessOperationUpdate(object update)
        {
            try
            {
                _logger.LogDebug("[OPERATIONS-WIDGET] Processing operation update");
                
                // In a real implementation, we would deserialize the update object
                // and update specific operations instead of refreshing all
                _ = Task.Run(async () => await RefreshAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OPERATIONS-WIDGET] Error processing operation update");
            }
        }
        
        private async Task CancelOperation(ActiveOperationViewModel operation)
        {
            if (operation == null) return;
            
            try
            {
                _logger.LogInformation("[OPERATIONS-WIDGET] Cancelling operation {OperationId}", operation.Id);
                
                // In a real implementation, we would call the cancellation service
                // For now, just remove from the list
                ActiveOperations.Remove(operation);
                
                _logger.LogInformation("[OPERATIONS-WIDGET] Operation {OperationId} cancelled", operation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OPERATIONS-WIDGET] Error cancelling operation {OperationId}", operation.Id);
            }
        }
    }
    
    public class ActiveOperationViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public OperationStatus Status { get; set; }
        public double Progress { get; set; }
        public string User { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool CanCancel { get; set; }
    }
} 