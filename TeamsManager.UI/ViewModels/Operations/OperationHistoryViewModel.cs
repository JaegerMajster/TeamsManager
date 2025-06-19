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
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Models;
using TeamsManager.Core.Extensions;

namespace TeamsManager.UI.ViewModels.Operations
{
    /// <summary>
    /// ViewModel dla widoku historii operacji
    /// </summary>
    public class OperationHistoryViewModel : INotifyPropertyChanged
    {
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly IUserService _userService;
        private readonly ILogger<OperationHistoryViewModel> _logger;
        private readonly IUIDialogService _uiDialogService;
        private CancellationTokenSource? _cancellationTokenSource;

        // Słowniki mapowania dla polskich nazw
        private static readonly Dictionary<string, string> EntityTypeMapping = new()
        {
            { "Wszystkie", "All" },
            { "Zespoły", "Team" },
            { "Użytkownicy", "User" },
            { "Działy", "Department" },
            { "Jednostki organizacyjne", "Generic" },
            { "Typy szkół", "SchoolType" },
            { "Przedmioty", "Subject" },
            { "Szablony zespołów", "TeamTemplate" },
            { "Operacje wsadowe", "Bulk" },
            { "System", "System" }
        };

        // Generujemy mapowanie dynamicznie z centralnych rozszerzeń
        private static readonly Dictionary<string, string> OperationMapping = 
            GenerateOperationMapping();

        // Generujemy mapowanie statusów dynamicznie z centralnych rozszerzeń
        private static readonly Dictionary<string, string> StatusMapping = 
            GenerateStatusMapping();

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
        private string? _selectedEntityType;
        private string? _selectedOperationFilter;

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
        private ObservableCollection<string> _availableEntityTypes = new();
        private ObservableCollection<string> _availableOperations = new();

        public OperationHistoryViewModel(
            IOperationHistoryService operationHistoryService,
            IUserService userService,
            ILogger<OperationHistoryViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _uiDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            InitializeCommands();
            InitializeFilterOptions();
            UpdateAvailableOperations();
            
            // Załaduj dane przy inicjalizacji
            _ = LoadOperationsAsync();
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

        public string? SelectedEntityType
        {
            get => _selectedEntityType;
            set
            {
                _selectedEntityType = value;
                OnPropertyChanged();
                UpdateAvailableOperations();
                ApplyFilters();
            }
        }

        public string? SelectedOperationFilter
        {
            get => _selectedOperationFilter;
            set
            {
                _selectedOperationFilter = value;
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

        public ObservableCollection<string> AvailableEntityTypes
        {
            get => _availableEntityTypes;
            set
            {
                _availableEntityTypes = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AvailableOperations
        {
            get => _availableOperations;
            set
            {
                _availableOperations = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Czy brak danych do wyświetlenia
        /// </summary>
        public bool HasNoData => !IsLoading && !FilteredOperations.Any();

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

        /// <summary>
        /// Generuje mapowanie operacji z centralnych rozszerzeń
        /// </summary>
        private static Dictionary<string, string> GenerateOperationMapping()
        {
            var mapping = new Dictionary<string, string> { { "Wszystkie", "All" } };
            
            foreach (OperationType operationType in Enum.GetValues<OperationType>())
            {
                var polishName = operationType.ToPolishString();
                var englishName = operationType.ToString();
                mapping[polishName] = englishName;
            }
            
            return mapping;
        }

        /// <summary>
        /// Generuje mapowanie statusów z centralnych rozszerzeń
        /// </summary>
        private static Dictionary<string, string> GenerateStatusMapping()
        {
            var mapping = new Dictionary<string, string> { { "Wszystkie", "All" } };
            
            foreach (OperationStatus status in Enum.GetValues<OperationStatus>())
            {
                var polishName = status.ToPolishString();
                var englishName = status.ToString();
                mapping[polishName] = englishName;
            }
            
            return mapping;
        }

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
            // Initialize available filter options using Polish names
            AvailableOperationTypes = new ObservableCollection<string>(OperationMapping.Keys);

            AvailableStatuses = new ObservableCollection<string>(StatusMapping.Keys);

            AvailableEntityTypes = new ObservableCollection<string>(EntityTypeMapping.Keys);

            AvailableOperations = new ObservableCollection<string> { "Wszystkie" };
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

                // Pobierz operacje z serwisu
                var operations = await _operationHistoryService.GetHistoryByFilterAsync(
                    startDate: null,
                    endDate: null,
                    operationType: null,
                    operationStatus: null,
                    createdBy: null,
                    page: 1,
                    pageSize: 1000 // Pobierz więcej rekordów na początku
                );

                _allOperations = operations.ToList();
                
                // Pobierz DisplayName dla użytkowników
                var userDisplayNames = await GetUserDisplayNamesAsync(_allOperations);
                
                Operations = new ObservableCollection<OperationHistoryItemViewModel>(
                    _allOperations.Select(op => new OperationHistoryItemViewModel(op, userDisplayNames.GetValueOrDefault(op.CreatedBy)))
                );

                CalculateStatistics();
                ApplyFilters();

                _logger.LogDebug($"Loaded {Operations.Count} operations from database");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Operation loading was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading operations");
                ErrorMessage = $"Błąd podczas ładowania operacji: {ex.Message}";
                
                // W przypadku błędu, wyczyść dane
                Operations.Clear();
                FilteredOperations.Clear();
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoData));
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

            // Entity type filter (nowy system)
            if (!string.IsNullOrEmpty(SelectedEntityType) && SelectedEntityType != "Wszystkie")
            {
                var englishEntityType = EntityTypeMapping.GetValueOrDefault(SelectedEntityType, SelectedEntityType);
                filtered = filtered.Where(op => GetEntityTypeFromOperationType(op.OperationType) == englishEntityType);
            }

            // Operation filter (nowy system)
            if (!string.IsNullOrEmpty(SelectedOperationFilter) && SelectedOperationFilter != "Wszystkie")
            {
                var englishOperation = OperationMapping.GetValueOrDefault(SelectedOperationFilter, SelectedOperationFilter);
                filtered = filtered.Where(op => op.OperationType.Equals(englishOperation, StringComparison.OrdinalIgnoreCase));
            }

            // Operation type filter (stary system - zachowany dla kompatybilności)
            if (!string.IsNullOrEmpty(SelectedOperationType) && SelectedOperationType != "Wszystkie")
            {
                var englishOperation = OperationMapping.GetValueOrDefault(SelectedOperationType, SelectedOperationType);
                filtered = filtered.Where(op => op.OperationType.Equals(englishOperation, StringComparison.OrdinalIgnoreCase));
            }

            // Status filter
            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "Wszystkie")
            {
                var englishStatus = StatusMapping.GetValueOrDefault(SelectedStatus, SelectedStatus);
                filtered = filtered.Where(op => op.Status.Equals(englishStatus, StringComparison.OrdinalIgnoreCase));
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

        /// <summary>
        /// Mapuje typ operacji na typ encji
        /// </summary>
        private string GetEntityTypeFromOperationType(string operationType)
        {
            return operationType switch
            {
                var op when op.StartsWith("Team") => "Team",
                var op when op.StartsWith("User") => "User", 
                var op when op.StartsWith("Department") => "Department",
                var op when op.StartsWith("Generic") => "Generic",
                var op when op.StartsWith("SchoolType") => "SchoolType",
                var op when op.StartsWith("Subject") => "Subject",
                var op when op.StartsWith("TeamTemplate") => "TeamTemplate",
                var op when op.StartsWith("Bulk") => "Bulk",
                var op when op.StartsWith("System") => "System",
                _ => "System"
            };
        }

        /// <summary>
        /// Aktualizuje dostępne operacje w zależności od wybranego typu encji
        /// </summary>
        private void UpdateAvailableOperations()
        {
            var operations = new List<string> { "Wszystkie" };

            if (string.IsNullOrEmpty(SelectedEntityType) || SelectedEntityType == "Wszystkie")
            {
                // Pokaż wszystkie operacje (polskie nazwy)
                operations.AddRange(OperationMapping.Keys.Where(k => k != "Wszystkie"));
            }
            else
            {
                // Pokaż operacje dla wybranego typu encji (polskie nazwy z centralnych rozszerzeń)
                var operationTypes = SelectedEntityType switch
                {
                    "Zespoły" => new[] { OperationType.TeamCreated, OperationType.TeamUpdated, OperationType.TeamDeleted, OperationType.TeamArchived, OperationType.TeamUnarchived },
                    "Użytkownicy" => new[] { OperationType.UserCreated, OperationType.UserUpdated, OperationType.UserDeactivated, OperationType.UserActivated },
                    "Działy" => new[] { OperationType.DepartmentCreated, OperationType.DepartmentUpdated, OperationType.DepartmentDeleted },
                    "Jednostki organizacyjne" => new[] { OperationType.GenericCreated, OperationType.GenericUpdated, OperationType.GenericDeleted },
                    "Typy szkół" => new[] { OperationType.SchoolTypeCreated, OperationType.SchoolTypeUpdated, OperationType.SchoolTypeDeleted },
                    "Przedmioty" => new[] { OperationType.SubjectCreated, OperationType.SubjectUpdated, OperationType.SubjectDeleted },
                    "Szablony zespołów" => new[] { OperationType.TeamTemplateCreated, OperationType.TeamTemplateUpdated, OperationType.TeamTemplateDeleted },
                    "Operacje wsadowe" => new[] { OperationType.BulkUserImport, OperationType.BulkTeamCreation, OperationType.BulkArchiving },
                    "System" => new[] { OperationType.SystemBackup, OperationType.SystemRestore },
                    _ => Array.Empty<OperationType>()
                };
                
                var polishOperations = operationTypes.Select(op => op.ToPolishString()).ToArray();
                
                operations.AddRange(polishOperations);
            }

            AvailableOperations = new ObservableCollection<string>(operations);
            
            // Resetuj wybór operacji jeśli nie jest już dostępna
            if (!string.IsNullOrEmpty(SelectedOperationFilter) && !operations.Contains(SelectedOperationFilter))
            {
                SelectedOperationFilter = null;
            }
        }

        private void ApplyPagination(List<OperationHistoryItemViewModel>? filteredList = null)
        {
            var source = filteredList ?? FilteredOperations.ToList();
            var skip = (CurrentPage - 1) * PageSize;
            var pagedResults = source.Skip(skip).Take(PageSize).ToList();

            FilteredOperations = new ObservableCollection<OperationHistoryItemViewModel>(pagedResults);

                            // Aktualizuj stany komend
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
            
            // Notify about HasNoData change
            OnPropertyChanged(nameof(HasNoData));
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

        private async void ShowOperationDetails(OperationHistoryItemViewModel? operation)
        {
            if (operation == null) return;

            try
            {
                _logger.LogDebug("Showing details for operation {OperationId}", operation.Id);

                // Przygotuj szczegółowe informacje o operacji
                var details = BuildOperationDetailsText(operation);

                // Wyświetl dialog informacyjny z szczegółami
                await _uiDialogService.ShowInformationAsync(
                    title: "Szczegóły operacji",
                    message: $"Typ: {operation.PolishOperationType}\nCel: {operation.DisplayTarget}\nStatus: {operation.PolishStatus}",
                    details: details
                );

                _logger.LogDebug("Showed details for operation {OperationId}", operation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing operation details for {OperationId}", operation.Id);
                
                await _uiDialogService.ShowErrorAsync(
                    "Błąd", 
                    "Nie udało się wyświetlić szczegółów operacji.",
                    ex.Message
                );
            }
        }

        /// <summary>
        /// Buduje tekst ze szczegółami operacji
        /// </summary>
        private string BuildOperationDetailsText(OperationHistoryItemViewModel operation)
        {
            var details = new System.Text.StringBuilder();

            // Podstawowe informacje
            details.AppendLine($"ID: {operation.Id}");
            details.AppendLine($"Typ: {operation.PolishOperationType}");
            details.AppendLine($"Status: {operation.PolishStatus}");
            details.AppendLine($"Rozpoczęto: {operation.FormattedStartTime}");
            details.AppendLine($"Ukończono: {operation.FormattedEndTime}");
            details.AppendLine($"Czas trwania: {operation.FormattedDuration}");
            details.AppendLine($"Utworzył: {operation.DisplayUser}");
            details.AppendLine();

            // Informacje o postępie (jeśli dostępne)
            if (operation.HasProgress)
            {
                details.AppendLine("=== POSTĘP OPERACJI ===");
                details.AppendLine($"Razem elementów: {operation.TotalItems}");
                details.AppendLine($"Przetworzone: {operation.ProcessedItems}");
                details.AppendLine($"Postęp: {operation.ProgressPercentage:F1}%");
                details.AppendLine();
            }

            // Informacje o błędzie (jeśli wystąpił)
            if (operation.HasError)
            {
                details.AppendLine("=== BŁĄD ===");
                details.AppendLine(operation.ErrorMessage);
                details.AppendLine();
            }

            // Szczegóły operacji (jeśli dostępne)
            if (operation.HasDetails)
            {
                details.AppendLine("=== SZCZEGÓŁY OPERACJI ===");
                details.AppendLine(operation.OperationDetails);
            }

            return details.ToString();
        }

        private async Task ExportToExcelAsync()
        {
            try
            {
                IsExporting = true;
                
    
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

        /// <summary>
        /// Pobiera DisplayName dla użytkowników z operacji
        /// </summary>
        private async Task<Dictionary<string, string>> GetUserDisplayNamesAsync(List<OperationHistory> operations)
        {
            var userDisplayNames = new Dictionary<string, string>();
            
            try
            {
                // Pobierz unikalne UPN użytkowników
                var userUpns = operations
                    .Where(op => !string.IsNullOrEmpty(op.CreatedBy) && op.CreatedBy != "system")
                    .Select(op => op.CreatedBy!)
                    .Distinct()
                    .ToList();

                if (!userUpns.Any())
                {
                    _logger.LogDebug("Brak UPN użytkowników do pobrania DisplayName");
                    return userDisplayNames;
                }

                _logger.LogDebug("Pobieranie DisplayName dla {Count} użytkowników: {Users}", userUpns.Count, string.Join(", ", userUpns));

                // Pobierz użytkowników z bazy danych
                foreach (var upn in userUpns)
                {
                    try
                    {
                        _logger.LogDebug("Pobieranie użytkownika dla UPN: {UPN}", upn);
                        var user = await _userService.GetUserByUpnAsync(upn);
                        if (user != null)
                        {
                            _logger.LogDebug("Znaleziono użytkownika {UPN}: DisplayName='{DisplayName}', FirstName='{FirstName}', LastName='{LastName}'", 
                                upn, user.DisplayName, user.FirstName, user.LastName);
                            
                            if (!string.IsNullOrEmpty(user.DisplayName))
                            {
                                userDisplayNames[upn] = user.DisplayName;
                                _logger.LogDebug("Dodano DisplayName dla {UPN}: '{DisplayName}'", upn, user.DisplayName);
                            }
                            else
                            {
                                _logger.LogWarning("Użytkownik {UPN} nie ma ustawionego DisplayName", upn);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Nie znaleziono użytkownika dla UPN: {UPN}", upn);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Nie udało się pobrać użytkownika {UPN}", upn);
                    }
                }

                _logger.LogDebug("Pobrano DisplayName dla {Count} z {Total} użytkowników. Mapowanie: {Mapping}", 
                    userDisplayNames.Count, userUpns.Count, 
                    string.Join(", ", userDisplayNames.Select(kvp => $"{kvp.Key}='{kvp.Value}'")));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania DisplayName użytkowników");
            }

            return userDisplayNames;
        }

        private void ClearFilters()
        {
            StartDate = null;
            EndDate = null;
            SelectedOperationType = null;
            SelectedStatus = null;
            UserFilter = string.Empty;
            SearchText = string.Empty;
            SelectedEntityType = null;
            SelectedOperationFilter = null;
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 