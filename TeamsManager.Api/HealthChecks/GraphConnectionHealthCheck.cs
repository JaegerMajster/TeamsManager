using Microsoft.Extensions.Diagnostics.HealthChecks;
using TeamsManager.Core.Abstractions.Services.Graph;

namespace TeamsManager.Api.HealthChecks
{
    /// <summary>
    /// Health check dla weryfikacji stanu połączenia z Microsoft Graph API
    /// Health check dla połączenia Graph API
    /// </summary>
    public class GraphConnectionHealthCheck : IHealthCheck
    {
        private readonly IGraphConnectionService _connectionService;
        private readonly ILogger<GraphConnectionHealthCheck> _logger;

        public GraphConnectionHealthCheck(
            IGraphConnectionService connectionService,
            ILogger<GraphConnectionHealthCheck> logger)
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
                
                _logger.LogInformation("Rozpoczęto sprawdzanie stanu połączenia Graph API");

                var diagnosticInfo = await _connectionService.DiagnoseConnectionAsync();
                
                if (diagnosticInfo == null)
                {
                    _logger.LogError("DiagnoseConnectionAsync zwróciło null");
                    return HealthCheckResult.Unhealthy(
                        "Nie można pobrać diagnostyki połączenia Graph API",
                        data: new Dictionary<string, object>
                        {
                            ["error"] = "Informacje diagnostyczne są null",
                            ["timestamp"] = DateTime.UtcNow
                        });
                }

                if (diagnosticInfo.IsConnected && diagnosticInfo.IsAuthenticated && diagnosticInfo.HasRequiredPermissions)
                {
                    _logger.LogInformation("Test połączenia Graph API zakończony sukcesem");
                    return HealthCheckResult.Healthy(
                        "Połączenie Graph API jest aktywne i funkcjonalne",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = true,
                            ["authenticated"] = diagnosticInfo.IsAuthenticated,
                            ["hasPermissions"] = diagnosticInfo.HasRequiredPermissions,
                            ["isConnected"] = diagnosticInfo.IsConnected,
                            ["status"] = diagnosticInfo.Status.ToString(),
                            ["lastChecked"] = diagnosticInfo.LastChecked,
                            ["timestamp"] = DateTime.UtcNow
                        });
                }
                else
                {
                    _logger.LogWarning("Test połączenia Graph API nie powiódł się. Connected: {Connected}, Authenticated: {Authenticated}, HasPermissions: {HasPermissions}", 
                        diagnosticInfo.IsConnected, diagnosticInfo.IsAuthenticated, diagnosticInfo.HasRequiredPermissions);
                    return HealthCheckResult.Degraded(
                        $"Test połączenia Graph API nie powiódł się. Connected: {diagnosticInfo.IsConnected}, Authenticated: {diagnosticInfo.IsAuthenticated}, HasPermissions: {diagnosticInfo.HasRequiredPermissions}",
                        data: new Dictionary<string, object>
                        {
                            ["connected"] = diagnosticInfo.IsConnected,
                            ["authenticated"] = diagnosticInfo.IsAuthenticated,
                            ["hasPermissions"] = diagnosticInfo.HasRequiredPermissions,
                            ["isConnected"] = diagnosticInfo.IsConnected,
                            ["status"] = diagnosticInfo.Status.ToString(),
                            ["errorCount"] = diagnosticInfo.Errors.Count,
                            ["timestamp"] = DateTime.UtcNow
                        });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania stanu połączenia Graph API");
                return HealthCheckResult.Unhealthy(
                    "Graph API health check failed",
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