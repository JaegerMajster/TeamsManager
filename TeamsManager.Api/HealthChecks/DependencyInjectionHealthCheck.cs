using Microsoft.Extensions.Diagnostics.HealthChecks;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;

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
                typeof(IPowerShellBulkOperationsService),
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
                        errors.Add($"Service {serviceType.Name} is not registered");
                        _logger.LogError("Service {ServiceType} is not registered in DI container", serviceType.Name);
                    }
                    else
                    {
                        _logger.LogDebug("Service {ServiceType} successfully resolved", serviceType.Name);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error resolving {serviceType.Name}: {ex.Message}");
                    _logger.LogError(ex, "Error resolving service {ServiceType}", serviceType.Name);
                }
            }

            if (errors.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "DI configuration issues detected",
                    data: new Dictionary<string, object> { ["errors"] = errors }
                ));
            }

            _logger.LogInformation("All critical services are properly registered in DI container");
            return Task.FromResult(HealthCheckResult.Healthy("All critical services are properly registered"));
        }
    }
} 