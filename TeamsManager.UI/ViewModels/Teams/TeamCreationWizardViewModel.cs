using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.Models.Teams;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Teams
{
    public class TeamCreationWizardViewModel : BaseViewModel
    {
        private readonly ITeamService _teamService;
        private readonly ITeamTemplateService _templateService;
        private readonly IUserService _userService;
        private readonly ISchoolTypeService _schoolTypeService;
        private readonly ISchoolYearService _schoolYearService;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<TeamCreationWizardViewModel> _logger;
        
        // Step tracking
        private int _currentStep;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                SetProperty(ref _currentStep, value);
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(IsLastStep));
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
                ((RelayCommand)PreviousCommand).RaiseCanExecuteChanged();
            }
        }
        
        public bool CanGoPrevious => CurrentStep > 0;
        public bool IsLastStep => CurrentStep == 3;
        
        // Step 1: Basic Info
        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                SetProperty(ref _displayName, value);
                UpdateGeneratedName();
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
        
        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        
        private TeamVisibility _selectedVisibility = TeamVisibility.Private;
        public TeamVisibility SelectedVisibility
        {
            get => _selectedVisibility;
            set
            {
                SetProperty(ref _selectedVisibility, value);
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
        
        public List<TeamVisibility> VisibilityOptions { get; } = new()
        {
            TeamVisibility.Private,
            TeamVisibility.Public
        };
        
        private SchoolType? _selectedSchoolType;
        public SchoolType? SelectedSchoolType
        {
            get => _selectedSchoolType;
            set
            {
                SetProperty(ref _selectedSchoolType, value);
                _ = LoadTemplatesForSchoolType();
            }
        }
        
        private SchoolYear? _selectedSchoolYear;
        public SchoolYear? SelectedSchoolYear
        {
            get => _selectedSchoolYear;
            set => SetProperty(ref _selectedSchoolYear, value);
        }
        
        public ObservableCollection<SchoolType> SchoolTypes { get; } = new();
        public ObservableCollection<SchoolYear> SchoolYears { get; } = new();
        
        // Step 2: Template
        private bool _useTemplate;
        public bool UseTemplate
        {
            get => _useTemplate;
            set
            {
                SetProperty(ref _useTemplate, value);
                OnPropertyChanged(nameof(ShowTemplateValues));
                UpdateGeneratedName();
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
        
        private TeamTemplate? _selectedTemplate;
        public TeamTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                SetProperty(ref _selectedTemplate, value);
                UpdateTemplateValues();
                UpdateGeneratedName();
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
        
        public bool ShowTemplateValues => UseTemplate && SelectedTemplate != null && TemplateValues.Any();
        
        public ObservableCollection<TeamTemplate> Templates { get; } = new();
        public ObservableCollection<TemplateValueViewModel> TemplateValues { get; } = new();
        
        private string _generatedName = string.Empty;
        public string GeneratedName
        {
            get => _generatedName;
            set => SetProperty(ref _generatedName, value);
        }
        
        public string FinalTeamName => UseTemplate && !string.IsNullOrWhiteSpace(GeneratedName) ? GeneratedName : DisplayName;
        
        // Step 3: Members
        private User? _selectedOwner;
        public User? SelectedOwner
        {
            get => _selectedOwner;
            set
            {
                SetProperty(ref _selectedOwner, value);
                ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
            }
        }
        
        private string _userSearchText = string.Empty;
        public string UserSearchText
        {
            get => _userSearchText;
            set
            {
                SetProperty(ref _userSearchText, value);
                FilterAvailableUsers();
            }
        }
        
        public ObservableCollection<User> AvailableOwners { get; } = new();
        public ObservableCollection<User> AvailableUsers { get; } = new();
        public ObservableCollection<User> FilteredAvailableUsers { get; } = new();
        public ObservableCollection<User> SelectedMembers { get; } = new();
        
        // Step 4: Summary
        private string _warningMessage = string.Empty;
        public string WarningMessage
        {
            get => _warningMessage;
            set => SetProperty(ref _warningMessage, value);
        }
        
        public bool ShowWarning => !string.IsNullOrWhiteSpace(WarningMessage);
        
        // Loading state
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                SetProperty(ref _isLoading, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }
        
        // Dialog result
        public bool DialogResult { get; private set; }
        public Team? CreatedTeam { get; private set; }
        
        // Commands
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddMembersCommand { get; }
        public ICommand RemoveMembersCommand { get; }
        
        // Constructor
        public TeamCreationWizardViewModel(
            ITeamService teamService,
            ITeamTemplateService templateService,
            IUserService userService,
            ISchoolTypeService schoolTypeService,
            ISchoolYearService schoolYearService,
            ICurrentUserService currentUserService,
            INotificationService notificationService,
            ILogger<TeamCreationWizardViewModel> logger)
        {
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _schoolTypeService = schoolTypeService ?? throw new ArgumentNullException(nameof(schoolTypeService));
            _schoolYearService = schoolYearService ?? throw new ArgumentNullException(nameof(schoolYearService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize commands
            NextCommand = new RelayCommand(async () => await NextStep(), CanGoNext);
            PreviousCommand = new RelayCommand(() => CurrentStep--, () => CanGoPrevious);
            CancelCommand = new RelayCommand(() => CloseDialog(false));
            AddMembersCommand = new RelayCommand(AddSelectedMembers);
            RemoveMembersCommand = new RelayCommand(RemoveSelectedMembers);
            
            // Subscribe to template value changes
            TemplateValues.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowTemplateValues));
            
            // Load initial data
            _ = LoadInitialData();
        }
        
        private async Task LoadInitialData()
        {
            try
            {
                IsLoading = true;
                
                // Load school types and years
                var schoolTypesTask = _schoolTypeService.GetAllActiveSchoolTypesAsync();
                var schoolYearsTask = _schoolYearService.GetAllActiveSchoolYearsAsync();
                var usersTask = _userService.GetAllActiveUsersAsync();
                var templatesTask = _templateService.GetAllActiveTemplatesAsync();
                
                await Task.WhenAll(schoolTypesTask, schoolYearsTask, usersTask, templatesTask);
                
                var schoolTypes = await schoolTypesTask;
                var schoolYears = await schoolYearsTask;
                var users = await usersTask;
                var templates = await templatesTask;
                
                SchoolTypes.Clear();
                foreach (var schoolType in schoolTypes)
                    SchoolTypes.Add(schoolType);
                
                SchoolYears.Clear();
                foreach (var schoolYear in schoolYears)
                    SchoolYears.Add(schoolYear);
                
                // Load owners (teachers and above)
                var owners = users.Where(u => u.Role >= UserRole.Nauczyciel).ToList();
                AvailableOwners.Clear();
                foreach (var owner in owners)
                    AvailableOwners.Add(owner);
                
                // Load all users for members
                AvailableUsers.Clear();
                foreach (var user in users)
                    AvailableUsers.Add(user);
                
                FilterAvailableUsers();
                
                // Load universal templates initially
                var universalTemplates = templates.Where(t => t.IsUniversal).ToList();
                Templates.Clear();
                foreach (var template in universalTemplates)
                    Templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ładowania danych początkowych wizarda");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Błąd ładowania danych wizarda: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task LoadTemplatesForSchoolType()
        {
            if (SelectedSchoolType == null)
            {
                // Load universal templates
                var universalTemplates = (await _templateService.GetUniversalTemplatesAsync()).ToList();
                Templates.Clear();
                foreach (var template in universalTemplates)
                    Templates.Add(template);
                return;
            }
            
            try
            {
                var schoolTypeTemplates = await _templateService.GetTemplatesBySchoolTypeAsync(SelectedSchoolType.Id);
                var universalTemplates = await _templateService.GetUniversalTemplatesAsync();
                
                var allTemplates = schoolTypeTemplates.Concat(universalTemplates).ToList();
                
                Templates.Clear();
                foreach (var template in allTemplates)
                    Templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd ładowania szablonów dla typu szkoły {SchoolTypeId}", SelectedSchoolType.Id);
            }
        }
        
        private void FilterAvailableUsers()
        {
            FilteredAvailableUsers.Clear();
            
            var filteredUsers = AvailableUsers.Where(u => 
                !SelectedMembers.Contains(u) && // Exclude already selected
                u != SelectedOwner && // Exclude selected owner
                (string.IsNullOrWhiteSpace(UserSearchText) || 
                 u.FullName.Contains(UserSearchText, StringComparison.OrdinalIgnoreCase) ||
                 u.UPN.Contains(UserSearchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            foreach (var user in filteredUsers)
                FilteredAvailableUsers.Add(user);
        }
        
        private void UpdateTemplateValues()
        {
            TemplateValues.Clear();
            
            if (SelectedTemplate == null) return;
            
            foreach (var placeholder in SelectedTemplate.Placeholders)
            {
                var templateValue = new TemplateValueViewModel
                {
                    Placeholder = placeholder,
                    DisplayName = GetPlaceholderDisplayName(placeholder),
                    Description = GetPlaceholderDescription(placeholder)
                };
                
                templateValue.ValueChanged += UpdateGeneratedName;
                TemplateValues.Add(templateValue);
            }
            
            OnPropertyChanged(nameof(ShowTemplateValues));
            UpdateGeneratedName();
        }
        
        private string GetPlaceholderDisplayName(string placeholder)
        {
            return placeholder switch
            {
                "TypSzkoly" => "Typ szkoły",
                "RokSzkolny" => "Rok szkolny",
                "Nauczyciel" => "Nauczyciel",
                "Przedmiot" => "Przedmiot",
                "Oddzial" => "Oddział",
                "Grupa" => "Grupa",
                "Kurs" => "Kurs",
                "Projekt" => "Projekt",
                _ => placeholder
            };
        }
        
        private string GetPlaceholderDescription(string placeholder)
        {
            return placeholder switch
            {
                "TypSzkoly" => "Krótka nazwa typu szkoły (np. LO, ZS)",
                "RokSzkolny" => "Rok szkolny (np. 2024/2025)",
                "Nauczyciel" => "Nazwisko nauczyciela",
                "Przedmiot" => "Nazwa przedmiotu",
                "Oddzial" => "Klasa/oddział (np. 1A, 2B)",
                "Grupa" => "Grupa uczniów",
                "Kurs" => "Nazwa kursu",
                "Projekt" => "Nazwa projektu",
                _ => $"Wartość dla {placeholder}"
            };
        }
        
        private void UpdateGeneratedName()
        {
            if (!UseTemplate || SelectedTemplate == null)
            {
                GeneratedName = string.Empty;
                OnPropertyChanged(nameof(FinalTeamName));
                return;
            }
            
            try
            {
                var values = new Dictionary<string, string>();
                
                // Auto-fill known values
                if (SelectedSchoolType != null && SelectedTemplate.Placeholders.Contains("TypSzkoly"))
                    values["TypSzkoly"] = SelectedSchoolType.ShortName;
                
                if (SelectedSchoolYear != null && SelectedTemplate.Placeholders.Contains("RokSzkolny"))
                    values["RokSzkolny"] = SelectedSchoolYear.Name;
                
                if (SelectedOwner != null && SelectedTemplate.Placeholders.Contains("Nauczyciel"))
                    values["Nauczyciel"] = SelectedOwner.LastName;
                
                // Add user-provided values
                foreach (var templateValue in TemplateValues)
                {
                    if (!string.IsNullOrWhiteSpace(templateValue.Value))
                        values[templateValue.Placeholder] = templateValue.Value;
                }
                
                GeneratedName = SelectedTemplate.GenerateTeamName(values);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd generowania nazwy z szablonu");
                GeneratedName = DisplayName;
            }
            
            OnPropertyChanged(nameof(FinalTeamName));
        }
        
        private void AddSelectedMembers()
        {
            // This would be implemented to add selected users from FilteredAvailableUsers to SelectedMembers
            // For now, it's a placeholder
        }
        
        private void RemoveSelectedMembers()
        {
            // This would be implemented to remove selected users from SelectedMembers
            // For now, it's a placeholder
        }
        
        private async Task NextStep()
        {
            if (IsLastStep)
            {
                await CreateTeam();
            }
            else
            {
                CurrentStep++;
                if (CurrentStep == 3) // Summary step
                {
                    UpdateSummaryWarnings();
                }
            }
        }
        
        private void UpdateSummaryWarnings()
        {
            var warnings = new List<string>();
            
            if (UseTemplate && SelectedTemplate != null)
            {
                var missingValues = TemplateValues.Where(tv => tv.IsRequired && string.IsNullOrWhiteSpace(tv.Value));
                if (missingValues.Any())
                {
                    warnings.Add($"Brakuje wartości dla placeholderów: {string.Join(", ", missingValues.Select(mv => mv.DisplayName))}");
                }
            }
            
            if (string.IsNullOrWhiteSpace(FinalTeamName))
            {
                warnings.Add("Nazwa zespołu nie może być pusta");
            }
            
            if (SelectedOwner == null)
            {
                warnings.Add("Musisz wybrać właściciela zespołu");
            }
            
            WarningMessage = string.Join("; ", warnings);
            OnPropertyChanged(nameof(ShowWarning));
        }
        
        private bool CanGoNext()
        {
            return CurrentStep switch
            {
                0 => !string.IsNullOrWhiteSpace(DisplayName), // Step 1: Basic info
                1 => !UseTemplate || SelectedTemplate != null, // Step 2: Template
                2 => SelectedOwner != null, // Step 3: Members
                3 => true, // Step 4: Summary (always can proceed to create)
                _ => false
            };
        }
        
        private async Task CreateTeam()
        {
            try
            {
                IsLoading = true;
                
                // Call the service method directly
                var createdTeam = await _teamService.CreateTeamAsync(
                    FinalTeamName,
                    Description,
                    SelectedOwner!.UPN,
                    SelectedVisibility,
                    "dummy-token", // This should come from proper token management
                    SelectedTemplate?.Id,
                    SelectedSchoolType?.Id,
                    SelectedSchoolYear?.Id,
                    GetTemplateValues());
                
                if (createdTeam != null)
                {
                    CreatedTeam = createdTeam;
                    
                    // Add members if any selected
                    if (SelectedMembers.Any())
                    {
                        var memberUpns = SelectedMembers.Select(m => m.UPN).ToList();
                        // Note: This method would need to be implemented in ITeamService
                        // await _teamService.AddUsersToTeamAsync(createdTeam.Id, memberUpns);
                    }
                    
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Zespół został utworzony pomyślnie!",
                        "success");
                    
                    CloseDialog(true);
                }
                else
                {
                    await _notificationService.SendNotificationToUserAsync(
                        _currentUserService.GetCurrentUserUpn() ?? "system",
                        "Nie udało się utworzyć zespołu",
                        "error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas tworzenia zespołu");
                await _notificationService.SendNotificationToUserAsync(
                    _currentUserService.GetCurrentUserUpn() ?? "system",
                    $"Wystąpił błąd podczas tworzenia zespołu: {ex.Message}",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private Dictionary<string, string> GetTemplateValues()
        {
            var values = new Dictionary<string, string>();
            foreach (var tv in TemplateValues)
            {
                if (!string.IsNullOrWhiteSpace(tv.Value))
                    values[tv.Placeholder] = tv.Value;
            }
            return values;
        }
        
        private void CloseDialog(bool result)
        {
            DialogResult = result;
            // This would close the dialog window - implementation depends on how the dialog is shown
        }
    }
} 