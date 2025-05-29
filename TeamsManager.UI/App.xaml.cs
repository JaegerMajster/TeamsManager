using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO; // Dla Path.Combine
using System.Windows;
using TeamsManager.Core.Abstractions;         // Dla ICurrentUserService
using TeamsManager.Core.Services.UserContext; // Dla CurrentUserService
using TeamsManager.Data;                      // Dla TeamsManagerDbContext
using Microsoft.EntityFrameworkCore;          // Dla UseSqlite i DbContextOptionsBuilder

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
            // --- Konfiguracja ICurrentUserService ---
            // Rejestrujemy jako Singleton, aby ta sama instancja była dostępna 
            // w całej aplikacji UI. Pozwoli to na ustawienie użytkownika 
            // po zalogowaniu i odczytanie go w dowolnym miejscu.
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

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

            // --- Rejestracja Głównego Okna (Przykład) ---
            // Jeśli używasz DI do tworzenia głównego okna.
            // services.AddTransient<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Po skonfigurowaniu DI, możesz utworzyć i pokazać główne okno aplikacji.
            // Przykład (odkomentuj i dostosuj, gdy będziesz miał MainWindow i MainViewModel):

            /*
            var mainWindow = new MainWindow(); // Lub pobierz z ServiceProvider, jeśli zarejestrowane
            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>(); // Przykład
            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();
            */

            // Na razie, dla testu, możemy po prostu uruchomić aplikację bez głównego okna,
            // aby sprawdzić, czy DI się poprawnie konfiguruje.
            // Lub, jeśli masz już jakieś proste okno startowe, możesz je tutaj uruchomić.

            // Przykład: Sprawdzenie, czy serwis ICurrentUserService jest dostępny
            var currentUserService = ServiceProvider.GetRequiredService<ICurrentUserService>();
            System.Diagnostics.Debug.WriteLine($"[UI DI Test] Current User UPN: {currentUserService.GetCurrentUserUpn()}");

            // Przykład: Sprawdzenie, czy DbContext jest dostępny (jeśli go rejestrowałeś)
            try
            {
                var dbContext = ServiceProvider.GetRequiredService<TeamsManagerDbContext>();
                // Możesz spróbować wykonać prostą operację, np. dbContext.Database.EnsureCreated();
                // lub dbContext.Database.CanConnect();
                // Pamiętaj, że jeśli plik teamsmanager_ui.db nie istnieje, 
                // EF Core spróbuje go utworzyć przy pierwszym użyciu, jeśli migracje są skonfigurowane
                // lub jeśli użyjesz EnsureCreated(). Na razie można to pominąć.
                System.Diagnostics.Debug.WriteLine($"[UI DI Test] DbContext instance created: {dbContext != null}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UI DI Test] Error creating DbContext: {ex.Message}");
                // Możesz tutaj wyświetlić MessageBox z błędem, jeśli chcesz.
                // MessageBox.Show($"Error initializing DbContext: {ex.Message}", "DI Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}