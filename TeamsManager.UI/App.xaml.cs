using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
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
            // --- POCZ¥TEK: REJESTRACJA IMemoryCache ---
            services.AddMemoryCache();
            // --- KONIEC: REJESTRACJA IMemoryCache ---

            // --- POCZ¥TEK: KONFIGURACJA LOGGERA ---
            services.AddLogging(configure =>
            {
                configure.AddDebug();
                configure.SetMinimumLevel(LogLevel.Debug);
            });
            // --- KONIEC: KONFIGURACJA LOGGERA ---

            // --- Konfiguracja ICurrentUserService ---
            // Rejestrujemy jako Singleton, aby ta sama instancja by³a dostêpna 
            // w ca³ej aplikacji UI. Pozwoli to na ustawienie u¿ytkownika 
            // po zalogowaniu i odczytanie go w dowolnym miejscu.
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

            // --- Rejestracja serwisów konfiguracji ---
            services.AddSingleton<ConfigurationManager>();
            services.AddSingleton<ConfigurationValidator>();
            services.AddSingleton<EncryptionService>();

            // --- POCZ¥TEK: MIGRACJA SERWISÓW DO DI (ETAP 3) ---
            
            // Rejestracja ConfigurationProvider
            services.AddSingleton<IMsalConfigurationProvider, MsalConfigurationProvider>();
            
            // Aktualizacja rejestracji MsalAuthService - teraz z dependencies
            services.AddSingleton<IMsalAuthService, MsalAuthService>();
            
            // GraphUserProfileService jest ju¿ zarejestrowany jako Scoped z Etapu 2
            services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
            
            // --- POCZ¥TEK: REJESTRACJA SERWISÓW TESTOWYCH (ETAP 5) ---
            // ManualTestingService jako Singleton - zachowuje stan miêdzy oknami
            services.AddSingleton<IManualTestingService, ManualTestingService>();
            // --- KONIEC: REJESTRACJA SERWISÓW TESTOWYCH (ETAP 5) ---
            
            // --- KONIEC: MIGRACJA SERWISÓW DO DI (ETAP 3) ---

            // --- POCZ¥TEK: KONFIGURACJA HTTPCLIENT ---
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
            // W docelowej architekturze z API, klient WPF raczej nie powinien mieæ
            // bezpoœredniego dostêpu do DbContext. Komunikacja z danymi powinna
            // odbywaæ siê przez TeamsManager.Api.
            // Tê sekcjê mo¿esz zakomentowaæ lub usun¹æ, jeœli UI bêdzie 
            // komunikowaæ siê wy³¹cznie z API.
            // Jeœli jednak chcesz mieæ DbContext dostêpny w UI (np. do testów,
            // lub jeœli czêœæ logiki ma byæ lokalna):
            string uiDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teamsmanager_ui.db");
            services.AddDbContext<TeamsManagerDbContext>(options =>
                options.UseSqlite($"Data Source={uiDbPath}"));

            // --- Rejestracja ViewModeli (Przyk³ady) ---
            // Tutaj w przysz³oœci bêdziesz rejestrowaæ swoje ViewModele,
            // aby mo¿na je by³o wstrzykiwaæ do widoków lub pobieraæ z ServiceProvider.
            // np. services.AddTransient<MainViewModel>();
            //      services.AddTransient<LoginViewModel>();

            // --- POCZ¥TEK: REJESTRACJA OKIEN (ETAP 4) ---
            // Rejestracja g³ównego okna (legacy)
            services.AddTransient<MainWindow>();
            
            // Opcjonalnie: rejestracja innych okien
            services.AddTransient<DashboardWindow>();
            
            // ManualTestingWindow jako Transient - nowa instancja przy ka¿dym otwarciu
            services.AddTransient<ManualTestingWindow>();
            
            // LoginWindow - nowa instancja przy ka¿dym logowaniu
            services.AddTransient<LoginWindow>();
            // --- KONIEC: REJESTRACJA OKIEN (ETAP 4) ---

            // --- POCZ¥TEK: REJESTRACJA SHELL (ETAP 0.1) ---
            // Rejestracja Shell ViewModels
            services.AddTransient<ViewModels.Shell.MainShellViewModel>();

            // Rejestracja Shell Views  
            services.AddTransient<Views.Shell.MainShellWindow>();
            // --- KONIEC: REJESTRACJA SHELL (ETAP 0.1) ---

            // --- POCZ¥TEK: REJESTRACJA DASHBOARD (ETAP 2) ---
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DashboardView>();

            // Tymczasowo: u¿ywamy uproszczonych implementacji dla Dashboard
            // W przysz³oœci bêd¹ zast¹pione komunikacj¹ z API
            services.AddSingleton<ITeamService, SimpleDashboardTeamService>();
            services.AddSingleton<IUserService, SimpleDashboardUserService>();
            services.AddSingleton<IOperationHistoryService, SimpleDashboardOperationHistoryService>();
            // --- KONIEC: REJESTRACJA DASHBOARD (ETAP 2) ---

            // --- POCZ¥TEK: REJESTRACJA APPLICATION SETTINGS (ETAP 1.3) ---
            // Serwis dla ApplicationSettings
            services.AddScoped<ApplicationSettingService>();

            // ViewModel dla ApplicationSettings
            services.AddTransient<ApplicationSettingsViewModel>();

            // Widok ApplicationSettings
            services.AddTransient<ApplicationSettingsView>();
            // --- KONIEC: REJESTRACJA APPLICATION SETTINGS (ETAP 1.3) ---

            // --- POCZ¥TEK: REJESTRACJA SCHOOL TYPES (ETAP 5) ---
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

            // --- POCZ¥TEK: REJESTRACJA SCHOOL YEARS (ETAP 6) ---
            // Core serwisy dla SchoolYears (ju¿ zarejestrowane powy¿ej)
            services.AddScoped<ISchoolYearService, TeamsManager.Core.Services.SchoolYearService>();
            services.AddScoped<ISchoolYearRepository, TeamsManager.Data.Repositories.SchoolYearRepository>();

            // Serwis UI dla SchoolYears
            services.AddTransient<SchoolYearUIService>();

            // ViewModele dla SchoolYears
            services.AddTransient<ViewModels.SchoolYears.SchoolYearListViewModel>();

            // Widoki SchoolYears
            services.AddTransient<Views.SchoolYears.SchoolYearListView>();
            // --- KONIEC: REJESTRACJA SCHOOL YEARS (ETAP 6) ---

            // --- POCZ¥TEK: REJESTRACJA SUBJECTS (ETAP 2.3) ---
            // ViewModele dla Subjects
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

            // --- POCZ¥TEK: REJESTRACJA DEPARTMENTS (ETAP 2.4) ---
            // Core serwisy dla Departments (ju¿ zarejestrowane powy¿ej w innych sekcjach)
            services.AddScoped<IDepartmentService, TeamsManager.Core.Services.DepartmentService>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.Department>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.Department>>();

            // ViewModele dla Departments
            services.AddTransient<ViewModels.Departments.DepartmentsManagementViewModel>();
            
            // Widoki Departments
            services.AddTransient<Views.Departments.DepartmentsManagementView>();
            // --- KONIEC: REJESTRACJA DEPARTMENTS (ETAP 2.4) ---

            // --- POCZ¥TEK: REJESTRACJA OPERATION HISTORY (ETAP 2.5) ---
            // ViewModele dla Operation History
            services.AddTransient<ViewModels.Operations.OperationHistoryViewModel>();
            services.AddTransient<ViewModels.Operations.OperationHistoryItemViewModel>();

            // Widoki Operation History
            services.AddTransient<Views.Operations.OperationHistoryView>();

            // Konwertery dla Operation History (singleton dla wydajnoœci)
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

            // --- POCZ¥TEK: REJESTRACJA USER LIST (ETAP 3.1) ---
            // Core serwisy dla Users ju¿ zarejestrowane powy¿ej (IUserService, IDepartmentService)
            
            // ViewModele dla User List
            services.AddTransient<ViewModels.Users.UserListViewModel>();
            services.AddTransient<ViewModels.Users.UserListItemViewModel>();

            // Widoki User List
            services.AddTransient<Views.Users.UserListView>();

            // UserControls
            services.AddTransient<UserControls.BulkOperationsToolbar>();

            // Konwertery dla User List (singleton dla wydajnoœci)
            services.AddSingleton<Converters.IntToVisibilityConverter>();
            services.AddSingleton<Converters.StringToBrushConverter>();
            // --- KONIEC: REJESTRACJA USER LIST (ETAP 3.1) ---

            // --- POCZ¥TEK: REJESTRACJA USER DETAIL FORM (ETAP 3.2) ---
            // ViewModele dla User Detail Form
            services.AddTransient<ViewModels.Users.UserDetailViewModel>();

            // Widoki User Detail Form
            services.AddTransient<Views.Users.UserDetailWindow>();

            // Konwertery specyficzne dla User Detail Form ju¿ zarejestrowane powy¿ej
            // (InverseBooleanConverter, EnumDescriptionConverter)
            services.AddSingleton<Converters.EnumDescriptionConverter>();
            // --- KONIEC: REJESTRACJA USER DETAIL FORM (ETAP 3.2) ---

            // --- POCZ¥TEK: REJESTRACJA USER SCHOOL TYPE ASSIGNMENT (ETAP 3.4) ---
            // ViewModele dla User School Type Assignment
            services.AddTransient<ViewModels.Users.UserSchoolTypeAssignmentViewModel>();

            // Widoki User School Type Assignment
            services.AddTransient<Views.Users.UserSchoolTypeAssignmentView>();

            // Konwertery dla User School Type Assignment
            services.AddSingleton<Converters.GreaterThanConverter>();
            services.AddSingleton<Converters.EqualToVisibilityConverter>();
            // --- KONIEC: REJESTRACJA USER SCHOOL TYPE ASSIGNMENT (ETAP 3.4) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM LIST VIEW (ETAP 4.1) ---
            // ViewModele dla Team List View
            services.AddTransient<ViewModels.Teams.TeamListViewModel>();

            // Widoki Team List View
            services.AddTransient<Views.Teams.TeamListView>();

            // Konwertery dla Team List View
            services.AddSingleton<Converters.TeamStatusToColorConverter>();
            services.AddSingleton<Converters.TeamStatusToArchiveVisibilityConverter>();
            services.AddSingleton<Converters.TeamStatusToRestoreVisibilityConverter>();
            // --- KONIEC: REJESTRACJA TEAM LIST VIEW (ETAP 4.1) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM CREATION WIZARD (ETAP 4.2) ---
            // ViewModele dla Team Creation Wizard
            services.AddTransient<ViewModels.Teams.TeamCreationWizardViewModel>();

            // Widoki Team Creation Wizard
            services.AddTransient<Views.Teams.TeamCreationWizardWindow>();

            // Konwertery dla Team Creation Wizard
            services.AddSingleton<Converters.StepStatusConverter>();
            // --- KONIEC: REJESTRACJA TEAM CREATION WIZARD (ETAP 4.2) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM MEMBERS MANAGEMENT (ETAP 4.3) ---
            // ViewModele dla Team Members Management
            services.AddTransient<ViewModels.Teams.TeamMembersViewModel>();

            // Widoki Team Members Management
            services.AddTransient<Views.Teams.TeamMembersView>();
            // --- KONIEC: REJESTRACJA TEAM MEMBERS MANAGEMENT (ETAP 4.3) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM CHANNELS MANAGEMENT (ETAP 4.4) ---
            // ViewModele dla Team Channels Management
            services.AddTransient<ViewModels.Teams.TeamChannelsViewModel>();
            services.AddTransient<ViewModels.Teams.ChannelCardViewModel>();

            // Widoki Team Channels Management
            services.AddTransient<Views.Teams.TeamChannelsView>();
            services.AddTransient<UserControls.ChannelCard>();
            // --- KONIEC: REJESTRACJA TEAM CHANNELS MANAGEMENT (ETAP 4.4) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM LIFECYCLE OPERATIONS (ETAP 4.5) ---
            // ViewModele dla Team Lifecycle Operations
            services.AddTransient<ViewModels.Teams.TeamLifecycleDialogViewModel>();

            // Widoki Team Lifecycle Operations
            services.AddTransient<Views.Teams.TeamLifecycleDialog>();
            // --- KONIEC: REJESTRACJA TEAM LIFECYCLE OPERATIONS (ETAP 4.5) ---

            // --- POCZ¥TEK: REJESTRACJA TEAM TEMPLATE EDITOR (ETAP 5.1) ---
            // ViewModele dla Team Template Editor
            services.AddTransient<ViewModels.Teams.TeamTemplateEditorViewModel>();

            // Widoki Team Template Editor
            services.AddTransient<Views.Teams.TeamTemplateEditorWindow>();

            // UserControls dla Team Template Editor
            services.AddTransient<UserControls.Teams.TemplatePreviewControl>();
            services.AddTransient<UserControls.Teams.TokenHelperPanel>();
            // --- KONIEC: REJESTRACJA TEAM TEMPLATE EDITOR (ETAP 5.1) ---

            // --- POCZ¥TEK: REJESTRACJA BULK IMPORT WIZARD (ETAP 5.2) ---
            // Core serwisy dla Import (ju¿ zarejestrowane w API)
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

            // --- POCZ¥TEK: REJESTRACJA REAL-TIME MONITORING (ETAP 5.3) ---
            // Core serwisy dla monitoringu - potrzebne w UI dla demonstracji
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
            
            // Konwertery dla monitoringu (singleton dla wydajnoœci)
            services.AddSingleton<Converters.HealthCheckToColorConverter>();
            services.AddSingleton<Converters.AlertLevelToColorConverter>();
            services.AddSingleton<Converters.ConnectionStateToColorConverter>();
            services.AddSingleton<Converters.PercentageToColorConverter>();
            services.AddSingleton<Converters.TimeSpanToStringConverter>();
            // --- KONIEC: REJESTRACJA REAL-TIME MONITORING (ETAP 5.3) ---
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Przywróæ normalny ShutdownMode i uruchom g³ówne okno
                System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                
                // Tworzenie MainShellWindow przez DI
                var mainShellWindow = ServiceProvider.GetRequiredService<MainShellWindow>();
                mainShellWindow.Show();
                
                // Weryfikacja DI (debug)
                System.Diagnostics.Debug.WriteLine($"[DI Test] MainShellWindow created via DI: {mainShellWindow != null}");

                // Sprawdzenie serwisów dla debugowania
                var currentUserService = ServiceProvider.GetRequiredService<ICurrentUserService>();
                System.Diagnostics.Debug.WriteLine($"[UI DI Test] Current User UPN: {currentUserService.GetCurrentUserUpn()}");

                // Weryfikacja serwisów z Etapów 2-3
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

                // Sprawdzenie DbContext
                try
                {
                    var dbContext = ServiceProvider.GetRequiredService<TeamsManagerDbContext>();
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] DbContext instance created: {dbContext != null}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] Error creating DbContext: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"B³¹d podczas tworzenia g³ównego okna:\n\n{ex.Message}\n\nSprawdŸ konfiguracjê serwisów.",
                    "B³¹d krytyczny",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}
