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
using TeamsManager.UI.Views.Configuration;
using TeamsManager.UI.Views;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using TeamsManager.UI.Services.Http;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services;
using Polly;

namespace TeamsManager.UI
{
    public partial class App : Application
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
            // Rejestrujemy jako Singleton, aby ta sama instancja była dostępna 
            // w całej aplikacji UI. Pozwoli to na ustawienie użytkownika 
            // po zalogowaniu i odczytanie go w dowolnym miejscu.
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

            // --- Rejestracja serwisów konfiguracji ---
            services.AddSingleton<ConfigurationManager>();
            services.AddSingleton<ConfigurationValidator>();
            services.AddSingleton<EncryptionService>();

            // --- POCZĄTEK: MIGRACJA SERWISÓW DO DI (ETAP 3) ---
            
            // Rejestracja ConfigurationProvider
            services.AddSingleton<IMsalConfigurationProvider, MsalConfigurationProvider>();
            
            // Aktualizacja rejestracji MsalAuthService - teraz z dependencies
            services.AddSingleton<IMsalAuthService, MsalAuthService>();
            
            // GraphUserProfileService jest już zarejestrowany jako Scoped z Etapu 2
            services.AddScoped<IGraphUserProfileService, GraphUserProfileService>();
            
            // --- POCZĄTEK: REJESTRACJA SERWISÓW TESTOWYCH (ETAP 5) ---
            // ManualTestingService jako Singleton - zachowuje stan między oknami
            services.AddSingleton<IManualTestingService, ManualTestingService>();
            // --- KONIEC: REJESTRACJA SERWISÓW TESTOWYCH (ETAP 5) ---
            
            // --- KONIEC: MIGRACJA SERWISÓW DO DI (ETAP 3) ---

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
            // W docelowej architekturze z API, klient WPF raczej nie powinien mieć
            // bezpośredniego dostępu do DbContext. Komunikacja z danymi powinna
            // odbywać się przez TeamsManager.Api.
            // Tę sekcję możesz zakomentować lub usunąć, jeśli UI będzie 
            // komunikować się wyłącznie z API.
            // Jeśli jednak chcesz mieć DbContext dostępny w UI (np. do testów,
            // lub jeśli część logiki ma być lokalna):
            string uiDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "teamsmanager_ui.db");
            services.AddDbContext<TeamsManagerDbContext>(options =>
                options.UseSqlite($"Data Source={uiDbPath}"));

            // --- Rejestracja ViewModeli (Przykłady) ---
            // Tutaj w przyszłości będziesz rejestrować swoje ViewModele,
            // aby można je było wstrzykiwać do widoków lub pobierać z ServiceProvider.
            // np. services.AddTransient<MainViewModel>();
            //      services.AddTransient<LoginViewModel>();

            // --- POCZĄTEK: REJESTRACJA OKIEN (ETAP 4) ---
            // Rejestracja głównego okna
            services.AddTransient<MainWindow>();
            
            // Opcjonalnie: rejestracja innych okien
            services.AddTransient<DashboardWindow>();
            
            // ManualTestingWindow jako Transient - nowa instancja przy każdym otwarciu
            services.AddTransient<ManualTestingWindow>();
            // --- KONIEC: REJESTRACJA OKIEN (ETAP 4) ---
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Ustaw ShutdownMode na Manual żeby aplikacja nie zamykała się automatycznie
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Sprawdź konfigurację przy starcie
                var configManager = ServiceProvider.GetRequiredService<ConfigurationManager>();
                var validator = ServiceProvider.GetRequiredService<ConfigurationValidator>();

                var validationResult = await validator.ValidateFullConfigurationAsync();

                if (!validationResult.IsValid)
                {
                    // Pokaż okno wykrycia problemu z konfiguracją
                    var detectionWindow = new ConfigurationDetectionWindow();
                    detectionWindow.SetValidationResult(validationResult);
                    var result = detectionWindow.ShowDialog();
                    
                    if (result == true)
                    {
                        // Użytkownik chce rozpocząć konfigurację - pokaż pierwsze okno konfiguracji (API)
                        try
                        {
                            var apiConfigWindow = new ApiConfigurationWindow();
                            var configResult = apiConfigWindow.ShowDialog();
                            
                            if (configResult == true)
                            {
                                // Konfiguracja zakończona pomyślnie - restart aplikacji
                                MessageBox.Show(
                                    "Konfiguracja została zakończona pomyślnie!\n\nAplikacja zostanie teraz zrestartowana.",
                                    "Sukces",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                                    
                                var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                                if (!string.IsNullOrEmpty(currentExe))
                                {
                                    System.Diagnostics.Process.Start(currentExe);
                                }
                                Application.Current.Shutdown();
                                return;
                            }
                            else
                            {
                                // Użytkownik anulował konfigurację - zamknij aplikację
                                Application.Current.Shutdown();
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Błąd podczas otwierania okna konfiguracji API:\n\n{ex.Message}",
                                "Błąd",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            Application.Current.Shutdown();
                            return;
                        }
                    }
                    else
                    {
                        // Użytkownik anulował - zamknij aplikację
                        Application.Current.Shutdown();
                        return;
                    }
                }
                else
                {
                    // Konfiguracja jest poprawna - przywróć normalny ShutdownMode i uruchom główne okno
                    Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                    
                    try
                    {
                        // ZMIANA: Tworzenie MainWindow przez DI
                        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                        mainWindow.Show();
                        
                        // Weryfikacja DI (debug)
                        System.Diagnostics.Debug.WriteLine($"[DI Test] MainWindow created via DI: {mainWindow != null}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Błąd podczas tworzenia głównego okna:\n\n{ex.Message}\n\nSprawdź konfigurację serwisów.",
                            "Błąd krytyczny",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Application.Current.Shutdown();
                        return;
                    }

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Wystąpił błąd podczas uruchamiania aplikacji:\n\n{ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
            }
        }
    }
}