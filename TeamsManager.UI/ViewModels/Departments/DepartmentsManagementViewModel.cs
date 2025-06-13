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
using TeamsManager.UI.ViewModels.OrganizationalUnits;

namespace TeamsManager.UI.ViewModels.Departments
{
    /// <summary>
    /// ViewModel dla zarządzania działami - hierarchiczny widok drzewa działów
    /// </summary>
    public class DepartmentsManagementViewModel : BaseViewModel
    {
        private readonly IDepartmentService _departmentService;
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly ILogger<DepartmentsManagementViewModel> _logger;
        private readonly MainShellViewModel _mainShellViewModel;
        private readonly DepartmentCodeMigrationService _migrationService;
        
        private ObservableCollection<OrganizationalUnitTreeItemViewModel> _organizationalUnits;
        private ICollectionView _organizationalUnitsView;
        private OrganizationalUnitTreeItemViewModel? _selectedItem;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private string? _errorMessage;

        public DepartmentsManagementViewModel(
            IDepartmentService departmentService,
            IOrganizationalUnitService organizationalUnitService,
            ILogger<DepartmentsManagementViewModel> logger,
            IUIDialogService uiDialogService,
            MainShellViewModel mainShellViewModel,
            DepartmentCodeMigrationService migrationService)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _organizationalUnitService = organizationalUnitService ?? throw new ArgumentNullException(nameof(organizationalUnitService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));
            _mainShellViewModel = mainShellViewModel ?? throw new ArgumentNullException(nameof(mainShellViewModel));
            _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));

            _organizationalUnits = new ObservableCollection<OrganizationalUnitTreeItemViewModel>();
            _organizationalUnitsView = CollectionViewSource.GetDefaultView(_organizationalUnits);
            _organizationalUnitsView.Filter = FilterOrganizationalUnits;

            // Initialize commands
            LoadDepartmentsCommand = new RelayCommand(async () => await LoadDepartmentsAsync(), () => !IsLoading);
            RefreshCommand = new RelayCommand(async () => await LoadDepartmentsAsync(forceRefresh: true), () => !IsLoading);
            AddDepartmentCommand = new RelayCommand(async () => await AddDepartmentAsync(), () => !IsLoading);
            EditDepartmentCommand = new RelayCommand(async () => await EditDepartmentAsync(), () => SelectedItem?.IsDepartment == true && !IsLoading);
            DeleteDepartmentCommand = new RelayCommand(async () => await DeleteDepartmentAsync(), () => SelectedItem?.IsDepartment == true && !IsLoading);
            ViewDepartmentCommand = new RelayCommand(async () => await ViewDepartmentAsync(), () => SelectedItem?.IsDepartment == true && !IsLoading);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            ClearSearchCommand = new RelayCommand(ClearSearch, () => !string.IsNullOrEmpty(SearchText));
            MigrateDepartmentCodesCommand = new RelayCommand(async () => await MigrateDepartmentCodesAsync(), () => !IsLoading);

            // Load initial data
            _ = LoadDepartmentsAsync();
        }

        #region Properties

        public ObservableCollection<OrganizationalUnitTreeItemViewModel> OrganizationalUnits
        {
            get => _organizationalUnits;
            set => SetProperty(ref _organizationalUnits, value);
        }

        public ICollectionView OrganizationalUnitsView
        {
            get => _organizationalUnitsView;
            set => SetProperty(ref _organizationalUnitsView, value);
        }

        public OrganizationalUnitTreeItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        // Kompatybilność z istniejącym kodem
        public DepartmentTreeItemViewModel? SelectedDepartment
        {
            get => SelectedItem?.IsDepartment == true ? 
                new DepartmentTreeItemViewModel(SelectedItem.Department!) : 
                null;
            set
            {
                // Znajdź odpowiadający OrganizationalUnitTreeItemViewModel
                if (value != null)
                {
                    var item = FindItemById(value.Id);
                    SelectedItem = item;
                }
                else
                {
                    SelectedItem = null;
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
                    OrganizationalUnitsView.Refresh();
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

        public int TotalDepartments => CountAllDepartments(_organizationalUnits);

        public int ActiveDepartments => CountActiveDepartments(_organizationalUnits);

        public int TotalOrganizationalUnits => CountAllOrganizationalUnits(_organizationalUnits);

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
                _logger.LogDebug("Loading organizational units and departments (forceRefresh: {ForceRefresh})", forceRefresh);

                // Zapisz ID zaznaczonego elementu przed odświeżeniem
                string? selectedItemId = SelectedItem?.Id;

                // Pobierz hierarchię jednostek organizacyjnych
                var hierarchy = await _organizationalUnitService.GetOrganizationalUnitsHierarchyAsync();
                
                // Pobierz wszystkie działy z przypisanymi jednostkami
                var allDepartments = await _departmentService.GetAllDepartmentsAsync();

                // Konwertuj na ViewModels
                var unitViewModels = new ObservableCollection<OrganizationalUnitTreeItemViewModel>();
                
                foreach (var rootUnit in hierarchy)
                {
                    var unitViewModel = CreateOrganizationalUnitTreeItem(rootUnit, null);
                    await LoadDepartmentsForUnit(unitViewModel, allDepartments);
                    unitViewModels.Add(unitViewModel);
                }

                _organizationalUnits.Clear();
                foreach (var unit in unitViewModels)
                {
                    _organizationalUnits.Add(unit);
                }

                // Przywróć zaznaczony element
                if (!string.IsNullOrEmpty(selectedItemId))
                {
                    var selectedItem = FindItemById(selectedItemId);
                    if (selectedItem != null)
                    {
                        ExpandPathToItem(selectedItem);
                        SelectedItem = selectedItem;
                        _logger.LogDebug("Restored selected item: {ItemName}", selectedItem.DisplayName);
                    }
                }

                // Refresh the view
                _organizationalUnitsView?.Refresh();
                
                // Odśwież właściwości liczników
                OnPropertyChanged(nameof(TotalDepartments));
                OnPropertyChanged(nameof(ActiveDepartments));
                OnPropertyChanged(nameof(TotalOrganizationalUnits));
                
                _logger.LogInformation("Loaded {Count} organizational units with departments", _organizationalUnits.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading organizational units and departments");
                ErrorMessage = $"Błąd podczas ładowania struktury organizacyjnej: {ex.Message}";
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
            SetExpansionState(_organizationalUnits, true);
        }

        private void CollapseAll()
        {
            SetExpansionState(_organizationalUnits, false);
        }

        private void SetExpansionState(ObservableCollection<OrganizationalUnitTreeItemViewModel> units, bool isExpanded)
        {
            foreach (var unit in units)
            {
                unit.IsExpanded = isExpanded;
                SetExpansionState(unit.Children, isExpanded);
            }
        }

        private DepartmentTreeItemViewModel? FindDepartmentById(string id)
        {
            // Ta metoda nie jest już używana - zastąpiona przez FindItemById
            var item = FindItemById(id);
            return item?.IsDepartment == true ? new DepartmentTreeItemViewModel(item.Department!) : null;
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
            // Ta metoda nie jest już używana - zaznaczenie obsługiwane przez SelectedItem
        }

        /// <summary>
        /// Rekurencyjnie czyści wszystkie zaznaczenia w drzewie
        /// </summary>
        private void ClearAllSelections(ObservableCollection<DepartmentTreeItemViewModel> departments)
        {
            // Ta metoda nie jest już używana
        }

        // Nowe metody dla OrganizationalUnitTreeItemViewModel

        private OrganizationalUnitTreeItemViewModel CreateOrganizationalUnitTreeItem(
            OrganizationalUnit unit, 
            OrganizationalUnitTreeItemViewModel? parent)
        {
            return new OrganizationalUnitTreeItemViewModel(unit, parent);
        }

        private async Task LoadDepartmentsForUnit(
            OrganizationalUnitTreeItemViewModel unitViewModel, 
            IEnumerable<Department> allDepartments)
        {
            // Znajdź działy przypisane do tej jednostki organizacyjnej
            var departmentsForUnit = allDepartments
                .Where(d => d.OrganizationalUnitId == unitViewModel.Id)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Name);

            // Dodaj działy jako dzieci jednostki organizacyjnej
            foreach (var department in departmentsForUnit)
            {
                var departmentViewModel = new OrganizationalUnitTreeItemViewModel(department, unitViewModel);
                unitViewModel.Children.Add(departmentViewModel);
            }

            // Rekurencyjnie załaduj działy dla pod-jednostek
            foreach (var childUnit in unitViewModel.Children.Where(c => !c.IsDepartment))
            {
                await LoadDepartmentsForUnit(childUnit, allDepartments);
            }
        }

        private bool FilterOrganizationalUnits(object item)
        {
            if (item is not OrganizationalUnitTreeItemViewModel unit) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var searchLower = SearchText.ToLowerInvariant();
            return ContainsSearchText(unit, searchLower) || HasMatchingChild(unit, searchLower);
        }

        private bool ContainsSearchText(OrganizationalUnitTreeItemViewModel unit, string searchText)
        {
            return unit.DisplayName.ToLowerInvariant().Contains(searchText) ||
                   (unit.Description?.ToLowerInvariant().Contains(searchText) == true);
        }

        private bool HasMatchingChild(OrganizationalUnitTreeItemViewModel parent, string searchLower)
        {
            return parent.Children.Any(child => 
                ContainsSearchText(child, searchLower) || HasMatchingChild(child, searchLower));
        }

        private int CountAllDepartments(ObservableCollection<OrganizationalUnitTreeItemViewModel> units)
        {
            int count = 0;
            foreach (var unit in units)
            {
                // Policz działy w tej jednostce (dzieci które są działami)
                count += unit.Children.Count(child => child.IsDepartment);
                // Rekurencyjnie policz działy w pod-jednostkach
                count += CountAllDepartments(unit.Children);
            }
            return count;
        }

        private int CountActiveDepartments(ObservableCollection<OrganizationalUnitTreeItemViewModel> units)
        {
            int count = 0;
            foreach (var unit in units)
            {
                // Policz aktywne działy w tej jednostce
                count += unit.Children.Count(child => child.IsDepartment && child.IsActive);
                // Rekurencyjnie policz aktywne działy w pod-jednostkach
                count += CountActiveDepartments(unit.Children);
            }
            return count;
        }

        private int CountAllOrganizationalUnits(ObservableCollection<OrganizationalUnitTreeItemViewModel> units)
        {
            int count = 0;
            foreach (var unit in units)
            {
                if (!unit.IsDepartment) count++; // Policz tylko jednostki organizacyjne, nie działy
                count += CountAllOrganizationalUnits(unit.Children);
            }
            return count;
        }

        private OrganizationalUnitTreeItemViewModel? FindItemById(string id)
        {
            return FindItemById(id, _organizationalUnits);
        }

        private OrganizationalUnitTreeItemViewModel? FindItemById(string id, ObservableCollection<OrganizationalUnitTreeItemViewModel> children)
        {
            foreach (var item in children)
            {
                if (item.Id == id)
                {
                    return item;
                }
                var found = FindItemById(id, item.Children);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void ExpandPathToItem(OrganizationalUnitTreeItemViewModel item)
        {
            var current = item.Parent;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent;
            }
        }

        #endregion
    }
} 