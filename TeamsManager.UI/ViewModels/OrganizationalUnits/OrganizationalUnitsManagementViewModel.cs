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

namespace TeamsManager.UI.ViewModels.OrganizationalUnits
{
    /// <summary>
    /// ViewModel dla zarządzania jednostkami organizacyjnymi - hierarchiczny widok drzewa jednostek
    /// </summary>
    public class OrganizationalUnitsManagementViewModel : BaseViewModel
    {
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<OrganizationalUnitsManagementViewModel> _logger;
        private readonly MainShellViewModel _mainShellViewModel;
        
        private ObservableCollection<OrganizationalUnitTreeItemViewModel> _organizationalUnits;
        private ICollectionView _organizationalUnitsView;
        private OrganizationalUnitTreeItemViewModel? _selectedOrganizationalUnit;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private string? _errorMessage;

        public OrganizationalUnitsManagementViewModel(
            IOrganizationalUnitService organizationalUnitService,
            IDepartmentService departmentService,
            ILogger<OrganizationalUnitsManagementViewModel> logger,
            IUIDialogService uiDialogService,
            MainShellViewModel mainShellViewModel)
        {
            _organizationalUnitService = organizationalUnitService ?? throw new ArgumentNullException(nameof(organizationalUnitService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));
            _mainShellViewModel = mainShellViewModel ?? throw new ArgumentNullException(nameof(mainShellViewModel));

            _organizationalUnits = new ObservableCollection<OrganizationalUnitTreeItemViewModel>();
            _organizationalUnitsView = CollectionViewSource.GetDefaultView(_organizationalUnits);
            _organizationalUnitsView.Filter = FilterOrganizationalUnits;

            // Initialize commands
            LoadOrganizationalUnitsCommand = new RelayCommand(async () => await LoadOrganizationalUnitsAsync(), () => !IsLoading);
            RefreshCommand = new RelayCommand(async () => await LoadOrganizationalUnitsAsync(forceRefresh: true), () => !IsLoading);
            AddOrganizationalUnitCommand = new RelayCommand(async () => await AddOrganizationalUnitAsync(), () => !IsLoading);
            EditOrganizationalUnitCommand = new RelayCommand(async () => await EditOrganizationalUnitAsync(), () => SelectedOrganizationalUnit != null && !IsLoading);
            DeleteOrganizationalUnitCommand = new RelayCommand(async () => await DeleteOrganizationalUnitAsync(), () => SelectedOrganizationalUnit != null && !IsLoading);
            ViewOrganizationalUnitCommand = new RelayCommand(async () => await ViewOrganizationalUnitAsync(), () => SelectedOrganizationalUnit != null && !IsLoading);
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            ClearSearchCommand = new RelayCommand(ClearSearch, () => !string.IsNullOrEmpty(SearchText));

            // Load initial data
            _ = LoadOrganizationalUnitsAsync();
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

        public OrganizationalUnitTreeItemViewModel? SelectedOrganizationalUnit
        {
            get => _selectedOrganizationalUnit;
            set
            {
                if (SetProperty(ref _selectedOrganizationalUnit, value))
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

        public int TotalOrganizationalUnits => CountAllOrganizationalUnits(_organizationalUnits);

        public int ActiveOrganizationalUnits => CountActiveOrganizationalUnits(_organizationalUnits);

        public int TotalDepartments => CountAllDepartments(_organizationalUnits);

        public int ActiveUnits => ActiveOrganizationalUnits;

        #endregion

        #region Commands

        public ICommand LoadOrganizationalUnitsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddOrganizationalUnitCommand { get; }
        public ICommand EditOrganizationalUnitCommand { get; }
        public ICommand DeleteOrganizationalUnitCommand { get; }
        public ICommand ViewOrganizationalUnitCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        #region Public Methods

        public async Task LoadOrganizationalUnitsAsync(bool forceRefresh = false)
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                _logger.LogDebug("Loading organizational units (forceRefresh: {ForceRefresh})", forceRefresh);

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

                OrganizationalUnits = unitViewModels;
                OrganizationalUnitsView = CollectionViewSource.GetDefaultView(OrganizationalUnits);
                OrganizationalUnitsView.Filter = FilterOrganizationalUnits;

                OnPropertyChanged(nameof(TotalOrganizationalUnits));
                OnPropertyChanged(nameof(ActiveOrganizationalUnits));
                OnPropertyChanged(nameof(TotalDepartments));
                OnPropertyChanged(nameof(ActiveUnits));

                _logger.LogDebug("Loaded {Count} root organizational units", unitViewModels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading organizational units");
                ErrorMessage = $"Błąd podczas ładowania jednostek organizacyjnych: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private OrganizationalUnitTreeItemViewModel CreateOrganizationalUnitTreeItem(
            OrganizationalUnit unit, 
            OrganizationalUnitTreeItemViewModel? parent)
        {
            var viewModel = new OrganizationalUnitTreeItemViewModel(unit, parent);

            // Rekurencyjnie dodaj podjednostki
            if (unit.SubUnits != null)
            {
                foreach (var subUnit in unit.SubUnits.OrderBy(su => su.SortOrder).ThenBy(su => su.Name))
                {
                    var subUnitViewModel = CreateOrganizationalUnitTreeItem(subUnit, viewModel);
                    viewModel.AddChild(subUnitViewModel);
                }
            }

            return viewModel;
        }

        private async Task LoadDepartmentsForUnit(
            OrganizationalUnitTreeItemViewModel unitViewModel, 
            IEnumerable<Department> allDepartments)
        {
            // Dodaj działy przypisane do tej jednostki
            var unitDepartments = allDepartments
                .Where(d => d.OrganizationalUnitId == unitViewModel.Id && d.IsActive)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Name);

            foreach (var department in unitDepartments)
            {
                unitViewModel.AddDepartment(department);
            }

            // Rekurencyjnie załaduj działy dla podjednostek
            foreach (var child in unitViewModel.Children)
            {
                await LoadDepartmentsForUnit(child, allDepartments);
            }
        }

        private bool FilterOrganizationalUnits(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (item is OrganizationalUnitTreeItemViewModel unit)
            {
                return ContainsSearchText(unit, SearchText.ToLower());
            }

            return false;
        }

        private bool ContainsSearchText(OrganizationalUnitTreeItemViewModel unit, string searchText)
        {
            // Sprawdź nazwę jednostki
            if (unit.Name.ToLower().Contains(searchText))
                return true;

            // Sprawdź opis
            if (!string.IsNullOrEmpty(unit.Description) && unit.Description.ToLower().Contains(searchText))
                return true;

            // Sprawdź działy
            if (unit.Departments.Any(d => d.Name.ToLower().Contains(searchText)))
                return true;

            // Sprawdź podjednostki rekurencyjnie
            return unit.Children.Any(child => ContainsSearchText(child, searchText));
        }

        private async Task AddOrganizationalUnitAsync()
        {
            try
            {
                _logger.LogDebug("Adding new organizational unit");
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.OrganizationalUnits.OrganizationalUnitEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<OrganizationalUnitEditViewModel>();
                
                // Jeśli wybrano jednostkę, ustaw ją jako nadrzędną
                string? parentUnitId = SelectedOrganizationalUnit?.Id;
                viewModel.InitializeForAdd(parentUnitId);
                
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                if (result == true)
                {
                    await LoadOrganizationalUnitsAsync(forceRefresh: true);
                    
                    // Znajdź i zaznacz nowo utworzoną jednostkę
                    if (!string.IsNullOrEmpty(viewModel.CreatedUnitId))
                    {
                        var createdUnit = FindOrganizationalUnitById(viewModel.CreatedUnitId, OrganizationalUnits);
                        if (createdUnit != null)
                        {
                            SelectedOrganizationalUnit = createdUnit;
                            createdUnit.IsSelected = true;
                        }
                    }

                    await UIDialogService.ShowSuccessAsync("Sukces", "Jednostka organizacyjna została pomyślnie utworzona.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding organizational unit");
                await UIDialogService.ShowErrorAsync("Błąd", $"Błąd podczas dodawania jednostki organizacyjnej: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task EditOrganizationalUnitAsync()
        {
            if (SelectedOrganizationalUnit == null) return;

            try
            {
                _logger.LogDebug("Editing organizational unit {UnitId}", SelectedOrganizationalUnit.Id);
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.OrganizationalUnits.OrganizationalUnitEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<OrganizationalUnitEditViewModel>();
                
                await viewModel.InitializeForEditAsync(SelectedOrganizationalUnit.Id);
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;

                var result = dialog.ShowDialog();
                if (result == true)
                {
                    await LoadOrganizationalUnitsAsync(forceRefresh: true);
                    
                    // Znajdź i zaznacz edytowaną jednostkę
                    var editedUnit = FindOrganizationalUnitById(SelectedOrganizationalUnit.Id, OrganizationalUnits);
                    if (editedUnit != null)
                    {
                        SelectedOrganizationalUnit = editedUnit;
                        editedUnit.IsSelected = true;
                    }

                    await UIDialogService.ShowSuccessAsync("Sukces", "Jednostka organizacyjna została pomyślnie zaktualizowana.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing organizational unit");
                await UIDialogService.ShowErrorAsync("Błąd", $"Błąd podczas edycji jednostki organizacyjnej: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task ViewOrganizationalUnitAsync()
        {
            if (SelectedOrganizationalUnit == null) return;

            try
            {
                _logger.LogDebug("Viewing organizational unit {UnitId}", SelectedOrganizationalUnit.Id);
                
                // Pokaż overlay
                _mainShellViewModel.IsDialogOpen = true;
                
                var dialog = new Views.OrganizationalUnits.OrganizationalUnitEditDialog();
                var viewModel = App.ServiceProvider.GetRequiredService<OrganizationalUnitEditViewModel>();
                
                await viewModel.InitializeForViewAsync(SelectedOrganizationalUnit.Id);
                dialog.DataContext = viewModel;
                dialog.Owner = System.Windows.Application.Current.MainWindow;

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing organizational unit");
                await UIDialogService.ShowErrorAsync("Błąd", $"Błąd podczas wyświetlania jednostki organizacyjnej: {ex.Message}");
            }
            finally
            {
                // Ukryj overlay
                _mainShellViewModel.IsDialogOpen = false;
            }
        }

        private async Task DeleteOrganizationalUnitAsync()
        {
            if (SelectedOrganizationalUnit == null) return;

            try
            {
                // Sprawdź czy można usunąć jednostkę
                var canDelete = await _organizationalUnitService.CanDeleteOrganizationalUnitAsync(SelectedOrganizationalUnit.Id);
                
                if (!canDelete)
                {
                    await UIDialogService.ShowWarningAsync("Ostrzeżenie", 
                        "Nie można usunąć tej jednostki organizacyjnej, ponieważ ma przypisane podjednostki lub działy. " +
                        "Najpierw przenieś lub usuń wszystkie elementy podrzędne.");
                    return;
                }

                var confirmResult = await UIDialogService.ShowConfirmationAsync("Potwierdzenie", 
                    $"Czy na pewno chcesz usunąć jednostkę organizacyjną '{SelectedOrganizationalUnit.Name}'?\n\n" +
                    "Ta operacja jest nieodwracalna.");

                if (confirmResult.IsPrimary)
                {
                    await _organizationalUnitService.DeleteOrganizationalUnitAsync(SelectedOrganizationalUnit.Id);
                    await LoadOrganizationalUnitsAsync(forceRefresh: true);
                    
                    SelectedOrganizationalUnit = null;
                    await UIDialogService.ShowSuccessAsync("Sukces", "Jednostka organizacyjna została pomyślnie usunięta.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting organizational unit {UnitId}", SelectedOrganizationalUnit?.Id);
                await UIDialogService.ShowErrorAsync("Błąd", $"Błąd podczas usuwania jednostki organizacyjnej: {ex.Message}");
            }
        }

        private void ExpandAll()
        {
            foreach (var unit in OrganizationalUnits)
            {
                unit.ExpandAll();
            }
        }

        private void CollapseAll()
        {
            foreach (var unit in OrganizationalUnits)
            {
                unit.CollapseAll();
            }
        }

        private int CountAllOrganizationalUnits(ObservableCollection<OrganizationalUnitTreeItemViewModel> units)
        {
            int count = units.Count;
            foreach (var unit in units)
            {
                count += CountAllOrganizationalUnits(unit.Children);
            }
            return count;
        }

        private int CountActiveOrganizationalUnits(ObservableCollection<OrganizationalUnitTreeItemViewModel> units)
        {
            int count = units.Count(u => u.IsActive);
            foreach (var unit in units)
            {
                count += CountActiveOrganizationalUnits(unit.Children);
            }
            return count;
        }

        private OrganizationalUnitTreeItemViewModel? FindOrganizationalUnitById(string id, ObservableCollection<OrganizationalUnitTreeItemViewModel> children)
        {
            foreach (var unit in children)
            {
                if (unit.Id == id)
                {
                    return unit;
                }
                var found = FindOrganizationalUnitById(id, unit.Children);
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
            (LoadOrganizationalUnitsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddOrganizationalUnitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditOrganizationalUnitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteOrganizationalUnitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ViewOrganizationalUnitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        #endregion
    }
} 