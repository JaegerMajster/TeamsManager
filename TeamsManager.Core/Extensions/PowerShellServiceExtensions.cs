using Microsoft.Extensions.DependencyInjection;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Services.PowerShell;

namespace TeamsManager.Core.Extensions
{
    /// <summary>
    /// Rozszerzenia do rejestracji serwisów PowerShell w kontenerze DI
    /// </summary>
    public static class PowerShellServiceExtensions
    {
        /// <summary>
        /// Rejestruje wszystkie serwisy PowerShell w kontenerze DI
        /// </summary>
        /// <param name="services">Kolekcja serwisów</param>
        /// <returns>Kolekcja serwisów dla łańcuchowania</returns>
        public static IServiceCollection AddPowerShellServices(this IServiceCollection services)
        {
            // Core services - Scoped zamiast Singleton
            services.AddScoped<IPowerShellConnectionService, PowerShellConnectionService>();
            services.AddScoped<IPowerShellCacheService, PowerShellCacheService>();
            services.AddScoped<IPowerShellUserResolverService, PowerShellUserResolverService>();

            // Domain services - pozostają Scoped
            services.AddScoped<IPowerShellTeamManagementService, PowerShellTeamManagementService>();
            services.AddScoped<IPowerShellUserManagementService, PowerShellUserManagementService>();
            services.AddScoped<IPowerShellBulkOperationsService, PowerShellBulkOperationsService>();

            // Facade - pozostaje Scoped
            services.AddScoped<IPowerShellService, PowerShellService>();

            return services;
        }
    }
}