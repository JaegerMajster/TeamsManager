using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Data.Repositories;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Tests.Infrastructure;
using TeamsManager.Tests.Integration;
using System;
using System.Threading.Tasks;

namespace TeamsManager.Tests.Repositories
{
    /// <summary>
    /// Bazowa klasa dla testów repozytoriów
    /// Rozszerza IntegrationTestBase o funkcjonalności specyficzne dla repozytoriów
    /// </summary>
    public abstract class RepositoryTestBase : IntegrationTestBase // Bez "Integration." w nazwie
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Rejestracja wszystkich repozytoriów używanych w testach
            services.AddScoped<IApplicationSettingRepository, ApplicationSettingRepository>();
            services.AddScoped<IOperationHistoryRepository, OperationHistoryRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITeamRepository, TeamRepository>();
            services.AddScoped<ITeamTemplateRepository, TeamTemplateRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<ISchoolYearRepository, SchoolYearRepository>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Tutaj możesz dodać więcej repozytoriów gdy będą potrzebne
        }

        /// <summary>
        /// Pobiera instancję repozytorium z kontenera DI
        /// </summary>
        protected T GetRepository<T>() where T : class
        {
            return ServiceScope.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Ustawia konkretnego użytkownika dla pojedynczej operacji
        /// </summary>
        protected void WithUser(string userName)
        {
            SetTestUser(userName);
        }

        /// <summary>
        /// Wykonuje operację jako konkretny użytkownik i przywraca domyślnego
        /// </summary>
        protected async Task<T> ExecuteAsUserAsync<T>(string userName, Func<Task<T>> operation)
        {
            var previousUser = CurrentUserService.GetCurrentUserUpn();
            try
            {
                SetTestUser(userName);
                return await operation();
            }
            finally
            {
                SetTestUser(previousUser ?? "test_user_integration_base_default");
            }
        }
    }
}