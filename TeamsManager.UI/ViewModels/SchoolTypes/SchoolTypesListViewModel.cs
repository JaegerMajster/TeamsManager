using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using TeamsManager.UI.Models.SchoolTypeModels;
using TeamsManager.UI.Services.UI;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Views.SchoolTypes;
using BaseViewModel = TeamsManager.UI.ViewModels.BaseViewModel;

namespace TeamsManager.UI.ViewModels.SchoolTypes
{
    /// <summary>
    /// ViewModel dla listy typów szkół
    /// </summary>
    public class SchoolTypesListViewModel : BaseViewModel
    {
        private readonly SchoolTypeUIService _schoolTypeUIService;
        private readonly ILogger<SchoolTypesListViewModel> _logger;
        
        private ObservableCollection<SchoolTypeDisplayModel> _schoolTypes;
        private ICollectionView _schoolTypesView;
        private SchoolTypeDisplayModel? _selectedSchoolType;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private bool _hasData;

        public SchoolTypesListViewModel(
            SchoolTypeUIService schoolTypeUIService,
            ILogger<SchoolTypesListViewModel> logger)
        {
            _schoolTypeUIService = schoolTypeUIService ?? throw new ArgumentNullException(nameof(schoolTypeUIService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _schoolTypes = new ObservableCollection<SchoolTypeDisplayModel>();
            _schoolTypesView = CollectionViewSource.GetDefaultView(_schoolTypes);
            _schoolTypesView.Filter = FilterSchoolTypes;

            // Inicjalizacja komend
            LoadDataCommand = new RelayCommand(async _ => await LoadDataAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddNewCommand = new RelayCommand(async _ => await AddNewSchoolTypeAsync());
            EditCommand = new RelayCommand(async _ => await EditSchoolTypeAsync(), _ => SelectedSchoolType != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteSchoolTypeAsync(), _ => SelectedSchoolType != null);
            ViewDetailsCommand = new RelayCommand(_ => ViewDetails(), _ => SelectedSchoolType != null);
        }

        #region Properties

        public ObservableCollection<SchoolTypeDisplayModel> SchoolTypes
        {
            get => _schoolTypes;
            set => SetProperty(ref _schoolTypes, value);
        }

        public ICollectionView SchoolTypesView
        {
            get => _schoolTypesView;
            set => SetProperty(ref _schoolTypesView, value);
        }

        public SchoolTypeDisplayModel? SelectedSchoolType
        {
            get => _selectedSchoolType;
            set
            {
                if (SetProperty(ref _selectedSchoolType, value))
                {
                    // Aktualizuj stan komend
                    ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ViewDetailsCommand).RaiseCanExecuteChanged();
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
                    _schoolTypesView.Refresh();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        public int TotalCount => SchoolTypes.Count;
        public int FilteredCount => _schoolTypesView.Cast<object>().Count();

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddNewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        #endregion

        #region Methods

        public async Task LoadDataAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                _logger.LogInformation("Rozpoczynanie ładowania listy typów szkół");

                var schoolTypes = await _schoolTypeUIService.GetAllActiveSchoolTypesAsync();

                SchoolTypes.Clear();
                foreach (var schoolType in schoolTypes)
                {
                    SchoolTypes.Add(schoolType);
                }

                HasData = SchoolTypes.Any();
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(FilteredCount));

                _logger.LogInformation("Załadowano {Count} typów szkół", SchoolTypes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania listy typów szkół");
                HasData = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshAsync()
        {
            _logger.LogInformation("Odświeżanie listy typów szkół");
            SearchText = string.Empty;
            await LoadDataAsync();
        }

        private async Task AddNewSchoolTypeAsync()
        {
            _logger.LogInformation("Otwieranie dialogu dodawania nowego typu szkoły");

            var dialog = new SchoolTypeEditDialog(null);
            var result = dialog.ShowDialog();

            if (result == true && dialog.DataContext is SchoolTypeEditViewModel vm && vm.EditedSchoolType != null)
            {
                var (success, errorMessage) = await _schoolTypeUIService.CreateSchoolTypeAsync(
                    vm.EditedSchoolType.ShortName,
                    vm.EditedSchoolType.FullName,
                    vm.EditedSchoolType.Description,
                    vm.EditedSchoolType.ColorCode,
                    vm.EditedSchoolType.SortOrder);

                if (success)
                {
                    await LoadDataAsync();
                }
                else
                {
                    _logger.LogWarning("Nie udało się utworzyć typu szkoły: {Error}", errorMessage);
                }
            }
        }

        private async Task EditSchoolTypeAsync()
        {
            if (SelectedSchoolType == null) return;

            _logger.LogInformation("Otwieranie dialogu edycji typu szkoły: {SchoolTypeId}", SelectedSchoolType.Id);

            var schoolTypeToEdit = SelectedSchoolType.ToSchoolType();
            var dialog = new SchoolTypeEditDialog(schoolTypeToEdit);
            var result = dialog.ShowDialog();

            if (result == true && dialog.DataContext is SchoolTypeEditViewModel vm && vm.EditedSchoolType != null)
            {
                var (success, errorMessage) = await _schoolTypeUIService.UpdateSchoolTypeAsync(vm.EditedSchoolType);

                if (success)
                {
                    // Odśwież tylko edytowany element
                    var updated = await _schoolTypeUIService.GetSchoolTypeByIdAsync(vm.EditedSchoolType.Id);
                    if (updated != null)
                    {
                        SelectedSchoolType.UpdateFromSchoolType(updated.ToSchoolType());
                    }
                }
                else
                {
                    _logger.LogWarning("Nie udało się zaktualizować typu szkoły: {Error}", errorMessage);
                }
            }
        }

        private async Task DeleteSchoolTypeAsync()
        {
            if (SelectedSchoolType == null) return;

            var displayName = SelectedSchoolType.DisplayName;
            
            // Potwierdzenie usunięcia
            var confirmMessage = $"Czy na pewno chcesz usunąć typ szkoły '{displayName}'?\n\n" +
                                "Ta operacja spowoduje dezaktywację typu szkoły.\n" +
                                "Powiązane zespoły i szablony pozostaną niezmienione.";

            // TODO: Użyć Material Design Dialog
            var result = System.Windows.MessageBox.Show(
                confirmMessage,
                "Potwierdź usunięcie",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _logger.LogInformation("Usuwanie typu szkoły: {SchoolTypeId}", SelectedSchoolType.Id);

                var (success, errorMessage) = await _schoolTypeUIService.DeleteSchoolTypeAsync(
                    SelectedSchoolType.Id, 
                    displayName);

                if (success)
                {
                    SchoolTypes.Remove(SelectedSchoolType);
                    SelectedSchoolType = null;
                    OnPropertyChanged(nameof(TotalCount));
                    OnPropertyChanged(nameof(FilteredCount));
                    HasData = SchoolTypes.Any();
                }
                else
                {
                    _logger.LogWarning("Nie udało się usunąć typu szkoły: {Error}", errorMessage);
                    
                    System.Windows.MessageBox.Show(
                        $"Nie można usunąć typu szkoły:\n{errorMessage}",
                        "Błąd",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ViewDetails()
        {
            if (SelectedSchoolType == null) return;

            _logger.LogInformation("Wyświetlanie szczegółów typu szkoły: {SchoolTypeId}", SelectedSchoolType.Id);
            
            // TODO: Implementacja wyświetlania szczegółów
            // Może otworzyć dialog tylko do odczytu lub przejść do widoku szczegółów
        }

        private bool FilterSchoolTypes(object obj)
        {
            if (obj is not SchoolTypeDisplayModel schoolType)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            var searchLower = SearchText.ToLower();

            return schoolType.ShortName.ToLower().Contains(searchLower) ||
                   schoolType.FullName.ToLower().Contains(searchLower) ||
                   (schoolType.Description?.ToLower().Contains(searchLower) ?? false);
        }

        #endregion
    }
} 