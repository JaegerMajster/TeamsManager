using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using System.Net.Http;
using System.Text.Json;
using System.Linq;

namespace TeamsManager.UI.ViewModels.Subjects
{
    public class SubjectEditViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SubjectEditViewModel> _logger;
        
        private Subject _subject = new();
        private ObservableCollection<SchoolType> _schoolTypes = new();
        private ObservableCollection<string> _categories = new();
        private SchoolType? _selectedSchoolType;
        private string _dialogTitle = "Edytuj przedmiot";
        private bool _isLoading;
        
        public SubjectEditViewModel(HttpClient httpClient, ILogger<SubjectEditViewModel> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            InitializeCategories();
        }
        
        #region Properties
        
        public string DialogTitle
        {
            get => _dialogTitle;
            set 
            { 
                _dialogTitle = value; 
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
        
        public string Name
        {
            get => _subject.Name;
            set 
            { 
                _subject.Name = value ?? string.Empty; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
            }
        }
        
        public string? Code
        {
            get => _subject.Code;
            set 
            { 
                _subject.Code = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
            }
        }
        
        public string? Description
        {
            get => _subject.Description;
            set 
            { 
                _subject.Description = value; 
                OnPropertyChanged();
            }
        }
        
        public int? Hours
        {
            get => _subject.Hours;
            set 
            { 
                _subject.Hours = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsValid));
            }
        }
        
        public string? Category
        {
            get => _subject.Category;
            set 
            { 
                _subject.Category = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<SchoolType> SchoolTypes
        {
            get => _schoolTypes;
            set 
            { 
                _schoolTypes = value; 
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<string> Categories
        {
            get => _categories;
            set 
            { 
                _categories = value; 
                OnPropertyChanged();
            }
        }
        
        public SchoolType? SelectedSchoolType
        {
            get => _selectedSchoolType;
            set 
            { 
                _selectedSchoolType = value; 
                _subject.DefaultSchoolTypeId = value?.Id;
                _subject.DefaultSchoolType = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Name) &&
                       (string.IsNullOrEmpty(Code) || Code.Length <= 10) &&
                       (!Hours.HasValue || Hours.Value >= 0);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        public async Task InitializeAsync()
        {
            await LoadSchoolTypesAsync();
        }
        
        public void LoadSubject(Subject subject)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            
            _subject = new Subject
            {
                Id = subject.Id,
                Name = subject.Name,
                Code = subject.Code,
                Description = subject.Description,
                Hours = subject.Hours,
                Category = subject.Category,
                DefaultSchoolTypeId = subject.DefaultSchoolTypeId,
                DefaultSchoolType = subject.DefaultSchoolType,
                IsActive = subject.IsActive,
                CreatedDate = subject.CreatedDate,
                ModifiedDate = subject.ModifiedDate,
                CreatedBy = subject.CreatedBy,
                ModifiedBy = subject.ModifiedBy
            };
            
            // Set selected school type
            if (!string.IsNullOrEmpty(subject.DefaultSchoolTypeId))
            {
                SelectedSchoolType = SchoolTypes.FirstOrDefault(st => st.Id == subject.DefaultSchoolTypeId);
            }
            
            // Notify all properties changed
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Code));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Hours));
            OnPropertyChanged(nameof(Category));
            OnPropertyChanged(nameof(SelectedSchoolType));
            OnPropertyChanged(nameof(IsValid));
        }
        
        public void ClearSubject()
        {
            _subject = new Subject();
            SelectedSchoolType = null;
            
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Code));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Hours));
            OnPropertyChanged(nameof(Category));
            OnPropertyChanged(nameof(SelectedSchoolType));
            OnPropertyChanged(nameof(IsValid));
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeCategories()
        {
            Categories = new ObservableCollection<string>
            {
                "Nauki ścisłe",
                "Języki obce", 
                "Przedmioty humanistyczne",
                "Przedmioty zawodowe",
                "Wychowanie fizyczne",
                "Inne"
            };
        }
        
        private async Task LoadSchoolTypesAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Ładowanie typów szkół dla przedmiotu");
                
                var response = await _httpClient.GetAsync("api/v1.0/SchoolTypes");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var schoolTypes = JsonSerializer.Deserialize<List<SchoolType>>(json, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                SchoolTypes.Clear();
                if (schoolTypes != null)
                {
                    foreach (var schoolType in schoolTypes.Where(st => st.IsActive))
                    {
                        SchoolTypes.Add(schoolType);
                    }
                }
                
                _logger.LogInformation("Załadowano {Count} typów szkół", schoolTypes?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania typów szkół");
                // Add a fallback or empty collection
                SchoolTypes.Clear();
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        #endregion
        
        #region IDataErrorInfo Implementation
        
        public string Error => string.Empty;
        
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Name):
                        if (string.IsNullOrWhiteSpace(Name))
                            return "Nazwa przedmiotu jest wymagana";
                        if (Name.Length > 200)
                            return "Nazwa przedmiotu może mieć maksymalnie 200 znaków";
                        break;
                        
                    case nameof(Code):
                        if (!string.IsNullOrEmpty(Code) && Code.Length > 10)
                            return "Kod przedmiotu może mieć maksymalnie 10 znaków";
                        break;
                        
                    case nameof(Hours):
                        if (Hours.HasValue && Hours.Value < 0)
                            return "Liczba godzin nie może być ujemna";
                        if (Hours.HasValue && Hours.Value > 10000)
                            return "Liczba godzin wydaje się za duża";
                        break;
                        
                    case nameof(Description):
                        if (!string.IsNullOrEmpty(Description) && Description.Length > 1000)
                            return "Opis może mieć maksymalnie 1000 znaków";
                        break;
                }
                return string.Empty;
            }
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