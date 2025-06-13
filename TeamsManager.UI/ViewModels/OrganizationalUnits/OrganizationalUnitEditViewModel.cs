using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.ViewModels.OrganizationalUnits
{
    /// <summary>
    /// Tryby pracy formularza jednostki organizacyjnej
    /// </summary>
    public enum OrganizationalUnitEditMode
    {
        Add,    // Dodawanie nowej jednostki
        View,   // Tylko podgląd jednostki
        Edit    // Edycja istniejącej jednostki
    }

    /// <summary>
    /// ViewModel dla okna dodawania/edycji/podglądu jednostki organizacyjnej
    /// </summary>
    public class OrganizationalUnitEditViewModel : BaseViewModel
    {
        private readonly IOrganizationalUnitService _organizationalUnitService;
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<OrganizationalUnitEditViewModel> _logger;
        private readonly IUIDialogService _uiDialogService;
        
        private OrganizationalUnit _model;
        private OrganizationalUnitEditMode _mode;
        private ObservableCollection<OrganizationalUnit> _availableParentUnits;
        private ObservableCollection<Department> _assignedDepartments;
        private bool _isLoading;
        private string? _errorMessage;
        private string? _statusMessage;
        private bool _hasNameConflict;
        private string? _nameConflictMessage;
        private bool _hasDepartmentsAssigned;
        private string? _departmentsValidationMessage;
        private bool _canDelete = true;
        private bool _parentUnitSelectionMade;
        private string? _generatedCode;
        private bool _hasCodeConflict;
        private string? _codeConflictMessage;

        // Kopie robocze danych - nie modyfikują oryginalnego Model do momentu zapisania
        private string? _workingName;
        private string? _workingParentUnitId;
        private bool? _workingIsActive;
        private string? _workingDescription;
        private int? _workingSortOrder;

        // ID nowo utworzonej jednostki (tylko dla trybu Add)
        private string? _createdUnitId;

        public OrganizationalUnitEditViewModel(
            IOrganizationalUnitService organizationalUnitService,
            IDepartmentService departmentService,
            ILogger<OrganizationalUnitEditViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _organizationalUnitService = organizationalUnitService ?? throw new ArgumentNullException(nameof(organizationalUnitService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _uiDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            _model = new OrganizationalUnit();
            _mode = OrganizationalUnitEditMode.Add;
            _availableParentUnits = new ObservableCollection<OrganizationalUnit>();
            _assignedDepartments = new ObservableCollection<Department>();

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(Cancel);
            EditCommand = new RelayCommand(SwitchToEditMode);

            // Load available parent units
            _ = LoadAvailableParentUnitsAsync();
            
            // Generuj kod jednostki
            _ = GenerateAndValidateCodeAsync();
            
            // Nasłuchuj zmian w nazwie jednostki
            PropertyChanged += OnPropertyChanged;
        }

        #region Properties

        /// <summary>
        /// Model jednostki organizacyjnej do edycji
        /// </summary>
        public OrganizationalUnit Model
        {
            get => _model;
            set
            {
                if (SetProperty(ref _model, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(SaveButtonText));
                    UpdateCommandStates();
                    
                    // Załaduj przypisane działy
                    _ = LoadAssignedDepartmentsAsync();
                }
            }
        }

        /// <summary>
        /// Tryb pracy formularza
        /// </summary>
        public OrganizationalUnitEditMode Mode
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
        /// Dostępne jednostki nadrzędne
        /// </summary>
        public ObservableCollection<OrganizationalUnit> AvailableParentUnits
        {
            get => _availableParentUnits;
            set => SetProperty(ref _availableParentUnits, value);
        }

        /// <summary>
        /// Działy przypisane do jednostki
        /// </summary>
        public ObservableCollection<Department> AssignedDepartments
        {
            get => _assignedDepartments;
            set => SetProperty(ref _assignedDepartments, value);
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
            OrganizationalUnitEditMode.Add => "Dodaj nową jednostkę organizacyjną",
            OrganizationalUnitEditMode.View => $"Podgląd jednostki: {Model?.Name ?? ""}",
            OrganizationalUnitEditMode.Edit => $"Edytuj jednostkę: {Model?.Name ?? ""}",
            _ => "Jednostka organizacyjna"
        };

        /// <summary>
        /// Tekst przycisku zapisu w zależności od trybu
        /// </summary>
        public string SaveButtonText => Mode switch
        {
            OrganizationalUnitEditMode.Add => "DODAJ",
            OrganizationalUnitEditMode.Edit => "ZAPISZ",
            OrganizationalUnitEditMode.View => "ZAMKNIJ",
            _ => "OK"
        };

        /// <summary>
        /// Czy formularz jest w trybie edycji (pola edytowalne)
        /// </summary>
        public bool IsEditMode => Mode == OrganizationalUnitEditMode.Add || Mode == OrganizationalUnitEditMode.Edit;

        /// <summary>
        /// Czy formularz jest w trybie tylko do odczytu
        /// </summary>
        public bool IsViewMode => Mode == OrganizationalUnitEditMode.View;

        /// <summary>
        /// Czy formularz jest w trybie dodawania
        /// </summary>
        public bool IsAddMode => Mode == OrganizationalUnitEditMode.Add;

        /// <summary>
        /// Czy pokazać informacje o hierarchii
        /// </summary>
        public bool ShowHierarchyInfo => Mode == OrganizationalUnitEditMode.Edit && !string.IsNullOrEmpty(Model?.Id) && !string.IsNullOrEmpty(Model?.ParentUnitId);

        /// <summary>
        /// Czy można zapisać
        /// </summary>
        public bool CanSave => !IsLoading && IsEditMode && !string.IsNullOrWhiteSpace(UnitName) && !HasNameConflict && !HasCodeConflict && CanEditFields && CanDelete;

        /// <summary>
        /// Czy można edytować pola
        /// </summary>
        public bool CanEditFields => IsEditMode && (Mode != OrganizationalUnitEditMode.Add || IsParentUnitSelected);

        /// <summary>
        /// Czy jednostka nadrzędna została wybrana (w trybie dodawania)
        /// </summary>
        public bool IsParentUnitSelected => !IsAddMode || _parentUnitSelectionMade;

        /// <summary>
        /// Czy ma konflikt nazwy
        /// </summary>
        public bool HasNameConflict
        {
            get => _hasNameConflict;
            set
            {
                if (SetProperty(ref _hasNameConflict, value))
                {
                    OnPropertyChanged(nameof(CanSave));
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
        /// Czy ma przypisane działy
        /// </summary>
        public bool HasDepartmentsAssigned
        {
            get => _hasDepartmentsAssigned;
            set => SetProperty(ref _hasDepartmentsAssigned, value);
        }

        /// <summary>
        /// Komunikat walidacji działów
        /// </summary>
        public string? DepartmentsValidationMessage
        {
            get => _departmentsValidationMessage;
            set => SetProperty(ref _departmentsValidationMessage, value);
        }

        /// <summary>
        /// Czy można usunąć jednostkę (nie ma podjednostek ani działów)
        /// </summary>
        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                if (SetProperty(ref _canDelete, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// ID nowo utworzonej jednostki (dostępne tylko po pomyślnym utworzeniu w trybie Add)
        /// </summary>
        public string? CreatedUnitId => _createdUnitId;

        /// <summary>
        /// Czy istnieją jakiekolwiek błędy walidacji
        /// </summary>
        public bool HasValidationErrors => HasNameConflict || HasDepartmentsAssigned || HasCodeConflict;

        /// <summary>
        /// Wygenerowany kod jednostki organizacyjnej
        /// </summary>
        public string? GeneratedCode
        {
            get => _generatedCode;
            set => SetProperty(ref _generatedCode, value);
        }

        /// <summary>
        /// Czy istnieje konflikt kodu
        /// </summary>
        public bool HasCodeConflict
        {
            get => _hasCodeConflict;
            set
            {
                if (SetProperty(ref _hasCodeConflict, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(HasValidationErrors));
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
        /// Nazwa jednostki - wrapper z automatyczną walidacją
        /// </summary>
        public string? UnitName
        {
            get => _workingName ?? Model?.Name;
            set
            {
                if (_workingName != value)
                {
                    _workingName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    _ = ValidateNameConflictAsync();
                    _ = GenerateAndValidateCodeAsync();
                }
            }
        }

        /// <summary>
        /// Jednostka nadrzędna - wrapper z automatyczną walidacją
        /// </summary>
        public string? ParentUnitId
        {
            get 
            {
                var value = _workingParentUnitId ?? Model?.ParentUnitId;
                // Konwertuj null na pusty string dla ComboBox (opcja "Brak")
                return value ?? string.Empty;
            }
            set
            {
                // Konwertuj pusty string na null dla bazy danych
                var normalizedValue = string.IsNullOrEmpty(value) ? null : value;
                
                if (_workingParentUnitId != normalizedValue)
                {
                    _workingParentUnitId = normalizedValue;
                    
                    // Oznacz że użytkownik dokonał wyboru (nawet jeśli wybrał "Brak")
                    if (IsAddMode)
                    {
                        _parentUnitSelectionMade = true;
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(CanEditFields));
                    OnPropertyChanged(nameof(IsParentUnitSelected));
                    _ = ValidateNameConflictAsync(); // Walidacja nazw po zmianie jednostki nadrzędnej
                    _ = GenerateAndValidateCodeAsync(); // Generuj kod po zmianie jednostki nadrzędnej
                }
            }
        }

        /// <summary>
        /// Czy jednostka jest aktywna - wrapper używający kopii roboczej
        /// </summary>
        public bool UnitIsActive
        {
            get => _workingIsActive ?? Model?.IsActive ?? true;
            set
            {
                if (_workingIsActive != value)
                {
                    _workingIsActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        /// <summary>
        /// Opis jednostki - wrapper używający kopii roboczej
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

        /// <summary>
        /// Liczba przypisanych działów
        /// </summary>
        public int AssignedDepartmentsCount => AssignedDepartments?.Count ?? 0;

        /// <summary>
        /// Poziom w hierarchii
        /// </summary>
        public int Level => Model?.Level ?? 0;

        /// <summary>
        /// Pełna ścieżka w hierarchii
        /// </summary>
        public string FullPath => Model?.FullPath ?? "";

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand EditCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje formularz w trybie dodawania nowej jednostki
        /// </summary>
        public void InitializeForAdd(string? parentUnitId = null)
        {
            ResetViewModel();
            Mode = OrganizationalUnitEditMode.Add;
            Model = new OrganizationalUnit
            {
                IsActive = true,
                SortOrder = 0,
                ParentUnitId = parentUnitId
            };
            ClearMessages();
            StatusMessage = "Wprowadź dane nowej jednostki organizacyjnej";
            
            // Oznacz że wybór został dokonany - zawsze, bo mamy domyślną opcję "Brak"
            _parentUnitSelectionMade = true;
            
            // Powiadom o zmianie właściwości
            RefreshAllProperties();
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie podglądu jednostki
        /// </summary>
        public async Task InitializeForViewAsync(string unitId)
        {
            ResetViewModel();
            Mode = OrganizationalUnitEditMode.View;
            await LoadOrganizationalUnitAsync(unitId);
            StatusMessage = "Podgląd danych jednostki organizacyjnej";
            RefreshAllProperties();
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie edycji jednostki
        /// </summary>
        public async Task InitializeForEditAsync(string unitId)
        {
            ResetViewModel();
            Mode = OrganizationalUnitEditMode.Edit;
            await LoadOrganizationalUnitAsync(unitId);
            StatusMessage = "Wprowadź zmiany w danych jednostki organizacyjnej";
            RefreshAllProperties();
        }

        #endregion

        #region Private Methods

        private async Task LoadOrganizationalUnitAsync(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return;

            IsLoading = true;
            ClearMessages();

            try
            {
                var unit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(unitId);
                if (unit != null)
                {
                    Model = unit;
                    RefreshAllProperties();
                }
                else
                {
                    ErrorMessage = "Nie znaleziono jednostki organizacyjnej o podanym identyfikatorze";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading organizational unit {UnitId}", unitId);
                ErrorMessage = $"Błąd podczas ładowania jednostki organizacyjnej: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAvailableParentUnitsAsync()
        {
            try
            {
                var units = await _organizationalUnitService.GetAllOrganizationalUnitsAsync();
                
                // Filtruj jednostki - nie można wybrać siebie jako rodzica
                var availableUnits = units.Where(u => u.Id != Model?.Id).ToList();
                
                AvailableParentUnits.Clear();
                
                // Dodaj opcję "Brak Jednostki nadrzędnej" na początku
                var noneOption = new OrganizationalUnit
                {
                    Id = string.Empty, // Pusty string oznacza brak jednostki nadrzędnej
                    Name = "Brak Jednostki nadrzędnej",
                    IsActive = true
                };
                AvailableParentUnits.Add(noneOption);
                
                // Dodaj pozostałe jednostki
                foreach (var unit in availableUnits.OrderBy(u => u.FullPath))
                {
                    AvailableParentUnits.Add(unit);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available parent units");
                ErrorMessage = "Błąd podczas ładowania dostępnych jednostek nadrzędnych";
            }
        }

        private async Task LoadAssignedDepartmentsAsync()
        {
            if (string.IsNullOrEmpty(Model?.Id)) return;

            try
            {
                var departments = await _organizationalUnitService.GetDepartmentsByOrganizationalUnitAsync(Model.Id);
                
                AssignedDepartments.Clear();
                foreach (var dept in departments)
                {
                    AssignedDepartments.Add(dept);
                }

                HasDepartmentsAssigned = AssignedDepartments.Count > 0;
                DepartmentsValidationMessage = HasDepartmentsAssigned 
                    ? $"Jednostka ma przypisane {AssignedDepartments.Count} działów"
                    : null;

                OnPropertyChanged(nameof(AssignedDepartmentsCount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assigned departments for unit {UnitId}", Model.Id);
            }
        }

        private async Task ValidateNameConflictAsync()
        {
            if (string.IsNullOrWhiteSpace(UnitName))
            {
                HasNameConflict = false;
                NameConflictMessage = null;
                return;
            }

            try
            {
                var isUnique = await _organizationalUnitService.IsNameUniqueAsync(
                    UnitName, 
                    ParentUnitId, 
                    Model?.Id);

                HasNameConflict = !isUnique;
                NameConflictMessage = HasNameConflict 
                    ? "Jednostka o tej nazwie już istnieje w tej lokalizacji hierarchii"
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating unit name uniqueness");
                HasNameConflict = false;
                NameConflictMessage = null;
            }
        }

        private async Task SaveAsync()
        {
            if (!CanSave) return;

            IsLoading = true;
            ClearMessages();

            try
            {
                // Przygotuj model do zapisu
                PrepareModelForSave();

                OrganizationalUnit savedUnit;

                if (Mode == OrganizationalUnitEditMode.Add)
                {
                    savedUnit = await _organizationalUnitService.CreateOrganizationalUnitAsync(Model);
                    _createdUnitId = savedUnit.Id;
                    StatusMessage = "Jednostka organizacyjna została pomyślnie utworzona";
                    _logger.LogInformation("Created organizational unit {UnitName} (ID: {UnitId})", savedUnit.Name, savedUnit.Id);
                }
                else
                {
                    savedUnit = await _organizationalUnitService.UpdateOrganizationalUnitAsync(Model);
                    StatusMessage = "Jednostka organizacyjna została pomyślnie zaktualizowana";
                    _logger.LogInformation("Updated organizational unit {UnitName} (ID: {UnitId})", savedUnit.Name, savedUnit.Id);
                }

                Model = savedUnit;
                ResetWorkingCopies();
                RefreshAllProperties();

                // Zamknij okno z sukcesem
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving organizational unit");
                ErrorMessage = $"Błąd podczas zapisywania jednostki organizacyjnej: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void PrepareModelForSave()
        {
            // Zastosuj zmiany z kopii roboczych do modelu
            if (_workingName != null) Model.Name = _workingName;
            if (_workingParentUnitId != null) Model.ParentUnitId = _workingParentUnitId;
            if (_workingIsActive.HasValue) Model.IsActive = _workingIsActive.Value;
            if (_workingDescription != null) Model.Description = _workingDescription;
            if (_workingSortOrder.HasValue) Model.SortOrder = _workingSortOrder.Value;
            
            // Ustaw wygenerowany kod
            if (!string.IsNullOrEmpty(GeneratedCode))
            {
                Model.Code = GeneratedCode;
            }
        }

        private void ResetWorkingCopies()
        {
            _workingName = null;
            _workingParentUnitId = null;
            _workingIsActive = null;
            _workingDescription = null;
            _workingSortOrder = null;
        }

        private void ResetViewModel()
        {
            ResetWorkingCopies();
            HasNameConflict = false;
            NameConflictMessage = null;
            HasDepartmentsAssigned = false;
            DepartmentsValidationMessage = null;
            HasCodeConflict = false;
            CodeConflictMessage = null;
            GeneratedCode = null;
            CanDelete = true;
            _parentUnitSelectionMade = false;
            _createdUnitId = null;
        }

        private async void Cancel()
        {
            // W trybie edycji - porzuć zmiany i wróć do oryginalnych danych
            if (Mode == OrganizationalUnitEditMode.Edit && !string.IsNullOrEmpty(Model?.Id))
            {
                try
                {
                    // Przeładuj oryginalne dane z bazy
                    await LoadOrganizationalUnitAsync(Model.Id);
                    
                    // Resetuj wszystkie flagi walidacji
                    ResetViewModel();
                    
                    // Odśwież wszystkie właściwości
                    RefreshAllProperties();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reloading organizational unit data during cancel");
                    ErrorMessage = "Błąd podczas anulowania zmian";
                }
            }
            
            RequestClose?.Invoke(false);
        }

        private void SwitchToEditMode()
        {
            if (Mode == OrganizationalUnitEditMode.View)
            {
                Mode = OrganizationalUnitEditMode.Edit;
                StatusMessage = "Wprowadź zmiany w danych jednostki organizacyjnej";
                RefreshAllProperties();
            }
        }

        private void RefreshAllProperties()
        {
            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsParentUnitSelected));
            OnPropertyChanged(nameof(UnitName));
            OnPropertyChanged(nameof(ParentUnitId));
            OnPropertyChanged(nameof(UnitIsActive));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(SortOrder));
            OnPropertyChanged(nameof(ShowHierarchyInfo));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(HasNameConflict));
            OnPropertyChanged(nameof(NameConflictMessage));
            OnPropertyChanged(nameof(HasDepartmentsAssigned));
            OnPropertyChanged(nameof(DepartmentsValidationMessage));
            OnPropertyChanged(nameof(HasCodeConflict));
            OnPropertyChanged(nameof(CodeConflictMessage));
            OnPropertyChanged(nameof(GeneratedCode));
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(AssignedDepartmentsCount));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(FullPath));
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
            // Reaguj na zmiany w modelu
            if (e.PropertyName == nameof(Model) && Model != null)
            {
                _ = ValidateNameConflictAsync();
                _ = GenerateAndValidateCodeAsync();
            }
        }

        /// <summary>
        /// Generuje kod jednostki organizacyjnej na podstawie hierarchii
        /// </summary>
        private async Task<string> GenerateOrganizationalUnitCodeAsync()
        {
            var workingName = _workingName ?? Model?.Name;
            if (string.IsNullOrWhiteSpace(workingName))
                return string.Empty;

            var codeParts = new List<string>();
            
            // Pobierz kod jednostki nadrzędnej
            var workingParentUnitId = _workingParentUnitId ?? Model?.ParentUnitId;
            if (!string.IsNullOrEmpty(workingParentUnitId))
            {
                try
                {
                    var parentUnit = await _organizationalUnitService.GetOrganizationalUnitByIdAsync(workingParentUnitId);
                    if (parentUnit != null && !string.IsNullOrEmpty(parentUnit.Code))
                    {
                        // Używamy kodu jednostki nadrzędnej jako prefiksu
                        codeParts.Add(parentUnit.Code);
                    }
                    else if (parentUnit != null && !string.IsNullOrEmpty(parentUnit.Name))
                    {
                        // Jeśli jednostka nadrzędna nie ma kodu, użyj znormalizowanej nazwy
                        var normalizedParentName = NormalizeUnitName(parentUnit.Name);
                        if (!string.IsNullOrEmpty(normalizedParentName))
                        {
                            codeParts.Add(normalizedParentName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting parent organizational unit for code generation");
                }
            }

            // Dodaj znormalizowaną nazwę tej jednostki
            var normalizedName = NormalizeUnitName(workingName);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                codeParts.Add(normalizedName);
            }

            return string.Join("-", codeParts);
        }

        /// <summary>
        /// Normalizuje nazwę jednostki do użycia w kodzie
        /// Formatowanie: PascalCase z pierwszą wielką literą każdego słowa
        /// Przykład: "LO semestr I" => "LOSemestrI"
        /// </summary>
        private string NormalizeUnitName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Usuń polskie znaki i zamień na odpowiedniki
            var normalized = name
                .Replace("ą", "a").Replace("Ą", "A")
                .Replace("ć", "c").Replace("Ć", "C")
                .Replace("ę", "e").Replace("Ę", "E")
                .Replace("ł", "l").Replace("Ł", "L")
                .Replace("ń", "n").Replace("Ń", "N")
                .Replace("ó", "o").Replace("Ó", "O")
                .Replace("ś", "s").Replace("Ś", "S")
                .Replace("ź", "z").Replace("Ź", "Z")
                .Replace("ż", "z").Replace("Ż", "Z");

            // Usuń wszystko oprócz liter, cyfr i spacji
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-zA-Z0-9\s]", "");
            
            // Podziel na słowa i zastosuj formatowanie PascalCase
            var words = normalized.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new System.Text.StringBuilder();
            
            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    // Pierwsza litera wielka, reszta bez zmian (zachowujemy oryginalne wielkości)
                    var firstChar = char.ToUpperInvariant(word[0]);
                    var restOfWord = word.Length > 1 ? word.Substring(1) : "";
                    result.Append(firstChar + restOfWord);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Sprawdza czy istnieje konflikt kodu
        /// </summary>
        private async Task<bool> CheckCodeConflictAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            try
            {
                var allUnits = await _organizationalUnitService.GetAllOrganizationalUnitsAsync();
                
                // Sprawdź czy istnieje inna jednostka z tym samym kodem
                var conflictingUnits = allUnits.Where(u => 
                    u.Code == code && 
                    u.Id != Model.Id // Wyklucz siebie (w przypadku edycji)
                ).ToList();

                return conflictingUnits.Any();
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
                var code = await GenerateOrganizationalUnitCodeAsync();
                GeneratedCode = code;
                
                // Aktualizuj kod w modelu
                if (Model != null)
                {
                    Model.Code = code;
                }

                // Sprawdź konflikty
                if (!string.IsNullOrEmpty(code))
                {
                    var hasConflict = await CheckCodeConflictAsync(code);
                    HasCodeConflict = hasConflict;
                    
                    if (hasConflict)
                    {
                        CodeConflictMessage = $"Jednostka o kodzie '{code}' już istnieje";
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
                _logger.LogError(ex, "Error generating organizational unit code");
                GeneratedCode = string.Empty;
                HasCodeConflict = false;
                CodeConflictMessage = null;
            }
        }

        #endregion

        #region Events

        public event Action<bool>? RequestClose;

        #endregion
    }
} 