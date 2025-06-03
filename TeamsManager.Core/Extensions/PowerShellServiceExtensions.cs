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
            // Core services - Singleton bo zarządzają stanem (runspace, cache)
            services.AddSingleton<IPowerShellConnectionService, PowerShellConnectionService>();
            services.AddSingleton<IPowerShellCacheService, PowerShellCacheService>();

            // Domain services - Scoped dla izolacji między żądaniami
            services.AddScoped<IPowerShellTeamManagementService, PowerShellTeamManagementService>();
            services.AddScoped<IPowerShellUserManagementService, PowerShellUserManagementService>();
            services.AddScoped<IPowerShellBulkOperationsService, PowerShellBulkOperationsService>();

            // Facade - Scoped aby korzystać z scoped services
            services.AddScoped<IPowerShellService, PowerShellService>();

            return services;
        }
    }
}