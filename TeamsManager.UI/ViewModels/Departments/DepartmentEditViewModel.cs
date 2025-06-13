using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.Departments
{
    /// <summary>
    /// Tryby pracy formularza działu
    /// </summary>
    public enum DepartmentEditMode
    {
        Add,    // Dodawanie nowego działu
        View,   // Tylko podgląd działu
        Edit    // Edycja istniejącego działu
    }

    /// <summary>
    /// ViewModel dla okna dodawania/edycji/podglądu działu
    /// </summary>
    public class DepartmentEditViewModel : BaseViewModel
    {
        private readonly IDepartmentService _departmentService;
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly ILogger<DepartmentEditViewModel> _logger;
        private readonly IUIDialogService _uiDialogService;
        private readonly ITeamService _teamService;
        
        private Department _model;
        private DepartmentEditMode _mode;
        private ObservableCollection<OrganizationalUnit> _availableOrganizationalUnits;
        private bool _isLoading;
        private string? _errorMessage;
        private string? _statusMessage;
        private string? _generatedCode;
        private bool _hasCodeConflict;
        private string? _codeConflictMessage;
        private bool _organizationalUnitSelectionMade;
        
        // Nowe właściwości dla ulepszonego error handlingu
        private bool _hasNameConflict;
        private string? _nameConflictMessage;
        private bool _hasTeamsAssigned;
        private string? _teamsValidationMessage;
        private bool _canDeactivate = true;

        // Kopie robocze danych - nie modyfikują oryginalnego Model do momentu zapisania
        private string? _workingName;
        private string? _workingOrganizationalUnitId;
        private bool _workingIsActive;
        private string? _workingDescription;
        private int? _workingSortOrder;

        // ID nowo utworzonego działu (tylko dla trybu Add)
        private string? _createdDepartmentId;

        public DepartmentEditViewModel(
            IDepartmentService departmentService,
            IOrganizationalUnitService organizationalUnitService,
            ILogger<DepartmentEditViewModel> logger,
            IUIDialogService uiDialogService,
            ITeamService teamService)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _organizationalUnitService = organizationalUnitService ?? throw new ArgumentNullException(nameof(organizationalUnitService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _uiDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));

            _model = new Department();
            _mode = DepartmentEditMode.Add;
            _availableOrganizationalUnits = new ObservableCollection<OrganizationalUnit>();

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(Cancel);
            EditCommand = new RelayCommand(SwitchToEditMode);

            // Load available organizational units
            _ = LoadAvailableOrganizationalUnitsAsync();
            
            // Nasłuchuj zmian w nazwie działu
            PropertyChanged += OnPropertyChanged;
        }

        #region Properties

        /// <summary>
        /// Model działu do edycji
        /// </summary>
        public Department Model
        {
            get => _model;
            set
            {
                if (SetProperty(ref _model, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(SaveButtonText));
                    UpdateCommandStates();
                    
                    // Generuj kod automatycznie przy zmianie modelu
                    _ = GenerateAndValidateCodeAsync();
                }
            }
        }

        /// <summary>
        /// Tryb pracy formularza
        /// </summary>
        public DepartmentEditMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(SaveButtonText));
                    OnPropertyChanged(nameof(IsEditMode));
                    OnPropertyChanged(nameof(IsViewMode));
                    OnPropertyChanged(nameof(IsAddMode));
                    OnPropertyChanged(nameof(ShowHierarchyInfo));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Dostępne jednostki organizacyjne
        /// </summary>
        public ObservableCollection<OrganizationalUnit> AvailableOrganizationalUnits
        {
            get => _availableOrganizationalUnits;
            set => SetProperty(ref _availableOrganizationalUnits, value);
        }

        /// <summary>
        /// Czy formularz jest w trybie ładowania
        /// </summary>
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

        /// <summary>
        /// Komunikat błędu
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Komunikat statusu
        /// </summary>
        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Czy jest błąd
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Tytuł okna w zależności od trybu
        /// </summary>
        public string WindowTitle => Mode switch
        {
            DepartmentEditMode.Add => "Dodaj nowy dział",
            DepartmentEditMode.View => $"Podgląd działu: {Model?.Name ?? ""}",
            DepartmentEditMode.Edit => $"Edytuj dział: {Model?.Name ?? ""}",
            _ => "Dział"
        };

        /// <summary>
        /// Tekst przycisku zapisu w zależności od trybu
        /// </summary>
        public string SaveButtonText => Mode switch
        {
            DepartmentEditMode.Add => "DODAJ",
            DepartmentEditMode.Edit => "ZAPISZ",
            DepartmentEditMode.View => "ZAMKNIJ",
            _ => "OK"
        };

        /// <summary>
        /// Czy formularz jest w trybie edycji (pola edytowalne)
        /// </summary>
        public bool IsEditMode => Mode == DepartmentEditMode.Add || Mode == DepartmentEditMode.Edit;

        /// <summary>
        /// Czy formularz jest w trybie tylko do odczytu
        /// </summary>
        public bool IsViewMode => Mode == DepartmentEditMode.View;

        /// <summary>
        /// Czy formularz jest w trybie dodawania
        /// </summary>
        public bool IsAddMode => Mode == DepartmentEditMode.Add;

        /// <summary>
        /// Czy pokazać informacje o hierarchii
        /// Pokazuj tylko w trybie edycji dla działów które mają przypisaną jednostkę organizacyjną
        /// </summary>
        public bool ShowHierarchyInfo => Mode == DepartmentEditMode.Edit && !string.IsNullOrEmpty(Model?.Id) && !string.IsNullOrEmpty(Model?.OrganizationalUnitId);

        /// <summary>
        /// Czy można zapisać
        /// </summary>
        public bool CanSave => !IsLoading && IsEditMode && !string.IsNullOrWhiteSpace(DepartmentName) && !HasCodeConflict && !HasNameConflict && CanEditFields && CanDeactivate;

        /// <summary>
        /// Czy można edytować pola
        /// </summary>
        public bool CanEditFields => IsEditMode && (Mode != DepartmentEditMode.Add || IsOrganizationalUnitSelected);

        /// <summary>
        /// Czy jednostka organizacyjna została wybrana (w trybie dodawania)
        /// W trybie dodawania sprawdzamy czy użytkownik dokonał wyboru
        /// </summary>
        public bool IsOrganizationalUnitSelected => !IsAddMode || _organizationalUnitSelectionMade;

        /// <summary>
        /// Automatycznie wygenerowany kod działu
        /// </summary>
        public string? GeneratedCode
        {
            get => _generatedCode;
            set => SetProperty(ref _generatedCode, value);
        }

        /// <summary>
        /// Czy istnieje konflikt kodu działu
        /// </summary>
        public bool HasCodeConflict
        {
            get => _hasCodeConflict;
            set
            {
                if (SetProperty(ref _hasCodeConflict, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Komunikat o konflikcie kodu
        /// </summary>
        public string? CodeConflictMessage
        {
            get => _codeConflictMessage;
            set => SetProperty(ref _codeConflictMessage, value);
        }

        /// <summary>
        /// Czy istnieje konflikt nazwy działu
        /// </summary>
        public bool HasNameConflict
        {
            get => _hasNameConflict;
            set
            {
                if (SetProperty(ref _hasNameConflict, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Komunikat o konflikcie nazwy
        /// </summary>
        public string? NameConflictMessage
        {
            get => _nameConflictMessage;
            set => SetProperty(ref _nameConflictMessage, value);
        }

        /// <summary>
        /// Czy dział ma przypisane zespoły (blokuje deaktywację)
        /// </summary>
        public bool HasTeamsAssigned
        {
            get => _hasTeamsAssigned;
            set => SetProperty(ref _hasTeamsAssigned, value);
        }

        /// <summary>
        /// Komunikat walidacji zespołów
        /// </summary>
        public string? TeamsValidationMessage
        {
            get => _teamsValidationMessage;
            set => SetProperty(ref _teamsValidationMessage, value);
        }

        /// <summary>
        /// Czy można deaktywować dział (nie ma zespołów)
        /// </summary>
        public bool CanDeactivate
        {
            get => _canDeactivate;
            set
            {
                if (SetProperty(ref _canDeactivate, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// ID nowo utworzonego działu (dostępne tylko po pomyślnym utworzeniu w trybie Add)
        /// </summary>
        public string? CreatedDepartmentId => _createdDepartmentId;

        /// <summary>
        /// Czy istnieją jakiekolwiek błędy walidacji
        /// </summary>
        public bool HasValidationErrors => HasCodeConflict || HasNameConflict || HasTeamsAssigned;

        /// <summary>
        /// Nazwa działu - wrapper z automatycznym generowaniem kodu
        /// </summary>
        public string? DepartmentName
        {
            get => _workingName ?? Model?.Name;
            set
            {
                if (_workingName != value)
                {
                    _workingName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    _ = GenerateAndValidateCodeAsync();
                    _ = ValidateNameConflictAsync();
                }
            }
        }

        /// <summary>
        /// Jednostka organizacyjna - wrapper z automatycznym generowaniem kodu
        /// </summary>
        public string? OrganizationalUnitId
        {
            get => _workingOrganizationalUnitId ?? Model?.OrganizationalUnitId ?? string.Empty; // Zwróć pusty string dla UI jeśli null
            set
            {
                // Konwertuj pusty string na null dla bazy danych
                var normalizedValue = string.IsNullOrEmpty(value) ? null : value;
                
                if (_workingOrganizationalUnitId != normalizedValue)
                {
                    _workingOrganizationalUnitId = normalizedValue;
                    
                    // Oznacz że użytkownik dokonał wyboru
                    if (IsAddMode)
                    {
                        _organizationalUnitSelectionMade = true;
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(CanEditFields));
                    OnPropertyChanged(nameof(IsOrganizationalUnitSelected));
                    _ = GenerateAndValidateCodeAsync();
                    _ = ValidateNameConflictAsync(); // Walidacja nazw po zmianie jednostki organizacyjnej
                }
            }
        }

        /// <summary>
        /// Wrapper dla właściwości IsActive z walidacją zespołów
        /// </summary>
        public bool DepartmentIsActive
        {
            get => _workingIsActive != default ? _workingIsActive : (Model?.IsActive ?? true);
            set
            {
                var currentValue = _workingIsActive != default ? _workingIsActive : (Model?.IsActive ?? true);
                if (currentValue != value)
                {
                    // Jeśli próbujemy deaktywować dział, sprawdź zespoły
                    if (!value && currentValue)
                    {
                        _ = ValidateTeamsBeforeDeactivationAsync(value);
                    }
                    else
                    {
                        _workingIsActive = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(CanSave));
                        
                        // Wyczyść komunikaty walidacji zespołów jeśli aktywujemy dział
                        if (value)
                        {
                            HasTeamsAssigned = false;
                            TeamsValidationMessage = null;
                            CanDeactivate = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Opis działu - wrapper używający kopii roboczej
        /// </summary>
        public string? Description
        {
            get => _workingDescription ?? Model?.Description;
            set
            {
                if (_workingDescription != value)
                {
                    _workingDescription = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// Kolejność sortowania - wrapper używający kopii roboczej
        /// </summary>
        public int SortOrder
        {
            get => _workingSortOrder ?? Model?.SortOrder ?? 0;
            set
            {
                if (_workingSortOrder != value)
                {
                    _workingSortOrder = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje formularz w trybie dodawania nowego działu
        /// </summary>
        public void InitializeForAdd(string? organizationalUnitId = null)
        {
            ResetViewModel();
            Mode = DepartmentEditMode.Add;
            Model = new Department
            {
                IsActive = true,
                SortOrder = 0,
                OrganizationalUnitId = organizationalUnitId
            };
            ClearMessages();
            StatusMessage = "Wprowadź dane nowego działu";
            
            // Jeśli przekazano organizationalUnitId, oznacz że wybór został dokonany
            // Jeśli nie, użytkownik będzie musiał wybrać z listy
            _organizationalUnitSelectionMade = !string.IsNullOrEmpty(organizationalUnitId);
            
            // Powiadom o zmianie właściwości
            RefreshAllProperties();
            
            // Wygeneruj kod po ustawieniu modelu
            _ = GenerateAndValidateCodeAsync();
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie podglądu działu
        /// </summary>
        public async Task InitializeForViewAsync(string departmentId)
        {
            ResetViewModel();
            Mode = DepartmentEditMode.View;
            await LoadDepartmentAsync(departmentId);
            StatusMessage = "Podgląd danych działu";
            RefreshAllProperties();
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie edycji działu
        /// </summary>
        public async Task InitializeForEditAsync(string departmentId)
        {
            ResetViewModel();
            Mode = DepartmentEditMode.Edit;
            await LoadDepartmentAsync(departmentId);
            StatusMessage = "Wprowadź zmiany w danych działu";
            RefreshAllProperties();
        }

        #endregion

        #region Private Methods

        private async Task LoadDepartmentAsync(string departmentId)
        {
            if (string.IsNullOrEmpty(departmentId)) return;

            IsLoading = true;
            ClearMessages();

            try
            {
                var department = await _departmentService.GetDepartmentByIdAsync(departmentId);
                if (department != null)
                {
                    Model = department;
                    RefreshAllProperties();
                }
                else
                {
                    ErrorMessage = "Nie znaleziono działu o podanym identyfikatorze";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading department {DepartmentId}", departmentId);
                ErrorMessage = $"Błąd podczas ładowania działu: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAvailableOrganizationalUnitsAsync()
        {
            try
            {
                var organizationalUnits = await _organizationalUnitService.GetAllOrganizationalUnitsAsync();
                
                // Filtruj tylko aktywne jednostki organizacyjne
                var availableUnits = organizationalUnits.Where(ou => ou.IsActive).ToList();

                AvailableOrganizationalUnits.Clear();

                foreach (var unit in availableUnits.OrderBy(ou => ou.FullPath))
                {
                    AvailableOrganizationalUnits.Add(unit);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available organizational units");
                // Nie blokujemy formularza, tylko logujemy błąd
            }
        }

        private async Task SaveAsync()
        {
            if (!CanSave) return;

            // W trybie View tylko zamykamy okno
            if (IsViewMode)
            {
                Cancel();
                return;
            }

            IsLoading = true;
            ClearMessages();

            try
            {
                // Zastosuj zmiany z kopii roboczych do oryginalnego Model przed zapisaniem
                ApplyWorkingChangesToModel();
                
                if (IsAddMode)
                {
                    // Tymczasowo używamy CreateDepartmentAsync z parentDepartmentId = null, 
                    // a potem aktualizujemy OrganizationalUnitId
                    var createdDepartment = await _departmentService.CreateDepartmentAsync(
                        Model.Name, 
                        Model.Description ?? string.Empty, 
                        null, // parentDepartmentId - nie używamy już hierarchii działów
                        Model.DepartmentCode);
                    
                    if (createdDepartment != null)
                    {
                        // Aktualizuj wszystkie pola, które nie są obsługiwane przez CreateDepartmentAsync
                        createdDepartment.SortOrder = Model.SortOrder;
                        createdDepartment.IsActive = Model.IsActive;
                        createdDepartment.OrganizationalUnitId = Model.OrganizationalUnitId; // Ustaw jednostkę organizacyjną
                        
                        await _departmentService.UpdateDepartmentAsync(createdDepartment);
                        
                        _logger.LogInformation("Created new department: {DepartmentName}", Model.Name);
                        StatusMessage = "Dział został pomyślnie utworzony";
                        _createdDepartmentId = createdDepartment.Id;
                    }
                    else
                    {
                        throw new InvalidOperationException("Nie udało się utworzyć działu");
                    }
                }
                else if (Mode == DepartmentEditMode.Edit)
                {
                    await _departmentService.UpdateDepartmentAsync(Model);
                    _logger.LogInformation("Updated department: {DepartmentName}", Model.Name);
                    StatusMessage = "Dział został pomyślnie zaktualizowany";
                }

                // Zamknij okno po pomyślnym zapisie
                await Task.Delay(1000); // Krótka pauza żeby użytkownik zobaczył komunikat
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving department");
                ErrorMessage = $"Błąd podczas zapisywania działu: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Kopiuje zmiany z kopii roboczych do oryginalnego Model
        /// </summary>
        private void ApplyWorkingChangesToModel()
        {
            if (Model == null) return;
            
            // Zastosuj zmiany tylko jeśli zostały wprowadzone
            if (_workingName != null)
            {
                Model.Name = _workingName;
            }
            
            if (_workingOrganizationalUnitId != null)
            {
                Model.OrganizationalUnitId = _workingOrganizationalUnitId;
            }
            
            if (_workingIsActive != default)
            {
                Model.IsActive = _workingIsActive;
            }
            
            if (_workingDescription != null)
            {
                Model.Description = _workingDescription;
            }
            
            if (_workingSortOrder != null)
            {
                Model.SortOrder = _workingSortOrder.Value;
            }
        }

        private async void Cancel()
        {
            // W trybie edycji - porzuć zmiany i wróć do oryginalnych danych
            if (Mode == DepartmentEditMode.Edit && !string.IsNullOrEmpty(Model?.Id))
            {
                try
                {
                    // Przeładuj oryginalne dane z bazy
                    await LoadDepartmentAsync(Model.Id);
                    
                    // Resetuj wszystkie flagi walidacji
                    ResetViewModel();
                    
                    // Odśwież wszystkie właściwości
                    RefreshAllProperties();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading department data during cancel");
                    ErrorMessage = "Błąd podczas anulowania zmian";
                }
            }
            
            RequestClose?.Invoke(false);
        }

        private void SwitchToEditMode()
        {
            if (Mode == DepartmentEditMode.View)
            {
                Mode = DepartmentEditMode.Edit;
                StatusMessage = "Wprowadź zmiany w danych działu";
                RefreshAllProperties();
            }
        }

        private void ResetViewModel()
        {
            // Resetuj wszystkie flagi i komunikaty
            _organizationalUnitSelectionMade = false;
            _hasCodeConflict = false;
            _hasNameConflict = false;
            _hasTeamsAssigned = false;
            _canDeactivate = true;
            
            // Wyczyść kopie robocze - powróć do oryginalnych wartości z Model
            _workingName = null;
            _workingOrganizationalUnitId = null;
            _workingIsActive = default;
            _workingDescription = null;
            _workingSortOrder = null;
            
            ClearMessages();
            
            // Resetuj komunikaty walidacji
            _generatedCode = null;
            _codeConflictMessage = null;
            _nameConflictMessage = null;
            _teamsValidationMessage = null;
        }

        private void RefreshAllProperties()
        {
            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsOrganizationalUnitSelected));
            OnPropertyChanged(nameof(DepartmentName));
            OnPropertyChanged(nameof(OrganizationalUnitId));
            OnPropertyChanged(nameof(DepartmentIsActive));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(SortOrder));
            OnPropertyChanged(nameof(ShowHierarchyInfo));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(GeneratedCode));
            OnPropertyChanged(nameof(HasCodeConflict));
            OnPropertyChanged(nameof(CodeConflictMessage));
            OnPropertyChanged(nameof(HasNameConflict));
            OnPropertyChanged(nameof(NameConflictMessage));
            OnPropertyChanged(nameof(HasTeamsAssigned));
            OnPropertyChanged(nameof(TeamsValidationMessage));
            OnPropertyChanged(nameof(CanDeactivate));
            UpdateCommandStates();
        }

        private void ClearMessages()
        {
            ErrorMessage = null;
            StatusMessage = null;
        }

        private void UpdateCommandStates()
        {
            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Reaguj na zmiany w nazwie działu lub dziale nadrzędnym
            if (e.PropertyName == nameof(Model) && Model != null)
            {
                _ = GenerateAndValidateCodeAsync();
            }
        }

        /// <summary>
        /// Normalizuje nazwę działu - usuwa polskie znaki, spacje i znaki specjalne
        /// </summary>
        private string NormalizeDepartmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Usuń polskie znaki
            var normalizedString = name.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            
            // Usuń spacje i znaki specjalne, zostaw tylko litery i cyfry
            result = new string(result.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            return result;
        }

        /// <summary>
        /// Generuje kod działu na podstawie hierarchii
        /// </summary>
        private async Task<string> GenerateDepartmentCodeAsync()
        {
            var workingName = _workingName ?? Model?.Name;
            if (string.IsNullOrWhiteSpace(workingName))
                return string.Empty;

            var codeParts = new List<string>();
            
            // Pobierz kod jednostki organizacyjnej
            var workingOrgUnitId = _workingOrganizationalUnitId ?? Model?.OrganizationalUnitId;
            if (!string.IsNullOrEmpty(workingOrgUnitId))
            {
                try
                {
                    var organizationalUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(workingOrgUnitId);
                    if (organizationalUnit != null && !string.IsNullOrEmpty(organizationalUnit.Name))
                    {
                        // Używamy znormalizowanej nazwy jednostki organizacyjnej jako prefiksu
                        var normalizedUnitName = NormalizeDepartmentName(organizationalUnit.Name);
                        if (!string.IsNullOrEmpty(normalizedUnitName))
                        {
                            codeParts.Add(normalizedUnitName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting organizational unit for code generation");
                }
            }

            // Dodaj znormalizowaną nazwę tego działu
            var normalizedName = NormalizeDepartmentName(workingName);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                codeParts.Add(normalizedName);
            }

            return string.Join("-", codeParts);
        }



        /// <summary>
        /// Sprawdza czy istnieje konflikt kodu w tej samej jednostce organizacyjnej
        /// </summary>
        private async Task<bool> CheckCodeConflictAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            try
            {
                var allDepartments = await _departmentService.GetAllDepartmentsAsync();
                
                // Sprawdź czy istnieje inny dział z tym samym kodem w tej samej jednostce organizacyjnej
                var conflictingDepartments = allDepartments.Where(d => 
                    d.DepartmentCode == code && 
                    d.OrganizationalUnitId == Model.OrganizationalUnitId &&
                    d.Id != Model.Id // Wyklucz siebie (w przypadku edycji)
                ).ToList();

                return conflictingDepartments.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking code conflict");
                return false;
            }
        }

        /// <summary>
        /// Generuje kod i sprawdza konflikty
        /// </summary>
        private async Task GenerateAndValidateCodeAsync()
        {
            try
            {
                // Generuj kod
                var code = await GenerateDepartmentCodeAsync();
                GeneratedCode = code;
                
                // Aktualizuj kod w modelu
                if (Model != null)
                {
                    Model.DepartmentCode = code;
                }

                // Sprawdź konflikty
                if (!string.IsNullOrEmpty(code))
                {
                    var hasConflict = await CheckCodeConflictAsync(code);
                    HasCodeConflict = hasConflict;
                    
                    if (hasConflict)
                    {
                        CodeConflictMessage = $"Dział o kodzie '{code}' już istnieje w tej jednostce organizacyjnej";
                    }
                    else
                    {
                        CodeConflictMessage = null;
                    }
                }
                else
                {
                    HasCodeConflict = false;
                    CodeConflictMessage = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating department code");
                GeneratedCode = string.Empty;
                HasCodeConflict = false;
                CodeConflictMessage = null;
            }
        }

        private async Task ValidateNameConflictAsync()
        {
            if (string.IsNullOrEmpty(Model?.Name))
            {
                HasNameConflict = false;
                NameConflictMessage = null;
                return;
            }

            try
            {
                var allDepartments = await _departmentService.GetAllDepartmentsAsync();
                
                // Sprawdź konflikty w tej samej jednostce organizacyjnej
                var conflictingDepartments = allDepartments.Where(d => 
                    d.Name.Equals(Model.Name, StringComparison.OrdinalIgnoreCase) && 
                    d.OrganizationalUnitId == Model.OrganizationalUnitId &&
                    d.Id != Model.Id // Wyklucz siebie (w przypadku edycji)
                ).ToList();

                if (conflictingDepartments.Any())
                {
                    HasNameConflict = true;
                    
                    // Znajdź nazwę jednostki organizacyjnej
                    if (!string.IsNullOrEmpty(Model.OrganizationalUnitId))
                    {
                        try
                        {
                            var organizationalUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(Model.OrganizationalUnitId);
                            var unitName = organizationalUnit?.Name ?? "wybranej jednostce organizacyjnej";
                            NameConflictMessage = $"Podana nazwa już istnieje w jednostce: {unitName}";
                        }
                        catch
                        {
                            NameConflictMessage = "Podana nazwa już istnieje w wybranej jednostce organizacyjnej";
                        }
                    }
                    else
                    {
                        NameConflictMessage = "Dział o podanej nazwie już istnieje";
                    }
                }
                else
                {
                    HasNameConflict = false;
                    NameConflictMessage = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking name conflict");
                HasNameConflict = false;
                NameConflictMessage = null;
            }
        }

        /// <summary>
        /// Sprawdza czy dział ma przypisane zespoły przed deaktywacją
        /// </summary>
        private async Task ValidateTeamsBeforeDeactivationAsync(bool newActiveState)
        {
            if (string.IsNullOrEmpty(Model?.Id))
            {
                Model.IsActive = newActiveState;
                OnPropertyChanged(nameof(DepartmentIsActive));
                OnPropertyChanged(nameof(CanSave));
                return;
            }

            try
            {
                // Sprawdź bezpośrednio czy dział ma przypisane aktywne zespoły
                var teamsInDepartment = await _teamService.GetTeamsByDepartmentAsync(Model.Id);
                var activeTeams = teamsInDepartment.Where(t => t.IsActive).ToList();

                if (activeTeams.Any())
                {
                    HasTeamsAssigned = true;
                    CanDeactivate = false;
                    
                    TeamsValidationMessage = $"Nie można deaktywować działu. Przypisanych jest {activeTeams.Count} aktywnych zespołów: {string.Join(", ", activeTeams.Take(3).Select(t => t.DisplayName))}{(activeTeams.Count > 3 ? "..." : "")}.";
                    
                    // Nie zmieniaj stanu - pozostaw aktywny
                    OnPropertyChanged(nameof(DepartmentIsActive));
                    OnPropertyChanged(nameof(CanSave));
                    return;
                }

                // Jeśli nie ma bezpośrednio przypisanych zespołów, sprawdź czy użytkownicy z działu są członkami zespołów
                var usersInDepartment = await _departmentService.GetUsersInDepartmentAsync(Model.Id);
                var activeUsers = usersInDepartment.Where(u => u.IsActive).ToList();

                if (activeUsers.Any())
                {
                    // Sprawdź czy którzyś z użytkowników ma aktywne członkostwa w zespołach
                    var usersWithTeams = activeUsers.Where(u => u.TeamMemberships?.Any(tm => tm.IsMembershipActive) == true).ToList();
                    
                    if (usersWithTeams.Any())
                    {
                        HasTeamsAssigned = true;
                        CanDeactivate = false;
                        
                        var teamCount = usersWithTeams.SelectMany(u => u.TeamMemberships.Where(tm => tm.IsMembershipActive)).Count();
                        TeamsValidationMessage = $"Nie można deaktywować działu. Użytkownicy z tego działu są członkami {teamCount} aktywnych zespołów.";
                        
                        // Nie zmieniaj stanu - pozostaw aktywny
                        OnPropertyChanged(nameof(DepartmentIsActive));
                        OnPropertyChanged(nameof(CanSave));
                        return;
                    }
                }

                // Jeśli nie ma konfliktów, pozwól na deaktywację
                HasTeamsAssigned = false;
                TeamsValidationMessage = null;
                CanDeactivate = true;
                
                Model.IsActive = newActiveState;
                OnPropertyChanged(nameof(DepartmentIsActive));
                OnPropertyChanged(nameof(CanSave));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating teams before deactivation");
                
                // W przypadku błędu, pozwól na deaktywację ale zaloguj ostrzeżenie
                _logger.LogWarning("Could not validate teams for department {DepartmentId}, allowing deactivation", Model.Id);
                HasTeamsAssigned = false;
                TeamsValidationMessage = null;
                CanDeactivate = true;
                
                Model.IsActive = newActiveState;
                OnPropertyChanged(nameof(DepartmentIsActive));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event wywoływany gdy okno ma zostać zamknięte
        /// </summary>
        public event Action<bool>? RequestClose;

        #endregion
    }
} 