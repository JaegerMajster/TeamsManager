using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel dla widoku ustawień aplikacji
    /// Zarządza listą ustawień, filtrowaniem i kategoriami
    /// </summary>
    public class ApplicationSettingsViewModel : INotifyPropertyChanged
    {
        private readonly ApplicationSettingService _settingService;
        private readonly ILogger<ApplicationSettingsViewModel> _logger;
        
        private ObservableCollection<ApplicationSettingItemViewModel> _settings = new();
        private ICollectionView _settingsView;
        private string _searchText = string.Empty;
        private string _selectedCategory = "Wszystkie";
        private bool _isLoading;
        private bool _hasError;
        private string _errorMessage = string.Empty;
        private bool _showOnlyRequired;
        private bool _showInvisible;

        public ApplicationSettingsViewModel(
            ApplicationSettingService settingService, 
            ILogger<ApplicationSettingsViewModel> logger)
        {
            _settingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            Settings = new ObservableCollection<ApplicationSettingItemViewModel>();
            Categories = new ObservableCollection<string> { "Wszystkie" };
            
            // Inicjalizacja widoku kolekcji z filtrowaniem i sortowaniem
            _settingsView = CollectionViewSource.GetDefaultView(Settings);
            _settingsView.Filter = FilterSettings;
            _settingsView.SortDescriptions.Add(new SortDescription(nameof(ApplicationSettingItemViewModel.Category), ListSortDirection.Ascending));
            _settingsView.SortDescriptions.Add(new SortDescription(nameof(ApplicationSettingItemViewModel.DisplayOrder), ListSortDirection.Ascending));
            
            // Inicjalizacja komend
            LoadSettingsCommand = new RelayCommand(async _ => await LoadSettingsAsync());
            RefreshCommand = new RelayCommand(async _ => await LoadSettingsAsync(true), _ => !IsLoading);
            AddSettingCommand = new RelayCommand(_ => AddNewSetting());
            DeleteSettingCommand = new RelayCommand(async setting => await DeleteSettingAsync(setting as ApplicationSettingItemViewModel));
            ExportCommand = new RelayCommand(async _ => await ExportSettingsAsync(), _ => Settings.Any());
            ImportCommand = new RelayCommand(async _ => await ImportSettingsAsync());
        }

        #region Properties

        public ObservableCollection<ApplicationSettingItemViewModel> Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public ICollectionView SettingsView => _settingsView;

        public ObservableCollection<string> Categories { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _settingsView?.Refresh();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    _settingsView?.Refresh();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowOnlyRequired
        {
            get => _showOnlyRequired;
            set
            {
                if (_showOnlyRequired != value)
                {
                    _showOnlyRequired = value;
                    OnPropertyChanged();
                    _settingsView?.Refresh();
                }
            }
        }

        public bool ShowInvisible
        {
            get => _showInvisible;
            set
            {
                if (_showInvisible != value)
                {
                    _showInvisible = value;
                    OnPropertyChanged();
                    _settingsView?.Refresh();
                }
            }
        }

        public int TotalSettings => Settings.Count;
        public int VisibleSettings => _settingsView?.Cast<object>().Count() ?? 0;

        #endregion

        #region Commands

        public ICommand LoadSettingsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddSettingCommand { get; }
        public ICommand DeleteSettingCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }

        #endregion

        #region Methods

        public async Task InitializeAsync()
        {
            await LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync(bool forceRefresh = false)
        {
            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                _logger.LogInformation("Ładowanie ustawień aplikacji");
                
                var settings = await _settingService.GetAllSettingsAsync(forceRefresh);
                
                Settings.Clear();
                Categories.Clear();
                Categories.Add("Wszystkie");
                
                var categories = new HashSet<string>();
                
                foreach (var setting in settings.OrderBy(s => s.DisplayOrder))
                {
                    var itemVm = new ApplicationSettingItemViewModel(setting);
                    itemVm.SettingSaved += OnSettingSaved;
                    Settings.Add(itemVm);
                    
                    if (!string.IsNullOrEmpty(setting.Category))
                        categories.Add(setting.Category);
                }
                
                // Dodaj unikalne kategorie
                foreach (var category in categories.OrderBy(c => c))
                {
                    Categories.Add(category);
                }
                
                _logger.LogInformation("Załadowano {Count} ustawień w {CategoryCount} kategoriach", 
                    settings.Count, categories.Count);
                
                OnPropertyChanged(nameof(TotalSettings));
                OnPropertyChanged(nameof(VisibleSettings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania ustawień");
                HasError = true;
                ErrorMessage = "Nie udało się załadować ustawień aplikacji. Sprawdź połączenie z serwerem.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool FilterSettings(object obj)
        {
            if (obj is not ApplicationSettingItemViewModel setting)
                return false;

            // Filtr widoczności
            if (!ShowInvisible && !setting.IsVisible)
                return false;

            // Filtr wymaganych
            if (ShowOnlyRequired && !setting.IsRequired)
                return false;

            // Filtr kategorii
            if (SelectedCategory != "Wszystkie" && setting.Category != SelectedCategory)
                return false;

            // Filtr wyszukiwania
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                return setting.Key.ToLower().Contains(searchLower) ||
                       setting.Description.ToLower().Contains(searchLower) ||
                       setting.Value.ToLower().Contains(searchLower);
            }

            return true;
        }

        private async void OnSettingSaved(object? sender, EventArgs e)
        {
            if (sender is ApplicationSettingItemViewModel settingVm)
            {
                try
                {
                    _logger.LogInformation("Zapisywanie ustawienia: {Key}", settingVm.Key);
                    
                    var success = await _settingService.UpdateSettingAsync(settingVm.GetModel());
                    
                    if (!success)
                    {
                        // TODO: Pokazać komunikat o błędzie
                        _logger.LogWarning("Nie udało się zapisać ustawienia {Key}", settingVm.Key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas zapisywania ustawienia {Key}", settingVm.Key);
                }
            }
        }

        private void AddNewSetting()
        {
            // TODO: Otworzyć dialog do dodawania nowego ustawienia
            _logger.LogInformation("Dodawanie nowego ustawienia - funkcja w przygotowaniu");
        }

        private async Task DeleteSettingAsync(ApplicationSettingItemViewModel setting)
        {
            if (setting == null) return;

            try
            {
                // TODO: Potwierdzenie usunięcia
                
                _logger.LogInformation("Usuwanie ustawienia: {Key}", setting.Key);
                
                var success = await _settingService.DeleteSettingAsync(setting.Key);
                
                if (success)
                {
                    Settings.Remove(setting);
                    OnPropertyChanged(nameof(TotalSettings));
                    OnPropertyChanged(nameof(VisibleSettings));
                }
                else
                {
                    _logger.LogWarning("Nie udało się usunąć ustawienia {Key}", setting.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania ustawienia {Key}", setting.Key);
            }
        }

        private async Task ExportSettingsAsync()
        {
            // TODO: Implementacja eksportu do JSON/CSV
            _logger.LogInformation("Eksport ustawień - funkcja w przygotowaniu");
            await Task.CompletedTask;
        }

        private async Task ImportSettingsAsync()
        {
            // TODO: Implementacja importu z JSON/CSV
            _logger.LogInformation("Import ustawień - funkcja w przygotowaniu");
            await Task.CompletedTask;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
} 