using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Services;
using TeamsManager.Core.Services.Graph;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Extensions
{
    /// <summary>
    /// Rozszerzenia do rejestracji serwisów Graph API w kontenerze DI
    /// </summary>
    public static class GraphServiceExtensions
    {
        /// <summary>
        /// Rejestruje wszystkie serwisy Graph API w kontenerze DI
        /// </summary>
        /// <param name="services">Kolekcja serwisów</param>
        /// <param name="includeAdminNotificationService">Czy zarejestrować GraphAdminNotificationService jako IAdminNotificationService</param>
        /// <returns>Kolekcja serwisów dla łańcuchowania</returns>
        public static IServiceCollection AddGraphServices(this IServiceCollection services, bool includeAdminNotificationService = false)
        {
            // Core dependencies - jeśli nie są już zarejestrowane
            // IModernHttpService i IConfidentialClientApplication powinny być już zarejestrowane w Program.cs/App.xaml.cs
            
            // Sprawdź czy IModernHttpService jest już zarejestrowany, jeśli nie - dodaj
            if (!IsServiceRegistered<IModernHttpService>(services))
            {
                services.AddScoped<IModernHttpService, ModernHttpService>();
            }

            // Konfiguracja Graph API - Singleton dla wydajności
            services.AddSingleton<GraphApiConfiguration>();

            // Enhanced Token Manager - Scoped dla lepszego zarządzania zasobami
            services.AddScoped<IGraphTokenManager, GraphTokenManager>();

            // Core Graph API services - Scoped dla lepszego zarządzania zasobami
            services.AddScoped<IGraphConnectionService, GraphConnectionService>();
            services.AddScoped<IGraphCacheService, GraphCacheService>();

            // Domain Graph API services - pozostają Scoped
            services.AddScoped<IGraphTeamManagementService, GraphTeamManagementService>();
            services.AddScoped<IGraphUserManagementService, GraphUserManagementService>();
            services.AddScoped<IGraphBulkOperationsService, GraphBulkOperationsService>();

            // Main Graph API Facade - pozostaje Scoped
            services.AddScoped<IGraphService, GraphService>();

            // Optional Graph-based notification service
            if (includeAdminNotificationService && !IsServiceRegistered<IAdminNotificationService>(services))
            {
                services.AddScoped<IAdminNotificationService, GraphAdminNotificationService>();
            }

            return services;
        }

        /// <summary>
        /// Sprawdza czy serwis jest już zarejestrowany w kontenerze DI
        /// </summary>
        /// <typeparam name="T">Typ serwisu do sprawdzenia</typeparam>
        /// <param name="services">Kolekcja serwisów</param>
        /// <returns>True jeśli serwis jest już zarejestrowany</returns>
        private static bool IsServiceRegistered<T>(IServiceCollection services)
        {
            return services.Any(x => x.ServiceType == typeof(T));
        }
    }
} 