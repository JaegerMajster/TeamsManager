using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Data;
using TeamsManager.Tests.Infrastructure;
using TeamsManager.Tests.Infrastructure.Services;
using TeamsManager.Core.Abstractions;
using System;
using System.Threading.Tasks;

namespace TeamsManager.Tests.Integration
{
    /// <summary>
    /// Bazowa klasa dla wszystkich testów integracyjnych
    /// Zapewnia konfigurację DbContext, serwisów i czyszczenie danych
    /// </summary>
    public abstract class IntegrationTestBase : IDisposable
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IServiceScope ServiceScope { get; }
        protected TestDbContext Context { get; }
        protected TestCurrentUserService CurrentUserService { get; }

        protected IntegrationTestBase()
        {
            // Konfiguracja kontenera DI dla testów
            var services = new ServiceCollection();

            // Konfiguracja DbContext - InMemory dla szybkości
            services.AddDbContext<TeamsManagerDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
                options.EnableSensitiveDataLogging(); // Pomocne przy debugowaniu testów
            });

            // Rejestracja TestDbContext z tymi samymi opcjami
            services.AddScoped<TestDbContext>(provider =>
            {
                var options = provider.GetRequiredService<DbContextOptions<TeamsManagerDbContext>>();
                return new TestDbContext(options);
            });

            // Rejestracja serwisów testowych
            services.AddSingleton<TestCurrentUserService>();
            services.AddSingleton<ICurrentUserService>(provider => provider.GetRequiredService<TestCurrentUserService>());

            // Tu możesz dodać więcej serwisów używanych w testach
            ConfigureServices(services);

            ServiceProvider = services.BuildServiceProvider();
            ServiceScope = ServiceProvider.CreateScope();

            // Pobierz instancje z kontenera
            Context = ServiceScope.ServiceProvider.GetRequiredService<TestDbContext>();
            CurrentUserService = ServiceScope.ServiceProvider.GetRequiredService<TestCurrentUserService>();

            // Upewnij się że baza jest utworzona
            Context.Database.EnsureCreated();
        }

        /// <summary>
        /// Metoda do nadpisania w klasach pochodnych
        /// Pozwala dodać dodatkowe serwisy specyficzne dla danego zestawu testów
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Domyślnie pusta - do nadpisania w klasach pochodnych
        }

        /// <summary>
        /// Zapisuje zmiany w kontekście
        /// </summary>
        protected async Task SaveChangesAsync()
        {
            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Zapisuje zmiany bez automatycznego ustawiania pól audytowych
        /// </summary>
        protected async Task SaveChangesWithoutAuditAsync()
        {
            Context.DisableAutoAudit();
            try
            {
                await Context.SaveChangesAsync();
            }
            finally
            {
                Context.EnableAutoAudit();
            }
        }

        /// <summary>
        /// Czyści wszystkie dane z bazy testowej
        /// </summary>
        protected async Task CleanDatabaseAsync()
        {
            // Kolejność jest ważna ze względu na relacje!
            Context.OperationHistories.RemoveRange(Context.OperationHistories);
            Context.TeamMembers.RemoveRange(Context.TeamMembers);
            Context.Channels.RemoveRange(Context.Channels);
            Context.Teams.RemoveRange(Context.Teams);
            Context.UserSchoolTypes.RemoveRange(Context.UserSchoolTypes);
            Context.UserSubjects.RemoveRange(Context.UserSubjects);
            Context.Users.RemoveRange(Context.Users);
            Context.Departments.RemoveRange(Context.Departments);
            Context.SchoolTypes.RemoveRange(Context.SchoolTypes);
            Context.SchoolYears.RemoveRange(Context.SchoolYears);
            Context.Subjects.RemoveRange(Context.Subjects);
            Context.TeamTemplates.RemoveRange(Context.TeamTemplates);
            Context.ApplicationSettings.RemoveRange(Context.ApplicationSettings);

            await SaveChangesAsync();
        }

        /// <summary>
        /// Ustawia konkretnego użytkownika dla testu
        /// </summary>
        protected void SetTestUser(string userName)
        {
            Context.SetTestUser(userName);
            CurrentUserService.SetCurrentUserUpn(userName);
        }

        /// <summary>
        /// Resetuje użytkownika do domyślnego
        /// </summary>
        protected void ResetTestUser()
        {
            Context.ResetTestUser();
            CurrentUserService.Reset();
        }

        public void Dispose()
        {
            ServiceScope?.Dispose();

            // ServiceProvider może być IDisposable, ale IServiceProvider nie gwarantuje tego
            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
        }
    }
}