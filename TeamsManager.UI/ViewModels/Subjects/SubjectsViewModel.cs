using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace TeamsManager.UI.ViewModels.Subjects
{
    public class SubjectsViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SubjectsViewModel> _logger;
        
        private ObservableCollection<Subject> _subjects = new();
        private Subject? _selectedSubject;
        private bool _isLoading;
        private string _searchText = string.Empty;
        private string _selectedCategory = "Wszystkie";
        private ICollectionView? _subjectsView;
        
        public SubjectsViewModel(HttpClient httpClient, ILogger<SubjectsViewModel> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            Subjects = new ObservableCollection<Subject>();
            Categories = new ObservableCollection<string>
            {
                "Wszystkie",
                "Nauki ścisłe",
                "Języki obce", 
                "Przedmioty humanistyczne",
                "Przedmioty zawodowe",
                "Wychowanie fizyczne",
                "Inne"
            };
            
            InitializeCommands();
            SetupCollectionView();
        }
        
        #region Properties
        
        public ObservableCollection<Subject> Subjects
        {
            get => _subjects;
            set 
            { 
                _subjects = value; 
                OnPropertyChanged();
                SetupCollectionView();
            }
        }
        
        public ObservableCollection<string> Categories { get; }
        
        public Subject? SelectedSubject
        {
            get => _selectedSubject;
            set 
            { 
                _selectedSubject = value; 
                OnPropertyChanged();
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                _isLoading = value; 
                OnPropertyChanged();
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
        
        public string SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                _selectedCategory = value; 
                OnPropertyChanged();
                ApplyFilters();
            }
        }
        
        public ICollectionView? SubjectsView
        {
            get => _subjectsView;
            private set 
            { 
                _subjectsView = value; 
                OnPropertyChanged();
            }
        }
        
        // Statistics
        public int TotalSubjects => Subjects?.Count ?? 0;
        public int FilteredSubjects => SubjectsView?.Cast<Subject>().Count() ?? 0;
        
        #endregion
        
        #region Commands
        
        public ICommand LoadSubjectsCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;
        public ICommand AddSubjectCommand { get; private set; } = null!;
        public ICommand EditSubjectCommand { get; private set; } = null!;
        public ICommand DeleteSubjectCommand { get; private set; } = null!;
        public ICommand ImportCsvCommand { get; private set; } = null!;
        public ICommand ExportCsvCommand { get; private set; } = null!;
        public ICommand ViewTeachersCommand { get; private set; } = null!;
        public ICommand ClearSearchCommand { get; private set; } = null!;
        
        #endregion
        
        #region Public Methods
        
        public async Task InitializeAsync()
        {
            await LoadSubjectsAsync();
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeCommands()
        {
            LoadSubjectsCommand = new RelayCommand(async _ => await LoadSubjectsAsync());
            RefreshCommand = new RelayCommand(async _ => await LoadSubjectsAsync());
            AddSubjectCommand = new RelayCommand(async _ => await AddSubjectAsync());
            EditSubjectCommand = new RelayCommand<Subject>(async s => await EditSubjectAsync(s), s => s != null);
            DeleteSubjectCommand = new RelayCommand<Subject>(async s => await DeleteSubjectAsync(s), s => s != null);
            ImportCsvCommand = new RelayCommand(async _ => await ImportCsvAsync());
            ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync());
            ViewTeachersCommand = new RelayCommand<Subject>(async s => await ViewTeachersAsync(s), s => s != null);
            ClearSearchCommand = new RelayCommand(_ => ClearSearch());
        }
        
        private void SetupCollectionView()
        {
            if (Subjects != null)
            {
                SubjectsView = CollectionViewSource.GetDefaultView(Subjects);
                SubjectsView.Filter = FilterSubjects;
            }
        }
        
        private void ApplyFilters()
        {
            SubjectsView?.Refresh();
            OnPropertyChanged(nameof(FilteredSubjects));
        }
        
        private bool FilterSubjects(object item)
        {
            if (item is not Subject subject) return false;
            
            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerm = SearchText.ToLowerInvariant();
                if (!subject.Name.ToLowerInvariant().Contains(searchTerm) &&
                    !(subject.Code?.ToLowerInvariant().Contains(searchTerm) ?? false) &&
                    !(subject.Category?.ToLowerInvariant().Contains(searchTerm) ?? false))
                {
                    return false;
                }
            }
            
            // Category filter
            if (SelectedCategory != "Wszystkie" && subject.Category != SelectedCategory)
            {
                return false;
            }
            
            return true;
        }
        
        private void ClearSearch()
        {
            SearchText = string.Empty;
            SelectedCategory = "Wszystkie";
        }
        
        private async Task LoadSubjectsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Ładowanie listy przedmiotów");
                
                var response = await _httpClient.GetAsync("api/v1.0/Subjects");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var subjects = JsonSerializer.Deserialize<List<Subject>>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                Subjects.Clear();
                if (subjects != null)
                {
                    foreach (var subject in subjects)
                    {
                        Subjects.Add(subject);
                    }
                }
                
                OnPropertyChanged(nameof(TotalSubjects));
                OnPropertyChanged(nameof(FilteredSubjects));
                
                _logger.LogInformation("Załadowano {Count} przedmiotów", subjects?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania przedmiotów");
                await ShowErrorSnackbar("Nie udało się załadować listy przedmiotów");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task AddSubjectAsync()
        {
            try
            {
                var editVm = App.ServiceProvider.GetRequiredService<SubjectEditViewModel>();
                await editVm.InitializeAsync();
                editVm.DialogTitle = "Dodaj nowy przedmiot";
                
                var dialog = new Views.Subjects.SubjectEditDialog { DataContext = editVm };
                var result = await DialogHost.Show(dialog);
                
                if (result is bool success && success && editVm.IsValid)
                {
                    var createDto = new
                    {
                        Name = editVm.Name,
                        Code = editVm.Code,
                        Description = editVm.Description,
                        Hours = editVm.Hours,
                        DefaultSchoolTypeId = editVm.SelectedSchoolType?.Id,
                        Category = editVm.Category
                    };
                    
                    var json = JsonSerializer.Serialize(createDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var response = await _httpClient.PostAsync("api/v1.0/Subjects", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadSubjectsAsync();
                        await ShowSuccessSnackbar("Przedmiot został dodany pomyślnie");
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Błąd podczas dodawania przedmiotu: {Error}", errorContent);
                        await ShowErrorSnackbar("Nie udało się dodać przedmiotu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dodawania przedmiotu");
                await ShowErrorSnackbar("Wystąpił błąd podczas dodawania przedmiotu");
            }
        }
        
        private async Task EditSubjectAsync(Subject? subject)
        {
            if (subject == null) return;
            
            try
            {
                var editVm = App.ServiceProvider.GetRequiredService<SubjectEditViewModel>();
                await editVm.InitializeAsync();
                editVm.DialogTitle = "Edytuj przedmiot";
                editVm.LoadSubject(subject);
                
                var dialog = new Views.Subjects.SubjectEditDialog { DataContext = editVm };
                var result = await DialogHost.Show(dialog);
                
                if (result is bool success && success && editVm.IsValid)
                {
                    var updateDto = new
                    {
                        Name = editVm.Name,
                        Code = editVm.Code,
                        Description = editVm.Description,
                        Hours = editVm.Hours,
                        DefaultSchoolTypeId = editVm.SelectedSchoolType?.Id,
                        Category = editVm.Category,
                        IsActive = true
                    };
                    
                    var json = JsonSerializer.Serialize(updateDto);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var response = await _httpClient.PutAsync($"api/v1.0/Subjects/{subject.Id}", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadSubjectsAsync();
                        await ShowSuccessSnackbar("Przedmiot został zaktualizowany pomyślnie");
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Błąd podczas aktualizacji przedmiotu: {Error}", errorContent);
                        await ShowErrorSnackbar("Nie udało się zaktualizować przedmiotu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas edycji przedmiotu");
                await ShowErrorSnackbar("Wystąpił błąd podczas edycji przedmiotu");
            }
        }
        
        private async Task DeleteSubjectAsync(Subject? subject)
        {
            if (subject == null) return;
            
            try
            {
                var confirmationDialog = new Views.Common.ConfirmationDialog
                {
                    DataContext = new
                    {
                        Title = "Potwierdzenie usunięcia",
                        Message = $"Czy na pewno chcesz usunąć przedmiot '{subject.Name}'?\n\nUwaga: Ta operacja spowoduje dezaktywację przedmiotu.",
                        ConfirmText = "USUŃ",
                        CancelText = "ANULUJ",
                        IsDestructive = true
                    }
                };
                
                var result = await DialogHost.Show(confirmationDialog);
                
                if (result is bool confirmed && confirmed)
                {
                    var response = await _httpClient.DeleteAsync($"api/v1.0/Subjects/{subject.Id}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        await LoadSubjectsAsync();
                        await ShowSuccessSnackbar("Przedmiot został usunięty pomyślnie");
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Błąd podczas usuwania przedmiotu: {Error}", errorContent);
                        await ShowErrorSnackbar("Nie udało się usunąć przedmiotu");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania przedmiotu");
                await ShowErrorSnackbar("Wystąpił błąd podczas usuwania przedmiotu");
            }
        }
        
        private async Task ImportCsvAsync()
        {
            try
            {
                var importVm = App.ServiceProvider.GetRequiredService<SubjectImportViewModel>();
                await importVm.InitializeAsync();
                
                var dialog = new Views.Subjects.SubjectImportDialog { DataContext = importVm };
                var result = await DialogHost.Show(dialog);
                
                if (result is bool success && success)
                {
                    await LoadSubjectsAsync();
                    await ShowSuccessSnackbar("Import przedmiotów zakończony pomyślnie");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas importu CSV");
                await ShowErrorSnackbar("Wystąpił błąd podczas importu CSV");
            }
        }
        
        private async Task ExportCsvAsync()
        {
            try
            {
                // TODO: Implement CSV export functionality
                await ShowInfoSnackbar("Funkcja eksportu CSV będzie dostępna wkrótce");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas eksportu CSV");
                await ShowErrorSnackbar("Wystąpił błąd podczas eksportu CSV");
            }
        }
        
        private async Task ViewTeachersAsync(Subject? subject)
        {
            if (subject == null) return;
            
            try
            {
                var response = await _httpClient.GetAsync($"api/v1.0/Subjects/{subject.Id}/teachers");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var teachers = JsonSerializer.Deserialize<List<User>>(json, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    var teachersDialog = new Views.Subjects.SubjectTeachersDialog
                    {
                        DataContext = new
                        {
                            SubjectName = subject.Name,
                            Teachers = teachers ?? new List<User>()
                        }
                    };
                    
                    await DialogHost.Show(teachersDialog);
                }
                else
                {
                    await ShowErrorSnackbar("Nie udało się pobrać listy nauczycieli");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania nauczycieli dla przedmiotu");
                await ShowErrorSnackbar("Wystąpił błąd podczas pobierania nauczycieli");
            }
        }
        
        private async Task ShowSuccessSnackbar(string message)
        {
            await Task.Run(() => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // TODO: Implement proper snackbar using Material Design
                _logger.LogInformation("Success: {Message}", message);
            }));
        }
        
        private async Task ShowErrorSnackbar(string message)
        {
            await Task.Run(() => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // TODO: Implement proper snackbar using Material Design
                _logger.LogError("Error: {Message}", message);
            }));
        }
        
        private async Task ShowInfoSnackbar(string message)
        {
            await Task.Run(() => System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // TODO: Implement proper snackbar using Material Design
                _logger.LogInformation("Info: {Message}", message);
            }));
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