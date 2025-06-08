using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Services.UserContext;
using TeamsManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Views;
using TeamsManager.UI.Views.Shell;
using TeamsManager.UI.ViewModels.Shell;
using TeamsManager.UI.Views.Dashboard;
using TeamsManager.UI.ViewModels.Dashboard;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.Services.Dashboard;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using TeamsManager.UI.Services.Http;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services;
using Polly;
using TeamsManager.UI.ViewModels.Settings;
using TeamsManager.UI.Views.Settings;
using TeamsManager.UI.Services.UI;
using TeamsManager.UI.ViewModels.SchoolTypes;
using TeamsManager.UI.Views.SchoolTypes;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Extensions;

namespace TeamsManager.UI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        public App()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // --- POCZĄTEK: KONFIGURACJA ICONFIGURATION ---
            var configurationBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
            // --- KONIEC: KONFIGURACJA ICONFIGURATION ---

            // --- POCZĄTEK: REJESTRACJA IMemoryCache ---
            services.AddMemoryCache();
            // --- KONIEC: REJESTRACJA IMemoryCache ---

            // --- POCZĄTEK: KONFIGURACJA LOGGERA ---
            services.AddLogging(configure =>
            {
                configure.AddDebug();
                configure.SetMinimumLevel(LogLevel.Debug);
            });
            // --- KONIEC: KONFIGURACJA LOGGERA ---

            // --- Konfiguracja ICurrentUserService ---
            // Rejestrujemy jako Singleton, aby ta sama instancja by�a dost�pna 
            // w ca�ej aplikacji UI. Pozwoli to na ustawienie u�ytkownika 
            // po zalogowaniu i odczytanie go w dowolnym miejscu.
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

            // --- Rejestracja serwis�w konfiguracji ---
            services.AddSingleton<TeamsManager.UI.Services.Configuration.ConfigurationManager>();
            services.AddSingleton<ConfigurationValidator>();
            services.AddSingleton<EncryptionService>();

            // --- POCZĄTEK: MIGRACJA SERWISW DO DI (ETAP 3) ---
            
            // Rejestracja ConfigurationProvider
            services.AddSingleton<IMsalConfigurationProvider, MsalConfigurationProvider>();
            
            // Aktualizacja rejestracji MsalAuthService - teraz z dependencies
            services.AddSingleton<IMsalAuthService, MsalAuthService>();
            
            // GraphUserProfileService jest ju� zarejestrowany jako Scoped z Etapu 2
            services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
            
            // --- POCZĄTEK: REJESTRACJA SERWISW TESTOWYCH (ETAP 5) ---
            // ManualTestingService jako Singleton - zachowuje stan mi�dzy oknami
            services.AddSingleton<IManualTestingService, ManualTestingService>();
            // --- KONIEC: REJESTRACJA SERWIS�W TESTOWYCH (ETAP 5) ---
            
            // --- KONIEC: MIGRACJA SERWIS�W DO DI (ETAP 3) ---

            // --- POCZĄTEK: KONFIGURACJA HTTPCLIENT ---
            // Rejestracja TokenAuthorizationHandler
            services.AddTransient<TokenAuthorizationHandler>();

            // Konfiguracja HttpClient dla Microsoft Graph - wzorowane na API
            services.AddHttpClient("MicrosoftGraph", client =>
            {
                client.BaseAddress = new Uri("https://graph.microsoft.com/");
                client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager-UI/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<TokenAuthorizationHandler>()
            .AddStandardResilienceHandler(options =>
            {
                // Retry Policy - skopiowane z API
                options.Retry.ShouldHandle = args => args.Outcome switch
                {
                    { } outcome when HttpClientResiliencePredicates.IsTransient(outcome) => PredicateResult.True(),
                    { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests => PredicateResult.True(),
                    { } outcome when outcome.Result?.StatusCode == System.Net.HttpStatusCode.RequestTimeout => PredicateResult.True(),
                    _ => PredicateResult.False()
                };
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.UseJitter = true;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                
                // Circuit Breaker - uproszczone dla UI
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);

                // Timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
            });

            // Default HttpClient bez specjalnej konfiguracji
            services.AddHttpClient();
            // --- KONIEC: KONFIGURACJA HTTPCLIENT ---

            // --- (Opcjonalnie) Konfiguracja TeamsManagerDbContext ---
            // W docelowej architekturze z API, klient WPF raczej nie powinien mie�
            // bezpo�redniego dost�pu do DbContext. Komunikacja z danymi powinna
            // odbywa� si� przez TeamsManager.Api.
            // T� sekcj� mo�esz zakomentowa� lub usun��, je�li UI b�dzie 
            // komunikowa� si� wy��cznie z API.
            // Je�li jednak chcesz mie� DbContext dost�pny w UI (np. do test�w,
            // lub je�li cz�� logiki ma by� lokalna):
            
            // BEZPIECZNA KONFIGURACJA BAZY DANYCH DLA PRODUCTION
            var connectionString = GetDatabaseConnectionString(configuration);
            
            services.AddDbContext<TeamsManagerDbContext>(options =>
                options.UseSqlite(connectionString));

            // --- Rejestracja ViewModeli (Przyk�ady) ---
            // Tutaj w przysz�o�ci b�dziesz rejestrowa� swoje ViewModele,
            // aby mo�na je by�o wstrzykiwa� do widok�w lub pobiera� z ServiceProvider.
            // np. services.AddTransient<MainViewModel>();
            //      services.AddTransient<LoginViewModel>();

            // --- POCZĄTEK: REJESTRACJA OKIEN (ETAP 4) ---
            // Rejestracja okien
            
            // Opcjonalnie: rejestracja innych okien

            
            // ManualTestingWindow jako Transient - nowa instancja przy ka�dym otwarciu
            services.AddTransient<ManualTestingWindow>();
            
            // LoginWindow - nowa instancja przy ka�dym logowaniu
            services.AddTransient<LoginWindow>();
            // --- KONIEC: REJESTRACJA OKIEN (ETAP 4) ---

            // --- POCZĄTEK: REJESTRACJA SHELL (ETAP 0.1) ---
            // Rejestracja Shell ViewModels
            services.AddTransient<ViewModels.Shell.MainShellViewModel>();

            // Rejestracja Shell Views  
            services.AddTransient<Views.Shell.MainShellWindow>();
            // --- KONIEC: REJESTRACJA SHELL (ETAP 0.1) ---

            // --- POCZĄTEK: REJESTRACJA DASHBOARD (ETAP 2) ---
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DashboardView>();

            // Serwisy używane przez Dashboard - MOCK dla rozwoju
            services.AddSingleton<ITeamService, SimpleDashboardTeamService>();
            // services.AddSingleton<IUserService, SimpleDashboardUserService>(); // USUNIĘTE: zastępuje prostym serwisem z bazy
            services.AddSingleton<IOperationHistoryService, SimpleDashboardOperationHistoryService>();
            
            // Prosta implementacja IUserService korzystająca z bazy danych
            services.AddScoped<IUserService, SimpleUserService>();
            // --- KONIEC: REJESTRACJA DASHBOARD (ETAP 2) ---

            // --- POCZĄTEK: REJESTRACJA APPLICATION SETTINGS (ETAP 1.3) ---
            // Serwis dla ApplicationSettings
            services.AddScoped<ApplicationSettingService>();

            // ViewModel dla ApplicationSettings
            services.AddTransient<ApplicationSettingsViewModel>();

            // Widok ApplicationSettings
            services.AddTransient<ApplicationSettingsView>();
            // --- KONIEC: REJESTRACJA APPLICATION SETTINGS (ETAP 1.3) ---

            // --- POCZĄTEK: REJESTRACJA SCHOOL TYPES (ETAP 5) ---
            // Core serwisy dla SchoolTypes (tymczasowo w UI - docelowo przez API)
            services.AddScoped<ISchoolTypeService, TeamsManager.Core.Services.SchoolTypeService>();
            services.AddScoped<INotificationService, TeamsManager.Core.Services.StubNotificationService>();
            
            // Dodatkowe serwisy wymagane przez SchoolTypeService
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.SchoolType>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.SchoolType>>();
            services.AddScoped<IUserRepository, TeamsManager.Data.Repositories.UserRepository>();
            services.AddScoped<IOperationHistoryService, SimpleDashboardOperationHistoryService>();
            services.AddScoped<IPowerShellCacheService, TeamsManager.Core.Services.PowerShell.PowerShellCacheService>();

            // Serwis UI dla SchoolTypes
            services.AddTransient<SchoolTypeUIService>();

            // ViewModele dla SchoolTypes
            services.AddTransient<ViewModels.SchoolTypes.SchoolTypesListViewModel>();

            // Widoki SchoolTypes
            services.AddTransient<Views.SchoolTypes.SchoolTypesListView>();
            // --- KONIEC: REJESTRACJA SCHOOL TYPES (ETAP 5) ---

            // --- POCZĄTEK: REJESTRACJA SCHOOL YEARS (ETAP 6) ---
            // Core serwisy dla SchoolYears (ju� zarejestrowane powy�ej)
            services.AddScoped<ISchoolYearService, TeamsManager.Core.Services.SchoolYearService>();
            services.AddScoped<ISchoolYearRepository, TeamsManager.Data.Repositories.SchoolYearRepository>();
            
            // Dodatkowe zależności wymagane przez SchoolYearService
            services.AddScoped<ITeamRepository, TeamsManager.Data.Repositories.TeamRepository>();

            // Serwis UI dla SchoolYears
            services.AddTransient<SchoolYearUIService>();

            // ViewModele dla SchoolYears
            services.AddTransient<ViewModels.SchoolYears.SchoolYearListViewModel>();

            // Widoki SchoolYears
            services.AddTransient<Views.SchoolYears.SchoolYearListView>();
            // --- KONIEC: REJESTRACJA SCHOOL YEARS (ETAP 6) ---

            // --- POCZĄTEK: REJESTRACJA SUBJECTS (ETAP 2.3) ---
            // ViewModele dla Subjects
            // Core serwisy dla Subjects
            services.AddScoped<ISubjectService, TeamsManager.Core.Services.SubjectService>();
            services.AddScoped<ISubjectRepository, TeamsManager.Data.Repositories.SubjectRepository>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.Subject>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.Subject>>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.UserSubject>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.UserSubject>>();

            services.AddTransient<ViewModels.Subjects.SubjectsViewModel>();
            services.AddTransient<ViewModels.Subjects.SubjectEditViewModel>();
            services.AddTransient<ViewModels.Subjects.SubjectImportViewModel>();

            // Widoki Subjects
            services.AddTransient<Views.Subjects.SubjectsView>();
            services.AddTransient<Views.Subjects.SubjectEditDialog>();
            services.AddTransient<Views.Subjects.SubjectImportDialog>();
            services.AddTransient<Views.Subjects.SubjectTeachersDialog>();
            
            // Common dialogs
            services.AddTransient<Views.Common.ConfirmationDialog>();

            // UI Services
            services.AddSingleton<Services.Abstractions.IUIDialogService, Services.UIDialogService>();
            // --- KONIEC: REJESTRACJA SUBJECTS (ETAP 2.3) ---

            // --- POCZĄTEK: REJESTRACJA DEPARTMENTS (ETAP 2.4) ---
            // Core serwisy dla Departments (ju� zarejestrowane powy�ej w innych sekcjach)
            services.AddScoped<IDepartmentService, TeamsManager.Core.Services.DepartmentService>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.Department>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.Department>>();

            // ViewModele dla Departments
            services.AddTransient<ViewModels.Departments.DepartmentsManagementViewModel>();
            
            // Widoki Departments
            services.AddTransient<Views.Departments.DepartmentsManagementView>();
            // --- KONIEC: REJESTRACJA DEPARTMENTS (ETAP 2.4) ---

            // --- POCZĄTEK: REJESTRACJA OPERATION HISTORY (ETAP 2.5) ---
            // ViewModele dla Operation History
            services.AddTransient<ViewModels.Operations.OperationHistoryViewModel>();
            services.AddTransient<ViewModels.Operations.OperationHistoryItemViewModel>();

            // Widoki Operation History
            services.AddTransient<Views.Operations.OperationHistoryView>();

            // Konwertery dla Operation History (singleton dla wydajno�ci)
            services.AddSingleton<Converters.OperationTypeToIconConverter>();
            services.AddSingleton<Converters.OperationStatusToColorConverter>();
            services.AddSingleton<Converters.OperationStatusToTextColorConverter>();
            services.AddSingleton<Converters.TimeSpanToReadableConverter>();
            services.AddSingleton<Converters.DateTimeToRelativeConverter>();
            services.AddSingleton<Converters.ProgressToPercentageConverter>();
            services.AddSingleton<Converters.ProgressToTextConverter>();
            services.AddSingleton<Converters.BooleanToVisibilityConverter>();
            services.AddSingleton<Converters.InverseBooleanToVisibilityConverter>();
            // --- KONIEC: REJESTRACJA OPERATION HISTORY (ETAP 2.5) ---

            // --- POCZĄTEK: REJESTRACJA USER LIST (ETAP 3.1) ---
            // Core serwisy dla Users ju� zarejestrowane powy�ej (IUserService, IDepartmentService)
            
            // ViewModele dla User List
            services.AddScoped<ViewModels.Users.UserListViewModel>(); // Scoped - zachowaj między nawigacją
            services.AddTransient<ViewModels.Users.UserListItemViewModel>();

            // Widoki User List
            services.AddTransient<Views.Users.UserListView>();

            // UserControls
            services.AddTransient<UserControls.BulkOperationsToolbar>();

            // Konwertery dla User List (singleton dla wydajno�ci)
            services.AddSingleton<Converters.IntToVisibilityConverter>();
            services.AddSingleton<Converters.StringToBrushConverter>();
            // --- KONIEC: REJESTRACJA USER LIST (ETAP 3.1) ---

            // --- POCZĄTEK: REJESTRACJA USER DETAIL FORM (ETAP 3.2) ---
            // ViewModele dla User Detail Form
            services.AddTransient<ViewModels.Users.UserDetailViewModel>();

            // Widoki User Detail Form
            services.AddTransient<Views.Users.UserDetailWindow>();

            // Konwertery specyficzne dla User Detail Form ju� zarejestrowane powy�ej
            // (InverseBooleanConverter, EnumDescriptionConverter)
            services.AddSingleton<Converters.EnumDescriptionConverter>();
            // --- KONIEC: REJESTRACJA USER DETAIL FORM (ETAP 3.2) ---

            // --- POCZĄTEK: REJESTRACJA USER SCHOOL TYPE ASSIGNMENT (ETAP 3.4) ---
            // ViewModele dla User School Type Assignment
            services.AddTransient<ViewModels.Users.UserSchoolTypeAssignmentViewModel>();

            // Widoki User School Type Assignment
            services.AddTransient<Views.Users.UserSchoolTypeAssignmentView>();

            // Konwertery dla User School Type Assignment
            services.AddSingleton<Converters.GreaterThanConverter>();
            services.AddSingleton<Converters.EqualToVisibilityConverter>();
            // --- KONIEC: REJESTRACJA USER SCHOOL TYPE ASSIGNMENT (ETAP 3.4) ---

            // --- POCZĄTEK: REJESTRACJA TEAM LIST VIEW (ETAP 4.1) ---
            // ViewModele dla Team List View
            services.AddTransient<ViewModels.Teams.TeamListViewModel>();

            // Widoki Team List View
            services.AddTransient<Views.Teams.TeamListView>();

            // Konwertery dla Team List View
            services.AddSingleton<Converters.TeamStatusToColorConverter>();
            services.AddSingleton<Converters.TeamStatusToArchiveVisibilityConverter>();
            services.AddSingleton<Converters.TeamStatusToRestoreVisibilityConverter>();
            // --- KONIEC: REJESTRACJA TEAM LIST VIEW (ETAP 4.1) ---

            // --- POCZĄTEK: REJESTRACJA TEAM CREATION WIZARD (ETAP 4.2) ---
            // ViewModele dla Team Creation Wizard
            services.AddTransient<ViewModels.Teams.TeamCreationWizardViewModel>();

            // Widoki Team Creation Wizard
            services.AddTransient<Views.Teams.TeamCreationWizardWindow>();

            // Konwertery dla Team Creation Wizard
            services.AddSingleton<Converters.StepStatusConverter>();
            // --- KONIEC: REJESTRACJA TEAM CREATION WIZARD (ETAP 4.2) ---

            // --- POCZĄTEK: REJESTRACJA TEAM MEMBERS MANAGEMENT (ETAP 4.3) ---
            // ViewModele dla Team Members Management
            services.AddTransient<ViewModels.Teams.TeamMembersViewModel>();

            // Widoki Team Members Management
            services.AddTransient<Views.Teams.TeamMembersView>();
            // --- KONIEC: REJESTRACJA TEAM MEMBERS MANAGEMENT (ETAP 4.3) ---

            // --- POCZĄTEK: REJESTRACJA TEAM CHANNELS MANAGEMENT (ETAP 4.4) ---
            // ViewModele dla Team Channels Management
            services.AddTransient<ViewModels.Teams.TeamChannelsViewModel>();
            services.AddTransient<ViewModels.Teams.ChannelCardViewModel>();

            // Widoki Team Channels Management
            services.AddTransient<Views.Teams.TeamChannelsView>();
            services.AddTransient<UserControls.ChannelCard>();
            // --- KONIEC: REJESTRACJA TEAM CHANNELS MANAGEMENT (ETAP 4.4) ---

            // --- POCZĄTEK: REJESTRACJA TEAM LIFECYCLE OPERATIONS (ETAP 4.5) ---
            // ViewModele dla Team Lifecycle Operations
            services.AddTransient<ViewModels.Teams.TeamLifecycleDialogViewModel>();

            // Widoki Team Lifecycle Operations
            services.AddTransient<Views.Teams.TeamLifecycleDialog>();
            // --- KONIEC: REJESTRACJA TEAM LIFECYCLE OPERATIONS (ETAP 4.5) ---

            // --- POCZĄTEK: REJESTRACJA TEAM TEMPLATE EDITOR (ETAP 5.1) ---
            // ViewModele dla Team Template Editor
            services.AddTransient<ViewModels.Teams.TeamTemplateEditorViewModel>();

            // Widoki Team Template Editor
            services.AddTransient<Views.Teams.TeamTemplateEditorWindow>();

            // UserControls dla Team Template Editor
            services.AddTransient<UserControls.Teams.TemplatePreviewControl>();
            services.AddTransient<UserControls.Teams.TokenHelperPanel>();
            // --- KONIEC: REJESTRACJA TEAM TEMPLATE EDITOR (ETAP 5.1) ---

            // --- POCZĄTEK: REJESTRACJA BULK IMPORT WIZARD (ETAP 5.2) ---
            // Core serwisy dla Import (ju� zarejestrowane w API)
            services.AddScoped<IDataImportOrchestrator, TeamsManager.Application.Services.DataImportOrchestrator>();

            // ViewModele dla Bulk Import Wizard
            services.AddTransient<ViewModels.Import.BulkImportWizardViewModel>();
            services.AddTransient<ViewModels.Import.ImportFileSelectionViewModel>();
            services.AddTransient<ViewModels.Import.ImportColumnMappingViewModel>();
            services.AddTransient<ViewModels.Import.ImportValidationViewModel>();
            services.AddTransient<ViewModels.Import.ImportProgressViewModel>();

            // Widoki Bulk Import Wizard
            services.AddTransient<Views.Import.BulkImportWizardWindow>();

            // UserControls dla Bulk Import Wizard
            services.AddTransient<UserControls.Import.FileSelectionStep>();
            // --- KONIEC: REJESTRACJA BULK IMPORT WIZARD (ETAP 5.2) ---

            // --- POCZĄTEK: REJESTRACJA REAL-TIME MONITORING (ETAP 5.3) ---
            // Core serwisy dla monitoringu - potrzebne w UI dla demonstracji
            
            // Rejestracja Authentication Services wymaganych przez PowerShell
            services.AddScoped<TeamsManager.Core.Abstractions.Services.Auth.ITokenManager, TeamsManager.Core.Services.Auth.TokenManager>();
            
            // Mock IConfidentialClientApplication dla TokenManager (nie używane w UI, ale wymagane przez DI)
            services.AddScoped<Microsoft.Identity.Client.IConfidentialClientApplication>(provider =>
            {
                return Microsoft.Identity.Client.ConfidentialClientApplicationBuilder.Create("mock-client-id")
                    .WithClientSecret("mock-secret")
                    .WithAuthority(new Uri("https://login.microsoftonline.com/mock-tenant"))
                    .Build();
            });
            
            services.AddPowerShellServices(); // Rejestruje IPowerShellConnectionService i inne serwisy PowerShell
            
            services.AddScoped<IHealthMonitoringOrchestrator, TeamsManager.Application.Services.HealthMonitoringOrchestrator>();
            services.AddScoped<TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService, TeamsManager.Core.Services.Cache.CacheInvalidationService>();
            
            // Serwisy monitoringu dla UI
            services.AddSingleton<ISignalRService, SignalRService>();
            services.AddScoped<IMonitoringDataService, MonitoringDataService>();
            services.AddSingleton<IMonitoringPerformanceOptimizer, MonitoringPerformanceOptimizer>();
            
            // ViewModele dla monitoringu
            services.AddTransient<ViewModels.Monitoring.MonitoringDashboardViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.SystemHealthWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.PerformanceMetricsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.AlertsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.AdvancedPerformanceChartWidgetViewModel>();
            
            // Widoki monitoringu
            services.AddTransient<Views.Monitoring.MonitoringDashboardView>();
            services.AddTransient<Views.Monitoring.Widgets.SystemHealthWidget>();
            services.AddTransient<Views.Monitoring.Widgets.PerformanceMetricsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.ActiveOperationsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.AlertsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.AdvancedPerformanceChartWidget>();
            
            // Konwertery dla monitoringu (singleton dla wydajno�ci)
            services.AddSingleton<Converters.HealthCheckToColorConverter>();
            services.AddSingleton<Converters.AlertLevelToColorConverter>();
            services.AddSingleton<Converters.ConnectionStateToColorConverter>();
            services.AddSingleton<Converters.PercentageToColorConverter>();
            // services.AddSingleton<Converters.TimeSpanToStringConverter>(); // Konwerter nie istnieje
            // --- KONIEC: REJESTRACJA REAL-TIME MONITORING (ETAP 5.3) ---

            // --- POCZĄTEK: REJESTRACJA BRAKUJĄCYCH VIEWMODELI (ETAP 6.0) ---
            
            // LoginViewModel - używany w LoginWindow
            services.AddTransient<ViewModels.LoginViewModel>();
            
            // Core serwisy które mogą być używane w różnych miejscach
            services.AddScoped<ITeamTemplateService, TeamsManager.Core.Services.TeamTemplateService>();
            services.AddScoped<ITeamTemplateRepository, TeamsManager.Data.Repositories.TeamTemplateRepository>();
            services.AddScoped<IChannelService, TeamsManager.Core.Services.ChannelService>();
            services.AddScoped<IModernHttpService, TeamsManager.Core.Services.ModernHttpService>();
            
            // Application Services (Orchestrators)
            services.AddScoped<ITeamLifecycleOrchestrator, TeamsManager.Application.Services.TeamLifecycleOrchestrator>();
            services.AddScoped<IBulkUserManagementOrchestrator, TeamsManager.Application.Services.BulkUserManagementOrchestrator>();
            services.AddScoped<IReportingOrchestrator, TeamsManager.Application.Services.ReportingOrchestrator>();
            services.AddScoped<ISchoolYearProcessOrchestrator, TeamsManager.Application.Services.SchoolYearProcessOrchestrator>();
            
            // Additional repositories needed by orchestrators
            services.AddScoped<IApplicationSettingRepository, TeamsManager.Data.Repositories.ApplicationSettingRepository>();
            services.AddScoped<IOperationHistoryRepository, TeamsManager.Data.Repositories.OperationHistoryRepository>();
            
            // Unit of Work pattern - sprawdzę czy istnieje implementacja
            // services.AddScoped<IUnitOfWork, TeamsManager.Data.UnitOfWork>(); // TODO: Dodać implementację UnitOfWork
            
            // Notification services
            services.AddScoped<IAdminNotificationService, TeamsManager.Core.Services.StubAdminNotificationService>();
            
            // UI Helper Services (nie implementują interfejsów ale są używane przez ViewModele)
            services.AddTransient<Services.UI.DepartmentTreeService>();
            
            // Brakujące konvertery (singleton dla wydajności)
            services.AddSingleton<Converters.InverseBooleanConverter>();
            services.AddSingleton<Converters.NullToVisibilityConverter>();
            services.AddSingleton<Converters.StringToVisibilityConverter>();
            services.AddSingleton<Converters.StringToBoolConverter>();
            services.AddSingleton<Converters.StringToDateConverter>();
            services.AddSingleton<Converters.StringToTimeConverter>();
            services.AddSingleton<Converters.TeamMemberRoleToStringConverter>();
            services.AddSingleton<Converters.WorkloadToColorConverter>();
            services.AddSingleton<Converters.WorkloadToColorSingleConverter>();
            services.AddSingleton<Converters.HierarchyLevelToMarginConverter>();
            services.AddSingleton<Converters.BoolToIconConverter>();
            services.AddSingleton<Converters.BoolToBackgroundConverter>();
            services.AddSingleton<Converters.ColorToBrushConverter>();
            // services.AddSingleton<Converters.OperationTypeToPolishNameConverter>(); // Konwerter nie istnieje
            
            // Brakujące UserControls
            services.AddTransient<UserControls.Teams.TestDataDialog>();
            
            // --- KONIEC: REJESTRACJA BRAKUJĄCYCH VIEWMODELI (ETAP 6.0) ---
        }

        /// <summary>
        /// Tworzy bezpieczny connection string dla bazy danych lokalnej
        /// </summary>
        private string GetDatabaseConnectionString(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            try
            {
                // Sprawdź czy jest zdefiniowany connection string w konfiguracji
                var configConnectionString = configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrEmpty(configConnectionString))
                {
                    System.Diagnostics.Debug.WriteLine($"[Database] Używam connection string z konfiguracji");
                    return configConnectionString;
                }

                // BEZPIECZNA LOKALIZACJA: LocalApplicationData
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolderPath = Path.Combine(appDataPath, "TeamsManager");
                
                // Upewnij się, że folder aplikacji istnieje
                if (!Directory.Exists(appFolderPath))
                {
                    Directory.CreateDirectory(appFolderPath);
                    System.Diagnostics.Debug.WriteLine($"[Database] Utworzono folder aplikacji: {appFolderPath}");
                }
                
                var dbPath = Path.Combine(appFolderPath, "teamsmanager.db");
                
                // Logowanie ścieżki
                System.Diagnostics.Debug.WriteLine($"[Database] Ścieżka bazy danych: {dbPath}");
                System.Diagnostics.Debug.WriteLine($"[Database] Folder aplikacji: {appFolderPath}");
                
                // MIGRACJA DANYCH Z STAREJ LOKALIZACJI (compatibility)
                var oldDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teamsmanager_ui.db");
                if (File.Exists(oldDbPath) && !File.Exists(dbPath))
                {
                    try
                    {
                        File.Copy(oldDbPath, dbPath, overwrite: false);
                        System.Diagnostics.Debug.WriteLine($"[Database] ✅ Zmigrowano bazę z {oldDbPath}");
                        
                        // Opcjonalnie: usuń starą bazę po udanej migracji
                        // File.Delete(oldDbPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Database] ⚠️ Błąd migracji: {ex.Message}");
                    }
                }
                
                return $"Data Source={dbPath}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Database] ❌ Błąd konfiguracji bazy danych: {ex.Message}");
                
                // Fallback do obecnego katalogu (dla przypadków krytycznych)
                var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teamsmanager_fallback.db");
                return $"Data Source={fallbackPath}";
            }
        }

        private async Task InitializeDatabaseAsync(TeamsManagerDbContext context)
        {
            try
            {
                // Sprawdź czy baza danych istnieje i utwórz ją jeśli nie
                await context.Database.EnsureCreatedAsync();
                
                // Sprawdź czy dane już istnieją
                var usersCount = await context.Users.CountAsync();
                var departmentsCount = await context.Departments.CountAsync();
                
                System.Diagnostics.Debug.WriteLine($"[Database] Users: {usersCount}, Departments: {departmentsCount}");
                
                // Jeśli baza jest pusta, dodaj przykładowe dane
                if (usersCount == 0 && departmentsCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[Database] Baza danych jest pusta, dodawanie przykładowych danych...");
                    await TeamsManager.Data.TestDataSeeder.SeedAsync(context);
                    System.Diagnostics.Debug.WriteLine("[Database] Przykładowe dane zostały dodane.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[Database] Baza danych zawiera już dane.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Database] Błąd podczas inicjalizacji bazy danych: {ex.Message}");
                MessageBox.Show(
                    $"Błąd podczas inicjalizacji bazy danych:\n\n{ex.Message}", 
                    "Błąd bazy danych", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Przywr�� normalny ShutdownMode i uruchom g��wne okno
                System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                
                // Tworzenie MainShellWindow przez DI
                var mainShellWindow = ServiceProvider.GetRequiredService<MainShellWindow>();
                mainShellWindow.Show();
                
                // Weryfikacja DI (debug)
                System.Diagnostics.Debug.WriteLine($"[DI Test] MainShellWindow created via DI: {mainShellWindow != null}");

                // Sprawdzenie serwis�w dla debugowania
                var currentUserService = ServiceProvider.GetRequiredService<ICurrentUserService>();
                System.Diagnostics.Debug.WriteLine($"[UI DI Test] Current User UPN: {currentUserService.GetCurrentUserUpn()}");

                // Weryfikacja serwis�w z Etap�w 2-3
                var httpClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IHttpClientFactory: {httpClientFactory != null}");

                var configProvider = ServiceProvider.GetService<TeamsManager.UI.Services.Configuration.IMsalConfigurationProvider>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IMsalConfigurationProvider: {configProvider != null}");

                var msalService = ServiceProvider.GetService<IMsalAuthService>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IMsalAuthService: {msalService != null}");

                var graphService = ServiceProvider.GetService<IGraphUserProfileService>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IGraphUserProfileService: {graphService != null}");

                // Test konfiguracji MSAL
                if (configProvider != null && configProvider.TryLoadConfiguration(out var msalConfig))
                {
                    System.Diagnostics.Debug.WriteLine($"[Config Test] MSAL configuration loaded successfully");
                    System.Diagnostics.Debug.WriteLine($"[Config Test] ClientId: {msalConfig?.AzureAd.ClientId}, Scopes: {string.Join(", ", msalConfig?.Scopes ?? new string[0])}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Config Test] Failed to load MSAL configuration");
                }

                // Sprawdzenie DbContext i seedowanie danych
                try
                {
                    var dbContext = ServiceProvider.GetRequiredService<TeamsManagerDbContext>();
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] DbContext instance created: {dbContext != null}");
                    
                    // Automatyczne seedowanie danych jeśli baza jest pusta (asynchronicznie)
                    _ = Task.Run(async () => await InitializeDatabaseAsync(dbContext));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] Error creating DbContext: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"B��d podczas tworzenia g��wnego okna:\n\n{ex.Message}\n\nSprawd� konfiguracj� serwis�w.",
                    "B��d krytyczny",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}
