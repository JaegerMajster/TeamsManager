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
            _connectionService = connectionService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Rozpoczęto sprawdzanie stanu połączenia PowerShell");

                // Sprawdź czy połączenie jest aktywne
                var isConnected = _connectionService.IsConnected;

                if (!isConnected)
                {
                    _logger.LogWarning("PowerShell nie jest połączony z Microsoft Graph");
                    return HealthCheckResult.Unhealthy(
                        "PowerShell connection is not active",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = false,
                            ["timestamp"] = DateTime.UtcNow
                        });
                }

                // Wykonaj test połączenia używając GetConnectionHealthAsync
                var healthInfo = await _connectionService.GetConnectionHealthAsync();

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