using Microsoft.Extensions.Diagnostics.HealthChecks;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.Api.HealthChecks
{
    /// <summary>
    /// Health check do weryfikacji poprawności konfiguracji Dependency Injection
    /// </summary>
    public class DependencyInjectionHealthCheck : IHealthCheck
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DependencyInjectionHealthCheck> _logger;

        public DependencyInjectionHealthCheck(
            IServiceProvider serviceProvider,
            ILogger<DependencyInjectionHealthCheck> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var criticalServices = new[]
            {
                typeof(IOperationHistoryService),
                typeof(INotificationService),
                typeof(ICurrentUserService),
                typeof(ITeamService),
                typeof(IUserService),
                typeof(IDepartmentService),
                typeof(IChannelService),
                typeof(ISubjectService),
                typeof(IApplicationSettingService),
                typeof(ISchoolTypeService),
                typeof(ISchoolYearService),
                typeof(ITeamTemplateService),
                typeof(ISchoolYearProcessOrchestrator),
                typeof(IDataImportOrchestrator),
                typeof(ITeamLifecycleOrchestrator),
                typeof(IBulkUserManagementOrchestrator),
                typeof(IHealthMonitoringOrchestrator),
                typeof(IReportingOrchestrator)
            };

            var errors = new List<string>();

            foreach (var serviceType in criticalServices)
            {
                try
                {
                    var service = _serviceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        errors.Add($"Serwis {serviceType.Name} nie jest zarejestrowany");
                        _logger.LogError("Serwis {ServiceType} nie jest zarejestrowany w kontenerze DI", serviceType.Name);
                    }
                    else
                    {
                        _logger.LogDebug("Serwis {ServiceType} pomyślnie rozwiązany", serviceType.Name);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Błąd podczas rozwiązywania {serviceType.Name}: {ex.Message}");
                    _logger.LogError(ex, "Błąd podczas rozwiązywania serwisu {ServiceType}", serviceType.Name);
                }
            }

            if (errors.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "Wykryto problemy z konfiguracją DI",
                    data: new Dictionary<string, object> { ["errors"] = errors }
                ));
            }

            _logger.LogInformation("Wszystkie krytyczne serwisy są poprawnie zarejestrowane w kontenerze DI");
            return Task.FromResult(HealthCheckResult.Healthy("Wszystkie krytyczne serwisy są poprawnie zarejestrowane"));
        }
    }
} 