using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.ViewModels.Shell;
using TeamsManager.UI.Services;

namespace TeamsManager.UI.ViewModels.Departments
{
    /// <summary>
    /// ViewModel dla zarządzania działami - hierarchiczny widok drzewa działów
    /// </summary>
    public class DepartmentsManagementViewModel : BaseViewModel
    {
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<DepartmentsManagementViewModel> _logger;
        private readonly MainShellViewModel _mainShellViewModel;
        private readonly DepartmentCodeMigrationService _migrationService;
        
        private ObservableCollection<DepartmentTreeItemViewModel> _departments;
        private ICollectionView _departmentsView;
        private DepartmentTreeItemViewModel? _selectedDepartment;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private string? _errorMessage;

        public DepartmentsManagementViewModel(
            IDepartmentService departmentService,
            ILogger<DepartmentsManagementViewModel> logger,
            IUIDialogService uiDialogService,
            MainShellViewModel mainShellViewModel,
            DepartmentCodeMigrationService migrationService)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));
            _mainShellViewModel = mainShellViewModel ?? throw new ArgumentNullException(nameof(mainShellViewModel));
            _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));

            _departments = new ObservableCollection<DepartmentTreeItemViewModel>();
            _departmentsView = CollectionViewSource.GetDefaultView(_departments);
            _departmentsView.Filter = FilterDepartments;

            // Initialize commands
            LoadDepartmentsCommand = new RelayCommand(async () => await LoadDepartmentsAsync(), () => !IsLoading);
            RefreshCommand = new RelayCommand(async () => await LoadDepartmentsAsync(forceRefresh: true), () => !IsLoading);
            AddDepartmentCommand = new RelayCommand(async () => await AddDepartmentAsync(), () => !IsLoading);
            EditDepartmentCommand = new RelayCommand(async () => await EditDepartmentAsync(), () => SelectedDepartment != null && !IsLoading);
            DeleteDepartmentCommand = new RelayCommand(async () => await DeleteDepartmentAsync(), () => SelectedDepartment != null && !IsLoading);
            ViewDepartmentCommand = new RelayCommand(async () => await ViewDepartmentAsync(), () => SelectedDepartment != null && !IsLoading);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            ClearSearchCommand = new RelayCommand(ClearSearch, () => !string.IsNullOrEmpty(SearchText));
            MigrateDepartmentCodesCommand = new RelayCommand(async () => await MigrateDepartmentCodesAsync(), () => !IsLoading);

            // Load initial data
            _ = LoadDepartmentsAsync();
        }

        #region Properties

        public ObservableCollection<DepartmentTreeItemViewModel> Departments
        {
            get => _departments;
            set => SetProperty(ref _departments, value);
        }

        public ICollectionView DepartmentsView
        {
            get => _departmentsView;
            set => SetProperty(ref _departmentsView, value);
        }

        public DepartmentTreeItemViewModel? SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                if (SetProperty(ref _selectedDepartment, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    DepartmentsView.Refresh();
                    UpdateCommandStates();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public int TotalDepartments => CountAllDepartments(_departments);

        public int ActiveDepartments => CountActiveDepartments(_departments);

        #endregion

        #region Commands

        public ICommand LoadDepartmentsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddDepartmentCommand { get; }
        public ICommand EditDepartmentCommand { get; }
        public ICommand DeleteDepartmentCommand { get; }
        public ICommand ViewDepartmentCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand MigrateDepartmentCodesCommand { get; }

        #endregion

        #region Public Methods

        public async Task LoadDepartmentsAsync(bool forceRefresh = false)
        {
            if (IsLoading) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                _logger.LogDebug("Loading departments (forceRefresh: {ForceRefresh})", forceRefresh);

                var departments = await _departmentService.GetAllDepartmentsAsync();
                
                // Build hierarchy
                var rootDepartments = departments.Where(d => d.IsRootDepartment).OrderBy(d => d.SortOrder).ThenBy(d => d.Name);
                
                Departments.Clear();
                foreach (var rootDept in rootDepartments)
                {
                    var treeItem = new DepartmentTreeItemViewModel(rootDept);
                    BuildDepartmentTree(treeItem, departments);
                    Departments.Add(treeItem);
                }

                OnPropertyChanged(nameof(TotalDepartments));
                OnPropertyChanged(nameof(ActiveDepartments));

                _logger.LogDebug("Loaded {Count} root departments", Departments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments");
                ErrorMessage = $"Błąd podczas ładowania działów: {ex.Message}";
                await ShowErrorDialog("Błąd", ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private void BuildDepartmentTree(DepartmentTreeItemViewModel parent, IEnumerable<Department> allDepartments)
        {
            var children = allDepartments
                .Where(d => d.ParentDepartmentId == parent.Department.Id)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Name);

            foreach (var child in children)
            {
                var childItem = new DepartmentTreeItemViewModel(child, parent);
                parent.Children.Add(childItem);
                BuildDepartmentTree(childItem, allDepartments);
            }
        }

        private bool FilterDepartments(object item)
        {
            if (item is not DepartmentTreeItemViewModel dept) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var searchLower = SearchText.ToLower();
            return dept.Name.ToLower().Contains(searchLower) ||
                   dept.Description.ToLower().Contains(searchLower) ||
                   (dept.Code?.ToLower().Contains(searchLower) ?? false) ||
                   HasMatchingChild(dept, searchLower);
        }

        private bool HasMatchingChild(DepartmentTreeItemViewModel parent, string searchLower)
        {
            return parent.Children.Any(child =>
                child.Name.ToLower().Contains(searchLower) ||
                child.Description.ToLower().Contains(searchLower) ||
                (child.Code?.ToLower().Contains(searchLower) ?? false) ||
                HasMatchingChild(child, searchLower));
        }

        private int CountAllDepartments(ObservableCollection<DepartmentTreeItemViewModel> departments)
        {
            int count = departments.Count;
            foreach (var dept in departments)
            {
                count += CountAllDepartments(dept.Children);
            }
            return count;
        }

        private int CountActiveDepartments(ObservableCollection<DepartmentTreeItemViewModel> departments)
        {
            int count = departments.Count(d => d.Department.IsActive);
            foreach (var dept in departments)
            {
                count += CountActiveDepartments(dept.Children);
            }
            return count;
        }

        private async Task AddDepartmentAsync()
        {
            try
            {
                _logger.LogDebug("Adding new department");
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.Departments.DepartmentEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<DepartmentEditViewModel>();
                
                viewModel.InitializeForAdd(SelectedDepartment?.Department.Id);
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    // Odśwież listę działów po dodaniu
                    await LoadDepartmentsAsync(forceRefresh: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding department");
                await ShowErrorDialog("Błąd", $"Błąd podczas dodawania działu: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task EditDepartmentAsync()
        {
            if (SelectedDepartment == null) return;

            try
            {
                _logger.LogDebug("Editing department {DepartmentId}", SelectedDepartment.Id);
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.Departments.DepartmentEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<DepartmentEditViewModel>();
                
                await viewModel.InitializeForEditAsync(SelectedDepartment.Department.Id);
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    // Odśwież listę działów po edycji
                    await LoadDepartmentsAsync(forceRefresh: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing department");
                await ShowErrorDialog("Błąd", $"Błąd podczas edycji działu: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task ViewDepartmentAsync()
        {
            if (SelectedDepartment == null) return;

            try
            {
                _logger.LogDebug("Viewing department {DepartmentId}", SelectedDepartment.Id);
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.Departments.DepartmentEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<DepartmentEditViewModel>();
                
                await viewModel.InitializeForViewAsync(SelectedDepartment.Department.Id);
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing department");
                await ShowErrorDialog("Błąd", $"Błąd podczas wyświetlania działu: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task DeleteDepartmentAsync()
        {
            if (SelectedDepartment == null) return;

            try
            {
                _logger.LogDebug("Deleting department {DepartmentId}", SelectedDepartment.Id);
                
                var hasChildren = SelectedDepartment.Children.Any();
                var message = hasChildren 
                    ? $"Czy na pewno chcesz usunąć dział '{SelectedDepartment.Name}' wraz z wszystkimi poddziałami?"
                    : $"Czy na pewno chcesz usunąć dział '{SelectedDepartment.Name}'?";

                var confirmed = UIDialogService != null ? await UIDialogService.ShowConfirmationDialog("Potwierdź usunięcie", message) : false;
                
                if (confirmed)
                {
                    await ShowInfoDialog("Usuń dział", "Funkcja usuwania działów zostanie wkrótce zaimplementowana.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                await ShowErrorDialog("Błąd", $"Błąd podczas usuwania działu: {ex.Message}");
            }
        }

        private void ExpandAll()
        {
            SetExpansionState(_departments, true);
        }

        private void CollapseAll()
        {
            SetExpansionState(_departments, false);
        }

        private void SetExpansionState(ObservableCollection<DepartmentTreeItemViewModel> departments, bool isExpanded)
        {
            foreach (var dept in departments)
            {
                dept.IsExpanded = isExpanded;
                SetExpansionState(dept.Children, isExpanded);
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        private void UpdateCommandStates()
        {
            (LoadDepartmentsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddDepartmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditDepartmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteDepartmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ViewDepartmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MigrateDepartmentCodesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task MigrateDepartmentCodesAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie migracji kodów działów...");
                
                // Pokaż potwierdzenie
                var confirmResult = System.Windows.MessageBox.Show(
                    "Czy na pewno chcesz zaktualizować kody wszystkich działów zgodnie z nowym schematem?\n\n" +
                    "Ta operacja:\n" +
                    "• Zaktualizuje kody działów według formuły: NazwaDzialuGlownego-NazwaDzialu1Poziomu-...-NazwaDzialu\n" +
                    "• Usunie polskie znaki i spacje z kodów\n" +
                    "• Może zająć kilka sekund\n\n" +
                    "Operacja jest bezpieczna - można ją cofnąć ręcznie edytując działy.",
                    "Migracja kodów działów",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (confirmResult != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                IsLoading = true;
                ErrorMessage = null;

                // Wykonaj migrację
                var result = await _migrationService.MigrateDepartmentCodesAsync();

                // Pokaż wyniki
                var resultMessage = result.GetSummary();
                
                if (result.IsSuccess)
                {
                    _logger.LogInformation("Migracja kodów działów zakończona sukcesem. Zaktualizowano: {Updated}, Pominięto: {Skipped}", 
                        result.UpdatedDepartments, result.SkippedDepartments);
                    
                    System.Windows.MessageBox.Show(
                        resultMessage,
                        "Migracja zakończona pomyślnie",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    
                    // Odśwież listę działów
                    await LoadDepartmentsAsync(forceRefresh: true);
                }
                else
                {
                    _logger.LogWarning("Migracja kodów działów zakończona z błędami: {Errors}", result.ErroredDepartments);
                    
                    System.Windows.MessageBox.Show(
                        resultMessage,
                        "Migracja zakończona z błędami",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas migracji kodów działów");
                ErrorMessage = $"Błąd podczas migracji kodów działów: {ex.Message}";
                await ShowErrorDialog("Błąd migracji", ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
} 