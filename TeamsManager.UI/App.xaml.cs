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
using TeamsManager.UI.Views;
using TeamsManager.UI.Views.Shell;
using TeamsManager.UI.ViewModels.Shell;
using TeamsManager.UI.ViewModels;
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
using TeamsManager.Core.Extensions;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Services;
using TeamsManager.Core.Services.Synchronization;
using TeamsManager.Core.Abstractions.Services.Synchronization;
using TeamsManager.Core.Common;
using Microsoft.Identity.Client;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.Tools;

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
            
            // Inicjalizuj konfigurację
            InitializeConfigurationAsync().ConfigureAwait(false);
        }

        private async Task InitializeConfigurationAsync()
        {
            try
            {
                // Inicjalizacja konfiguracji - tymczasowo wyłączona
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Log błąd inicjalizacji konfiguracji
                System.Diagnostics.Debug.WriteLine($"Błąd inicjalizacji konfiguracji: {ex.Message}");
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {

            var configurationBuilder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();
            services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);


            services.AddMemoryCache();

            services.AddLogging(configure =>
            {
                configure.AddDebug();
                configure.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                
                // Upewnij się, że katalog logs istnieje
                var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamsManager", "logs");
                Directory.CreateDirectory(logsPath);
                
                // Dodaj FileLoggerProvider do zapisywania logów do plików UTF-8
                configure.AddProvider(new Services.Configuration.FileLoggerProvider(logsPath));
            });

            // Konfiguracja ICurrentUserService
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

            // NOWY SYSTEM KONFIGURACJI V2.0
            services.AddSingleton<AdvancedEncryptionService>();
            services.AddSingleton<IConfigurationManagerV2, ConfigurationManagerV2>();
            services.AddSingleton<ConfigurationInitializer>();
            
            // Okno i ViewModel konfiguracji
            services.AddTransient<ConfigurationSetupWindow>();
            services.AddTransient<ConfigurationSetupViewModel>();
            
            // DEBUG TOOL dla konfiguracji
            // services.AddTransient<TeamsManager.UI.Tools.ConfigurationDebugTool>();
            
            // Rejestracja IPublicClientApplication dla MsalAuthService - MINIMALNA STATYCZNA KONFIGURACJA
            // BEZ DYNAMICZNEGO ŁADOWANIA - tylko placeholder dla testów
            services.AddSingleton<Microsoft.Identity.Client.IPublicClientApplication>(provider =>
            {
                try
                {
                    var logger = provider.GetRequiredService<ILogger<App>>();
                    logger.LogInformation("Tworzenie prostego MSAL PublicClientApplication...");
                    
                    // MINIMALNA STATYCZNA KONFIGURACJA - bez żadnego dynamicznego ładowania
                    var app = Microsoft.Identity.Client.PublicClientApplicationBuilder
                        .Create("00000000-0000-0000-0000-000000000000") // Placeholder Client ID
                        .WithAuthority(new Uri("https://login.microsoftonline.com/common"))
                        .WithRedirectUri("http://localhost")
                        .Build();
                    
                    logger.LogInformation("MSAL PublicClientApplication utworzone pomyślnie");
                    return app;
                }
                catch (Exception ex)
                {
                    // Ostatni fallback - logujemy do konsoli
                    Console.WriteLine($"[MSAL] Błąd podczas tworzenia MSAL: {ex.Message}");
                    Console.WriteLine($"[MSAL] Stack trace: {ex.StackTrace}");
                    
                    // Bardzo prosta konfiguracja bez żadnych dodatkowych funkcji
                    return Microsoft.Identity.Client.PublicClientApplicationBuilder
                        .Create("placeholder-client-id")
                        .WithAuthority(new Uri("https://login.microsoftonline.com/common"))
                        .WithRedirectUri("http://localhost")
                        .Build();
                }
            });
            
            services.AddSingleton<IMsalAuthService, MsalAuthService>();
            services.AddScoped<ConditionalAccessAnalyzer>();
            services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
            services.AddScoped<IUserSynchronizationService, UserSynchronizationService>();
            services.AddSingleton<IManualTestingService, ManualTestingService>();

                        services.AddTransient<TokenAuthorizationHandler>();
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

            // HttpClient dla TeamsManager API (diagnostyka i monitoring)
            services.AddHttpClient<ITeamsManagerApiService, TeamsManagerApiService>(client =>
            {
                client.BaseAddress = new Uri("https://localhost:7037/");
                client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager-UI/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options =>
            {
                // Retry Policy dla API
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
                
                // Circuit Breaker
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
            });

            // HttpClient dla TeamsManager API (główne operacje CRUD)
            services.AddHttpClient<TeamsManagerApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://localhost:7037/api/");
                client.DefaultRequestHeaders.Add("User-Agent", "TeamsManager-UI/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options =>
            {
                // Retry Policy dla API
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
                
                // Circuit Breaker
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
            });

            // Default HttpClient bez specjalnej konfiguracji
            services.AddHttpClient();

            // W docelowej architekturze z API, klient WPF raczej nie powinien mieć
            // bezpośredniego dostępu do DbContext. Komunikacja z danymi powinna
            // odbywać się przez TeamsManager.Api.
            // Tę sekcję możesz zakomentować lub usunąć, jeśli UI będzie 
            // komunikować się wyłącznie z API.
            // Jeśli jednak chcesz mieć DbContext dostępny w UI (np. do testów,
            // lub jeśli część logiki ma być lokalna):
            
            // BEZPIECZNA KONFIGURACJA BAZY DANYCH DLA PRODUCTION
            var connectionString = GetDatabaseConnectionString(configuration);
            
            services.AddDbContext<TeamsManagerDbContext>(options =>
                options.UseSqlite(connectionString));


            // np. services.AddTransient<MainViewModel>();
            //      services.AddTransient<LoginViewModel>();

            services.AddTransient<ManualTestingWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<ConfigurationSetupWindow>();

            services.AddSingleton<ViewModels.Shell.MainShellViewModel>();
            services.AddTransient<Views.Shell.MainShellWindow>();
            services.AddTransient<ConfigurationSetupViewModel>();

            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DashboardView>();
            services.AddSingleton<ITeamService, SimpleDashboardTeamService>();
            services.AddScoped<IOperationHistoryService, TeamsManager.Core.Services.OperationHistoryService>();
            services.AddScoped<IOperationHistoryRepository, TeamsManager.Data.Repositories.OperationHistoryRepository>();
            services.AddScoped<ICurrentUserService, TeamsManager.Core.Services.UserContext.CurrentUserService>();
            services.AddScoped<IAdminNotificationService, TeamsManager.Core.Services.StubAdminNotificationService>();
            services.AddScoped<IUnitOfWork, TeamsManager.Data.UnitOfWork.EfUnitOfWork>();
            services.AddScoped<IGenericRepository<UserSchoolType>, TeamsManager.Data.Repositories.GenericRepository<UserSchoolType>>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<SeedDataService>();

            services.AddScoped<TeamsManager.Core.Services.ApplicationSettingService>();
            services.AddTransient<ApplicationSettingsViewModel>();
            services.AddTransient<ApplicationSettingsView>();

            services.AddScoped<ISchoolTypeService, TeamsManager.Core.Services.SchoolTypeService>();
            services.AddScoped<INotificationService, TeamsManager.Core.Services.StubNotificationService>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.SchoolType>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.SchoolType>>();
            services.AddScoped<IUserRepository, TeamsManager.Data.Repositories.UserRepository>();

            services.AddTransient<SchoolTypeUIService>();
            services.AddTransient<DepartmentCodeMigrationService>();
            services.AddTransient<ViewModels.SchoolTypes.SchoolTypesListViewModel>();
            services.AddTransient<Views.SchoolTypes.SchoolTypesListView>();

            services.AddScoped<ISchoolYearService, TeamsManager.Core.Services.SchoolYearService>();
            services.AddScoped<ISchoolYearRepository, TeamsManager.Data.Repositories.SchoolYearRepository>();
            services.AddScoped<ITeamRepository, TeamsManager.Data.Repositories.TeamRepository>();
            services.AddTransient<SchoolYearUIService>();
            services.AddTransient<ViewModels.SchoolYears.SchoolYearListViewModel>();
            services.AddTransient<Views.SchoolYears.SchoolYearListView>();

            services.AddScoped<ISubjectService, TeamsManager.Core.Services.SubjectService>();
            services.AddScoped<ISubjectRepository, TeamsManager.Data.Repositories.SubjectRepository>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.Subject>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.Subject>>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.UserSubject>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.UserSubject>>();

            services.AddTransient<ViewModels.Subjects.SubjectsViewModel>();
            services.AddTransient<ViewModels.Subjects.SubjectEditViewModel>();
            services.AddTransient<ViewModels.Subjects.SubjectImportViewModel>();

            services.AddTransient<Views.Subjects.SubjectsView>();
            services.AddTransient<Views.Subjects.SubjectEditDialog>();
            services.AddTransient<Views.Subjects.SubjectImportDialog>();
            services.AddTransient<Views.Subjects.SubjectTeachersDialog>();
            
            services.AddTransient<Views.Common.ConfirmationDialog>();
            services.AddSingleton<Services.Abstractions.IUIDialogService, Services.UIDialogService>();


            // Core serwisy dla Departments (już zarejestrowane powyżej w innych sekcjach)
            services.AddScoped<IDepartmentService, TeamsManager.Core.Services.DepartmentService>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.Department>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.Department>>();


            // Core serwisy dla OrganizationalUnits
            services.AddScoped<IOrganizationalUnitService, TeamsManager.Core.Services.OrganizationalUnitService>();
            services.AddScoped<IGenericRepository<TeamsManager.Core.Models.OrganizationalUnit>, TeamsManager.Data.Repositories.GenericRepository<TeamsManager.Core.Models.OrganizationalUnit>>();
            
            // ViewModels dla Organizational Units
            services.AddTransient<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitEditViewModel>();
            services.AddTransient<TeamsManager.UI.ViewModels.OrganizationalUnits.OrganizationalUnitsManagementViewModel>();


            services.AddTransient<Views.OrganizationalUnits.OrganizationalUnitsManagementView>();
            services.AddTransient<Views.OrganizationalUnits.OrganizationalUnitEditDialog>();


            services.AddTransient<ViewModels.Departments.DepartmentsManagementViewModel>();
            services.AddTransient<ViewModels.Departments.DepartmentEditViewModel>();


            services.AddTransient<ViewModels.Dialogs.UniversalDialogViewModel>();

            services.AddTransient<Views.Departments.DepartmentsManagementView>();


            // ViewModele dla Operation History
            services.AddTransient<ViewModels.Operations.OperationHistoryViewModel>();
            services.AddTransient<ViewModels.Operations.OperationHistoryItemViewModel>();

            // Widoki Operation History
            services.AddTransient<Views.Operations.OperationHistoryView>();

            // Konwertery dla Operation History (singleton dla wydajności)
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
            // Core serwisy dla Users już zarejestrowane powyżej (IUserService, IDepartmentService)
            
            // ViewModele dla User List
            services.AddScoped<ViewModels.Users.UserListViewModel>(); // Scoped - zachowaj między nawigacją
            services.AddTransient<ViewModels.Users.UserListItemViewModel>();

            // Widoki User List
            services.AddTransient<Views.Users.UserListView>();

            // UserControls
            services.AddTransient<UserControls.BulkOperationsToolbar>();

            // Konwertery dla User List (singleton dla wydajności)
            services.AddSingleton<Converters.IntToVisibilityConverter>();
            services.AddSingleton<Converters.StringToBrushConverter>();
            // --- KONIEC: REJESTRACJA USER LIST (ETAP 3.1) ---

            // --- POCZĄTEK: REJESTRACJA USER DETAIL FORM (ETAP 3.2) ---
            // ViewModele dla User Detail Form
            services.AddTransient<ViewModels.Users.UserDetailViewModel>();

            // Widoki User Detail Form
            services.AddTransient<Views.Users.UserDetailWindow>();

            // Konwertery specyficzne dla User Detail Form już zarejestrowane powyżej
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
            // Core serwisy dla Import (już zarejestrowane w API)
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
            
            // Authentication Services
            
            // IConfidentialClientApplication dla TokenManager - RZECZYWISTA KONFIGURACJA z Azure AD V2.0
            services.AddScoped<Microsoft.Identity.Client.IConfidentialClientApplication>(provider =>
            {
                try
                {
                    var logger = provider.GetRequiredService<ILogger<App>>();
                    logger.LogInformation("Tworzenie IConfidentialClientApplication z rzeczywistą konfiguracją Azure AD...");
                    
                    // Ładowanie konfiguracji Azure AD z systemu V2.0
                    var configManager = provider.GetService<IConfigurationManagerV2>();
                    if (configManager != null)
                    {
                        try
                        {
                            var azureAdConfig = configManager.GetConfigurationAsync<AzureAdConfiguration>("azure-ad").GetAwaiter().GetResult();
                            
                            if (azureAdConfig != null && 
                                !string.IsNullOrWhiteSpace(azureAdConfig.Api.ClientId) && 
                                !string.IsNullOrWhiteSpace(azureAdConfig.Api.ClientSecret) &&
                                !string.IsNullOrWhiteSpace(azureAdConfig.TenantId))
                            {
                                var authority = $"https://login.microsoftonline.com/{azureAdConfig.TenantId}";
                                
                                logger.LogInformation("Konfiguracja IConfidentialClientApplication: ClientId='{ApiClientId}', Authority='{Authority}', ClientSecret is set: {IsSecretSet}",
                                    azureAdConfig.Api.ClientId,
                                    authority,
                                    !string.IsNullOrWhiteSpace(azureAdConfig.Api.ClientSecret));
                                
                                return Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
                                    .Create(azureAdConfig.Api.ClientId)
                                    .WithClientSecret(azureAdConfig.Api.ClientSecret)
                                    .WithAuthority(new Uri(authority))
                                    .Build();
                            }
                            else
                            {
                                logger.LogWarning("Konfiguracja Azure AD API niekompletna - brak ClientId, ClientSecret lub TenantId");
                            }
                        }
                        catch (Exception configEx)
                        {
                            logger.LogWarning(configEx, "Błąd podczas ładowania konfiguracji Azure AD dla IConfidentialClientApplication");
                        }
                    }
                    else
                    {
                        logger.LogWarning("ConfigurationManagerV2 niedostępny");
                    }
                    
                    // Fallback do placeholder (dla przypadków gdy konfiguracja nie jest dostępna)
                    logger.LogWarning("Używam placeholder IConfidentialClientApplication - OBO może nie działać");
                    return Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
                        .Create("placeholder-api-client-id")
                        .WithAuthority(new Uri("https://login.microsoftonline.com/common"))
                        .WithClientSecret("placeholder-secret")
                        .Build();
                }
                catch (Exception ex)
                {
                    // Ostatni fallback
                    Console.WriteLine($"[IConfidentialClientApplication] Krytyczny błąd: {ex.Message}");
                    
                    return Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
                        .Create("placeholder-api-client-id")
                        .WithAuthority(new Uri("https://login.microsoftonline.com/common"))
                        .WithClientSecret("placeholder-secret")
                        .Build();
                }
            });
            
            // TokenManager - używa GraphApiConfiguration dla scope'ów
            services.AddScoped<TeamsManager.Core.Abstractions.Services.Auth.ITokenManager>(provider =>
            {
                var confidentialClientApp = provider.GetRequiredService<IConfidentialClientApplication>();
                var memoryCache = provider.GetRequiredService<IMemoryCache>();
                var logger = provider.GetRequiredService<ILogger<TeamsManager.Core.Services.Auth.TokenManager>>();
                var configuration = provider.GetRequiredService<IConfiguration>();
                var graphConfig = provider.GetRequiredService<GraphApiConfiguration>();
                
                return new TeamsManager.Core.Services.Auth.TokenManager(
                    confidentialClientApp, 
                    memoryCache, 
                    logger, 
                    configuration, 
                    graphConfig);
            });
            
            // Konfiguracja Graph API - Singleton dla wydajności
            services.AddSingleton<GraphApiConfiguration>();
            
            services.AddGraphServices(includeAdminNotificationService: true); // Rejestruje wszystkie Graph API services
            
            services.AddScoped<IHealthMonitoringOrchestrator, TeamsManager.Application.Services.HealthMonitoringOrchestrator>();
            services.AddScoped<TeamsManager.Core.Abstractions.Services.Cache.ICacheInvalidationService, TeamsManager.Core.Services.Cache.CacheInvalidationService>();
            
            // Serwisy monitoringu dla UI
            services.AddSingleton<ISignalRService, SignalRService>();
            services.AddScoped<ITeamsManagerApiService, TeamsManagerApiService>();
            services.AddScoped<IMonitoringDataService, MonitoringDataService>();
            services.AddSingleton<IMonitoringPerformanceOptimizer, MonitoringPerformanceOptimizer>();
            
            // Nowy serwis monitorowania specjalnie dla TeamsManager
            // services.AddScoped<ITeamsManagerMonitoringService, TeamsManagerMonitoringService>();
            
            // ViewModele dla monitoringu
            services.AddTransient<ViewModels.Monitoring.MonitoringDashboardViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.TeamsManagerHealthWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.TeamsManagerMetricsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.ActiveOperationsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.AlertsWidgetViewModel>();
            services.AddTransient<ViewModels.Monitoring.Widgets.AdvancedPerformanceChartWidgetViewModel>();
            
            // Widoki monitoringu
            services.AddTransient<Views.Monitoring.MonitoringDashboardView>();
            services.AddTransient<Views.Monitoring.Widgets.TeamsManagerHealthWidget>();
            services.AddTransient<Views.Monitoring.Widgets.TeamsManagerMetricsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.ActiveOperationsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.AlertsWidget>();
            services.AddTransient<Views.Monitoring.Widgets.AdvancedPerformanceChartWidget>();
            
            // Konwertery dla monitoringu (singleton dla wydajności)
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
            
            // Modern Circuit Breaker
            services.AddSingleton<ModernCircuitBreaker>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<ModernCircuitBreaker>>();
                return new ModernCircuitBreaker(
                    failureThreshold: 5,
                    openDuration: TimeSpan.FromMinutes(1),
                    logger: logger
                );
            });
            
            // Graph Synchronizers
            services.AddScoped<IGraphSynchronizer<Team, GraphTeam>, TeamSynchronizer>();
            services.AddScoped<IGraphSynchronizer<User, GraphUser>, UserSynchronizer>();
            services.AddScoped<IGraphSynchronizer<Channel, GraphChannel>, ChannelSynchronizer>();
            
            // Application Services (Orchestrators)
            services.AddScoped<ITeamLifecycleOrchestrator, TeamsManager.Application.Services.TeamLifecycleOrchestrator>();
            services.AddScoped<IBulkUserManagementOrchestrator, TeamsManager.Application.Services.BulkUserManagementOrchestrator>();
            services.AddScoped<IReportingOrchestrator, TeamsManager.Application.Services.ReportingOrchestrator>();
            services.AddScoped<ISchoolYearProcessOrchestrator, TeamsManager.Application.Services.SchoolYearProcessOrchestrator>();
            
            // Dodatkowe repozytoria potrzebne przez orkiestratory
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
                System.Diagnostics.Debug.WriteLine("=== APP: Inicjalizacja bazy danych ===");
                await context.Database.EnsureCreatedAsync();
                System.Diagnostics.Debug.WriteLine("=== APP: Baza danych zainicjalizowana ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== APP: Błąd inicjalizacji bazy danych: {ex.Message} ===");
            }
        }

        private async Task RunConfigurationTestAsync()
        {
            try
            {
                Console.WriteLine("🔧 TEST KONFIGURACJI V2.0");
                
                var configManager = ServiceProvider.GetRequiredService<IConfigurationManagerV2>();
                
                // Test 1: Application Configuration
                Console.WriteLine("\n1. Testowanie Application Configuration...");
                var appConfig = await configManager.LoadApplicationConfigurationAsync();
                if (appConfig != null)
                {
                    Console.WriteLine($"✅ Application Name: {appConfig.Application.Name}");
                    Console.WriteLine($"✅ Version: {appConfig.Application.Version}");
                    Console.WriteLine($"✅ Environment: {appConfig.Environment}");
                    Console.WriteLine($"✅ API Base URL: {appConfig.Api.BaseUrl}");
                }
                else
                {
                    Console.WriteLine("❌ Application Configuration NIE WCZYTANA");
                }

                // Test 2: Azure AD Configuration (ZASZYFROWANA)
                Console.WriteLine("\n2. Testowanie Azure AD Configuration (zaszyfrowana)...");
                var azureConfig = await configManager.LoadAzureAdConfigurationAsync();
                if (azureConfig != null)
                {
                    Console.WriteLine($"✅ Tenant ID: {azureConfig.TenantId}");
                    Console.WriteLine($"✅ UI Client ID: {azureConfig.Ui.ClientId}");
                    Console.WriteLine($"✅ API Client ID: {azureConfig.Api.ClientId}");
                    Console.WriteLine($"✅ Client Secret: {(string.IsNullOrEmpty(azureConfig.Api.ClientSecret) ? "BRAK" : "USTAWIONY")}");
                    Console.WriteLine($"✅ Audience: {azureConfig.Api.Audience}");
                }
                else
                {
                    Console.WriteLine("❌ Azure AD Configuration NIE WCZYTANA");
                }

                Console.WriteLine("\n✅ TEST ZAKOŃCZONY");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ BŁĄD TESTU: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            ILogger<App>? logger = null;
            
            try
            {
                // Najpierw skonfiguruj logger
                var logsDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TeamsManager", "logs");
                
                using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                {
                    builder
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug)
                        .AddProvider(new Services.Configuration.FileLoggerProvider(logsDirectory));
                });
                
                logger = loggerFactory.CreateLogger<App>();
                
                logger.LogInformation("🚀 TEAMSMANAGER - URUCHOMIENIE APLIKACJI");
                logger.LogInformation($"🚀 Argumenty: {string.Join(", ", e.Args)}");
                logger.LogInformation($"🚀 Użytkownik: {Environment.UserName}");
                logger.LogInformation($"🚀 Maszyna: {Environment.MachineName}");
                logger.LogInformation($"🚀 Wersja .NET: {Environment.Version}");
                
                Console.WriteLine("🚀 APP: OnStartup rozpoczęty");
                Console.WriteLine($"🚀 APP: Argumenty: {string.Join(", ", e.Args)}");
                
                // TEST SZYFROWANIA - dodany dla debugowania problemu
                Console.WriteLine("🧪 APP: Uruchamiam test szyfrowania...");
                logger.LogInformation("🧪 Uruchamiam test szyfrowania...");
                
                ConfigurationTool.TestEncryption();
                
                Console.WriteLine("🧪 APP: Test szyfrowania zakończony");
                logger.LogInformation("🧪 Test szyfrowania zakończony");
                
                // TEST KONFIGURACJI - sprawdź argument --test-config
                if (e.Args.Length > 0 && e.Args[0] == "--test-config")
                {
                    Console.WriteLine("🔧 APP: Wykryto argument --test-config, uruchamiam test");
                    logger.LogInformation("🔧 Wykryto argument --test-config, uruchamiam test");
                    
                    base.OnStartup(e);
                    await RunConfigurationTestAsync();
                    
                    Console.WriteLine("✅ APP: Test zakończony, zamykam aplikację");
                    logger.LogInformation("✅ Test zakończony, zamykam aplikację");
                    
                    Shutdown();
                    return;
                }
                
                base.OnStartup(e);
                
                System.Diagnostics.Debug.WriteLine("=== APP: Uruchamianie aplikacji ===");
                Console.WriteLine("=== APP: Uruchamianie aplikacji ===");
                logger.LogInformation("=== URUCHAMIANIE GŁÓWNEJ APLIKACJI ===");
                
                // Sprawdź czy konfiguracja jest kompletna
                logger.LogInformation("🔍 Sprawdzam kompletność konfiguracji...");
                
                var isConfigComplete = await ConfigurationSetupWindow.CheckConfigurationAsync(ServiceProvider);
                
                logger.LogInformation($"🔍 Wynik sprawdzania konfiguracji: {(isConfigComplete ? "KOMPLETNA" : "NIEKOMPLETNA")}");
                
                if (!isConfigComplete)
                {
                    System.Diagnostics.Debug.WriteLine("=== APP: Konfiguracja niekompletna - pokazuję okno konfiguracji ===");
                    logger.LogWarning("❌ Konfiguracja niekompletna - pokazuję okno konfiguracji");
                    
                    // Pokaż okno konfiguracji
                    var configResult = ConfigurationSetupWindow.ShowConfigurationDialog(null, ServiceProvider);
                    
                    logger.LogInformation($"🔧 Wynik okna konfiguracji: {configResult}");
                    
                    if (configResult != true)
                    {
                        // Użytkownik anulował konfigurację - zamknij aplikację
                        logger.LogWarning("❌ Użytkownik anulował konfigurację - zamykam aplikację");
                        
                        MessageBox.Show("Aplikacja zostanie zamknięta, ponieważ konfiguracja jest wymagana do prawidłowego działania.",
                                      "Konfiguracja anulowana", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Bezpieczne zamknięcie aplikacji
                        try
                        {
                            this.Shutdown();
                        }
                        catch
                        {
                            Environment.Exit(0);
                        }
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine("=== APP: Konfiguracja zakończona pomyślnie ===");
                    logger.LogInformation("✅ Konfiguracja zakończona pomyślnie");
                    
                    // Po zapisaniu konfiguracji sprawdź ponownie czy jest kompletna
                    logger.LogInformation("🔍 Sprawdzam ponownie kompletność konfiguracji po zapisie...");
                    
                    isConfigComplete = await ConfigurationSetupWindow.CheckConfigurationAsync(ServiceProvider);
                    
                    logger.LogInformation($"🔍 Wynik ponownego sprawdzania: {(isConfigComplete ? "KOMPLETNA" : "NIEKOMPLETNA")}");
                    
                    if (!isConfigComplete)
                    {
                        logger.LogError("❌ Konfiguracja nadal niekompletna po zapisie - zamykam aplikację");
                        
                        MessageBox.Show("Konfiguracja nadal jest niekompletna. Aplikacja zostanie zamknięta.",
                                      "Błąd konfiguracji", MessageBoxButton.OK, MessageBoxImage.Error);
                        
                        try
                        {
                            this.Shutdown();
                        }
                        catch
                        {
                            Environment.Exit(0);
                        }
                        return;
                    }
                }
                
                logger.LogInformation("✅ Konfiguracja jest kompletna - kontynuuję uruchomienie");
                
                // Przywróć normalny ShutdownMode i uruchom główne okno
                try
                {
                    this.ShutdownMode = ShutdownMode.OnLastWindowClose;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[APP] Nie można ustawić ShutdownMode: {ex.Message}");
                    logger.LogWarning(ex, "Nie można ustawić ShutdownMode");
                }
                
                System.Diagnostics.Debug.WriteLine("=== APP: Tworzenie MainShellWindow przez DI ===");
                Console.WriteLine("=== APP: Tworzenie MainShellWindow przez DI ===");
                logger.LogInformation("🏠 Tworzenie głównego okna aplikacji...");
                
                // Tworzenie MainShellWindow przez DI
                var mainShellWindow = ServiceProvider.GetRequiredService<Views.Shell.MainShellWindow>();
                
                System.Diagnostics.Debug.WriteLine($"=== APP: MainShellWindow utworzone: {mainShellWindow != null} ===");
                Console.WriteLine($"=== APP: MainShellWindow utworzone: {mainShellWindow != null} ===");
                logger.LogInformation($"🏠 MainShellWindow utworzone: {mainShellWindow != null}");
                
                mainShellWindow.Show();
                
                System.Diagnostics.Debug.WriteLine("=== APP: MainShellWindow.Show() wywołane ===");
                Console.WriteLine("=== APP: MainShellWindow.Show() wywołane ===");
                logger.LogInformation("🏠 MainShellWindow.Show() wywołane - aplikacja uruchomiona");
                
                // Weryfikacja DI (debug)
                System.Diagnostics.Debug.WriteLine($"[DI Test] MainShellWindow created via DI: {mainShellWindow != null}");

                // Sprawdzenie serwisów dla debugowania
                var currentUserService = ServiceProvider.GetRequiredService<ICurrentUserService>();
                System.Diagnostics.Debug.WriteLine($"[UI DI Test] Current User UPN: {currentUserService.GetCurrentUserUpn()}");

                // Weryfikacja serwisów z Etapów 2-3
                var httpClientFactory = ServiceProvider.GetService<IHttpClientFactory>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IHttpClientFactory: {httpClientFactory != null}");

                var configManager = ServiceProvider.GetService<IConfigurationManagerV2>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IConfigurationManagerV2: {configManager != null}");

                var msalService = ServiceProvider.GetService<IMsalAuthService>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IMsalAuthService: {msalService != null}");

                var graphService = ServiceProvider.GetService<IGraphUserProfileService>();
                System.Diagnostics.Debug.WriteLine($"[DI Test] IGraphUserProfileService: {graphService != null}");

                // Test konfiguracji Azure AD V2.0
                if (configManager != null)
                {
                    try
                    {
                        var azureAdConfig = await configManager.LoadAzureAdConfigurationAsync();
                        if (azureAdConfig != null && azureAdConfig.IsValid())
                        {
                            System.Diagnostics.Debug.WriteLine($"[Config Test] Azure AD configuration loaded successfully");
                            System.Diagnostics.Debug.WriteLine($"[Config Test] UI ClientId: {azureAdConfig.Ui.ClientId}, API ClientId: {azureAdConfig.Api.ClientId}");
                            logger.LogInformation("✅ Konfiguracja Azure AD załadowana pomyślnie");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Config Test] Azure AD configuration is invalid or incomplete");
                            logger.LogWarning("❌ Konfiguracja Azure AD jest nieprawidłowa lub niekompletna");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Config Test] Failed to load Azure AD configuration: {ex.Message}");
                        logger.LogError(ex, "❌ Błąd podczas ładowania konfiguracji Azure AD");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Config Test] ConfigurationManagerV2 service not available");
                    logger.LogWarning("❌ Serwis ConfigurationManagerV2 niedostępny");
                }

                // Sprawdzenie DbContext i seedowanie danych
                try
                {
                    var dbContext = ServiceProvider.GetRequiredService<TeamsManagerDbContext>();
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] DbContext instance created: {dbContext != null}");
                    logger.LogInformation($"📊 DbContext utworzony: {dbContext != null}");
                    
                    // Inicjalizacja danych początkowych (Seed Data)
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            await InitializeDatabaseAsync(dbContext);
                            
                            // Inicjalizacja danych początkowych systemu
                            using var scope = ServiceProvider.CreateScope();
                            var seedDataService = scope.ServiceProvider.GetRequiredService<SeedDataService>();
                            await seedDataService.InitializeDefaultDataAsync();
                            
                            System.Diagnostics.Debug.WriteLine("[SeedData] Dane początkowe systemu zostały zainicjalizowane");
                            logger.LogInformation("📊 Dane początkowe systemu zostały zainicjalizowane");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SeedData] Błąd podczas inicjalizacji danych początkowych: {ex.Message}");
                            logger.LogError(ex, "❌ Błąd podczas inicjalizacji danych początkowych");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI DI Test] Error creating DbContext: {ex.Message}");
                    logger.LogError(ex, "❌ Błąd podczas tworzenia DbContext");
                }
                
                logger.LogInformation("🎉 APLIKACJA URUCHOMIONA POMYŚLNIE");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ KRYTYCZNY BŁĄD podczas uruchamiania aplikacji");
                
                MessageBox.Show(
                    $"Błąd podczas tworzenia głównego okna:\n\n{ex.Message}\n\nSprawdź konfigurację serwisów.",
                    "Błąd krytyczny",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Bezpieczne zamknięcie aplikacji
                try
                {
                    this.Shutdown();
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
        }
    }
}

