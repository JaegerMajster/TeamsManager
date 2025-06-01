// Plik: TeamsManager.Tests/Integration/IntegrationTestBase.cs
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
    public abstract class IntegrationTestBase : IDisposable
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IServiceScope ServiceScope { get; }
        protected TestDbContext Context { get; }
        protected TestCurrentUserService CurrentUserService { get; }

        protected IntegrationTestBase()
        {
            var services = new ServiceCollection();

            // 1. Rejestracja TestCurrentUserService jako ICurrentUserService (Singleton dla spójności w teście)
            services.AddSingleton<TestCurrentUserService>();
            services.AddSingleton<ICurrentUserService>(provider => provider.GetRequiredService<TestCurrentUserService>());

            // 2. Rejestracja DbContextOptions<TeamsManagerDbContext>
            services.AddSingleton(provider => // Może być Scoped, jeśli każdy test/scope ma mieć inne opcje
            {
                return new DbContextOptionsBuilder<TeamsManagerDbContext>()
                    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                    .EnableSensitiveDataLogging()
                    .Options;
            });

            // 3. Rejestracja TestDbContext.
            // Kontener DI wstrzyknie DbContextOptions<TeamsManagerDbContext> i ICurrentUserService.
            services.AddScoped<TestDbContext>();

            // 4. Rejestracja TeamsManagerDbContext tak, aby wskazywał na TestDbContext.
            services.AddScoped<TeamsManagerDbContext>(provider => provider.GetRequiredService<TestDbContext>());

            ConfigureServices(services);

            ServiceProvider = services.BuildServiceProvider();
            ServiceScope = ServiceProvider.CreateScope();

            Context = ServiceScope.ServiceProvider.GetRequiredService<TestDbContext>();
            CurrentUserService = ServiceScope.ServiceProvider.GetRequiredService<TestCurrentUserService>();

            Context.Database.EnsureCreated();
            SetTestUser("test_user_integration_base_default"); // Ustawienie domyślnego użytkownika na początku
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Domyślnie pusta
        }

        protected async Task SaveChangesAsync()
        {
            await Context.SaveChangesAsync();
        }

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

        protected async Task CleanDatabaseAsync()
        {
            Context.OperationHistories.RemoveRange(Context.OperationHistories);
            Context.UserSubjects.RemoveRange(Context.UserSubjects);
            Context.UserSchoolTypes.RemoveRange(Context.UserSchoolTypes);
            Context.TeamMembers.RemoveRange(Context.TeamMembers);
            Context.Channels.RemoveRange(Context.Channels);
            Context.Teams.RemoveRange(Context.Teams);
            Context.Users.RemoveRange(Context.Users);
            Context.Departments.RemoveRange(Context.Departments);
            Context.Subjects.RemoveRange(Context.Subjects);
            Context.TeamTemplates.RemoveRange(Context.TeamTemplates);
            Context.SchoolTypes.RemoveRange(Context.SchoolTypes);
            Context.SchoolYears.RemoveRange(Context.SchoolYears);
            Context.ApplicationSettings.RemoveRange(Context.ApplicationSettings);
            await SaveChangesWithoutAuditAsync();
        }

        protected void SetTestUser(string userName)
        {
            CurrentUserService.SetCurrentUserUpn(userName);
        }

        protected void ResetTestUser()
        {
            CurrentUserService.Reset();
        }

        public void Dispose()
        {
            ServiceScope?.Dispose();
            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}