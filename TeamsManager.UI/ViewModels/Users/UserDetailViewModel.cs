using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.Models.ViewModels;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Users
{
    /// <summary>
    /// ViewModel dla okna szczegółów/edycji użytkownika.
    /// Obsługuje tryby tworzenia i edycji użytkowników.
    /// </summary>
    public class UserDetailViewModel : INotifyPropertyChanged
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly IDepartmentService _departmentService;  // ✅ NAPRAWKA: Dodano IDepartmentService
        private readonly ILogger<UserDetailViewModel> _logger;
        private readonly UserSchoolTypeAssignmentViewModel _userSchoolTypeAssignmentViewModel;
        
        private UserDetailModel _model;
        private ObservableCollection<Department> _departments;
        private bool _isEditMode;
        private string? _userId;
        private bool _isLoading;
        private string? _errorMessage;
        private string _statusMessage = string.Empty;
        private bool? _dialogResult;

        public UserDetailViewModel(
            ITeamsManagerApiService apiService,
            IDepartmentService departmentService,  // ✅ NAPRAWKA: Dodano IDepartmentService
            ILogger<UserDetailViewModel> logger,
            UserSchoolTypeAssignmentViewModel userSchoolTypeAssignmentViewModel)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));  // ✅ NAPRAWKA
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userSchoolTypeAssignmentViewModel = userSchoolTypeAssignmentViewModel ?? throw new ArgumentNullException(nameof(userSchoolTypeAssignmentViewModel));

            _model = new UserDetailModel();
            _departments = new ObservableCollection<Department>();

            // Subscribe to model property changes for validation
            _model.PropertyChanged += OnModelPropertyChanged;

            // Initialize commands
            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
            CancelCommand = new RelayCommand(Cancel);
            UploadAvatarCommand = new RelayCommand(UploadAvatar);
            RemoveAvatarCommand = new RelayCommand(RemoveAvatar, () => Model.HasAvatar);
        }

        #region Properties

        public UserDetailModel Model
        {
            get => _model;
            set
            {
                if (_model != value)
                {
                    if (_model != null)
                        _model.PropertyChanged -= OnModelPropertyChanged;
                    
                    _model = value;
                    
                    if (_model != null)
                        _model.PropertyChanged += OnModelPropertyChanged;
                    
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        public ObservableCollection<Department> Departments
        {
            get => _departments;
            set
            {
                _departments = value;
                OnPropertyChanged();
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            private set
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                UpdateCommandStates();
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                _dialogResult = value;
                OnPropertyChanged();
            }
        }

        public string WindowTitle => IsEditMode ? "Edytuj użytkownika" : "Nowy użytkownik";

        public string SaveButtonText => IsEditMode ? "ZAPISZ ZMIANY" : "UTWÓRZ UŻYTKOWNIKA";

        public bool HasErrors => GetValidationErrors().Any();

        /// <summary>
        /// ViewModel dla zarządzania przypisaniami do typów szkół
        /// </summary>
        public UserSchoolTypeAssignmentViewModel UserSchoolTypeAssignmentViewModel => _userSchoolTypeAssignmentViewModel;

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand UploadAvatarCommand { get; }
        public ICommand RemoveAvatarCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Inicjalizuje ViewModel dla edycji istniejącego użytkownika lub tworzenia nowego.
        /// </summary>
        /// <param name="userId">ID użytkownika do edycji (null dla nowego użytkownika)</param>
        public async Task InitializeAsync(string? userId = null)
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StatusMessage = "Ładowanie danych...";

                // Załaduj działy
                await LoadDepartmentsAsync();

                // Jeśli edycja, załaduj użytkownika
                if (!string.IsNullOrEmpty(userId))
                {
                    _userId = userId;
                    IsEditMode = true;
                    await LoadUserAsync(userId);
                }
                else
                {
                    IsEditMode = false;
                    StatusMessage = "Uzupełnij dane nowego użytkownika";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji widoku użytkownika");
                ErrorMessage = $"Błąd ładowania danych: {ex.Message}";
                StatusMessage = "Wystąpił błąd";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private async Task LoadDepartmentsAsync()
        {
            try
            {
                _logger.LogInformation("=== ROZPOCZĘCIE ŁADOWANIA DZIAŁÓW ===");
                _logger.LogDebug("✅ NAPRAWKA: Wywołanie IDepartmentService.GetAllDepartmentsAsync (bezpośrednio z bazy UI)...");
                
                var departments = await _departmentService.GetAllDepartmentsAsync();
                
                _logger.LogInformation("✅ NAPRAWKA: Wynik IDepartmentService.GetAllDepartmentsAsync: {DepartmentsCount} działów", 
                    departments?.Count() ?? 0);
                
                if (departments != null)
                {
                    foreach (var dept in departments)
                    {
                        _logger.LogDebug("Dział: ID={Id}, Name={Name}, IsActive={IsActive}", 
                            dept.Id, dept.Name, dept.IsActive);
                    }
                }
                
                Departments.Clear();
                _logger.LogDebug("Wyczyszczono kolekcję Departments");
                
                // Jeśli nie ma działów, dodaj domyślny dział
                if (departments == null || !departments.Any())
                {
                    _logger.LogWarning("⚠️ Brak działów w bazie danych. Może być potrzebna inicjalizacja danych.");
                    
                    // Dodaj domyślny dział jako fallback
                    var defaultDepartment = new Department
                    {
                        Id = "temp-default",
                        Name = "Brak działów - skontaktuj się z administratorem",
                        Description = "Domyślny dział tymczasowy",
                        IsActive = true
                    };
                    Departments.Add(defaultDepartment);
                    _logger.LogInformation("Dodano tymczasowy domyślny dział: {DepartmentId}", defaultDepartment.Id);
                }
                else
                {
                    var sortedDepartments = departments.OrderBy(d => d.Name).ToList();
                    _logger.LogDebug("Sortowanie działów według nazwy: {Count} działów", sortedDepartments.Count);
                    
                    foreach (var dept in sortedDepartments)
                    {
                        Departments.Add(dept);
                        _logger.LogDebug("Dodano dział do kolekcji: {DepartmentId} - {DepartmentName}", dept.Id, dept.Name);
                    }
                    _logger.LogInformation("✅ Załadowano {Count} działów do ComboBox", Departments.Count);
                }
                
                _logger.LogInformation("Aktualna zawartość kolekcji Departments: {Count} elementów", Departments.Count);
                foreach (var dept in Departments)
                {
                    _logger.LogDebug("  - {Id}: {Name}", dept.Id, dept.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 WYJĄTEK podczas ładowania działów: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                
                // Dodaj domyślny dział jako fallback w przypadku błędu
                Departments.Clear();
                var errorDepartment = new Department
                {
                    Id = "error-fallback",
                    Name = "Błąd ładowania działów - sprawdź połączenie",
                    Description = "Fallback dla błędu ładowania",
                    IsActive = true
                };
                Departments.Add(errorDepartment);
                _logger.LogInformation("Dodano fallback dział po błędzie: {DepartmentId}", errorDepartment.Id);
                
                // NIE rzucaj wyjątku ponownie - pozwól formularzowi się otworzyć z fallback działem
                _logger.LogInformation("Formularz może się otworzyć z fallback działem");
            }
            finally
            {
                _logger.LogInformation("=== ZAKOŃCZENIE ŁADOWANIA DZIAŁÓW ===");
            }
        }

        private async Task LoadUserAsync(string userId)
        {
            try
            {
                var user = await _apiService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    throw new InvalidOperationException($"Nie znaleziono użytkownika o ID: {userId}");
                }

                MapUserToModel(user);
                
                // Ustaw użytkownika w UserSchoolTypeAssignmentViewModel
                _userSchoolTypeAssignmentViewModel.CurrentUser = user;
                
                StatusMessage = "Dane użytkownika załadowane";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania użytkownika {UserId}", userId);
                throw;
            }
        }

        private void MapUserToModel(User user)
        {
            Model.FirstName = user.FirstName;
            Model.LastName = user.LastName;
            Model.Upn = user.UPN;
            Model.Role = user.Role;
            Model.DepartmentId = user.DepartmentId;
            Model.Phone = user.Phone;
            Model.AlternateEmail = user.AlternateEmail;
            Model.BirthDate = user.BirthDate;
            Model.EmploymentDate = user.EmploymentDate;
            Model.Position = user.Position;
            Model.Notes = user.Notes;
            Model.IsSystemAdmin = user.IsSystemAdmin;
            // Avatar data would be loaded from separate service if available
        }

        private User MapModelToUser(User? existingUser = null)
        {
            var user = existingUser ?? new User();
            
            user.FirstName = Model.FirstName;
            user.LastName = Model.LastName;
            user.UPN = Model.Upn;
            user.Role = Model.Role;
            user.DepartmentId = Model.DepartmentId;
            user.Phone = Model.Phone;
            user.AlternateEmail = Model.AlternateEmail;
            user.BirthDate = Model.BirthDate;
            user.EmploymentDate = Model.EmploymentDate;
            user.Position = Model.Position;
            user.Notes = Model.Notes;
            user.IsSystemAdmin = Model.IsSystemAdmin;
            user.IsActive = true; // New/updated users are active by default

            return user;
        }

        private bool CanSave()
        {
            return !HasErrors && !IsLoading;
        }

        private async Task SaveAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("=== ROZPOCZĘCIE PROCESU ZAPISYWANIA UŻYTKOWNIKA ===");
                _logger.LogInformation("Tryb edycji: {IsEditMode}, UserId: {UserId}", IsEditMode, _userId);
                _logger.LogInformation("Model danych: FirstName={FirstName}, LastName={LastName}, UPN={UPN}, Role={Role}, DepartmentId={DepartmentId}", 
                    Model.FirstName, Model.LastName, Model.Upn, Model.Role, Model.DepartmentId);

                if (IsEditMode)
                {
                    StatusMessage = "Zapisywanie zmian...";
                    _logger.LogInformation("TRYB EDYCJI: Aktualizacja użytkownika {UserId}", _userId);
                    
                    // Load current user and apply changes
                    _logger.LogDebug("Pobieranie aktualnych danych użytkownika...");
                    var currentUser = await _apiService.GetUserByIdAsync(_userId!);
                    if (currentUser == null)
                    {
                        _logger.LogError("BŁĄD: Nie można znaleźć użytkownika o ID: {UserId}", _userId);
                        throw new InvalidOperationException("Nie można znaleźć użytkownika do aktualizacji");
                    }
                    _logger.LogDebug("Pobrano dane użytkownika: {UserName}", $"{currentUser.FirstName} {currentUser.LastName}");

                    _logger.LogInformation("Wywołanie API: UpdateUserAsync z parametrami: UserId={UserId}, FirstName={FirstName}, LastName={LastName}, UPN={UPN}, Role={Role}, DepartmentId={DepartmentId}", 
                        _userId, Model.FirstName, Model.LastName, Model.Upn, Model.Role, Model.DepartmentId);

                    var success = await _apiService.UpdateUserAsync(
                        _userId!,
                        Model.FirstName,
                        Model.LastName,
                        Model.Upn,
                        Model.Role,
                        Model.DepartmentId,
                        Model.Phone,
                        Model.AlternateEmail
                    );

                    _logger.LogInformation("Wynik aktualizacji: {Success}", success);

                    if (success)
                    {
                        StatusMessage = "Zmiany zostały zapisane";
                        DialogResult = true;
                        _logger.LogInformation("✅ SUKCES: Użytkownik zaktualizowany pomyślnie");
                    }
                    else
                    {
                        ErrorMessage = "Nie udało się zapisać zmian";
                        StatusMessage = "Błąd zapisu";
                        _logger.LogError("❌ BŁĄD: Aktualizacja użytkownika nie powiodła się");
                    }
                }
                else
                {
                    StatusMessage = "Tworzenie użytkownika...";
                    _logger.LogInformation("TRYB TWORZENIA: Nowy użytkownik");
                    _logger.LogInformation("Parametry tworzenia: FirstName={FirstName}, LastName={LastName}, UPN={UPN}, Role={Role}, DepartmentId={DepartmentId}, Password={HasPassword}", 
                        Model.FirstName, Model.LastName, Model.Upn, Model.Role, Model.DepartmentId, !string.IsNullOrEmpty(Model.Password));

                    _logger.LogInformation("Wywołanie API: CreateUserAsync...");
                    var newUser = await _apiService.CreateUserAsync(
                        Model.FirstName,
                        Model.LastName,
                        Model.Upn,
                        Model.Role,
                        Model.DepartmentId,
                        Model.Password,
                        false, // sendWelcomeEmail
                        Model.Phone,
                        Model.AlternateEmail,
                        null // externalId
                    );

                    _logger.LogInformation("Wynik tworzenia: {NewUser}", newUser != null ? $"Utworzono użytkownika ID: {newUser.Id}" : "NULL - tworzenie nie powiodło się");

                    if (newUser != null)
                    {
                        StatusMessage = "Użytkownik został utworzony";
                        DialogResult = true;
                        _logger.LogInformation("✅ SUKCES: Użytkownik utworzony pomyślnie - ID: {UserId}, UPN: {UPN}", newUser.Id, newUser.UPN);
                    }
                    else
                    {
                        ErrorMessage = "Nie udało się utworzyć użytkownika";
                        StatusMessage = "Błąd tworzenia";
                        _logger.LogError("❌ BŁĄD: Tworzenie użytkownika nie powiodło się - API zwróciło NULL");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 WYJĄTEK podczas zapisu użytkownika: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                ErrorMessage = $"Błąd zapisu: {ex.Message}";
                StatusMessage = "Wystąpił błąd";
            }
            finally
            {
                IsLoading = false;
                _logger.LogInformation("=== ZAKOŃCZENIE PROCESU ZAPISYWANIA UŻYTKOWNIKA ===");
            }
        }

        private void Cancel()
        {
            DialogResult = false;
        }

        private void UploadAvatar()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Wybierz zdjęcie profilowe",
                    Filter = "Pliki obrazów|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*",
                    CheckFileExists = true,
                    CheckPathExists = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    var fileInfo = new FileInfo(filePath);

                    // Sprawdź rozmiar pliku (max 5MB)
                    if (fileInfo.Length > 5 * 1024 * 1024)
                    {
                        ErrorMessage = "Plik jest za duży. Maksymalny rozmiar to 5MB.";
                        return;
                    }

                    // Read file data
                    var imageData = File.ReadAllBytes(filePath);
                    Model.AvatarData = imageData;
                    
                    StatusMessage = "Zdjęcie profilowe zostało załadowane";
                    ErrorMessage = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania avatara");
                ErrorMessage = $"Błąd ładowania zdjęcia: {ex.Message}";
            }
        }

        private void RemoveAvatar()
        {
            Model.AvatarData = null;
            StatusMessage = "Zdjęcie profilowe zostało usunięte";
            UpdateCommandStates();
        }

        private string[] GetValidationErrors()
        {
            var errors = new List<string>();
            
            foreach (var property in typeof(UserDetailModel).GetProperties())
            {
                if (Model is IDataErrorInfo dataErrorInfo)
                {
                    var error = dataErrorInfo[property.Name];
                    if (!string.IsNullOrEmpty(error))
                    {
                        errors.Add(error);
                    }
                }
            }
            
            return errors.ToArray();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateCommandStates();
            OnPropertyChanged(nameof(HasErrors));
        }

        private void UpdateCommandStates()
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveAvatarCommand as RelayCommand)?.RaiseCanExecuteChanged();
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