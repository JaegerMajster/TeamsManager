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
        private readonly ILogger<DepartmentEditViewModel> _logger;
        
        private Department _model;
        private DepartmentEditMode _mode;
        private ObservableCollection<Department> _availableParentDepartments;
        private bool _isLoading;
        private string? _errorMessage;
        private string? _statusMessage;
        private string? _generatedCode;
        private bool _hasCodeConflict;
        private string? _codeConflictMessage;
        private bool _parentDepartmentSelectionMade;

        public DepartmentEditViewModel(
            IDepartmentService departmentService,
            ILogger<DepartmentEditViewModel> logger,
            IUIDialogService uiDialogService)
        {
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            UIDialogService = uiDialogService ?? throw new ArgumentNullException(nameof(uiDialogService));

            _model = new Department();
            _mode = DepartmentEditMode.Add;
            _availableParentDepartments = new ObservableCollection<Department>();

            // Initialize commands
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => CanSave);
            CancelCommand = new RelayCommand(Cancel);

            // Load available parent departments
            _ = LoadAvailableParentDepartmentsAsync();
            
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
        /// Dostępne działy nadrzędne
        /// </summary>
        public ObservableCollection<Department> AvailableParentDepartments
        {
            get => _availableParentDepartments;
            set => SetProperty(ref _availableParentDepartments, value);
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
        /// Czy pokazywać informacje o hierarchii (tylko dla istniejących działów)
        /// </summary>
        public bool ShowHierarchyInfo => !IsAddMode && !string.IsNullOrEmpty(Model?.Id);

        /// <summary>
        /// Czy można zapisać
        /// </summary>
        public bool CanSave => !IsLoading && IsEditMode && !string.IsNullOrWhiteSpace(DepartmentName) && !HasCodeConflict && CanEditFields;

        /// <summary>
        /// Czy można edytować pola (w trybie dodawania wymaga wyboru działu nadrzędnego)
        /// </summary>
        public bool CanEditFields => IsViewMode || IsEditMode && (Mode != DepartmentEditMode.Add || IsParentDepartmentSelected);

        /// <summary>
        /// Czy dział nadrzędny został wybrany (w trybie dodawania)
        /// W trybie dodawania sprawdzamy czy użytkownik dokonał wyboru - nawet jeśli wybrał "Brak"
        /// </summary>
        public bool IsParentDepartmentSelected => !IsAddMode || _parentDepartmentSelectionMade;

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
        /// Nazwa działu - wrapper z automatycznym generowaniem kodu
        /// </summary>
        public string? DepartmentName
        {
            get => Model?.Name;
            set
            {
                if (Model != null && Model.Name != value)
                {
                    Model.Name = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    _ = GenerateAndValidateCodeAsync();
                }
            }
        }

        /// <summary>
        /// Dział nadrzędny - wrapper z automatycznym generowaniem kodu
        /// </summary>
        public string? ParentDepartmentId
        {
            get => Model?.ParentDepartmentId;
            set
            {
                if (Model != null && Model.ParentDepartmentId != value)
                {
                    Model.ParentDepartmentId = value;
                    
                    // Oznacz że użytkownik dokonał wyboru (nawet jeśli wybrał "Brak")
                    if (IsAddMode)
                    {
                        _parentDepartmentSelectionMade = true;
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSave));
                    OnPropertyChanged(nameof(CanEditFields));
                    OnPropertyChanged(nameof(IsParentDepartmentSelected));
                    _ = GenerateAndValidateCodeAsync();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje formularz w trybie dodawania nowego działu
        /// </summary>
        public void InitializeForAdd(string? parentDepartmentId = null)
        {
            Mode = DepartmentEditMode.Add;
            Model = new Department
            {
                IsActive = true,
                SortOrder = 0,
                ParentDepartmentId = parentDepartmentId
            };
            ClearMessages();
            StatusMessage = "Wprowadź dane nowego działu";
            
            // Zresetuj flagę wyboru działu nadrzędnego
            _parentDepartmentSelectionMade = !string.IsNullOrEmpty(parentDepartmentId);
            
            // Powiadom o zmianie właściwości
            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsParentDepartmentSelected));
            OnPropertyChanged(nameof(DepartmentName));
            OnPropertyChanged(nameof(ParentDepartmentId));
            
            // Wygeneruj kod po ustawieniu modelu
            _ = GenerateAndValidateCodeAsync();
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie podglądu działu
        /// </summary>
        public async Task InitializeForViewAsync(string departmentId)
        {
            Mode = DepartmentEditMode.View;
            await LoadDepartmentAsync(departmentId);
            StatusMessage = "Podgląd danych działu";
            
            // Powiadom o zmianie właściwości wrapper
            OnPropertyChanged(nameof(DepartmentName));
            OnPropertyChanged(nameof(ParentDepartmentId));
            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsParentDepartmentSelected));
        }

        /// <summary>
        /// Inicjalizuje formularz w trybie edycji działu
        /// </summary>
        public async Task InitializeForEditAsync(string departmentId)
        {
            Mode = DepartmentEditMode.Edit;
            await LoadDepartmentAsync(departmentId);
            StatusMessage = "Wprowadź zmiany w danych działu";
            
            // Powiadom o zmianie właściwości wrapper
            OnPropertyChanged(nameof(DepartmentName));
            OnPropertyChanged(nameof(ParentDepartmentId));
            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsParentDepartmentSelected));
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
                    
                    // Powiadom o zmianie właściwości wrapper
                    OnPropertyChanged(nameof(DepartmentName));
                    OnPropertyChanged(nameof(ParentDepartmentId));
                    OnPropertyChanged(nameof(CanEditFields));
                    OnPropertyChanged(nameof(IsParentDepartmentSelected));
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

        private async Task LoadAvailableParentDepartmentsAsync()
        {
            try
            {
                var departments = await _departmentService.GetAllDepartmentsAsync();
                
                // Filtruj działy - nie można wybrać siebie jako rodzica ani swoich potomków
                var availableDepartments = departments.Where(d => 
                    d.IsActive && 
                    (IsAddMode || (d.Id != Model.Id && !Model.IsParentOf(d.Id)))
                ).ToList();

                AvailableParentDepartments.Clear();
                
                // Dodaj opcję "Brak" dla działów głównych
                AvailableParentDepartments.Add(new Department 
                { 
                    Id = string.Empty, 
                    Name = "-- Brak (dział główny) --" 
                });

                foreach (var dept in availableDepartments.OrderBy(d => d.FullPath))
                {
                    AvailableParentDepartments.Add(dept);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available parent departments");
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
                if (IsAddMode)
                {
                    var createdDepartment = await _departmentService.CreateDepartmentAsync(
                        Model.Name, 
                        Model.Description ?? string.Empty, 
                        Model.ParentDepartmentId, 
                        Model.DepartmentCode);
                    
                    if (createdDepartment != null)
                    {
                        // Aktualizuj dodatkowe pola, które nie są obsługiwane przez CreateDepartmentAsync
                        createdDepartment.SortOrder = Model.SortOrder;
                        createdDepartment.IsActive = Model.IsActive;
                        
                        await _departmentService.UpdateDepartmentAsync(createdDepartment);
                        
                        _logger.LogInformation("Created new department: {DepartmentName}", Model.Name);
                        StatusMessage = "Dział został pomyślnie utworzony";
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

        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        private void ClearMessages()
        {
            ErrorMessage = null;
            StatusMessage = null;
        }

        private void UpdateCommandStates()
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
            if (string.IsNullOrWhiteSpace(Model?.Name))
                return string.Empty;

            var codeParts = new List<string>();
            
            // Jeśli ma działu nadrzędnego, pobierz jego hierarchię
            if (!string.IsNullOrEmpty(Model.ParentDepartmentId))
            {
                try
                {
                    var parentDepartment = await _departmentService.GetDepartmentByIdAsync(Model.ParentDepartmentId);
                    if (parentDepartment != null)
                    {
                        // Pobierz kod rodzica (który już zawiera pełną hierarchię)
                        if (!string.IsNullOrEmpty(parentDepartment.DepartmentCode))
                        {
                            codeParts.Add(parentDepartment.DepartmentCode);
                        }
                        else
                        {
                            // Jeśli rodzic nie ma kodu, wygeneruj go rekurencyjnie
                            var parentCode = await GenerateCodeForDepartment(parentDepartment);
                            if (!string.IsNullOrEmpty(parentCode))
                            {
                                codeParts.Add(parentCode);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting parent department for code generation");
                }
            }

            // Dodaj znormalizowaną nazwę tego działu
            var normalizedName = NormalizeDepartmentName(Model.Name);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                codeParts.Add(normalizedName);
            }

            return string.Join("-", codeParts);
        }

        /// <summary>
        /// Generuje kod dla konkretnego działu (metoda pomocnicza)
        /// </summary>
        private async Task<string> GenerateCodeForDepartment(Department department)
        {
            var codeParts = new List<string>();
            
            // Jeśli ma działu nadrzędnego, pobierz jego kod
            if (!string.IsNullOrEmpty(department.ParentDepartmentId))
            {
                try
                {
                    var parentDepartment = await _departmentService.GetDepartmentByIdAsync(department.ParentDepartmentId);
                    if (parentDepartment != null)
                    {
                        var parentCode = await GenerateCodeForDepartment(parentDepartment);
                        if (!string.IsNullOrEmpty(parentCode))
                        {
                            codeParts.Add(parentCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting parent department for code generation");
                }
            }

            // Dodaj znormalizowaną nazwę tego działu
            var normalizedName = NormalizeDepartmentName(department.Name);
            if (!string.IsNullOrEmpty(normalizedName))
            {
                codeParts.Add(normalizedName);
            }

            return string.Join("-", codeParts);
        }

        /// <summary>
        /// Sprawdza czy istnieje konflikt kodu na tym samym poziomie hierarchii
        /// </summary>
        private async Task<bool> CheckCodeConflictAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            try
            {
                var allDepartments = await _departmentService.GetAllDepartmentsAsync();
                
                // Sprawdź czy istnieje inny dział z tym samym kodem na tym samym poziomie
                var conflictingDepartments = allDepartments.Where(d => 
                    d.DepartmentCode == code && 
                    d.ParentDepartmentId == Model.ParentDepartmentId &&
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
                        CodeConflictMessage = $"Dział o kodzie '{code}' już istnieje na tym poziomie hierarchii";
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

        #endregion

        #region Events

        /// <summary>
        /// Event wywoływany gdy okno ma zostać zamknięte
        /// </summary>
        public event Action<bool>? RequestClose;

        #endregion
    }
} 