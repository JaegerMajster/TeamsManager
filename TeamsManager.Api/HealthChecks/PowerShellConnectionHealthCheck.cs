using Microsoft.Extensions.Diagnostics.HealthChecks;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Api.HealthChecks
{
    /// <summary>
    /// Health check dla weryfikacji stanu połączenia z Microsoft Graph przez PowerShell
    /// </summary>
    public class PowerShellConnectionHealthCheck : IHealthCheck
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly ILogger<PowerShellConnectionHealthCheck> _logger;

        public PowerShellConnectionHealthCheck(
            IPowerShellConnectionService connectionService,
            ILogger<PowerShellConnectionHealthCheck> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                _logger.LogInformation("Rozpoczęto sprawdzanie stanu połączenia PowerShell");

                // Wykonaj test połączenia używając GetConnectionHealthAsync
                var healthInfo = await _connectionService.GetConnectionHealthAsync();
                
                if (healthInfo == null)
                {
                    _logger.LogError("GetConnectionHealthAsync zwróciło null");
                    return HealthCheckResult.Unhealthy(
                        "Unable to retrieve connection health information",
                        data: new Dictionary<string, object>
                        {
                            ["error"] = "Health info is null",
                            ["timestamp"] = DateTime.UtcNow
                        });
                }

                if (healthInfo.IsConnected && healthInfo.TokenValid)
                {
                    _logger.LogInformation("Test połączenia PowerShell zakończony sukcesem");
                    return HealthCheckResult.Healthy(
                        "PowerShell connection is active and functional",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = true,
                            ["tokenValid"] = healthInfo.TokenValid,
                            ["runspaceState"] = healthInfo.RunspaceState,
                            ["circuitBreakerState"] = healthInfo.CircuitBreakerState,
                            ["lastSuccessfulConnection"] = healthInfo.LastSuccessfulConnection,
                            ["timestamp"] = DateTime.UtcNow
                        });
                }
                else
                {
                    _logger.LogWarning("Test połączenia PowerShell nie powiódł się. Connected: {Connected}, TokenValid: {TokenValid}", 
                        healthInfo.IsConnected, healthInfo.TokenValid);
                    return HealthCheckResult.Degraded(
                        $"PowerShell connection test failed. Connected: {healthInfo.IsConnected}, TokenValid: {healthInfo.TokenValid}",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = healthInfo.IsConnected,
                            ["tokenValid"] = healthInfo.TokenValid,
                            ["runspaceState"] = healthInfo.RunspaceState,
                            ["circuitBreakerState"] = healthInfo.CircuitBreakerState,
                            ["lastConnectionAttempt"] = healthInfo.LastConnectionAttempt,
                            ["timestamp"] = DateTime.UtcNow
                        });
                }
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania stanu połączenia PowerShell");
                return HealthCheckResult.Unhealthy(
                    "PowerShell health check failed",
                    exception: ex,
                    data: new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["timestamp"] = DateTime.UtcNow
                    });
            }
        }
    }
} 