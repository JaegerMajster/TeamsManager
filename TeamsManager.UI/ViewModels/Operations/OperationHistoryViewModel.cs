using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.ViewModels.Operations
{
    /// <summary>
    /// ViewModel dla widoku historii operacji
    /// </summary>
    public class OperationHistoryViewModel : INotifyPropertyChanged
    {
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly ILogger<OperationHistoryViewModel> _logger;
        private CancellationTokenSource? _cancellationTokenSource;

        // Collections
        private ObservableCollection<OperationHistoryItemViewModel> _operations = new();
        private ObservableCollection<OperationHistoryItemViewModel> _filteredOperations = new();
        private List<OperationHistory> _allOperations = new();

        // Selected items
        private OperationHistoryItemViewModel? _selectedOperation;

        // Filter properties
        private DateTime? _startDate;
        private DateTime? _endDate;
        private string? _selectedOperationType;
        private string? _selectedStatus;
        private string _userFilter = string.Empty;
        private string _searchText = string.Empty;

        // Pagination
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private int _totalOperations;

        // Statistics
        private int _totalOperationsCount;
        private double _successRate;
        private TimeSpan _averageExecutionTime;
        private int _completedOperations;
        private int _failedOperations;
        private int _inProgressOperations;

        // Loading states
        private bool _isLoading;
        private bool _isLoadingDetails;
        private bool _isExporting;
        private string? _errorMessage;

        // Available filter options
        private ObservableCollection<string> _availableOperationTypes = new();
        private ObservableCollection<string> _availableStatuses = new();

        public OperationHistoryViewModel(
            IOperationHistoryService operationHistoryService,
            ILogger<OperationHistoryViewModel> logger)
        {
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeCommands();
            InitializeFilterOptions();
            LoadMockData(); // Temporary for development
        }

        #region Properties

        public ObservableCollection<OperationHistoryItemViewModel> Operations
        {
            get => _operations;
            set
            {
                _operations = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<OperationHistoryItemViewModel> FilteredOperations
        {
            get => _filteredOperations;
            set
            {
                _filteredOperations = value;
                OnPropertyChanged();
            }
        }

        public OperationHistoryItemViewModel? SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                _selectedOperation = value;
                OnPropertyChanged();
                ShowDetailsCommand.RaiseCanExecuteChanged();
            }
        }

        // Filter Properties
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string? SelectedOperationType
        {
            get => _selectedOperationType;
            set
            {
                _selectedOperationType = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string? SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string UserFilter
        {
            get => _userFilter;
            set
            {
                _userFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // Pagination Properties
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                ApplyPagination();
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                _totalPages = value;
                OnPropertyChanged();
            }
        }

        public int TotalOperations
        {
            get => _totalOperations;
            private set
            {
                _totalOperations = value;
                OnPropertyChanged();
            }
        }

        // Statistics Properties
        public int TotalOperationsCount
        {
            get => _totalOperationsCount;
            private set
            {
                _totalOperationsCount = value;
                OnPropertyChanged();
            }
        }

        public double SuccessRate
        {
            get => _successRate;
            private set
            {
                _successRate = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan AverageExecutionTime
        {
            get => _averageExecutionTime;
            private set
            {
                _averageExecutionTime = value;
                OnPropertyChanged();
            }
        }

        public int CompletedOperations
        {
            get => _completedOperations;
            private set
            {
                _completedOperations = value;
                OnPropertyChanged();
            }
        }

        public int FailedOperations
        {
            get => _failedOperations;
            private set
            {
                _failedOperations = value;
                OnPropertyChanged();
            }
        }

        public int InProgressOperations
        {
            get => _inProgressOperations;
            private set
            {
                _inProgressOperations = value;
                OnPropertyChanged();
            }
        }

        // Loading States
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoadingDetails
        {
            get => _isLoadingDetails;
            set
            {
                _isLoadingDetails = value;
                OnPropertyChanged();
            }
        }

        public bool IsExporting
        {
            get => _isExporting;
            set
            {
                _isExporting = value;
                OnPropertyChanged();
                ExportToExcelCommand.RaiseCanExecuteChanged();
                ExportToPdfCommand.RaiseCanExecuteChanged();
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        // Filter Options
        public ObservableCollection<string> AvailableOperationTypes
        {
            get => _availableOperationTypes;
            set
            {
                _availableOperationTypes = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableStatuses
        {
            get => _availableStatuses;
            set
            {
                _availableStatuses = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public RelayCommand RefreshCommand { get; private set; } = null!;
        public RelayCommand<OperationHistoryItemViewModel?> ShowDetailsCommand { get; private set; } = null!;
        public RelayCommand ExportToExcelCommand { get; private set; } = null!;
        public RelayCommand ExportToPdfCommand { get; private set; } = null!;
        public RelayCommand ClearFiltersCommand { get; private set; } = null!;
        public RelayCommand PreviousPageCommand { get; private set; } = null!;
        public RelayCommand NextPageCommand { get; private set; } = null!;

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            RefreshCommand = new RelayCommand(async () => await LoadOperationsAsync(), () => !IsLoading);
            ShowDetailsCommand = new RelayCommand<OperationHistoryItemViewModel?>(ShowOperationDetails, op => op != null);
            ExportToExcelCommand = new RelayCommand(async () => await ExportToExcelAsync(), () => !IsExporting && FilteredOperations.Any());
            ExportToPdfCommand = new RelayCommand(async () => await ExportToPdfAsync(), () => !IsExporting && FilteredOperations.Any());
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            PreviousPageCommand = new RelayCommand(() => CurrentPage--, () => CurrentPage > 1);
            NextPageCommand = new RelayCommand(() => CurrentPage++, () => CurrentPage < TotalPages);
        }

        private void InitializeFilterOptions()
        {
            // Initialize available filter options
            AvailableOperationTypes = new ObservableCollection<string>
            {
                "Wszystkie",
                "TeamCreated", "TeamUpdated", "TeamDeleted",
                "UserCreated", "UserUpdated", "UserDeleted",
                "SystemBackup", "SystemRestore", "DataImport", "DataExport"
            };

            AvailableStatuses = new ObservableCollection<string>
            {
                "Wszystkie",
                "Completed", "Failed", "InProgress", "PartialSuccess"
            };
        }

        private void LoadMockData()
        {
            _logger.LogDebug("Loading mock data for OperationHistory");

            var mockOperations = new List<OperationHistory>
            {
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.Now.AddMinutes(-30),
                    CompletedAt = DateTime.Now.AddMinutes(-29),
                    Type = OperationType.TeamCreated,
                    TargetEntityType = "Team",
                    TargetEntityId = "team-123",
                    TargetEntityName = "Klasa 3A",
                    Status = OperationStatus.Completed,
                    CreatedBy = "admin@example.com",
                    ProcessedItems = 1,
                    TotalItems = 1,
                    OperationDetails = "{\"Name\":\"Klasa 3A\",\"Description\":\"Klasa matematyczno-fizyczna\"}"
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.Now.AddHours(-2),
                    CompletedAt = DateTime.Now.AddHours(-2).AddMinutes(15),
                    Type = OperationType.BulkUserImport,
                    TargetEntityType = "Users",
                    TargetEntityId = "import-456",
                    TargetEntityName = "users_import.csv",
                    Status = OperationStatus.PartialSuccess,
                    CreatedBy = "operator@example.com",
                    ProcessedItems = 85,
                    TotalItems = 100,
                    OperationDetails = "{\"FileName\":\"users_import.csv\",\"TotalRows\":100,\"ProcessedRows\":85,\"ErrorRows\":15}"
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.Now.AddDays(-1),
                    CompletedAt = DateTime.Now.AddDays(-1).AddMinutes(5),
                    Type = OperationType.SystemBackup,
                    TargetEntityType = "Database",
                    TargetEntityName = "System Backup",
                    Status = OperationStatus.Completed,
                    CreatedBy = "system@example.com",
                    ProcessedItems = 1,
                    TotalItems = 1,
                    OperationDetails = "{\"BackupSize\":\"2.5GB\",\"Tables\":45,\"Duration\":\"00:05:23\"}"
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.Now.AddMinutes(-5),
                    Type = OperationType.UserUpdated,
                    TargetEntityType = "User",
                    TargetEntityId = "user-789",
                    TargetEntityName = "Jan Kowalski",
                    Status = OperationStatus.InProgress,
                    CreatedBy = "admin@example.com",
                    ProcessedItems = 0,
                    TotalItems = 1,
                    OperationDetails = "{\"UserId\":\"user-789\",\"Changes\":[\"Email\",\"Department\"]}"
                },
                new()
                {
                    Id = Guid.NewGuid().ToString(),
                    StartedAt = DateTime.Now.AddDays(-2),
                    CompletedAt = DateTime.Now.AddDays(-2).AddMinutes(1),
                    Type = OperationType.TeamDeleted,
                    TargetEntityType = "Team",
                    TargetEntityId = "team-old",
                    TargetEntityName = "Stary Zespół",
                    Status = OperationStatus.Failed,
                    CreatedBy = "manager@example.com",
                    ErrorMessage = "Cannot delete team with active members",
                    ProcessedItems = 0,
                    TotalItems = 1,
                    OperationDetails = "{\"TeamId\":\"team-old\",\"ActiveMembers\":12}"
                }
            };

            _allOperations = mockOperations;
            Operations = new ObservableCollection<OperationHistoryItemViewModel>(
                mockOperations.Select(op => new OperationHistoryItemViewModel(op))
            );

            CalculateStatistics();
            ApplyFilters();
        }

        private async Task LoadOperationsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                _logger.LogDebug("Loading operations from API");

                // TODO: Replace with actual API call
                // var operations = await _operationHistoryService.GetAllAsync(_cancellationTokenSource.Token);
                
                // For now, use mock data
                await Task.Delay(1000, _cancellationTokenSource.Token); // Simulate API call
                LoadMockData();

                _logger.LogDebug($"Loaded {Operations.Count} operations");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Operation loading was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading operations");
                ErrorMessage = $"Błąd podczas ładowania operacji: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = Operations.AsEnumerable();

            // Date range filter
            if (StartDate.HasValue)
            {
                filtered = filtered.Where(op => op.StartTime >= StartDate.Value);
            }

            if (EndDate.HasValue)
            {
                filtered = filtered.Where(op => op.StartTime <= EndDate.Value.Date.AddDays(1));
            }

            // Operation type filter
            if (!string.IsNullOrEmpty(SelectedOperationType) && SelectedOperationType != "Wszystkie")
            {
                filtered = filtered.Where(op => op.OperationType.Equals(SelectedOperationType, StringComparison.OrdinalIgnoreCase));
            }

            // Status filter
            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "Wszystkie")
            {
                filtered = filtered.Where(op => op.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase));
            }

            // User filter
            if (!string.IsNullOrEmpty(UserFilter))
            {
                filtered = filtered.Where(op => op.DisplayUser.Contains(UserFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Search text filter
            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered.Where(op => 
                    op.OperationType.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    op.DisplayTarget.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    op.DisplayUser.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(op.ErrorMessage) && op.ErrorMessage.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            var filteredList = filtered.ToList();
            TotalOperations = filteredList.Count;
            TotalPages = (int)Math.Ceiling((double)TotalOperations / PageSize);
            
            // Reset current page if it's beyond the new total
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = 1;
            }

            ApplyPagination(filteredList);
        }

        private void ApplyPagination(List<OperationHistoryItemViewModel>? filteredList = null)
        {
            var source = filteredList ?? FilteredOperations.ToList();
            var skip = (CurrentPage - 1) * PageSize;
            var pagedResults = source.Skip(skip).Take(PageSize).ToList();

            FilteredOperations = new ObservableCollection<OperationHistoryItemViewModel>(pagedResults);

            // Update command states
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }

        private void CalculateStatistics()
        {
            if (!Operations.Any())
            {
                TotalOperationsCount = 0;
                SuccessRate = 0;
                AverageExecutionTime = TimeSpan.Zero;
                CompletedOperations = 0;
                FailedOperations = 0;
                InProgressOperations = 0;
                return;
            }

            TotalOperationsCount = Operations.Count;
            CompletedOperations = Operations.Count(op => op.IsSuccess);
            FailedOperations = Operations.Count(op => op.IsFailed);
            InProgressOperations = Operations.Count(op => op.IsInProgress);

            SuccessRate = (double)CompletedOperations / TotalOperationsCount * 100;

            var completedOps = Operations.Where(op => op.Duration.HasValue).ToList();
            if (completedOps.Any())
            {
                var totalTicks = completedOps.Sum(op => op.Duration!.Value.Ticks);
                AverageExecutionTime = new TimeSpan(totalTicks / completedOps.Count);
            }
        }

        private void ShowOperationDetails(OperationHistoryItemViewModel? operation)
        {
            if (operation != null)
            {
                // TODO: Show details popup
                _logger.LogDebug($"Showing details for operation {operation.Id}");
            }
        }

        private async Task ExportToExcelAsync()
        {
            try
            {
                IsExporting = true;
                
                // TODO: Implement Excel export
                await Task.Delay(2000); // Simulate export
                
                _logger.LogDebug("Excel export completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to Excel");
                ErrorMessage = $"Błąd eksportu do Excel: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        private async Task ExportToPdfAsync()
        {
            try
            {
                IsExporting = true;
                
                // TODO: Implement PDF export
                await Task.Delay(2000); // Simulate export
                
                _logger.LogDebug("PDF export completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to PDF");
                ErrorMessage = $"Błąd eksportu do PDF: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }

        private void ClearFilters()
        {
            StartDate = null;
            EndDate = null;
            SelectedOperationType = null;
            SelectedStatus = null;
            UserFilter = string.Empty;
            SearchText = string.Empty;
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 