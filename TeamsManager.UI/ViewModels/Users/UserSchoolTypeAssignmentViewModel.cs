using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.UI.Models.ViewModels;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Users
{
    /// <summary>
    /// ViewModel dla zarządzania przypisaniami użytkownika do typów szkół.
    /// Obsługuje dodawanie, edycję i usuwanie przypisań z walidacją obciążenia.
    /// </summary>
    public class UserSchoolTypeAssignmentViewModel : INotifyPropertyChanged
    {
        private readonly IUserService _userService;
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UserSchoolTypeAssignmentViewModel> _logger;

        private User? _currentUser;
        private ObservableCollection<SchoolTypeAssignmentModel> _assignments = new();
        private ObservableCollection<SchoolType> _availableSchoolTypes = new();
        private SchoolType? _selectedSchoolType;
        private bool _isLoading;
        private string? _errorMessage;
        private decimal _totalWorkloadPercentage;
        private bool _showInactiveAssignments;

        public UserSchoolTypeAssignmentViewModel(
            IUserService userService,
            ISchoolTypeService schoolTypeService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<UserSchoolTypeAssignmentViewModel> logger)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _schoolTypeService = schoolTypeService ?? throw new ArgumentNullException(nameof(schoolTypeService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeCommands();
        }

        #region Properties

        /// <summary>
        /// Aktualny użytkownik dla którego zarządzamy przypisaniami
        /// </summary>
        public User? CurrentUser
        {
            get => _currentUser;
            set 
            { 
                _currentUser = value; 
                OnPropertyChanged();
                _ = LoadAssignmentsAsync();
            }
        }

        /// <summary>
        /// Lista przypisań użytkownika do typów szkół
        /// </summary>
        public ObservableCollection<SchoolTypeAssignmentModel> Assignments
        {
            get => _assignments;
            set { _assignments = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Lista dostępnych typów szkół do przypisania
        /// </summary>
        public ObservableCollection<SchoolType> AvailableSchoolTypes
        {
            get => _availableSchoolTypes;
            set { _availableSchoolTypes = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Wybrany typ szkoły do dodania
        /// </summary>
        public SchoolType? SelectedSchoolType
        {
            get => _selectedSchoolType;
            set { _selectedSchoolType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Czy trwa ładowanie danych
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Komunikat o błędzie
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Całkowite obciążenie procentowe (suma aktywnych przypisań)
        /// </summary>
        public decimal TotalWorkloadPercentage
        {
            get => _totalWorkloadPercentage;
            private set { _totalWorkloadPercentage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Czy pokazywać nieaktywne przypisania
        /// </summary>
        public bool ShowInactiveAssignments
        {
            get => _showInactiveAssignments;
            set 
            { 
                _showInactiveAssignments = value; 
                OnPropertyChanged();
                _ = LoadAssignmentsAsync();
            }
        }

        /// <summary>
        /// Czy można dodać nowe przypisanie
        /// </summary>
        public bool CanAddAssignment => 
            CurrentUser != null && 
            SelectedSchoolType != null && 
            TotalWorkloadPercentage < 100 &&
            !IsLoading;

        #endregion

        #region Commands

        public ICommand LoadAssignmentsCommand { get; private set; } = null!;
        public ICommand AddAssignmentCommand { get; private set; } = null!;
        public ICommand SaveAssignmentCommand { get; private set; } = null!;
        public ICommand RemoveAssignmentCommand { get; private set; } = null!;
        public ICommand RefreshCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            LoadAssignmentsCommand = new RelayCommand(async _ => await LoadAssignmentsAsync());
            AddAssignmentCommand = new RelayCommand(async _ => await AddAssignmentAsync(), _ => CanAddAssignment);
            SaveAssignmentCommand = new RelayCommand(async param => await SaveAssignmentAsync(param as SchoolTypeAssignmentModel));
            RemoveAssignmentCommand = new RelayCommand(async param => await RemoveAssignmentAsync(param as SchoolTypeAssignmentModel));
            RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ładuje przypisania dla aktualnego użytkownika
        /// </summary>
        private async Task LoadAssignmentsAsync()
        {
            if (CurrentUser == null) return;

            try
            {
                IsLoading = true;
                ErrorMessage = null;

                _logger.LogInformation("Ładowanie przypisań dla użytkownika {UserId}", CurrentUser.Id);

                // Pobierz świeże dane użytkownika z przypisaniami
                var user = await _userService.GetUserByIdAsync(CurrentUser.Id, forceRefresh: true);
                if (user == null)
                {
                    ErrorMessage = "Nie można pobrać danych użytkownika";
                    return;
                }

                // Konwertuj na modele widoku
                var assignments = user.SchoolTypeAssignments
                    .Where(a => ShowInactiveAssignments || (a.IsActive && a.IsCurrentlyActive))
                    .Select(CreateAssignmentModel)
                    .OrderBy(a => a.SchoolTypeName);

                Assignments.Clear();
                foreach (var assignment in assignments)
                {
                    Assignments.Add(assignment);
                }

                CalculateTotalWorkload();
                await LoadAvailableSchoolTypesAsync();

                _logger.LogInformation("Załadowano {Count} przypisań", Assignments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania przypisań dla użytkownika {UserId}", CurrentUser?.Id);
                ErrorMessage = $"Błąd podczas ładowania przypisań: {ex.Message}";
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    ErrorMessage,
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Ładuje dostępne typy szkół
        /// </summary>
        private async Task LoadAvailableSchoolTypesAsync()
        {
            try
            {
                var allSchoolTypes = await _schoolTypeService.GetAllActiveSchoolTypesAsync();
                var assignedSchoolTypeIds = Assignments
                    .Where(a => a.IsCurrentlyActive)
                    .Select(a => a.SchoolTypeId)
                    .ToHashSet();

                AvailableSchoolTypes.Clear();
                foreach (var schoolType in allSchoolTypes.Where(st => !assignedSchoolTypeIds.Contains(st.Id)))
                {
                    AvailableSchoolTypes.Add(schoolType);
                }

                // Reset wyboru jeśli typ szkoły nie jest już dostępny
                if (SelectedSchoolType != null && !AvailableSchoolTypes.Contains(SelectedSchoolType))
                {
                    SelectedSchoolType = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania typów szkół");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Nie udało się pobrać typów szkół: {ex.Message}",
                    "error");
            }
        }

        /// <summary>
        /// Dodaje nowe przypisanie
        /// </summary>
        private async Task AddAssignmentAsync()
        {
            if (!CanAddAssignment || SelectedSchoolType == null) return;

            try
            {
                var defaultWorkload = Math.Min(20, 100 - TotalWorkloadPercentage);
                
                var newAssignment = new SchoolTypeAssignmentModel
                {
                    Id = Guid.NewGuid().ToString(), // Tymczasowe ID
                    SchoolTypeId = SelectedSchoolType.Id,
                    SchoolTypeName = SelectedSchoolType.FullName,
                    SchoolTypeShortName = SelectedSchoolType.ShortName,
                    SchoolTypeColor = SelectedSchoolType.ColorCode ?? "#0078D4",
                    WorkloadPercentage = defaultWorkload,
                    AssignedDate = DateTime.Now,
                    IsCurrentlyActive = true,
                    IsModified = true
                };

                Assignments.Add(newAssignment);
                CalculateTotalWorkload();
                await LoadAvailableSchoolTypesAsync();

                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Dodano przypisanie do {newAssignment.SchoolTypeName}",
                    "success");

                _logger.LogInformation("Dodano nowe przypisanie do {SchoolTypeName}", newAssignment.SchoolTypeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dodawania przypisania");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Nie udało się dodać przypisania: {ex.Message}",
                    "error");
            }
        }

        /// <summary>
        /// Zapisuje zmiany w przypisaniu
        /// </summary>
        private async Task SaveAssignmentAsync(SchoolTypeAssignmentModel? assignment)
        {
            if (assignment == null || !assignment.IsModified || !assignment.IsValid || CurrentUser == null) return;

            try
            {
                IsLoading = true;

                _logger.LogInformation("Zapisywanie przypisania {AssignmentId}", assignment.Id);

                UserSchoolType? result;

                if (assignment.IsNewAssignment)
                {
                    // Nowe przypisanie
                    result = await _userService.AssignUserToSchoolTypeAsync(
                        CurrentUser.Id,
                        assignment.SchoolTypeId,
                        assignment.AssignedDate,
                        assignment.EndDate,
                        assignment.WorkloadPercentage,
                        assignment.Notes
                    );

                    if (result != null)
                    {
                        assignment.Id = result.Id;
                        assignment.ResetModified();
                        await _notificationService.SendNotificationToUserAsync(
                            _currentUserService.GetCurrentUserUpn() ?? "system",
                            "Przypisanie zostało zapisane",
                            "success");
                    }
                    else
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            _currentUserService.GetCurrentUserUpn() ?? "system",
                            "Nie udało się zapisać przypisania",
                            "error");
                    }
                }
                else
                {
                    // Aktualizacja przez bezpośredni API call (UserService nie ma metody update)
                    // Dla uproszczenia użyjemy usunięcia i ponownego utworzenia
                    var removeSuccess = await _userService.RemoveUserFromSchoolTypeAsync(assignment.Id);
                    if (removeSuccess)
                    {
                        result = await _userService.AssignUserToSchoolTypeAsync(
                            CurrentUser.Id,
                            assignment.SchoolTypeId,
                            assignment.AssignedDate,
                            assignment.EndDate,
                            assignment.WorkloadPercentage,
                            assignment.Notes
                        );

                        if (result != null)
                        {
                            assignment.Id = result.Id;
                            assignment.ResetModified();
                            await _notificationService.SendNotificationToUserAsync(
                                _currentUserService.GetCurrentUserUpn() ?? "system",
                                "Przypisanie zostało zaktualizowane",
                                "success");
                        }
                    }
                }

                CalculateTotalWorkload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania przypisania {AssignmentId}", assignment.Id);
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Nie udało się zapisać przypisania: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Usuwa przypisanie
        /// </summary>
        private async Task RemoveAssignmentAsync(SchoolTypeAssignmentModel? assignment)
        {
            if (assignment == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Czy na pewno chcesz usunąć przypisanie do {assignment.SchoolTypeName}?",
                "Potwierdzenie",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

            if (!result) return;

            try
            {
                IsLoading = true;

                _logger.LogInformation("Usuwanie przypisania {AssignmentId}", assignment.Id);

                if (assignment.IsNewAssignment)
                {
                    // Nowe przypisanie - po prostu usuń z listy
                    Assignments.Remove(assignment);
                }
                else
                {
                    // Istniejące przypisanie - usuń przez serwis
                    var success = await _userService.RemoveUserFromSchoolTypeAsync(assignment.Id);
                    if (success)
                    {
                        Assignments.Remove(assignment);
                        await _notificationService.SendNotificationToUserAsync(
                            _currentUserService.GetCurrentUserUpn() ?? "system",
                            "Przypisanie zostało usunięte",
                            "success");
                    }
                    else
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            _currentUserService.GetCurrentUserUpn() ?? "system",
                            "Nie udało się usunąć przypisania",
                            "error");
                        return;
                    }
                }

                CalculateTotalWorkload();
                await LoadAvailableSchoolTypesAsync();

                _logger.LogInformation("Usunięto przypisanie do {SchoolTypeName}", assignment.SchoolTypeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania przypisania {AssignmentId}", assignment.Id);
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Nie udało się usunąć przypisania: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Odświeża wszystkie dane
        /// </summary>
        private async Task RefreshDataAsync()
        {
            await LoadAssignmentsAsync();
        }

        /// <summary>
        /// Oblicza całkowite obciążenie procentowe
        /// </summary>
        private void CalculateTotalWorkload()
        {
            TotalWorkloadPercentage = Assignments
                .Where(a => a.IsCurrentlyActive)
                .Sum(a => a.WorkloadPercentage);

            // Odśwież CanAddAssignment
            OnPropertyChanged(nameof(CanAddAssignment));
        }

        /// <summary>
        /// Tworzy model widoku z encji domenowej
        /// </summary>
        private SchoolTypeAssignmentModel CreateAssignmentModel(UserSchoolType userSchoolType)
        {
            return new SchoolTypeAssignmentModel
            {
                Id = userSchoolType.Id,
                SchoolTypeId = userSchoolType.SchoolTypeId,
                SchoolTypeName = userSchoolType.SchoolType?.FullName ?? "Nieznany typ szkoły",
                SchoolTypeShortName = userSchoolType.SchoolType?.ShortName ?? "",
                SchoolTypeColor = userSchoolType.SchoolType?.ColorCode ?? "#0078D4",
                WorkloadPercentage = userSchoolType.WorkloadPercentage ?? 0,
                AssignedDate = userSchoolType.AssignedDate,
                EndDate = userSchoolType.EndDate,
                Notes = userSchoolType.Notes ?? string.Empty,
                IsCurrentlyActive = userSchoolType.IsCurrentlyActive,
                IsModified = false
            };
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