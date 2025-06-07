using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Models.SchoolYearModels;
using TeamsManager.UI.Services.UI;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.SchoolYears
{
    /// <summary>
    /// ViewModel dla listy lat szkolnych
    /// Obsługuje wyświetlanie, wyszukiwanie i operacje CRUD
    /// </summary>
    public class SchoolYearListViewModel : BaseViewModel
    {
        private readonly SchoolYearUIService _schoolYearUIService;
        private readonly ILogger<SchoolYearListViewModel> _logger;

        // ===== WŁAŚCIWOŚCI DANYCH =====
        private ObservableCollection<SchoolYearDisplayModel> _schoolYears;
        private ICollectionView _schoolYearsView;
        private SchoolYearDisplayModel? _selectedSchoolYear;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private bool _hasData;
        private string _statusMessage = string.Empty;

        public SchoolYearListViewModel(
            SchoolYearUIService schoolYearUIService,
            ILogger<SchoolYearListViewModel> logger)
        {
            _schoolYearUIService = schoolYearUIService ?? throw new ArgumentNullException(nameof(schoolYearUIService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Inicjalizacja kolekcji
            _schoolYears = new ObservableCollection<SchoolYearDisplayModel>();
            _schoolYearsView = CollectionViewSource.GetDefaultView(_schoolYears);
            _schoolYearsView.Filter = SchoolYearFilter;

            // Inicjalizacja komend
            LoadDataCommand = new RelayCommand(_ => LoadDataAsync(), _ => !IsLoading);
            RefreshCommand = new RelayCommand(_ => RefreshDataAsync(), _ => !IsLoading);
            AddNewCommand = new RelayCommand(_ => AddNewSchoolYear(), _ => !IsLoading);
            EditCommand = new RelayCommand(_ => EditSchoolYear(), _ => CanEdit());
            DeleteCommand = new RelayCommand(_ => DeleteSchoolYearAsync(), _ => CanDelete());
            SetAsCurrentCommand = new RelayCommand(_ => SetAsCurrentAsync(), _ => CanSetAsCurrent());
            ClearSearchCommand = new RelayCommand(_ => ClearSearch(), _ => !string.IsNullOrEmpty(SearchText));

            // Automatyczne ładowanie danych
            _ = LoadDataAsync();
        }

        // ===== WŁAŚCIWOŚCI PUBLICZNE =====

        public ObservableCollection<SchoolYearDisplayModel> SchoolYears
        {
            get => _schoolYears;
            private set
            {
                _schoolYears = value;
                OnPropertyChanged();
                _schoolYearsView = CollectionViewSource.GetDefaultView(_schoolYears);
                _schoolYearsView.Filter = SchoolYearFilter;
                OnPropertyChanged(nameof(SchoolYearsView));
                OnPropertyChanged(nameof(HasData));
            }
        }

        public ICollectionView SchoolYearsView
        {
            get => _schoolYearsView;
            private set
            {
                _schoolYearsView = value;
                OnPropertyChanged();
            }
        }

        public SchoolYearDisplayModel? SelectedSchoolYear
        {
            get => _selectedSchoolYear;
            set
            {
                _selectedSchoolYear = value;
                OnPropertyChanged();
                
                // Odśwież dostępność komend
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SetAsCurrentCommand).RaiseCanExecuteChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                
                // Odśwież filtr
                _schoolYearsView?.Refresh();
                ((RelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
                
                // Odśwież dostępność komend
                ((RelayCommand)LoadDataCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
                ((RelayCommand)AddNewCommand).RaiseCanExecuteChanged();
                ((RelayCommand)EditCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SetAsCurrentCommand).RaiseCanExecuteChanged();
            }
        }

        public bool HasData
        {
            get => _schoolYears?.Count > 0;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        // ===== STATYSTYKI =====

        public int TotalSchoolYears => _schoolYears?.Count ?? 0;
        
        public int CurrentSchoolYears => _schoolYears?.Count(sy => sy.IsCurrent) ?? 0;
        
        public int ActiveSchoolYears => _schoolYears?.Count(sy => sy.IsActive) ?? 0;
        
        public int FutureSchoolYears => _schoolYears?.Count(sy => sy.Status == "Przyszły") ?? 0;

        // ===== KOMENDY =====

        public RelayCommand LoadDataCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddNewCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand SetAsCurrentCommand { get; }
        public RelayCommand ClearSearchCommand { get; }

        // ===== METODY PRYWATNE =====

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Ładowanie lat szkolnych...";

                _logger.LogInformation("Rozpoczęcie ładowania lat szkolnych");

                var schoolYears = await _schoolYearUIService.GetAllActiveSchoolYearsAsync();
                var displayModels = schoolYears.ToList();

                SchoolYears.Clear();
                foreach (var schoolYear in displayModels)
                {
                    SchoolYears.Add(schoolYear);
                }

                StatusMessage = $"Załadowano {displayModels.Count} lat szkolnych";
                
                // Odśwież statystyki
                OnPropertyChanged(nameof(TotalSchoolYears));
                OnPropertyChanged(nameof(CurrentSchoolYears));
                OnPropertyChanged(nameof(ActiveSchoolYears));
                OnPropertyChanged(nameof(FutureSchoolYears));

                _logger.LogInformation("Pomyślnie załadowano {Count} lat szkolnych", displayModels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania lat szkolnych");
                StatusMessage = $"Błąd podczas ładowania: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Odświeżanie danych...";

                _logger.LogInformation("Rozpoczęcie odświeżania lat szkolnych");

                // Wymuś odświeżenie z cache
                var schoolYears = await _schoolYearUIService.GetAllActiveSchoolYearsAsync(forceRefresh: true);
                var displayModels = schoolYears.ToList();

                SchoolYears.Clear();
                foreach (var schoolYear in displayModels)
                {
                    SchoolYears.Add(schoolYear);
                }

                StatusMessage = $"Odświeżono {displayModels.Count} lat szkolnych";
                
                // Odśwież statystyki
                OnPropertyChanged(nameof(TotalSchoolYears));
                OnPropertyChanged(nameof(CurrentSchoolYears));
                OnPropertyChanged(nameof(ActiveSchoolYears));
                OnPropertyChanged(nameof(FutureSchoolYears));

                _logger.LogInformation("Pomyślnie odświeżono {Count} lat szkolnych", displayModels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania lat szkolnych");
                StatusMessage = $"Błąd podczas odświeżania: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddNewSchoolYear()
        {
            _logger.LogInformation("Otwieranie formularza dodawania nowego roku szkolnego");
            
            // TODO: Implementacja okna dialogowego dla dodawania
            // Będzie implementowana w Etapie 2
            StatusMessage = "Funkcja dodawania nowego roku szkolnego będzie dostępna w następnej wersji";
        }

        private void EditSchoolYear()
        {
            if (SelectedSchoolYear == null) return;

            _logger.LogInformation("Otwieranie formularza edycji roku szkolnego {Name}", SelectedSchoolYear.Name);
            
            // TODO: Implementacja okna dialogowego dla edycji
            // Będzie implementowana w Etapie 2
            StatusMessage = $"Funkcja edycji roku szkolnego '{SelectedSchoolYear.Name}' będzie dostępna w następnej wersji";
        }

        private async Task DeleteSchoolYearAsync()
        {
            if (SelectedSchoolYear == null) return;

            try
            {
                _logger.LogInformation("Próba usunięcia roku szkolnego {Name}", SelectedSchoolYear.Name);

                // Sprawdź czy można usunąć
                var canDelete = await _schoolYearUIService.CanDeleteSchoolYearAsync(SelectedSchoolYear.Id);
                if (!canDelete)
                {
                    StatusMessage = $"Nie można usunąć roku szkolnego '{SelectedSchoolYear.Name}'";
                    return;
                }

                // TODO: Dodać potwierdzenie użytkownika (MessageBox)
                // Na razie tylko symulacja
                StatusMessage = $"Potwierdź usunięcie roku szkolnego '{SelectedSchoolYear.Name}' - funkcja będzie dostępna w pełnej wersji";

                /*
                var result = await _schoolYearUIService.DeleteSchoolYearAsync(
                    SelectedSchoolYear.Id, 
                    SelectedSchoolYear.Name);

                if (result)
                {
                    SchoolYears.Remove(SelectedSchoolYear);
                    SelectedSchoolYear = null;
                    StatusMessage = "Rok szkolny został usunięty";
                }
                */
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania roku szkolnego");
                StatusMessage = $"Błąd podczas usuwania: {ex.Message}";
            }
        }

        private async Task SetAsCurrentAsync()
        {
            if (SelectedSchoolYear == null) return;
            if (SelectedSchoolYear.IsCurrent) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Ustawianie roku '{SelectedSchoolYear.Name}' jako bieżący...";

                _logger.LogInformation("Ustawianie roku szkolnego {Name} jako bieżący", SelectedSchoolYear.Name);

                var result = await _schoolYearUIService.SetCurrentSchoolYearAsync(
                    SelectedSchoolYear.Id,
                    SelectedSchoolYear.Name);

                if (result)
                {
                    // Odśwież listę aby pokazać zmiany
                    await RefreshDataAsync();
                    StatusMessage = $"Rok szkolny '{SelectedSchoolYear.Name}' został ustawiony jako bieżący";
                }
                else
                {
                    StatusMessage = $"Nie udało się ustawić roku '{SelectedSchoolYear.Name}' jako bieżący";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ustawiania roku szkolnego jako bieżący");
                StatusMessage = $"Błąd podczas operacji: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        // ===== WALIDACJA KOMEND =====

        private bool CanEdit()
        {
            return !IsLoading && SelectedSchoolYear != null;
        }

        private bool CanDelete()
        {
            return !IsLoading && SelectedSchoolYear != null && !SelectedSchoolYear.IsCurrent;
        }

        private bool CanSetAsCurrent()
        {
            return !IsLoading && SelectedSchoolYear != null && !SelectedSchoolYear.IsCurrent;
        }

        // ===== FILTROWANIE =====

        private bool SchoolYearFilter(object item)
        {
            if (item is not SchoolYearDisplayModel schoolYear) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var searchLower = SearchText.ToLowerInvariant();
            
            return schoolYear.Name.ToLowerInvariant().Contains(searchLower) ||
                   schoolYear.Description.ToLowerInvariant().Contains(searchLower) ||
                   schoolYear.Status.ToLowerInvariant().Contains(searchLower) ||
                   schoolYear.Period.ToLowerInvariant().Contains(searchLower);
        }

        // ===== METODY PUBLICZNE =====

        /// <summary>
        /// Odświeża dane z serwera
        /// </summary>
        public async Task RefreshAsync()
        {
            await RefreshDataAsync();
        }

        /// <summary>
        /// Wybiera rok szkolny o podanym ID
        /// </summary>
        public void SelectSchoolYear(string schoolYearId)
        {
            SelectedSchoolYear = SchoolYears.FirstOrDefault(sy => sy.Id == schoolYearId);
        }

        /// <summary>
        /// Czyści wybór
        /// </summary>
        public void ClearSelection()
        {
            SelectedSchoolYear = null;
        }

        /// <summary>
        /// Eksportuje statystyki
        /// </summary>
        public string GetStatisticsSummary()
        {
            return $"Łącznie: {TotalSchoolYears}, Bieżących: {CurrentSchoolYears}, Aktywnych: {ActiveSchoolYears}, Przyszłych: {FutureSchoolYears}";
        }
    }
} 