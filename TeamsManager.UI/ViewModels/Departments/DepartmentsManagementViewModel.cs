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
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                _logger.LogDebug("Loading departments (forceRefresh: {ForceRefresh})", forceRefresh);

                // Zapisz ID zaznaczonego działu przed odświeżeniem
                string? selectedDepartmentId = SelectedDepartment?.Id;

                var departments = await _departmentService.GetAllDepartmentsAsync(forceRefresh: forceRefresh);
                
                // Build hierarchy
                var rootDepartments = departments.Where(d => d.IsRootDepartment).OrderBy(d => d.SortOrder).ThenBy(d => d.Name);
                
                _departments.Clear();
                
                foreach (var rootDept in rootDepartments)
                {
                    var rootItem = new DepartmentTreeItemViewModel(rootDept);
                    BuildDepartmentTree(rootItem, departments);
                    _departments.Add(rootItem);
                }

                // Przywróć zaznaczony element, rozwiń ścieżkę do niego i ustaw wizualne zaznaczenie
                if (!string.IsNullOrEmpty(selectedDepartmentId))
                {
                    var selectedItem = FindDepartmentById(selectedDepartmentId);
                    if (selectedItem != null)
                    {
                        ExpandPathToItem(selectedItem);
                        SelectedDepartment = selectedItem;
                        UpdateTreeViewSelection(selectedItem);
                        _logger.LogDebug("Restored selected department: {DepartmentName}", selectedItem.Name);
                    }
                }

                // Refresh the view
                _departmentsView?.Refresh();
                
                // Odśwież właściwości liczników
                OnPropertyChanged(nameof(TotalDepartments));
                OnPropertyChanged(nameof(ActiveDepartments));
                
                _logger.LogInformation("Loaded {Count} departments", _departments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading departments");
                ErrorMessage = $"Błąd podczas ładowania działów: {ex.Message}";
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
                    // Pobierz ID nowo utworzonego działu
                    string? newDepartmentId = viewModel.CreatedDepartmentId;
                    
                    // Odśwież listę działów po dodaniu
                    await LoadDepartmentsAsync(forceRefresh: true);
                    
                    // Automatycznie zaznacz nowo utworzony dział
                    if (!string.IsNullOrEmpty(newDepartmentId))
                    {
                        var newDepartment = FindDepartmentById(newDepartmentId);
                        if (newDepartment != null)
                        {
                            SelectedDepartment = newDepartment;
                            ExpandPathToItem(newDepartment);
                            UpdateTreeViewSelection(newDepartment);
                            _logger.LogDebug("Automatically selected newly created department: {DepartmentId}", newDepartmentId);
                        }
                    }
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
                    ? $"Nie można usunąć działu '{SelectedDepartment.Name}', ponieważ ma poddziały.\n\nNajpierw usuń wszystkie poddziały."
                    : $"Czy na pewno chcesz usunąć dział '{SelectedDepartment.Name}'?\n\nTa operacja oznacza dział jako nieaktywny (logiczne usunięcie).";

                if (hasChildren)
                {
                    await ShowWarningDialog("Nie można usunąć działu", message);
                    return;
                }

                var confirmed = UIDialogService != null ? await UIDialogService.ShowConfirmationDialog("Potwierdź usunięcie", message) : false;
                
                if (confirmed)
                {
                    IsLoading = true;
                    ErrorMessage = null;

                    var success = await _departmentService.DeleteDepartmentAsync(SelectedDepartment.Department.Id);
                    
                    if (success)
                    {
                        _logger.LogInformation("Dział '{DepartmentName}' został pomyślnie usunięty", SelectedDepartment.Name);
                        await ShowSuccessDialog("Sukces", $"Dział '{SelectedDepartment.Name}' został pomyślnie usunięty.");
                        
                        // Odśwież listę działów
                        await LoadDepartmentsAsync(forceRefresh: true);
                        
                        // Wyczyść zaznaczenie
                        SelectedDepartment = null;
                    }
                    else
                    {
                        await ShowErrorDialog("Błąd", "Nie udało się usunąć działu. Sprawdź logi aplikacji.");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot delete department due to validation rules");
                await ShowWarningDialog("Nie można usunąć działu", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                ErrorMessage = $"Błąd podczas usuwania działu: {ex.Message}";
                await ShowErrorDialog("Błąd", ErrorMessage);
            }
            finally
            {
                IsLoading = false;
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

        private DepartmentTreeItemViewModel? FindDepartmentById(string id)
        {
            foreach (var dept in _departments)
            {
                if (dept.Id == id)
                {
                    return dept;
                }
                var found = FindDepartmentById(id, dept.Children);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private DepartmentTreeItemViewModel? FindDepartmentById(string id, ObservableCollection<DepartmentTreeItemViewModel> children)
        {
            foreach (var dept in children)
            {
                if (dept.Id == id)
                {
                    return dept;
                }
                var found = FindDepartmentById(id, dept.Children);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
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

        /// <summary>
        /// Rozwija ścieżkę do podanego elementu (wszystkich rodziców)
        /// </summary>
        private void ExpandPathToItem(DepartmentTreeItemViewModel item)
        {
            var current = item.Parent;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Aktualizuje wizualne zaznaczenie w TreeView - ustawia IsSelected na odpowiednich elementach
        /// </summary>
        private void UpdateTreeViewSelection(DepartmentTreeItemViewModel selectedItem)
        {
            // Najpierw wyczyść wszystkie zaznaczenia
            ClearAllSelections(_departments);
            
            // Następnie ustaw zaznaczenie na wybranym elemencie
            selectedItem.IsSelected = true;
        }

        /// <summary>
        /// Rekurencyjnie czyści wszystkie zaznaczenia w drzewie
        /// </summary>
        private void ClearAllSelections(ObservableCollection<DepartmentTreeItemViewModel> departments)
        {
            foreach (var dept in departments)
            {
                dept.IsSelected = false;
                ClearAllSelections(dept.Children);
            }
        }

        #endregion
    }
} 