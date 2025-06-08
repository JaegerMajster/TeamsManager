using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Teams
{
    /// <summary>
    /// ViewModel dla edytora szablonów zespołów z live preview i pomocnikiem tokenów
    /// </summary>
    public class TeamTemplateEditorViewModel : BaseViewModel
    {
        private readonly ITeamTemplateService _teamTemplateService;
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly ILogger<TeamTemplateEditorViewModel> _logger;
        
        private TeamTemplate _template = new();
        private string _templateContent = string.Empty;
        private string _previewText = "Wprowadź szablon, aby zobaczyć podgląd...";
        private bool _isLoading;
        private bool _isSaving;
        private bool _hasValidationErrors;
        private string _editMode = "Nowy szablon";
        private string _windowTitle = "Edytor Szablonu Zespołu";

        /// <summary>
        /// Konstruktor ViewModelu
        /// </summary>
        public TeamTemplateEditorViewModel(
            ITeamTemplateService teamTemplateService,
            ISchoolTypeService schoolTypeService,
            ILogger<TeamTemplateEditorViewModel> logger)
        {
            _teamTemplateService = teamTemplateService ?? throw new ArgumentNullException(nameof(teamTemplateService));
            _schoolTypeService = schoolTypeService ?? throw new ArgumentNullException(nameof(schoolTypeService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Inicjalizacja kolekcji
            AvailableTokens = new ObservableCollection<PlaceholderToken>();
            ValidationErrors = new ObservableCollection<string>();
            SchoolTypes = new ObservableCollection<SchoolType>();

            // Inicjalizacja komend
            InitializeCommands();
            
            // Inicjalizacja tokenów
            InitializeTokens();
            
            // Inicjalizacja szablonu
            InitializeTemplate();
        }

        #region Properties

        /// <summary>
        /// Aktualnie edytowany szablon
        /// </summary>
        public TeamTemplate Template
        {
            get => _template;
            set 
            { 
                if (SetProperty(ref _template, value))
                {
                    if (value != null)
                    {
                        TemplateContent = value.Template;
                        UpdateEditMode();
                        UpdateWindowTitle();
                    }
                }
            }
        }

        /// <summary>
        /// Zawartość wzorca szablonu
        /// </summary>
        public string TemplateContent
        {
            get => _templateContent;
            set
            {
                if (SetProperty(ref _templateContent, value))
                {
                    UpdatePreview();
                    ValidateTemplate();
                }
            }
        }

        /// <summary>
        /// Tekst podglądu generowanej nazwy
        /// </summary>
        public string PreviewText
        {
            get => _previewText;
            private set => SetProperty(ref _previewText, value);
        }

        /// <summary>
        /// Czy trwa ładowanie danych
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Czy trwa zapisywanie
        /// </summary>
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        /// <summary>
        /// Czy są błędy walidacji
        /// </summary>
        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            private set => SetProperty(ref _hasValidationErrors, value);
        }

        /// <summary>
        /// Tryb edycji (Nowy szablon / Edycja szablonu)
        /// </summary>
        public string EditMode
        {
            get => _editMode;
            private set => SetProperty(ref _editMode, value);
        }

        /// <summary>
        /// Tytuł okna
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            private set => SetProperty(ref _windowTitle, value);
        }

        /// <summary>
        /// Czy można zapisać szablon
        /// </summary>
        public bool CanSave => !IsSaving && !IsLoading && !HasValidationErrors && !string.IsNullOrWhiteSpace(Template.Name) && !string.IsNullOrWhiteSpace(TemplateContent);

        /// <summary>
        /// Dostępne tokeny placeholderów
        /// </summary>
        public ObservableCollection<PlaceholderToken> AvailableTokens { get; }

        /// <summary>
        /// Błędy walidacji szablonu
        /// </summary>
        public ObservableCollection<string> ValidationErrors { get; }

        /// <summary>
        /// Dostępne typy szkół
        /// </summary>
        public ObservableCollection<SchoolType> SchoolTypes { get; }

        /// <summary>
        /// Wynik dialogu (null - nie zamknięto, true - zapisano, false - anulowano)
        /// </summary>
        public bool? DialogResult { get; private set; }

        #endregion

        #region Commands

        /// <summary>
        /// Komenda zapisania szablonu
        /// </summary>
        public ICommand SaveCommand { get; private set; } = null!;

        /// <summary>
        /// Komenda anulowania edycji
        /// </summary>
        public ICommand CancelCommand { get; private set; } = null!;

        /// <summary>
        /// Komenda wstawienia tokenu do wzorca
        /// </summary>
        public ICommand InsertTokenCommand { get; private set; } = null!;

        /// <summary>
        /// Komenda testowania z przykładowymi danymi
        /// </summary>
        public ICommand TestWithDataCommand { get; private set; } = null!;

        /// <summary>
        /// Komenda użycia przykładowych wartości w testowaniu
        /// </summary>
        public ICommand UseExampleValuesCommand { get; private set; } = null!;

        /// <summary>
        /// Komenda zastosowania danych testowych
        /// </summary>
        public ICommand ApplyTestDataCommand { get; private set; } = null!;

        #endregion

        #region Events

        /// <summary>
        /// Zdarzenie żądania zamknięcia okna
        /// </summary>
        public event Action? RequestClose;

        #endregion

        #region Initialization

        /// <summary>
        /// Inicjalizacja komend
        /// </summary>
        private void InitializeCommands()
        {
            SaveCommand = new AsyncRelayCommand(SaveTemplateAsync, _ => CanSave);
            CancelCommand = new RelayCommand(CancelEdit);
            InsertTokenCommand = new RelayCommand<PlaceholderToken>(InsertToken);
            TestWithDataCommand = new RelayCommand(TestWithData);
            UseExampleValuesCommand = new RelayCommand(UseExampleValues);
            ApplyTestDataCommand = new RelayCommand(ApplyTestData);
        }

        /// <summary>
        /// Inicjalizacja dostępnych tokenów
        /// </summary>
        private void InitializeTokens()
        {
            var tokens = new[]
            {
                new PlaceholderToken 
                { 
                    Name = "TypSzkoly", 
                    Description = "Typ szkoły (np. LO, ZSZ)", 
                    ExampleValue = "LO",
                    IsRequired = true
                },
                new PlaceholderToken 
                { 
                    Name = "Szkola", 
                    Description = "Nazwa szkoły", 
                    ExampleValue = "Szkoła Podstawowa nr 1",
                    IsRequired = true
                },
                new PlaceholderToken 
                { 
                    Name = "Oddzial", 
                    Description = "Oznaczenie oddziału/klasy", 
                    ExampleValue = "1A",
                    IsRequired = true
                },
                new PlaceholderToken 
                { 
                    Name = "Klasa", 
                    Description = "Numer klasy", 
                    ExampleValue = "1",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Przedmiot", 
                    Description = "Nazwa przedmiotu", 
                    ExampleValue = "Matematyka",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Nauczyciel", 
                    Description = "Imię i nazwisko nauczyciela", 
                    ExampleValue = "Jan Kowalski",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Rok", 
                    Description = "Rok szkolny", 
                    ExampleValue = "2024/2025",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Semestr", 
                    Description = "Numer semestru", 
                    ExampleValue = "I",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Kurs", 
                    Description = "Nazwa kursu", 
                    ExampleValue = "Podstawy programowania",
                    IsRequired = false
                },
                new PlaceholderToken 
                { 
                    Name = "Grupa", 
                    Description = "Oznaczenie grupy", 
                    ExampleValue = "Grupa 1",
                    IsRequired = false
                }
            };

            foreach (var token in tokens)
            {
                token.ValueChanged += (s, e) => UpdatePreview();
                AvailableTokens.Add(token);
            }
        }

        /// <summary>
        /// Inicjalizacja nowego szablonu
        /// </summary>
        private void InitializeTemplate()
        {
            Template = new TeamTemplate
            {
                IsActive = true,
                Language = "Polski",
                Separator = " - ",
                Category = "Ogólne"
            };
        }

        /// <summary>
        /// Inicjalizacja ViewModelu - ładowanie danych
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;

            try
            {
                await LoadSchoolTypesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd inicjalizacji edytora szablonów");
                ValidationErrors.Add($"Błąd inicjalizacji: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Ładowanie typów szkół
        /// </summary>
        private async Task LoadSchoolTypesAsync()
        {
            try
            {
                var schoolTypes = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
                SchoolTypes.Clear();
                
                foreach (var schoolType in schoolTypes.Where(st => st.IsActive))
                {
                    SchoolTypes.Add(schoolType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ładowania typów szkół");
                ValidationErrors.Add("Nie udało się załadować typów szkół");
            }
        }

        #endregion

        #region Preview and Validation

        /// <summary>
        /// Aktualizacja podglądu generowanej nazwy
        /// </summary>
        private void UpdatePreview()
        {
            if (string.IsNullOrWhiteSpace(TemplateContent))
            {
                PreviewText = "Wprowadź szablon, aby zobaczyć podgląd...";
                return;
            }

            try
            {
                var values = new Dictionary<string, string>();
                foreach (var token in AvailableTokens)
                {
                    values[token.Name] = !string.IsNullOrEmpty(token.CurrentValue) 
                        ? token.CurrentValue 
                        : token.ExampleValue;
                }

                var tempTemplate = new TeamTemplate { Template = TemplateContent };
                PreviewText = tempTemplate.GenerateTeamName(values);
            }
            catch (Exception ex)
            {
                PreviewText = $"Błąd generowania: {ex.Message}";
            }
        }

        /// <summary>
        /// Walidacja szablonu
        /// </summary>
        private void ValidateTemplate()
        {
            ValidationErrors.Clear();

            if (string.IsNullOrWhiteSpace(TemplateContent))
            {
                ValidationErrors.Add("Szablon nie może być pusty");
                HasValidationErrors = true;
                OnPropertyChanged(nameof(CanSave));
                return;
            }

            try
            {
                var tempTemplate = new TeamTemplate { Template = TemplateContent };
                var errors = tempTemplate.ValidateTemplate();

                foreach (var error in errors)
                {
                    ValidationErrors.Add(error);
                }

                HasValidationErrors = ValidationErrors.Count > 0;
                OnPropertyChanged(nameof(CanSave));
            }
            catch (Exception ex)
            {
                ValidationErrors.Add($"Błąd walidacji: {ex.Message}");
                HasValidationErrors = true;
                OnPropertyChanged(nameof(CanSave));
            }
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Zapisuje szablon zespołu
        /// </summary>
        private async Task SaveTemplateAsync()
        {
            if (!CanSave)
                return;

            try
            {
                IsSaving = true;
                
                if (string.IsNullOrEmpty(Template.Id))
                {
                    // Nowy szablon
                    var created = await _teamTemplateService.CreateTemplateAsync(
                        Template.Name,
                        TemplateContent,
                        Template.Description ?? string.Empty,
                        Template.IsUniversal,
                        Template.SchoolTypeId,
                        Template.Category);
                    
                    if (created != null)
                    {
                        DialogResult = true;
                        RequestClose?.Invoke();
                    }
                    else
                    {
                        MessageBox.Show("Nie udało się utworzyć szablonu", "Błąd", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // Aktualizacja
                    Template.Template = TemplateContent;
                    var success = await _teamTemplateService.UpdateTemplateAsync(Template);
                    
                    if (success)
                    {
                        DialogResult = true;
                        RequestClose?.Invoke();
                    }
                    else
                    {
                        MessageBox.Show("Nie udało się zaktualizować szablonu", "Błąd", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania szablonu");
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// Anuluje edycję
        /// </summary>
        private void CancelEdit()
        {
            DialogResult = false;
            RequestClose?.Invoke();
        }

        /// <summary>
        /// Wstawia token do wzorca szablonu
        /// </summary>
        private void InsertToken(PlaceholderToken? token)
        {
            if (token == null)
                return;

            var tokenText = token.DisplayName;
            
            // Wstawienie tokenu - sprawdzenie czy trzeba dodać spację
            if (!string.IsNullOrEmpty(TemplateContent))
            {
                // Jeśli ostatni znak to nie spacja ani separator, dodaj spację
                var lastChar = TemplateContent[TemplateContent.Length - 1];
                if (lastChar != ' ' && lastChar != '-' && lastChar != '_')
                {
                    TemplateContent += " ";
                }
                TemplateContent += tokenText;
            }
            else
            {
                TemplateContent = tokenText;
            }
            
            // Logowanie dla debugowania
            _logger.LogDebug("Wstawiono token {TokenName} do wzorca szablonu", token.Name);
        }

        /// <summary>
        /// Otwiera dialog testowania z danymi
        /// </summary>
        private void TestWithData()
        {
            try
            {
                // Tworzenie okna dialogowego
                var testDialog = new Window
                {
                    Title = "Testuj szablon z danymi",
                    Width = 520,
                    Height = 640,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.CanResize,
                    ShowInTaskbar = false,
                    Style = System.Windows.Application.Current.FindResource("BaseWindowStyle") as Style
                };

                // Tworzenie UserControl z danymi testowymi
                var testDataControl = new UserControls.Teams.TestDataDialog
                {
                    DataContext = this
                };

                testDialog.Content = testDataControl;
                
                // Ustawienie właściciela jeśli to możliwe
                var mainWindow = System.Windows.Application.Current.Windows
                    .OfType<Views.Shell.MainShellWindow>()
                    .FirstOrDefault();
                if (mainWindow != null)
                {
                    testDialog.Owner = mainWindow;
                }

                // Pokazanie dialogu
                testDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas otwierania dialogu testowego");
                
                // Fallback do prostszej wersji
                UseExampleValues();
            }
        }

        /// <summary>
        /// Używa przykładowych wartości do testowania
        /// </summary>
        private void UseExampleValues()
        {
            foreach (var token in AvailableTokens)
            {
                token.CurrentValue = token.ExampleValue;
            }
        }

        /// <summary>
        /// Zastosowuje dane testowe
        /// </summary>
        private void ApplyTestData()
        {
            UpdatePreview();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Aktualizuje tryb edycji
        /// </summary>
        private void UpdateEditMode()
        {
            EditMode = string.IsNullOrEmpty(Template.Id) ? "Nowy szablon" : "Edycja szablonu";
        }

        /// <summary>
        /// Aktualizuje tytuł okna
        /// </summary>
        private void UpdateWindowTitle()
        {
            if (string.IsNullOrEmpty(Template.Id))
            {
                WindowTitle = "Nowy szablon zespołu";
            }
            else
            {
                WindowTitle = $"Edycja szablonu: {Template.Name}";
            }
        }

        #endregion
    }
} 
