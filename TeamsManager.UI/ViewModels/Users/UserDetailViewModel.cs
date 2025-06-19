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
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly ILogger<UserDetailViewModel> _logger;
        private readonly UserSchoolTypeAssignmentViewModel _userSchoolTypeAssignmentViewModel;
        private readonly IMsalAuthService _msalAuthService;
        
        private UserDetailModel _model;
        private ObservableCollection<Department> _departments;
        private bool _isEditMode;
        private string? _userId;
        private bool _isLoading;
        private string? _errorMessage;
        private string _statusMessage = string.Empty;
        private bool? _dialogResult;

        public UserDetailViewModel(
            IUserService userService,
            IDepartmentService departmentService,
            ILogger<UserDetailViewModel> logger,
            UserSchoolTypeAssignmentViewModel userSchoolTypeAssignmentViewModel,
            IMsalAuthService msalAuthService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userSchoolTypeAssignmentViewModel = userSchoolTypeAssignmentViewModel ?? throw new ArgumentNullException(nameof(userSchoolTypeAssignmentViewModel));
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));

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
                var departments = await _departmentService.GetAllDepartmentsAsync();
                Departments.Clear();
                
                // Jeśli nie ma działów, dodaj domyślny dział
                if (!departments.Any())
                {
                    _logger.LogWarning("Brak działów w bazie danych. Może być potrzebna inicjalizacja danych.");
                    
                    // Dodaj domyślny dział jako fallback
                    var defaultDepartment = new Department
                    {
                        Id = "temp-default",
                        Name = "Brak działów - skontaktuj się z administratorem",
                        Description = "Domyślny dział tymczasowy",
                        IsActive = true
                    };
                    Departments.Add(defaultDepartment);
                    
                    _logger.LogInformation("Dodano tymczasowy domyślny dział dla formularza użytkownika");
                }
                else
                {
                    foreach (var dept in departments.OrderBy(d => d.Name))
                    {
                        Departments.Add(dept);
                    }
                    _logger.LogDebug("Załadowano {Count} działów", departments.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania działów");
                
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
                
                // NIE rzucaj wyjątku ponownie - pozwól formularzowi się otworzyć z fallback działem
                _logger.LogInformation("Dodano fallback dział po błędzie - formularz może się otworzyć");
            }
        }

        private async Task LoadUserAsync(string userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
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
                
                // Pobierz token dostępu z MSAL Auth Service
                var accessToken = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    ErrorMessage = "Nie można uzyskać tokenu dostępu. Spróbuj ponownie zalogować się.";
                    return;
                }

                if (IsEditMode)
                {
                    StatusMessage = "Zapisywanie zmian...";
                    
                    // Load current user and apply changes
                    var currentUser = await _userService.GetUserByIdAsync(_userId!);
                    if (currentUser == null)
                    {
                        throw new InvalidOperationException("Nie można znaleźć użytkownika do aktualizacji");
                    }

                    var updatedUser = MapModelToUser(currentUser);
                    var success = await _userService.UpdateUserAsync(updatedUser, accessToken);

                    if (success)
                    {
                        StatusMessage = "Zmiany zostały zapisane";
                        DialogResult = true;
                    }
                    else
                    {
                        ErrorMessage = "Nie udało się zapisać zmian";
                        StatusMessage = "Błąd zapisu";
                    }
                }
                else
                {
                    StatusMessage = "Tworzenie użytkownika...";
                    
                    var newUser = await _userService.CreateUserAsync(
                        Model.FirstName,
                        Model.LastName,
                        Model.Upn,
                        Model.Role,
                        Model.DepartmentId,
                        Model.Password,
                        accessToken,
                        sendWelcomeEmail: false,
                        phone: Model.Phone,
                        alternateEmail: Model.AlternateEmail,
                        externalId: null,
                        birthDate: Model.BirthDate,
                        employmentDate: Model.EmploymentDate,
                        position: Model.Position,
                        notes: Model.Notes,
                        isSystemAdmin: Model.IsSystemAdmin
                    );

                    if (newUser != null)
                    {
                        StatusMessage = "Użytkownik został utworzony";
                        DialogResult = true;
                    }
                    else
                    {
                        ErrorMessage = "Nie udało się utworzyć użytkownika";
                        StatusMessage = "Błąd tworzenia";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisu użytkownika");
                ErrorMessage = $"Błąd zapisu: {ex.Message}";
                StatusMessage = "Wystąpił błąd";
            }
            finally
            {
                IsLoading = false;
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

        /// <summary>
        /// Pobiera token dostępu z MSAL Auth Service
        /// </summary>
        private async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                var authResult = await _msalAuthService.AcquireTokenSilentAsync();
                if (authResult?.AccessToken != null)
                {
                    return authResult.AccessToken;
                }

                _logger.LogWarning("Nie można pobrać tokenu w trybie cichym, może być wymagana ponowna autoryzacja");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania tokenu dostępu");
                return null;
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