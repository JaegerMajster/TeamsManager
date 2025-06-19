using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Views;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Views.Dashboard;
using TeamsManager.UI.ViewModels.Dashboard;
using TeamsManager.UI.Services;
using TeamsManager.UI.Models;

namespace TeamsManager.UI.ViewModels.Shell
{
    public class MainShellViewModel : INotifyPropertyChanged
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMsalAuthService _msalAuthService;
        private readonly ConfigurationManager _configManager;
        private readonly IGraphUserProfileService _graphUserProfileService;
        private readonly ConditionalAccessAnalyzer _conditionalAccessAnalyzer;
        private readonly IUserSynchronizationService _userSynchronizationService;
        private readonly ILogger<MainShellViewModel> _logger;

        private object? _currentView;
        private string _currentViewTitle = "Dashboard";
        private string _userDisplayName = "Użytkownik";
        private string _userEmail = string.Empty;
        private string _userJobTitle = string.Empty;
        private string _userOfficeLocation = string.Empty;
        private string _userPhone = string.Empty;
        private string _userDepartment = string.Empty;
        private string _userId = string.Empty;
        private string _tenantId = string.Empty;
        private DateTime? _lastLoginTime;
        private BitmapImage? _userProfilePicture;
        private bool _isLoadingProfile = false;
        private string? _securitySummary = "🔐 Sprawdzanie...";
        private DateTime? _tokenExpiresOn;
        private string _tokenCacheStatus = "Brak danych";
        private bool _isDialogOpen;

        public MainShellViewModel(
            IServiceProvider serviceProvider,
            ICurrentUserService currentUserService,
            IMsalAuthService msalAuthService,
            ConfigurationManager configManager,
            IGraphUserProfileService graphUserProfileService,
            ConditionalAccessAnalyzer conditionalAccessAnalyzer,
            IUserSynchronizationService userSynchronizationService,
            ILogger<MainShellViewModel> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _msalAuthService = msalAuthService ?? throw new ArgumentNullException(nameof(msalAuthService));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            _graphUserProfileService = graphUserProfileService ?? throw new ArgumentNullException(nameof(graphUserProfileService));
            _conditionalAccessAnalyzer = conditionalAccessAnalyzer ?? throw new ArgumentNullException(nameof(conditionalAccessAnalyzer));
            _userSynchronizationService = userSynchronizationService ?? throw new ArgumentNullException(nameof(userSynchronizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeCommands();
            LoadUserInfo();
            LoadDashboard();
            UpdateTokenStatus();
        }

        // Properties
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public string CurrentViewTitle
        {
            get => _currentViewTitle;
            set
            {
                _currentViewTitle = value;
                OnPropertyChanged();
            }
        }

        public string UserDisplayName
        {
            get => _userDisplayName;
            set
            {
                _userDisplayName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UserInitials)); // Zaktualizuj także UserInitials
            }
        }

        public string UserEmail
        {
            get => _userEmail;
            set
            {
                _userEmail = value;
                OnPropertyChanged();
            }
        }

        public string UserJobTitle
        {
            get => _userJobTitle;
            set
            {
                _userJobTitle = value;
                OnPropertyChanged();
            }
        }

        public string UserOfficeLocation
        {
            get => _userOfficeLocation;
            set
            {
                _userOfficeLocation = value;
                OnPropertyChanged();
            }
        }

        public string UserPhone
        {
            get => _userPhone;
            set
            {
                _userPhone = value;
                OnPropertyChanged();
            }
        }

        public string UserDepartment
        {
            get => _userDepartment;
            set
            {
                _userDepartment = value;
                OnPropertyChanged();
            }
        }

        public string UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        public string TenantId
        {
            get => _tenantId;
            set
            {
                _tenantId = value;
                OnPropertyChanged();
            }
        }

        public DateTime? LastLoginTime
        {
            get => _lastLoginTime;
            set
            {
                _lastLoginTime = value;
                OnPropertyChanged();
            }
        }

        public string UserInitials
        {
            get
            {
                if (string.IsNullOrEmpty(UserDisplayName)) return "U";
                
                var parts = UserDisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                }
                else if (parts.Length == 1 && parts[0].Length >= 2)
                {
                    return parts[0].Substring(0, 2).ToUpper();
                }
                else
                {
                    return parts[0][0].ToString().ToUpper();
                }
            }
        }

        public BitmapImage? UserProfilePicture
        {
            get => _userProfilePicture;
            set
            {
                _userProfilePicture = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoadingProfile
        {
            get => _isLoadingProfile;
            set
            {
                _isLoadingProfile = value;
                OnPropertyChanged();
            }
        }

        public string? SecuritySummary
        {
            get => _securitySummary;
            set
            {
                _securitySummary = value;
                OnPropertyChanged();
            }
        }

        public DateTime? TokenExpiresOn
        {
            get => _tokenExpiresOn;
            set
            {
                _tokenExpiresOn = value;
                OnPropertyChanged();
            }
        }

        public string TokenCacheStatus
        {
            get => _tokenCacheStatus;
            set
            {
                _tokenCacheStatus = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Czy jest otwarty dialog (dla overlay)
        /// </summary>
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set
            {
                _isDialogOpen = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand NavigateToDashboardCommand { get; private set; } = null!;
        public ICommand NavigateToUsersCommand { get; private set; } = null!;
        public ICommand NavigateToTeamsCommand { get; private set; } = null!;
        public ICommand NavigateToSchoolTypesCommand { get; private set; } = null!;
        public ICommand NavigateToSchoolYearsCommand { get; private set; } = null!;
        public ICommand NavigateToSubjectsCommand { get; private set; } = null!;
        public ICommand NavigateToDepartmentsCommand { get; private set; } = null!;
        public ICommand NavigateToOrganizationalUnitsCommand { get; private set; } = null!;
        public ICommand NavigateToOperationHistoryCommand { get; private set; } = null!;
        public ICommand NavigateToMonitoringCommand { get; private set; } = null!;
        public ICommand NavigateToSettingsCommand { get; private set; } = null!;
        public ICommand NavigateToManualTestingCommand { get; private set; } = null!;
        public ICommand LogoutCommand { get; private set; } = null!;
        public ICommand ViewProfileCommand { get; private set; } = null!;
        public ICommand RefreshProfileCommand { get; private set; } = null!;
        public ICommand ClearTokenCacheCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            NavigateToDashboardCommand = new RelayCommand(ExecuteNavigateToDashboard);
            NavigateToUsersCommand = new RelayCommand(ExecuteNavigateToUsers);
            NavigateToTeamsCommand = new RelayCommand(ExecuteNavigateToTeams);
            NavigateToSchoolTypesCommand = new RelayCommand(ExecuteNavigateToSchoolTypes);
            NavigateToSchoolYearsCommand = new RelayCommand(ExecuteNavigateToSchoolYears);
            NavigateToSubjectsCommand = new RelayCommand(ExecuteNavigateToSubjects);
            NavigateToDepartmentsCommand = new RelayCommand(ExecuteNavigateToDepartments);
            NavigateToOrganizationalUnitsCommand = new RelayCommand(ExecuteNavigateToOrganizationalUnits);
            NavigateToOperationHistoryCommand = new RelayCommand(ExecuteNavigateToOperationHistory);
            NavigateToMonitoringCommand = new RelayCommand(ExecuteNavigateToMonitoring);
            NavigateToSettingsCommand = new RelayCommand(ExecuteNavigateToSettings);
            NavigateToManualTestingCommand = new RelayCommand(ExecuteNavigateToManualTesting);
            LogoutCommand = new RelayCommand(ExecuteLogout);
            ViewProfileCommand = new RelayCommand(ExecuteViewProfile);
            RefreshProfileCommand = new RelayCommand(ExecuteRefreshProfile);
            ClearTokenCacheCommand = new RelayCommand(ExecuteClearTokenCache);
        }

        public void LoadUserInfo()
        {
            try
            {
                UserEmail = _currentUserService.GetCurrentUserUpn() ?? "Nie zalogowano";
                UserDisplayName = UserEmail.Contains("@") ? UserEmail.Split('@')[0] : UserEmail;
                
                // Ustaw LastLoginTime przy pierwszym załadowaniu
                if (LastLoginTime == null)
                {
                    LastLoginTime = DateTime.Now;
                }
                
                _logger.LogDebug("Załadowano podstawowe informacje o użytkowniku: {DisplayName} ({Email})", UserDisplayName, UserEmail);
                
                // Pobierz szczegółowy profil z Microsoft Graph
                _ = LoadDetailedUserProfileAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania informacji o użytkowniku");
                UserDisplayName = "Użytkownik";
                UserEmail = "Nie zalogowano";
            }
        }

        /// <summary>
        /// Ładuje szczegółowe informacje o użytkowniku z Microsoft Graph
        /// </summary>
        public async Task LoadDetailedUserProfileAsync()
        {
            if (IsLoadingProfile) return;

            IsLoadingProfile = true;
            
            try
            {
                _logger.LogInformation("Pobieranie szczegółowego profilu użytkownika z Microsoft Graph...");
                
                // Pobierz token dostępu
                var authResult = await _msalAuthService.AcquireTokenSilentAsync();
                if (authResult?.AccessToken == null)
                {
                    _logger.LogWarning("Brak tokenu dostępu - nie można pobrać profilu z Graph");
                    return;
                }

                // Ustaw TenantId z tokenu
                TenantId = authResult.TenantId ?? string.Empty;

                // Pobierz profil użytkownika
                var userProfile = await _graphUserProfileService.GetUserProfileAsync(authResult.AccessToken);
                if (userProfile != null)
                {
                    // Aktualizuj dane użytkownika
                    if (!string.IsNullOrEmpty(userProfile.DisplayName))
                    {
                        UserDisplayName = userProfile.DisplayName;
                    }
                    
                    if (!string.IsNullOrEmpty(userProfile.UserPrincipalName))
                    {
                        UserEmail = userProfile.UserPrincipalName;
                    }
                    else if (!string.IsNullOrEmpty(userProfile.Mail))
                    {
                        UserEmail = userProfile.Mail;
                    }

                    UserJobTitle = userProfile.JobTitle ?? string.Empty;
                    UserOfficeLocation = userProfile.OfficeLocation ?? string.Empty;
                    UserPhone = string.Empty; // Graph API UserProfile nie zawiera numerów telefonów w podstawowym response
                    UserDepartment = string.Empty; // Graph API UserProfile nie zawiera działu w podstawowym response
                    UserId = userProfile.Id ?? string.Empty;
                    
                    // Ustaw ostatni czas logowania na teraz
                    LastLoginTime = DateTime.Now;

                    _logger.LogInformation("Załadowano profil użytkownika: {DisplayName}, Stanowisko: {JobTitle}, Lokalizacja: {OfficeLocation}, Dział: {Department}", 
                        UserDisplayName, UserJobTitle, UserOfficeLocation, UserDepartment);
                }

                // Pobierz zdjęcie profilowe
                var profilePicture = await _graphUserProfileService.GetUserPhotoAsync(authResult.AccessToken);
                if (profilePicture != null)
                {
                    UserProfilePicture = profilePicture;
                    _logger.LogDebug("Załadowano zdjęcie profilowe użytkownika");
                }
                else
                {
                    _logger.LogDebug("Brak zdjęcia profilowego użytkownika lub błąd podczas pobierania");
                }

                // Analizuj Conditional Access
                try
                {
                    var conditionalAccessInfo = _conditionalAccessAnalyzer.AnalyzeToken(authResult);
                    if (conditionalAccessInfo != null)
                    {
                        var summary = GenerateSecuritySummary(conditionalAccessInfo);
                        UpdateConditionalAccessInfo(summary, conditionalAccessInfo.TokenExpiresOn);
                        _logger.LogDebug("Conditional Access info analyzed and updated");
                    }
                    else
                    {
                        UpdateConditionalAccessInfo("🔐 Podstawowy", authResult.ExpiresOn.DateTime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas analizy Conditional Access");
                    UpdateConditionalAccessInfo("🔐 Podstawowy", authResult.ExpiresOn.DateTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania szczegółowego profilu użytkownika");
            }
            finally
            {
                IsLoadingProfile = false;
            }
        }

        public async Task<bool> CheckAutoLoginAsync()
        {
            try
            {
                Console.WriteLine("=== SPRAWDZANIE AUTO-LOGIN ===");
                var loginSettings = await _configManager.LoadLoginSettingsAsync();
                
                if (loginSettings != null && loginSettings.AutoLogin && !string.IsNullOrEmpty(loginSettings.LastUserEmail))
                {
                    _logger.LogInformation("Auto-login włączony dla użytkownika: {Email}", loginSettings.LastUserEmail);
                    Console.WriteLine($"=== AUTO-LOGIN WŁĄCZONY DLA: {loginSettings.LastUserEmail} ===");
                    
                    // Spróbuj cichego logowania
                    try
                    {
                        Console.WriteLine("=== PRÓBA CICHEGO LOGOWANIA ===");
                        var authResult = await _msalAuthService.AcquireTokenSilentAsync();
                        if (authResult != null && !string.IsNullOrEmpty(authResult.AccessToken))
                        {
                            Console.WriteLine($"=== CICHY LOGIN POMYŚLNY: {authResult.Account?.Username} ===");
                            // WAŻNE: Ustaw UPN w CurrentUserService po pomyślnym cichym logowaniu
                            var userUpn = authResult.Account?.Username;
                            _logger.LogInformation("Auto-login pomyślny, ustawiam UPN: {UserUpn}", userUpn);
                            _currentUserService.SetCurrentUserUpn(userUpn);
                            
                
                            var currentUpn = _currentUserService.GetCurrentUserUpn();
                            _logger.LogInformation("UPN po auto-login: {CurrentUpn}", currentUpn);
                            
                            // Synchronizuj użytkownika z lokalną bazą danych
                            if (!string.IsNullOrEmpty(userUpn))
                            {
                                _logger.LogInformation("Rozpoczynanie synchronizacji użytkownika {UserUpn} po auto-login", userUpn);
                                Console.WriteLine($"=== WYWOŁANIE SYNCHRONIZACJI W AUTO-LOGIN: {userUpn} ===");
                                
                                var syncResult = await _userSynchronizationService.SynchronizeLoggedUserAsync(authResult.AccessToken, userUpn);
                                if (syncResult)
                                {
                                    _logger.LogInformation("Synchronizacja użytkownika {UserUpn} po auto-login zakończona pomyślnie", userUpn);
                                    Console.WriteLine($"=== SYNCHRONIZACJA AUTO-LOGIN POMYŚLNA: {userUpn} ===");
                                }
                                else
                                {
                                    _logger.LogWarning("Synchronizacja użytkownika {UserUpn} po auto-login nie powiodła się", userUpn);
                                    Console.WriteLine($"=== SYNCHRONIZACJA AUTO-LOGIN NIEPOMYŚLNA: {userUpn} ===");
                                }
                            }
                            
                            LoadUserInfo();
                            await LoadDetailedUserProfileAsync();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Cicha autentykacja nie powiodła się, wymagane logowanie interaktywne");
                    }
                }
                else
                {
                    Console.WriteLine("=== AUTO-LOGIN WYŁĄCZONY LUB BRAK USTAWIEŃ ===");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania auto-login");
                Console.WriteLine($"=== BŁĄD AUTO-LOGIN: {ex.Message} ===");
            }
            
            Console.WriteLine("=== AUTO-LOGIN ZWRACA FALSE ===");
            return false;
        }

        /// <summary>
        /// Aktualizuje status tokenów i cache
        /// </summary>
        public void UpdateTokenStatus()
        {
            try
            {
    
                var cacheStatus = MsalCacheHelper.GetCacheStatus();
                if (cacheStatus.Exists)
                {
                    var sizeKb = cacheStatus.SizeBytes / 1024.0;
                    TokenCacheStatus = $"Cache: {sizeKb:F1} KB ({cacheStatus.LastModified:HH:mm})";
                }
                else
                {
                    TokenCacheStatus = "Cache: Brak danych";
                }

                _logger.LogDebug("Token status updated: {CacheStatus}", TokenCacheStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating token status");
                TokenCacheStatus = "Cache: Błąd";
            }
        }

        /// <summary>
        /// Aktualizuje informacje o Conditional Access
        /// </summary>
        public void UpdateConditionalAccessInfo(string? securitySummary, DateTime? tokenExpiresOn)
        {
            SecuritySummary = securitySummary;
            TokenExpiresOn = tokenExpiresOn;
            
            _logger.LogDebug("Conditional Access info updated: {SecuritySummary}, Expires: {ExpiresOn}", 
                           securitySummary, tokenExpiresOn);
        }

        /// <summary>
        /// Generuje podsumowanie bezpieczeństwa na podstawie analizy Conditional Access
        /// </summary>
        private string GenerateSecuritySummary(ConditionalAccessInfo info)
        {
            var factors = new List<string>();
            
            if (info.RequiresMfa && info.MfaCompleted)
            {
                factors.Add("MFA");
            }
            
            if (info.IsManagedDevice)
            {
                factors.Add("Managed Device");
            }
            
            if (info.IsCompliantDevice)
            {
                factors.Add("Compliant Device");
            }
            
            if (info.IsTrustedLocation)
            {
                factors.Add("Trusted Location");
            }

            if (factors.Count >= 3)
            {
                return "🔐 High Security";
            }
            else if (factors.Count >= 1)
            {
                return "🔐 Medium Security";
            }
            else
            {
                return "🔐 Basic Security";
            }
        }

        private void LoadDashboard()
        {
            ExecuteNavigateToDashboard();
        }

        private void ExecuteNavigateToDashboard()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Dashboard");
                
                // Utwórz DashboardView z DI (z automatycznym DataContext)
                var dashboardView = _serviceProvider.GetRequiredService<Views.Dashboard.DashboardView>();
                var dashboardViewModel = _serviceProvider.GetRequiredService<ViewModels.Dashboard.DashboardViewModel>();
                dashboardView.DataContext = dashboardViewModel;
                
                CurrentView = dashboardView;
                CurrentViewTitle = "Dashboard";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Dashboard");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Dashboard:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToUsers()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Użytkowników");
                
                // Utwórz UserListView z DI i przypisz ViewModel
                var userListView = _serviceProvider.GetRequiredService<Views.Users.UserListView>();
                var userListViewModel = _serviceProvider.GetRequiredService<ViewModels.Users.UserListViewModel>();
                userListView.DataContext = userListViewModel;
                
                CurrentView = userListView;
                CurrentViewTitle = "Zarządzanie Użytkownikami";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Użytkowników");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Użytkowników:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToTeams()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Zespołów");
                
                // Utwórz TeamListView z DI i przypisz ViewModel
                var teamListView = _serviceProvider.GetRequiredService<Views.Teams.TeamListView>();
                var teamListViewModel = _serviceProvider.GetRequiredService<ViewModels.Teams.TeamListViewModel>();
                teamListView.DataContext = teamListViewModel;
                
                CurrentView = teamListView;
                CurrentViewTitle = "Zarządzanie Zespołami";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Zespołów");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Zespołów:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToSchoolTypes()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Typów Szkół");
                
                // Utwórz SchoolTypesListView z DI i przypisz ViewModel
                var schoolTypesView = _serviceProvider.GetRequiredService<Views.SchoolTypes.SchoolTypesListView>();
                var schoolTypesViewModel = _serviceProvider.GetRequiredService<ViewModels.SchoolTypes.SchoolTypesListViewModel>();
                schoolTypesView.DataContext = schoolTypesViewModel;
                
                CurrentView = schoolTypesView;
                CurrentViewTitle = "Zarządzanie Typami Szkół";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Typów Szkół");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Typów Szkół:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToSchoolYears()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Lat Szkolnych");
                
                // Utwórz SchoolYearListView z DI i przypisz ViewModel
                var schoolYearView = _serviceProvider.GetRequiredService<Views.SchoolYears.SchoolYearListView>();
                var schoolYearViewModel = _serviceProvider.GetRequiredService<ViewModels.SchoolYears.SchoolYearListViewModel>();
                schoolYearView.DataContext = schoolYearViewModel;
                
                CurrentView = schoolYearView;
                CurrentViewTitle = "Zarządzanie Latami Szkolnymi";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Lat Szkolnych");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Lat Szkolnych:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToSubjects()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Przedmiotów");
                
                // Utwórz SubjectsView z DI i przypisz ViewModel
                var subjectsView = _serviceProvider.GetRequiredService<Views.Subjects.SubjectsView>();
                var subjectsViewModel = _serviceProvider.GetRequiredService<ViewModels.Subjects.SubjectsViewModel>();
                subjectsView.DataContext = subjectsViewModel;
                
                CurrentView = subjectsView;
                CurrentViewTitle = "Zarządzanie przedmiotami";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Przedmiotów");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Przedmiotów:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToDepartments()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Działów");
                
                // Utwórz DepartmentsManagementView z DI i przypisz ViewModel
                var departmentsView = _serviceProvider.GetRequiredService<Views.Departments.DepartmentsManagementView>();
                var departmentsViewModel = _serviceProvider.GetRequiredService<ViewModels.Departments.DepartmentsManagementViewModel>();
                departmentsView.DataContext = departmentsViewModel;
                
                CurrentView = departmentsView;
                CurrentViewTitle = "Zarządzanie Działami";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Działów");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Działów:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToOrganizationalUnits()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Jednostek Organizacyjnych");
                
                // Utwórz OrganizationalUnitsManagementView z DI i przypisz ViewModel
                var organizationalUnitsView = _serviceProvider.GetRequiredService<Views.OrganizationalUnits.OrganizationalUnitsManagementView>();
                var organizationalUnitsViewModel = _serviceProvider.GetRequiredService<ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel>();
                organizationalUnitsView.DataContext = organizationalUnitsViewModel;
                
                CurrentView = organizationalUnitsView;
                CurrentViewTitle = "Zarządzanie Jednostkami Organizacyjnymi";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Jednostek Organizacyjnych");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Jednostek Organizacyjnych:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToOperationHistory()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Historii Operacji");
                
                // Utwórz OperationHistoryView z DI i przypisz ViewModel
                var operationHistoryView = _serviceProvider.GetRequiredService<Views.Operations.OperationHistoryView>();
                var operationHistoryViewModel = _serviceProvider.GetRequiredService<ViewModels.Operations.OperationHistoryViewModel>();
                operationHistoryView.DataContext = operationHistoryViewModel;
                
                CurrentView = operationHistoryView;
                CurrentViewTitle = "Historia Operacji";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Historii Operacji");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Historii Operacji:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToMonitoring()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Monitoring Dashboard");
                
                // Utwórz MonitoringDashboardView z DI i przypisz ViewModel
                var monitoringView = _serviceProvider.GetRequiredService<Views.Monitoring.MonitoringDashboardView>();
                var monitoringViewModel = _serviceProvider.GetRequiredService<ViewModels.Monitoring.MonitoringDashboardViewModel>();
                monitoringView.DataContext = monitoringViewModel;
                
                CurrentView = monitoringView;
                CurrentViewTitle = "Monitoring Systemu";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Monitoring Dashboard");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Monitoring Dashboard:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToSettings()
        {
            try
            {
                _logger.LogDebug("Nawigacja do Ustawień");
                
                // Utwórz ApplicationSettingsView z DI i przypisz ViewModel
                var settingsView = _serviceProvider.GetRequiredService<Views.Settings.ApplicationSettingsView>();
                var settingsViewModel = _serviceProvider.GetRequiredService<ViewModels.Settings.ApplicationSettingsViewModel>();
                settingsView.DataContext = settingsViewModel;
                
                CurrentView = settingsView;
                CurrentViewTitle = "Ustawienia Aplikacji";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas nawigacji do Ustawień");
                System.Windows.MessageBox.Show($"Błąd nawigacji do Ustawień:\n\n{ex.Message}\n\nInner: {ex.InnerException?.Message}", "Błąd Menu", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNavigateToManualTesting()
        {
            try
            {
                _logger.LogDebug("Otwieranie okna testów manualnych");
                var testingWindow = _serviceProvider.GetRequiredService<ManualTestingWindow>();
                testingWindow.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas otwierania okna testów");
            }
        }

        private async void ExecuteLogout()
        {
            try
            {
                _logger.LogInformation("Wylogowywanie użytkownika...");
                
                // Wyczyść zapisane dane logowania
                await _configManager.ClearLoginSettingsAsync();
                
                // Wyloguj z MSAL
                await _msalAuthService.SignOutAsync();
                
                // Zresetuj informacje o użytkowniku
                UserDisplayName = "Nie zalogowano";
                UserEmail = string.Empty;
                UserJobTitle = string.Empty;
                UserOfficeLocation = string.Empty;
                UserProfilePicture = null;
                
                // Pokaż okno logowania
                IsDialogOpen = true;
                var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
                
                try
                {
                    if (loginWindow.ShowDialog() == true)
                    {
                        // Odśwież informacje o użytkowniku po ponownym zalogowaniu
                        LoadUserInfo();
                    }
                    else
                    {
                        // Zamknij aplikację jeśli użytkownik anulował
                        System.Windows.Application.Current.Shutdown();
                    }
                }
                finally
                {
                    IsDialogOpen = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wylogowywania");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void ExecuteViewProfile()
        {
            try
            {
                _logger.LogInformation("Otwieranie okna profilu użytkownika");
                
                // Pokaż overlay
                IsDialogOpen = true;
                
                var profileWindow = new TeamsManager.UI.Views.UserProfileWindow(this);
                profileWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas otwierania profilu użytkownika");
                System.Windows.MessageBox.Show(
                    $"Błąd podczas otwierania profilu: {ex.Message}",
                    "Błąd",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                // Ukryj overlay
                IsDialogOpen = false;
            }
        }

        private async void ExecuteRefreshProfile()
        {
            try
            {
                _logger.LogInformation("Odświeżanie profilu użytkownika");
                
                await LoadDetailedUserProfileAsync();
                UpdateTokenStatus();
                
                // Krótkie potwierdzenie
                System.Windows.MessageBox.Show(
                    "Profil użytkownika został odświeżony",
                    "Odświeżanie profilu",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania profilu");
                System.Windows.MessageBox.Show(
                    $"Błąd podczas odświeżania profilu: {ex.Message}",
                    "Błąd",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void ExecuteClearTokenCache()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Czy na pewno chcesz wyczyścić cache tokenów?\n\n" +
                    "To spowoduje, że przy następnym logowaniu będziesz musiał/a ponownie przejść przez proces uwierzytelniania.",
                    "Wyczyść Cache Tokenów",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _logger.LogInformation("Czyszczenie cache tokenów");
                    
                    await MsalCacheHelper.ClearTokenCacheAsync(_logger);
                    UpdateTokenStatus();
                    
                    System.Windows.MessageBox.Show(
                        "Cache tokenów został wyczyszczony",
                        "Cache wyczyszczony",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas czyszczenia cache tokenów");
                System.Windows.MessageBox.Show(
                    $"Błąd podczas czyszczenia cache: {ex.Message}",
                    "Błąd",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 